using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "SuperAdmin")]
    public class SubscriptionPlansController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SubscriptionPlansController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: SubscriptionPlans
        public async Task<IActionResult> Index()
        {
            var plans = await _context.SubscriptionPlans
                .OrderBy(p => p.MonthlyPrice)
                .ToListAsync();

            var activeUsage = await _context.Businesses
                .Where(b => b.IsActive)
                .GroupBy(b => b.PlanId)
                .Select(g => new { PlanId = g.Key, Count = g.Count() })
                .ToListAsync();

            ViewBag.ActiveUsageByPlan = activeUsage.ToDictionary(x => x.PlanId, x => x.Count);
            return View(plans);
        }

        // GET: SubscriptionPlans/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subscriptionPlan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(m => m.PlanId == id);
            if (subscriptionPlan == null)
            {
                return NotFound();
            }

            return View(subscriptionPlan);
        }

        // GET: SubscriptionPlans/Create
        public IActionResult Create()
        {
            return View();
        }

        // POST: SubscriptionPlans/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PlanId,PlanName,MonthlyPrice,MaxTablesAllowed,IsActive")] SubscriptionPlan subscriptionPlan)
        {
            if (ModelState.IsValid)
            {
                _context.Add(subscriptionPlan);
                _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
                {
                    ActionType = "Create",
                    EntityType = "SubscriptionPlan",
                    Details = $"Plan '{subscriptionPlan.PlanName}' created at {subscriptionPlan.MonthlyPrice:0.00}/month.",
                    ActorUserId = TryGetCurrentUserId(),
                    ActorName = User.Identity?.Name ?? "SuperAdmin",
                    CreatedAt = PhilippineTime.Now
                });
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(subscriptionPlan);
        }

        // GET: SubscriptionPlans/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var subscriptionPlan = await _context.SubscriptionPlans.FindAsync(id);
            if (subscriptionPlan == null)
            {
                return NotFound();
            }
            return View(subscriptionPlan);
        }

        // POST: SubscriptionPlans/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PlanId,PlanName,MonthlyPrice,MaxTablesAllowed,IsActive")] SubscriptionPlan subscriptionPlan)
        {
            if (id != subscriptionPlan.PlanId)
            {
                return NotFound();
            }

            var existing = await _context.SubscriptionPlans.AsNoTracking().FirstOrDefaultAsync(p => p.PlanId == id);
            if (existing == null)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(subscriptionPlan);
                    _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
                    {
                        ActionType = "Edit",
                        EntityType = "SubscriptionPlan",
                        EntityId = subscriptionPlan.PlanId,
                        Details = $"Plan '{existing.PlanName}' updated to '{subscriptionPlan.PlanName}', price {existing.MonthlyPrice:0.00}->{subscriptionPlan.MonthlyPrice:0.00}, active {existing.IsActive}->{subscriptionPlan.IsActive}.",
                        ActorUserId = TryGetCurrentUserId(),
                        ActorName = User.Identity?.Name ?? "SuperAdmin",
                        CreatedAt = PhilippineTime.Now
                    });
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!SubscriptionPlanExists(subscriptionPlan.PlanId))
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
            return View(subscriptionPlan);
        }

        // GET: SubscriptionPlans/Delete — Redirects to Archive
        public IActionResult Delete(int? id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: SubscriptionPlans/Delete — Redirects to Archive
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: SubscriptionPlans/Archive/5 — Toggle IsActive (with audit log)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(id);
            if (plan == null) return NotFound();
            plan.IsActive = !plan.IsActive;
            var actionType = plan.IsActive ? "Restore" : "Archive";
            _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
            {
                ActionType = actionType,
                EntityType = "SubscriptionPlan",
                EntityId = plan.PlanId,
                Details = $"Plan \u2018{plan.PlanName}\u2019 {(plan.IsActive ? "restored" : "archived")}.",
                ActorUserId = TryGetCurrentUserId(),
                ActorName = User.Identity?.Name ?? "SuperAdmin",
                CreatedAt = PhilippineTime.Now
            });
            await _context.SaveChangesAsync();
            TempData["Success"] = plan.IsActive ? $"Plan \u2018{plan.PlanName}\u2019 has been restored." : $"Plan \u2018{plan.PlanName}\u2019 has been archived.";
            return RedirectToAction(nameof(Index));
        }

        private bool SubscriptionPlanExists(int id)
        {
            return _context.SubscriptionPlans.Any(e => e.PlanId == id);
        }

        private int? TryGetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}

