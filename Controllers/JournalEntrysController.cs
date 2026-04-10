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
    [Authorize(Roles = "MainAdmin")]
    public class JournalEntrysController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JournalEntrysController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: JournalEntrys
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var entries = await _context.JournalEntries
                .Include(j => j.Business)
                .Where(j => j.BusinessId == businessId.Value)
                .OrderByDescending(j => j.EntryDate)
                .ThenByDescending(j => j.JournalEntryId)
                .ToListAsync();

            var entryIds = entries.Select(e => e.JournalEntryId).ToList();

            var lines = await _context.JournalEntryLines
                .Include(l => l.ChartOfAccount)
                .Where(l => entryIds.Contains(l.JournalEntryId))
                .OrderBy(l => l.JournalEntryId)
                .ThenBy(l => l.JournalLineId)
                .ToListAsync();

            var timelines = entries.Select(entry =>
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
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            return View();
        }

        // POST: JournalEntrys/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("JournalEntryId,BusinessId,ReferenceId,ReferenceType,EntryDate,Description")] JournalEntry journalEntry)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            journalEntry.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(journalEntry);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", journalEntry.BusinessId);
            return View(journalEntry);
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", journalEntry.BusinessId);
            return View(journalEntry);
        }

        // POST: JournalEntrys/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("JournalEntryId,BusinessId,ReferenceId,ReferenceType,EntryDate,Description")] JournalEntry journalEntry)
        {
            if (id != journalEntry.JournalEntryId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            journalEntry.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(journalEntry);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!JournalEntryExists(journalEntry.JournalEntryId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", journalEntry.BusinessId);
            return View(journalEntry);
        }

        // GET: JournalEntrys/Delete/5
        public async Task<IActionResult> Delete(int? id)
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

        // POST: JournalEntrys/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntry = await _context.JournalEntries.FirstOrDefaultAsync(j => j.JournalEntryId == id && j.BusinessId == businessId.Value);
            if (journalEntry != null)
            {
                _context.JournalEntries.Remove(journalEntry);
            }

            await _context.SaveChangesAsync();
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
    }
}

