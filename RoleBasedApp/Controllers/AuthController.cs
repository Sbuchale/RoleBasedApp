using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using RoleBasedApp.Data;
using RoleBasedApp.Models;
using RoleBasedApp.Services;
using System.Security.Claims;
using RoleBasedApp.DTOs;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _db;
        private readonly TokenService _tokenService;
        private readonly AuditService _audit;

        public AuthController(
            UserManager<ApplicationUser> userManager,
            ApplicationDbContext db,
            TokenService tokenService,
            AuditService audit)
        {
            _userManager = userManager;
            _db = db;
            _tokenService = tokenService;
            _audit = audit;
        }

        [AllowAnonymous]
        [HttpPost("login")]
        public async Task<IActionResult> Login(LoginRequest request)
        {
            var user = await _userManager.FindByNameAsync(
                request.Username);

            Console.WriteLine(
    $"Login debug: UserFound={user != null}, IsActive={user?.IsActive}");

            if (user == null || !user.IsActive)
            {
                _audit.Add(
                    "LOGIN_FAILED", "User", null,
                    null, new { request.Username },
                    false, "INVALID_CREDENTIALS");

                await _audit.SaveAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }

            var validPassword =
                await _userManager.CheckPasswordAsync(
                    user, request.Password);

            if (!validPassword)
            {
                _audit.Add(
                    "LOGIN_FAILED", "User", user.Id.ToString(),
                    null, null, false, "INVALID_CREDENTIALS");

                await _audit.SaveAsync();

                return Unauthorized(new
                {
                    message = "Invalid username or password."
                });
            }

            var roleNames = await (
                from userRole in _db.UserRoles
                join role in _db.Roles
                    on userRole.RoleId equals role.Id
                where userRole.UserId == user.Id
                      && role.IsActive
                select role.Name!
            ).Distinct().ToListAsync();

            var (token, expiresIn) =
                _tokenService.GenerateToken(user);

            _audit.Add(
                "LOGIN_SUCCESS", "User", user.Id.ToString(),
                null, new { user.UserName },
                true);

            await _audit.SaveAsync();

            return Ok(new
            {
                accessToken = token,
                expiresIn,
                username = user.UserName,
                fullName = user.FullName,
                roles = roleNames
            });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> Me()
        {
            var idValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idValue, out var userId))
                return Unauthorized();

            var user = await _db.Users
                .Where(x => x.Id == userId && x.IsActive)
                .Select(x => new
                {
                    x.Id,
                    x.UserName,
                    x.FullName,
                    x.Email
                })
                .FirstOrDefaultAsync();

            if (user == null)
                return Unauthorized();

            return Ok(user);
        }

        [Authorize]
        [HttpGet("my-permissions")]
        public async Task<IActionResult> MyPermissions()
        {
            var idValue = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            if (!int.TryParse(idValue, out var userId))
                return Unauthorized();

            var permissions = await (
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
                      && menu.IsActive
                select permission.Code
            ).Distinct().ToListAsync();

            return Ok(permissions);
        }

    }
}
