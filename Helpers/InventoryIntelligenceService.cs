using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface IInventoryIntelligenceService
    {
        Task<InventoryReorderSummaryViewModel> BuildReorderSummaryAsync(int businessId, CancellationToken cancellationToken = default);
        Task<InventoryReorderSummaryViewModel> BuildReorderSummaryAsync(int businessId, int lookbackDays, int leadTimeDays, int safetyStockDays, int targetCoverageDays, CancellationToken cancellationToken = default);
    }

    public class InventoryIntelligenceService : IInventoryIntelligenceService
    {
        private const int MinLookbackDays = 7;
        private const int MaxLookbackDays = 90;
        private const int MinLeadTimeDays = 1;
        private const int MaxLeadTimeDays = 30;
        private const int MaxCoverageDays = 60;

        private readonly ApplicationDbContext _context;

        public InventoryIntelligenceService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryReorderSummaryViewModel> BuildReorderSummaryAsync(int businessId, CancellationToken cancellationToken = default)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusinessId == businessId, cancellationToken);

            if (business == null)
            {
                return new InventoryReorderSummaryViewModel();
            }

            return await BuildReorderSummaryAsync(
                businessId,
                business.InventoryReorderLookbackDays,
                business.InventoryLeadTimeDays,
                business.InventorySafetyStockDays,
                business.InventoryTargetCoverageDays,
                cancellationToken);
        }

        public async Task<InventoryReorderSummaryViewModel> BuildReorderSummaryAsync(int businessId, int lookbackDays, int leadTimeDays, int safetyStockDays, int targetCoverageDays, CancellationToken cancellationToken = default)
        {
            lookbackDays = Clamp(lookbackDays, MinLookbackDays, MaxLookbackDays, 30);
            leadTimeDays = Clamp(leadTimeDays, MinLeadTimeDays, MaxLeadTimeDays, 3);
            safetyStockDays = Clamp(safetyStockDays, 0, MaxCoverageDays, 2);
            targetCoverageDays = Clamp(targetCoverageDays, 1, MaxCoverageDays, 7);
            var lookbackStart = PhilippineTime.Now.AddDays(-lookbackDays);

            var items = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.IsActive)
                .OrderBy(m => m.ItemName)
                .Select(m => new
                {
                    m.ItemId,
                    m.ItemName,
                    m.StockAvailable,
                    m.LowStockThreshold
                })
                .ToListAsync(cancellationToken);

            var recentSales = await _context.OrderDetails
                .AsNoTracking()
                .Where(d => d.MenuItem.BusinessId == businessId && d.Order.OrderTime >= lookbackStart)
                .GroupBy(d => d.ItemId)
                .Select(g => new
                {
                    ItemId = g.Key,
                    QuantitySold = g.Sum(d => d.Quantity)
                })
                .ToDictionaryAsync(x => x.ItemId, x => x.QuantitySold, cancellationToken);

            var recommendations = items
                .Select(item => BuildRecommendation(item.ItemId, item.ItemName, item.StockAvailable, item.LowStockThreshold, lookbackDays, leadTimeDays, safetyStockDays, targetCoverageDays, recentSales))
                .Where(item => item.RecommendedReorderQuantity > 0)
                .OrderByDescending(item => GetUrgencyRank(item.Urgency))
                .ThenBy(item => item.DaysUntilStockout ?? decimal.MaxValue)
                .ThenByDescending(item => item.RecommendedReorderQuantity)
                .ThenBy(item => item.ItemName)
                .ToList();

            return new InventoryReorderSummaryViewModel
            {
                TotalRecommendations = recommendations.Count,
                CriticalRecommendations = recommendations.Count(item => item.Urgency == "Critical"),
                RecommendedUnits = recommendations.Sum(item => item.RecommendedReorderQuantity),
                Items = recommendations
            };
        }

        private static InventoryReorderRecommendationViewModel BuildRecommendation(
            int itemId,
            string itemName,
            int currentStock,
            int lowStockThreshold,
            int lookbackDays,
            int leadTimeDays,
            int safetyStockDays,
            int targetCoverageDays,
            IReadOnlyDictionary<int, int> recentSales)
        {
            recentSales.TryGetValue(itemId, out var quantitySoldInLookback);

            var averageDailyDemand = lookbackDays > 0
                ? Math.Round(quantitySoldInLookback / (decimal)lookbackDays, 2, MidpointRounding.AwayFromZero)
                : 0m;

            var reorderPoint = averageDailyDemand > 0m
                ? (int)Math.Ceiling(averageDailyDemand * (leadTimeDays + safetyStockDays))
                : lowStockThreshold;

            reorderPoint = Math.Max(reorderPoint, lowStockThreshold);

            var targetStock = averageDailyDemand > 0m
                ? (int)Math.Ceiling(averageDailyDemand * (leadTimeDays + safetyStockDays + targetCoverageDays))
                : Math.Max(lowStockThreshold * 2, lowStockThreshold + 5);

            targetStock = Math.Max(targetStock, reorderPoint);

            var recommendedReorderQuantity = Math.Max(0, targetStock - currentStock);

            if (averageDailyDemand == 0m && currentStock > lowStockThreshold)
            {
                recommendedReorderQuantity = 0;
            }

            decimal? daysUntilStockout = null;
            if (averageDailyDemand > 0m)
            {
                daysUntilStockout = Math.Round(currentStock / averageDailyDemand, 1, MidpointRounding.AwayFromZero);
            }

            return new InventoryReorderRecommendationViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                CurrentStock = currentStock,
                LowStockThreshold = lowStockThreshold,
                QuantitySoldInLookback = quantitySoldInLookback,
                AverageDailyDemand = averageDailyDemand,
                ReorderPoint = reorderPoint,
                TargetStock = targetStock,
                RecommendedReorderQuantity = recommendedReorderQuantity,
                DaysUntilStockout = daysUntilStockout,
                Urgency = DetermineUrgency(currentStock, reorderPoint, lowStockThreshold, daysUntilStockout)
            };
        }

        private static string DetermineUrgency(int currentStock, int reorderPoint, int lowStockThreshold, decimal? daysUntilStockout)
        {
            if (currentStock <= 0 || (daysUntilStockout.HasValue && daysUntilStockout.Value <= 2m))
            {
                return "Critical";
            }

            if (currentStock <= reorderPoint || currentStock <= lowStockThreshold)
            {
                return "High";
            }

            return "Stable";
        }

        private static int GetUrgencyRank(string urgency)
        {
            return urgency switch
            {
                "Critical" => 3,
                "High" => 2,
                _ => 1
            };
        }

        private static int Clamp(int value, int min, int max, int fallback)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Min(Math.Max(value, min), max);
        }
    }
}