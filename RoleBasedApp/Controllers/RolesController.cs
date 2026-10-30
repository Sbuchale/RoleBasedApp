using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using RoleBasedApp.DTOs;
using RoleBasedApp.Models;
using RoleBasedApp.Security;
using RoleBasedApp.Services;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class RolesController : ControllerBase
    {
        private readonly RoleManager<ApplicationRole> _roleManager;
        private readonly ApplicationDbContext _db;
        private readonly AuditService _audit;

        public RolesController(
            RoleManager<ApplicationRole> roleManager,
            ApplicationDbContext db,
            AuditService audit)
        {
            _roleManager = roleManager;
            _db = db;
            _audit = audit;
        }

        [HttpGet]
        [HasPermission("ROLE_VIEW")]
        public async Task<IActionResult> GetRoles()
        {
            var roles = await _db.Roles
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Description,
                    x.IsActive
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpGet("permissions")]
        [HasPermission("ROLE_VIEW")]
        public async Task<IActionResult> GetPermissions()
        {
            var permissions = await _db.Permissions
                .OrderBy(x => x.Code)
                .Select(x => new
                {
                    x.Id,
                    x.Code,
                    x.Description
                })
                .ToListAsync();

            return Ok(permissions);
        }

        [HttpGet("menus")]
        [HasPermission("ROLE_VIEW")]
        public async Task<IActionResult> GetMenus()
        {
            var menus = await _db.Menus
                .Where(x => x.IsActive)
                .OrderBy(x => x.DisplayOrder)
                .Select(x => new
                {
                    x.Id,
                    x.Name,
                    x.Route
                })
                .ToListAsync();

            return Ok(menus);
        }

        [HttpPost]
        [HasPermission("ROLE_CREATE")]
        public async Task<IActionResult> CreateRole(
            CreateRoleRequest request)
        {
            var name = request.Name.Trim();

            if (await _roleManager.RoleExistsAsync(name))
            {
                return Conflict(new
                {
                    message = "Role already exists."
                });
            }

            var role = new ApplicationRole
            {
                Name = name,
                Description = request.Description.Trim(),
                IsActive = true
            };

            var result = await _roleManager.CreateAsync(role);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(x => x.Description)
                });
            }

            _audit.Add(
                "ROLE_CREATED", "Role", role.Id.ToString(),
                null, new { role.Name, role.Description },
                true);

            await _audit.SaveAsync();

            return Created(
                $"/api/roles/{role.Id}",
                new { role.Id, role.Name, role.Description });
        }

        [HttpGet("active-for-user-request")]
        [HasPermission("USER_VIEW")]
        public async Task<IActionResult> GetActiveRolesForUserRequest()
        {
            var roles = await _db.Roles
                .Where(x => x.IsActive)
                .OrderBy(x => x.Name)
                .Select(x => new
                {
                    x.Id,
                    x.Name
                })
                .ToListAsync();

            return Ok(roles);
        }

        [HttpGet("{id:int}/permissions")]
        [HasPermission("ROLE_VIEW")]
        public async Task<IActionResult> GetRolePermissions(int id)
        {
            var role = await _db.Roles.FindAsync(id);

            if (role == null)
                return NotFound();

            var permissions = await (
                from rp in _db.RolePermissions
                join p in _db.Permissions
                    on rp.PermissionId equals p.Id
                where rp.RoleId == id
                select new
                {
                    menuId = rp.MenuId,
                    permissionCode = p.Code,
                    isAllowed = rp.IsAllowed
                }
            ).ToListAsync();

            return Ok(permissions);
        }

        [HttpPut("{id:int}/permissions")]
        [HasPermission("ROLE_ASSIGN")]
        public async Task<IActionResult> UpdatePermissions(
            int id,
            UpdateRolePermissionsRequest request)
        {
            var role = await _db.Roles.FindAsync(id);

            if (role == null)
                return NotFound();

            if (role.Name == "ADMIN")
            {
                return BadRequest(new
                {
                    message = "Use a controlled process to modify ADMIN permissions."
                });
            }

            var duplicatePermissions = request.Permissions
                .GroupBy(x => new
                {
                    x.MenuId,
                    x.PermissionCode
                })
                .Any(group => group.Count() > 1);

            if (duplicatePermissions)
            {
                return BadRequest(new
                {
                    message = "Duplicate menu and permission combinations were submitted."
                });
            }

            var requestedMenuIds = request.Permissions
                .Select(x => x.MenuId)
                .Distinct()
                .ToList();

            var validMenuCount = await _db.Menus
                .CountAsync(x => requestedMenuIds.Contains(x.Id));

            if (validMenuCount != requestedMenuIds.Count)
                return BadRequest("Invalid menu ID.");

            var requestedCodes = request.Permissions
                .Select(x => x.PermissionCode)
                .Distinct()
                .ToList();

            var validCodes = await _db.Permissions
                .Where(x => requestedCodes.Contains(x.Code))
                .Select(x => x.Code)
                .ToListAsync();

            if (validCodes.Count != requestedCodes.Count)
                return BadRequest("Invalid permission code.");

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var oldPermissions = await _db.RolePermissions
                .Where(x => x.RoleId == id)
                .ToListAsync();

            var oldSnapshot = oldPermissions.Select(x => new
            {
                x.MenuId,
                x.PermissionId,
                x.IsAllowed
            }).ToList();

            _db.RolePermissions.RemoveRange(oldPermissions);

            var permissionLookup = await _db.Permissions
                .ToDictionaryAsync(x => x.Code, x => x.Id);

            foreach (var item in request.Permissions)
            {
                _db.RolePermissions.Add(new RolePermission
                {
                    RoleId = id,
                    MenuId = item.MenuId,
                    PermissionId =
                        permissionLookup[item.PermissionCode],
                    IsAllowed = item.IsAllowed
                });
            }

            _audit.Add(
                "ROLE_PERMISSIONS_CHANGED",
                "Role",
                id.ToString(),
                oldSnapshot,
                request.Permissions,
                true);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Role permissions updated."
            });
        }

        [HttpPut("{id:int}")]
        [HasPermission("ROLE_CREATE")]
        public async Task<IActionResult> UpdateRole(
            int id,
            CreateRoleRequest request)
        {
            var role = await _db.Roles.FindAsync(id);

            if (role == null)
                return NotFound(new
                {
                    message = "Role not found."
                });

            var name = request.Name.Trim();
            var description = request.Description.Trim();

            if (string.IsNullOrWhiteSpace(name) ||
                string.IsNullOrWhiteSpace(description))
            {
                return BadRequest(new
                {
                    message = "Role name and description are required."
                });
            }

            var duplicateRole = await _db.Roles.AnyAsync(x =>
                x.Name == name && x.Id != id);

            if (duplicateRole)
            {
                return Conflict(new
                {
                    message = "Role already exists."
                });
            }

            var oldSnapshot = new
            {
                role.Name,
                role.Description
            };

            role.Name = name;
            role.Description = description;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(x => x.Description)
                });
            }

            _audit.Add(
                "ROLE_UPDATED",
                "Role",
                id.ToString(),
                oldSnapshot,
                new { role.Name, role.Description },
                true);

            await _audit.SaveAsync();

            return Ok(new
            {
                role.Id,
                role.Name,
                role.Description,
                role.IsActive
            });
        }


        [HttpPut("{id:int}/status")]
        [HasPermission("ROLE_CREATE")]
        public async Task<IActionResult> UpdateRoleStatus(
            int id,
            [FromBody] bool isActive)
        {
            var role = await _db.Roles.FindAsync(id);

            if (role == null)
            {
                return NotFound(new
                {
                    message = "Role not found."
                });
            }

            if (role.Name == "ADMIN" && !isActive)
            {
                return BadRequest(new
                {
                    message = "The ADMIN role cannot be deactivated."
                });
            }

            var oldStatus = role.IsActive;

            role.IsActive = isActive;

            var result = await _roleManager.UpdateAsync(role);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(x => x.Description)
                });
            }

            _audit.Add(
                "ROLE_STATUS_CHANGED",
                "Role",
                id.ToString(),
                new { IsActive = oldStatus },
                new { IsActive = role.IsActive },
                true);

            await _audit.SaveAsync();

            return Ok(new
            {
                role.Id,
                role.Name,
                role.IsActive,
                message = "Role status updated successfully."
            });
        }

    }
}
