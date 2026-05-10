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
        private const string DraftStatus = "Draft";
        private const string OrderedStatus = "Ordered";
        private const string PartiallyReceivedStatus = "PartiallyReceived";
        private const string ReceivedStatus = "Received";
        private const string ClosedStatus = "Closed";
        private const string CancelledStatus = "Cancelled";
        private const string AllStatusesFilter = "All";
        private const string PromoFlagTransactionType = "PromoFlag";
        private const string ReviewFlagTransactionType = "ReviewFlag";
        private const string ErrorTempDataKey = "Error";
        private const string WarningTempDataKey = "Warning";
        private const string SuccessTempDataKey = "Success";

        private readonly ApplicationDbContext _context;
        private readonly IInventoryIntelligenceService _inventoryIntelligenceService;
        private readonly IDemandForecastService _demandForecastService;
        private readonly IInventoryAnomalyService _inventoryAnomalyService;

        public InventoryController(
            ApplicationDbContext context,
            IInventoryIntelligenceService inventoryIntelligenceService,
            IDemandForecastService demandForecastService,
            IInventoryAnomalyService inventoryAnomalyService)
        {
            _context = context;
            _inventoryIntelligenceService = inventoryIntelligenceService;
            _demandForecastService = demandForecastService;
            _inventoryAnomalyService = inventoryAnomalyService;
        }

        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var reorderSummary = await _inventoryIntelligenceService.BuildReorderSummaryAsync(businessId.Value);
            var demandForecast = await _demandForecastService.BuildDemandForecastSummaryAsync(businessId.Value);
            var anomalySummary = await _inventoryAnomalyService.BuildSummaryAsync(businessId.Value);
            MergeForecastSignals(reorderSummary, demandForecast);

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

            var recentAlerts = await _context.InventoryAlertLogs
                .AsNoTracking()
                .Where(a => a.BusinessId == businessId.Value && a.AlertType == InventoryAlertTypes.ReorderDigest)
                .OrderByDescending(a => a.SentAt)
                .Take(5)
                .Select(a => new InventoryAlertHistoryEntryViewModel
                {
                    TriggerSource = a.TriggerSource,
                    RecipientEmail = a.RecipientEmail,
                    RecommendationCount = a.RecommendationCount,
                    RecommendedUnits = a.RecommendedUnits,
                    SentAt = a.SentAt
                })
                .ToListAsync();

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId.Value && s.IsActive)
                .OrderBy(s => s.SupplierName)
                .Select(s => new SupplierOptionViewModel
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    LeadTimeDaysOverride = s.LeadTimeDaysOverride
                })
                .ToListAsync();

            var recentPurchaseOrders = await _context.PurchaseOrders
                .AsNoTracking()
                .Where(p => p.BusinessId == businessId.Value)
                .OrderByDescending(p => p.CreatedAt)
                .Take(5)
                .Select(p => new PurchaseOrderListItemViewModel
                {
                    PurchaseOrderId = p.PurchaseOrderId,
                    SupplierId = p.SupplierId,
                    PurchaseOrderNumber = p.PurchaseOrderNumber,
                    SupplierName = p.Supplier.SupplierName,
                    Status = p.Status,
                    TotalItems = p.PurchaseOrderLines.Count,
                    TotalUnits = p.PurchaseOrderLines.Sum(line => line.Quantity),
                    ReceivedUnits = p.PurchaseOrderLines.Sum(line => line.ReceivedQuantity),
                    TotalCost = p.PurchaseOrderLines.Sum(line => line.LineTotal),
                    CreatedAt = p.CreatedAt,
                    ExpectedDeliveryDate = p.ExpectedDeliveryDate,
                    Notes = p.Notes
                })
                .ToListAsync();

            var recentSuppliers = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId.Value)
                .OrderByDescending(s => s.IsActive)
                .ThenBy(s => s.SupplierName)
                .Take(6)
                .Select(s => new SupplierListItemViewModel
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    EmailAddress = s.EmailAddress,
                    PhoneNumber = s.PhoneNumber,
                    LeadTimeDaysOverride = s.LeadTimeDaysOverride,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt
                })
                .ToListAsync();

            var model = new InventoryIndexViewModel
            {
                MenuItems = menuItems,
                LowStockItems = lowStockItems,
                RecentTransactions = recentTransactions,
                ReorderSummary = reorderSummary,
                DemandForecast = demandForecast,
                AnomalySummary = anomalySummary,
                AlertHistory = new InventoryAlertHistoryViewModel
                {
                    LatestAlert = recentAlerts.FirstOrDefault(),
                    RecentAlerts = recentAlerts
                },
                Suppliers = suppliers,
                RecentPurchaseOrders = recentPurchaseOrders,
                RecentSuppliers = recentSuppliers,
                PurchaseOrderStatuses = GetPurchaseOrderStatuses()
            };

            return View(model);
        }

        private static void MergeForecastSignals(
            InventoryReorderSummaryViewModel reorderSummary,
            InventoryDemandForecastSummaryViewModel demandForecast)
        {
            if (reorderSummary.Items.Count == 0 || demandForecast.Items.Count == 0)
            {
                return;
            }

            var forecastByItemId = demandForecast.Items.ToDictionary(item => item.ItemId);
            foreach (var recommendation in reorderSummary.Items)
            {
                if (!forecastByItemId.TryGetValue(recommendation.ItemId, out var forecast))
                {
                    continue;
                }

                recommendation.ForecastSuggestedReorderQuantity = forecast.ForecastSuggestedReorderQuantity;
                recommendation.ForecastedDaysUntilStockout = forecast.ForecastedDaysUntilStockout;
                recommendation.ForecastedDailyDemand = forecast.WeightedDailyForecast;
                recommendation.Forecast7Days = forecast.Forecast7Days;
                recommendation.ForecastTrendDirection = forecast.TrendDirection;
            }
        }

        public async Task<IActionResult> PurchaseOrders(string? status = null, int? supplierId = null, DateTime? startDate = null, DateTime? endDate = null)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var selectedStatus = NormalizePurchaseOrderStatus(status);
            var supplierFilters = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.BusinessId == businessId.Value)
                .OrderBy(s => s.SupplierName)
                .Select(s => new SupplierListItemViewModel
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.SupplierName,
                    IsActive = s.IsActive
                })
                .ToListAsync();

            var query = _context.PurchaseOrders
                .AsNoTracking()
                .Where(po => po.BusinessId == businessId.Value);

            if (!string.Equals(selectedStatus, AllStatusesFilter, StringComparison.OrdinalIgnoreCase))
            {
                query = query.Where(po => po.Status == selectedStatus);
            }

            if (supplierId.HasValue)
            {
                query = query.Where(po => po.SupplierId == supplierId.Value);
            }

            if (startDate.HasValue)
            {
                var start = startDate.Value.Date;
                query = query.Where(po => po.CreatedAt >= start);
            }

            if (endDate.HasValue)
            {
                var endExclusive = endDate.Value.Date.AddDays(1);
                query = query.Where(po => po.CreatedAt < endExclusive);
            }

            var purchaseOrders = await query
                .OrderByDescending(po => po.CreatedAt)
                .Select(po => new PurchaseOrderListItemViewModel
                {
                    PurchaseOrderId = po.PurchaseOrderId,
                    SupplierId = po.SupplierId,
                    PurchaseOrderNumber = po.PurchaseOrderNumber,
                    SupplierName = po.Supplier.SupplierName,
                    Status = po.Status,
                    TotalItems = po.PurchaseOrderLines.Count,
                    TotalUnits = po.PurchaseOrderLines.Sum(line => line.Quantity),
                    ReceivedUnits = po.PurchaseOrderLines.Sum(line => line.ReceivedQuantity),
                    TotalCost = po.PurchaseOrderLines.Sum(line => line.LineTotal),
                    CreatedAt = po.CreatedAt,
                    ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                    Notes = po.Notes
                })
                .ToListAsync();

            return View(new PurchaseOrderListPageViewModel
            {
                SelectedStatus = selectedStatus,
                SelectedSupplierId = supplierId,
                StartDate = startDate,
                EndDate = endDate,
                AvailableStatuses = GetPurchaseOrderStatuses(includeAll: true),
                SupplierFilters = supplierFilters,
                PurchaseOrders = purchaseOrders
            });
        }

        public async Task<IActionResult> PurchaseOrderDetails(int id)
        {
            if (!ModelState.IsValid || id <= 0)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(id, businessId.Value);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            return View(BuildPurchaseOrderDetailsViewModel(purchaseOrder));
        }

        public async Task<IActionResult> SupplierDetails(int id)
        {
            if (!ModelState.IsValid || id <= 0)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var supplier = await _context.Suppliers
                .AsNoTracking()
                .Where(s => s.SupplierId == id && s.BusinessId == businessId.Value)
                .Select(s => new SupplierDetailsViewModel
                {
                    SupplierId = s.SupplierId,
                    SupplierName = s.SupplierName,
                    ContactPerson = s.ContactPerson,
                    EmailAddress = s.EmailAddress,
                    PhoneNumber = s.PhoneNumber,
                    LeadTimeDaysOverride = s.LeadTimeDaysOverride,
                    IsActive = s.IsActive,
                    CreatedAt = s.CreatedAt,
                    UpdatedAt = s.UpdatedAt,
                    PurchaseOrderCount = _context.PurchaseOrders.Count(po => po.SupplierId == s.SupplierId),
                    ActiveDraftCount = _context.PurchaseOrders.Count(po => po.SupplierId == s.SupplierId && po.Status == DraftStatus),
                    RecentPurchaseOrders = _context.PurchaseOrders
                        .Where(po => po.SupplierId == s.SupplierId)
                        .OrderByDescending(po => po.CreatedAt)
                        .Take(6)
                        .Select(po => new PurchaseOrderListItemViewModel
                        {
                            PurchaseOrderId = po.PurchaseOrderId,
                            SupplierId = po.SupplierId,
                            PurchaseOrderNumber = po.PurchaseOrderNumber,
                            SupplierName = s.SupplierName,
                            Status = po.Status,
                            TotalItems = po.PurchaseOrderLines.Count,
                            TotalUnits = po.PurchaseOrderLines.Sum(line => line.Quantity),
                            ReceivedUnits = po.PurchaseOrderLines.Sum(line => line.ReceivedQuantity),
                            TotalCost = po.PurchaseOrderLines.Sum(line => line.LineTotal),
                            CreatedAt = po.CreatedAt,
                            ExpectedDeliveryDate = po.ExpectedDeliveryDate,
                            Notes = po.Notes
                        })
                        .ToList()
                })
                .FirstOrDefaultAsync();

            if (supplier == null)
            {
                return NotFound();
            }

            return View(supplier);
        }

        public async Task<IActionResult> PrintablePurchaseOrder(int id)
        {
            if (!ModelState.IsValid || id <= 0)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(id, businessId.Value);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            return View(BuildPrintablePurchaseOrderViewModel(purchaseOrder));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateSupplier(CreateSupplierRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Please provide valid supplier details.";
                return RedirectToAction(nameof(Index));
            }

            var normalizedName = request.SupplierName.Trim();
            var exists = await _context.Suppliers.AnyAsync(s =>
                s.BusinessId == businessId.Value && s.IsActive && s.SupplierName == normalizedName);
            if (exists)
            {
                TempData[ErrorTempDataKey] = $"Supplier '{normalizedName}' already exists.";
                return RedirectToAction(nameof(Index));
            }

            _context.Suppliers.Add(new Supplier
            {
                BusinessId = businessId.Value,
                SupplierName = normalizedName,
                ContactPerson = NormalizeOptional(request.ContactPerson),
                EmailAddress = NormalizeOptional(request.EmailAddress),
                PhoneNumber = NormalizeOptional(request.PhoneNumber),
                LeadTimeDaysOverride = request.LeadTimeDaysOverride,
                IsActive = true,
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();
            TempData[SuccessTempDataKey] = $"Supplier '{normalizedName}' added successfully.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateSupplier(UpdateSupplierRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Please provide valid supplier details.";
                return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId && s.BusinessId == businessId.Value);
            if (supplier == null)
            {
                return NotFound();
            }

            var normalizedName = request.SupplierName.Trim();
            var exists = await _context.Suppliers.AnyAsync(s =>
                s.BusinessId == businessId.Value &&
                s.SupplierId != request.SupplierId &&
                s.SupplierName == normalizedName);

            if (exists)
            {
                TempData[ErrorTempDataKey] = $"Supplier '{normalizedName}' already exists.";
                return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
            }

            supplier.SupplierName = normalizedName;
            supplier.ContactPerson = NormalizeOptional(request.ContactPerson);
            supplier.EmailAddress = NormalizeOptional(request.EmailAddress);
            supplier.PhoneNumber = NormalizeOptional(request.PhoneNumber);
            supplier.LeadTimeDaysOverride = request.LeadTimeDaysOverride;
            supplier.UpdatedAt = PhilippineTime.Now;

            await _context.SaveChangesAsync();
            TempData[SuccessTempDataKey] = $"Supplier '{normalizedName}' updated.";
            return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeactivateSupplier(SupplierActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId && s.BusinessId == businessId.Value);
            if (supplier == null)
            {
                return NotFound();
            }

            if (!supplier.IsActive)
            {
                TempData[WarningTempDataKey] = $"Supplier '{supplier.SupplierName}' is already inactive.";
                return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
            }

            supplier.IsActive = false;
            supplier.UpdatedAt = PhilippineTime.Now;
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Supplier '{supplier.SupplierName}' was deactivated.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReactivateSupplier(SupplierActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId && s.BusinessId == businessId.Value);
            if (supplier == null)
            {
                return NotFound();
            }

            if (supplier.IsActive)
            {
                TempData[WarningTempDataKey] = $"Supplier '{supplier.SupplierName}' is already active.";
                return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
            }

            supplier.IsActive = true;
            supplier.UpdatedAt = PhilippineTime.Now;
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Supplier '{supplier.SupplierName}' was reactivated.";
            return RedirectToAction(nameof(SupplierDetails), new { id = request.SupplierId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateDraftPurchaseOrder(CreateDraftPurchaseOrderRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Please select a supplier before creating a draft purchase order.";
                return RedirectToAction(nameof(Index));
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId && s.BusinessId == businessId.Value && s.IsActive);
            if (supplier == null)
            {
                TempData[ErrorTempDataKey] = "Selected supplier could not be found.";
                return RedirectToAction(nameof(Index));
            }

            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null)
            {
                return NotFound();
            }

            var lookbackDays = business.InventoryReorderLookbackDays;
            var leadTimeDays = supplier.LeadTimeDaysOverride ?? business.InventoryLeadTimeDays;
            var safetyStockDays = business.InventorySafetyStockDays;
            var targetCoverageDays = business.InventoryTargetCoverageDays;
            var reorderSummary = await _inventoryIntelligenceService.BuildReorderSummaryAsync(
                businessId.Value,
                lookbackDays,
                leadTimeDays,
                safetyStockDays,
                targetCoverageDays);
            var demandForecast = await _demandForecastService.BuildDemandForecastSummaryAsync(
                businessId.Value,
                business.InventoryForecastLookbackDays,
                business.InventoryForecastHorizonDays);
            var draftQuantities = BuildDraftPurchaseOrderQuantities(
                reorderSummary,
                demandForecast,
                business.InventoryForecastHorizonDays);

            if (draftQuantities.Count == 0)
            {
                TempData[WarningTempDataKey] = "No draft purchase order was created because neither inventory recommendations nor the forecast horizon suggests additional units right now.";
                return RedirectToAction(nameof(Index));
            }

            var purchaseOrderNumber = await GeneratePurchaseOrderNumberAsync(businessId.Value);
            var now = PhilippineTime.Now;
            var userId = GetUserId();

            var itemIds = draftQuantities.Keys.ToList();
            var menuItemCosts = await _context.MenuItems
                .AsNoTracking()
                .Where(m => m.BusinessId == businessId.Value && itemIds.Contains(m.ItemId))
                .ToDictionaryAsync(m => m.ItemId, m => new { m.ItemName, m.CostPrice });

            var purchaseOrder = new PurchaseOrder
            {
                BusinessId = businessId.Value,
                SupplierId = supplier.SupplierId,
                PurchaseOrderNumber = purchaseOrderNumber,
                Status = DraftStatus,
                Notes = NormalizeOptional(request.Notes) ?? $"Generated from Inventory Recommendations using a {business.InventoryForecastHorizonDays}-day forecast horizon.",
                CreatedByUserId = userId,
                CreatedAt = now,
                ExpectedDeliveryDate = now.AddDays(leadTimeDays)
            };

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            foreach (var draftQuantity in draftQuantities)
            {
                if (!menuItemCosts.TryGetValue(draftQuantity.Key, out var itemData))
                {
                    continue;
                }

                var unitCost = itemData.CostPrice;
                var quantity = draftQuantity.Value;
                _context.PurchaseOrderLines.Add(new PurchaseOrderLine
                {
                    PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                    ItemId = draftQuantity.Key,
                    Quantity = quantity,
                    ReceivedQuantity = 0,
                    UnitCost = unitCost,
                    LineTotal = unitCost * quantity
                });
            }

            await _context.SaveChangesAsync();
            TempData[SuccessTempDataKey] = $"Draft purchase order {purchaseOrderNumber} created for {supplier.SupplierName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateAnomalyPurchaseOrder(InventoryAnomalyActionRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid || !request.SupplierId.HasValue)
            {
                TempData[ErrorTempDataKey] = "Select a supplier before creating a draft purchase order from an anomaly.";
                return RedirectToAction(nameof(Index));
            }

            var supplier = await _context.Suppliers
                .FirstOrDefaultAsync(s => s.SupplierId == request.SupplierId.Value && s.BusinessId == businessId.Value && s.IsActive);
            if (supplier == null)
            {
                TempData[ErrorTempDataKey] = "Selected supplier could not be found.";
                return RedirectToAction(nameof(Index));
            }

            var business = await _context.Businesses
                .AsNoTracking()
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null)
            {
                return NotFound();
            }

            var leadTimeDays = supplier.LeadTimeDaysOverride ?? business.InventoryLeadTimeDays;
            var reorderSummary = await _inventoryIntelligenceService.BuildReorderSummaryAsync(
                businessId.Value,
                business.InventoryReorderLookbackDays,
                leadTimeDays,
                business.InventorySafetyStockDays,
                business.InventoryTargetCoverageDays);
            var demandForecast = await _demandForecastService.BuildDemandForecastSummaryAsync(
                businessId.Value,
                business.InventoryForecastLookbackDays,
                business.InventoryForecastHorizonDays);
            var draftQuantities = BuildDraftPurchaseOrderQuantities(
                reorderSummary,
                demandForecast,
                business.InventoryForecastHorizonDays);

            if (!draftQuantities.TryGetValue(request.ItemId, out var quantity) || quantity <= 0)
            {
                TempData[WarningTempDataKey] = "That spike is already covered by current stock, so no draft PO was created.";
                return RedirectToAction(nameof(Index));
            }

            var menuItem = await _context.MenuItems
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.ItemId == request.ItemId && m.BusinessId == businessId.Value && m.IsActive);
            if (menuItem == null)
            {
                return NotFound();
            }

            var purchaseOrderNumber = await GeneratePurchaseOrderNumberAsync(businessId.Value);
            var now = PhilippineTime.Now;
            var userId = GetUserId();

            var purchaseOrder = new PurchaseOrder
            {
                BusinessId = businessId.Value,
                SupplierId = supplier.SupplierId,
                PurchaseOrderNumber = purchaseOrderNumber,
                Status = DraftStatus,
                Notes = $"Generated from anomaly spike detection for {menuItem.ItemName} using a {business.InventoryForecastHorizonDays}-day forecast horizon.",
                CreatedByUserId = userId,
                CreatedAt = now,
                ExpectedDeliveryDate = now.AddDays(leadTimeDays)
            };

            _context.PurchaseOrders.Add(purchaseOrder);
            await _context.SaveChangesAsync();

            _context.PurchaseOrderLines.Add(new PurchaseOrderLine
            {
                PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                ItemId = menuItem.ItemId,
                Quantity = quantity,
                ReceivedQuantity = 0,
                UnitCost = menuItem.CostPrice,
                LineTotal = menuItem.CostPrice * quantity
            });

            await _context.SaveChangesAsync();
            TempData[SuccessTempDataKey] = $"Draft purchase order {purchaseOrderNumber} created for spike item {menuItem.ItemName}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlagDeadStockPromotion(InventoryAnomalyActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "The dead-stock action could not be completed.";
                return RedirectToAction(nameof(Index));
            }

            return await FlagInventoryReviewAsync(request.ItemId, PromoFlagTransactionType, "Flagged for promo review from dead-stock anomaly.", "flagged for promo review");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> FlagSalesDropReview(InventoryAnomalyActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "The sales-drop review action could not be completed.";
                return RedirectToAction(nameof(Index));
            }

            return await FlagInventoryReviewAsync(request.ItemId, ReviewFlagTransactionType, "Flagged for manager review from sales-drop anomaly.", "flagged for manager review");
        }

        private static Dictionary<int, int> BuildDraftPurchaseOrderQuantities(
            InventoryReorderSummaryViewModel reorderSummary,
            InventoryDemandForecastSummaryViewModel demandForecast,
            int forecastHorizonDays)
        {
            var quantities = reorderSummary.Items
                .Where(item => item.RecommendedReorderQuantity > 0)
                .ToDictionary(item => item.ItemId, item => item.RecommendedReorderQuantity);

            foreach (var forecastItem in demandForecast.Items)
            {
                var forecastGap = GetForecastGapForHorizon(forecastItem, forecastHorizonDays);
                if (forecastGap <= 0)
                {
                    continue;
                }

                if (quantities.TryGetValue(forecastItem.ItemId, out var existingQuantity))
                {
                    quantities[forecastItem.ItemId] = Math.Max(existingQuantity, forecastGap);
                    continue;
                }

                quantities[forecastItem.ItemId] = forecastGap;
            }

            return quantities;
        }

        private static int GetForecastGapForHorizon(InventoryDemandForecastItemViewModel forecastItem, int forecastHorizonDays)
        {
            var projectedUnits = forecastHorizonDays switch
            {
                <= 7 => forecastItem.Forecast7Days,
                <= 14 => forecastItem.Forecast14Days,
                _ => forecastItem.Forecast30Days
            };

            return Math.Max(0, projectedUnits - forecastItem.CurrentStock);
        }

        private async Task<IActionResult> FlagInventoryReviewAsync(int itemId, string transactionType, string notes, string successAction)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(m => m.ItemId == itemId && m.BusinessId == businessId.Value && m.IsActive);
            if (menuItem == null)
            {
                return NotFound();
            }

            var now = PhilippineTime.Now;
            var recentFlagExists = await _context.InventoryTransactions
                .AsNoTracking()
                .AnyAsync(t => t.BusinessId == businessId.Value
                    && t.ItemId == itemId
                    && t.TransactionType == transactionType
                    && t.CreatedAt >= now.AddDays(-7));

            if (recentFlagExists)
            {
                TempData[WarningTempDataKey] = $"{menuItem.ItemName} already has a recent {successAction} note.";
                return RedirectToAction(nameof(Index));
            }

            _context.InventoryTransactions.Add(new InventoryTransaction
            {
                BusinessId = businessId.Value,
                ItemId = itemId,
                QuantityChange = 0,
                PreviousStock = menuItem.StockAvailable,
                NewStock = menuItem.StockAvailable,
                TransactionType = transactionType,
                Notes = notes,
                CreatedAt = now
            });

            await _context.SaveChangesAsync();
            TempData[SuccessTempDataKey] = $"{menuItem.ItemName} was {successAction}.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdatePurchaseOrderDraft(UpdatePurchaseOrderDraftRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Please provide valid purchase-order updates.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            if (request.PurchaseOrderLineIds.Count != request.Quantities.Count ||
                request.PurchaseOrderLineIds.Count != request.UnitCosts.Count)
            {
                TempData[ErrorTempDataKey] = "Purchase-order line updates were incomplete.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            var purchaseOrder = await LoadPurchaseOrderAsync(request.PurchaseOrderId, businessId.Value, trackChanges: true);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            if (!string.Equals(purchaseOrder.Status, DraftStatus, StringComparison.OrdinalIgnoreCase))
            {
                TempData[ErrorTempDataKey] = "Only draft purchase orders can be edited.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            var lineMap = purchaseOrder.PurchaseOrderLines.ToDictionary(line => line.PurchaseOrderLineId);
            var lineUpdateResult = ApplyDraftLineUpdates(request, lineMap);
            if (!lineUpdateResult.Success)
            {
                TempData[ErrorTempDataKey] = lineUpdateResult.ErrorMessage;
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            if (lineUpdateResult.UpdatedLineCount == 0)
            {
                TempData[ErrorTempDataKey] = "A draft purchase order must have at least one line with quantity greater than zero.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            purchaseOrder.Notes = NormalizeOptional(request.Notes);
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Draft purchase order {purchaseOrder.PurchaseOrderNumber} updated.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkPurchaseOrderOrdered(PurchaseOrderActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(request.PurchaseOrderId, businessId.Value, trackChanges: true);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            if (!string.Equals(purchaseOrder.Status, DraftStatus, StringComparison.OrdinalIgnoreCase))
            {
                TempData[ErrorTempDataKey] = "Only draft purchase orders can be marked as ordered.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            if (!purchaseOrder.PurchaseOrderLines.Any(line => line.Quantity > 0))
            {
                TempData[ErrorTempDataKey] = "Add at least one line before marking this purchase order as ordered.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            purchaseOrder.Status = OrderedStatus;
            purchaseOrder.OrderedAt = PhilippineTime.Now;
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Purchase order {purchaseOrder.PurchaseOrderNumber} is now marked as ordered.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelPurchaseOrder(PurchaseOrderActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(request.PurchaseOrderId, businessId.Value, trackChanges: true);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            if (!(string.Equals(purchaseOrder.Status, DraftStatus, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(purchaseOrder.Status, OrderedStatus, StringComparison.OrdinalIgnoreCase)) ||
                purchaseOrder.PurchaseOrderLines.Sum(line => line.ReceivedQuantity) > 0)
            {
                TempData[ErrorTempDataKey] = "Only unreceived draft or ordered purchase orders can be cancelled.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            purchaseOrder.Status = CancelledStatus;
            purchaseOrder.ReceivedAt = PhilippineTime.Now;
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Purchase order {purchaseOrder.PurchaseOrderNumber} was cancelled.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ClosePurchaseOrder(PurchaseOrderActionRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(request.PurchaseOrderId, businessId.Value, trackChanges: true);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            var hasOutstandingUnits = purchaseOrder.PurchaseOrderLines.Any(line => line.ReceivedQuantity < line.Quantity);
            if (!(string.Equals(purchaseOrder.Status, OrderedStatus, StringComparison.OrdinalIgnoreCase) ||
                  string.Equals(purchaseOrder.Status, PartiallyReceivedStatus, StringComparison.OrdinalIgnoreCase)) ||
                !hasOutstandingUnits)
            {
                TempData[ErrorTempDataKey] = "Only ordered purchase orders with outstanding units can be closed early.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            purchaseOrder.Status = ClosedStatus;
            purchaseOrder.ReceivedAt = PhilippineTime.Now;
            await _context.SaveChangesAsync();

            TempData[SuccessTempDataKey] = $"Purchase order {purchaseOrder.PurchaseOrderNumber} was closed with remaining units outstanding.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ReceivePurchaseOrder(ReceivePurchaseOrderRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest();
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var purchaseOrder = await LoadPurchaseOrderAsync(request.PurchaseOrderId, businessId.Value, trackChanges: true);
            if (purchaseOrder == null)
            {
                return NotFound();
            }

            if (!string.Equals(purchaseOrder.Status, OrderedStatus, StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(purchaseOrder.Status, PartiallyReceivedStatus, StringComparison.OrdinalIgnoreCase))
            {
                TempData[ErrorTempDataKey] = "Only ordered purchase orders can be received into stock.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            if (request.PurchaseOrderLineIds.Count != request.ReceiveQuantities.Count)
            {
                TempData[ErrorTempDataKey] = "Receipt quantities were incomplete.";
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            var receivePlan = BuildReceivePlan(request, purchaseOrder.PurchaseOrderLines);
            if (!receivePlan.Success)
            {
                TempData[ErrorTempDataKey] = receivePlan.ErrorMessage;
                return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
            }

            await using var transaction = await _context.Database.BeginTransactionAsync();
            var now = PhilippineTime.Now;

            foreach (var receipt in receivePlan.Receipts)
            {
                var line = receipt.Line;
                var menuItem = line.MenuItem;
                var previousReceivedQuantity = line.ReceivedQuantity;
                var previousStock = menuItem.StockAvailable;
                menuItem.StockAvailable += receipt.ReceiveQuantity;
                line.ReceivedQuantity += receipt.ReceiveQuantity;

                _context.InventoryTransactions.Add(new InventoryTransaction
                {
                    BusinessId = businessId.Value,
                    ItemId = menuItem.ItemId,
                    QuantityChange = receipt.ReceiveQuantity,
                    PreviousStock = previousStock,
                    NewStock = menuItem.StockAvailable,
                    TransactionType = "Restock",
                    Notes = $"Received via PO {purchaseOrder.PurchaseOrderNumber}",
                    CreatedAt = now
                });

                _context.PurchaseOrderReceipts.Add(new PurchaseOrderReceipt
                {
                    PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                    BusinessId = businessId.Value,
                    ItemId = menuItem.ItemId,
                    QuantityReceived = receipt.ReceiveQuantity,
                    PreviousReceivedQuantity = previousReceivedQuantity,
                    NewReceivedQuantity = line.ReceivedQuantity,
                    PreviousStock = previousStock,
                    NewStock = menuItem.StockAvailable,
                    Notes = NormalizeOptional(request.Notes) ?? $"Received via PO {purchaseOrder.PurchaseOrderNumber}",
                    ReceivedAt = now
                });
            }

            purchaseOrder.Status = purchaseOrder.PurchaseOrderLines.All(line => line.ReceivedQuantity >= line.Quantity)
                ? ReceivedStatus
                : PartiallyReceivedStatus;
            purchaseOrder.ReceivedAt = string.Equals(purchaseOrder.Status, ReceivedStatus, StringComparison.OrdinalIgnoreCase)
                ? now
                : null;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            TempData[SuccessTempDataKey] = string.Equals(purchaseOrder.Status, ReceivedStatus, StringComparison.OrdinalIgnoreCase)
                ? $"Purchase order {purchaseOrder.PurchaseOrderNumber} fully received and stock was updated."
                : $"Purchase order {purchaseOrder.PurchaseOrderNumber} partially received and stock was updated.";
            return RedirectToAction(nameof(PurchaseOrderDetails), new { id = request.PurchaseOrderId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Restock(RestockRequest request)
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            if (!ModelState.IsValid)
            {
                TempData[ErrorTempDataKey] = "Please provide a valid restock quantity.";
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

            TempData[SuccessTempDataKey] = $"Restocked {request.Quantity} unit(s) for {menuItem.ItemName}. New stock: {menuItem.StockAvailable}.";
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
                TempData[ErrorTempDataKey] = "Please provide a valid stock adjustment request.";
                return RedirectToAction(nameof(Index));
            }

            var menuItem = await _context.MenuItems
                .FirstOrDefaultAsync(m => m.ItemId == request.ItemId && m.BusinessId == businessId.Value);

            if (menuItem == null) return NotFound();

            if (request.TransactionType == "Spoilage" && request.Quantity <= 0)
            {
                TempData[ErrorTempDataKey] = "Spoilage quantity must be greater than zero.";
                return RedirectToAction(nameof(Index));
            }

            if (request.TransactionType == "Correction" && request.Quantity == 0)
            {
                TempData[ErrorTempDataKey] = "Correction quantity cannot be zero.";
                return RedirectToAction(nameof(Index));
            }

            var quantityChange = request.TransactionType == "Spoilage"
                ? -Math.Abs(request.Quantity)
                : request.Quantity;

            var previousStock = menuItem.StockAvailable;
            var newStock = previousStock + quantityChange;

            if (newStock < 0)
            {
                TempData[ErrorTempDataKey] = $"Adjustment would make {menuItem.ItemName} stock negative. Current stock: {previousStock}.";
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
            TempData[SuccessTempDataKey] = $"{request.TransactionType} saved for {menuItem.ItemName}: {signed}. New stock: {newStock}.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirstValue("BusinessId");
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private int? GetUserId()
        {
            var value = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? User.FindFirstValue("UserId");
            return int.TryParse(value, out var userId) ? userId : null;
        }

        private async Task<string> GeneratePurchaseOrderNumberAsync(int businessId)
        {
            var datePart = PhilippineTime.Now.ToString("yyyyMMdd");
            var prefix = $"PO-{datePart}-";

            var todaysCount = await _context.PurchaseOrders.CountAsync(p =>
                p.BusinessId == businessId && p.PurchaseOrderNumber.StartsWith(prefix));

            return $"{prefix}{(todaysCount + 1).ToString("D3")}";
        }

        private static string? NormalizeOptional(string? value)
        {
            return string.IsNullOrWhiteSpace(value) ? null : value.Trim();
        }

        private static List<string> GetPurchaseOrderStatuses(bool includeAll = false)
        {
            var statuses = new List<string>
            {
                DraftStatus,
                OrderedStatus,
                PartiallyReceivedStatus,
                ReceivedStatus,
                ClosedStatus,
                CancelledStatus
            };

            if (includeAll)
            {
                statuses.Insert(0, AllStatusesFilter);
            }

            return statuses;
        }

        private static string NormalizePurchaseOrderStatus(string? status)
        {
            var normalized = NormalizeOptional(status);
            if (string.IsNullOrWhiteSpace(normalized))
            {
                return AllStatusesFilter;
            }

            return GetPurchaseOrderStatuses(includeAll: true)
                .FirstOrDefault(value => string.Equals(value, normalized, StringComparison.OrdinalIgnoreCase))
                ?? AllStatusesFilter;
        }

        private static (bool Success, string? ErrorMessage, List<(PurchaseOrderLine Line, int ReceiveQuantity)> Receipts) BuildReceivePlan(
            ReceivePurchaseOrderRequest request,
            ICollection<PurchaseOrderLine> purchaseOrderLines)
        {
            var lineMap = purchaseOrderLines.ToDictionary(line => line.PurchaseOrderLineId);
            var receipts = new List<(PurchaseOrderLine Line, int ReceiveQuantity)>();

            for (var index = 0; index < request.PurchaseOrderLineIds.Count; index++)
            {
                var lineId = request.PurchaseOrderLineIds[index];
                var receiveQuantity = request.ReceiveQuantities[index];

                if (!lineMap.TryGetValue(lineId, out var line))
                {
                    return (false, "One or more purchase-order lines could not be found.", new List<(PurchaseOrderLine, int)>());
                }

                if (receiveQuantity < 0)
                {
                    return (false, $"Receive quantity for {line.MenuItem.ItemName} cannot be negative.", new List<(PurchaseOrderLine, int)>());
                }

                var outstandingQuantity = Math.Max(0, line.Quantity - line.ReceivedQuantity);
                if (receiveQuantity > outstandingQuantity)
                {
                    return (false, $"Receive quantity for {line.MenuItem.ItemName} cannot exceed the outstanding quantity of {outstandingQuantity}.", new List<(PurchaseOrderLine, int)>());
                }

                if (receiveQuantity > 0)
                {
                    receipts.Add((line, receiveQuantity));
                }
            }

            if (!receipts.Any())
            {
                return (false, "Enter at least one quantity to receive.", new List<(PurchaseOrderLine, int)>());
            }

            return (true, null, receipts);
        }

        private (bool Success, string? ErrorMessage, int UpdatedLineCount) ApplyDraftLineUpdates(
            UpdatePurchaseOrderDraftRequest request,
            IReadOnlyDictionary<int, PurchaseOrderLine> lineMap)
        {
            var updatedLineCount = 0;

            for (var index = 0; index < request.PurchaseOrderLineIds.Count; index++)
            {
                var lineId = request.PurchaseOrderLineIds[index];
                if (!lineMap.TryGetValue(lineId, out var line))
                {
                    return (false, "One or more purchase-order lines could not be found.", 0);
                }

                var quantity = request.Quantities[index];
                var unitCost = request.UnitCosts[index];

                if (quantity < 0)
                {
                    return (false, $"Quantity for {line.MenuItem.ItemName} cannot be negative.", 0);
                }

                if (unitCost < 0)
                {
                    return (false, $"Unit cost for {line.MenuItem.ItemName} cannot be negative.", 0);
                }

                if (quantity == 0)
                {
                    _context.PurchaseOrderLines.Remove(line);
                    continue;
                }

                line.Quantity = quantity;
                line.UnitCost = unitCost;
                line.LineTotal = unitCost * quantity;
                updatedLineCount++;
            }

            return (true, null, updatedLineCount);
        }

        private async Task<PurchaseOrder?> LoadPurchaseOrderAsync(int purchaseOrderId, int businessId, bool trackChanges = false)
        {
            var query = _context.PurchaseOrders
                .Include(po => po.Business)
                .Include(po => po.Supplier)
                .Include(po => po.PurchaseOrderLines)
                    .ThenInclude(line => line.MenuItem)
                .Include(po => po.Receipts)
                    .ThenInclude(receipt => receipt.MenuItem)
                .Where(po => po.PurchaseOrderId == purchaseOrderId && po.BusinessId == businessId);

            if (!trackChanges)
            {
                query = query.AsNoTracking();
            }

            return await query.FirstOrDefaultAsync();
        }

        private static PurchaseOrderDetailsViewModel BuildPurchaseOrderDetailsViewModel(PurchaseOrder purchaseOrder)
        {
            return new PurchaseOrderDetailsViewModel
            {
                PurchaseOrderId = purchaseOrder.PurchaseOrderId,
                SupplierId = purchaseOrder.SupplierId,
                PurchaseOrderNumber = purchaseOrder.PurchaseOrderNumber,
                Status = purchaseOrder.Status,
                SupplierName = purchaseOrder.Supplier.SupplierName,
                ContactPerson = purchaseOrder.Supplier.ContactPerson,
                EmailAddress = purchaseOrder.Supplier.EmailAddress,
                PhoneNumber = purchaseOrder.Supplier.PhoneNumber,
                Notes = purchaseOrder.Notes,
                CreatedAt = purchaseOrder.CreatedAt,
                OrderedAt = purchaseOrder.OrderedAt,
                ReceivedAt = purchaseOrder.ReceivedAt,
                ExpectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate,
                ReceiptHistory = purchaseOrder.Receipts
                    .OrderByDescending(receipt => receipt.ReceivedAt)
                    .ThenByDescending(receipt => receipt.PurchaseOrderReceiptId)
                    .Select(receipt => new PurchaseOrderReceiptHistoryEntryViewModel
                    {
                        PurchaseOrderReceiptId = receipt.PurchaseOrderReceiptId,
                        ItemName = receipt.MenuItem.ItemName,
                        QuantityReceived = receipt.QuantityReceived,
                        PreviousReceivedQuantity = receipt.PreviousReceivedQuantity,
                        NewReceivedQuantity = receipt.NewReceivedQuantity,
                        PreviousStock = receipt.PreviousStock,
                        NewStock = receipt.NewStock,
                        Notes = receipt.Notes,
                        ReceivedAt = receipt.ReceivedAt
                    })
                    .ToList(),
                Lines = purchaseOrder.PurchaseOrderLines
                    .OrderBy(line => line.MenuItem.ItemName)
                    .Select(line => new PurchaseOrderLineEditorViewModel
                    {
                        PurchaseOrderLineId = line.PurchaseOrderLineId,
                        ItemId = line.ItemId,
                        ItemName = line.MenuItem.ItemName,
                        CurrentStock = line.MenuItem.StockAvailable,
                        Quantity = line.Quantity,
                        ReceivedQuantity = line.ReceivedQuantity,
                        UnitCost = line.UnitCost
                    })
                    .ToList()
            };
        }

        private static PrintablePurchaseOrderViewModel BuildPrintablePurchaseOrderViewModel(PurchaseOrder purchaseOrder)
        {
            return new PrintablePurchaseOrderViewModel
            {
                PurchaseOrderNumber = purchaseOrder.PurchaseOrderNumber,
                Status = purchaseOrder.Status,
                BusinessName = purchaseOrder.Business.BusinessName,
                SupplierName = purchaseOrder.Supplier.SupplierName,
                ContactPerson = purchaseOrder.Supplier.ContactPerson,
                EmailAddress = purchaseOrder.Supplier.EmailAddress,
                PhoneNumber = purchaseOrder.Supplier.PhoneNumber,
                Notes = purchaseOrder.Notes,
                CreatedAt = purchaseOrder.CreatedAt,
                ExpectedDeliveryDate = purchaseOrder.ExpectedDeliveryDate,
                ReceiptHistory = purchaseOrder.Receipts
                    .OrderByDescending(receipt => receipt.ReceivedAt)
                    .ThenByDescending(receipt => receipt.PurchaseOrderReceiptId)
                    .Select(receipt => new PurchaseOrderReceiptHistoryEntryViewModel
                    {
                        PurchaseOrderReceiptId = receipt.PurchaseOrderReceiptId,
                        ItemName = receipt.MenuItem.ItemName,
                        QuantityReceived = receipt.QuantityReceived,
                        PreviousReceivedQuantity = receipt.PreviousReceivedQuantity,
                        NewReceivedQuantity = receipt.NewReceivedQuantity,
                        PreviousStock = receipt.PreviousStock,
                        NewStock = receipt.NewStock,
                        Notes = receipt.Notes,
                        ReceivedAt = receipt.ReceivedAt
                    })
                    .ToList(),
                Lines = purchaseOrder.PurchaseOrderLines
                    .OrderBy(line => line.MenuItem.ItemName)
                    .Select(line => new PurchaseOrderLineEditorViewModel
                    {
                        PurchaseOrderLineId = line.PurchaseOrderLineId,
                        ItemId = line.ItemId,
                        ItemName = line.MenuItem.ItemName,
                        CurrentStock = line.MenuItem.StockAvailable,
                        Quantity = line.Quantity,
                        ReceivedQuantity = line.ReceivedQuantity,
                        UnitCost = line.UnitCost
                    })
                    .ToList()
            };
        }
    }
}
