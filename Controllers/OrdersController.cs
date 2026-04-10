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
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var orders = _context.Orders
                .Include(o => o.Booking)
                .Include(o => o.Business)
                .Include(o => o.Cashier)
                .Where(o => o.BusinessId == businessId.Value);

            return View(await orders.ToListAsync());
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

            return View(order);
        }

        // GET: Orders/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus");
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            ViewData["CashierId"] = new SelectList(_context.Users.Where(u => u.BusinessId == businessId.Value), "UserId", "EmailAddress");
            return View();
        }

        // POST: Orders/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("OrderId,BusinessId,BookingId,CashierId,OrderTime")] Order order)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            order.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(order);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", order.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", order.BusinessId);
            ViewData["CashierId"] = new SelectList(_context.Users.Where(u => u.BusinessId == businessId.Value), "UserId", "EmailAddress", order.CashierId);
            return View(order);
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

        // GET: Orders/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

            return View(order);
        }

        // POST: Orders/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var order = await _context.Orders.FirstOrDefaultAsync(o => o.OrderId == id && o.BusinessId == businessId.Value);
            if (order != null)
            {
                _context.Orders.Remove(order);
            }

            await _context.SaveChangesAsync();
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

