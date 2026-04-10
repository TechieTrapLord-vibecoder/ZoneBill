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
    public class SpacesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SpacesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Spaces
        public async Task<IActionResult> Index()
        {
            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            
            // Only load spaces completely belonging to their own business!
            var spaces = await _context.Spaces
                .Include(s => s.Business)
                .Where(s => s.BusinessId.ToString() == myBusinessId)
                .ToListAsync();

            return View(spaces);
        }

        // GET: Spaces/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(m => m.SpaceId == id);
            if (space == null)
            {
                return NotFound();
            }

            return View(space);
        }

        // GET: Spaces/Create
        public async Task<IActionResult> Create()
        {
            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            if (!int.TryParse(myBusinessId, out var businessId))
            {
                return Forbid();
            }

            var business = await _context.Businesses
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null)
            {
                return Forbid();
            }

            var maxTablesAllowed = Math.Max(1, business.Plan?.MaxTablesAllowed ?? 1);
            var currentTables = await _context.Spaces
                .CountAsync(s => s.BusinessId == businessId && s.IsActive);

            if (currentTables >= maxTablesAllowed)
            {
                TempData["Error"] = $"Your current plan allows only {maxTablesAllowed} table(s). Upgrade your plan to add more.";
                return RedirectToAction("Index", "Billing");
            }
            
            // Only allow assigning the space to their OWN business
            ViewData["BusinessId"] = new SelectList(
                _context.Businesses.Where(b => b.BusinessId == businessId), 
                "BusinessId", 
                "BusinessName"
            );
            return View();
        }

        // POST: Spaces/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("SpaceId,BusinessId,SpaceName,FloorArea,Capacity,CurrentHourlyRate,CurrentStatus,IsActive")] Space space)
        {
            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            if (!int.TryParse(myBusinessId, out var businessId))
            {
                return Forbid();
            }

            var business = await _context.Businesses
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId);
            if (business == null)
            {
                return Forbid();
            }

            var maxTablesAllowed = Math.Max(1, business.Plan?.MaxTablesAllowed ?? 1);
            var currentTables = await _context.Spaces
                .CountAsync(s => s.BusinessId == businessId && s.IsActive);

            space.BusinessId = businessId;

            if (currentTables >= maxTablesAllowed)
            {
                TempData["Error"] = $"Your current plan allows only {maxTablesAllowed} table(s). Upgrade your plan to add more.";
                return RedirectToAction("Index", "Billing");
            }

            if (ModelState.IsValid)
            {
                _context.Add(space);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(
                _context.Businesses.Where(b => b.BusinessId == businessId),
                "BusinessId",
                "BusinessName",
                businessId);
            return View(space);
        }

        // GET: Spaces/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.SpaceId == id && s.BusinessId.ToString() == myBusinessId);
            if (space == null)
            {
                return NotFound();
            }
            
            ViewData["BusinessId"] = new SelectList(
                _context.Businesses.Where(b => b.BusinessId.ToString() == myBusinessId), 
                "BusinessId", 
                "BusinessName", 
                space.BusinessId
            );
            return View(space);
        }

        // POST: Spaces/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("SpaceId,BusinessId,SpaceName,FloorArea,Capacity,CurrentHourlyRate,CurrentStatus,IsActive")] Space space)
        {
            if (id != space.SpaceId)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(space);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SpaceExists(space.SpaceId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses, "BusinessId", "BusinessName", space.BusinessId);
            return View(space);
        }

        // GET: Spaces/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var space = await _context.Spaces
                .Include(s => s.Business)
                .FirstOrDefaultAsync(m => m.SpaceId == id);
            if (space == null)
            {
                return NotFound();
            }

            return View(space);
        }

        // POST: Spaces/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var space = await _context.Spaces.FindAsync(id);
            if (space != null)
            {
                _context.Spaces.Remove(space);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SpaceExists(int id)
        {
            return _context.Spaces.Any(e => e.SpaceId == id);
        }
    }
}

