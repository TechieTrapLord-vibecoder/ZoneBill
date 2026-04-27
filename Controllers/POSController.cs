using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Filters;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin,Manager,Cashier")]
    [ServiceFilter(typeof(ActiveSubscriptionFilter))]
    public class POSController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IEmailService _emailService;

        public POSController(ApplicationDbContext context, IEmailService emailService)
        {
            _context = context;
            _emailService = emailService;
        }

        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var spaces = await _context.Spaces
                .Where(s => s.BusinessId == businessId.Value && s.IsActive)
                .OrderBy(s => s.SpaceName)
                .ToListAsync();

            var activeBookings = await _context.Bookings
                .Where(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active")
                .ToListAsync();

            var activeBookingIds = activeBookings.Select(b => b.BookingId).ToList();
            var orderItemCounts = await _context.OrderDetails
                .Where(od => activeBookingIds.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                .GroupBy(od => od.Order.BookingId)
                .Select(g => new { BookingId = g.Key, Count = g.Sum(od => od.Quantity) })
                .ToDictionaryAsync(x => x.BookingId, x => x.Count);

            var orderTotals = activeBookingIds.Count > 0
                ? await _context.OrderDetails
                    .Where(od => activeBookingIds.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                    .GroupBy(od => od.Order.BookingId)
                    .Select(g => new { BookingId = g.Key, Total = g.Sum(od => od.Quantity * od.LockedUnitPrice) })
                    .ToDictionaryAsync(x => x.BookingId, x => x.Total)
                : new Dictionary<int, decimal>();

            var business = await _context.Businesses.FirstAsync(b => b.BusinessId == businessId.Value);

            var model = new PosDashboardViewModel
            {
                Spaces = spaces.Select(space =>
                {
                    var activeBooking = activeBookings.FirstOrDefault(b => b.SpaceId == space.SpaceId);
                    return new PosSpaceCardViewModel
                    {
                        SpaceId = space.SpaceId,
                        SpaceName = space.SpaceName,
                        FloorArea = space.FloorArea,
                        Capacity = space.Capacity,
                        HourlyRate = space.CurrentHourlyRate,
                        Status = activeBooking == null ? "Available" : "Occupied",
                        ActiveBookingId = activeBooking?.BookingId,
                        ActiveStartTime = activeBooking?.StartTime,
                        OrderItemCount = activeBooking != null && orderItemCounts.TryGetValue(activeBooking.BookingId, out var cnt) ? cnt : 0,
                        CheckoutRequested = activeBooking?.CheckoutRequested ?? false,
                        RequestedSplitCount = activeBooking?.RequestedSplitCount,
                        MenuTotal = activeBooking != null && orderTotals.TryGetValue(activeBooking.BookingId, out var mt) ? mt : 0,
                        TaxRatePercentage = business.TaxRatePercentage,
                        LockedHourlyRate = activeBooking?.LockedHourlyRate ?? space.CurrentHourlyRate,
                        ReferenceCode = activeBooking?.ReferenceCode
                    };
                }).ToList(),
                Customers = await _context.Customers
                    .Where(c => c.BusinessId == businessId.Value)
                    .OrderBy(c => c.Name)
                    .ToListAsync(),
                MenuItems = await _context.MenuItems
                    .Where(m => m.BusinessId == businessId.Value && m.IsActive)
                    .OrderBy(m => m.ItemName)
                    .ToListAsync()
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> GetSpaceStatus()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var spaces = await _context.Spaces
                .Where(s => s.BusinessId == businessId.Value && s.IsActive)
                .OrderBy(s => s.SpaceName)
                .ToListAsync();

            var activeBookings = await _context.Bookings
                .Where(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active")
                .ToListAsync();

            var activeBookingIds = activeBookings.Select(b => b.BookingId).ToList();
            var orderItemCounts = await _context.OrderDetails
                .Where(od => activeBookingIds.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                .GroupBy(od => od.Order.BookingId)
                .Select(g => new { BookingId = g.Key, Count = g.Sum(od => od.Quantity) })
                .ToDictionaryAsync(x => x.BookingId, x => x.Count);

            var result = spaces.Select(space =>
            {
                var activeBooking = activeBookings.FirstOrDefault(b => b.SpaceId == space.SpaceId);
                var count = activeBooking != null && orderItemCounts.TryGetValue(activeBooking.BookingId, out var cnt) ? cnt : 0;
                return new
                {
                    spaceId = space.SpaceId,
                    status = activeBooking == null ? "Available" : "Occupied",
                    activeBookingId = activeBooking?.BookingId,
                    startTime = activeBooking?.StartTime.ToString("o"),
                    orderItemCount = count,
                    checkoutRequested = activeBooking?.CheckoutRequested ?? false
                };
            });

            return Json(result);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> StartSession(StartSessionRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!await HasOpenShiftAsync(businessId.Value))
            {
                TempData["Error"] = "You must open a shift before starting a session. Go to Shift & Cash Drawer.";
                return RedirectToAction(nameof(Index));
            }

            var space = await _context.Spaces
                .FirstOrDefaultAsync(s => s.SpaceId == request.SpaceId && s.BusinessId == businessId.Value);
            if (space == null) return NotFound();

            var hasActiveBooking = await _context.Bookings.AnyAsync(b =>
                b.BusinessId == businessId.Value &&
                b.SpaceId == request.SpaceId &&
                b.BookingStatus == "Active");

            if (hasActiveBooking)
            {
                TempData["Error"] = "This table already has an active session.";
                return RedirectToAction(nameof(Index));
            }

            var booking = new Booking
            {
                BusinessId = businessId.Value,
                SpaceId = request.SpaceId,
                CustomerId = request.CustomerId,
                StartTime = PhilippineTime.Now,
                EndTime = null,
                DurationHours = null,
                LockedHourlyRate = space.CurrentHourlyRate,
                BookingStatus = "Active",
                ReferenceCode = $"BK-{PhilippineTime.Now:yyyyMMddHHmmss}-{request.SpaceId}"
            };

            space.CurrentStatus = "Occupied";
            _context.Bookings.Add(booking);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddOrder(AddOrderRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!await HasOpenShiftAsync(businessId.Value))
            {
                TempData["Error"] = "You must have an open shift to add orders.";
                return RedirectToAction(nameof(Index));
            }

            if (request.Quantity <= 0)
            {
                TempData["Error"] = "Quantity must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            var booking = await _context.Bookings.FirstOrDefaultAsync(b =>
                b.BookingId == request.BookingId &&
                b.BusinessId == businessId.Value &&
                b.BookingStatus == "Active");
            if (booking == null) return NotFound();

            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m =>
                m.ItemId == request.ItemId &&
                m.BusinessId == businessId.Value &&
                m.IsActive);
            if (menuItem == null) return NotFound();

            if (menuItem.StockAvailable < request.Quantity)
            {
                TempData["Error"] = $"Only {menuItem.StockAvailable} stock left for {menuItem.ItemName}.";
                return RedirectToAction(nameof(Index));
            }

            var cashierId = GetUserId();
            if (cashierId == null)
            {
                TempData["Error"] = "Unable to determine current user for order.";
                return RedirectToAction(nameof(Index));
            }

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.BookingId == booking.BookingId && o.BusinessId == businessId.Value);

            if (order == null)
            {
                order = new Order
                {
                    BusinessId = businessId.Value,
                    BookingId = booking.BookingId,
                    CashierId = cashierId.Value,
                    OrderTime = PhilippineTime.Now
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            }

            var detail = new OrderDetail
            {
                OrderId = order.OrderId,
                ItemId = menuItem.ItemId,
                Quantity = request.Quantity,
                LockedUnitPrice = menuItem.CurrentPrice
            };

            _context.OrderDetails.Add(detail);
            menuItem.StockAvailable -= request.Quantity;
            await _context.SaveChangesAsync();

            if (menuItem.StockAvailable <= 0)
            {
                var mainAdmin = await _context.Users
                    .FirstOrDefaultAsync(u => u.BusinessId == businessId.Value && u.UserRole == "MainAdmin" && u.IsActive);
                if (mainAdmin != null)
                {
                    var business = await _context.Businesses.FindAsync(businessId.Value);
                    _ = _emailService.SendLowStockAlertAsync(
                        mainAdmin.EmailAddress,
                        $"{mainAdmin.FirstName} {mainAdmin.LastName}",
                        menuItem.ItemName,
                        business?.BusinessName ?? "Your Business");
                }
            }

            return RedirectToAction(nameof(Index));
        }

        // POST /POS/BatchAddOrder  (JSON — used by the POS card cart)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchAddOrder([FromBody] PosBatchOrderRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!await HasOpenShiftAsync(businessId.Value))
                return BadRequest(new { error = "You must have an open shift to add orders." });

            if (request.Items == null || request.Items.Count == 0)
                return BadRequest(new { error = "No items provided." });

            var booking = await _context.Bookings.FirstOrDefaultAsync(b =>
                b.BookingId == request.BookingId &&
                b.BusinessId == businessId.Value &&
                b.BookingStatus == "Active");
            if (booking == null) return NotFound();

            var cashierId = GetUserId();
            if (cashierId == null) return BadRequest(new { error = "Unable to determine current user." });

            var order = await _context.Orders
                .FirstOrDefaultAsync(o => o.BookingId == booking.BookingId && o.BusinessId == businessId.Value);
            if (order == null)
            {
                order = new Order
                {
                    BusinessId = businessId.Value,
                    BookingId = booking.BookingId,
                    CashierId = cashierId.Value,
                    OrderTime = PhilippineTime.Now
                };
                _context.Orders.Add(order);
                await _context.SaveChangesAsync();
            }

            var added = new List<string>();
            var errors = new List<string>();

            foreach (var item in request.Items)
            {
                if (item.Quantity <= 0) continue;

                var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m =>
                    m.ItemId == item.ItemId && m.BusinessId == businessId.Value && m.IsActive);
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

                if (menuItem.StockAvailable <= 0)
                {
                    var mainAdmin = await _context.Users
                        .FirstOrDefaultAsync(u => u.BusinessId == businessId.Value && u.UserRole == "MainAdmin" && u.IsActive);
                    if (mainAdmin != null)
                    {
                        var business = await _context.Businesses.FindAsync(businessId.Value);
                        _ = _emailService.SendLowStockAlertAsync(
                            mainAdmin.EmailAddress,
                            $"{mainAdmin.FirstName} {mainAdmin.LastName}",
                            menuItem.ItemName,
                            business?.BusinessName ?? "Your Business");
                    }
                }
            }

            if (added.Count == 0)
                return BadRequest(new { error = string.Join(" ", errors) });

            await _context.SaveChangesAsync();
            return Ok(new { success = true, message = string.Join(", ", added) + " added.", errors });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Checkout(CheckoutRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!await HasOpenShiftAsync(businessId.Value))
            {
                TempData["Error"] = "You must have an open shift to process a checkout.";
                return RedirectToAction(nameof(Index));
            }

            var discountPercentage = Math.Round(request.DiscountPercentage, 2);
            if (discountPercentage < 0m || discountPercentage > 100m)
            {
                TempData["Error"] = "Discount must be between 0% and 100%.";
                return RedirectToAction(nameof(Index));
            }

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Space)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId && b.BusinessId == businessId.Value && b.BookingStatus == "Active");

            if (booking == null) return NotFound();

            var endTime = PhilippineTime.Now;
            var durationHours = Math.Round((decimal)(endTime - booking.StartTime).TotalHours, 2);
            if (durationHours < 0.01m) durationHours = 0.01m;

            var tableCharge = Math.Round(durationHours * booking.LockedHourlyRate, 2);

            var orderDetails = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .Where(od => od.Order.BusinessId == businessId.Value && od.Order.BookingId == booking.BookingId)
                .ToListAsync();

            var menuCharge = orderDetails.Sum(od => od.Quantity * od.LockedUnitPrice);
            var subTotal = Math.Round(tableCharge + menuCharge, 2);
            var discountAmount = Math.Round(subTotal * (discountPercentage / 100m), 2);
            var taxableBase = Math.Max(0m, subTotal - discountAmount);
            var taxRateApplied = Math.Round(business.TaxRatePercentage / 100m, 4);
            var taxAmount = Math.Round(taxableBase * taxRateApplied, 2);
            var total = Math.Round(taxableBase + taxAmount, 2);

            var invoice = new Invoice
            {
                BusinessId = businessId.Value,
                BookingId = booking.BookingId,
                SubTotal = subTotal,
                DiscountAmount = discountAmount,
                TaxAmount = taxAmount,
                TotalAmount = total,
                TaxRateApplied = taxRateApplied,
                PaymentStatus = "Unpaid",
                GeneratedDate = PhilippineTime.Now
            };

            _context.Invoices.Add(invoice);
            await _context.SaveChangesAsync();

            var accountsReceivable = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");
            var salesRevenue = await GetOrCreateAccountAsync(businessId.Value, "Sales Revenue", "Revenue");
            var taxPayable = taxAmount > 0m
                ? await GetOrCreateAccountAsync(businessId.Value, "Output Tax Payable", "Liability")
                : null;

            var journalEntry = new JournalEntry
            {
                BusinessId = businessId.Value,
                ReferenceId = invoice.InvoiceId,
                ReferenceType = "Invoice",
                EntryDate = PhilippineTime.Now,
                Description = $"Invoice #{invoice.InvoiceId} posted from POS checkout"
            };

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.AddRange(
                new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = accountsReceivable.AccountId,
                    Debit = total,
                    Credit = 0m
                },
                new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = salesRevenue.AccountId,
                    Debit = 0m,
                    Credit = Math.Round(taxableBase, 2)
                });

            if (taxPayable != null)
            {
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = taxPayable.AccountId,
                    Debit = 0m,
                    Credit = taxAmount
                });
            }

            _context.InvoiceItems.Add(new InvoiceItem
            {
                InvoiceId = invoice.InvoiceId,
                ItemType = "Space",
                Description = $"Space usage ({durationHours:0.00} hr)",
                Quantity = 1,
                UnitPrice = tableCharge,
                Total = tableCharge
            });

            foreach (var item in orderDetails)
            {
                var lineTotal = Math.Round(item.Quantity * item.LockedUnitPrice, 2);
                _context.InvoiceItems.Add(new InvoiceItem
                {
                    InvoiceId = invoice.InvoiceId,
                    ItemType = "Menu",
                    Description = item.MenuItem.ItemName,
                    Quantity = item.Quantity,
                    UnitPrice = item.LockedUnitPrice,
                    Total = lineTotal
                });
            }

            booking.EndTime = endTime;
            booking.DurationHours = durationHours;
            booking.BookingStatus = "Completed";
            booking.CheckoutRequested = false;

            if (booking.Space != null)
            {
                booking.Space.CurrentStatus = "Available";
            }

            // ── Auto-create Payment and mark invoice Paid ──────────────────
            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod)
                ? "Cash"
                : request.PaymentMethod.Trim();

            var payment = new Payment
            {
                BusinessId = businessId.Value,
                InvoiceId = invoice.InvoiceId,
                AmountPaid = total,
                PaymentMethod = normalizedPaymentMethod,
                PaymentDate = PhilippineTime.Now,
                ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim()
            };

            var cashAccountName = normalizedPaymentMethod.Equals("GCash", StringComparison.OrdinalIgnoreCase)
                ? "GCash Wallet"
                : normalizedPaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase)
                    ? "Card Clearing"
                    : "Cash";

            var cashAccount = await GetOrCreateAccountAsync(businessId.Value, cashAccountName, "Asset");

            var paymentJournal = new JournalEntry
            {
                BusinessId = businessId.Value,
                ReferenceId = invoice.InvoiceId,
                ReferenceType = "Payment",
                EntryDate = PhilippineTime.Now,
                Description = $"Payment received for Invoice #{invoice.InvoiceId} via {normalizedPaymentMethod}"
            };

            _context.JournalEntries.Add(paymentJournal);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.AddRange(
                new JournalEntryLine
                {
                    JournalEntryId = paymentJournal.JournalEntryId,
                    AccountId = cashAccount.AccountId,
                    Debit = total,
                    Credit = 0m
                },
                new JournalEntryLine
                {
                    JournalEntryId = paymentJournal.JournalEntryId,
                    AccountId = accountsReceivable.AccountId,
                    Debit = 0m,
                    Credit = total
                });

            invoice.PaymentStatus = "Paid";
            _context.Payments.Add(payment);

            await _context.SaveChangesAsync();

            // ── Send customer receipt email if email was captured ──────────
            if (!string.IsNullOrWhiteSpace(booking.CustomerEmail) && !booking.CustomerReceiptEmailSent)
            {
                var emailItems = orderDetails
                    .Select(od => (od.MenuItem.ItemName, od.Quantity, Math.Round(od.Quantity * od.LockedUnitPrice, 2)))
                    .ToList();

                _ = _emailService.SendCustomerReceiptAsync(
                    booking.CustomerEmail,
                    business.BusinessName,
                    booking.Space?.SpaceName ?? "",
                    booking.ReferenceCode ?? "",
                    tableCharge, menuCharge, taxAmount, total,
                    emailItems);

                booking.CustomerReceiptEmailSent = true;
                await _context.SaveChangesAsync();
            }

            TempData["Success"] = $"Checkout complete. Invoice #{invoice.InvoiceId} — Paid via {normalizedPaymentMethod} ({discountPercentage:0.##}% discount, {business.TaxRatePercentage:0.##}% tax).";
            return RedirectToAction(nameof(Index));
        }

        // ── Live Status (polling) ──────────────────────────────────────────
        [HttpGet]
        public async Task<IActionResult> LiveStatus()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var activeBookings = await _context.Bookings
                .Where(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active")
                .ToListAsync();

            var ids = activeBookings.Select(b => b.BookingId).ToList();

            var counts = ids.Count > 0
                ? await _context.OrderDetails
                    .Where(od => ids.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                    .GroupBy(od => od.Order.BookingId)
                    .Select(g => new { BookingId = g.Key, Count = g.Sum(od => od.Quantity), Total = g.Sum(od => od.Quantity * od.LockedUnitPrice) })
                    .ToListAsync()
                : new List<object>().Select(x => new { BookingId = 0, Count = 0, Total = 0m }).ToList();

            var result = activeBookings.Select(b =>
            {
                var c = counts.FirstOrDefault(x => x.BookingId == b.BookingId);
                return new
                {
                    spaceId = b.SpaceId,
                    bookingId = b.BookingId,
                    orderItemCount = c?.Count ?? 0,
                    menuTotal = c?.Total ?? 0m,
                    checkoutRequested = b.CheckoutRequested,
                    requestedSplitCount = b.RequestedSplitCount
                };
            });

            return Json(result);
        }

        // ── Tables Floor Layout ────────────────────────────────────────────
        public async Task<IActionResult> Tables()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var spaces = await _context.Spaces
                .Where(s => s.BusinessId == businessId.Value && s.IsActive)
                .OrderBy(s => s.FloorArea).ThenBy(s => s.SpaceName)
                .ToListAsync();

            var activeBookings = await _context.Bookings
                .Where(b => b.BusinessId == businessId.Value && b.BookingStatus == "Active")
                .ToListAsync();

            var activeBookingIds = activeBookings.Select(b => b.BookingId).ToList();
            var orderItemCounts = activeBookingIds.Count > 0
                ? await _context.OrderDetails
                    .Where(od => activeBookingIds.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                    .GroupBy(od => od.Order.BookingId)
                    .Select(g => new { BookingId = g.Key, Count = g.Sum(od => od.Quantity) })
                    .ToDictionaryAsync(x => x.BookingId, x => x.Count)
                : new Dictionary<int, int>();

            var orderTotals2 = activeBookingIds.Count > 0
                ? await _context.OrderDetails
                    .Where(od => activeBookingIds.Contains(od.Order.BookingId) && od.Order.BusinessId == businessId.Value)
                    .GroupBy(od => od.Order.BookingId)
                    .Select(g => new { BookingId = g.Key, Total = g.Sum(od => od.Quantity * od.LockedUnitPrice) })
                    .ToDictionaryAsync(x => x.BookingId, x => x.Total)
                : new Dictionary<int, decimal>();

            var business2 = await _context.Businesses.FirstAsync(b => b.BusinessId == businessId.Value);

            var cards = spaces.Select(space =>
            {
                var ab = activeBookings.FirstOrDefault(b => b.SpaceId == space.SpaceId);
                return new PosSpaceCardViewModel
                {
                    SpaceId = space.SpaceId,
                    SpaceName = space.SpaceName,
                    FloorArea = space.FloorArea,
                    Capacity = space.Capacity,
                    HourlyRate = space.CurrentHourlyRate,
                    Status = ab == null ? "Available" : "Occupied",
                    ActiveBookingId = ab?.BookingId,
                    ActiveStartTime = ab?.StartTime,
                    OrderItemCount = ab != null && orderItemCounts.TryGetValue(ab.BookingId, out var cnt) ? cnt : 0,
                    CheckoutRequested = ab?.CheckoutRequested ?? false,
                    MenuTotal = ab != null && orderTotals2.TryGetValue(ab.BookingId, out var mt) ? mt : 0,
                    TaxRatePercentage = business2.TaxRatePercentage,
                    LockedHourlyRate = ab?.LockedHourlyRate ?? space.CurrentHourlyRate,
                    ReferenceCode = ab?.ReferenceCode
                };
            }).ToList();

            var model = new PosTableLayoutViewModel
            {
                Floors = cards
                    .GroupBy(c => c.FloorArea)
                    .Select(g => new PosFloorAreaViewModel { FloorArea = g.Key, Spaces = g.ToList() })
                    .ToList(),
                AvailableSpaces = spaces
            };

            return View(model);
        }

        // ── Transfer Table ─────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TransferTable(TransferTableRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var booking = await _context.Bookings
                .Include(b => b.Space)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId
                    && b.BusinessId == businessId.Value
                    && b.BookingStatus == "Active");

            if (booking == null)
            {
                TempData["Error"] = "Active booking not found.";
                return RedirectToAction(nameof(Tables));
            }

            if (booking.SpaceId == request.ToSpaceId)
            {
                TempData["Error"] = "Cannot transfer to the same table.";
                return RedirectToAction(nameof(Tables));
            }

            var targetSpace = await _context.Spaces
                .FirstOrDefaultAsync(s => s.SpaceId == request.ToSpaceId
                    && s.BusinessId == businessId.Value
                    && s.IsActive);

            if (targetSpace == null)
            {
                TempData["Error"] = "Destination table not found.";
                return RedirectToAction(nameof(Tables));
            }

            var targetHasActive = await _context.Bookings.AnyAsync(b =>
                b.SpaceId == request.ToSpaceId
                && b.BusinessId == businessId.Value
                && b.BookingStatus == "Active");

            if (targetHasActive)
            {
                TempData["Error"] = $"{targetSpace.SpaceName} already has an active session.";
                return RedirectToAction(nameof(Tables));
            }

            var sourceSpace = booking.Space;
            var sourceSpaceName = sourceSpace?.SpaceName ?? "Unknown";

            // Move the booking
            booking.SpaceId = request.ToSpaceId;
            booking.LockedHourlyRate = targetSpace.CurrentHourlyRate;

            // Update space statuses
            if (sourceSpace != null) sourceSpace.CurrentStatus = "Available";
            targetSpace.CurrentStatus = "Occupied";

            // Audit log
            var cashierId = GetUserId();
            if (cashierId.HasValue)
            {
                _context.PosAuditLogs.Add(new PosAuditLog
                {
                    BusinessId = businessId.Value,
                    CashierId = cashierId.Value,
                    BookingId = booking.BookingId,
                    ActionType = "TransferTable",
                    SourceSpaceId = sourceSpace?.SpaceId,
                    SourceSpaceName = sourceSpaceName,
                    TargetSpaceId = targetSpace.SpaceId,
                    TargetSpaceName = targetSpace.SpaceName,
                    Details = $"Booking #{booking.BookingId} transferred from {sourceSpaceName} to {targetSpace.SpaceName}",
                    CreatedAt = PhilippineTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Booking #{booking.BookingId} transferred from {sourceSpaceName} to {targetSpace.SpaceName}.";
            return RedirectToAction(nameof(Tables));
        }

        // ── Split Checkout ─────────────────────────────────────────────────
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SplitCheckout(SplitCheckoutRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!await HasOpenShiftAsync(businessId.Value))
            {
                TempData["Error"] = "You must have an open shift to process a split checkout.";
                return RedirectToAction(nameof(Index));
            }

            var discountPercentage = Math.Round(request.DiscountPercentage, 2);
            if (discountPercentage < 0m || discountPercentage > 100m)
            {
                TempData["Error"] = "Discount must be between 0% and 100%.";
                return RedirectToAction(nameof(Index));
            }

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            var booking = await _context.Bookings
                .Include(b => b.Space)
                .FirstOrDefaultAsync(b => b.BookingId == request.BookingId
                    && b.BusinessId == businessId.Value
                    && b.BookingStatus == "Active");
            if (booking == null) return NotFound();

            var endTime = PhilippineTime.Now;
            var durationHours = Math.Round((decimal)(endTime - booking.StartTime).TotalHours, 2);
            if (durationHours < 0.01m) durationHours = 0.01m;

            var tableCharge = Math.Round(durationHours * booking.LockedHourlyRate, 2);

            var orderDetails = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .Where(od => od.Order.BusinessId == businessId.Value && od.Order.BookingId == booking.BookingId)
                .ToListAsync();

            var menuCharge = orderDetails.Sum(od => od.Quantity * od.LockedUnitPrice);
            var subTotal = Math.Round(tableCharge + menuCharge, 2);
            var discountAmount = Math.Round(subTotal * (discountPercentage / 100m), 2);
            var taxableBase = Math.Max(0m, subTotal - discountAmount);
            var taxRateApplied = Math.Round(business.TaxRatePercentage / 100m, 4);
            var taxAmount = Math.Round(taxableBase * taxRateApplied, 2);
            var total = Math.Round(taxableBase + taxAmount, 2);

            var splitCount = request.SplitCount;
            var splitAmount = Math.Round(total / splitCount, 2);
            // give any rounding remainder to the last split
            var lastSplitAmount = total - (splitAmount * (splitCount - 1));

            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(request.PaymentMethod)
                ? "Cash"
                : request.PaymentMethod.Trim();

            var cashAccountName = normalizedPaymentMethod.Equals("GCash", StringComparison.OrdinalIgnoreCase)
                ? "GCash Wallet"
                : normalizedPaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase)
                    ? "Card Clearing"
                    : "Cash";

            var accountsReceivable = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");
            var salesRevenue = await GetOrCreateAccountAsync(businessId.Value, "Sales Revenue", "Revenue");
            var taxPayable = taxAmount > 0m
                ? await GetOrCreateAccountAsync(businessId.Value, "Output Tax Payable", "Liability")
                : null;
            var cashAccount = await GetOrCreateAccountAsync(businessId.Value, cashAccountName, "Asset");

            var invoiceIds = new List<int>();

            for (int i = 0; i < splitCount; i++)
            {
                var isLast = i == splitCount - 1;
                var splitTotal = isLast ? lastSplitAmount : splitAmount;

                // Proportional breakdown for each split
                var splitSubTotal = Math.Round(subTotal / splitCount, 2);
                var splitDiscount = Math.Round(discountAmount / splitCount, 2);
                var splitTaxableBase = Math.Max(0m, splitSubTotal - splitDiscount);
                var splitTax = Math.Round(splitTaxableBase * taxRateApplied, 2);

                // Adjust last split for rounding
                if (isLast)
                {
                    splitSubTotal = subTotal - (Math.Round(subTotal / splitCount, 2) * (splitCount - 1));
                    splitDiscount = discountAmount - (Math.Round(discountAmount / splitCount, 2) * (splitCount - 1));
                    splitTaxableBase = Math.Max(0m, splitSubTotal - splitDiscount);
                    splitTax = taxAmount - (Math.Round(Math.Max(0m, Math.Round(subTotal / splitCount, 2) - Math.Round(discountAmount / splitCount, 2)) * taxRateApplied, 2) * (splitCount - 1));
                    splitTotal = Math.Round(splitTaxableBase + splitTax, 2);
                }

                var invoice = new Invoice
                {
                    BusinessId = businessId.Value,
                    BookingId = booking.BookingId,
                    SubTotal = splitSubTotal,
                    DiscountAmount = splitDiscount,
                    TaxAmount = splitTax,
                    TotalAmount = splitTotal,
                    TaxRateApplied = taxRateApplied,
                    PaymentStatus = "Paid",
                    GeneratedDate = PhilippineTime.Now
                };

                _context.Invoices.Add(invoice);
                await _context.SaveChangesAsync();
                invoiceIds.Add(invoice.InvoiceId);

                // Invoice line items (only on the first split to avoid duplication)
                if (i == 0)
                {
                    _context.InvoiceItems.Add(new InvoiceItem
                    {
                        InvoiceId = invoice.InvoiceId,
                        ItemType = "Space",
                        Description = $"Space usage ({durationHours:0.00} hr) — split {splitCount} ways",
                        Quantity = 1,
                        UnitPrice = tableCharge,
                        Total = tableCharge
                    });

                    foreach (var od in orderDetails)
                    {
                        _context.InvoiceItems.Add(new InvoiceItem
                        {
                            InvoiceId = invoice.InvoiceId,
                            ItemType = "Menu",
                            Description = od.MenuItem.ItemName,
                            Quantity = od.Quantity,
                            UnitPrice = od.LockedUnitPrice,
                            Total = Math.Round(od.Quantity * od.LockedUnitPrice, 2)
                        });
                    }
                }

                // Sales journal entry (DR AR, CR Revenue + Tax)
                var salesJournal = new JournalEntry
                {
                    BusinessId = businessId.Value,
                    ReferenceId = invoice.InvoiceId,
                    ReferenceType = "Invoice",
                    EntryDate = PhilippineTime.Now,
                    Description = $"Invoice #{invoice.InvoiceId} — split {i + 1}/{splitCount} from POS"
                };
                _context.JournalEntries.Add(salesJournal);
                await _context.SaveChangesAsync();

                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntryId = salesJournal.JournalEntryId,
                    AccountId = accountsReceivable.AccountId,
                    Debit = splitTotal,
                    Credit = 0m
                });
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntryId = salesJournal.JournalEntryId,
                    AccountId = salesRevenue.AccountId,
                    Debit = 0m,
                    Credit = Math.Round(splitTaxableBase, 2)
                });
                if (taxPayable != null && splitTax > 0m)
                {
                    _context.JournalEntryLines.Add(new JournalEntryLine
                    {
                        JournalEntryId = salesJournal.JournalEntryId,
                        AccountId = taxPayable.AccountId,
                        Debit = 0m,
                        Credit = splitTax
                    });
                }

                // Payment + payment journal entry (DR Cash/GCash/Card, CR AR)
                _context.Payments.Add(new Payment
                {
                    BusinessId = businessId.Value,
                    InvoiceId = invoice.InvoiceId,
                    AmountPaid = splitTotal,
                    PaymentMethod = normalizedPaymentMethod,
                    PaymentDate = PhilippineTime.Now,
                    ReferenceNumber = string.IsNullOrWhiteSpace(request.ReferenceNumber) ? null : request.ReferenceNumber.Trim()
                });

                var payJournal = new JournalEntry
                {
                    BusinessId = businessId.Value,
                    ReferenceId = invoice.InvoiceId,
                    ReferenceType = "Payment",
                    EntryDate = PhilippineTime.Now,
                    Description = $"Payment for Invoice #{invoice.InvoiceId} (split {i + 1}/{splitCount}) via {normalizedPaymentMethod}"
                };
                _context.JournalEntries.Add(payJournal);
                await _context.SaveChangesAsync();

                _context.JournalEntryLines.AddRange(
                    new JournalEntryLine
                    {
                        JournalEntryId = payJournal.JournalEntryId,
                        AccountId = cashAccount.AccountId,
                        Debit = splitTotal,
                        Credit = 0m
                    },
                    new JournalEntryLine
                    {
                        JournalEntryId = payJournal.JournalEntryId,
                        AccountId = accountsReceivable.AccountId,
                        Debit = 0m,
                        Credit = splitTotal
                    });

                await _context.SaveChangesAsync();
            }

            // Complete booking
            booking.EndTime = endTime;
            booking.DurationHours = durationHours;
            booking.BookingStatus = "Completed";
            booking.CheckoutRequested = false;
            if (booking.Space != null) booking.Space.CurrentStatus = "Available";

            // Audit log
            var userId = GetUserId();
            if (userId.HasValue)
            {
                _context.PosAuditLogs.Add(new PosAuditLog
                {
                    BusinessId = businessId.Value,
                    CashierId = userId.Value,
                    BookingId = booking.BookingId,
                    ActionType = "SplitCheckout",
                    SourceSpaceId = booking.SpaceId,
                    SourceSpaceName = booking.Space?.SpaceName,
                    SplitCount = splitCount,
                    InvoiceIds = string.Join(",", invoiceIds),
                    Details = $"Split {splitCount}-way checkout via {normalizedPaymentMethod}, total ₱{total:N2} ({discountPercentage:0.##}% discount)",
                    CreatedAt = PhilippineTime.Now
                });
            }

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Split checkout complete — {splitCount} invoices created (₱{splitAmount:N2} each) via {normalizedPaymentMethod}.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private int? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId) ? userId : null;
        }

        // GET /POS/GetBookingOrders?bookingId=X  — returns current order items for a booking
        [HttpGet]
        public async Task<IActionResult> GetBookingOrders(int bookingId)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var booking = await _context.Bookings
                .FirstOrDefaultAsync(b => b.BookingId == bookingId && b.BusinessId == businessId.Value && b.BookingStatus == "Active");
            if (booking == null) return NotFound();

            var details = await _context.OrderDetails
                .Include(od => od.MenuItem)
                .Include(od => od.Order)
                .Where(od => od.Order.BookingId == bookingId && od.Order.BusinessId == businessId.Value)
                .Select(od => new
                {
                    od.OrderDetailId,
                    od.MenuItem.ItemName,
                    od.Quantity,
                    od.LockedUnitPrice,
                    Subtotal = od.Quantity * od.LockedUnitPrice
                })
                .ToListAsync();

            return Json(details);
        }

        // POST /POS/VoidOrderItem
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VoidOrderItem([FromForm] int orderDetailId)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Json(new { ok = false, error = "Unauthorized" });

            var detail = await _context.OrderDetails
                .Include(od => od.Order)
                .Include(od => od.MenuItem)
                .FirstOrDefaultAsync(od => od.OrderDetailId == orderDetailId && od.Order.BusinessId == businessId.Value);

            if (detail == null) return Json(new { ok = false, error = "Item not found" });

            // Verify booking is still active
            var booking = await _context.Bookings.FirstOrDefaultAsync(b =>
                b.BookingId == detail.Order.BookingId && b.BookingStatus == "Active");
            if (booking == null) return Json(new { ok = false, error = "Session is no longer active" });

            // Restore stock
            detail.MenuItem.StockAvailable += detail.Quantity;

            _context.OrderDetails.Remove(detail);

            // Remove empty order if no more details remain after removal
            var remaining = await _context.OrderDetails.CountAsync(od => od.OrderId == detail.OrderId && od.OrderDetailId != detail.OrderDetailId);
            if (remaining == 0)
            {
                var order = await _context.Orders.FindAsync(detail.OrderId);
                if (order != null) _context.Orders.Remove(order);
            }

            await _context.SaveChangesAsync();
            return Json(new { ok = true });
        }

        private async Task<bool> HasOpenShiftAsync(int businessId)
        {
            var userId = GetUserId();
            if (userId == null) return false;
            return await _context.PosShifts.AnyAsync(s =>
                s.BusinessId == businessId && s.CashierId == userId.Value && s.Status == "Open");
        }

        private async Task<ChartOfAccount> GetOrCreateAccountAsync(int businessId, string accountName, string accountType)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.BusinessId == businessId && a.AccountName == accountName);

            if (account != null)
            {
                return account;
            }

            account = new ChartOfAccount
            {
                BusinessId = businessId,
                AccountName = accountName,
                AccountType = accountType,
                IsActive = true
            };

            _context.ChartOfAccounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }
    }
}
