using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface IInventoryAlertService
    {
        Task<InventoryAlertDispatchResult> SendReorderAlertAsync(InventoryAlertDispatchRequest request, CancellationToken cancellationToken = default);
    }

    public class InventoryAlertService : IInventoryAlertService
    {
        private readonly ApplicationDbContext _context;
        private readonly IInventoryIntelligenceService _inventoryIntelligenceService;
        private readonly IInventoryAnomalyService _inventoryAnomalyService;
        private readonly IEmailService _emailService;

        public InventoryAlertService(
            ApplicationDbContext context,
            IInventoryIntelligenceService inventoryIntelligenceService,
            IInventoryAnomalyService inventoryAnomalyService,
            IEmailService emailService)
        {
            _context = context;
            _inventoryIntelligenceService = inventoryIntelligenceService;
            _inventoryAnomalyService = inventoryAnomalyService;
            _emailService = emailService;
        }

        public async Task<InventoryAlertDispatchResult> SendReorderAlertAsync(InventoryAlertDispatchRequest request, CancellationToken cancellationToken = default)
        {
            var summary = await _inventoryIntelligenceService.BuildReorderSummaryAsync(
                request.BusinessId,
                request.LookbackDays,
                request.LeadTimeDays,
                request.SafetyStockDays,
                request.TargetCoverageDays,
                cancellationToken);
            var anomalySummary = request.IncludeAnomalies
                ? await _inventoryAnomalyService.BuildSummaryAsync(request.BusinessId, cancellationToken)
                : new InventoryAnomalySummaryViewModel();

            if (summary.TotalRecommendations == 0 && anomalySummary.TotalAnomalies == 0)
            {
                return InventoryAlertDispatchResult.NoRecommendations(summary, anomalySummary);
            }

            var signature = BuildAlertSignature(request, summary, anomalySummary);
            var now = PhilippineTime.Now;

            if (!request.ForceSend)
            {
                var lastMatchingAlert = await _context.InventoryAlertLogs
                    .AsNoTracking()
                    .Where(a => a.BusinessId == request.BusinessId
                        && a.AlertType == InventoryAlertTypes.ReorderDigest
                        && a.AlertSignature == signature)
                    .OrderByDescending(a => a.SentAt)
                    .FirstOrDefaultAsync(cancellationToken);

                if (lastMatchingAlert != null && lastMatchingAlert.SentAt >= now.AddHours(-24))
                {
                    return InventoryAlertDispatchResult.Deduped(summary, anomalySummary, lastMatchingAlert.SentAt);
                }
            }

            await _emailService.SendInventoryReorderDigestAsync(new InventoryDigestEmailRequest
            {
                ToEmail = request.RecipientEmail,
                ToName = request.RecipientName,
                Summary = summary,
                BusinessName = request.BusinessName,
                LookbackDays = request.LookbackDays,
                LeadTimeDays = request.LeadTimeDays,
                TargetCoverageDays = request.TargetCoverageDays,
                AnomalySummary = anomalySummary.TotalAnomalies > 0 ? anomalySummary : null
            });

            _context.InventoryAlertLogs.Add(new InventoryAlertLog
            {
                BusinessId = request.BusinessId,
                AlertType = InventoryAlertTypes.ReorderDigest,
                TriggerSource = request.TriggerSource,
                RecipientEmail = request.RecipientEmail,
                RecipientName = request.RecipientName,
                RecommendationCount = summary.TotalRecommendations,
                RecommendedUnits = summary.RecommendedUnits,
                AlertSignature = signature,
                RecommendationSnapshotJson = JsonSerializer.Serialize(summary.Items.Select(item => new
                {
                    item.ItemId,
                    item.ItemName,
                    item.CurrentStock,
                    item.RecommendedReorderQuantity,
                    item.Urgency,
                    item.AverageDailyDemand
                })),
                SentAt = now
            });

            await _context.SaveChangesAsync(cancellationToken);

            return InventoryAlertDispatchResult.Sent(summary, anomalySummary, now);
        }

        private static string BuildAlertSignature(InventoryAlertDispatchRequest request, InventoryReorderSummaryViewModel summary, InventoryAnomalySummaryViewModel anomalySummary)
        {
            var payload = JsonSerializer.Serialize(new
            {
                request.BusinessId,
                request.RecipientEmail,
                request.LookbackDays,
                request.LeadTimeDays,
                request.SafetyStockDays,
                request.TargetCoverageDays,
                Items = summary.Items.Select(item => new
                {
                    item.ItemId,
                    item.CurrentStock,
                    item.RecommendedReorderQuantity,
                    item.Urgency
                }),
                Anomalies = anomalySummary.Items.Select(item => new
                {
                    item.ItemId,
                    item.AnomalyType,
                    item.Severity,
                    item.RecentPeriodUnits,
                    item.BaselinePeriodUnits
                })
            });

            var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(payload));
            return Convert.ToHexString(hashBytes);
        }
    }

    public static class InventoryAlertTypes
    {
        public const string ReorderDigest = "ReorderDigest";
    }

    public static class InventoryAlertSources
    {
        public const string Automation = "Automation";
        public const string ManualTest = "ManualTest";
    }

    public class InventoryAlertDispatchRequest
    {
        public int BusinessId { get; set; }
        public string BusinessName { get; set; } = string.Empty;
        public string RecipientEmail { get; set; } = string.Empty;
        public string RecipientName { get; set; } = string.Empty;
        public int LookbackDays { get; set; }
        public int LeadTimeDays { get; set; }
        public int SafetyStockDays { get; set; }
        public int TargetCoverageDays { get; set; }
        public string TriggerSource { get; set; } = InventoryAlertSources.Automation;
        public bool ForceSend { get; set; }
        public bool IncludeAnomalies { get; set; }
    }

    public class InventoryAlertDispatchResult
    {
        public bool WasSent { get; private set; }
        public bool WasDeduped { get; private set; }
        public bool HasRecommendations { get; private set; }
        public InventoryReorderSummaryViewModel Summary { get; private set; } = new();
        public InventoryAnomalySummaryViewModel AnomalySummary { get; private set; } = new();
        public DateTime? SentAt { get; private set; }
        public DateTime? LastSentAt { get; private set; }

        public static InventoryAlertDispatchResult Sent(InventoryReorderSummaryViewModel summary, InventoryAnomalySummaryViewModel anomalySummary, DateTime sentAt)
        {
            return new InventoryAlertDispatchResult
            {
                WasSent = true,
                HasRecommendations = summary.TotalRecommendations > 0,
                Summary = summary,
                AnomalySummary = anomalySummary,
                SentAt = sentAt
            };
        }

        public static InventoryAlertDispatchResult Deduped(InventoryReorderSummaryViewModel summary, InventoryAnomalySummaryViewModel anomalySummary, DateTime lastSentAt)
        {
            return new InventoryAlertDispatchResult
            {
                WasDeduped = true,
                HasRecommendations = summary.TotalRecommendations > 0,
                Summary = summary,
                AnomalySummary = anomalySummary,
                LastSentAt = lastSentAt
            };
        }

        public static InventoryAlertDispatchResult NoRecommendations(InventoryReorderSummaryViewModel summary, InventoryAnomalySummaryViewModel anomalySummary)
        {
            return new InventoryAlertDispatchResult
            {
                HasRecommendations = false,
                Summary = summary,
                AnomalySummary = anomalySummary
            };
        }
    }
}