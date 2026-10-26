using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using RoleBasedApp.Security;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuditLogsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public AuditLogsController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [HasPermission("AUDIT_VIEW")]
        public async Task<IActionResult> GetLogs(
            int page = 1,
            int pageSize = 50)
        {
            page = Math.Max(page, 1);
            pageSize = Math.Clamp(pageSize, 1, 200);

            var query = _db.AuditLogs
                .AsNoTracking()
                .OrderByDescending(x => x.TimestampUtc);

            var total = await query.CountAsync();

            var items = await query
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(x => new
                {
                    x.Id,
                    x.UserId,
                    x.Username,
                    x.Action,
                    x.EntityName,
                    x.EntityId,
                    x.TimestampUtc,
                    x.Success,
                    x.ErrorCode,
                    x.IpAddress,
                    x.CorrelationId
                })
                .ToListAsync();

            return Ok(new
            {
                page,
                pageSize,
                total,
                items
            });
        }
    }
}
