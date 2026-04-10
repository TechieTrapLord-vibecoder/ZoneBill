using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin,Manager,Cashier")]
    public class PaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PaymentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Payments
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payments = _context.Payments
                .Include(p => p.Business)
                .Include(p => p.Invoice)
                .Where(p => p.BusinessId == businessId.Value);

            return View(await payments.ToListAsync());
        }

        // GET: Payments/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payment = await _context.Payments
                .Include(p => p.Business)
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(m => m.PaymentId == id && m.BusinessId == businessId.Value);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // GET: Payments/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceiveFromInvoice(int invoiceId, string paymentMethod = "Cash", string? referenceNumber = null)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var invoice = await _context.Invoices
                .FirstOrDefaultAsync(i => i.InvoiceId == invoiceId && i.BusinessId == businessId.Value);

            if (invoice == null)
            {
                return NotFound();
            }

            if (invoice.PaymentStatus == "Paid")
            {
                TempData["Error"] = "Invoice is already marked as paid.";
                return RedirectToAction("Details", "Invoices", new { id = invoiceId });
            }

            var normalizedPaymentMethod = string.IsNullOrWhiteSpace(paymentMethod) ? "Cash" : paymentMethod.Trim();
            var normalizedReferenceNumber = string.IsNullOrWhiteSpace(referenceNumber) ? null : referenceNumber.Trim();

            var payment = new Payment
            {
                BusinessId = businessId.Value,
                InvoiceId = invoice.InvoiceId,
                AmountPaid = invoice.TotalAmount,
                PaymentMethod = normalizedPaymentMethod,
                PaymentDate = PhilippineTime.Now,
                ReferenceNumber = normalizedReferenceNumber
            };

            var cashAccountName = normalizedPaymentMethod.Equals("GCash", StringComparison.OrdinalIgnoreCase)
                ? "GCash Wallet"
                : normalizedPaymentMethod.Equals("Card", StringComparison.OrdinalIgnoreCase)
                    ? "Card Clearing"
                    : "Cash";

            var cashAccount = await GetOrCreateAccountAsync(businessId.Value, cashAccountName, "Asset");
            var accountsReceivable = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");

            var journalEntry = new JournalEntry
            {
                BusinessId = businessId.Value,
                ReferenceId = invoice.InvoiceId,
                ReferenceType = "Payment",
                EntryDate = PhilippineTime.Now,
                Description = $"Payment received for Invoice #{invoice.InvoiceId} via {normalizedPaymentMethod}"
            };

            _context.JournalEntries.Add(journalEntry);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.AddRange(
                new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = cashAccount.AccountId,
                    Debit = payment.AmountPaid,
                    Credit = 0m
                },
                new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = accountsReceivable.AccountId,
                    Debit = 0m,
                    Credit = payment.AmountPaid
                });

            invoice.PaymentStatus = "Paid";
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment received and invoice marked as Paid.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
        }

        // POST: Payments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("PaymentId,BusinessId,InvoiceId,AmountPaid,PaymentMethod,PaymentDate,ReferenceNumber")] Payment payment)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            payment.BusinessId = businessId.Value;
            payment.PaymentDate = payment.PaymentDate == default ? PhilippineTime.Now : payment.PaymentDate;

            if (ModelState.IsValid)
            {
                _context.Add(payment);

                var invoice = await _context.Invoices.FirstOrDefaultAsync(i => i.InvoiceId == payment.InvoiceId && i.BusinessId == businessId.Value);
                if (invoice != null && payment.AmountPaid >= invoice.TotalAmount)
                {
                    invoice.PaymentStatus = "Paid";
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", payment.BusinessId);
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", payment.InvoiceId);
            return View(payment);
        }

        // GET: Payments/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == id && p.BusinessId == businessId.Value);
            if (payment == null)
            {
                return NotFound();
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", payment.BusinessId);
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", payment.InvoiceId);
            return View(payment);
        }

        // POST: Payments/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("PaymentId,BusinessId,InvoiceId,AmountPaid,PaymentMethod,PaymentDate,ReferenceNumber")] Payment payment)
        {
            if (id != payment.PaymentId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            payment.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(payment);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!PaymentExists(payment.PaymentId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", payment.BusinessId);
            ViewData["InvoiceId"] = new SelectList(_context.Invoices.Where(i => i.BusinessId == businessId.Value), "InvoiceId", "PaymentStatus", payment.InvoiceId);
            return View(payment);
        }

        // GET: Payments/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payment = await _context.Payments
                .Include(p => p.Business)
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(m => m.PaymentId == id && m.BusinessId == businessId.Value);
            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        // POST: Payments/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payment = await _context.Payments.FirstOrDefaultAsync(p => p.PaymentId == id && p.BusinessId == businessId.Value);
            if (payment != null)
            {
                _context.Payments.Remove(payment);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Payments.Any(e => e.PaymentId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private async Task<ChartOfAccount> GetOrCreateAccountAsync(int businessId, string accountName, string accountType)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.BusinessId == businessId && a.AccountName == accountName);

            if (account != null)
            {
                return account;
            }

            account = new ChartOfAccount
            {
                BusinessId = businessId,
                AccountName = accountName,
                AccountType = accountType,
                IsActive = true
            };

            _context.ChartOfAccounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }
    }
}

