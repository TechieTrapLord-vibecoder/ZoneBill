using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "SuperAdmin,MainAdmin")]
    public class BusinessesController : Controller
    {
        private const string InventorySettingsTab = "inventory";

        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;
        private readonly CloudinaryDotNet.Cloudinary? _cloudinary;
        private readonly IInventoryAlertService _inventoryAlertService;
        private readonly IConfiguration _configuration;

        public BusinessesController(ApplicationDbContext context, IWebHostEnvironment environment, IServiceProvider serviceProvider, IInventoryAlertService inventoryAlertService, IConfiguration configuration)
        {
            _context = context;
            _environment = environment;
            _cloudinary = serviceProvider.GetService<CloudinaryDotNet.Cloudinary>();
            _inventoryAlertService = inventoryAlertService;
            _configuration = configuration;
        }

        // GET: Businesses
        public async Task<IActionResult> Index(int page = 1)
        {
            // If it's a MainAdmin, redirect them directly to THEIR specific business details page
            if (User.IsInRole("MainAdmin"))
            {
                var businessClaim = User.FindFirst("BusinessId");
                if (businessClaim != null && int.TryParse(businessClaim.Value, out int businessId))
                {
                    return RedirectToAction("Details", new { id = businessId });
                }
            }

            // Otherwise, it's a SuperAdmin, so show them all businesses
            const int pageSize = 10;
            var query = _context.Businesses.Include(b => b.Plan);
            var totalCount = await query.CountAsync();
            var activeCount = await query.CountAsync(b => b.IsActive);
            var totalPages = Math.Max(1, (int)Math.Ceiling(totalCount / (double)pageSize));
            page = Math.Clamp(page, 1, totalPages);
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;
            ViewBag.ActiveCount = activeCount;
            return View(await query.OrderBy(b => b.BusinessName)
                .Skip((page - 1) * pageSize).Take(pageSize).ToListAsync());
        }

        // GET: Businesses/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            // Security Check for MainAdmin
            if (User.IsInRole("MainAdmin"))
            {
                var myBusinessId = User.FindFirst("BusinessId")?.Value;
                if (id.ToString() != myBusinessId)
                {
                    return Forbid(); // Non-owners cannot see other business profiles
                }
            }

            var business = await _context.Businesses
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(m => m.BusinessId == id);
            if (business == null)
            {
                return NotFound();
            }

            var lifecycle = await _context.BusinessLifecycleEvents
                .Where(e => e.BusinessId == business.BusinessId)
                .OrderByDescending(e => e.CreatedAt)
                .Take(20)
                .ToListAsync();

            ViewBag.LifecycleEvents = lifecycle;

            return View(business);
        }

        [HttpPost]
        [Authorize(Roles = "SuperAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SetLifecycleState(int id, bool isActive, string? reason, string? returnUrl = null)
        {
            var business = await _context.Businesses.FindAsync(id);
            if (business == null)
            {
                TempData["Error"] = "Business not found.";
                return RedirectToAction(nameof(Index));
            }

            var previousIsActive = business.IsActive;
            var previousStatus = business.SubscriptionStatus;

            if (previousIsActive == isActive)
            {
                TempData["Warning"] = "No lifecycle change was made.";
                if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
                {
                    return Redirect(returnUrl);
                }
                return RedirectToAction(nameof(Details), new { id });
            }

            business.IsActive = isActive;
            if (!isActive)
            {
                business.SubscriptionStatus = "Suspended";
            }
            else if (business.CurrentPeriodEnd.HasValue && business.CurrentPeriodEnd > PhilippineTime.Now)
            {
                business.SubscriptionStatus = "Active";
            }
            else
            {
                business.SubscriptionStatus = "PastDue";
            }

            var actorId = TryGetCurrentUserId();
            var actorName = User.Identity?.Name ?? "SuperAdmin";
            var eventType = isActive ? "Reactivated" : "Suspended";

            _context.BusinessLifecycleEvents.Add(new BusinessLifecycleEvent
            {
                BusinessId = business.BusinessId,
                EventType = eventType,
                PreviousValue = $"IsActive={previousIsActive};Status={previousStatus}",
                NewValue = $"IsActive={business.IsActive};Status={business.SubscriptionStatus}",
                Reason = string.IsNullOrWhiteSpace(reason) ? "Manual lifecycle change" : reason.Trim(),
                ActorUserId = actorId,
                ActorName = actorName,
                CreatedAt = PhilippineTime.Now
            });

            _context.SuperAdminAuditLogs.Add(new SuperAdminAuditLog
            {
                ActionType = eventType,
                EntityType = "Business",
                EntityId = business.BusinessId,
                BusinessId = business.BusinessId,
                BusinessName = business.BusinessName,
                Details = $"Business set to {(isActive ? "Active" : "Inactive")}; subscription status {business.SubscriptionStatus}.",
                Reason = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
                ActorUserId = actorId,
                ActorName = actorName,
                CreatedAt = PhilippineTime.Now
            });

            await _context.SaveChangesAsync();

            TempData["Success"] = isActive
                ? $"{business.BusinessName} reactivated."
                : $"{business.BusinessName} suspended.";

            if (!string.IsNullOrWhiteSpace(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return Redirect(returnUrl);
            }

            return RedirectToAction(nameof(Details), new { id });
        }

        // GET: Businesses/Create
        public IActionResult Create()
        {
            ViewData["PlanId"] = new SelectList(_context.SubscriptionPlans, "PlanId", "PlanName");
            return View();
        }

        // POST: Businesses/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("BusinessId,PlanId,BusinessName,DomainPrefix,LogoUrl,TaxRatePercentage,CreatedAt,IsActive")] Business business, IFormFile? logoFile)
        {
            business.DomainPrefix = (business.DomainPrefix ?? string.Empty).Trim().ToLowerInvariant();

            if (await _context.Businesses.AnyAsync(b => b.DomainPrefix == business.DomainPrefix))
            {
                ModelState.AddModelError("DomainPrefix", "This domain prefix is already in use.");
            }

            if (logoFile != null && logoFile.Length > 0)
            {
                var savedLogoPath = await SaveLogoAsync(logoFile);
                if (savedLogoPath == null)
                {
                    ModelState.AddModelError("logoFile", "Only .png, .jpg, .jpeg, and .webp files are allowed for logos.");
                }
                else
                {
                    business.LogoUrl = savedLogoPath;
                }
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Add(business);
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateException ex) when (IsDomainPrefixUniqueConstraintViolation(ex))
                {
                    ModelState.AddModelError("DomainPrefix", "This domain prefix is already in use.");
                }
            }
            ViewData["PlanId"] = new SelectList(_context.SubscriptionPlans, "PlanId", "PlanName", business.PlanId);
            return View(business);
        }

        // GET: Businesses/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var business = await _context.Businesses.FindAsync(id);
            if (business == null)
            {
                return NotFound();
            }
            ViewData["PlanId"] = new SelectList(_context.SubscriptionPlans, "PlanId", "PlanName", business.PlanId);
            return View(business);
        }

        // POST: Businesses/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("BusinessId,PlanId,BusinessName,DomainPrefix,LogoUrl,TaxRatePercentage,CreatedAt,IsActive")] Business business, IFormFile? logoFile)
        {
            if (id != business.BusinessId)
            {
                return NotFound();
            }

            business.DomainPrefix = (business.DomainPrefix ?? string.Empty).Trim().ToLowerInvariant();

            if (await _context.Businesses.AnyAsync(b => b.BusinessId != id && b.DomainPrefix == business.DomainPrefix))
            {
                ModelState.AddModelError("DomainPrefix", "This domain prefix is already in use.");
            }

            var existingBusiness = await _context.Businesses.FirstOrDefaultAsync(b => b.BusinessId == id);
            if (existingBusiness == null)
            {
                return NotFound();
            }

            if (logoFile != null && logoFile.Length > 0)
            {
                var savedLogoPath = await SaveLogoAsync(logoFile);
                if (savedLogoPath == null)
                {
                    ModelState.AddModelError("logoFile", "Only .png, .jpg, .jpeg, and .webp files are allowed for logos.");
                }
                else
                {
                    existingBusiness.LogoUrl = savedLogoPath;
                }
            }
            else
            {
                existingBusiness.LogoUrl = business.LogoUrl;
            }

            if (ModelState.IsValid)
            {
                try
                {
                    existingBusiness.PlanId = business.PlanId;
                    existingBusiness.BusinessName = business.BusinessName;
                    existingBusiness.DomainPrefix = business.DomainPrefix;
                    existingBusiness.TaxRatePercentage = business.TaxRatePercentage;
                    existingBusiness.CreatedAt = business.CreatedAt;
                    existingBusiness.IsActive = business.IsActive;

                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!BusinessExists(business.BusinessId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                catch (DbUpdateException ex) when (IsDomainPrefixUniqueConstraintViolation(ex))
                {
                    ModelState.AddModelError("DomainPrefix", "This domain prefix is already in use.");
                }
            }
            ViewData["PlanId"] = new SelectList(_context.SubscriptionPlans, "PlanId", "PlanName", business.PlanId);
            return View(business);
        }

        // ── SETTINGS ─────────────────────────────────────────────────────────

        // GET: Businesses/Settings
        [Authorize(Roles = "MainAdmin")]
        public async Task<IActionResult> Settings(string? tab = null)
        {
            var businessId = GetMainAdminBusinessId();
            if (businessId == null) return Forbid();

            var business = await _context.Businesses.FindAsync(businessId.Value);
            if (business == null) return NotFound();

            ViewData["ActiveSettingsTab"] = NormalizeSettingsTab(tab);
            return View(business);
        }

        // POST: Businesses/Settings
        [HttpPost]
        [Authorize(Roles = "MainAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settings(BusinessSettingsInputModel input)
        {
            var myBusinessId = GetMainAdminBusinessId();
            if (myBusinessId == null || myBusinessId.Value != input.BusinessId) return Forbid();

            var business = await _context.Businesses.FindAsync(input.BusinessId);
            if (business == null) return NotFound();

            AddSettingsValidationErrors(input);

            if (input.LogoFile != null && input.LogoFile.Length > 0)
            {
                var savedLogoPath = await SaveLogoAsync(input.LogoFile);
                if (savedLogoPath == null)
                    ModelState.AddModelError(nameof(input.LogoFile), "Only .png, .jpg, .jpeg, and .webp files are allowed.");
                else
                    business.LogoUrl = savedLogoPath;
            }

            if (!ModelState.IsValid)
            {
                ViewData["ActiveSettingsTab"] = InventorySettingsTab;
                ApplySettingsToBusiness(business, input);
                return View(business);
            }

            ApplySettingsToBusiness(business, input);

            if (input.InitialCapital > 0)
            {
                await SyncInitialCapitalEntriesAsync(input.BusinessId, input.InitialCapital);
            }

            await _context.SaveChangesAsync();
            TempData["Success"] = "Business settings saved successfully.";
            return RedirectToAction(nameof(Settings), new { tab = InventorySettingsTab });
        }

        [HttpPost]
        [Authorize(Roles = "MainAdmin")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestReorderEmail(BusinessSettingsInputModel input)
        {
            var myBusinessId = GetMainAdminBusinessId();
            if (myBusinessId == null || myBusinessId.Value != input.BusinessId) return Forbid();

            var business = await _context.Businesses.FindAsync(input.BusinessId);
            if (business == null) return NotFound();

            AddSettingsValidationErrors(input);
            ApplySettingsToBusiness(business, input);

            if (!ModelState.IsValid)
            {
                ViewData["ActiveSettingsTab"] = InventorySettingsTab;
                return View(nameof(Settings), business);
            }

            if (!IsSendGridConfigured())
            {
                TempData["Error"] = "SendGrid is not configured, so ZoneBill cannot send a test reorder email yet.";
                return RedirectToAction(nameof(Settings), new { tab = InventorySettingsTab });
            }

            var admin = await _context.Users.FirstOrDefaultAsync(
                u => u.BusinessId == input.BusinessId && u.UserRole == "MainAdmin" && u.IsActive);

            var recipientEmail = !string.IsNullOrWhiteSpace(business.InventoryAlertEmail)
                ? business.InventoryAlertEmail.Trim()
                : admin?.EmailAddress;

            if (string.IsNullOrWhiteSpace(recipientEmail))
            {
                TempData["Error"] = "No inventory alert email is available. Add an override email or make sure the MainAdmin account has a valid email address.";
                return RedirectToAction(nameof(Settings), new { tab = InventorySettingsTab });
            }

            var result = await _inventoryAlertService.SendReorderAlertAsync(new InventoryAlertDispatchRequest
            {
                BusinessId = input.BusinessId,
                BusinessName = business.BusinessName,
                RecipientEmail = recipientEmail,
                RecipientName = admin != null ? $"{admin.FirstName} {admin.LastName}" : business.BusinessName,
                LookbackDays = business.InventoryReorderLookbackDays,
                LeadTimeDays = business.InventoryLeadTimeDays,
                SafetyStockDays = business.InventorySafetyStockDays,
                TargetCoverageDays = business.InventoryTargetCoverageDays,
                TriggerSource = InventoryAlertSources.ManualTest,
                ForceSend = true
            });

            if (!result.HasRecommendations)
            {
                TempData["Warning"] = "No test reorder email was sent because the current settings produced no reorder recommendations.";
                return RedirectToAction(nameof(Settings), new { tab = InventorySettingsTab });
            }

            TempData["Success"] = $"Test reorder email sent to {recipientEmail}.";
            return RedirectToAction(nameof(Settings), new { tab = InventorySettingsTab });
        }

        private int? GetMainAdminBusinessId()
        {
            var raw = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(raw, out var id) ? id : null;
        }

        // GET: Businesses/Delete — Deletion disabled; use SetLifecycleState to archive
        public IActionResult Delete(int? id)
        {
            TempData["Warning"] = "Deletion is disabled. Use Suspend to archive a business.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Businesses/Delete — Deletion disabled
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public IActionResult DeleteConfirmed(int id)
        {
            TempData["Warning"] = "Deletion is disabled. Use Suspend to archive a business.";
            return RedirectToAction(nameof(Index));
        }

        private bool BusinessExists(int id)
        {
            return _context.Businesses.Any(e => e.BusinessId == id);
        }

        private async Task SyncInitialCapitalEntriesAsync(int businessId, decimal initialCapital)
        {
            var cashAccount = await EnsureAccountAsync(businessId, "Cash on Hand", "Asset");
            var equityAccount = await EnsureAccountAsync(businessId, "Owner's Equity", "Equity");

            var journalEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(e => e.BusinessId == businessId && e.ReferenceType == "InitialCapital");

            if (journalEntry == null)
            {
                journalEntry = new JournalEntry
                {
                    BusinessId = businessId,
                    ReferenceType = "InitialCapital",
                    EntryDate = PhilippineTime.Now,
                    Description = "Initial Capital Investment"
                };
                _context.JournalEntries.Add(journalEntry);
                await _context.SaveChangesAsync();
            }

            var existingLines = await _context.JournalEntryLines
                .Where(l => l.JournalEntryId == journalEntry.JournalEntryId)
                .ToListAsync();
            if (existingLines.Any())
            {
                _context.JournalEntryLines.RemoveRange(existingLines);
            }

            _context.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntryId = journalEntry.JournalEntryId,
                AccountId = cashAccount.AccountId,
                Debit = initialCapital,
                Credit = 0
            });
            _context.JournalEntryLines.Add(new JournalEntryLine
            {
                JournalEntryId = journalEntry.JournalEntryId,
                AccountId = equityAccount.AccountId,
                Debit = 0,
                Credit = initialCapital
            });
        }

        private async Task<ChartOfAccount> EnsureAccountAsync(int businessId, string accountName, string accountType)
        {
            var account = await _context.ChartOfAccounts
                .FirstOrDefaultAsync(a => a.BusinessId == businessId && a.AccountName == accountName && a.AccountType == accountType);
            if (account != null)
            {
                return account;
            }

            account = new ChartOfAccount
            {
                BusinessId = businessId,
                AccountName = accountName,
                AccountType = accountType,
                IsActive = true
            };
            _context.ChartOfAccounts.Add(account);
            await _context.SaveChangesAsync();
            return account;
        }

        private void AddSettingsValidationErrors(BusinessSettingsInputModel input)
        {
            if (string.IsNullOrWhiteSpace(input.BusinessName))
                ModelState.AddModelError("BusinessName", "Business name is required.");
            if (input.TaxRatePercentage < 0 || input.TaxRatePercentage > 100)
                ModelState.AddModelError("TaxRatePercentage", "Tax rate must be between 0 and 100.");
            if (input.InitialCapital < 0)
                ModelState.AddModelError("InitialCapital", "Initial capital cannot be negative.");
            if (!string.IsNullOrWhiteSpace(input.InventoryAlertEmail) && !new System.ComponentModel.DataAnnotations.EmailAddressAttribute().IsValid(input.InventoryAlertEmail))
                ModelState.AddModelError("InventoryAlertEmail", "Inventory alert email must be a valid email address.");
            if (input.InventoryReorderLookbackDays < 7 || input.InventoryReorderLookbackDays > 90)
                ModelState.AddModelError("InventoryReorderLookbackDays", "Lookback days must be between 7 and 90.");
            if (input.InventoryLeadTimeDays < 1 || input.InventoryLeadTimeDays > 30)
                ModelState.AddModelError("InventoryLeadTimeDays", "Lead time must be between 1 and 30 days.");
            if (input.InventorySafetyStockDays < 0 || input.InventorySafetyStockDays > 30)
                ModelState.AddModelError("InventorySafetyStockDays", "Safety stock days must be between 0 and 30.");
            if (input.InventoryTargetCoverageDays < 1 || input.InventoryTargetCoverageDays > 60)
                ModelState.AddModelError("InventoryTargetCoverageDays", "Target coverage must be between 1 and 60 days.");
            if (input.InventoryForecastLookbackDays < 14 || input.InventoryForecastLookbackDays > 90)
                ModelState.AddModelError("InventoryForecastLookbackDays", "Forecast lookback must be between 14 and 90 days.");
            if (input.InventoryForecastHorizonDays < 7 || input.InventoryForecastHorizonDays > 30)
                ModelState.AddModelError("InventoryForecastHorizonDays", "Forecast horizon must be between 7 and 30 days.");
        }

        private static void ApplySettingsToBusiness(Business business, BusinessSettingsInputModel input)
        {
            business.BusinessName = input.BusinessName.Trim();
            business.TaxRatePercentage = input.TaxRatePercentage;
            business.InitialCapital = input.InitialCapital;
            business.ThemePreference = string.IsNullOrWhiteSpace(input.ThemePreference) ? "Nightlife" : input.ThemePreference;
            business.InventoryAlertEnabled = input.InventoryAlertEnabled;
            business.InventoryAlertEmail = string.IsNullOrWhiteSpace(input.InventoryAlertEmail) ? null : input.InventoryAlertEmail.Trim();
            business.InventoryReorderLookbackDays = ClampInt(input.InventoryReorderLookbackDays, 7, 90, 30);
            business.InventoryLeadTimeDays = ClampInt(input.InventoryLeadTimeDays, 1, 30, 3);
            business.InventorySafetyStockDays = ClampInt(input.InventorySafetyStockDays, 0, 30, 2);
            business.InventoryTargetCoverageDays = ClampInt(input.InventoryTargetCoverageDays, 1, 60, 7);
            business.InventoryForecastLookbackDays = ClampInt(input.InventoryForecastLookbackDays, 14, 90, 28);
            business.InventoryForecastHorizonDays = ClampInt(input.InventoryForecastHorizonDays, 7, 30, 7);
        }

        private bool IsSendGridConfigured()
        {
            var apiKey = _configuration["SendGrid:ApiKey"];
            return !string.IsNullOrWhiteSpace(apiKey)
                && !apiKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase)
                && !apiKey.Contains("YOUR_SENDGRID_API_KEY", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeSettingsTab(string? tab)
        {
            return tab switch
            {
                "appearance" => "appearance",
                "financial" => "financial",
                InventorySettingsTab => InventorySettingsTab,
                _ => "profile"
            };
        }

        private static int ClampInt(int value, int min, int max, int fallback)
        {
            if (value <= 0)
            {
                return fallback;
            }

            return Math.Min(Math.Max(value, min), max);
        }

        private static bool IsDomainPrefixUniqueConstraintViolation(DbUpdateException ex)
        {
            var message = ex.InnerException?.Message ?? ex.GetBaseException().Message;
            return message.Contains("IX_Businesses_DomainPrefix", StringComparison.OrdinalIgnoreCase);
        }

        private async Task<string?> SaveLogoAsync(IFormFile logoFile)
        {
            var extension = Path.GetExtension(logoFile.FileName);
            var allowedExtensions = new[] { ".png", ".jpg", ".jpeg", ".webp" };
            if (string.IsNullOrWhiteSpace(extension) || !allowedExtensions.Contains(extension, StringComparer.OrdinalIgnoreCase))
            {
                return null;
            }

            if (_cloudinary != null)
            {
                using var stream = logoFile.OpenReadStream();
                var uploadParams = new CloudinaryDotNet.Actions.ImageUploadParams()
                {
                    File = new CloudinaryDotNet.FileDescription(logoFile.FileName, stream),
                    Folder = "zonebill/logos",
                    Transformation = new CloudinaryDotNet.Transformation().Width(500).Height(500).Crop("limit")
                };
                var uploadResult = await _cloudinary.UploadAsync(uploadParams);
                if (uploadResult.Error != null)
                {
                    // Fallback to local if Cloudinary fails, or throw exception. 
                    // We'll log it and let it fallback to local for now.
                    Console.WriteLine($"Cloudinary Upload Error: {uploadResult.Error.Message}");
                }
                else
                {
                    return uploadResult.SecureUrl.ToString();
                }
            }

            var logosDirectory = Path.Combine(_environment.WebRootPath, "images", "logos");
            Directory.CreateDirectory(logosDirectory);

            var fileName = $"business-logo-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var filePath = Path.Combine(logosDirectory, fileName);

            using var fileStream = new FileStream(filePath, FileMode.Create);
            await logoFile.CopyToAsync(fileStream);

            return $"/images/logos/{fileName}";
        }

        private int? TryGetCurrentUserId()
        {
            var raw = User.FindFirstValue(ClaimTypes.NameIdentifier);
            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}

