using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface IInventoryAnomalyService
    {
        Task<InventoryAnomalySummaryViewModel> BuildSummaryAsync(int businessId, CancellationToken cancellationToken = default);
    }

    public class InventoryAnomalyService : IInventoryAnomalyService
    {
        private const int AnalysisWindowDays = 60;
        private const int RecentWindowDays = 7;
        private const int BaselineWindowDays = 21;
        private const int DeadStockDays = 30;
        private const int DropWindowDays = 30;

        private readonly ApplicationDbContext _context;

        private sealed record ItemSalesPoint(DateTime Day, int Units);
        private sealed record ItemMetrics(
            int Recent7Units,
            int Baseline21Units,
            int Recent30Units,
            int Previous30Units,
            decimal Recent7Average,
            decimal Baseline21Average,
            int DaysWithoutSales);

        public InventoryAnomalyService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryAnomalySummaryViewModel> BuildSummaryAsync(int businessId, CancellationToken cancellationToken = default)
        {
            var today = PhilippineTime.Now.Date;
            var analysisStart = today.AddDays(-(AnalysisWindowDays - 1));

            var items = await _context.MenuItems
                .AsNoTracking()
                .Where(item => item.BusinessId == businessId && item.IsActive)
                .OrderBy(item => item.ItemName)
                .Select(item => new
                {
                    item.ItemId,
                    item.ItemName,
                    item.StockAvailable
                })
                .ToListAsync(cancellationToken);

            var dailySales = await _context.OrderDetails
                .AsNoTracking()
                .Where(detail => detail.MenuItem.BusinessId == businessId && detail.Order.OrderTime >= analysisStart)
                .GroupBy(detail => new { detail.ItemId, Day = detail.Order.OrderTime.Date })
                .Select(group => new
                {
                    group.Key.ItemId,
                    group.Key.Day,
                    Units = group.Sum(detail => detail.Quantity)
                })
                .ToListAsync(cancellationToken);

            var salesByItem = dailySales
                .GroupBy(entry => entry.ItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group
                        .OrderBy(entry => entry.Day)
                        .Select(entry => new ItemSalesPoint(entry.Day, entry.Units))
                        .ToList());

            var anomalies = new List<InventoryAnomalyItemViewModel>();
            foreach (var item in items)
            {
                salesByItem.TryGetValue(item.ItemId, out var itemSales);
                itemSales ??= new List<ItemSalesPoint>();

                var metrics = BuildMetrics(itemSales, today);
                var anomaly = DetectDemandSpike(item.ItemId, item.ItemName, item.StockAvailable, metrics)
                    ?? DetectDeadStock(item.ItemId, item.ItemName, item.StockAvailable, metrics)
                    ?? DetectSalesDrop(item.ItemId, item.ItemName, item.StockAvailable, metrics);

                if (anomaly != null)
                {
                    anomalies.Add(anomaly);
                }
            }

            var orderedAnomalies = anomalies
                .OrderByDescending(item => item.Severity == "High")
                .ThenBy(item => item.AnomalyType)
                .ThenBy(item => item.ItemName)
                .ToList();

            return new InventoryAnomalySummaryViewModel
            {
                TotalAnomalies = orderedAnomalies.Count,
                SpikeCount = orderedAnomalies.Count(item => item.AnomalyType == "DemandSpike"),
                DeadStockCount = orderedAnomalies.Count(item => item.AnomalyType == "DeadStock"),
                DropCount = orderedAnomalies.Count(item => item.AnomalyType == "SalesDrop"),
                Items = orderedAnomalies
            };
        }

        private static int SumUnits(IEnumerable<ItemSalesPoint> itemSales, DateTime startInclusive, DateTime endInclusive)
        {
            return itemSales
                .Where(entry => entry.Day >= startInclusive && entry.Day <= endInclusive)
                .Sum(entry => entry.Units);
        }

        private static ItemMetrics BuildMetrics(IEnumerable<ItemSalesPoint> itemSales, DateTime today)
        {
            var sales = itemSales.ToList();
            var recent7Units = SumUnits(sales, today.AddDays(-(RecentWindowDays - 1)), today);
            var baseline21Units = SumUnits(sales, today.AddDays(-(RecentWindowDays + BaselineWindowDays)), today.AddDays(-RecentWindowDays));
            var recent30Units = SumUnits(sales, today.AddDays(-(DropWindowDays - 1)), today);
            var previous30Units = SumUnits(sales, today.AddDays(-(DropWindowDays * 2 - 1)), today.AddDays(-DropWindowDays));
            var recent7Average = recent7Units / (decimal)RecentWindowDays;
            var baseline21Average = baseline21Units / (decimal)BaselineWindowDays;
            var lastSaleDate = sales.LastOrDefault()?.Day;
            var daysWithoutSales = lastSaleDate.HasValue ? (today - lastSaleDate.Value.Date).Days : AnalysisWindowDays;

            return new ItemMetrics(
                recent7Units,
                baseline21Units,
                recent30Units,
                previous30Units,
                recent7Average,
                baseline21Average,
                daysWithoutSales);
        }

        private static InventoryAnomalyItemViewModel? DetectDemandSpike(int itemId, string itemName, int currentStock, ItemMetrics metrics)
        {
            if (metrics.Baseline21Average < 1m || metrics.Recent7Average < metrics.Baseline21Average * 1.6m || metrics.Recent7Units < 10)
            {
                return null;
            }

            var trendChange = metrics.Baseline21Average == 0m
                ? 100m
                : Math.Round(((metrics.Recent7Average - metrics.Baseline21Average) / metrics.Baseline21Average) * 100m, 1, MidpointRounding.AwayFromZero);

            return new InventoryAnomalyItemViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                CurrentStock = currentStock,
                AnomalyType = "DemandSpike",
                Severity = trendChange >= 120m ? "High" : "Medium",
                SummaryText = $"Recent weekly demand is running {trendChange:0.#}% above the trailing baseline.",
                RecentPeriodUnits = metrics.Recent7Units,
                BaselinePeriodUnits = metrics.Baseline21Units,
                TrendChangePercent = trendChange
            };
        }

        private static InventoryAnomalyItemViewModel? DetectDeadStock(int itemId, string itemName, int currentStock, ItemMetrics metrics)
        {
            if (currentStock <= 0 || metrics.Recent30Units > 0)
            {
                return null;
            }

            return new InventoryAnomalyItemViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                CurrentStock = currentStock,
                AnomalyType = "DeadStock",
                Severity = metrics.DaysWithoutSales >= 45 ? "High" : "Medium",
                SummaryText = $"No sales recorded in the last {DeadStockDays} days while stock is still on hand.",
                RecentPeriodUnits = metrics.Recent30Units,
                BaselinePeriodUnits = metrics.Previous30Units,
                DaysWithoutSales = metrics.DaysWithoutSales
            };
        }

        private static InventoryAnomalyItemViewModel? DetectSalesDrop(int itemId, string itemName, int currentStock, ItemMetrics metrics)
        {
            if (metrics.Previous30Units < 12 || metrics.Recent30Units > metrics.Previous30Units * 0.5m)
            {
                return null;
            }

            var trendChange = metrics.Previous30Units == 0
                ? -100m
                : Math.Round(((metrics.Recent30Units - metrics.Previous30Units) / (decimal)metrics.Previous30Units) * 100m, 1, MidpointRounding.AwayFromZero);

            return new InventoryAnomalyItemViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                CurrentStock = currentStock,
                AnomalyType = "SalesDrop",
                Severity = trendChange <= -70m ? "High" : "Medium",
                SummaryText = $"Last 30 days are down {Math.Abs(trendChange):0.#}% versus the prior 30-day period.",
                RecentPeriodUnits = metrics.Recent30Units,
                BaselinePeriodUnits = metrics.Previous30Units,
                TrendChangePercent = trendChange
            };
        }
    }
}
