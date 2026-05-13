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
        public async Task<IActionResult> Index(string? search, string? paymentMethod, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;

            var payments = _context.Payments
                .Include(p => p.Business)
                .Include(p => p.Invoice)
                .Where(p => p.BusinessId == businessId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out var invoiceId))
                {
                    payments = payments.Where(p => p.InvoiceId == invoiceId);
                }
                else
                {
                    payments = payments.Where(p =>
                        (p.ReferenceNumber != null && p.ReferenceNumber.Contains(trimmedSearch)) ||
                        (p.PaymentMethod != null && p.PaymentMethod.Contains(trimmedSearch)));
                }
            }

            if (!string.IsNullOrWhiteSpace(paymentMethod))
            {
                payments = payments.Where(p => p.PaymentMethod == paymentMethod);
            }

            if (fromDate.HasValue)
            {
                payments = payments.Where(p => p.PaymentDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                payments = payments.Where(p => p.PaymentDate < endDateExclusive);
            }

            ViewBag.TotalCollected = await payments.SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;
            ViewBag.CashCount = await payments.CountAsync(p => p.PaymentMethod == "Cash");
            ViewBag.GCashCount = await payments.CountAsync(p => p.PaymentMethod == "GCash");
            ViewBag.CardCount = await payments.CountAsync(p => p.PaymentMethod == "Card");

            var totalCount = await payments.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            ViewBag.Search = search;
            ViewBag.PaymentMethod = paymentMethod;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(await payments
                .OrderByDescending(p => p.PaymentDate)
                .ThenByDescending(p => p.PaymentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync());
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
            TempData["Error"] = "Manual payment creation is disabled. Payments are recorded from checkout or invoice collection.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Payments/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("PaymentId,BusinessId,InvoiceId,AmountPaid,PaymentMethod,PaymentDate,ReferenceNumber")] Payment payment)
        {
            TempData["Error"] = "Manual payment creation is disabled. Use POS checkout or receive payment from invoice.";
            return RedirectToAction(nameof(Index));
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

            var cashAccount          = await GetOrCreateAccountAsync(businessId.Value, cashAccountName,       "Asset");
            var accountsReceivable   = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");
            var salesRevenue         = await GetOrCreateAccountAsync(businessId.Value, "Sales Revenue",        "Revenue");

            // ── Step 1: Invoice recognition journal (Dr AR / Cr Sales Revenue / Cr Tax) ──
            var taxableBase = invoice.TotalAmount - invoice.TaxAmount;
            var taxPayable = invoice.TaxAmount > 0m
                ? await GetOrCreateAccountAsync(businessId.Value, "Output Tax Payable", "Liability")
                : null;

            var invoiceJournal = new JournalEntry
            {
                BusinessId   = businessId.Value,
                ReferenceId  = invoice.InvoiceId,
                ReferenceType = "Invoice",
                EntryDate    = PhilippineTime.Now,
                Description  = $"Invoice #{invoice.InvoiceId} — revenue recognised on collection"
            };
            _context.JournalEntries.Add(invoiceJournal);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntryId = invoiceJournal.JournalEntryId,
                AccountId      = accountsReceivable.AccountId,
                Debit          = invoice.TotalAmount,
                Credit         = 0m
            });
            _context.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntryId = invoiceJournal.JournalEntryId,
                AccountId      = salesRevenue.AccountId,
                Debit          = 0m,
                Credit         = taxableBase
            });
            if (taxPayable != null)
            {
                _context.JournalEntryLines.Add(new JournalEntryLine
                {
                    JournalEntryId = invoiceJournal.JournalEntryId,
                    AccountId      = taxPayable.AccountId,
                    Debit          = 0m,
                    Credit         = invoice.TaxAmount
                });
            }

            // ── Step 2: Payment collection journal (Dr Cash / Cr AR) ──
            var paymentJournal = new JournalEntry
            {
                BusinessId    = businessId.Value,
                ReferenceId   = invoice.InvoiceId,
                ReferenceType = "Payment",
                EntryDate     = PhilippineTime.Now,
                Description   = $"Payment received for Invoice #{invoice.InvoiceId} via {normalizedPaymentMethod}"
            };
            _context.JournalEntries.Add(paymentJournal);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.AddRange(
                new JournalEntryLine
                {
                    JournalEntryId = paymentJournal.JournalEntryId,
                    AccountId      = cashAccount.AccountId,
                    Debit          = payment.AmountPaid,
                    Credit         = 0m
                },
                new JournalEntryLine
                {
                    JournalEntryId = paymentJournal.JournalEntryId,
                    AccountId      = accountsReceivable.AccountId,
                    Debit          = 0m,
                    Credit         = payment.AmountPaid
                });

            invoice.PaymentStatus = "Paid";
            _context.Payments.Add(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Payment received and invoice marked as Paid.";
            return RedirectToAction("Details", "Invoices", new { id = invoiceId });
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

        // GET: Payments/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Payment records are kept for financial audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Payments/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Payment records are kept for financial audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        private bool PaymentExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.Payments.Any(e => e.PaymentId == id && e.BusinessId == businessId.Value);
        }

        // POST: Payments/Void/5  — MainAdmin and Manager only
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Roles = "MainAdmin,Manager")]
        public async Task<IActionResult> Void(int id, string? reason)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var payment = await _context.Payments
                .Include(p => p.Invoice)
                .FirstOrDefaultAsync(p => p.PaymentId == id && p.BusinessId == businessId.Value);

            if (payment == null) return NotFound();

            if (payment.Invoice == null)
            {
                TempData["Error"] = "Cannot void: linked invoice not found.";
                return RedirectToAction(nameof(Index));
            }

            // Reverse the original journal entry
            var cashAccountName = (payment.PaymentMethod ?? "Cash").Equals("GCash", StringComparison.OrdinalIgnoreCase)
                ? "GCash Wallet"
                : (payment.PaymentMethod ?? "Cash").Equals("Card", StringComparison.OrdinalIgnoreCase)
                    ? "Card Clearing"
                    : "Cash";

            var cashAccount = await GetOrCreateAccountAsync(businessId.Value, cashAccountName, "Asset");
            var accountsReceivable = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");

            var reversal = new JournalEntry
            {
                BusinessId = businessId.Value,
                ReferenceId = payment.PaymentId,
                ReferenceType = "PaymentVoid",
                EntryDate = PhilippineTime.Now,
                Description = $"VOID: Payment #{payment.PaymentId} reversed" +
                              (string.IsNullOrWhiteSpace(reason) ? "" : $" — {reason.Trim()}")
            };

            _context.JournalEntries.Add(reversal);
            await _context.SaveChangesAsync();

            _context.JournalEntryLines.AddRange(
                new JournalEntryLine
                {
                    JournalEntryId = reversal.JournalEntryId,
                    AccountId = accountsReceivable.AccountId,
                    Debit = payment.AmountPaid,
                    Credit = 0m
                },
                new JournalEntryLine
                {
                    JournalEntryId = reversal.JournalEntryId,
                    AccountId = cashAccount.AccountId,
                    Debit = 0m,
                    Credit = payment.AmountPaid
                });

            // Reset invoice status back to Unpaid
            payment.Invoice.PaymentStatus = "Unpaid";
            _context.Payments.Remove(payment);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Payment #{id} voided. Invoice reset to Unpaid.";
            return RedirectToAction(nameof(Index));
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

