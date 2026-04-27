using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
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
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;

        public AccountController(ApplicationDbContext context, IConfiguration configuration, IEmailService emailService)
        {
            _context = context;
            _configuration = configuration;
            _emailService = emailService;
        }

        // ──────────────────────────────────────────────────────────────────────
        // LOGIN / LOGOUT
        // ──────────────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult Login(string? returnUrl = null)
        {
            if (User.Identity != null && User.Identity.IsAuthenticated)
                return RedirectToRoleDefault(User);

            ViewData["ReturnUrl"] = returnUrl;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Login(LoginViewModel model, string? returnUrl = null)
        {
            ViewData["ReturnUrl"] = returnUrl;

            if (ModelState.IsValid)
            {
                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmailAddress == model.Email && u.IsActive);

                bool passwordValid = false;
                if (user != null)
                {
                    if (user.PasswordHash.StartsWith("$2"))
                    {
                        // BCrypt hash
                        passwordValid = BCrypt.Net.BCrypt.Verify(model.Password, user.PasswordHash);
                    }
                    else
                    {
                        // Legacy plain-text — verify then upgrade to BCrypt
                        if (user.PasswordHash == model.Password)
                        {
                            passwordValid = true;
                            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password);
                            await _context.SaveChangesAsync();
                        }
                    }
                }

                if (passwordValid)
                {
                    var claims = new List<Claim>
                    {
                        new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                        new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                        new Claim(ClaimTypes.Email, user.EmailAddress),
                        new Claim(ClaimTypes.Role, user.UserRole)
                    };

                    if (user.BusinessId.HasValue)
                        claims.Add(new Claim("BusinessId", user.BusinessId.Value.ToString()));

                    var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
                    var authProperties = new AuthenticationProperties
                    {
                        IsPersistent = model.RememberMe,
                        ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7)
                    };

                    await HttpContext.SignInAsync(
                        CookieAuthenticationDefaults.AuthenticationScheme,
                        new ClaimsPrincipal(claimsIdentity),
                        authProperties);

                    if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                        return Redirect(returnUrl);

                    return RedirectToAction("Index", "Home");
                }

                ModelState.AddModelError(string.Empty, "Invalid login attempt. Check your email or password.");
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        // ──────────────────────────────────────────────────────────────────────
        // REGISTER BUSINESS
        // ──────────────────────────────────────────────────────────────────────

        [HttpGet]
        public async Task<IActionResult> RegisterBusiness(int planId)
        {
            // Auto-clean expired pending registrations
            var cutoff = PhilippineTime.Now.AddHours(-2);
            var expired = await _context.PendingRegistrations
                .Where(p => !p.IsUsed && p.ExpiresAt < cutoff)
                .ToListAsync();
            if (expired.Any())
            {
                _context.PendingRegistrations.RemoveRange(expired);
                await _context.SaveChangesAsync();
            }

            var plan = await _context.SubscriptionPlans.FindAsync(planId);
            if (plan == null) return RedirectToAction("Index", "Home");

            ViewBag.PlanName = plan.PlanName;
            ViewBag.Price = plan.MonthlyPrice;

            var model = new RegisterBusinessViewModel { PlanId = planId };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RegisterBusiness(RegisterBusinessViewModel model)
        {
            var plan = await _context.SubscriptionPlans.FindAsync(model.PlanId);
            if (plan == null) return RedirectToAction("Index", "Home");

            if (await _context.Users.AnyAsync(u => u.EmailAddress == model.EmailAddress))
                ModelState.AddModelError("EmailAddress", "Email is already registered.");

            if (!ModelState.IsValid)
            {
                ViewBag.PlanName = plan.PlanName;
                ViewBag.Price = plan.MonthlyPrice;
                return View(model);
            }

            var isFreePlan = plan.MonthlyPrice <= 0m;

            // ── FREE PLAN: create account immediately ─────────────────────────
            if (isFreePlan)
            {
                var business = await CreateBusinessAndUserAsync(model, plan, "Active", PhilippineTime.Now.AddMonths(1));
                if (business == null)
                {
                    ModelState.AddModelError("BusinessName", "That business name is already taken. Please try a slightly different name.");
                    ViewBag.PlanName = plan.PlanName;
                    ViewBag.Price = plan.MonthlyPrice;
                    return View(model);
                }
                return RedirectToAction("Index", "Businesses");
            }

            // ── PAID PLAN: store pending registration and redirect to Stripe ──
            var stripeSecretKey = _configuration["Stripe:SecretKey"];
            if (string.IsNullOrWhiteSpace(stripeSecretKey) || stripeSecretKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
            {
                TempData["Error"] = "Payment processing is not configured yet. Please contact support.";
                ViewBag.PlanName = plan.PlanName;
                ViewBag.Price = plan.MonthlyPrice;
                return View(model);
            }

            var checkoutPriceId = await ResolveCheckoutPriceIdAsync(plan);
            if (string.IsNullOrWhiteSpace(checkoutPriceId))
            {
                TempData["Error"] = "This plan is not yet linked to a payment price. Please contact support.";
                ViewBag.PlanName = plan.PlanName;
                ViewBag.Price = plan.MonthlyPrice;
                return View(model);
            }

            var token = Guid.NewGuid().ToString("N");
            _context.PendingRegistrations.Add(new PendingRegistration
            {
                Token = token,
                PlanId = model.PlanId,
                BusinessName = model.BusinessName,
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailAddress = model.EmailAddress,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                CreatedAt = PhilippineTime.Now,
                ExpiresAt = PhilippineTime.Now.AddHours(2),
                IsUsed = false
            });
            await _context.SaveChangesAsync();

            var domain = $"{Request.Scheme}://{Request.Host}";
            var sessionOptions = new SessionCreateOptions
            {
                Mode = "subscription",
                SuccessUrl = $"{domain}/Account/RegistrationSuccess?token={token}&session_id={{CHECKOUT_SESSION_ID}}",
                CancelUrl = $"{domain}/Account/RegistrationCancelled?token={token}",
                CustomerEmail = model.EmailAddress,
                LineItems = new List<SessionLineItemOptions>
                {
                    new SessionLineItemOptions { Price = checkoutPriceId, Quantity = 1 }
                },
                Metadata = new Dictionary<string, string>
                {
                    ["PendingToken"] = token,
                    ["PlanId"] = plan.PlanId.ToString()
                },
                SubscriptionData = new SessionSubscriptionDataOptions
                {
                    Metadata = new Dictionary<string, string>
                    {
                        ["PendingToken"] = token,
                        ["PlanId"] = plan.PlanId.ToString()
                    }
                }
            };

            try
            {
                var service = new SessionService();
                var session = await service.CreateAsync(sessionOptions);

                if (string.IsNullOrWhiteSpace(session.Url))
                {
                    TempData["Error"] = "Could not create a Stripe checkout session. Please try again.";
                    ViewBag.PlanName = plan.PlanName;
                    ViewBag.Price = plan.MonthlyPrice;
                    return View(model);
                }

                return Redirect(session.Url);
            }
            catch (StripeException ex)
            {
                TempData["Error"] = $"Payment setup failed: {ex.Message}";
                ViewBag.PlanName = plan.PlanName;
                ViewBag.Price = plan.MonthlyPrice;
                return View(model);
            }
        }

        // ──────────────────────────────────────────────────────────────────────
        // REGISTRATION SUCCESS / CANCELLED (Stripe callbacks)
        // ──────────────────────────────────────────────────────────────────────

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrationSuccess(string? token, string? session_id)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                TempData["Error"] = "Invalid registration link.";
                return RedirectToAction("Index", "Home");
            }

            var pending = await _context.PendingRegistrations
                .Include(p => p.Plan)
                .FirstOrDefaultAsync(p => p.Token == token);

            if (pending == null)
            {
                TempData["Error"] = "This registration link is invalid. Please register again.";
                return RedirectToAction("Index", "Home");
            }

            // Webhook may have already created the account – just send them to login
            if (pending.IsUsed)
            {
                TempData["Success"] = "Your account has been set up! Please log in to get started.";
                return RedirectToAction("Login");
            }

            if (pending.ExpiresAt <= PhilippineTime.Now)
            {
                TempData["Error"] = "This registration link has expired. Please register again.";
                return RedirectToAction("Index", "Home");
            }

            // Edge case: email registered between checkout start and success redirect
            if (await _context.Users.AnyAsync(u => u.EmailAddress == pending.EmailAddress))
            {
                pending.IsUsed = true;
                await _context.SaveChangesAsync();
                TempData["Error"] = "This email is already registered. Please log in.";
                return RedirectToAction("Login");
            }

            var newBusiness = await CreateBusinessAndUserAsync(
                pending,
                pending.Plan,
                "Active",
                PhilippineTime.Now.AddMonths(1));

            if (newBusiness == null)
            {
                TempData["Error"] = "There was an issue finalizing your account. Please contact support.";
                return RedirectToAction("Index", "Home");
            }

            pending.IsUsed = true;
            await _context.SaveChangesAsync();

            // Auto sign in
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailAddress == pending.EmailAddress);
            if (user != null)
                await SignInUserAsync(user);

            TempData["Success"] = $"Welcome to ZoneBill, {pending.FirstName}! Your subscription is now active.";
            return RedirectToAction("Index", "Businesses");
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> RegistrationCancelled(string? token)
        {
            if (!string.IsNullOrWhiteSpace(token))
            {
                var pending = await _context.PendingRegistrations
                    .FirstOrDefaultAsync(p => p.Token == token && !p.IsUsed);
                if (pending != null)
                {
                    _context.PendingRegistrations.Remove(pending);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Error"] = "Payment was cancelled. Your account was not created. You can try again below.";
            return RedirectToAction("Index", "Home");
        }

        // ──────────────────────────────────────────────────────────────────────
        // FORGOT / RESET PASSWORD
        // ──────────────────────────────────────────────────────────────────────

        [HttpGet]
        public IActionResult ForgotPassword() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            // Always show the same message to prevent email enumeration
            const string genericMessage = "If that email address is registered, a password reset link has been sent.";

            if (string.IsNullOrWhiteSpace(email))
            {
                TempData["Success"] = genericMessage;
                return RedirectToAction(nameof(ForgotPassword));
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailAddress == email.Trim().ToLower());
            if (user != null)
            {
                var rawToken = Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N");
                user.PasswordResetToken = BCrypt.Net.BCrypt.HashPassword(rawToken);
                user.PasswordResetTokenExpiry = ZoneBill_Lloren.Helpers.PhilippineTime.Now.AddHours(1);
                await _context.SaveChangesAsync();

                var resetLink = Url.Action("ResetPassword", "Account",
                    new { token = rawToken, email = user.EmailAddress },
                    Request.Scheme)!;

                await _emailService.SendPasswordResetEmailAsync(
                    user.EmailAddress,
                    $"{user.FirstName} {user.LastName}",
                    resetLink);
            }

            TempData["Success"] = genericMessage;
            return RedirectToAction(nameof(ForgotPassword));
        }

        [HttpGet]
        public async Task<IActionResult> ResetPassword(string? token, string? email)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(email))
                return RedirectToAction(nameof(Login));

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailAddress == email.Trim().ToLower());
            if (user == null || user.PasswordResetToken == null || user.PasswordResetTokenExpiry == null
                || user.PasswordResetTokenExpiry < ZoneBill_Lloren.Helpers.PhilippineTime.Now
                || !BCrypt.Net.BCrypt.Verify(token, user.PasswordResetToken))
            {
                TempData["Error"] = "This password reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            ViewBag.Token = token;
            ViewBag.Email = email;
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(string token, string email, string newPassword, string confirmPassword)
        {
            if (newPassword != confirmPassword)
            {
                ViewBag.Token = token;
                ViewBag.Email = email;
                ModelState.AddModelError(string.Empty, "Passwords do not match.");
                return View();
            }

            if (newPassword.Length < 8)
            {
                ViewBag.Token = token;
                ViewBag.Email = email;
                ModelState.AddModelError(string.Empty, "Password must be at least 8 characters.");
                return View();
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailAddress == email.Trim().ToLower());
            if (user == null || user.PasswordResetToken == null || user.PasswordResetTokenExpiry == null
                || user.PasswordResetTokenExpiry < ZoneBill_Lloren.Helpers.PhilippineTime.Now
                || !BCrypt.Net.BCrypt.Verify(token, user.PasswordResetToken))
            {
                TempData["Error"] = "This password reset link is invalid or has expired. Please request a new one.";
                return RedirectToAction(nameof(ForgotPassword));
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            user.PasswordResetToken = null;
            user.PasswordResetTokenExpiry = null;
            await _context.SaveChangesAsync();

            TempData["Success"] = "Your password has been reset. You can now log in.";
            return RedirectToAction(nameof(Login));
        }

        [Authorize]
        [HttpGet]
        public IActionResult ChangePassword() => View();

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(string currentPassword, string newPassword, string confirmPassword)
        {
            var userId = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var user = await _context.Users.FindAsync(userId);
            if (user == null) return Forbid();

            if (!BCrypt.Net.BCrypt.Verify(currentPassword, user.PasswordHash))
            {
                ModelState.AddModelError(string.Empty, "Current password is incorrect.");
                return View();
            }

            if (newPassword != confirmPassword)
            {
                ModelState.AddModelError(string.Empty, "New passwords do not match.");
                return View();
            }

            if (newPassword.Length < 8)
            {
                ModelState.AddModelError(string.Empty, "Password must be at least 8 characters.");
                return View();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();

            TempData["Success"] = "Password changed successfully.";
            return RedirectToAction(nameof(ChangePassword));
        }

        // GOOGLE SSO
        // ──────────────────────────────────────────────────────────────────────

        [HttpPost]
        [ValidateAntiForgeryToken]
        public IActionResult GoogleLogin(string? returnUrl = null)
        {
            var redirectUrl = Url.Action("GoogleResponse", "Account", new { ReturnUrl = returnUrl });
            var properties = new AuthenticationProperties { RedirectUri = redirectUrl };
            return Challenge(properties, Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme);
        }

        [HttpGet]
        public async Task<IActionResult> GoogleResponse(string? returnUrl = null)
        {
            var result = await HttpContext.AuthenticateAsync(CookieAuthenticationDefaults.AuthenticationScheme);

            if (result.Succeeded)
            {
                var email = result.Principal.FindFirstValue(ClaimTypes.Email);
                var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailAddress == email);
                if (user != null)
                {
                    await SignInUserAsync(user);
                    return RedirectToRoleDefault(user.UserRole);
                }

                TempData["Error"] = "No ZoneBill account found for that Google email. Please register a new account or log in with your password.";
                return RedirectToAction(nameof(Login));
            }

            return RedirectToAction(nameof(Login));
        }

        // ──────────────────────────────────────────────────────────────────────
        // PRIVATE HELPERS
        // ──────────────────────────────────────────────────────────────────────

        /// <summary>Creates Business + seeds Chart of Accounts + creates User + signs them in. Returns the new Business, or null on domain conflict.</summary>
        private async Task<Business?> CreateBusinessAndUserAsync(
            RegisterBusinessViewModel model,
            SubscriptionPlan plan,
            string subscriptionStatus,
            DateTime currentPeriodEnd)
        {
            var domainPrefix = await GenerateUniqueDomainPrefixAsync(BuildBaseDomainPrefix(model.BusinessName));

            var newBusiness = new Business
            {
                PlanId = plan.PlanId,
                BusinessName = model.BusinessName,
                DomainPrefix = domainPrefix,
                SubscriptionStatus = subscriptionStatus,
                CurrentPeriodEnd = currentPeriodEnd,
                CreatedAt = PhilippineTime.Now,
                IsActive = true
            };

            _context.Businesses.Add(newBusiness);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (IsDomainPrefixUniqueConstraintViolation(ex)) { return null; }

            await SeedChartOfAccountsAsync(newBusiness.BusinessId);

            var newUser = new User
            {
                BusinessId = newBusiness.BusinessId,
                UserRole = "MainAdmin",
                FirstName = model.FirstName,
                LastName = model.LastName,
                EmailAddress = model.EmailAddress,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(model.Password),
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();

            await SignInUserAsync(newUser);
            return newBusiness;
        }

        /// <summary>Overload used by RegistrationSuccess (data comes from PendingRegistration).</summary>
        private async Task<Business?> CreateBusinessAndUserAsync(
            PendingRegistration pending,
            SubscriptionPlan plan,
            string subscriptionStatus,
            DateTime currentPeriodEnd)
        {
            var domainPrefix = await GenerateUniqueDomainPrefixAsync(BuildBaseDomainPrefix(pending.BusinessName));

            var newBusiness = new Business
            {
                PlanId = plan.PlanId,
                BusinessName = pending.BusinessName,
                DomainPrefix = domainPrefix,
                SubscriptionStatus = subscriptionStatus,
                CurrentPeriodEnd = currentPeriodEnd,
                CreatedAt = PhilippineTime.Now,
                IsActive = true
            };

            _context.Businesses.Add(newBusiness);
            try { await _context.SaveChangesAsync(); }
            catch (DbUpdateException ex) when (IsDomainPrefixUniqueConstraintViolation(ex)) { return null; }

            await SeedChartOfAccountsAsync(newBusiness.BusinessId);

            var newUser = new User
            {
                BusinessId = newBusiness.BusinessId,
                UserRole = "MainAdmin",
                FirstName = pending.FirstName,
                LastName = pending.LastName,
                EmailAddress = pending.EmailAddress,
                PasswordHash = pending.PasswordHash,
                IsActive = true
            };

            _context.Users.Add(newUser);
            await _context.SaveChangesAsync();
            return newBusiness;
        }

        private async Task SeedChartOfAccountsAsync(int businessId)
        {
            _context.ChartOfAccounts.AddRange(
                new ChartOfAccount { BusinessId = businessId, AccountName = "Cash", AccountType = "Asset", IsActive = true },
                new ChartOfAccount { BusinessId = businessId, AccountName = "Accounts Receivable", AccountType = "Asset", IsActive = true },
                new ChartOfAccount { BusinessId = businessId, AccountName = "Sales Revenue", AccountType = "Revenue", IsActive = true },
                new ChartOfAccount { BusinessId = businessId, AccountName = "Discount Expense", AccountType = "Expense", IsActive = true }
            );
            await _context.SaveChangesAsync();
        }

        private async Task SignInUserAsync(User user)
        {
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, $"{user.FirstName} {user.LastName}"),
                new Claim(ClaimTypes.Email, user.EmailAddress),
                new Claim(ClaimTypes.Role, user.UserRole)
            };
            if (user.BusinessId.HasValue)
                claims.Add(new Claim("BusinessId", user.BusinessId.Value.ToString()));

            var identity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(identity),
                new AuthenticationProperties { IsPersistent = true, ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) });
        }

        private IActionResult RedirectToRoleDefault(ClaimsPrincipal user)
        {
            var role = user.Claims.FirstOrDefault(c => c.Type == ClaimTypes.Role)?.Value ?? "";
            return RedirectToRoleDefault(role);
        }

        private IActionResult RedirectToRoleDefault(string role) => role switch
        {
            "SuperAdmin" => RedirectToAction("Index", "Businesses"),
            "MainAdmin"  => RedirectToAction("Index", "Spaces"),
            _            => RedirectToAction("Index", "Home")
        };

        private static string BuildBaseDomainPrefix(string? businessName)
        {
            var sanitized = new string((businessName ?? string.Empty).Where(char.IsLetterOrDigit).ToArray()).ToLowerInvariant();
            return !string.IsNullOrWhiteSpace(sanitized) ? sanitized : "b" + Guid.NewGuid().ToString("N")[..5];
        }

        private async Task<string> GenerateUniqueDomainPrefixAsync(string basePrefix)
        {
            var candidate = basePrefix;
            var suffix = 1;
            while (await _context.Businesses.AnyAsync(b => b.DomainPrefix == candidate))
                candidate = $"{basePrefix}{suffix++}";
            return candidate;
        }

        private static bool IsDomainPrefixUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.GetBaseException().Message;
            return message.Contains("IX_Businesses_DomainPrefix", StringComparison.OrdinalIgnoreCase);
        }

        private static async Task<string?> ResolveCheckoutPriceIdAsync(SubscriptionPlan plan)
        {
            if (string.IsNullOrWhiteSpace(plan.StripePriceId)) return null;
            if (plan.StripePriceId.StartsWith("price_", StringComparison.OrdinalIgnoreCase)) return plan.StripePriceId;
            if (!plan.StripePriceId.StartsWith("prod_", StringComparison.OrdinalIgnoreCase)) return null;

            var expectedAmountCents = decimal.ToInt64(plan.MonthlyPrice * 100m);
            var priceService = new PriceService();
            var prices = await priceService.ListAsync(new PriceListOptions
            {
                Product = plan.StripePriceId,
                Active = true,
                Type = "recurring",
                Limit = 100
            });

            var exact = prices.Data.FirstOrDefault(p =>
                p.Recurring?.Interval == "month" &&
                string.Equals(p.Currency, "php", StringComparison.OrdinalIgnoreCase) &&
                p.UnitAmount == expectedAmountCents);

            return exact?.Id ?? prices.Data.FirstOrDefault(p => p.Recurring?.Interval == "month")?.Id;
        }
    }
}