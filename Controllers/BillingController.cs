using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Stripe;
using Stripe.Checkout;
using System.Security.Claims;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "MainAdmin")]
    public class BillingController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;

        public BillingController(ApplicationDbContext context, IConfiguration configuration)
        {
            _context = context;
            _configuration = configuration;
        }

        public async Task<IActionResult> Index()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            var availablePlans = await _context.SubscriptionPlans
                .Where(p => p.IsActive)
                .OrderBy(p => p.MonthlyPrice)
                .ToListAsync();

            var invoices = await _context.SubscriptionInvoices
                .Include(i => i.Plan)
                .Where(i => i.BusinessId == business.BusinessId)
                .OrderByDescending(i => i.IssuedAt)
                .Take(12)
                .ToListAsync();

            var stripeSecretKey = _configuration["Stripe:SecretKey"];
            var hasConfiguredSecret = !string.IsNullOrWhiteSpace(stripeSecretKey) && !stripeSecretKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
            var hasStripeSubscription = !string.IsNullOrWhiteSpace(business.StripeSubscriptionId) && !business.StripeSubscriptionId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase);
            var canManageStripeSubscription = hasConfiguredSecret && hasStripeSubscription;
            var isCancellationScheduled = false;
            DateTime? cancellationEffectiveDate = business.CurrentPeriodEnd;
            var subscriptionManagementStatus = business.Plan.MonthlyPrice <= 0m
                ? "Free plans do not create a Stripe subscription. Switch plans anytime from the cards below."
                : "No linked Stripe subscription was found for this business yet.";

            if (canManageStripeSubscription)
            {
                try
                {
                    var subscription = await new SubscriptionService().GetAsync(business.StripeSubscriptionId);
                    isCancellationScheduled = subscription.CancelAtPeriodEnd;
                    cancellationEffectiveDate = subscription.CancelAt ?? business.CurrentPeriodEnd;
                    subscriptionManagementStatus = isCancellationScheduled
                        ? $"Your subscription will remain active until {(subscription.CancelAt ?? business.CurrentPeriodEnd ?? PhilippineTime.Now):yyyy-MM-dd}."
                        : "Your subscription is managed in-app. You can schedule cancellation at period end and resume before renewal.";
                }
                catch (StripeException ex)
                {
                    canManageStripeSubscription = false;
                    subscriptionManagementStatus = $"Unable to sync Stripe subscription details right now: {ex.Message}";
                }
            }

            var viewModel = new BillingPageViewModel
            {
                Business = business,
                CurrentPlan = business.Plan,
                AvailablePlans = availablePlans,
                RecentInvoices = invoices,
                IsSubscriptionExpired = IsSubscriptionExpired(business),
                CanManageStripeSubscription = canManageStripeSubscription,
                IsCancellationScheduled = isCancellationScheduled,
                CancellationEffectiveDate = cancellationEffectiveDate,
                SubscriptionManagementStatus = subscriptionManagementStatus,
                PlanComparisonRows = BuildPlanComparisonRows(availablePlans)
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CancelSubscription()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            if (string.IsNullOrWhiteSpace(business.StripeSubscriptionId))
            {
                TempData["Error"] = "No active Stripe subscription is linked to this business.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var subscription = await new SubscriptionService().UpdateAsync(
                    business.StripeSubscriptionId,
                    new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = true
                    });

                business.CurrentPeriodEnd = subscription.CancelAt ?? business.CurrentPeriodEnd;
                await _context.SaveChangesAsync();

                TempData["Success"] = $"Subscription cancellation scheduled. Access will remain active until {(subscription.CancelAt ?? business.CurrentPeriodEnd ?? PhilippineTime.Now):yyyy-MM-dd}.";
            }
            catch (StripeException ex)
            {
                TempData["Error"] = $"Unable to schedule cancellation: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResumeSubscription()
        {
            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            if (string.IsNullOrWhiteSpace(business.StripeSubscriptionId))
            {
                TempData["Error"] = "No Stripe subscription is linked to this business.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                var subscription = await new SubscriptionService().UpdateAsync(
                    business.StripeSubscriptionId,
                    new SubscriptionUpdateOptions
                    {
                        CancelAtPeriodEnd = false
                    });

                await _context.SaveChangesAsync();

                TempData["Success"] = "Scheduled cancellation removed. Your subscription will renew normally.";
            }
            catch (StripeException ex)
            {
                TempData["Error"] = $"Unable to resume subscription: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PayAndSetPlan(int planId)
        {
            if (!ModelState.IsValid)
            {
                TempData["Error"] = "Unable to process billing request.";
                return RedirectToAction(nameof(Index));
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            var plan = await _context.SubscriptionPlans
                .FirstOrDefaultAsync(p => p.PlanId == planId && p.IsActive);
            if (plan == null)
            {
                TempData["Error"] = "Selected plan is not available.";
                return RedirectToAction(nameof(Index));
            }

            if (plan.MonthlyPrice <= 0m)
            {
                var periodStart = PhilippineTime.Now;
                var periodEnd = periodStart.AddMonths(1);

                business.PlanId = plan.PlanId;
                business.SubscriptionStatus = "Active";
                business.CurrentPeriodEnd = periodEnd;
                business.StripeSubscriptionId = null;

                _context.SubscriptionInvoices.Add(new SubscriptionInvoice
                {
                    BusinessId = business.BusinessId,
                    PlanId = plan.PlanId,
                    Amount = 0m,
                    Status = "Paid",
                    PaymentMethod = "FreeTier",
                    IssuedAt = periodStart,
                    PaidAt = periodStart,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    ExternalReference = $"FREE-{Guid.NewGuid():N}"
                });

                await _context.SaveChangesAsync();
                TempData["Success"] = "Free testing tier activated. Upgrade anytime to unlock POS, Inventory, Reports, and Shifts.";
                return RedirectToAction(nameof(Index));
            }

            if (string.IsNullOrWhiteSpace(plan.StripePriceId) || plan.StripePriceId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "This plan is not yet linked to Stripe (price or product ID missing).";
                return RedirectToAction(nameof(Index));
            }

            var stripeSecretKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(stripeSecretKey) || stripeSecretKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Stripe Secret Key is not configured yet. Please add your Stripe keys in appsettings.";
                return RedirectToAction(nameof(Index));
            }

            var checkoutPriceId = await ResolveCheckoutPriceIdAsync(plan);
            if (string.IsNullOrWhiteSpace(checkoutPriceId))
            {
                TempData["Error"] = "Unable to resolve a valid Stripe monthly price for this plan.";
                return RedirectToAction(nameof(Index));
            }

            var domain = $"{Request.Scheme}://{Request.Host}";
            var options = new SessionCreateOptions
            {
                Mode = "subscription",
                SuccessUrl = $"{domain}/Billing/CheckoutSuccess?session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/Billing/CheckoutCancelled",
                ClientReferenceId = business.BusinessId.ToString(),
                Customer = string.IsNullOrWhiteSpace(business.StripeCustomerId) ? null : business.StripeCustomerId,
                CustomerEmail = string.IsNullOrWhiteSpace(business.StripeCustomerId) ? User.FindFirstValue(ClaimTypes.Email) : null,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions
                    {
                        Price = checkoutPriceId,
                        Quantity = 1
                    }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["BusinessId"] = business.BusinessId.ToString(),
                    ["PlanId"] = plan.PlanId.ToString()
                },
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["BusinessId"] = business.BusinessId.ToString(),
                        ["PlanId"] = plan.PlanId.ToString()
                    }
                }
            };

            try
            {
                var service = new SessionService();
                var session = await service.CreateAsync(options);

                if (string.IsNullOrWhiteSpace(session.Url))
                {
                    TempData["Error"] = "Stripe did not return a checkout URL.";
                    return RedirectToAction(nameof(Index));
                }

                return Redirect(session.Url);
            }
            catch (StripeException ex)
            {
                TempData["Error"] = $"Stripe checkout failed: {ex.Message}";
                return RedirectToAction(nameof(Index));
            }
        }

        [HttpGet]
        public async Task<IActionResult> CheckoutSuccess(string? session_id)
        {
            if (string.IsNullOrWhiteSpace(session_id))
            {
                TempData["Success"] = "Checkout completed. Your subscription will be updated shortly.";
                return RedirectToAction(nameof(Index));
            }

            var businessId = GetBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses
                .FirstOrDefaultAsync(b => b.BusinessId == businessId.Value);
            if (business == null) return NotFound();

            try
            {
                var sessionService = new SessionService();
                var session = await sessionService.GetAsync(session_id);

                if (session.PaymentStatus != "paid")
                {
                    TempData["Success"] = "Checkout completed. Waiting for payment confirmation.";
                    return RedirectToAction(nameof(Index));
                }

                // Extract PlanId from session metadata
                int? targetPlanId = null;
                if (session.Metadata.TryGetValue("PlanId", out var planIdStr) && int.TryParse(planIdStr, out var pid))
                {
                    targetPlanId = pid;
                }

                var plan = targetPlanId.HasValue
                    ? await _context.SubscriptionPlans.FirstOrDefaultAsync(p => p.PlanId == targetPlanId.Value && p.IsActive)
                    : null;

                if (plan != null)
                {
                    business.PlanId = plan.PlanId;
                }

                // Update Stripe references
                var customerId = session.CustomerId;
                if (!string.IsNullOrWhiteSpace(customerId))
                {
                    business.StripeCustomerId = customerId;
                }

                var subscriptionId = session.SubscriptionId;
                if (!string.IsNullOrWhiteSpace(subscriptionId))
                {
                    business.StripeSubscriptionId = subscriptionId;
                }

                business.SubscriptionStatus = "Active";
                business.CurrentPeriodEnd = PhilippineTime.Now.AddMonths(1);

                // Record subscription invoice if not already recorded
                var externalReference = $"CHECKOUT-{session.Id}";
                var alreadyExists = await _context.SubscriptionInvoices.AnyAsync(i => i.ExternalReference == externalReference);
                if (!alreadyExists)
                {
                    _context.SubscriptionInvoices.Add(new SubscriptionInvoice
                    {
                        BusinessId = business.BusinessId,
                        PlanId = plan?.PlanId ?? business.PlanId,
                        Amount = (session.AmountTotal ?? 0) / 100m,
                        Status = "Paid",
                        PaymentMethod = "Stripe",
                        IssuedAt = PhilippineTime.Now,
                        PaidAt = PhilippineTime.Now,
                        PeriodStart = PhilippineTime.Now,
                        PeriodEnd = PhilippineTime.Now.AddMonths(1),
                        ExternalReference = externalReference
                    });
                }

                await _context.SaveChangesAsync();

                TempData["Success"] = $"Payment successful! Your plan has been switched to {plan?.PlanName ?? "the selected plan"}.";
            }
            catch (StripeException)
            {
                TempData["Success"] = "Checkout completed. Your subscription will be updated shortly via webhook.";
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public IActionResult CheckoutCancelled()
        {
            TempData["Error"] = "Stripe checkout was cancelled.";
            return RedirectToAction(nameof(Index));
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private static bool IsSubscriptionExpired(Business business)
        {
            if (business.SubscriptionStatus != "Active") return true;
            if (!business.CurrentPeriodEnd.HasValue) return true;
            return business.CurrentPeriodEnd.Value <= PhilippineTime.Now;
        }

        private static async Task<string?> ResolveCheckoutPriceIdAsync(SubscriptionPlan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.StripePriceId))
            {
                return null;
            }

            if (plan.StripePriceId.StartsWith("price_", StringComparison.OrdinalIgnoreCase))
            {
                return plan.StripePriceId;
            }

            if (!plan.StripePriceId.StartsWith("prod_", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            var expectedAmountCents = decimal.ToInt64(plan.MonthlyPrice * 100m);
            var priceService = new PriceService();
            var prices = await priceService.ListAsync(new PriceListOptions
            {
                Product = plan.StripePriceId,
                Active = true,
                Type = "recurring",
                Limit = 100
            });

            var exactPrice = prices.Data.FirstOrDefault(p =>
                p.Recurring?.Interval == "month" &&
                string.Equals(p.Currency, "php", StringComparison.OrdinalIgnoreCase) &&
                p.UnitAmount == expectedAmountCents);

            if (!string.IsNullOrWhiteSpace(exactPrice?.Id))
            {
                return exactPrice.Id;
            }

            return prices.Data
                .FirstOrDefault(p => p.Recurring?.Interval == "month")
                ?.Id;
        }

        private static List<BillingPlanComparisonRow> BuildPlanComparisonRows(IReadOnlyList<SubscriptionPlan> plans)
        {
            return new List<BillingPlanComparisonRow>
            {
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Monthly price",
                    Values = plans.Select(plan => plan.MonthlyPrice <= 0m ? "Free" : plan.MonthlyPrice.ToString("C")).ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Max tables",
                    Values = plans.Select(plan => plan.MaxTablesAllowed.ToString()).ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "POS and checkout",
                    Values = plans.Select(_ => "Included").ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Inventory tracking",
                    Values = plans.Select(_ => "Included").ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Financial reports",
                    Values = plans.Select(_ => "Included").ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Shift and drawer controls",
                    Values = plans.Select(_ => "Included").ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Billing mode",
                    Values = plans.Select(plan => plan.MonthlyPrice <= 0m ? "No Stripe charge" : "Stripe monthly renewal").ToList()
                },
                new BillingPlanComparisonRow
                {
                    FeatureLabel = "Best fit",
                    Values = plans.Select(DescribePlanFit).ToList()
                }
            };
        }

        private static string DescribePlanFit(SubscriptionPlan plan)
        {
            if (plan.MonthlyPrice <= 0m)
            {
                return "Testing and setup";
            }

            if (plan.MaxTablesAllowed <= 10)
            {
                return "Small floor operations";
            }

            if (plan.MaxTablesAllowed <= 25)
            {
                return "Growing venues";
            }

            return "High-volume branches";
        }
    }
}
