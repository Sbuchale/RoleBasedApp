using RoleBasedApp.Data;
using RoleBasedApp.Models;
using System.Security.Claims;
using System.Text.Json;

namespace RoleBasedApp.Services
{
    public class AuditService
    {
        private readonly ApplicationDbContext _db;
        private readonly IHttpContextAccessor _http;

        public AuditService(
            ApplicationDbContext db,
            IHttpContextAccessor http)
        {
            _db = db;
            _http = http;
        }

        public void Add(
            string action,
            string entityName,
            string? entityId,
            object? oldValues,
            object? newValues,
            bool success,
            string? errorCode = null)
        {
            var context = _http.HttpContext;

            var idValue = context?.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            int? userId = int.TryParse(
                idValue, out var parsedId)
                ? parsedId
                : null;

            var entry = new AuditLog
            {
                UserId = userId,

                Username = context?.User.Identity?.Name
                    ?? "Anonymous",

                Action = action,

                EntityName = entityName,

                EntityId = entityId,

                OldValuesJson = oldValues == null
                    ? null
                    : JsonSerializer.Serialize(oldValues),

                NewValuesJson = newValues == null
                    ? null
                    : JsonSerializer.Serialize(newValues),

                IpAddress = context?
                    .Connection.RemoteIpAddress?.ToString(),

                CorrelationId = context?.TraceIdentifier,

                Success = success,

                ErrorCode = errorCode,

                TimestampUtc = DateTime.UtcNow
            };

            _db.AuditLogs.Add(entry);
        }

        public Task SaveAsync()
        {
            return _db.SaveChangesAsync();
        }
    }
}
