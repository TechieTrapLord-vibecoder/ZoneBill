using SendGrid;
using SendGrid.Helpers.Mail;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
        Task SendLowStockAlertAsync(string toEmail, string toName, string itemName, string businessName);
        Task SendUnpaidInvoiceSummaryAsync(string toEmail, string toName, int count, string businessName);
        Task SendLowStockDigestAsync(string toEmail, string toName, List<string> itemNames, string businessName);
        Task SendInventoryReorderDigestAsync(InventoryDigestEmailRequest request);
        Task SendStaleShiftAlertAsync(string toEmail, string toName, string cashierName, DateTime openedAt, string businessName);
        Task SendCustomerReceiptAsync(string toEmail, string businessName, string spaceName, string referenceCode, decimal timeCharge, decimal menuTotal, decimal taxAmount, decimal total, List<(string Name, int Qty, decimal LineTotal)> items);
    }

    public class InventoryDigestEmailRequest
    {
        public string ToEmail { get; set; } = string.Empty;
        public string ToName { get; set; } = string.Empty;
        public InventoryReorderSummaryViewModel Summary { get; set; } = new();
        public string BusinessName { get; set; } = string.Empty;
        public int LookbackDays { get; set; }
        public int LeadTimeDays { get; set; }
        public int TargetCoverageDays { get; set; }
        public InventoryAnomalySummaryViewModel? AnomalySummary { get; set; }
    }

    public class EmailService : IEmailService
    {
        private const string PlaceholderToken = "PLACEHOLDER";
        private readonly string _apiKey;
        private readonly string _fromEmail;
        private readonly string _fromName;

        public EmailService(IConfiguration configuration)
        {
            _apiKey = configuration["SendGrid:ApiKey"] ?? string.Empty;
            _fromEmail = configuration["SendGrid:FromEmail"] ?? "noreply@zonebill.app";
            _fromName = configuration["SendGrid:FromName"] ?? "ZoneBill";
        }

        public async Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink)
        {
            if (!IsConfigured())
                return; // silently skip if not configured

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var subject = "Reset your ZoneBill password";
            var plainText = $"Click the link below to reset your password. This link expires in 1 hour.\n\n{resetLink}\n\nIf you did not request a password reset, ignore this email.";
            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;"">
  <h2 style=""color:#0d6efd;"">Reset your ZoneBill password</h2>
  <p>Click the button below to reset your password. This link expires in <strong>1 hour</strong>.</p>
  <a href=""{resetLink}"" style=""display:inline-block;padding:12px 24px;background:#0d6efd;color:#fff;text-decoration:none;border-radius:6px;font-weight:bold;"">Reset Password</a>
  <p style=""margin-top:24px;font-size:13px;color:#666;"">If you did not request a password reset, you can safely ignore this email.</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

        public async Task SendLowStockAlertAsync(string toEmail, string toName, string itemName, string businessName)
        {
            if (!IsConfigured())
                return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var subject = $"[ZoneBill] Low Stock Alert — {itemName}";
            var plainText = $"This is an automated alert from ZoneBill.\n\nItem '{itemName}' at {businessName} is now OUT OF STOCK.\n\nPlease restock it in the Inventory section.";
            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;"">
  <h2 style=""color:#dc3545;"">⚠️ Out of Stock Alert</h2>
  <p>This is an automated alert from <strong>ZoneBill</strong>.</p>
  <p>The following item at <strong>{businessName}</strong> is now <span style=""color:#dc3545;font-weight:bold;"">OUT OF STOCK</span>:</p>
  <div style=""background:#f8d7da;border:1px solid #f5c2c7;border-radius:6px;padding:12px 16px;font-size:1.1rem;font-weight:bold;color:#842029;"">
    {itemName}
  </div>
  <p style=""margin-top:16px;"">Please restock it in the <strong>Inventory</strong> section of ZoneBill.</p>
  <p style=""font-size:12px;color:#888;margin-top:24px;"">You're receiving this because you are a MainAdmin of {businessName}.</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

        public async Task SendUnpaidInvoiceSummaryAsync(string toEmail, string toName, int count, string businessName)
        {
            if (!IsConfigured())
                return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var subject = $"[ZoneBill] {count} Unpaid Invoice{(count != 1 ? "s" : "")} — {businessName}";
            var plainText = $"Good morning! You have {count} unpaid invoice{(count != 1 ? "s" : "")} at {businessName}. Please review them in the Invoices section of ZoneBill.";
            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;"">
  <h2 style=""color:#f97316;"">📋 Daily Invoice Summary</h2>
  <p>Good morning, <strong>{toName}</strong>!</p>
  <p>You currently have <strong style=""color:#f97316;font-size:1.2rem;"">{count}</strong> unpaid invoice{(count != 1 ? "s" : "")} at <strong>{businessName}</strong>.</p>
  <p>Please log in to ZoneBill and review them in the <strong>Invoices</strong> section.</p>
  <p style=""font-size:12px;color:#888;margin-top:24px;"">This is an automated daily summary from ZoneBill.</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

        public async Task SendLowStockDigestAsync(string toEmail, string toName, List<string> itemNames, string businessName)
        {
            if (!IsConfigured())
                return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var subject = $"[ZoneBill] {itemNames.Count} Low-Stock Item{(itemNames.Count != 1 ? "s" : "")} — {businessName}";
            var itemList = string.Join(", ", itemNames);
            var plainText = $"Daily low-stock summary for {businessName}:\n\n{itemList}\n\nPlease restock these items in the Inventory section.";
            var itemHtml = string.Join("", itemNames.Select(n => $"<li style=\"padding:4px 0;\">{System.Net.WebUtility.HtmlEncode(n)}</li>"));
            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;"">
  <h2 style=""color:#dc3545;"">📦 Daily Low-Stock Summary</h2>
  <p>The following <strong>{itemNames.Count}</strong> item{(itemNames.Count != 1 ? "s" : "")} at <strong>{businessName}</strong> are at or below their low-stock threshold:</p>
  <ul style=""background:#f8d7da;border:1px solid #f5c2c7;border-radius:6px;padding:12px 16px 12px 32px;color:#842029;font-weight:600;"">{itemHtml}</ul>
  <p>Please restock them in the <strong>Inventory</strong> section of ZoneBill.</p>
  <p style=""font-size:12px;color:#888;margin-top:24px;"">This is an automated daily digest from ZoneBill.</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

                public async Task SendInventoryReorderDigestAsync(InventoryDigestEmailRequest request)
                {
                    if (!IsConfigured())
                                return;

                    if (request.Summary.Items.Count == 0 && (request.AnomalySummary == null || request.AnomalySummary.TotalAnomalies == 0))
                                return;

                        var client = new SendGridClient(_apiKey);
                        var from = new EmailAddress(_fromEmail, _fromName);
                    var to = new EmailAddress(request.ToEmail, request.ToName);
                    var includeAnomalies = request.AnomalySummary != null && request.AnomalySummary.TotalAnomalies > 0;
                    var subject = BuildInventoryDigestSubject(request, includeAnomalies);
                    var plainText = BuildInventoryDigestPlainText(request, includeAnomalies);
                    var html = BuildInventoryDigestHtml(request, includeAnomalies);

                        var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
                        await client.SendEmailAsync(msg);
                }

        public async Task SendStaleShiftAlertAsync(string toEmail, string toName, string cashierName, DateTime openedAt, string businessName)
        {
            if (!IsConfigured())
                return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail, toName);

            var hours = (int)(PhilippineTime.Now - openedAt).TotalHours;
            var subject = $"[ZoneBill] Shift Open {hours}h+ — {cashierName} at {businessName}";
            var plainText = $"Alert: {cashierName}'s shift at {businessName} has been open for over {hours} hours (since {openedAt:MMM d, h:mm tt}). Please verify and close the shift if needed.";
            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;"">
  <h2 style=""color:#f97316;"">⏰ Stale Shift Alert</h2>
  <p><strong>{cashierName}</strong>'s shift at <strong>{businessName}</strong> has been open for <strong style=""color:#f97316;"">{hours}+ hours</strong>.</p>
  <p>Opened at: <strong>{openedAt:MMMM d, yyyy h:mm tt}</strong></p>
  <p>Please log in to ZoneBill and close the shift if it was left open by mistake.</p>
  <p style=""font-size:12px;color:#888;margin-top:24px;"">This is an automated alert from ZoneBill.</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

        public async Task SendCustomerReceiptAsync(string toEmail, string businessName, string spaceName, string referenceCode, decimal timeCharge, decimal menuTotal, decimal taxAmount, decimal total, List<(string Name, int Qty, decimal LineTotal)> items)
        {
            if (!IsConfigured())
                return;

            var client = new SendGridClient(_apiKey);
            var from = new EmailAddress(_fromEmail, _fromName);
            var to = new EmailAddress(toEmail);

            var subject = $"Your receipt from {businessName} — Ref {referenceCode}";

            var itemRows = string.Join("", items.Select(i =>
                $"<tr><td style=\"padding:6px 12px;border-bottom:1px solid #334155;\">{System.Net.WebUtility.HtmlEncode(i.Name)}</td>" +
                $"<td style=\"padding:6px 12px;border-bottom:1px solid #334155;text-align:center;\">{i.Qty}</td>" +
                $"<td style=\"padding:6px 12px;border-bottom:1px solid #334155;text-align:right;\">₱{i.LineTotal:0.00}</td></tr>"));

            var plainItems = string.Join("\n", items.Select(i => $"  {i.Name} x{i.Qty} — ₱{i.LineTotal:0.00}"));
            var plainText = $"Receipt from {businessName}\nTable: {spaceName} | Ref: {referenceCode}\n\nTable time: ₱{timeCharge:0.00}\n{plainItems}\nMenu total: ₱{menuTotal:0.00}\nTax: ₱{taxAmount:0.00}\nTotal: ₱{total:0.00}\n\nThank you for visiting!";

            var html = $@"
<div style=""font-family:sans-serif;max-width:480px;margin:auto;background:#0F172A;color:#F1F5F9;padding:24px;border-radius:12px;"">
  <h2 style=""color:#06B6D4;text-align:center;margin-bottom:4px;"">Thank you for visiting!</h2>
  <p style=""text-align:center;color:#94A3B8;margin-top:0;"">{System.Net.WebUtility.HtmlEncode(businessName)} &bull; {System.Net.WebUtility.HtmlEncode(spaceName)} &bull; Ref: {System.Net.WebUtility.HtmlEncode(referenceCode ?? "")}</p>
  <table style=""width:100%;border-collapse:collapse;margin-top:16px;"">
    <tr style=""color:#06B6D4;font-weight:600;""><td style=""padding:6px 12px;border-bottom:2px solid #334155;"">Item</td><td style=""padding:6px 12px;border-bottom:2px solid #334155;text-align:center;"">Qty</td><td style=""padding:6px 12px;border-bottom:2px solid #334155;text-align:right;"">Amount</td></tr>
    <tr><td style=""padding:6px 12px;border-bottom:1px solid #334155;"">Table time</td><td style=""padding:6px 12px;border-bottom:1px solid #334155;text-align:center;"">—</td><td style=""padding:6px 12px;border-bottom:1px solid #334155;text-align:right;"">₱{timeCharge:0.00}</td></tr>
    {itemRows}
  </table>
  <div style=""margin-top:12px;padding:8px 12px;display:flex;justify-content:space-between;""><span style=""color:#94A3B8;"">Tax</span><span>₱{taxAmount:0.00}</span></div>
  <div style=""padding:10px 12px;border-top:2px solid #06B6D4;font-size:1.2em;font-weight:700;display:flex;justify-content:space-between;color:#F97316;""><span>Total</span><span>₱{total:0.00}</span></div>
  <p style=""text-align:center;color:#94A3B8;font-size:12px;margin-top:24px;"">Powered by ZoneBill</p>
</div>";

            var msg = MailHelper.CreateSingleEmail(from, to, subject, plainText, html);
            await client.SendEmailAsync(msg);
        }

        private bool IsConfigured()
        {
            return !string.IsNullOrWhiteSpace(_apiKey) && !_apiKey.Contains(PlaceholderToken, StringComparison.OrdinalIgnoreCase);
        }

        private static string BuildInventoryDigestSubject(InventoryDigestEmailRequest request, bool includeAnomalies)
        {
            if (includeAnomalies)
            {
                return $"[ZoneBill] Inventory Digest — {request.BusinessName}";
            }

            var suffix = request.Summary.TotalRecommendations != 1 ? "s" : string.Empty;
            return $"[ZoneBill] {request.Summary.TotalRecommendations} Reorder Recommendation{suffix} — {request.BusinessName}";
        }

        private static string BuildInventoryDigestPlainText(InventoryDigestEmailRequest request, bool includeAnomalies)
        {
            var topItems = request.Summary.Items.Take(8).ToList();
            var plainLines = string.Join("\n", topItems.Select(item => $"- {item.ItemName}: reorder {item.RecommendedReorderQuantity}, stock {item.CurrentStock}, demand {item.AverageDailyDemand:0.##}/day"));

            if (!includeAnomalies)
            {
                return $"Daily inventory recommendations for {request.BusinessName}\n\nRecommendations: {request.Summary.TotalRecommendations}\nCritical items: {request.Summary.CriticalRecommendations}\nRecommended units: {request.Summary.RecommendedUnits}\nLookback: {request.LookbackDays} days\nLead time: {request.LeadTimeDays} days\nTarget coverage: {request.TargetCoverageDays} days\n\nTop recommendations:\n{plainLines}\n\nReview Inventory in ZoneBill to restock these items.";
            }

            var anomalyLines = string.Join("\n", request.AnomalySummary!.Items.Take(6).Select(item => $"- {item.ItemName}: {item.AnomalyType}, {item.SummaryText}"));
            return $"Daily inventory digest for {request.BusinessName}\n\nRecommendations: {request.Summary.TotalRecommendations}\nCritical items: {request.Summary.CriticalRecommendations}\nRecommended units: {request.Summary.RecommendedUnits}\nAnomalies: {request.AnomalySummary.TotalAnomalies}\nLookback: {request.LookbackDays} days\nLead time: {request.LeadTimeDays} days\nTarget coverage: {request.TargetCoverageDays} days\n\nTop reorder recommendations:\n{(string.IsNullOrWhiteSpace(plainLines) ? "- None today" : plainLines)}\n\nTop anomaly signals:\n{(string.IsNullOrWhiteSpace(anomalyLines) ? "- None detected" : anomalyLines)}\n\nReview Inventory in ZoneBill to act on these signals.";
        }

        private static string BuildInventoryDigestHtml(InventoryDigestEmailRequest request, bool includeAnomalies)
        {
            var topItems = request.Summary.Items.Take(8).ToList();
            var rows = string.Join("", topItems.Select(item =>
                $"<tr>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;font-weight:600;\">{System.Net.WebUtility.HtmlEncode(item.ItemName)}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:center;\">{item.Urgency}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;\">{item.CurrentStock}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:right;\">{item.RecommendedReorderQuantity}</td>" +
                $"</tr>"));

            var heading = includeAnomalies ? "Inventory Daily Digest" : "Inventory Recommendations";
            var anomalySection = includeAnomalies ? BuildAnomalySectionHtml(request.AnomalySummary!) : string.Empty;

            return $@"
<div style=""font-family:sans-serif;max-width:640px;margin:auto;"">
    <h2 style=""color:#0f766e;"">{heading}</h2>
    <p>Hello <strong>{System.Net.WebUtility.HtmlEncode(request.ToName)}</strong>, here is today's inventory summary for <strong>{System.Net.WebUtility.HtmlEncode(request.BusinessName)}</strong>.</p>
    <div style=""display:flex;gap:12px;flex-wrap:wrap;margin:16px 0;"">
        <div style=""background:#ecfeff;border:1px solid #a5f3fc;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#155e75;"">Recommendations</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{request.Summary.TotalRecommendations}</div></div>
        <div style=""background:#fff7ed;border:1px solid #fdba74;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#9a3412;"">Critical</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{request.Summary.CriticalRecommendations}</div></div>
        <div style=""background:#f0fdf4;border:1px solid #86efac;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#166534;"">Suggested Units</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{request.Summary.RecommendedUnits}</div></div>
    </div>
    <p style=""color:#475569;"">Model inputs: <strong>{request.LookbackDays}</strong> day lookback, <strong>{request.LeadTimeDays}</strong> day lead time, <strong>{request.TargetCoverageDays}</strong> day target coverage.</p>
    <table style=""width:100%;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;"">
        <thead>
            <tr style=""background:#f8fafc;color:#334155;"">
                <th style=""padding:10px;text-align:left;"">Item</th>
                <th style=""padding:10px;text-align:center;"">Urgency</th>
                <th style=""padding:10px;text-align:right;"">Stock</th>
                <th style=""padding:10px;text-align:right;"">Reorder</th>
            </tr>
        </thead>
        <tbody>{(string.IsNullOrWhiteSpace(rows) ? "<tr><td colspan=\"4\" style=\"padding:10px;color:#64748b;\">No reorder recommendations today.</td></tr>" : rows)}</tbody>
    </table>
    {anomalySection}
    <p style=""margin-top:16px;color:#64748b;font-size:12px;"">Open the Inventory page in ZoneBill to restock directly from these recommendations.</p>
</div>";
        }

        private static string BuildAnomalySectionHtml(InventoryAnomalySummaryViewModel anomalySummary)
        {
            var anomalyRows = string.Join("", anomalySummary.Items.Take(6).Select(item =>
                $"<tr>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;font-weight:600;\">{System.Net.WebUtility.HtmlEncode(item.ItemName)}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:center;\">{item.AnomalyType}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;text-align:center;\">{item.Severity}</td>" +
                $"<td style=\"padding:8px 10px;border-bottom:1px solid #e2e8f0;\">{System.Net.WebUtility.HtmlEncode(item.SummaryText)}</td>" +
                $"</tr>"));

            return $@"
    <div style=""margin-top:18px;display:flex;gap:12px;flex-wrap:wrap;"">
        <div style=""background:#fef2f2;border:1px solid #fca5a5;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#991b1b;"">Demand Spikes</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{anomalySummary.SpikeCount}</div></div>
        <div style=""background:#fff7ed;border:1px solid #fdba74;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#9a3412;"">Dead Stock</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{anomalySummary.DeadStockCount}</div></div>
        <div style=""background:#ecfeff;border:1px solid #a5f3fc;border-radius:8px;padding:12px 14px;min-width:140px;""><div style=""font-size:12px;color:#155e75;"">Sales Drops</div><div style=""font-size:24px;font-weight:700;color:#0f172a;"">{anomalySummary.DropCount}</div></div>
    </div>
    <table style=""width:100%;margin-top:14px;border-collapse:collapse;border:1px solid #e2e8f0;border-radius:8px;overflow:hidden;"">
        <thead>
            <tr style=""background:#f8fafc;color:#334155;"">
                <th style=""padding:10px;text-align:left;"">Item</th>
                <th style=""padding:10px;text-align:center;"">Signal</th>
                <th style=""padding:10px;text-align:center;"">Severity</th>
                <th style=""padding:10px;text-align:left;"">Summary</th>
            </tr>
        </thead>
        <tbody>{anomalyRows}</tbody>
    </table>";
        }
    }
}
