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
    public class AdjustmentsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AdjustmentsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Adjustments
        public async Task<IActionResult> Index(string? search, string? adjustmentType, int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;

            var adjustments = _context.Adjustments
                .Include(a => a.Invoice)
                .Where(a => a.Invoice.BusinessId == businessId.Value)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                var trimmedSearch = search.Trim();
                if (int.TryParse(trimmedSearch, out var invoiceId))
                {
                    adjustments = adjustments.Where(a => a.InvoiceId == invoiceId);
                }
                else
                {
                    adjustments = adjustments.Where(a =>
                        (a.Reason != null && a.Reason.Contains(trimmedSearch)) ||
                        (a.Invoice.PaymentStatus != null && a.Invoice.PaymentStatus.Contains(trimmedSearch)));
                }
            }

            if (!string.IsNullOrWhiteSpace(adjustmentType))
            {
                adjustments = adjustments.Where(a => a.AdjustmentType == adjustmentType);
            }

            var totalCount = await adjustments.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Min(Math.Max(page, 1), totalPages);

            ViewBag.Search = search;
            ViewBag.AdjustmentType = adjustmentType;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(await adjustments
                .OrderByDescending(a => a.AdjustmentId)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync());
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

                // Post journal entry for the adjustment
                var adjustmentsAccount = await GetOrCreateAccountAsync(businessId.Value, "Sales Adjustments", "Expense");
                var arAccount = await GetOrCreateAccountAsync(businessId.Value, "Accounts Receivable", "Asset");

                var je = new JournalEntry
                {
                    BusinessId = businessId.Value,
                    ReferenceId = adjustment.AdjustmentId,
                    ReferenceType = "Adjustment",
                    EntryDate = PhilippineTime.Now,
                    Description = $"{adjustment.AdjustmentType} adjustment of \u20b1{adjustment.Amount:N2} on Invoice #{adjustment.InvoiceId}" +
                                  (!string.IsNullOrWhiteSpace(adjustment.Reason) ? $": {adjustment.Reason}" : "")
                };
                _context.JournalEntries.Add(je);
                await _context.SaveChangesAsync();

                bool isCredit = adjustment.AdjustmentType.Equals("Credit", StringComparison.OrdinalIgnoreCase);
                _context.JournalEntryLines.AddRange(
                    new JournalEntryLine
                    {
                        JournalEntryId = je.JournalEntryId,
                        AccountId = isCredit ? adjustmentsAccount.AccountId : arAccount.AccountId,
                        Debit = adjustment.Amount,
                        Credit = 0m
                    },
                    new JournalEntryLine
                    {
                        JournalEntryId = je.JournalEntryId,
                        AccountId = isCredit ? arAccount.AccountId : adjustmentsAccount.AccountId,
                        Debit = 0m,
                        Credit = adjustment.Amount
                    });
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

        // GET: Adjustments/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Financial records cannot be removed.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Adjustments/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Financial records cannot be removed.";
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

        private async Task<ChartOfAccount> GetOrCreateAccountAsync(int businessId, string accountName, string accountType)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.BusinessId == businessId && a.AccountName == accountName);
            if (account != null) return account;
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

        private void PopulateInvoiceDropdown(int businessId, int? selectedId = null)
        {
            var invoices = _context.Invoices
                .Where(i => i.BusinessId == businessId)
                .OrderByDescending(i => i.InvoiceId)
                .Select(i => new { i.InvoiceId, i.InvoiceNumber, i.TotalAmount, i.PaymentStatus })
                .ToList()
                .Select(i => new { i.InvoiceId, Display = $"{(string.IsNullOrEmpty(i.InvoiceNumber) ? $"#{i.InvoiceId}" : i.InvoiceNumber)} — ₱{i.TotalAmount:N2} ({i.PaymentStatus})" })
                .ToList();
            ViewData["InvoiceId"] = new SelectList(invoices, "InvoiceId", "Display", selectedId);
        }
    }
}

