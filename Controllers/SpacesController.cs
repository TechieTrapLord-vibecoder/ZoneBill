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
        public async Task<IActionResult> Index(int page = 1)
        {
            var myBusinessId = User.FindFirst("BusinessId")?.Value;

            const int pageSize = 10;
            var query = _context.Spaces
                .Include(s => s.Business)
                .Where(s => s.BusinessId.ToString() == myBusinessId);
            var totalCount = await query.CountAsync();
            var availableCount = await query.CountAsync(s => s.CurrentStatus == "Available");
            var occupiedCount = await query.CountAsync(s => s.CurrentStatus == "Occupied");
            var inactiveCount = await query.CountAsync(s => !s.IsActive);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.SpaceTotal = totalCount;
            ViewBag.SpaceAvailable = availableCount;
            ViewBag.SpaceOccupied = occupiedCount;
            ViewBag.SpaceInactive = inactiveCount;
            return View(await query.OrderBy(s => s.SpaceName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
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
            space.IsActive = true;
            ModelState.Remove("BusinessId");
            ModelState.Remove("IsActive");
            ModelState.Remove("Business");

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
        public async Task<IActionResult> Edit(int id, [Bind("SpaceId,SpaceName,FloorArea,Capacity,CurrentHourlyRate,CurrentStatus,IsActive")] Space space)
        {
            if (id != space.SpaceId)
            {
                return NotFound();
            }

            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            if (!int.TryParse(myBusinessId, out int businessId))
                return Unauthorized();

            // Verify the space belongs to this user's business
            var exists = await _context.Spaces.AnyAsync(s => s.SpaceId == id && s.BusinessId == businessId);
            if (!exists)
                return NotFound();

            space.BusinessId = businessId;
            ModelState.Remove("Business");
            ModelState.Remove("BusinessId");

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
            return View(space);
        }

        // GET: Spaces/Delete — Redirects to Archive
        public IActionResult Delete(int? id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: Spaces/Delete — Redirects to Archive
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: Spaces/Archive/5 — Toggle IsActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var myBusinessId = User.FindFirst("BusinessId")?.Value;
            if (!int.TryParse(myBusinessId, out var businessId)) return Forbid();
            var space = await _context.Spaces.FirstOrDefaultAsync(s => s.SpaceId == id && s.BusinessId == businessId);
            if (space == null) return NotFound();
            space.IsActive = !space.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = space.IsActive ? $"Space \u2018{space.SpaceName}\u2019 has been restored." : $"Space \u2018{space.SpaceName}\u2019 has been archived.";
            return RedirectToAction(nameof(Index));
        }

        private bool SpaceExists(int id)
        {
            return _context.Spaces.Any(e => e.SpaceId == id);
        }
    }
}

