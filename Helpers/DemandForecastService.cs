using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface IDemandForecastService
    {
        Task<InventoryDemandForecastSummaryViewModel> BuildDemandForecastSummaryAsync(int businessId, CancellationToken cancellationToken = default);
        Task<InventoryDemandForecastSummaryViewModel> BuildDemandForecastSummaryAsync(int businessId, int lookbackDays, int primaryHorizonDays, CancellationToken cancellationToken = default);
    }

    public class DemandForecastService : IDemandForecastService
    {
        private const int AccuracyWindowDays = 7;
        private const int MinLookbackDays = 14;
        private const int MaxLookbackDays = 90;
        private const int MinHorizonDays = 7;
        private const int MaxHorizonDays = 30;

        private readonly ApplicationDbContext _context;

        public DemandForecastService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<InventoryDemandForecastSummaryViewModel> BuildDemandForecastSummaryAsync(int businessId, CancellationToken cancellationToken = default)
        {
            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusinessId == businessId, cancellationToken);

            if (business == null)
            {
                return new InventoryDemandForecastSummaryViewModel();
            }

            return await BuildDemandForecastSummaryAsync(
                businessId,
                business.InventoryForecastLookbackDays,
                business.InventoryForecastHorizonDays,
                cancellationToken);
        }

        public async Task<InventoryDemandForecastSummaryViewModel> BuildDemandForecastSummaryAsync(int businessId, int lookbackDays, int primaryHorizonDays, CancellationToken cancellationToken = default)
        {
            lookbackDays = Clamp(lookbackDays, MinLookbackDays, MaxLookbackDays, 28);
            primaryHorizonDays = Clamp(primaryHorizonDays, MinHorizonDays, MaxHorizonDays, 7);
            var now = PhilippineTime.Now;
            var today = now.Date;
            var accuracyDataStart = today.AddDays(-(lookbackDays + AccuracyWindowDays - 1));

            var items = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId && m.IsActive)
                .OrderBy(m => m.ItemName)
                .Select(m => new
                {
                    m.ItemId,
                    m.ItemName,
                    m.StockAvailable
                })
                .ToListAsync(cancellationToken);

            var dailySales = await _context.OrderDetails
                .AsNoTracking()
                .Where(d => d.MenuItem.BusinessId == businessId && d.Order.OrderTime >= accuracyDataStart)
                .GroupBy(d => new { d.ItemId, Day = d.Order.OrderTime.Date })
                .Select(g => new
                {
                    g.Key.ItemId,
                    g.Key.Day,
                    Quantity = g.Sum(d => d.Quantity)
                })
                .ToListAsync(cancellationToken);

            var salesByItem = dailySales
                .GroupBy(x => x.ItemId)
                .ToDictionary(
                    group => group.Key,
                    group => group.ToDictionary(x => x.Day, x => x.Quantity));

            var itemsWithForecast = new List<InventoryDemandForecastItemViewModel>();
            var accuracyItems = new List<InventoryForecastAccuracyItemViewModel>();

            foreach (var item in items)
            {
                itemsWithForecast.Add(BuildForecastItem(item.ItemId, item.ItemName, item.StockAvailable, lookbackDays, today, salesByItem));

                var accuracyItem = BuildAccuracyItem(item.ItemId, item.ItemName, lookbackDays, today, salesByItem);
                if (accuracyItem != null)
                {
                    accuracyItems.Add(accuracyItem);
                }
            }

            itemsWithForecast = itemsWithForecast
                .OrderByDescending(item => item.ForecastSuggestedReorderQuantity)
                .ThenBy(item => item.ForecastedDaysUntilStockout ?? decimal.MaxValue)
                .ThenBy(item => item.ItemName)
                .ToList();

            var orderedAccuracyItems = accuracyItems
                .OrderBy(item => item.AccuracyPercent)
                .ThenByDescending(item => item.AbsoluteErrorUnits)
                .ThenBy(item => item.ItemName)
                .ToList();

            return new InventoryDemandForecastSummaryViewModel
            {
                LookbackDays = lookbackDays,
                PrimaryHorizonDays = primaryHorizonDays,
                ItemsForecasted = itemsWithForecast.Count,
                TotalProjectedUnits7Days = itemsWithForecast.Sum(item => item.Forecast7Days),
                TotalProjectedUnits14Days = itemsWithForecast.Sum(item => item.Forecast14Days),
                TotalProjectedUnits30Days = itemsWithForecast.Sum(item => item.Forecast30Days),
                Accuracy = BuildAccuracySummary(orderedAccuracyItems),
                Items = itemsWithForecast
            };
        }

        private static InventoryDemandForecastItemViewModel BuildForecastItem(
            int itemId,
            string itemName,
            int currentStock,
            int lookbackDays,
            DateTime today,
            IReadOnlyDictionary<int, Dictionary<DateTime, int>> salesByItem)
        {
            salesByItem.TryGetValue(itemId, out var itemSales);
            itemSales ??= new Dictionary<DateTime, int>();

            var dailySeries = Enumerable.Range(0, lookbackDays)
                .Select(offset =>
                {
                    var day = today.AddDays(-(lookbackDays - 1 - offset));
                    itemSales.TryGetValue(day, out var quantity);
                    return quantity;
                })
                .ToList();

            var weightedDailyForecast = CalculateWeightedMovingAverage(dailySeries);
            var forecast7Days = (int)Math.Ceiling(weightedDailyForecast * 7m);
            var forecast14Days = (int)Math.Ceiling(weightedDailyForecast * 14m);
            var forecast30Days = (int)Math.Ceiling(weightedDailyForecast * 30m);
            decimal? forecastedDaysUntilStockout = weightedDailyForecast > 0m
                ? Math.Round(currentStock / weightedDailyForecast, 1, MidpointRounding.AwayFromZero)
                : null;
            var trendDirection = DetermineTrendDirection(dailySeries);
            var confidenceLabel = DetermineConfidenceLabel(dailySeries);
            var forecastSuggestedReorderQuantity = Math.Max(0, forecast7Days - currentStock);

            return new InventoryDemandForecastItemViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                CurrentStock = currentStock,
                WeightedDailyForecast = Math.Round(weightedDailyForecast, 2, MidpointRounding.AwayFromZero),
                Forecast7Days = forecast7Days,
                Forecast14Days = forecast14Days,
                Forecast30Days = forecast30Days,
                ForecastedDaysUntilStockout = forecastedDaysUntilStockout,
                TrendDirection = trendDirection,
                ConfidenceLabel = confidenceLabel,
                ForecastSuggestedReorderQuantity = forecastSuggestedReorderQuantity
            };
        }

        private static InventoryForecastAccuracyItemViewModel? BuildAccuracyItem(
            int itemId,
            string itemName,
            int lookbackDays,
            DateTime today,
            IReadOnlyDictionary<int, Dictionary<DateTime, int>> salesByItem)
        {
            salesByItem.TryGetValue(itemId, out var itemSales);
            itemSales ??= new Dictionary<DateTime, int>();

            var trainingSeries = Enumerable.Range(0, lookbackDays)
                .Select(offset =>
                {
                    var day = today.AddDays(-(lookbackDays + AccuracyWindowDays - 1 - offset));
                    itemSales.TryGetValue(day, out var quantity);
                    return quantity;
                })
                .ToList();

            var actualSeries = Enumerable.Range(0, AccuracyWindowDays)
                .Select(offset =>
                {
                    var day = today.AddDays(-(AccuracyWindowDays - 1 - offset));
                    itemSales.TryGetValue(day, out var quantity);
                    return quantity;
                })
                .ToList();

            if (trainingSeries.All(value => value == 0) && actualSeries.All(value => value == 0))
            {
                return null;
            }

            var forecastedUnits = (int)Math.Ceiling(CalculateWeightedMovingAverage(trainingSeries) * AccuracyWindowDays);
            var actualUnits = actualSeries.Sum();
            var absoluteError = Math.Abs(forecastedUnits - actualUnits);
            decimal accuracyPercent;
            if (actualUnits == 0)
            {
                accuracyPercent = forecastedUnits == 0 ? 100m : 0m;
            }
            else
            {
                accuracyPercent = Math.Max(0m, 100m - ((absoluteError / (decimal)actualUnits) * 100m));
            }

            var biasDirection = "Balanced";
            if (forecastedUnits > actualUnits)
            {
                biasDirection = "Over";
            }
            else if (forecastedUnits < actualUnits)
            {
                biasDirection = "Under";
            }

            return new InventoryForecastAccuracyItemViewModel
            {
                ItemId = itemId,
                ItemName = itemName,
                ForecastedUnits7Days = forecastedUnits,
                ActualUnits7Days = actualUnits,
                AbsoluteErrorUnits = absoluteError,
                AccuracyPercent = Math.Round(accuracyPercent, 1, MidpointRounding.AwayFromZero),
                BiasDirection = biasDirection
            };
        }

        private static InventoryForecastAccuracySummaryViewModel BuildAccuracySummary(IReadOnlyCollection<InventoryForecastAccuracyItemViewModel> accuracyItems)
        {
            if (accuracyItems.Count == 0)
            {
                return new InventoryForecastAccuracySummaryViewModel();
            }

            return new InventoryForecastAccuracySummaryViewModel
            {
                ItemsMeasured = accuracyItems.Count,
                AccurateItems = accuracyItems.Count(item => item.AccuracyPercent >= 80m),
                OverForecastedItems = accuracyItems.Count(item => item.BiasDirection == "Over"),
                UnderForecastedItems = accuracyItems.Count(item => item.BiasDirection == "Under"),
                AverageAccuracyPercent = Math.Round(accuracyItems.Average(item => item.AccuracyPercent), 1, MidpointRounding.AwayFromZero),
                Items = accuracyItems.ToList()
            };
        }

        private static decimal CalculateWeightedMovingAverage(IReadOnlyList<int> series)
        {
            if (series.Count == 0)
            {
                return 0m;
            }

            decimal weightedSum = 0m;
            decimal totalWeight = 0m;

            for (var index = 0; index < series.Count; index++)
            {
                var weight = index + 1;
                weightedSum += series[index] * weight;
                totalWeight += weight;
            }

            if (totalWeight == 0m)
            {
                return 0m;
            }

            return weightedSum / totalWeight;
        }

        private static string DetermineTrendDirection(IReadOnlyList<int> series)
        {
            if (series.Count < 14)
            {
                return "Stable";
            }

            var splitIndex = series.Count / 2;
            var olderAverage = series.Take(splitIndex).Average();
            var recentAverage = series.Skip(splitIndex).Average();
            var delta = recentAverage - olderAverage;

            if (delta >= 0.75d)
            {
                return "Rising";
            }

            if (delta <= -0.75d)
            {
                return "Falling";
            }

            return "Stable";
        }

        private static string DetermineConfidenceLabel(IReadOnlyList<int> series)
        {
            if (series.Count == 0)
            {
                return "Low";
            }

            var activeDays = series.Count(value => value > 0);
            var coverageRatio = activeDays / (decimal)series.Count;

            if (coverageRatio >= 0.65m)
            {
                return "High";
            }

            if (coverageRatio >= 0.35m)
            {
                return "Medium";
            }

            return "Low";
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
