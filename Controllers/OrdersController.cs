using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin,Manager,Cashier")]
    public class OrdersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public OrdersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Orders
        public async Task<IActionResult> Index(string? search, int? cashierId, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 5;

            var orders = _context.Orders
                .Include(o => o.Booking)
                .Include(o => o.Business)
                .Include(o => o.Cashier)
                .Where(o => o.BusinessId == businessId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out var orderId))
                {
                    orders = orders.Where(o => o.OrderId == orderId || o.BookingId == orderId);
                }
                else
                {
                    orders = orders.Where(o =>
                        (o.Cashier != null && o.Cashier.EmailAddress.Contains(trimmedSearch)) ||
                        (o.Booking != null && o.Booking.ReferenceCode != null && o.Booking.ReferenceCode.Contains(trimmedSearch)) ||
                        (o.Booking != null && o.Booking.BookingStatus.Contains(trimmedSearch)));
                }
            }

            if (cashierId.HasValue)
            {
                orders = orders.Where(o => o.CashierId == cashierId.Value);
            }

            if (fromDate.HasValue)
            {
                orders = orders.Where(o => o.OrderTime >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                orders = orders.Where(o => o.OrderTime < endDateExclusive);
            }

            ViewBag.TotalOrders = await orders.CountAsync();
            ViewBag.TotalPortalOrders = await orders.CountAsync(o => o.OrderSource == "Portal");

            var filteredOrderIds = orders.Select(o => o.OrderId);
            var filteredDetails = _context.OrderDetails.Where(od => filteredOrderIds.Contains(od.OrderId));
            ViewBag.TotalItemLines = await filteredDetails.CountAsync();
            ViewBag.TotalQtySold = await filteredDetails.SumAsync(od => (int?)od.Quantity) ?? 0;
            ViewBag.TotalMenuSales = await filteredDetails.SumAsync(od => (decimal?)(od.Quantity * od.LockedUnitPrice)) ?? 0m;

            var totalCount = await orders.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            ViewBag.Search = search;
            ViewBag.CashierId = cashierId;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            var cashierOptions = await _context.Users
                .Where(u => u.BusinessId == businessId.Value)
                .OrderBy(u => u.EmailAddress)
                .Select(u => new { u.UserId, u.EmailAddress })
                .ToListAsync();
            ViewBag.Cashiers = new SelectList(cashierOptions, "UserId", "EmailAddress", cashierId);

            var pageOrders = await orders
                .OrderByDescending(o => o.OrderTime)
                .ThenByDescending(o => o.OrderId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var pageOrderIds = pageOrders.Select(o => o.OrderId).ToList();
            var rowMetrics = pageOrderIds.Count == 0
                ? new Dictionary<int, OrderListRowMetrics>()
                : await _context.OrderDetails
                    .Where(od => pageOrderIds.Contains(od.OrderId))
                    .GroupBy(od => od.OrderId)
                    .Select(g => new OrderListRowMetrics
                    {
                        OrderId = g.Key,
                        LineCount = g.Count(),
                        TotalQty = g.Sum(x => x.Quantity),
                        MenuTotal = g.Sum(x => x.Quantity * x.LockedUnitPrice),
                        ServedLines = g.Count(x => x.IsServed),
                        UnservedLines = g.Count(x => !x.IsServed)
                    })
                    .ToDictionaryAsync(x => x.OrderId);

            ViewBag.RowMetrics = rowMetrics;

            return View(pageOrders);
        }

        // GET: Orders/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var order = await _context.Orders
                .Include(o => o.Booking)
                .Include(o => o.Business)
                .Include(o => o.Cashier)
                .FirstOrDefaultAsync(m => m.OrderId == id && m.BusinessId == businessId.Value);
            if (order == null)
            {
                return NotFound();
            }

            var lines = await _context.OrderDetails
                .Include(od => od.MenuItem)
                .Where(od => od.OrderId == order.OrderId)
                .OrderByDescending(od => od.OrderDetailId)
                .ToListAsync();

            ViewBag.OrderLines = lines;
            ViewBag.MenuTotal = lines.Sum(od => od.Quantity * od.LockedUnitPrice);
            ViewBag.TotalQty = lines.Sum(od => od.Quantity);
            ViewBag.LineCount = lines.Count;

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            TempData["Error"] = "Manual order creation is disabled. Add orders from POS Dashboard.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("OrderId,BusinessId,BookingId,CashierId,OrderTime")] Order order)
        {
            TempData["Error"] = "Manual order creation is disabled. Add orders from POS Dashboard.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Orders/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.BusinessId == businessId.Value);
            if (order == null)
            {
                return NotFound();
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", order.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", order.BusinessId);
            ViewData["CashierId"] = new SelectList(_context.Users.Where(u => u.BusinessId == businessId.Value), "UserId", "EmailAddress", order.CashierId);
            return View(order);
        }

        // POST: Orders/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("OrderId,BusinessId,BookingId,CashierId,OrderTime")] Order order)
        {
            if (id != order.OrderId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            order.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(order);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!OrderExists(order.OrderId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", order.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", order.BusinessId);
            ViewData["CashierId"] = new SelectList(_context.Users.Where(u => u.BusinessId == businessId.Value), "UserId", "EmailAddress", order.CashierId);
            return View(order);
        }

        // GET: Orders/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Order records are kept for audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Orders/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Order records are kept for audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        private bool OrderExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Orders.Any(e => e.OrderId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

