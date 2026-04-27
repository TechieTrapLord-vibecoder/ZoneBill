using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    /// <summary>
    /// Public controller — no [Authorize]. Serves the QR-based customer portal
    /// so walk-in customers can view their session and order food/drinks.
    /// </summary>
    public class CustomerController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public CustomerController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        // GET /Customer/Space/{spaceId}
        [HttpGet("Customer/Space/{spaceId:int}")]
        public async Task<IActionResult> Space(int spaceId)
        {
            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);

            if (space == null) return NotFound();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            var model = new CustomerPortalViewModel
            {
                SpaceId = space.SpaceId,
                SpaceName = space.SpaceName,
                FloorArea = space.FloorArea,
                BusinessName = space.Business.BusinessName,
                HourlyRate = space.CurrentHourlyRate,
                HasActiveSession = booking != null
            };

            if (booking != null)
            {
                var now = PhilippineTime.Now;
                var elapsed = Math.Round((decimal)(now - booking.StartTime).TotalHours, 2);
                if (elapsed < 0.01m) elapsed = 0.01m;
                var timeCharge = Math.Round(elapsed * booking.LockedHourlyRate, 2);

                var orderItems = await _context.OrderDetails
                    .Include(od => od.Order)
                    .Include(od => od.MenuItem)
                    .Where(od => od.Order.BookingId == booking.BookingId
                        && od.Order.BusinessId == space.BusinessId)
                    .Select(od => new CustomerOrderItemViewModel
                    {
                        ItemName = od.MenuItem.ItemName,
                        Quantity = od.Quantity,
                        UnitPrice = od.LockedUnitPrice,
                        LineTotal = od.Quantity * od.LockedUnitPrice
                    })
                    .ToListAsync();

                var menuTotal = orderItems.Sum(o => o.LineTotal);
                var subTotal = timeCharge + menuTotal;
                var taxRate = space.Business.TaxRatePercentage / 100m;
                var taxAmount = Math.Round(subTotal * taxRate, 2);

                model.BookingId = booking.BookingId;
                model.ReferenceCode = booking.ReferenceCode;
                model.StartTime = booking.StartTime;
                model.ElapsedHours = elapsed;
                model.TimeCharge = timeCharge;
                model.OrderItems = orderItems;
                model.MenuTotal = menuTotal;
                model.TaxRatePercent = space.Business.TaxRatePercentage;
                model.TaxAmount = taxAmount;
                model.EstimatedTotal = Math.Round(subTotal + taxAmount, 2);
                model.CheckoutRequested = booking.CheckoutRequested;
                model.CustomerEmail = booking.CustomerEmail;

                model.MenuItems = await _context.MenuItems
                    .Where(m => m.BusinessId == space.BusinessId && m.IsActive && m.StockAvailable > 0)
                    .OrderBy(m => m.ItemName)
                    .Select(m => new CustomerMenuItemViewModel
                    {
                        ItemId = m.ItemId,
                        ItemName = m.ItemName,
                        Price = m.CurrentPrice,
                        StockAvailable = m.StockAvailable
                    })
                    .ToListAsync();
            }

            return View(model);
        }

        // POST /Customer/Space/{spaceId}/Order
        [HttpPost("Customer/Space/{spaceId:int}/Order")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Order(int spaceId, CustomerOrderRequest request)
        {
            if (request.SpaceId != spaceId)
                return BadRequest();

            var space = await _context.Spaces
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            if (booking == null)
            {
                TempData["Error"] = "No active session for this table. Please ask staff to start a session.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            if (request.Quantity <= 0)
            {
                TempData["Error"] = "Quantity must be at least 1.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(m => m.ItemId == request.ItemId
                    && m.BusinessId == space.BusinessId
                    && m.IsActive);

            if (menuItem == null)
            {
                TempData["Error"] = "Menu item not found.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            if (menuItem.StockAvailable < request.Quantity)
            {
                TempData["Error"] = $"Only {menuItem.StockAvailable} left in stock for {menuItem.ItemName}.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            // Find or create the order for this booking
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.BookingId == booking.BookingId
                    && o.BusinessId == space.BusinessId);

            if (order == null)
            {
                // Need a CashierId — use the first active cashier/manager for this business
                var fallbackCashierId = await _context.Users
                    .Where(u => u.BusinessId == space.BusinessId
                        && u.IsActive
                        && (u.UserRole == "Cashier" || u.UserRole == "Manager" || u.UserRole == "MainAdmin"))
                    .Select(u => u.UserId)
                    .FirstOrDefaultAsync();

                if (fallbackCashierId == 0)
                {
                    TempData["Error"] = "Unable to process order. Please ask staff for assistance.";
                    return RedirectToAction(nameof(Space), new { spaceId });
                }

                order = new Order
                {
                    BusinessId = space.BusinessId,
                    BookingId = booking.BookingId,
                    CashierId = fallbackCashierId,
                    OrderTime = PhilippineTime.Now
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            }

            _context.OrderDetails.Add(new OrderDetail
            {
                OrderId = order.OrderId,
                ItemId = menuItem.ItemId,
                Quantity = request.Quantity,
                LockedUnitPrice = menuItem.CurrentPrice
            });

            menuItem.StockAvailable -= request.Quantity;
            await _context.SaveChangesAsync();

            if (menuItem.StockAvailable <= 0)
            {
                var mainAdmin = await _context.Users
                    .FirstOrDefaultAsync(u => u.BusinessId == space.BusinessId && u.UserRole == "MainAdmin" && u.IsActive);
                if (mainAdmin != null)
                {
                    _ = _emailService.SendLowStockAlertAsync(
                        mainAdmin.EmailAddress,
                        $"{mainAdmin.FirstName} {mainAdmin.LastName}",
                        menuItem.ItemName,
                        space.Business.BusinessName);
                }
            }

            TempData["Success"] = $"{request.Quantity}× {menuItem.ItemName} added to your order!";
            return RedirectToAction(nameof(Space), new { spaceId });
        }

        // POST /Customer/Space/{spaceId}/BatchOrder  (JSON)
        [HttpPost("Customer/Space/{spaceId:int}/BatchOrder")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchOrder(int spaceId, [FromBody] List<CustomerOrderRequest> items)
        {
            if (items == null || items.Count == 0)
                return BadRequest(new { error = "No items in order." });

            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            if (booking == null)
                return BadRequest(new { error = "No active session for this table." });

            // Find or create order
            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.BookingId == booking.BookingId && o.BusinessId == space.BusinessId);

            if (order == null)
            {
                var fallbackCashierId = await _context.Users
                    .Where(u => u.BusinessId == space.BusinessId && u.IsActive
                        && (u.UserRole == "Cashier" || u.UserRole == "Manager" || u.UserRole == "MainAdmin"))
                    .Select(u => u.UserId)
                    .FirstOrDefaultAsync();

                if (fallbackCashierId == 0)
                    return BadRequest(new { error = "Unable to process order. Please ask staff for assistance." });

                order = new Order
                {
                    BusinessId = space.BusinessId,
                    BookingId = booking.BookingId,
                    CashierId = fallbackCashierId,
                    OrderTime = PhilippineTime.Now
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            }

            var errors = new List<string>();
            var added = new List<string>();

            foreach (var item in items)
            {
                if (item.Quantity <= 0) continue;

                var menuItem = await _context.MenuItems
                    .FirstOrDefaultAsync(m => m.ItemId == item.ItemId
                        && m.BusinessId == space.BusinessId && m.IsActive);

                if (menuItem == null) { errors.Add($"Item #{item.ItemId} not found."); continue; }
                if (menuItem.StockAvailable < item.Quantity)
                {
                    errors.Add($"Only {menuItem.StockAvailable} left for {menuItem.ItemName}.");
                    continue;
                }

                _context.OrderDetails.Add(new OrderDetail
                {
                    OrderId = order.OrderId,
                    ItemId = menuItem.ItemId,
                    Quantity = item.Quantity,
                    LockedUnitPrice = menuItem.CurrentPrice
                });

                menuItem.StockAvailable -= item.Quantity;
                added.Add($"{item.Quantity}× {menuItem.ItemName}");
            }

            await _context.SaveChangesAsync();

            if (added.Count == 0)
                return BadRequest(new { error = string.Join(" ", errors) });

            return Ok(new { success = true, message = string.Join(", ", added) + " added to your order!", errors });
        }

        // POST /Customer/Space/{spaceId}/RequestCheckout
        [HttpPost("Customer/Space/{spaceId:int}/RequestCheckout")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RequestCheckout(int spaceId, [FromForm] int? splitCount)
        {
            var space = await _context.Spaces
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            if (booking == null)
            {
                TempData["Error"] = "No active session for this table.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            booking.CheckoutRequested = true;
            booking.RequestedSplitCount = (splitCount.HasValue && splitCount.Value >= 2) ? splitCount.Value : null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Checkout requested! The front desk has been notified.";
            return RedirectToAction(nameof(Space), new { spaceId });
        }

        // GET /Customer/Space/{spaceId}/Status — JSON polling endpoint
        [HttpGet("Customer/Space/{spaceId:int}/Status")]
        public async Task<IActionResult> Status(int spaceId, [FromQuery] int? bookingId)
        {
            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            // If client tracks a specific bookingId, check that booking directly
            if (bookingId.HasValue)
            {
                var tracked = await _context.Bookings
                    .FirstOrDefaultAsync(b => b.BookingId == bookingId.Value
                        && b.SpaceId == spaceId
                        && b.BusinessId == space.BusinessId);

                if (tracked != null && tracked.BookingStatus == "Completed")
                    return Json(new { bookingStatus = "Completed" });
            }

            // Find the currently active booking for this space
            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            if (booking == null)
                return Json(new { bookingStatus = (string?)null });

            var now = PhilippineTime.Now;
            var elapsed = Math.Round((decimal)(now - booking.StartTime).TotalHours, 2);
            if (elapsed < 0.01m) elapsed = 0.01m;
            var timeCharge = Math.Round(elapsed * booking.LockedHourlyRate, 2);

            var orderItems = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .Where(od => od.Order.BookingId == booking.BookingId
                    && od.Order.BusinessId == space.BusinessId)
                .Select(od => new
                {
                    itemName = od.MenuItem.ItemName,
                    quantity = od.Quantity,
                    unitPrice = od.LockedUnitPrice,
                    lineTotal = od.Quantity * od.LockedUnitPrice
                })
                .ToListAsync();

            var menuTotal = orderItems.Sum(o => o.lineTotal);
            var subTotal = timeCharge + menuTotal;
            var taxRate = space.Business.TaxRatePercentage / 100m;
            var taxAmount = Math.Round(subTotal * taxRate, 2);

            return Json(new
            {
                bookingStatus = "Active",
                checkoutRequested = booking.CheckoutRequested,
                orderItems,
                menuTotal,
                taxAmount,
                estimatedTotal = Math.Round(subTotal + taxAmount, 2),
                elapsedHours = elapsed,
                timeCharge
            });
        }

        // POST /Customer/Space/{spaceId}/SaveEmail
        [HttpPost("Customer/Space/{spaceId:int}/SaveEmail")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveEmail(int spaceId, [FromForm] string email)
        {
            if (string.IsNullOrWhiteSpace(email) || !new EmailAddressAttribute().IsValid(email))
            {
                TempData["Error"] = "Please enter a valid email address.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            var space = await _context.Spaces
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Active");

            if (booking == null)
            {
                TempData["Error"] = "No active session for this table.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            booking.CustomerEmail = email.Trim();
            await _context.SaveChangesAsync();

            TempData["Success"] = "Email saved! You'll receive your receipt when checkout completes.";
            return RedirectToAction(nameof(Space), new { spaceId });
        }

        // GET /Customer/Space/{spaceId}/Receipt
        [HttpGet("Customer/Space/{spaceId:int}/Receipt")]
        public async Task<IActionResult> Receipt(int spaceId)
        {
            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(s => s.SpaceId == spaceId && s.IsActive);
            if (space == null) return NotFound();

            // Find the most recently completed booking for this space
            var booking = await _context.Bookings
                .Where(b => b.SpaceId == spaceId
                    && b.BusinessId == space.BusinessId
                    && b.BookingStatus == "Completed")
                .OrderByDescending(b => b.EndTime)
                .FirstOrDefaultAsync();

            if (booking == null)
            {
                TempData["Error"] = "No completed session found.";
                return RedirectToAction(nameof(Space), new { spaceId });
            }

            // Compute time charge from actual Duration
            var duration = booking.DurationHours ?? 0m;
            if (duration < 0.01m) duration = 0.01m;
            var timeCharge = Math.Round(duration * booking.LockedHourlyRate, 2);

            var orderItems = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .Where(od => od.Order.BookingId == booking.BookingId
                    && od.Order.BusinessId == space.BusinessId)
                .Select(od => new CustomerOrderItemViewModel
                {
                    ItemName = od.MenuItem.ItemName,
                    Quantity = od.Quantity,
                    UnitPrice = od.LockedUnitPrice,
                    LineTotal = od.Quantity * od.LockedUnitPrice
                })
                .ToListAsync();

            var menuTotal = orderItems.Sum(o => o.LineTotal);
            var subTotal = timeCharge + menuTotal;
            var taxRate = space.Business.TaxRatePercentage / 100m;
            var taxAmount = Math.Round(subTotal * taxRate, 2);
            var totalAmount = Math.Round(subTotal + taxAmount, 2);

            // Get payment info from the invoice
            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.BookingId == booking.BookingId
                    && i.BusinessId == space.BusinessId);

            decimal paidAmount = 0;
            string paymentMethod = "—";
            if (invoice != null)
            {
                var payment = await _context.Payments
                    .Where(p => p.InvoiceId == invoice.InvoiceId)
                    .OrderByDescending(p => p.PaymentDate)
                    .FirstOrDefaultAsync();

                if (payment != null)
                {
                    paidAmount = payment.AmountPaid;
                    paymentMethod = payment.PaymentMethod;
                }
            }

            var model = new CustomerReceiptViewModel
            {
                SpaceName = space.SpaceName,
                BusinessName = space.Business.BusinessName,
                ReferenceCode = booking.ReferenceCode,
                StartTime = booking.StartTime,
                EndTime = booking.EndTime,
                DurationHours = booking.DurationHours,
                OrderItems = orderItems,
                TimeCharge = timeCharge,
                MenuTotal = menuTotal,
                TaxRatePercent = space.Business.TaxRatePercentage,
                TaxAmount = taxAmount,
                TotalAmount = totalAmount,
                PaidAmount = paidAmount,
                PaymentMethod = paymentMethod,
                CustomerEmail = booking.CustomerEmail
            };

            // Send receipt email once if customer provided email
            if (!string.IsNullOrWhiteSpace(booking.CustomerEmail) && !booking.CustomerReceiptEmailSent)
            {
                var emailItems = orderItems
                    .Select(o => (o.ItemName, o.Quantity, o.LineTotal))
                    .ToList();

                _ = _emailService.SendCustomerReceiptAsync(
                    booking.CustomerEmail,
                    space.Business.BusinessName,
                    space.SpaceName,
                    booking.ReferenceCode ?? "",
                    timeCharge, menuTotal, taxAmount, totalAmount,
                    emailItems);

                booking.CustomerReceiptEmailSent = true;
                await _context.SaveChangesAsync();
                model.ReceiptEmailSent = true;
            }

            return View(model);
        }
    }
}
