using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using RoleBasedApp.Security;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public UsersController(ApplicationDbContext db)
        {
            _db = db;
        }

        [HttpGet]
        [HasPermission("USER_VIEW")]
        public async Task<IActionResult> GetUsers()
        {
            var users = await (
                from user in _db.Users

                join userRole in _db.UserRoles
                    on user.Id equals userRole.UserId into roleJoin

                from userRole in roleJoin.DefaultIfEmpty()

                join role in _db.Roles
                    on userRole.RoleId equals role.Id into roleDetails

                from role in roleDetails.DefaultIfEmpty()

                select new
                {
                    user.Id,
                    user.UserName,
                    user.FullName,
                    user.Email,
                    user.IsActive,
                    RoleName = role == null ? null : role.Name
                }
            ).ToListAsync();

            return Ok(users);
        }
    }
}
