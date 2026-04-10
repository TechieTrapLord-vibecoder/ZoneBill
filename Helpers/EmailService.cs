using SendGrid;
using SendGrid.Helpers.Mail;

namespace ZoneBill_Lloren.Helpers
{
    public interface IEmailService
    {
        Task SendPasswordResetEmailAsync(string toEmail, string toName, string resetLink);
        Task SendLowStockAlertAsync(string toEmail, string toName, string itemName, string businessName);
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
    }
}
