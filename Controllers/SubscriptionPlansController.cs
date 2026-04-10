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
            return View(await _context.SubscriptionPlans.ToListAsync());
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

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(subscriptionPlan);
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

        // GET: SubscriptionPlans/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

        // POST: SubscriptionPlans/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var subscriptionPlan = await _context.SubscriptionPlans.FindAsync(id);
            if (subscriptionPlan != null)
            {
                _context.SubscriptionPlans.Remove(subscriptionPlan);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool SubscriptionPlanExists(int id)
        {
            return _context.SubscriptionPlans.Any(e => e.PlanId == id);
        }
    }
}

