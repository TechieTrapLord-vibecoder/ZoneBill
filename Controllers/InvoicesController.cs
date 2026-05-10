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
    public class InvoicesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InvoicesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Invoices
        public async Task<IActionResult> Index(string? search, string? status, DateTime? fromDate, DateTime? toDate, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;

            var invoices = _context.Invoices
                .Include(i => i.Booking)
                .Include(i => i.Business)
                .Where(i => i.BusinessId == businessId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out var numericSearch))
                {
                    invoices = invoices.Where(i => i.InvoiceId == numericSearch || i.BookingId == numericSearch);
                }
                else
                {
                    invoices = invoices.Where(i =>
                        (i.Booking != null && i.Booking.ReferenceCode != null && i.Booking.ReferenceCode.Contains(trimmedSearch)) ||
                        (i.PaymentStatus != null && i.PaymentStatus.Contains(trimmedSearch)));
                }
            }

            if (!string.IsNullOrWhiteSpace(status))
            {
                if (status == "overdue")
                {
                    var overdueCutoff = PhilippineTime.Now.Date.AddDays(-3);
                    invoices = invoices.Where(i => i.PaymentStatus != "Paid" && i.GeneratedDate < overdueCutoff);
                }
                else if (status == "Unpaid")
                {
                    invoices = invoices.Where(i => i.PaymentStatus != "Paid");
                }
                else
                {
                    invoices = invoices.Where(i => i.PaymentStatus == status);
                }
            }

            if (fromDate.HasValue)
            {
                invoices = invoices.Where(i => i.GeneratedDate >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var endDateExclusive = toDate.Value.Date.AddDays(1);
                invoices = invoices.Where(i => i.GeneratedDate < endDateExclusive);
            }

            ViewBag.TotalRevenue = await invoices.SumAsync(i => (decimal?)i.TotalAmount) ?? 0m;
            ViewBag.PaidCount = await invoices.CountAsync(i => i.PaymentStatus == "Paid");
            ViewBag.UnpaidCount = await invoices.CountAsync(i => i.PaymentStatus != "Paid");
            var overdueCutoffForSummary = PhilippineTime.Now.Date.AddDays(-3);
            ViewBag.OverdueCount = await invoices.CountAsync(i => i.PaymentStatus != "Paid" && i.GeneratedDate < overdueCutoffForSummary);

            var totalCount = await invoices.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            ViewBag.Search = search;
            ViewBag.Status = status;
            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(await invoices
                .OrderByDescending(i => i.GeneratedDate)
                .ThenByDescending(i => i.InvoiceId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync());
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
            TempData["Error"] = "Manual invoice creation is disabled. Invoices are auto-generated from POS checkout.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Invoices/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult Create([Bind("InvoiceId,BusinessId,BookingId,SubTotal,DiscountAmount,TaxAmount,TotalAmount,TaxRateApplied,PaymentStatus,GeneratedDate")] Invoice invoice)
        {
            TempData["Error"] = "Manual invoice creation is disabled. Use POS checkout to generate invoices.";
            return RedirectToAction(nameof(Index));
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

        // GET: Invoices/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Invoice records are kept for financial audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Invoices/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Invoice records are kept for financial audit purposes.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Invoices/BulkMarkPaid
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BulkMarkPaid(int[] invoiceIds)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (invoiceIds != null && invoiceIds.Length > 0)
            {
                var invoices = await _context.Invoices
                    .Where(i => invoiceIds.Contains(i.InvoiceId) && i.BusinessId == businessId.Value && i.PaymentStatus != "Paid")
                    .ToListAsync();

                var cashAccount = await GetOrCreateAccountAsync(businessId.Value, "Cash", "Asset");
                var accountsReceivable = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");

                foreach (var inv in invoices)
                {
                    var payment = new Payment
                    {
                        BusinessId = businessId.Value,
                        InvoiceId = inv.InvoiceId,
                        AmountPaid = inv.TotalAmount,
                        PaymentMethod = "Manual",
                        PaymentDate = PhilippineTime.Now,
                        ReferenceNumber = "Bulk mark paid"
                    };

                    var journalEntry = new JournalEntry
                    {
                        BusinessId = businessId.Value,
                        ReferenceId = inv.InvoiceId,
                        ReferenceType = "Payment",
                        EntryDate = payment.PaymentDate,
                        Description = $"Manual bulk payment for Invoice #{inv.InvoiceId}"
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

                    inv.PaymentStatus = "Paid";
                    _context.Payments.Add(payment);
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Marked {invoices.Count} invoice(s) as paid and created matching payment records.";
            }

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

