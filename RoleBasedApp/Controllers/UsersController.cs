using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using RoleBasedApp.Security;
using Microsoft.AspNetCore.Identity;
using RoleBasedApp.Models;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UsersController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;

        public UsersController(
     ApplicationDbContext db,
     UserManager<ApplicationUser> userManager)
        {
            _db = db;
            _userManager = userManager;
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

        [HttpPut("{id:int}/status")]
        [HasPermission("USER_EDIT")]
        public async Task<IActionResult> UpdateUserStatus(
    int id,
    [FromBody] bool isActive)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
                return NotFound("User not found.");

            user.IsActive = isActive;

            var result = await _userManager.UpdateAsync(user);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(x => x.Description)
                });
            }

            return Ok(new
            {
                message = "User status updated successfully.",
                userId = user.Id,
                userName = user.UserName,
                isActive = user.IsActive
            });
        }

        [HttpPut("{id:int}/reset-password")]
        [HasPermission("USER_EDIT")]
        public async Task<IActionResult> ResetUserPassword(
    int id,
    [FromBody] string newPassword)
        {
            var user = await _userManager.FindByIdAsync(id.ToString());

            if (user == null)
                return NotFound("User not found.");

            if (string.IsNullOrWhiteSpace(newPassword))
                return BadRequest("Password is required.");

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);

            var result = await _userManager.ResetPasswordAsync(
                user,
                token,
                newPassword);

            if (!result.Succeeded)
            {
                return BadRequest(new
                {
                    errors = result.Errors.Select(x => x.Description)
                });
            }

            return Ok(new
            {
                message = "Password reset successfully.",
                userId = user.Id,
                userName = user.UserName
            });
        }

    }
}
