using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using System.Security.Claims;

namespace RoleBasedApp.Security
{
    public class PermissionAuthorizationHandler:AuthorizationHandler<PermissionRequirement>
    {
        private readonly ApplicationDbContext _db;

        public PermissionAuthorizationHandler(
            ApplicationDbContext db)
        {
            _db = db;
        }
        protected override async Task HandleRequirementAsync(
            AuthorizationHandlerContext context,
            PermissionRequirement requirement)
        {
            var idValue = context.User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idValue, out var userId))
                return;

            var isActive = await _db.Users
                .AnyAsync(x => x.Id == userId && x.IsActive);

            if (!isActive)
                return;

            var hasPermission =
                await (
                    from userRole in _db.UserRoles

                    join role in _db.Roles
                        on userRole.RoleId equals role.Id

                    join rp in _db.RolePermissions
                        on role.Id equals rp.RoleId

                    join permission in _db.Permissions
                        on rp.PermissionId equals permission.Id

                    join menu in _db.Menus
                        on rp.MenuId equals menu.Id

                    where userRole.UserId == userId
                          && role.IsActive
                          && rp.IsAllowed
                          && (
    permission.Code == requirement.Permission
    || (
        requirement.Permission == "USER_APPROVE_OR_REJECT"
        && (
            permission.Code == "USER_APPROVE"
            || permission.Code == "USER_REJECT"
        )
    )
)
                          && menu.IsActive

                    select permission.Id
                ).AnyAsync();

            if (hasPermission)
            {
                context.Succeed(requirement);
            }
        }
    }
}
