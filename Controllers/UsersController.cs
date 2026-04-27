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
    public class UsersController : Controller
    {
        private static readonly string[] SuperAdminAssignableRoles = { "SuperAdmin", "MainAdmin", "Manager", "Cashier" };
        private static readonly string[] MainAdminAssignableRoles = { "Manager", "Cashier" };

        private readonly ApplicationDbContext _context;

        public UsersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Users
        public async Task<IActionResult> Index()
        {
            var query = _context.Users.Include(u => u.Business).AsQueryable();

            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                query = query.Where(u => u.BusinessId == businessId.Value);
            }

            return View(await query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName).ToListAsync());
        }

        // GET: Users/Details/5
        public async Task<IActionResult> Details(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var query = _context.Users
                .Include(u => u.Business)
                .AsQueryable();

            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                query = query.Where(u => u.BusinessId == businessId.Value);
            }

            var user = await query.FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            var activity = await _context.PosAuditLogs
                .Where(a => a.CashierId == user.UserId)
                .OrderByDescending(a => a.CreatedAt)
                .Take(20)
                .ToListAsync();

            return View(new UserDetailsViewModel { User = user, RecentActivity = activity });
        }

        // GET: Users/Create
        public IActionResult Create()
        {
            var isSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.IsSuperAdmin = isSuperAdmin;
            SetRoleOptions(isSuperAdmin, "Cashier");

            if (isSuperAdmin)
            {
                ViewData["BusinessId"] = new SelectList(_context.Businesses, "BusinessId", "BusinessName");
            }

            return View();
        }

        // POST: Users/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create([Bind("UserId,BusinessId,UserRole,FirstName,LastName,EmailAddress,PasswordHash,IsActive")] User user)
        {
            var isSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.IsSuperAdmin = isSuperAdmin;

            if (!isSuperAdmin)
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                user.BusinessId = businessId.Value;
            }

            user.UserRole = NormalizeAssignableRole(user.UserRole, isSuperAdmin);

            if (await _context.Users.AnyAsync(u => u.EmailAddress == user.EmailAddress))
            {
                ModelState.AddModelError("EmailAddress", "Email is already registered.");
            }

            if (ModelState.IsValid)
            {
                user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
                _context.Add(user);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            if (isSuperAdmin)
            {
                ViewData["BusinessId"] = new SelectList(_context.Businesses, "BusinessId", "BusinessName", user.BusinessId);
            }

            SetRoleOptions(isSuperAdmin, user.UserRole);

            return View(user);
        }

        // GET: Users/Edit/5
        public async Task<IActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var query = _context.Users.AsQueryable();

            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                query = query.Where(u => u.BusinessId == businessId.Value);
            }

            var user = await query.FirstOrDefaultAsync(u => u.UserId == id.Value);
            if (user == null)
            {
                return NotFound();
            }

            var isSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.IsSuperAdmin = isSuperAdmin;
            SetRoleOptions(isSuperAdmin, user.UserRole);

            if (isSuperAdmin)
            {
                ViewData["BusinessId"] = new SelectList(_context.Businesses, "BusinessId", "BusinessName", user.BusinessId);
            }

            return View(user);
        }

        // POST: Users/Edit/5
        // To protect from overposting attacks, enable the specific properties you want to bind to.
        // For more details, see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, [Bind("UserId,BusinessId,UserRole,FirstName,LastName,EmailAddress,PasswordHash,IsActive")] User user)
        {
            if (id != user.UserId)
            {
                return NotFound();
            }

            var isSuperAdmin = User.IsInRole("SuperAdmin");
            ViewBag.IsSuperAdmin = isSuperAdmin;

            var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (existingUser == null)
            {
                return NotFound();
            }

            if (!isSuperAdmin)
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                if (existingUser.BusinessId != businessId.Value)
                {
                    return Forbid();
                }

                existingUser.BusinessId = businessId.Value;
                existingUser.UserRole = NormalizeAssignableRole(user.UserRole, isSuperAdmin);
            }
            else
            {
                existingUser.BusinessId = user.BusinessId;
                existingUser.UserRole = NormalizeAssignableRole(user.UserRole, isSuperAdmin);
            }

            existingUser.FirstName = user.FirstName;
            existingUser.LastName = user.LastName;
            existingUser.EmailAddress = user.EmailAddress;
            if (!string.IsNullOrWhiteSpace(user.PasswordHash))
            {
                existingUser.PasswordHash = BCrypt.Net.BCrypt.HashPassword(user.PasswordHash);
            }
            existingUser.IsActive = user.IsActive;

            var duplicateEmail = await _context.Users
                .AnyAsync(u => u.EmailAddress == existingUser.EmailAddress && u.UserId != existingUser.UserId);
            if (duplicateEmail)
            {
                ModelState.AddModelError("EmailAddress", "Email is already registered.");
            }

            if (ModelState.IsValid)
            {
                try
                {
                    _context.Update(existingUser);
                    await _context.SaveChangesAsync();
                }
                catch (DbUpdateConcurrencyException)
                {
                    if (!UserExists(existingUser.UserId))
                    {
                        return NotFound();
                    }
                    else
                    {
                        throw;
                    }
                }
                return RedirectToAction(nameof(Index));
            }

            if (isSuperAdmin)
            {
                ViewData["BusinessId"] = new SelectList(_context.Businesses, "BusinessId", "BusinessName", existingUser.BusinessId);
            }

            SetRoleOptions(isSuperAdmin, existingUser.UserRole);

            return View(existingUser);
        }

        // GET: Users/Delete/5
        public async Task<IActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return NotFound();
            }

            var query = _context.Users
                .Include(u => u.Business)
                .AsQueryable();

            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();

                query = query.Where(u => u.BusinessId == businessId.Value);
            }

            var user = await query.FirstOrDefaultAsync(m => m.UserId == id);
            if (user == null)
            {
                return NotFound();
            }

            return View(user);
        }

        // GET: Users/ResetPassword/5
        public async Task<IActionResult> ResetPassword(int? id)
        {
            if (id == null) return NotFound();
            var query = _context.Users.Include(u => u.Business).AsQueryable();
            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();
                query = query.Where(u => u.BusinessId == businessId.Value);
            }
            var user = await query.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();
            ViewBag.UserName = $"{user.FirstName} {user.LastName}";
            ViewBag.UserId = user.UserId;
            return View();
        }

        // POST: Users/ResetPassword/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(int id, string newPassword)
        {
            var query = _context.Users.AsQueryable();
            if (User.IsInRole("MainAdmin"))
            {
                var businessId = GetBusinessId();
                if (businessId == null) return Forbid();
                query = query.Where(u => u.BusinessId == businessId.Value);
            }
            var user = await query.FirstOrDefaultAsync(u => u.UserId == id);
            if (user == null) return NotFound();

            if (string.IsNullOrWhiteSpace(newPassword) || newPassword.Length < 6)
            {
                ViewBag.UserName = $"{user.FirstName} {user.LastName}";
                ViewBag.UserId = user.UserId;
                ModelState.AddModelError("", "Password must be at least 6 characters.");
                return View();
            }

            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            await _context.SaveChangesAsync();
            TempData["Success"] = $"Password for {user.FirstName} {user.LastName} has been reset.";
            return RedirectToAction(nameof(Index));
        }

        // POST: Users/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var user = await _context.Users.FirstOrDefaultAsync(u => u.UserId == id);
            if (user != null)
            {
                if (User.IsInRole("MainAdmin"))
                {
                    var businessId = GetBusinessId();
                    if (businessId == null) return Forbid();
                    if (user.BusinessId != businessId.Value) return Forbid();
                }

                _context.Users.Remove(user);
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private bool UserExists(int id)
        {
            return _context.Users.Any(e => e.UserId == id);
        }

        private int? GetBusinessId()
        {
            var value = User.FindFirst("BusinessId")?.Value;
            return int.TryParse(value, out var businessId) ? businessId : null;
        }

        private void SetRoleOptions(bool isSuperAdmin, string? selectedRole)
        {
            var roles = isSuperAdmin ? SuperAdminAssignableRoles : MainAdminAssignableRoles;
            var normalizedSelectedRole = NormalizeAssignableRole(selectedRole, isSuperAdmin);
            ViewBag.Roles = new SelectList(roles, normalizedSelectedRole);
        }

        private static string NormalizeAssignableRole(string? requestedRole, bool isSuperAdmin)
        {
            var roles = isSuperAdmin ? SuperAdminAssignableRoles : MainAdminAssignableRoles;
            if (string.IsNullOrWhiteSpace(requestedRole)) return "Cashier";

            var matchedRole = roles.FirstOrDefault(r => r.Equals(requestedRole, StringComparison.OrdinalIgnoreCase));
            return matchedRole ?? "Cashier";
        }
    }
}

