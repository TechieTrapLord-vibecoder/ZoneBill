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
    public class BookingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public BookingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Bookings
        public async Task<IActionResult> Index(string? search, string? status, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 5;

            var bookings = _context.Bookings
                .Include(b => b.Business)
                .Include(b => b.Customer)
                .Include(b => b.Space)
                .Where(b => b.BusinessId == businessId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out var bookingId))
                {
                    bookings = bookings.Where(b => b.BookingId == bookingId);
                }
                else
                {
                    bookings = bookings.Where(b =>
                        (b.ReferenceCode != null && b.ReferenceCode.Contains(trimmedSearch)) ||
                        (b.Space != null && b.Space.SpaceName.Contains(trimmedSearch)) ||
                        (b.Customer != null && b.Customer.Name.Contains(trimmedSearch)));
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                bookings = bookings.Where(b => b.BookingStatus == status);
            }

            if (fromDate.HasValue)
            {
                bookings = bookings.Where(b => b.StartTime >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                bookings = bookings.Where(b => b.StartTime < endDateExclusive);
            }

            ViewBag.TotalBookings = await bookings.CountAsync();
            ViewBag.ActiveBookings = await bookings.CountAsync(b => b.BookingStatus == "Active");
            ViewBag.CompletedBookings = await bookings.CountAsync(b => b.BookingStatus == "Completed");
            ViewBag.CancelledBookings = await bookings.CountAsync(b => b.BookingStatus == "Cancelled");

            var totalCount = await bookings.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(await bookings
                .OrderByDescending(b => b.StartTime)
                .ThenByDescending(b => b.BookingId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync());
        }

        // GET: Bookings/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var booking = await _context.Bookings
                .Include(b => b.Business)
                .Include(b => b.Customer)
                .Include(b => b.Space)
                .FirstOrDefaultAsync(m => m.BookingId == id && m.BusinessId == businessId.Value);
            if (booking == null)
            {
                return NotFound();
            }

            return View(booking);
        }

        // GET: Bookings/Create
        public IActionResult Create()
        {
            TempData["Error"] = "Manual booking creation is disabled. Start sessions from POS Dashboard.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Bookings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("BookingId,BusinessId,SpaceId,CustomerId,StartTime,EndTime,DurationHours,LockedHourlyRate,BookingStatus,ReferenceCode")] Booking booking)
        {
            TempData["Error"] = "Manual booking creation is disabled. Start sessions from POS Dashboard.";
            return RedirectToAction(nameof(Index));
        }

        // GET: Bookings/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id && b.BusinessId == businessId.Value);
            if (booking == null)
            {
                return NotFound();
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", booking.BusinessId);
            ViewData["CustomerId"] = new SelectList(_context.Customers.Where(c => c.BusinessId == businessId.Value), "CustomerId", "Name", booking.CustomerId);
            ViewData["SpaceId"] = new SelectList(_context.Spaces.Where(s => s.BusinessId == businessId.Value), "SpaceId", "SpaceName", booking.SpaceId);
            return View(booking);
        }

        // POST: Bookings/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BookingId,BusinessId,SpaceId,CustomerId,StartTime,EndTime,DurationHours,LockedHourlyRate,BookingStatus,ReferenceCode")] Booking booking)
        {
            if (id != booking.BookingId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            booking.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(booking);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BookingExists(booking.BookingId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", booking.BusinessId);
            ViewData["CustomerId"] = new SelectList(_context.Customers.Where(c => c.BusinessId == businessId.Value), "CustomerId", "Name", booking.CustomerId);
            ViewData["SpaceId"] = new SelectList(_context.Spaces.Where(s => s.BusinessId == businessId.Value), "SpaceId", "SpaceName", booking.SpaceId);
            return View(booking);
        }

        // GET: Bookings/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Booking records are kept for audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Bookings/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Booking records are kept for audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        private bool BookingExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Bookings.Any(e => e.BookingId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

