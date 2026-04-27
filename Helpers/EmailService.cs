using SendGrid;
using SendGrid.Helpers.Mail;

namespace ZoneBill_Lloren.Helpers
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
        Task SendLowStockAlertAsync(string toEmail, string toName, string itemName, string businessName);
        Task SendUnpaidInvoiceSummaryAsync(string toEmail, string toName, int count, string businessName);
        Task SendLowStockDigestAsync(string toEmail, string toName, List<string> itemNames, string businessName);
        Task SendStaleShiftAlertAsync(string toEmail, string toName, string cashierName, DateTime openedAt, string businessName);
        Task SendCustomerReceiptAsync(string toEmail, string businessName, string spaceName, string referenceCode, decimal timeCharge, decimal menuTotal, decimal taxAmount, decimal total, List<(string Name, int Qty, decimal LineTotal)> items);
    }

    public class EmailService : IEmailService
    {
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
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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

        public async Task SendStaleShiftAlertAsync(string toEmail, string toName, string cashierName, DateTime openedAt, string businessName)
        {
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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
            if (string.IsNullOrWhiteSpace(_apiKey) || _apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
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
    }
}
