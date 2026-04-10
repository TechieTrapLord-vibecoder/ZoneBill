using Microsoft.AspNetCore.Authentication.Cookies;
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

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication(); // 1. Authenticate Who They Are First
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
    context.SaveChanges();

    // 2. Ensure the SuperAdmin always exists (logs in via Google SSO only)
    if (!context.Users.Any(u => u.EmailAddress == "j.lloren.546693@umindanao.edu.ph"))
    {
        context.Users.Add(new User
        {
            FirstName = "John Nikolai",
            LastName = "Lloren",
            EmailAddress = "j.lloren.546693@umindanao.edu.ph",
            PasswordHash = "Google_SSO_Login",
            UserRole = "SuperAdmin",
            IsActive = true,
            BusinessId = null
        });
        context.SaveChanges();
    }
}

app.Run();
