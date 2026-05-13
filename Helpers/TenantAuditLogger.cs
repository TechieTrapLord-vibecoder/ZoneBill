using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using ZoneBill_Lloren.Data;
using ZoneBill_Lloren.Models;

namespace ZoneBill_Lloren.Helpers
{
    public interface ITenantAuditLogger
    {
        Task LogAsync(int businessId, ClaimsPrincipal user, string actionType, string entityType, string? entityId, string? details);
        Task LogSystemAsync(int businessId, string actionType, string entityType, string? entityId, string? details);
        Task LogDirectAsync(int businessId, int userId, string userName, string userRole, string actionType, string entityType, string? entityId, string? details);
    }

    public class TenantAuditLogger : ITenantAuditLogger
    {
        private readonly ApplicationDbContext _context;

        public TenantAuditLogger(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task LogAsync(int businessId, ClaimsPrincipal user, string actionType, string entityType, string? entityId, string? details)
        {
            var userIdStr = user.FindFirstValue(ClaimTypes.NameIdentifier);
            var userId = int.TryParse(userIdStr, out var id) ? (int?)id : null;
            
            var userName = user.FindFirstValue("FullName") ?? user.FindFirstValue(ClaimTypes.Name) ?? "Unknown User";
            var role = user.FindFirstValue(ClaimTypes.Role);

            var log = new TenantAuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                UserName = userName,
                UserRole = role,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = PhilippineTime.Now
            };

            _context.TenantAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogSystemAsync(int businessId, string actionType, string entityType, string? entityId, string? details)
        {
            var log = new TenantAuditLog
            {
                BusinessId = businessId,
                UserId = null,
                UserName = "System",
                UserRole = "System",
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = PhilippineTime.Now
            };

            _context.TenantAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }

        public async Task LogDirectAsync(int businessId, int userId, string userName, string userRole, string actionType, string entityType, string? entityId, string? details)
        {
            var log = new TenantAuditLog
            {
                BusinessId = businessId,
                UserId = userId,
                UserName = userName,
                UserRole = userRole,
                ActionType = actionType,
                EntityType = entityType,
                EntityId = entityId,
                Details = details,
                CreatedAt = PhilippineTime.Now
            };

            _context.TenantAuditLogs.Add(log);
            await _context.SaveChangesAsync();
        }
    }
}
