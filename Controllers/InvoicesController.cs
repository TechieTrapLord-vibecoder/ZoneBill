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
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Invoices
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoices = _context.Invoices
                .Include(i => i.Booking)
                .Include(i => i.Business)
                .Where(i => i.BusinessId == businessId.Value);

            return View(await invoices.ToListAsync());
        }

        // GET: Invoices/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices
                .Include(i => i.Booking)
                .Include(i => i.Business)
                .FirstOrDefaultAsync(m => m.InvoiceId == id && m.BusinessId == businessId.Value);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // GET: Invoices/Receipt/5
        public async Task<IActionResult> Receipt(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices
                .Include(i => i.Booking)
                .Include(i => i.Business)
                .FirstOrDefaultAsync(i => i.InvoiceId == id && i.BusinessId == businessId.Value);
            if (invoice == null)
            {
                return NotFound();
            }

            var items = await _context.InvoiceItems
                .Where(ii => ii.InvoiceId == invoice.InvoiceId)
                .OrderBy(ii => ii.InvoiceItemId)
                .ToListAsync();

            var payments = await _context.Payments
                .Where(p => p.BusinessId == businessId.Value && p.InvoiceId == invoice.InvoiceId)
                .OrderByDescending(p => p.PaymentDate)
                .ToListAsync();

            var adjustments = await _context.Adjustments
                .Where(a => a.InvoiceId == invoice.InvoiceId)
                .ToListAsync();

            var paidAmount = payments.Sum(p => p.AmountPaid);
            var adjustmentSum = adjustments
                .Where(a => a.AdjustmentType == "Debit").Sum(a => a.Amount)
                - adjustments
                .Where(a => a.AdjustmentType == "Credit").Sum(a => a.Amount);

            var lookupUrl = Url.Action(nameof(Details), "Invoices", new { id = invoice.InvoiceId }, Request.Scheme) ?? string.Empty;
            var viewModel = new InvoiceReceiptViewModel
            {
                Invoice = invoice,
                Items = items,
                Payments = payments,
                Adjustments = adjustments,
                PaidAmount = paidAmount,
                AdjustmentSum = adjustmentSum,
                Balance = Math.Max(0m, invoice.TotalAmount + adjustmentSum - paidAmount),
                InvoiceLookupUrl = lookupUrl
            };

            return View(viewModel);
        }

        // GET: Invoices/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus");
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            return View();
        }

        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("InvoiceId,BusinessId,BookingId,SubTotal,DiscountAmount,TaxAmount,TotalAmount,TaxRateApplied,PaymentStatus,GeneratedDate")] Invoice invoice)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            invoice.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(invoice);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", invoice.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", invoice.BusinessId);
            return View(invoice);
        }

        // GET: Invoices/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.BusinessId == businessId.Value);
            if (invoice == null)
            {
                return NotFound();
            }
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", invoice.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", invoice.BusinessId);
            return View(invoice);
        }

        // POST: Invoices/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InvoiceId,BusinessId,BookingId,SubTotal,DiscountAmount,TaxAmount,TotalAmount,TaxRateApplied,PaymentStatus,GeneratedDate")] Invoice invoice)
        {
            if (id != invoice.InvoiceId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            invoice.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(invoice);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceExists(invoice.InvoiceId))
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
            ViewData["BookingId"] = new SelectList(_context.Bookings.Where(b => b.BusinessId == businessId.Value), "BookingId", "BookingStatus", invoice.BookingId);
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", invoice.BusinessId);
            return View(invoice);
        }

        // GET: Invoices/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices
                .Include(i => i.Booking)
                .Include(i => i.Business)
                .FirstOrDefaultAsync(m => m.InvoiceId == id && m.BusinessId == businessId.Value);
            if (invoice == null)
            {
                return NotFound();
            }

            return View(invoice);
        }

        // POST: Invoices/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == id && i.BusinessId == businessId.Value);
            if (invoice != null)
            {
                _context.Invoices.Remove(invoice);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Invoices.Any(e => e.InvoiceId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

