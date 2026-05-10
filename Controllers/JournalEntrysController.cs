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
    [Authorize(Roles = "MainAdmin")]
    public class JournalEntrysController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JournalEntrysController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: JournalEntrys
        public async Task<IActionResult> Index(int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;
            var entriesQuery = _context.JournalEntries
                .Include(j => j.Business)
                .Where(j => j.BusinessId == businessId.Value)
                .OrderByDescending(j => j.EntryDate)
                .ThenByDescending(j => j.JournalEntryId);

            var totalCount = await entriesQuery.CountAsync();
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);

            var pagedEntries = await entriesQuery
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var entryIds = pagedEntries.Select(e => e.JournalEntryId).ToList();

            var lines = await _context.JournalEntryLines
                .Include(l => l.ChartOfAccount)
                .Where(l => entryIds.Contains(l.JournalEntryId))
                .OrderBy(l => l.JournalEntryId)
                .ThenBy(l => l.JournalLineId)
                .ToListAsync();

            var timelines = pagedEntries.Select(entry =>
            {
                var entryLines = lines.Where(l => l.JournalEntryId == entry.JournalEntryId).ToList();
                return new JournalEntryTimelineViewModel
                {
                    Entry = entry,
                    Lines = entryLines,
                    TotalDebit = entryLines.Sum(l => l.Debit),
                    TotalCredit = entryLines.Sum(l => l.Credit)
                };
            }).ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            return View(timelines);
        }

        // GET: JournalEntrys/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntry = await _context.JournalEntries
                .Include(j => j.Business)
                .FirstOrDefaultAsync(m => m.JournalEntryId == id && m.BusinessId == businessId.Value);
            if (journalEntry == null)
            {
                return NotFound();
            }

            return View(journalEntry);
        }

        // GET: JournalEntrys/Create
        public async Task<IActionResult> Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var model = new JournalEntryEditorViewModel
            {
                BusinessId = businessId.Value,
                EntryDate = PhilippineTime.Now
            };

            await PopulateEditorOptionsAsync(model, businessId.Value);
            return View(model);
        }

        // POST: JournalEntrys/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(JournalEntryEditorViewModel model)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            model.BusinessId = businessId.Value;
            NormalizeLines(model);
            ValidateLines(model);

            if (ModelState.IsValid)
            {
                var journalEntry = new JournalEntry
                {
                    BusinessId = businessId.Value,
                    ReferenceId = model.ReferenceId,
                    ReferenceType = string.IsNullOrWhiteSpace(model.ReferenceType) ? null : model.ReferenceType.Trim(),
                    EntryDate = model.EntryDate == default ? PhilippineTime.Now : model.EntryDate,
                    Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim()
                };

                _context.Add(journalEntry);
                await _context.SaveChangesAsync();

                _context.JournalEntryLines.AddRange(model.Lines.Select(line => new JournalEntryLine
                {
                    JournalEntryId = journalEntry.JournalEntryId,
                    AccountId = line.AccountId!.Value,
                    Debit = line.Debit,
                    Credit = line.Credit
                }));

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateEditorOptionsAsync(model, businessId.Value);
            return View(model);
        }

        // GET: JournalEntrys/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntry = await _context.JournalEntries.FirstOrDefaultAsync(j => j.JournalEntryId == id && j.BusinessId == businessId.Value);
            if (journalEntry == null)
            {
                return NotFound();
            }
            var model = new JournalEntryEditorViewModel
            {
                JournalEntryId = journalEntry.JournalEntryId,
                BusinessId = journalEntry.BusinessId,
                ReferenceId = journalEntry.ReferenceId,
                ReferenceType = journalEntry.ReferenceType,
                EntryDate = journalEntry.EntryDate,
                Description = journalEntry.Description,
                Lines = await _context.JournalEntryLines
                    .Where(l => l.JournalEntryId == journalEntry.JournalEntryId)
                    .OrderBy(l => l.JournalLineId)
                    .Select(l => new JournalEntryLineEditorViewModel
                    {
                        JournalLineId = l.JournalLineId,
                        AccountId = l.AccountId,
                        Debit = l.Debit,
                        Credit = l.Credit
                    })
                    .ToListAsync()
            };

            await PopulateEditorOptionsAsync(model, businessId.Value);
            return View(model);
        }

        // POST: JournalEntrys/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, JournalEntryEditorViewModel model)
        {
            if (id != model.JournalEntryId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            model.BusinessId = businessId.Value;
            NormalizeLines(model);
            ValidateLines(model);

            if (ModelState.IsValid)
            {
                var existingEntry = await _context.JournalEntries
                    .FirstOrDefaultAsync(j => j.JournalEntryId == id && j.BusinessId == businessId.Value);
                if (existingEntry == null)
                {
                    return NotFound();
                }

                try
                {
                    existingEntry.ReferenceId = model.ReferenceId;
                    existingEntry.ReferenceType = string.IsNullOrWhiteSpace(model.ReferenceType) ? null : model.ReferenceType.Trim();
                    existingEntry.EntryDate = model.EntryDate;
                    existingEntry.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

                    var existingLines = await _context.JournalEntryLines
                        .Where(l => l.JournalEntryId == existingEntry.JournalEntryId)
                        .ToListAsync();

                    _context.JournalEntryLines.RemoveRange(existingLines);
                    _context.JournalEntryLines.AddRange(model.Lines.Select(line => new JournalEntryLine
                    {
                        JournalEntryId = existingEntry.JournalEntryId,
                        AccountId = line.AccountId!.Value,
                        Debit = line.Debit,
                        Credit = line.Credit
                    }));

                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!JournalEntryExists(id))
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

            await PopulateEditorOptionsAsync(model, businessId.Value);
            return View(model);
        }

        // GET: JournalEntrys/Delete — Deletion disabled
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Journal entries are permanent ledger records.";
            return RedirectToAction(nameof(Index));
        }

        // POST: JournalEntrys/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Journal entries are permanent ledger records.";
            return RedirectToAction(nameof(Index));
        }

        private bool JournalEntryExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.JournalEntries.Any(e => e.JournalEntryId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private async Task PopulateEditorOptionsAsync(JournalEntryEditorViewModel model, int businessId)
        {
            model.AccountOptions = await _context.ChartOfAccounts
                .Where(a => a.BusinessId == businessId && a.IsActive)
                .OrderBy(a => a.AccountType)
                .ThenBy(a => a.AccountName)
                .Select(a => new SelectListItem
                {
                    Value = a.AccountId.ToString(),
                    Text = $"{a.AccountName} ({a.AccountType})"
                })
                .ToListAsync();

            while (model.Lines.Count < 8)
            {
                model.Lines.Add(new JournalEntryLineEditorViewModel());
            }
        }

        private static void NormalizeLines(JournalEntryEditorViewModel model)
        {
            model.Lines = model.Lines
                .Where(line => line.AccountId.HasValue || line.Debit > 0 || line.Credit > 0)
                .ToList();
        }

        private void ValidateLines(JournalEntryEditorViewModel model)
        {
            if (!model.Lines.Any())
            {
                ModelState.AddModelError(string.Empty, "Add at least two journal lines.");
                return;
            }

            var totalDebit = 0m;
            var totalCredit = 0m;

            for (var index = 0; index < model.Lines.Count; index++)
            {
                var line = model.Lines[index];
                if (!line.AccountId.HasValue)
                {
                    ModelState.AddModelError($"Lines[{index}].AccountId", "Select an account.");
                }

                if ((line.Debit <= 0 && line.Credit <= 0) || (line.Debit > 0 && line.Credit > 0))
                {
                    ModelState.AddModelError(string.Empty, $"Line {index + 1} must have either a debit or a credit amount.");
                }

                totalDebit += line.Debit;
                totalCredit += line.Credit;
            }

            if (model.Lines.Count < 2)
            {
                ModelState.AddModelError(string.Empty, "Add at least two journal lines.");
            }

            if (totalDebit != totalCredit)
            {
                ModelState.AddModelError(string.Empty, $"Entry is unbalanced. Debit {totalDebit:C} must equal Credit {totalCredit:C}.");
            }
        }
    }
}

