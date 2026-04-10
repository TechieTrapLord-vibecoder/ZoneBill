using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Linq;
using System.Text.Json;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [ApiController]
    [AllowAnonymous]
    [Route("api/stripe/webhook")]
    public class StripeWebhookController : ControllerBase
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public StripeWebhookController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        [HttpPost]
        public async Task<IActionResult> Handle()
        {
            var webhookSecret = _configuration["Stripe:WebhookSecret"];
            if (string.IsNullOrWhiteSpace(webhookSecret) || webhookSecret.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                return BadRequest("Stripe webhook secret is not configured.");
            }

            var json = await new StreamReader(HttpContext.Request.Body).ReadToEndAsync();
            var signatureHeader = Request.Headers["Stripe-Signature"].ToString();
            if (string.IsNullOrWhiteSpace(signatureHeader))
            {
                return BadRequest("Missing Stripe signature header.");
            }

            try
            {
                EventUtility.ConstructEvent(json, signatureHeader, webhookSecret);
            }
            catch (StripeException)
            {
                return BadRequest("Invalid Stripe signature.");
            }
            catch (Exception)
            {
                return BadRequest("Unable to parse Stripe event.");
            }

            using var document = JsonDocument.Parse(json);
            if (!document.RootElement.TryGetProperty("type", out var eventTypeElement))
            {
                return BadRequest("Missing Stripe event type.");
            }

            if (!document.RootElement.TryGetProperty("data", out var dataElement) ||
                !dataElement.TryGetProperty("object", out var eventObject))
            {
                return BadRequest("Missing Stripe event payload.");
            }

            var eventType = eventTypeElement.GetString() ?? string.Empty;
            switch (eventType)
            {
                case "checkout.session.completed":
                    await HandleCheckoutSessionCompletedAsync(eventObject);
                    break;
                case "invoice.payment_succeeded":
                    await HandleInvoicePaymentSucceededAsync(eventObject);
                    break;
                case "invoice.payment_failed":
                    await HandleInvoicePaymentFailedAsync(eventObject);
                    break;
                case "customer.subscription.updated":
                case "customer.subscription.deleted":
                    await HandleSubscriptionStateChangeAsync(eventObject, eventType);
                    break;
            }

            await _context.SaveChangesAsync();
            return Ok();
        }

        private async Task HandleCheckoutSessionCompletedAsync(JsonElement sessionObject)
        {
            // Check if this is a brand-new business registration (no BusinessId yet, uses PendingToken)
            if (TryGetMetadataValue(sessionObject, "PendingToken", out var pendingToken) && !string.IsNullOrWhiteSpace(pendingToken))
            {
                await ActivatePendingRegistrationAsync(pendingToken);
                return;
            }

            var business = await FindBusinessFromSessionAsync(sessionObject);
            if (business == null)
            {
                return;
            }

            var customerId = GetIdFromStringOrObject(sessionObject, "customer");
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                business.StripeCustomerId = customerId;
            }

            var subscriptionId = GetIdFromStringOrObject(sessionObject, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                business.StripeSubscriptionId = subscriptionId;
            }

            // Apply the PlanId from metadata if present
            if (TryGetMetadataValue(sessionObject, "PlanId", out var planIdStr) && int.TryParse(planIdStr, out var planId))
            {
                var plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);
                if (plan != null)
                {
                    business.PlanId = plan.PlanId;
                }
            }

            var paymentStatus = GetString(sessionObject, "payment_status");
            if (paymentStatus == "paid")
            {
                business.SubscriptionStatus = "Active";
                business.CurrentPeriodEnd = PhilippineTime.Now.AddMonths(1);
            }
            else if (business.SubscriptionStatus != "Active")
            {
                business.SubscriptionStatus = "PendingPayment";
            }
        }

        /// <summary>
        /// Webhook fallback: activates a PendingRegistration if the user paid but closed the browser
        /// before being redirected to /Account/RegistrationSuccess.
        /// </summary>
        private async Task ActivatePendingRegistrationAsync(string token)
        {
            var pending = await _context.PendingRegistrations
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.Token == token && !p.IsUsed);

            if (pending == null) return;

            // Email already taken (edge case)
            if (await _context.Users.AnyAsync(u => u.EmailAddress == pending.EmailAddress))
            {
                pending.IsUsed = true;
                return;
            }

            // Build a unique domain prefix without AccountController helpers
            var basePrefix = new string((pending.BusinessName ?? "").Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            if (string.IsNullOrWhiteSpace(basePrefix)) basePrefix = "b" + Guid.NewGuid().ToString("N")[..5];
            var domainPrefix = basePrefix;
            var suffix = 1;
            while (await _context.Businesses.AnyAsync(b => b.DomainPrefix == domainPrefix))
                domainPrefix = $"{basePrefix}{suffix++}";

            var newBusiness = new Business
            {
                PlanId = pending.PlanId,
                BusinessName = pending.BusinessName,
                DomainPrefix = domainPrefix,
                SubscriptionStatus = "Active",
                CurrentPeriodEnd = PhilippineTime.Now.AddMonths(1),
                CreatedAt = PhilippineTime.Now,
                IsActive = true
            };

            _context.Businesses.Add(newBusiness);
            await _context.SaveChangesAsync();

            _context.ChartOfAccounts.AddRange(
                new ChartOfAccount { BusinessId = newBusiness.BusinessId, AccountName = "Cash", AccountType = "Asset", IsActive = true },
                new ChartOfAccount { BusinessId = newBusiness.BusinessId, AccountName = "Accounts Receivable", AccountType = "Asset", IsActive = true },
                new ChartOfAccount { BusinessId = newBusiness.BusinessId, AccountName = "Sales Revenue", AccountType = "Revenue", IsActive = true },
                new ChartOfAccount { BusinessId = newBusiness.BusinessId, AccountName = "Discount Expense", AccountType = "Expense", IsActive = true }
            );
            await _context.SaveChangesAsync();

            _context.Users.Add(new User
            {
                BusinessId = newBusiness.BusinessId,
                UserRole = "MainAdmin",
                FirstName = pending.FirstName,
                LastName = pending.LastName,
                EmailAddress = pending.EmailAddress,
                PasswordHash = pending.PasswordHash,
                IsActive = true
            });

            pending.IsUsed = true;
            // SaveChangesAsync is called by the main Handle() method after all handlers complete
        }

        private async Task HandleInvoicePaymentSucceededAsync(JsonElement invoiceObject)
        {
            var business = await FindBusinessFromInvoiceAsync(invoiceObject);
            if (business == null)
            {
                return;
            }

            var customerId = GetIdFromStringOrObject(invoiceObject, "customer");
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                business.StripeCustomerId = customerId;
            }

            var subscriptionId = GetIdFromStringOrObject(invoiceObject, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                business.StripeSubscriptionId = subscriptionId;
            }

            var stripePriceId = GetPriceIdFromInvoice(invoiceObject);
            var stripeProductId = GetProductIdFromInvoice(invoiceObject);
            SubscriptionPlan? plan = null;
            if (!string.IsNullOrWhiteSpace(stripePriceId) || !string.IsNullOrWhiteSpace(stripeProductId))
            {
                plan = await _context.SubscriptionPlans.FirstOrDefaultAsync(p =>
                    p.IsActive &&
                    (p.StripePriceId == stripePriceId || p.StripePriceId == stripeProductId));
            }

            plan ??= await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == business.PlanId);
            if (plan != null)
            {
                business.PlanId = plan.PlanId;
            }

            var periodStartUnix = GetLongFromInvoiceLinePeriod(invoiceObject, "start");
            var periodEndUnix = GetLongFromInvoiceLinePeriod(invoiceObject, "end");

            var periodStart = periodStartUnix.HasValue
                ? PhilippineTime.ToDateTime(DateTimeOffset.FromUnixTimeSeconds(periodStartUnix.Value).UtcDateTime)
                : PhilippineTime.Now;
            var periodEnd = periodEndUnix.HasValue
                ? PhilippineTime.ToDateTime(DateTimeOffset.FromUnixTimeSeconds(periodEndUnix.Value).UtcDateTime)
                : periodStart.AddMonths(1);

            business.SubscriptionStatus = "Active";
            business.CurrentPeriodEnd = periodEnd;

            var externalReference = GetString(invoiceObject, "id");
            if (!string.IsNullOrWhiteSpace(externalReference))
            {
                var alreadyExists = await _context.SubscriptionInvoices.AnyAsync(i => i.ExternalReference == externalReference);
                if (alreadyExists)
                {
                    return;
                }
            }

            var issuedAtUnix = GetLong(invoiceObject, "created");
            var issuedAt = issuedAtUnix.HasValue
                ? PhilippineTime.ToDateTime(DateTimeOffset.FromUnixTimeSeconds(issuedAtUnix.Value).UtcDateTime)
                : PhilippineTime.Now;
            var paidAtUnix = GetLongFromNestedObject(invoiceObject, "status_transitions", "paid_at");
            var paidAt = paidAtUnix.HasValue
                ? PhilippineTime.ToDateTime(DateTimeOffset.FromUnixTimeSeconds(paidAtUnix.Value).UtcDateTime)
                : issuedAt;

            var amountPaidCents = GetLong(invoiceObject, "amount_paid");
            var totalCents = GetLong(invoiceObject, "total");
            var selectedCents = amountPaidCents.HasValue && amountPaidCents.Value > 0 ? amountPaidCents.Value : totalCents.GetValueOrDefault();
            var amountPaid = selectedCents / 100m;

            _context.SubscriptionInvoices.Add(new SubscriptionInvoice
            {
                BusinessId = business.BusinessId,
                PlanId = plan?.PlanId ?? business.PlanId,
                Amount = amountPaid,
                Status = "Paid",
                PaymentMethod = "Stripe",
                IssuedAt = issuedAt,
                PaidAt = paidAt,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                ExternalReference = externalReference
            });
        }

        private async Task HandleInvoicePaymentFailedAsync(JsonElement invoiceObject)
        {
            var business = await FindBusinessFromInvoiceAsync(invoiceObject);
            if (business == null)
            {
                return;
            }

            business.SubscriptionStatus = "PastDue";

            var subscriptionId = GetIdFromStringOrObject(invoiceObject, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                business.StripeSubscriptionId = subscriptionId;
            }
        }

        private async Task HandleSubscriptionStateChangeAsync(JsonElement subscriptionObject, string eventType)
        {
            var customerId = GetIdFromStringOrObject(subscriptionObject, "customer");
            if (string.IsNullOrWhiteSpace(customerId))
            {
                return;
            }

            var business = await _context.Businesses.FirstOrDefaultAsync(b => b.StripeCustomerId == customerId);
            if (business == null)
            {
                return;
            }

            var subscriptionId = GetString(subscriptionObject, "id");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                business.StripeSubscriptionId = subscriptionId;
            }

            var stripeStatus = GetString(subscriptionObject, "status")?.ToLowerInvariant();
            if (eventType == "customer.subscription.deleted" || stripeStatus is "canceled" or "unpaid")
            {
                business.SubscriptionStatus = "Cancelled";
                return;
            }

            if (stripeStatus is "past_due" or "incomplete_expired")
            {
                business.SubscriptionStatus = "PastDue";
                return;
            }

            if (stripeStatus is "active" or "trialing")
            {
                business.SubscriptionStatus = "Active";
            }

            var currentPeriodEndUnix = GetLong(subscriptionObject, "current_period_end");
            if (currentPeriodEndUnix.HasValue)
            {
                business.CurrentPeriodEnd = PhilippineTime.ToDateTime(DateTimeOffset.FromUnixTimeSeconds(currentPeriodEndUnix.Value).UtcDateTime);
            }
        }

        private async Task<Business?> FindBusinessFromSessionAsync(JsonElement sessionObject)
        {
            if (TryGetMetadataValue(sessionObject, "BusinessId", out var businessIdValue) && int.TryParse(businessIdValue, out var businessId))
            {
                return await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == businessId);
            }

            var clientReferenceId = GetString(sessionObject, "client_reference_id");
            if (int.TryParse(clientReferenceId, out var clientReferenceBusinessId))
            {
                return await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == clientReferenceBusinessId);
            }

            return null;
        }

        private async Task<Business?> FindBusinessFromInvoiceAsync(JsonElement invoiceObject)
        {
            if (TryGetMetadataValue(invoiceObject, "BusinessId", out var businessIdValue) && int.TryParse(businessIdValue, out var metadataBusinessId))
            {
                var byMetadata = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == metadataBusinessId);
                if (byMetadata != null)
                {
                    return byMetadata;
                }
            }

            var customerId = GetIdFromStringOrObject(invoiceObject, "customer");
            if (!string.IsNullOrWhiteSpace(customerId))
            {
                var byCustomer = await _context.Businesses.FirstOrDefaultAsync(b => b.StripeCustomerId == customerId);
                if (byCustomer != null)
                {
                    return byCustomer;
                }
            }

            var subscriptionId = GetIdFromStringOrObject(invoiceObject, "subscription");
            if (!string.IsNullOrWhiteSpace(subscriptionId))
            {
                return await _context.Businesses.FirstOrDefaultAsync(b => b.StripeSubscriptionId == subscriptionId);
            }

            return null;
        }

        private static string? GetString(JsonElement source, string propertyName)
        {
            if (!source.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.String ? property.GetString() : null;
        }

        private static long? GetLong(JsonElement source, string propertyName)
        {
            if (!source.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            return property.ValueKind == JsonValueKind.Number && property.TryGetInt64(out var value) ? value : null;
        }

        private static long? GetLongFromNestedObject(JsonElement source, string nestedObjectName, string propertyName)
        {
            if (!source.TryGetProperty(nestedObjectName, out var nestedObject) || nestedObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            return GetLong(nestedObject, propertyName);
        }

        private static string? GetIdFromStringOrObject(JsonElement source, string propertyName)
        {
            if (!source.TryGetProperty(propertyName, out var property))
            {
                return null;
            }

            if (property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }

            if (property.ValueKind == JsonValueKind.Object && property.TryGetProperty("id", out var idProperty) && idProperty.ValueKind == JsonValueKind.String)
            {
                return idProperty.GetString();
            }

            return null;
        }

        private static bool TryGetMetadataValue(JsonElement source, string key, out string value)
        {
            value = string.Empty;
            if (!source.TryGetProperty("metadata", out var metadata) || metadata.ValueKind != JsonValueKind.Object)
            {
                return false;
            }

            if (!metadata.TryGetProperty(key, out var metadataValue) || metadataValue.ValueKind != JsonValueKind.String)
            {
                return false;
            }

            value = metadataValue.GetString() ?? string.Empty;
            return !string.IsNullOrWhiteSpace(value);
        }

        private static string? GetPriceIdFromInvoice(JsonElement invoiceObject)
        {
            if (!invoiceObject.TryGetProperty("lines", out var linesObject) || linesObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!linesObject.TryGetProperty("data", out var linesArray) || linesArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var lineItem in linesArray.EnumerateArray())
            {
                if (!lineItem.TryGetProperty("price", out var priceObject) || priceObject.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var priceId = GetString(priceObject, "id");
                if (!string.IsNullOrWhiteSpace(priceId))
                {
                    return priceId;
                }
            }

            return null;
        }

        private static string? GetProductIdFromInvoice(JsonElement invoiceObject)
        {
            if (!invoiceObject.TryGetProperty("lines", out var linesObject) || linesObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!linesObject.TryGetProperty("data", out var linesArray) || linesArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var lineItem in linesArray.EnumerateArray())
            {
                if (!lineItem.TryGetProperty("price", out var priceObject) || priceObject.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var productId = GetIdFromStringOrObject(priceObject, "product");
                if (!string.IsNullOrWhiteSpace(productId))
                {
                    return productId;
                }
            }

            return null;
        }

        private static long? GetLongFromInvoiceLinePeriod(JsonElement invoiceObject, string periodProperty)
        {
            if (!invoiceObject.TryGetProperty("lines", out var linesObject) || linesObject.ValueKind != JsonValueKind.Object)
            {
                return null;
            }

            if (!linesObject.TryGetProperty("data", out var linesArray) || linesArray.ValueKind != JsonValueKind.Array)
            {
                return null;
            }

            foreach (var lineItem in linesArray.EnumerateArray())
            {
                if (!lineItem.TryGetProperty("period", out var periodObject) || periodObject.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                var value = GetLong(periodObject, periodProperty);
                if (value.HasValue)
                {
                    return value;
                }
            }

            return null;
        }
    }
}
