using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using Stripe;
using System.Globalization;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Filters;
using ZoneBill_Lloren.Helpers;
using ZoneBill_Lloren.Models;

var builder = WebApplication.CreateBuilder(args);

// Set default culture to Philippine Peso (₱)
var phCulture = new CultureInfo("en-PH");
CultureInfo.DefaultThreadCurrentCulture = phCulture;
CultureInfo.DefaultThreadCurrentUICulture = phCulture;

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection")));
builder.Services.AddScoped<ActiveSubscriptionFilter>();
builder.Services.AddScoped<ZoneBill_Lloren.Helpers.IEmailService, ZoneBill_Lloren.Helpers.EmailService>();
builder.Services.AddScoped<ZoneBill_Lloren.Helpers.INotificationService, ZoneBill_Lloren.Helpers.NotificationService>();
builder.Services.AddScoped<ZoneBill_Lloren.Helpers.ITenantAuditLogger, ZoneBill_Lloren.Helpers.TenantAuditLogger>();
builder.Services.AddScoped<IInventoryIntelligenceService, InventoryIntelligenceService>();
builder.Services.AddScoped<IDemandForecastService, DemandForecastService>();
builder.Services.AddScoped<IInventoryAnomalyService, InventoryAnomalyService>();
builder.Services.AddScoped<IInventoryAlertService, InventoryAlertService>();
builder.Services.AddScoped<INotificationService, NotificationService>();
builder.Services.AddHostedService<ZoneBill_Lloren.Helpers.AutomationWorker>();

builder.Services.AddRateLimiter(options =>
{
    options.AddFixedWindowLimiter("Login", opt =>
    {
        opt.PermitLimit = 20;
        opt.Window = TimeSpan.FromMinutes(1);
        opt.QueueProcessingOrder = System.Threading.RateLimiting.QueueProcessingOrder.OldestFirst;
        opt.QueueLimit = 0;
    });
    options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
    options.OnRejected = async (context, token) =>
    {
        context.HttpContext.Response.StatusCode = 429;
        context.HttpContext.Response.Redirect("/Home/StatusCode?code=429");
        await Task.CompletedTask;
    };
});

// Configure Cookie Authentication for Roles (SuperAdmin, MainAdmin, Staff)
builder.Services.AddAuthentication(options =>
    {
        options.DefaultScheme = CookieAuthenticationDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = Microsoft.AspNetCore.Authentication.Google.GoogleDefaults.AuthenticationScheme;
    })
    .AddCookie(options =>
    {
        options.LoginPath = "/Account/Login"; // Redirects unauthorized users here
        options.ExpireTimeSpan = TimeSpan.FromDays(7);
    })
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"] ?? "PLACEHOLDER_CLIENT_ID";
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"] ?? "PLACEHOLDER_CLIENT_SECRET";
    });

var stripeSecretKey = builder.Configuration["Stripe:SecretKey"];
if (!string.IsNullOrWhiteSpace(stripeSecretKey) && !stripeSecretKey.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
{
    StripeConfiguration.ApiKey = stripeSecretKey;
}

// Configure Cloudinary
builder.Services.Configure<CloudinarySettings>(builder.Configuration.GetSection("CloudinarySettings"));
var cloudinarySettings = builder.Configuration.GetSection("CloudinarySettings").Get<CloudinarySettings>();
if (cloudinarySettings != null && !string.IsNullOrWhiteSpace(cloudinarySettings.CloudName) && cloudinarySettings.CloudName != "YOUR_CLOUD_NAME")
{
    var account = new CloudinaryDotNet.Account(
        cloudinarySettings.CloudName,
        cloudinarySettings.ApiKey,
        cloudinarySettings.ApiSecret
    );
    var cloudinary = new CloudinaryDotNet.Cloudinary(account);
    builder.Services.AddSingleton(cloudinary);
}

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}
else
{
    app.UseDeveloperExceptionPage();
}

// Custom status code pages (404, 429, 500) for all environments
app.UseStatusCodePagesWithReExecute("/Home/StatusCode", "?code={0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseRateLimiter();

app.UseAuthentication(); // 1. Authenticate Who They Are First

app.Use(async (context, next) =>
{
    context.Items["ThemePreference"] = "Nightlife";
    context.Items["BrandName"] = "ZoneBill";
    context.Items["BrandLogoUrl"] = "/images/my-logo.png";

    if (context.User.Identity?.IsAuthenticated == true)
    {
        var businessIdClaim = context.User.FindFirst("BusinessId")?.Value;
        if (int.TryParse(businessIdClaim, out var businessId))
        {
            var dbContext = context.RequestServices.GetRequiredService<ApplicationDbContext>();
            var businessBranding = await dbContext.Businesses
                .AsNoTracking()
                .Where(b => b.BusinessId == businessId)
                .Select(b => new
                {
                    b.ThemePreference,
                    b.BusinessName,
                    b.LogoUrl
                })
                .FirstOrDefaultAsync();

            if (businessBranding != null)
            {
                if (!string.IsNullOrWhiteSpace(businessBranding.ThemePreference))
                {
                    context.Items["ThemePreference"] = businessBranding.ThemePreference;
                }

                if (!string.IsNullOrWhiteSpace(businessBranding.BusinessName))
                {
                    context.Items["BrandName"] = businessBranding.BusinessName;
                }

                if (!string.IsNullOrWhiteSpace(businessBranding.LogoUrl))
                {
                    context.Items["BrandLogoUrl"] = businessBranding.LogoUrl;
                }
            }
        }
    }

    await next();
});

app.UseAuthorization();  // 2. Authorize What They Can See

app.MapControllers();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// --- AUTOMATICALLY CREATE SEED DATA (ADMIN & PLANS) ---
using (var scope = app.Services.CreateScope())
{
    var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();

    // 1. Seed the default SaaS Subscription Plans if they don't exist
    var defaultPlans = new[]
    {
        new SubscriptionPlan { PlanName = "Basic Lounge", MonthlyPrice = 0.00m, StripePriceId = "price_PLACEHOLDER_BASIC", MaxTablesAllowed = 1, IsActive = true },
        new SubscriptionPlan { PlanName = "Standard Hub", MonthlyPrice = 999.00m, StripePriceId = "prod_UIMLwpP25RboT2", MaxTablesAllowed = 15, IsActive = true },
        new SubscriptionPlan { PlanName = "Enterprise Venue", MonthlyPrice = 1999.00m, StripePriceId = "prod_UIMM6STipQMpp0", MaxTablesAllowed = 50, IsActive = true }
    };

    foreach (var plan in defaultPlans)
    {
        var existingPlan = context.SubscriptionPlans.FirstOrDefault(p => p.PlanName == plan.PlanName);
        if (existingPlan == null)
        {
            context.SubscriptionPlans.Add(plan);
            continue;
        }

        existingPlan.MonthlyPrice = plan.MonthlyPrice;
        existingPlan.MaxTablesAllowed = plan.MaxTablesAllowed;
        existingPlan.IsActive = true;

        if (string.IsNullOrWhiteSpace(existingPlan.StripePriceId) ||
            existingPlan.StripePriceId.Contains("PLACEHOLDER", StringComparison.OrdinalIgnoreCase))
        {
            existingPlan.StripePriceId = plan.StripePriceId;
        }
    }

    // 2. FORCE RESTORE the absolute SuperAdmin Account
    var adminEmail = "j.lloren.546693@umindanao.edu.ph";
    var existingAdmin = context.Users.FirstOrDefault(u => u.EmailAddress == adminEmail);
    var hashedPassword = BCrypt.Net.BCrypt.HashPassword("Lloren@12345");
    if (existingAdmin == null)
    {
        context.Users.Add(new User { FirstName = "John Nikolai", LastName = "Lloren", EmailAddress = adminEmail, PasswordHash = hashedPassword, UserRole = "SuperAdmin", IsActive = true, BusinessId = null });
    }
    else
    {
        existingAdmin.PasswordHash = hashedPassword;
        existingAdmin.UserRole = "SuperAdmin"; // Force update to SuperAdmin
        existingAdmin.BusinessId = null; // Detach from any business
    }
    context.SaveChanges();

    // Find the business owned by the user they already created
    var realOwner = context.Users.FirstOrDefault(u => u.EmailAddress == "hmmthatsjohn@gmail.com");
    if (realOwner != null && realOwner.BusinessId.HasValue)
    {
        var realBusinessId = realOwner.BusinessId.Value;

        // Seed Spaces (Billiard Tables) to their existing business
        if (!context.Spaces.Any(s => s.BusinessId == realBusinessId))
        {
            context.Spaces.AddRange(
                new Space { BusinessId = realBusinessId, SpaceName = "Table 1", FloorArea = "Main Floor", Capacity = 4, CurrentHourlyRate = 120m, CurrentStatus = "Open" },
                new Space { BusinessId = realBusinessId, SpaceName = "Table 2", FloorArea = "Main Floor", Capacity = 4, CurrentHourlyRate = 120m, CurrentStatus = "Open" },
                new Space { BusinessId = realBusinessId, SpaceName = "Table 3", FloorArea = "Main Floor", Capacity = 4, CurrentHourlyRate = 120m, CurrentStatus = "Open" },
                new Space { BusinessId = realBusinessId, SpaceName = "VIP Room 1", FloorArea = "2nd Floor", Capacity = 10, CurrentHourlyRate = 250m, CurrentStatus = "Open" }
            );
        }

        // Seed Menu Items to their existing business
        if (!context.MenuItems.Any(m => m.BusinessId == realBusinessId))
        {
            context.MenuItems.AddRange(
                new MenuItem { BusinessId = realBusinessId, ItemName = "San Miguel Light", CurrentPrice = 75m, CostPrice = 40m, StockAvailable = 100 },
                new MenuItem { BusinessId = realBusinessId, ItemName = "Red Horse Beer", CurrentPrice = 85m, CostPrice = 45m, StockAvailable = 100 },
                new MenuItem { BusinessId = realBusinessId, ItemName = "French Fries", CurrentPrice = 120m, CostPrice = 50m, StockAvailable = 50 },
                new MenuItem { BusinessId = realBusinessId, ItemName = "Coke in Can", CurrentPrice = 60m, CostPrice = 25m, StockAvailable = 200 },
                new MenuItem { BusinessId = realBusinessId, ItemName = "Nachos Platter", CurrentPrice = 250m, CostPrice = 100m, StockAvailable = 30 }
            );
        }

        context.SaveChanges();
    }
}

app.Run();
