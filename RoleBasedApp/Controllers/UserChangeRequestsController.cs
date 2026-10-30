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
using System.Security.Claims;
using System.Text.Json;

namespace RoleBasedApp.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class UserChangeRequestsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly AuditService _audit;

        public UserChangeRequestsController(
            ApplicationDbContext db,
            UserManager<ApplicationUser> userManager,
            AuditService audit)
        {
            _db = db;
            _userManager = userManager;
            _audit = audit;
        }

        private int CurrentUserId()
        {
            var value = User.FindFirstValue(
                ClaimTypes.NameIdentifier);

            return int.TryParse(value, out var id)
                ? id
                : throw new UnauthorizedAccessException();
        }

        [HttpPost("create-user")]
        [HasPermission("USER_CREATE")]
        public async Task<IActionResult> CreateUserRequest(
            CreateUserRequest request)
        {
            var username = request.Username.Trim();
            var email = request.Email.Trim();

            var roleExists = await _db.Roles.AnyAsync(
                x => x.Id == request.RoleId && x.IsActive);

            if (!roleExists)
                return BadRequest("Invalid or inactive role.");

            if (await _userManager.FindByNameAsync(username) != null)
                return Conflict("Username already exists.");

            if (await _userManager.FindByEmailAsync(email) != null)
                return Conflict("Email already exists.");

            var makerId = CurrentUserId();

            var payload = new
            {
                username,
                fullName = request.FullName.Trim(),
                email,
                roleId = request.RoleId
            };

            var change = new UserChangeRequest
            {
                RequestType = ChangeRequestType.Create,
                Status = ApprovalStatus.Pending,
                MakerUserId = makerId,
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            _db.UserChangeRequests.Add(change);

            _audit.Add(
                "USER_CREATE_REQUESTED",
                "UserChangeRequest",
                null,
                null,
                payload,
                true);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                requestId = change.Id,
                status = change.Status.ToString()
            });
        }

        [HttpPost("{id:int}/role-change")]
        [HasPermission("USER_EDIT")]
        public async Task<IActionResult> RequestRoleChange(
            int id,
            [FromBody] int newRoleId)
        {
            var target = await _db.Users.FindAsync(id);

            if (target == null)
                return NotFound("User not found.");

            if (target.Id == CurrentUserId())
            {
                return BadRequest(
                    "You cannot request a role change for yourself.");
            }

            var role = await _db.Roles.FirstOrDefaultAsync(
                x => x.Id == newRoleId && x.IsActive);

            if (role == null)
                return BadRequest("Invalid or inactive role.");

            var alreadyPending = await _db.UserChangeRequests
                .AnyAsync(x =>
                    x.TargetUserId == id &&
                    x.Status == ApprovalStatus.Pending);

            if (alreadyPending)
                return Conflict("A pending request already exists.");

            var currentRoles = await _db.UserRoles
                .Where(x => x.UserId == id)
                .Select(x => x.RoleId)
                .ToListAsync();

            var payload = new
            {
                roleId = newRoleId,
                previousRoleIds = currentRoles
            };

            var change = new UserChangeRequest
            {
                TargetUserId = id,
                RequestType = ChangeRequestType.RoleChange,
                Status = ApprovalStatus.Pending,
                MakerUserId = CurrentUserId(),
                PayloadJson = JsonSerializer.Serialize(payload)
            };

            _db.UserChangeRequests.Add(change);

            _audit.Add(
                "USER_ROLE_CHANGE_REQUESTED",
                "User",
                id.ToString(),
                new { roleIds = currentRoles },
                new { roleId = newRoleId },
                true);

            await _db.SaveChangesAsync();

            return Ok(new
            {
                requestId = change.Id,
                status = change.Status.ToString()
            });
        }

        [HttpGet("pending")]
        [HasPermission("USER_APPROVE_OR_REJECT")]
        public async Task<IActionResult> Pending()
        {
            var requests = await _db.UserChangeRequests
                .Where(x => x.Status == ApprovalStatus.Pending)
                .OrderBy(x => x.CreatedAtUtc)
                .Select(x => new
                {
                    x.Id,
                    x.TargetUserId,
                    requestType = x.RequestType.ToString(),
                    x.MakerUserId,
                    x.CreatedAtUtc,
                    x.PayloadJson
                })
                .ToListAsync();

            return Ok(requests);
        }

        [HttpGet("{id:int}")]
        [HasPermission("USER_APPROVE")]
        public async Task<IActionResult> GetRequest(int id)
        {
            var request = await _db.UserChangeRequests
                .Where(x => x.Id == id)
                .Select(x => new
                {
                    x.Id,
                    x.TargetUserId,
                    requestType = x.RequestType.ToString(),
                    status = x.Status.ToString(),
                    x.MakerUserId,
                    x.CheckerUserId,
                    x.PayloadJson,
                    x.RejectionReason,
                    x.CreatedAtUtc,
                    x.DecidedAtUtc
                })
                .FirstOrDefaultAsync();

            if (request == null)
                return NotFound();

            var history = await _db.ApprovalHistory
                .Where(x => x.RequestId == id)
                .OrderBy(x => x.DecisionAtUtc)
                .ToListAsync();

            return Ok(new { request, history });
        }

        [HttpPost("{id:int}/approve")]
        [HasPermission("USER_APPROVE")]
        public async Task<IActionResult> Approve(int id)
        {
            var checkerId = CurrentUserId();

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var request = await _db.UserChangeRequests
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
                return NotFound();

            if (request.Status != ApprovalStatus.Pending)
                return Conflict("Request has already been processed.");

            if (request.MakerUserId == checkerId)
                return BadRequest("Maker cannot approve their own request.");

            if (request.RequestType == ChangeRequestType.Create)
            {
                //var payload = JsonSerializer.Deserialize<CreateUserRequest>(
                //    request.PayloadJson);
                var payload = JsonSerializer.Deserialize<CreateUserRequest>(
    request.PayloadJson,
    new JsonSerializerOptions
    {
        PropertyNameCaseInsensitive = true
    });

                if (payload == null)
                    return BadRequest("Invalid request payload.");

                if (await _userManager.FindByNameAsync(
                    payload.Username) != null)
                {
                    return Conflict("Username already exists.");
                }

                if (await _userManager.FindByEmailAsync(
                    payload.Email) != null)
                {
                    return Conflict("Email already exists.");
                }

                var roleExists = await _db.Roles.AnyAsync(
                    x => x.Id == payload.RoleId && x.IsActive);

                if (!roleExists)
                    return BadRequest("Role is no longer active.");

                var newUser = new ApplicationUser
                {
                    UserName = payload.Username.Trim(),
                    Email = payload.Email.Trim(),
                    FullName = payload.FullName.Trim(),
                    IsActive = false,
                    EmailConfirmed = false
                };

                // The Identity store uses the same scoped DbContext.
                // The outer transaction covers these writes.
                var result = await _userManager.CreateAsync(newUser);

                if (!result.Succeeded)
                {
                    return BadRequest(new
                    {
                        errors = result.Errors.Select(x => x.Description)
                    });
                }

                _db.UserRoles.Add(new UserRole
                {
                    UserId = newUser.Id,
                    RoleId = payload.RoleId,
                    AssignedByUserId = checkerId
                });

                request.TargetUserId = newUser.Id;
            }
            else if (request.RequestType == ChangeRequestType.RoleChange)
            {
                var payload = JsonSerializer.Deserialize<RoleChangePayload>(
                    request.PayloadJson);

                if (payload == null)
                    return BadRequest("Invalid role-change payload.");

                var target = await _db.Users
                    .FirstOrDefaultAsync(x =>
                        x.Id == request.TargetUserId);

                if (target == null)
                    return NotFound("Target user not found.");

                if (target.Id == checkerId)
                    return BadRequest("You cannot approve your own role change.");

                var roleExists = await _db.Roles.AnyAsync(
                    x => x.Id == payload.RoleId && x.IsActive);

                if (!roleExists)
                    return BadRequest("Role is no longer active.");

                var existing = await _db.UserRoles
                    .Where(x => x.UserId == target.Id)
                    .ToListAsync();

                _db.UserRoles.RemoveRange(existing);

                _db.UserRoles.Add(new UserRole
                {
                    UserId = target.Id,
                    RoleId = payload.RoleId,
                    AssignedByUserId = checkerId
                });
            }
            else
            {
                return BadRequest(
                    "This request type is not implemented in this example.");
            }

            request.Status = ApprovalStatus.Approved;
            request.CheckerUserId = checkerId;
            request.DecidedAtUtc = DateTime.UtcNow;

            _db.ApprovalHistory.Add(new ApprovalHistory
            {
                RequestId = request.Id,
                MakerUserId = request.MakerUserId,
                CheckerUserId = checkerId,
                Decision = ApprovalStatus.Approved,
                Comments = "Approved",
                DecisionAtUtc = DateTime.UtcNow
            });

            _audit.Add(
                "USER_CHANGE_APPROVED",
                "UserChangeRequest",
                request.Id.ToString(),
                new { status = ApprovalStatus.Pending.ToString() },
                new { status = ApprovalStatus.Approved.ToString() },
                true);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Request approved.",
                requestId = request.Id,
                status = request.Status.ToString()
            });
        }

        [HttpPost("{id:int}/reject")]
        [HasPermission("USER_REJECT")]
        public async Task<IActionResult> Reject(
            int id,
            RejectRequest input)
        {
            var checkerId = CurrentUserId();

            await using var transaction =
                await _db.Database.BeginTransactionAsync();

            var request = await _db.UserChangeRequests
                .FirstOrDefaultAsync(x => x.Id == id);

            if (request == null)
                return NotFound();

            if (request.Status != ApprovalStatus.Pending)
                return Conflict("Request has already been processed.");

            if (request.MakerUserId == checkerId)
                return BadRequest("Maker cannot reject their own request.");

            request.Status = ApprovalStatus.Rejected;
            request.CheckerUserId = checkerId;
            request.RejectionReason = input.Reason.Trim();
            request.DecidedAtUtc = DateTime.UtcNow;

            _db.ApprovalHistory.Add(new ApprovalHistory
            {
                RequestId = request.Id,
                MakerUserId = request.MakerUserId,
                CheckerUserId = checkerId,
                Decision = ApprovalStatus.Rejected,
                Comments = input.Reason.Trim(),
                DecisionAtUtc = DateTime.UtcNow
            });

            _audit.Add(
                "USER_CHANGE_REJECTED",
                "UserChangeRequest",
                request.Id.ToString(),
                new { status = ApprovalStatus.Pending.ToString() },
                new
                {
                    status = ApprovalStatus.Rejected.ToString(),
                    reason = input.Reason.Trim()
                },
                true);

            await _db.SaveChangesAsync();
            await transaction.CommitAsync();

            return Ok(new
            {
                message = "Request rejected.",
                requestId = request.Id,
                status = request.Status.ToString()
            });
        }

        public class RoleChangePayload
        {
            public int RoleId { get; set; }

            public List<int> PreviousRoleIds { get; set; } = new();
        }
    }
}

