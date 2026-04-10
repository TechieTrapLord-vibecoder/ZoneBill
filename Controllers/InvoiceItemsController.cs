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
    public class InvoiceItemsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoiceItemsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: InvoiceItems
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceItems = _context.InvoiceItems
                .Include(i => i.Invoice)
                .Where(i => i.Invoice.BusinessId == businessId.Value);

            return View(await invoiceItems.ToListAsync());
        }

        // GET: InvoiceItems/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceItem = await _context.InvoiceItems
                .Include(i => i.Invoice)
                .FirstOrDefaultAsync(m => m.InvoiceItemId == id && m.Invoice.BusinessId == businessId.Value);
            if (invoiceItem == null)
            {
                return NotFound();
            }

            return View(invoiceItem);
        }

        // GET: InvoiceItems/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus");
            return View();
        }

        // POST: InvoiceItems/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("InvoiceItemId,InvoiceId,ItemType,Description,Quantity,UnitPrice,Total")] InvoiceItem invoiceItem)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceExists = await _context.Invoices.AnyAsync(i => i.InvoiceId == invoiceItem.InvoiceId && i.BusinessId == businessId.Value);
            if (!invoiceExists)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                _context.Add(invoiceItem);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", invoiceItem.InvoiceId);
            return View(invoiceItem);
        }

        // GET: InvoiceItems/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceItem = await _context.InvoiceItems
                .Include(ii => ii.Invoice)
                .FirstOrDefaultAsync(ii => ii.InvoiceItemId == id && ii.Invoice.BusinessId == businessId.Value);
            if (invoiceItem == null)
            {
                return NotFound();
            }
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", invoiceItem.InvoiceId);
            return View(invoiceItem);
        }

        // POST: InvoiceItems/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("InvoiceItemId,InvoiceId,ItemType,Description,Quantity,UnitPrice,Total")] InvoiceItem invoiceItem)
        {
            if (id != invoiceItem.InvoiceItemId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceExists = await _context.Invoices.AnyAsync(i => i.InvoiceId == invoiceItem.InvoiceId && i.BusinessId == businessId.Value);
            if (!invoiceExists)
            {
                return Forbid();
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(invoiceItem);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!InvoiceItemExists(invoiceItem.InvoiceItemId))
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
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", invoiceItem.InvoiceId);
            return View(invoiceItem);
        }

        // GET: InvoiceItems/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceItem = await _context.InvoiceItems
                .Include(i => i.Invoice)
                .FirstOrDefaultAsync(m => m.InvoiceItemId == id && m.Invoice.BusinessId == businessId.Value);
            if (invoiceItem == null)
            {
                return NotFound();
            }

            return View(invoiceItem);
        }

        // POST: InvoiceItems/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoiceItem = await _context.InvoiceItems
                .Include(ii => ii.Invoice)
                .FirstOrDefaultAsync(ii => ii.InvoiceItemId == id && ii.Invoice.BusinessId == businessId.Value);
            if (invoiceItem != null)
            {
                _context.InvoiceItems.Remove(invoiceItem);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool InvoiceItemExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.InvoiceItems.Any(e => e.InvoiceItemId == id && e.Invoice.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

