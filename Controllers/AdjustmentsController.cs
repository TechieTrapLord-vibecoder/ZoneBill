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
    public class AdjustmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdjustmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Adjustments
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var adjustments = _context.Adjustments
                .Include(a => a.Invoice)
                .Where(a => a.Invoice.BusinessId == businessId.Value);

            return View(await adjustments.ToListAsync());
        }

        // GET: Adjustments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var adjustment = await _context.Adjustments
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id && m.Invoice.BusinessId == businessId.Value);
            if (adjustment == null)
            {
                return NotFound();
            }

            return View(adjustment);
        }

        // GET: Adjustments/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            PopulateInvoiceDropdown(businessId.Value);
            return View();
        }

        // POST: Adjustments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AdjustmentId,InvoiceId,AdjustmentType,Amount,Reason")] Adjustment adjustment)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceExists = await _context.Invoices.AnyAsync(i => i.InvoiceId == adjustment.InvoiceId && i.BusinessId == businessId.Value);
            if (!invoiceExists) return Forbid();

            if (ModelState.IsValid)
            {
                _context.Add(adjustment);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            PopulateInvoiceDropdown(businessId.Value, adjustment.InvoiceId);
            return View(adjustment);
        }

        // GET: Adjustments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var adjustment = await _context.Adjustments
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(a => a.AdjustmentId == id && a.Invoice.BusinessId == businessId.Value);
            if (adjustment == null)
            {
                return NotFound();
            }
            PopulateInvoiceDropdown(businessId.Value, adjustment.InvoiceId);
            return View(adjustment);
        }

        // POST: Adjustments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AdjustmentId,InvoiceId,AdjustmentType,Amount,Reason")] Adjustment adjustment)
        {
            if (id != adjustment.AdjustmentId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceExists = await _context.Invoices.AnyAsync(i => i.InvoiceId == adjustment.InvoiceId && i.BusinessId == businessId.Value);
            if (!invoiceExists) return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(adjustment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!AdjustmentExists(adjustment.AdjustmentId))
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
            PopulateInvoiceDropdown(businessId.Value, adjustment.InvoiceId);
            return View(adjustment);
        }

        // GET: Adjustments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var adjustment = await _context.Adjustments
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(m => m.AdjustmentId == id && m.Invoice.BusinessId == businessId.Value);
            if (adjustment == null)
            {
                return NotFound();
            }

            return View(adjustment);
        }

        // POST: Adjustments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var adjustment = await _context.Adjustments
                .Include(a => a.Invoice)
                .FirstOrDefaultAsync(a => a.AdjustmentId == id && a.Invoice.BusinessId == businessId.Value);
            if (adjustment != null)
            {
                _context.Adjustments.Remove(adjustment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool AdjustmentExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Adjustments.Any(e => e.AdjustmentId == id && e.Invoice.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private void PopulateInvoiceDropdown(int businessId, int? selectedId = null)
        {
            var invoices = _context.Invoices
                .Where(i => i.BusinessId == businessId)
                .OrderByDescending(i => i.InvoiceId)
                .Select(i => new { i.InvoiceId, i.TotalAmount, i.PaymentStatus })
                .ToList()
                .Select(i => new { i.InvoiceId, Display = $"Invoice #{i.InvoiceId} \u2014 \u20B1{i.TotalAmount:N2} ({i.PaymentStatus})" })
                .ToList();
            ViewData["InvoiceId"] = new SelectList(invoices, "InvoiceId", "Display", selectedId);
        }
    }
}

