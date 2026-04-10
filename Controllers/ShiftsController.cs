using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Filters;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin,Manager,Cashier")]
    [ServiceFilter(typeof(ActiveSubscriptionFilter))]
    public class ShiftsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ShiftsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            var userId = GetUserId();
            if (businessId == null || userId == null) return Forbid();

            var activeShift = await _context.PosShifts
                .Include(s => s.Cashier)
                .FirstOrDefaultAsync(s =>
                    s.BusinessId == businessId.Value &&
                    s.CashierId == userId.Value &&
                    s.Status == "Open");

            var model = new ShiftIndexViewModel();

            if (activeShift != null)
            {
                var now = PhilippineTime.Now;
                var cashSales = await _context.Payments
                    .Where(p =>
                        p.BusinessId == businessId.Value &&
                        p.PaymentDate >= activeShift.OpenedAt &&
                        p.PaymentDate <= now &&
                        p.PaymentMethod == "Cash")
                    .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

                var drawerTxns = await _context.CashDrawerTransactions
                    .Where(t => t.BusinessId == businessId.Value && t.ShiftId == activeShift.ShiftId)
                    .OrderByDescending(t => t.CreatedAt)
                    .Take(30)
                    .ToListAsync();

                model.ActiveShift = activeShift;
                model.ActiveShiftCashSales = cashSales;
                model.ActiveShiftCashIn = drawerTxns.Where(t => t.TransactionType == "CashIn").Sum(t => t.Amount);
                model.ActiveShiftCashOut = drawerTxns.Where(t => t.TransactionType == "CashOut").Sum(t => t.Amount);
                model.ActiveShiftTransactions = drawerTxns;
            }

            model.RecentShifts = await _context.PosShifts
                .Include(s => s.Cashier)
                .Where(s => s.BusinessId == businessId.Value)
                .OrderByDescending(s => s.OpenedAt)
                .Take(15)
                .ToListAsync();

            if (User.IsInRole("MainAdmin") || User.IsInRole("Manager"))
            {
                model.OpenShiftOptions = await _context.PosShifts
                    .Include(s => s.Cashier)
                    .Where(s => s.BusinessId == businessId.Value && s.Status == "Open")
                    .OrderByDescending(s => s.OpenedAt)
                    .Select(s => new ShiftOptionViewModel
                    {
                        ShiftId = s.ShiftId,
                        Label = $"#{s.ShiftId} - {s.Cashier.FirstName} {s.Cashier.LastName} ({s.Cashier.EmailAddress})"
                    })
                    .ToListAsync();

                model.ClosedShiftOptions = await _context.PosShifts
                    .Include(s => s.Cashier)
                    .Where(s => s.BusinessId == businessId.Value && s.Status == "Closed")
                    .OrderByDescending(s => s.ClosedAt)
                    .Take(30)
                    .Select(s => new ShiftOptionViewModel
                    {
                        ShiftId = s.ShiftId,
                        Label = $"#{s.ShiftId} - {s.Cashier.FirstName} {s.Cashier.LastName} ({s.Cashier.EmailAddress})"
                    })
                    .ToListAsync();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Open(OpenShiftRequest request)
        {
            var businessId = GetBusinessId();
            var userId = GetUserId();
            if (businessId == null || userId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter a valid opening cash amount.";
                return RedirectToAction(nameof(Index));
            }

            var hasOpenShift = await _context.PosShifts.AnyAsync(s =>
                s.BusinessId == businessId.Value &&
                s.CashierId == userId.Value &&
                s.Status == "Open");

            if (hasOpenShift)
            {
                TempData["Error"] = "You already have an open shift.";
                return RedirectToAction(nameof(Index));
            }

            _context.PosShifts.Add(new PosShift
            {
                BusinessId = businessId.Value,
                CashierId = userId.Value,
                OpenedAt = PhilippineTime.Now,
                OpeningCash = request.OpeningCash,
                ExpectedCash = request.OpeningCash,
                Status = "Open",
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim()
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Shift opened successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AddDrawerTransaction(CashDrawerTransactionRequest request)
        {
            var businessId = GetBusinessId();
            var userId = GetUserId();
            if (businessId == null || userId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter a valid drawer transaction.";
                return RedirectToAction(nameof(Index));
            }

            var shift = await _context.PosShifts.FirstOrDefaultAsync(s =>
                s.BusinessId == businessId.Value &&
                s.CashierId == userId.Value &&
                s.Status == "Open");

            if (shift == null)
            {
                TempData["Error"] = "No open shift found. Open a shift first.";
                return RedirectToAction(nameof(Index));
            }

            _context.CashDrawerTransactions.Add(new CashDrawerTransaction
            {
                BusinessId = businessId.Value,
                ShiftId = shift.ShiftId,
                TransactionType = request.TransactionType,
                Amount = request.Amount,
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? null : request.Notes.Trim(),
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();
            TempData["Success"] = "Cash drawer transaction saved.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Close(CloseShiftRequest request)
        {
            var businessId = GetBusinessId();
            var userId = GetUserId();
            if (businessId == null || userId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please enter a valid actual cash amount.";
                return RedirectToAction(nameof(Index));
            }

            var shift = await _context.PosShifts.FirstOrDefaultAsync(s =>
                s.BusinessId == businessId.Value &&
                s.CashierId == userId.Value &&
                s.Status == "Open");

            if (shift == null)
            {
                TempData["Error"] = "No open shift found.";
                return RedirectToAction(nameof(Index));
            }

            var expectedCash = await ComputeExpectedCashAsync(shift, businessId.Value, PhilippineTime.Now);
            var variance = request.ActualCash - expectedCash;

            shift.ClosedAt = PhilippineTime.Now;
            shift.Status = "Closed";
            shift.ExpectedCash = Math.Round(expectedCash, 2);
            shift.ActualCash = Math.Round(request.ActualCash, 2);
            shift.Variance = Math.Round(variance, 2);
            shift.Notes = string.IsNullOrWhiteSpace(request.Notes)
                ? shift.Notes
                : request.Notes.Trim();

            await _context.SaveChangesAsync();

            var varianceText = variance == 0m ? "balanced exactly" : variance > 0 ? $"over by {variance:C}" : $"short by {Math.Abs(variance):C}";
            TempData["Success"] = $"Shift closed successfully. Drawer is {varianceText}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "MainAdmin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForceClose(ForceCloseShiftRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid shift and actual cash value for force close.";
                return RedirectToAction(nameof(Index));
            }

            var shift = await _context.PosShifts.FirstOrDefaultAsync(s =>
                s.BusinessId == businessId.Value &&
                s.ShiftId == request.ShiftId &&
                s.Status == "Open");

            if (shift == null)
            {
                TempData["Error"] = "Open shift not found for force close.";
                return RedirectToAction(nameof(Index));
            }

            var expectedCash = await ComputeExpectedCashAsync(shift, businessId.Value, PhilippineTime.Now);
            var variance = request.ActualCash - expectedCash;

            shift.ClosedAt = PhilippineTime.Now;
            shift.Status = "Closed";
            shift.ExpectedCash = Math.Round(expectedCash, 2);
            shift.ActualCash = Math.Round(request.ActualCash, 2);
            shift.Variance = Math.Round(variance, 2);
            shift.Notes = CombineNotes(shift.Notes, string.IsNullOrWhiteSpace(request.Notes) ? "Force closed by manager." : $"Force closed by manager: {request.Notes.Trim()}");

            await _context.SaveChangesAsync();

            var varianceText = variance == 0m ? "balanced exactly" : variance > 0 ? $"over by {variance:C}" : $"short by {Math.Abs(variance):C}";
            TempData["Success"] = $"Shift #{shift.ShiftId} force closed. Drawer is {varianceText}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Roles = "MainAdmin,Manager")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reopen(ReopenShiftRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid shift to reopen.";
                return RedirectToAction(nameof(Index));
            }

            var shift = await _context.PosShifts.FirstOrDefaultAsync(s =>
                s.BusinessId == businessId.Value &&
                s.ShiftId == request.ShiftId &&
                s.Status == "Closed");

            if (shift == null)
            {
                TempData["Error"] = "Closed shift not found.";
                return RedirectToAction(nameof(Index));
            }

            var cashierHasOpenShift = await _context.PosShifts.AnyAsync(s =>
                s.BusinessId == businessId.Value &&
                s.CashierId == shift.CashierId &&
                s.Status == "Open" &&
                s.ShiftId != shift.ShiftId);

            if (cashierHasOpenShift)
            {
                TempData["Error"] = "Cannot reopen: this cashier already has another open shift.";
                return RedirectToAction(nameof(Index));
            }

            shift.Status = "Open";
            shift.ClosedAt = null;
            shift.ActualCash = null;
            shift.Variance = null;
            shift.Notes = CombineNotes(shift.Notes, string.IsNullOrWhiteSpace(request.Notes) ? "Reopened by manager." : $"Reopened by manager: {request.Notes.Trim()}");

            await _context.SaveChangesAsync();
            TempData["Success"] = $"Shift #{shift.ShiftId} reopened successfully.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private int? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(value, out var userId) ? userId : null;
        }

        private async Task<decimal> ComputeExpectedCashAsync(PosShift shift, int businessId, DateTime now)
        {
            var cashSales = await _context.Payments
                .Where(p =>
                    p.BusinessId == businessId &&
                    p.PaymentDate >= shift.OpenedAt &&
                    p.PaymentDate <= now &&
                    p.PaymentMethod == "Cash")
                .SumAsync(p => (decimal?)p.AmountPaid) ?? 0m;

            var drawerTxns = await _context.CashDrawerTransactions
                .Where(t => t.BusinessId == businessId && t.ShiftId == shift.ShiftId)
                .ToListAsync();

            var cashIn = drawerTxns.Where(t => t.TransactionType == "CashIn").Sum(t => t.Amount);
            var cashOut = drawerTxns.Where(t => t.TransactionType == "CashOut").Sum(t => t.Amount);
            return shift.OpeningCash + cashSales + cashIn - cashOut;
        }

        private static string CombineNotes(string? existing, string addition)
        {
            var combined = string.IsNullOrWhiteSpace(existing)
                ? addition
                : $"{existing} | {addition}";

            return combined.Length > 255 ? combined[..255] : combined;
        }
    }
}
