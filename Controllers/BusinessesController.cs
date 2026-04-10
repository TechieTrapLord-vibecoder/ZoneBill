using Microsoft.AspNetCore.Authorization;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Controllers
{
    [Authorize(Roles = "SuperAdmin,MainAdmin")]
    public class BusinessesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _environment;

        public BusinessesController(ApplicationDbContext context, IWebHostEnvironment environment)
        {
            _context = context;
            _environment = environment;
        }

        // GET: Businesses
        public async Task<IActionResult> Index()
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
            var applicationDbContext = _context.Businesses.Include(b => b.Plan);
            return View(await applicationDbContext.ToListAsync());
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

            return View(business);
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

        // GET: Businesses/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var business = await _context.Businesses
                .Include(b => b.Plan)
                .FirstOrDefaultAsync(m => m.BusinessId == id);
            if (business == null)
            {
                return NotFound();
            }

            return View(business);
        }

        // POST: Businesses/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var business = await _context.Businesses.FindAsync(id);
            if (business != null)
            {
                _context.Businesses.Remove(business);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool BusinessExists(int id)
        {
            return _context.Businesses.Any(e => e.BusinessId == id);
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

            var logosDirectory = Path.Combine(_environment.WebRootPath, "images", "logos");
            Directory.CreateDirectory(logosDirectory);

            var fileName = $"business-logo-{Guid.NewGuid():N}{extension.ToLowerInvariant()}";
            var filePath = Path.Combine(logosDirectory, fileName);

            using var stream = new FileStream(filePath, FileMode.Create);
            await logoFile.CopyToAsync(stream);

            return $"/images/logos/{fileName}";
        }
    }
}

