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
    public class JournalEntryLinesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JournalEntryLinesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: JournalEntryLines
        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var lines = _context.JournalEntryLines
                .Include(j => j.ChartOfAccount)
                .Include(j => j.JournalEntry)
                .Where(j => j.JournalEntry.BusinessId == businessId.Value);

            return View(await lines.ToListAsync());
        }

        // GET: JournalEntryLines/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntryLine = await _context.JournalEntryLines
                .Include(j => j.ChartOfAccount)
                .Include(j => j.JournalEntry)
                .FirstOrDefaultAsync(m => m.JournalLineId == id && m.JournalEntry.BusinessId == businessId.Value);
            if (journalEntryLine == null)
            {
                return NotFound();
            }

            return View(journalEntryLine);
        }

        // GET: JournalEntryLines/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            ViewData["AccountId"] = new SelectList(_context.ChartOfAccounts.Where(a => a.BusinessId == businessId.Value), "AccountId", "AccountName");
            ViewData["JournalEntryId"] = new SelectList(_context.JournalEntries.Where(j => j.BusinessId == businessId.Value), "JournalEntryId", "JournalEntryId");
            return View();
        }

        // POST: JournalEntryLines/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("JournalLineId,JournalEntryId,AccountId,Debit,Credit")] JournalEntryLine journalEntryLine)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var validEntry = await _context.JournalEntries.AnyAsync(j => j.JournalEntryId == journalEntryLine.JournalEntryId && j.BusinessId == businessId.Value);
            var validAccount = await _context.ChartOfAccounts.AnyAsync(a => a.AccountId == journalEntryLine.AccountId && a.BusinessId == businessId.Value);
            if (!validEntry || !validAccount) return Forbid();

            if (ModelState.IsValid)
            {
                _context.Add(journalEntryLine);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["AccountId"] = new SelectList(_context.ChartOfAccounts.Where(a => a.BusinessId == businessId.Value), "AccountId", "AccountName", journalEntryLine.AccountId);
            ViewData["JournalEntryId"] = new SelectList(_context.JournalEntries.Where(j => j.BusinessId == businessId.Value), "JournalEntryId", "JournalEntryId", journalEntryLine.JournalEntryId);
            return View(journalEntryLine);
        }

        // GET: JournalEntryLines/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntryLine = await _context.JournalEntryLines
                .Include(j => j.JournalEntry)
                .FirstOrDefaultAsync(j => j.JournalLineId == id && j.JournalEntry.BusinessId == businessId.Value);
            if (journalEntryLine == null)
            {
                return NotFound();
            }
            ViewData["AccountId"] = new SelectList(_context.ChartOfAccounts.Where(a => a.BusinessId == businessId.Value), "AccountId", "AccountName", journalEntryLine.AccountId);
            ViewData["JournalEntryId"] = new SelectList(_context.JournalEntries.Where(j => j.BusinessId == businessId.Value), "JournalEntryId", "JournalEntryId", journalEntryLine.JournalEntryId);
            return View(journalEntryLine);
        }

        // POST: JournalEntryLines/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("JournalLineId,JournalEntryId,AccountId,Debit,Credit")] JournalEntryLine journalEntryLine)
        {
            if (id != journalEntryLine.JournalLineId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var validEntry = await _context.JournalEntries.AnyAsync(j => j.JournalEntryId == journalEntryLine.JournalEntryId && j.BusinessId == businessId.Value);
            var validAccount = await _context.ChartOfAccounts.AnyAsync(a => a.AccountId == journalEntryLine.AccountId && a.BusinessId == businessId.Value);
            if (!validEntry || !validAccount) return Forbid();

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(journalEntryLine);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!JournalEntryLineExists(journalEntryLine.JournalLineId))
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
            ViewData["AccountId"] = new SelectList(_context.ChartOfAccounts.Where(a => a.BusinessId == businessId.Value), "AccountId", "AccountName", journalEntryLine.AccountId);
            ViewData["JournalEntryId"] = new SelectList(_context.JournalEntries.Where(j => j.BusinessId == businessId.Value), "JournalEntryId", "JournalEntryId", journalEntryLine.JournalEntryId);
            return View(journalEntryLine);
        }

        // GET: JournalEntryLines/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntryLine = await _context.JournalEntryLines
                .Include(j => j.ChartOfAccount)
                .Include(j => j.JournalEntry)
                .FirstOrDefaultAsync(m => m.JournalLineId == id && m.JournalEntry.BusinessId == businessId.Value);
            if (journalEntryLine == null)
            {
                return NotFound();
            }

            return View(journalEntryLine);
        }

        // POST: JournalEntryLines/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var journalEntryLine = await _context.JournalEntryLines
                .Include(j => j.JournalEntry)
                .FirstOrDefaultAsync(j => j.JournalLineId == id && j.JournalEntry.BusinessId == businessId.Value);
            if (journalEntryLine != null)
            {
                _context.JournalEntryLines.Remove(journalEntryLine);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool JournalEntryLineExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.JournalEntryLines.Any(e => e.JournalLineId == id && e.JournalEntry.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

