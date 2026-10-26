using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Models;

namespace RoleBasedApp.Data
{
    public class DatabaseInitializer
    {
        public static async Task InitializeAsync(
       IServiceProvider services,
       IConfiguration configuration)
        {
            using var scope = services.CreateScope();

            var db = scope.ServiceProvider
                .GetRequiredService<ApplicationDbContext>();

            var userManager = scope.ServiceProvider
                .GetRequiredService<UserManager<ApplicationUser>>();

            await db.Database.MigrateAsync();

            var menuDefinitions = new[]
            {
            new Menu
            {
                Name = "Dashboard",
                Route = "/dashboard",
                DisplayOrder = 1
            },
            new Menu
            {
                Name = "User Master",
                Route = "/users",
                DisplayOrder = 2
            },
            new Menu
            {
                Name = "Role Master",
                Route = "/roles",
                DisplayOrder = 3
            },
            new Menu
            {
                Name = "Pending Approvals",
                Route = "/approvals",
                DisplayOrder = 4
            },
            new Menu
            {
                Name = "Audit Logs",
                Route = "/audit-logs",
                DisplayOrder = 5
            }
        };

            foreach (var menu in menuDefinitions)
            {
                if (!await db.Menus.AnyAsync(
                    x => x.Route == menu.Route))
                {
                    db.Menus.Add(menu);
                }
            }

            var permissionDefinitions = new[]
            {
            ("DASHBOARD_VIEW", "View dashboard"),
            ("USER_VIEW", "View users"),
            ("USER_CREATE", "Create user requests"),
            ("USER_EDIT", "Edit user requests"),
            ("USER_APPROVE", "Approve user requests"),
            ("USER_REJECT", "Reject user requests"),
            ("ROLE_VIEW", "View roles"),
            ("ROLE_CREATE", "Create roles"),
            ("ROLE_EDIT", "Edit roles"),
            ("ROLE_ASSIGN", "Assign role permissions"),
            ("AUDIT_VIEW", "View audit logs")
        };

            foreach (var item in permissionDefinitions)
            {
                if (!await db.Permissions.AnyAsync(
                    x => x.Code == item.Item1))
                {
                    db.Permissions.Add(new Permission
                    {
                        Code = item.Item1,
                        Description = item.Item2
                    });
                }
            }

            await db.SaveChangesAsync();

            var roleNames = new[]
            {
            "ADMIN",
            "USER_MAKER",
            "USER_CHECKER",
            "VIEWER"
        };

            foreach (var roleName in roleNames)
            {
                if (!await db.Roles.AnyAsync(
                    x => x.NormalizedName == roleName))
                {
                    db.Roles.Add(new ApplicationRole
                    {
                        Name = roleName,
                        NormalizedName = roleName,
                        Description = roleName switch
                        {
                            "ADMIN" => "System administrator",
                            "USER_MAKER" => "Creates user requests",
                            "USER_CHECKER" => "Approves user requests",
                            _ => "Read-only user"
                        },
                        IsActive = true
                    });
                }
            }

            await db.SaveChangesAsync();

            var permissions = await db.Permissions.ToListAsync();
            var menus = await db.Menus.ToListAsync();
            var roles = await db.Roles.ToListAsync();

            foreach (var role in roles)
            {
                foreach (var menu in menus)
                {
                    var allowedCodes = GetAllowedPermissions(
                        role.Name ?? "",
                        menu.Route);

                    foreach (var code in allowedCodes)
                    {
                        var permission = permissions.First(
                            x => x.Code == code);

                        var exists = await db.RolePermissions.AnyAsync(
                            x => x.RoleId == role.Id
                                 && x.MenuId == menu.Id
                                 && x.PermissionId == permission.Id);

                        if (!exists)
                        {
                            db.RolePermissions.Add(
                                new RolePermission
                                {
                                    RoleId = role.Id,
                                    MenuId = menu.Id,
                                    PermissionId = permission.Id,
                                    IsAllowed = true
                                });
                        }
                    }
                }
            }

            await db.SaveChangesAsync();

            var adminUsername =
                configuration["BootstrapAdmin:Username"];

            var adminPassword =
                configuration["BootstrapAdmin:Password"];

            if (string.IsNullOrWhiteSpace(adminUsername) ||
                string.IsNullOrWhiteSpace(adminPassword))
            {
                throw new InvalidOperationException(
                    "Configure BootstrapAdmin:Username and " +
                    "BootstrapAdmin:Password using User Secrets.");
            }

            var admin = await userManager.FindByNameAsync(
                adminUsername);

            if (admin == null)
            {
                admin = new ApplicationUser
                {
                    UserName = adminUsername,
                    Email = configuration["BootstrapAdmin:Email"],
                    FullName = "System Administrator",
                    IsActive = true,
                    EmailConfirmed = false
                };

                var result = await userManager.CreateAsync(
                    admin,
                    adminPassword);

                if (!result.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join(
                            "; ",
                            result.Errors.Select(x => x.Description)));
                }
            }

            var adminRole = roles.First(x => x.Name == "ADMIN");

            var checkerUsername =
                configuration["BootstrapChecker:Username"];

            var checkerPassword =
                configuration["BootstrapChecker:Password"];

            var checkerEmail =
                configuration["BootstrapChecker:Email"];

            if (string.IsNullOrWhiteSpace(checkerUsername) ||
                string.IsNullOrWhiteSpace(checkerPassword) ||
                string.IsNullOrWhiteSpace(checkerEmail))
            {
                throw new InvalidOperationException(
                    "Configure BootstrapChecker settings using User Secrets.");
            }

            var checker = await userManager.FindByNameAsync(
                checkerUsername);

            if (checker == null)
            {
                checker = new ApplicationUser
                {
                    UserName = checkerUsername,
                    Email = checkerEmail,
                    FullName = "User Checker",
                    IsActive = true,
                    EmailConfirmed = false
                };

                var checkerResult = await userManager.CreateAsync(
                    checker,
                    checkerPassword);

                if (!checkerResult.Succeeded)
                {
                    throw new InvalidOperationException(
                        string.Join(
                            "; ",
                            checkerResult.Errors.Select(x => x.Description)));
                }
            }

            var checkerRole = roles.First(x => x.Name == "USER_CHECKER");

            if (!await db.UserRoles.AnyAsync(
                x => x.UserId == checker.Id &&
                     x.RoleId == checkerRole.Id))
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = checker.Id,
                    RoleId = checkerRole.Id,
                    AssignedByUserId = admin.Id
                });

                await db.SaveChangesAsync();
            }

            if (!await db.UserRoles.AnyAsync(
                x => x.UserId == admin.Id
                     && x.RoleId == adminRole.Id))
            {
                db.UserRoles.Add(new UserRole
                {
                    UserId = admin.Id,
                    RoleId = adminRole.Id,
                    AssignedByUserId = admin.Id
                });

                await db.SaveChangesAsync();
            }
        }

        private static string[] GetAllowedPermissions(
            string role,
            string route)
        {
            if (role == "ADMIN")
            {
                return route switch
                {
                    "/dashboard" => new[] { "DASHBOARD_VIEW" },
                    "/users" => new[]
                    {
                    "USER_VIEW", "USER_CREATE", "USER_EDIT",
                    "USER_APPROVE", "USER_REJECT"
                },
                    "/roles" => new[]
                    {
                    "ROLE_VIEW", "ROLE_CREATE", "ROLE_EDIT",
                    "ROLE_ASSIGN"
                },
                    "/approvals" => new[]
                    {
                    "USER_APPROVE", "USER_REJECT"
                },
                    "/audit-logs" => new[] { "AUDIT_VIEW" },
                    _ => Array.Empty<string>()
                };
            }

            if (role == "USER_MAKER")
            {
                return route switch
                {
                    "/dashboard" => new[] { "DASHBOARD_VIEW" },
                    "/users" => new[]
                    {
                    "USER_VIEW", "USER_CREATE", "USER_EDIT"
                },
                    _ => Array.Empty<string>()
                };
            }

            if (role == "USER_CHECKER")
            {
                return route switch
                {
                    "/dashboard" => new[] { "DASHBOARD_VIEW" },
                    "/users" => new[] { "USER_VIEW" },
                    "/approvals" => new[]
                    {
                    "USER_APPROVE", "USER_REJECT"
                },
                    "/audit-logs" => new[] { "AUDIT_VIEW" },
                    _ => Array.Empty<string>()
                };
            }

            if (role == "VIEWER")
            {
                return route switch
                {
                    "/dashboard" => new[] { "DASHBOARD_VIEW" },
                    "/users" => new[] { "USER_VIEW" },
                    _ => Array.Empty<string>()
                };
            }

            return Array.Empty<string>();
        }
    }
}
