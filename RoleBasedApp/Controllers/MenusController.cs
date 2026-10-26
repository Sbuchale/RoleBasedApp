using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using System.Security.Claims;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class MenusController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public MenusController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        public async Task<IActionResult> GetMyMenus()
        {
            var idValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idValue, out var userId))
                return Unauthorized();

            var menus = await (
                from userRole in _db.UserRoles

                join role in _db.Roles
                    on userRole.RoleId equals role.Id

                join rp in _db.RolePermissions
                    on role.Id equals rp.RoleId

                join menu in _db.Menus
                    on rp.MenuId equals menu.Id

                join permission in _db.Permissions
                    on rp.PermissionId equals permission.Id

                where userRole.UserId == userId
                      && role.IsActive
                      && menu.IsActive
                      && rp.IsAllowed

                group permission.Code by new
                {
                    menu.Id,
                    menu.Name,
                    menu.Route,
                    menu.DisplayOrder
                } into permissionGroup

                orderby permissionGroup.Key.DisplayOrder

                select new
                {
                    permissionGroup.Key.Id,
                    permissionGroup.Key.Name,
                    permissionGroup.Key.Route,
                    Permissions = permissionGroup
                        .Distinct()
                        .ToList()
                }
            ).ToListAsync();

            return Ok(menus);
        }
    }
}
