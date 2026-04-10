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
    [Authorize(Roles = "MainAdmin,Manager")]
    public class MenuItemsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public MenuItemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: MenuItems
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItems = _context.MenuItems
                .Include(m => m.Business)
                .Where(m => m.BusinessId == businessId.Value);
            return View(await menuItems.ToListAsync());
        }

        // GET: MenuItems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItem = await _context.MenuItems
                .Include(m => m.Business)
                .FirstOrDefaultAsync(m => m.ItemId == id && m.BusinessId == businessId.Value);
            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // GET: MenuItems/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            return View();
        }

        // POST: MenuItems/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("ItemId,BusinessId,ItemName,CurrentPrice,CostPrice,StockAvailable,LowStockThreshold,IsActive")] MenuItem menuItem)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            menuItem.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(menuItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", menuItem.BusinessId);
            return View(menuItem);
        }

        // GET: MenuItems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.ItemId == id && m.BusinessId == businessId.Value);
            if (menuItem == null)
            {
                return NotFound();
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", menuItem.BusinessId);
            return View(menuItem);
        }

        // POST: MenuItems/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("ItemId,BusinessId,ItemName,CurrentPrice,CostPrice,StockAvailable,LowStockThreshold,IsActive")] MenuItem menuItem)
        {
            if (id != menuItem.ItemId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            menuItem.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(menuItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!MenuItemExists(menuItem.ItemId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", menuItem.BusinessId);
            return View(menuItem);
        }

        // GET: MenuItems/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItem = await _context.MenuItems
                .Include(m => m.Business)
                .FirstOrDefaultAsync(m => m.ItemId == id && m.BusinessId == businessId.Value);
            if (menuItem == null)
            {
                return NotFound();
            }

            return View(menuItem);
        }

        // POST: MenuItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.ItemId == id && m.BusinessId == businessId.Value);
            if (menuItem != null)
            {
                _context.MenuItems.Remove(menuItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool MenuItemExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.MenuItems.Any(e => e.ItemId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

