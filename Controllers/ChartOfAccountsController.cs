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
    public class ChartOfAccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ChartOfAccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: ChartOfAccounts
        public async Task<IActionResult> Index(int page = 1)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            const int pageSize = 10;
            var query = _context.ChartOfAccounts
                .Include(c => c.Business)
                .Where(c => c.BusinessId == businessId.Value);
            var totalCount = await query.CountAsync();
            var activeCount = await query.CountAsync(c => c.IsActive);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.ActiveCount = activeCount;
            return View(await query.OrderBy(c => c.AccountType).ThenBy(c => c.AccountName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        // GET: ChartOfAccounts/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var chartOfAccount = await _context.ChartOfAccounts
                .Include(c => c.Business)
                .FirstOrDefaultAsync(m => m.AccountId == id && m.BusinessId == businessId.Value);
            if (chartOfAccount == null)
            {
                return NotFound();
            }

            return View(chartOfAccount);
        }

        // GET: ChartOfAccounts/Create
        public IActionResult Create()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName");
            return View();
        }

        // POST: ChartOfAccounts/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("AccountId,BusinessId,AccountName,AccountType,IsActive")] ChartOfAccount chartOfAccount)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            chartOfAccount.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                _context.Add(chartOfAccount);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", chartOfAccount.BusinessId);
            return View(chartOfAccount);
        }

        // GET: ChartOfAccounts/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var chartOfAccount = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountId == id && c.BusinessId == businessId.Value);
            if (chartOfAccount == null)
            {
                return NotFound();
            }
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", chartOfAccount.BusinessId);
            return View(chartOfAccount);
        }

        // POST: ChartOfAccounts/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("AccountId,BusinessId,AccountName,AccountType,IsActive")] ChartOfAccount chartOfAccount)
        {
            if (id != chartOfAccount.AccountId)
            {
                return NotFound();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            chartOfAccount.BusinessId = businessId.Value;

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(chartOfAccount);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!ChartOfAccountExists(chartOfAccount.AccountId))
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
            ViewData["BusinessId"] = new SelectList(_context.Businesses.Where(b => b.BusinessId == businessId.Value), "BusinessId", "BusinessName", chartOfAccount.BusinessId);
            return View(chartOfAccount);
        }

        // GET: ChartOfAccounts/Delete — Redirects to Archive
        public IActionResult Delete(int? id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: ChartOfAccounts/Delete — Redirects to Archive
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            return RedirectToAction(nameof(Index));
        }

        // POST: ChartOfAccounts/Archive/5 — Toggle IsActive
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Archive(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();
            var account = await _context.ChartOfAccounts.FirstOrDefaultAsync(c => c.AccountId == id && c.BusinessId == businessId.Value);
            if (account == null) return NotFound();
            account.IsActive = !account.IsActive;
            await _context.SaveChangesAsync();
            TempData["Success"] = account.IsActive ? $"Account \u2018{account.AccountName}\u2019 has been restored." : $"Account \u2018{account.AccountName}\u2019 has been archived.";
            return RedirectToAction(nameof(Index));
        }

        private bool ChartOfAccountExists(int id)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return false;
            return _context.ChartOfAccounts.Any(e => e.AccountId == id && e.BusinessId == businessId.Value);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}

