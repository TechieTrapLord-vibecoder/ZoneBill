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
        public async Task<IActionResult> Index(int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;
            var query = _context.MenuItems
                .Include(m => m.Business)
                .Where(m => m.BusinessId == businessId.Value)
                .OrderBy(m => m.Category)
                .ThenBy(m => m.SortOrder)
                .ThenBy(m => m.ItemName);
            var totalCount = await query.CountAsync();
            var lowStockCount = await _context.MenuItems.CountAsync(m => m.BusinessId == businessId.Value && m.IsActive && m.StockAvailable <= m.LowStockThreshold);
            var inactiveCount = await _context.MenuItems.CountAsync(m => m.BusinessId == businessId.Value && !m.IsActive);
            var categoryCount = await _context.MenuItems.Where(m => m.BusinessId == businessId.Value).Select(m => m.Category).Distinct().CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.LowStockCount = lowStockCount;
            ViewBag.InactiveCount = inactiveCount;
            ViewBag.CategoryCount = categoryCount;
            return View(await query.Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
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
        public async Task<IActionResult> Create([Bind("ItemId,BusinessId,ItemName,Category,ImageUrl,SortOrder,CurrentPrice,CostPrice,StockAvailable,LowStockThreshold,IsActive")] MenuItem menuItem)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            menuItem.BusinessId = businessId.Value;
            ModelState.Remove(nameof(MenuItem.BusinessId));
            ModelState.Remove(nameof(MenuItem.Business));

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
        public async Task<IActionResult> Edit(int id, [Bind("ItemId,BusinessId,ItemName,Category,ImageUrl,SortOrder,CurrentPrice,CostPrice,StockAvailable,LowStockThreshold,IsActive")] MenuItem menuItem)
        {
            if (id != menuItem.ItemId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            menuItem.BusinessId = businessId.Value;
            ModelState.Remove(nameof(MenuItem.BusinessId));
            ModelState.Remove(nameof(MenuItem.Business));

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

        // GET: MenuItems/Delete — Redirects to Archive
        public IActionResult Delete(int? id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: MenuItems/Delete — Redirects to Archive
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            var menuItem = await _context.MenuItems.FirstOrDefaultAsync(m => m.ItemId == id && m.BusinessId == businessId.Value);
            if (menuItem == null) return NotFound();
            menuItem.IsActive = !menuItem.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = menuItem.IsActive ? $"\u2018{menuItem.ItemName}\u2019 has been restored." : $"\u2018{menuItem.ItemName}\u2019 has been archived.";
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

