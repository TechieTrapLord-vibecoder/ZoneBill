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
    [Authorize(Roles = "MainAdmin,Manager")]
    [ServiceFilter(typeof(ActiveSubscriptionFilter))]
    public class InventoryController : Controller
    {
        private readonly ApplicationDbContext _context;

        public InventoryController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItems = await _context.MenuItems
                .Where(m => m.BusinessId == businessId.Value)
                .OrderBy(m => m.ItemName)
                .ToListAsync();

            var lowStockItems = menuItems
                .Where(m => m.IsActive && m.StockAvailable <= m.LowStockThreshold)
                .OrderBy(m => m.StockAvailable)
                .ToList();

            var recentTransactions = await _context.InventoryTransactions
                .Include(t => t.MenuItem)
                .Where(t => t.BusinessId == businessId.Value)
                .OrderByDescending(t => t.CreatedAt)
                .Take(75)
                .ToListAsync();

            var model = new InventoryIndexViewModel
            {
                MenuItems = menuItems,
                LowStockItems = lowStockItems,
                RecentTransactions = recentTransactions
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(RestockRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid restock quantity.";
                return RedirectToAction(nameof(Index));
            }

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(m => m.ItemId == request.ItemId && m.BusinessId == businessId.Value);

            if (menuItem == null) return NotFound();

            var previousStock = menuItem.StockAvailable;
            menuItem.StockAvailable += request.Quantity;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                BusinessId = businessId.Value,
                ItemId = menuItem.ItemId,
                QuantityChange = request.Quantity,
                PreviousStock = previousStock,
                NewStock = menuItem.StockAvailable,
                TransactionType = "Restock",
                Notes = string.IsNullOrWhiteSpace(request.Notes) ? "Manual restock" : request.Notes.Trim(),
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = $"Restocked {request.Quantity} unit(s) for {menuItem.ItemName}. New stock: {menuItem.StockAvailable}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> AdjustStock(StockAdjustmentRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Please provide a valid stock adjustment request.";
                return RedirectToAction(nameof(Index));
            }

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(m => m.ItemId == request.ItemId && m.BusinessId == businessId.Value);

            if (menuItem == null) return NotFound();

            if (request.TransactionType == "Spoilage" && request.Quantity <= 0)
            {
                TempData["Error"] = "Spoilage quantity must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            if (request.TransactionType == "Correction" && request.Quantity == 0)
            {
                TempData["Error"] = "Correction quantity cannot be zero.";
                return RedirectToAction(nameof(Index));
            }

            var quantityChange = request.TransactionType == "Spoilage"
                ? -Math.Abs(request.Quantity)
                : request.Quantity;

            var previousStock = menuItem.StockAvailable;
            var newStock = previousStock + quantityChange;

            if (newStock < 0)
            {
                TempData["Error"] = $"Adjustment would make {menuItem.ItemName} stock negative. Current stock: {previousStock}.";
                return RedirectToAction(nameof(Index));
            }

            menuItem.StockAvailable = newStock;

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                BusinessId = businessId.Value,
                ItemId = menuItem.ItemId,
                QuantityChange = quantityChange,
                PreviousStock = previousStock,
                NewStock = newStock,
                TransactionType = request.TransactionType,
                Notes = string.IsNullOrWhiteSpace(request.Notes)
                    ? $"Manual {request.TransactionType.ToLowerInvariant()} adjustment"
                    : request.Notes.Trim(),
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();

            var signed = quantityChange >= 0 ? $"+{quantityChange}" : quantityChange.ToString();
            TempData["Success"] = $"{request.TransactionType} saved for {menuItem.ItemName}: {signed}. New stock: {newStock}.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }
    }
}
