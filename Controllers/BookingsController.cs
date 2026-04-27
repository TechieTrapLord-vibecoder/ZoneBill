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
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var bookings = _context.Bookings
                .Include(b => b.Business)
                .Include(b => b.Customer)
                .Include(b => b.Space)
                .Where(b => b.BusinessId == businessId.Value);

            return View(await bookings.ToListAsync());
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
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            ViewData["CustomerId"] = new SelectList(_context.Customers.Where(c => c.BusinessId == businessId.Value), "CustomerId", "Name");
            ViewData["SpaceId"] = new SelectList(_context.Spaces.Where(s => s.BusinessId == businessId.Value), "SpaceId", "SpaceName");
            return View();
        }

        // POST: Bookings/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BookingId,BusinessId,SpaceId,CustomerId,StartTime,EndTime,DurationHours,LockedHourlyRate,BookingStatus,ReferenceCode")] Booking booking)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            booking.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(booking);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", booking.BusinessId);
            ViewData["CustomerId"] = new SelectList(_context.Customers.Where(c => c.BusinessId == businessId.Value), "CustomerId", "Name", booking.CustomerId);
            ViewData["SpaceId"] = new SelectList(_context.Spaces.Where(s => s.BusinessId == businessId.Value), "SpaceId", "SpaceName", booking.SpaceId);
            return View(booking);
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

        // GET: Bookings/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

        // POST: Bookings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var booking = await _context.Bookings.FirstOrDefaultAsync(b => b.BookingId == id && b.BusinessId == businessId.Value);
            if (booking != null)
            {
                _context.Bookings.Remove(booking);
            }

            await _context.SaveChangesAsync();
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

