using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Claims;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace CbeMeetingAttendance.Controllers
{
    [ApiController]
    [Route("api/users")]
    public class UserController : ControllerBase
    {
        private readonly IUserService _userService;
        private readonly IWebHostEnvironment _environment;

        public UserController(IUserService userService, IWebHostEnvironment environment)
        {
            _userService = userService ?? throw new ArgumentNullException(nameof(userService));
            _environment = environment ?? throw new ArgumentNullException(nameof(environment));
        }

        // POST /api/users/register — Public (anyone can create an account)
        [HttpPost("register")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> RegisterUser([FromBody] RegisterUserDto dto)
        {
            var result = await _userService.RegisterUserAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // GET /api/users — Admin only (list all user accounts)
        [Authorize(Roles = "Admin")]
        [HttpGet]
        public async Task<ActionResult<ApiResponse<IEnumerable<UserDto>>>> GetAllUsers()
        {
            var result = await _userService.GetAllUsersAsync();
            return Ok(result);
        }

        // GET /api/users/{id} — Admin only
        [Authorize(Roles = "Admin")]
        [HttpGet("{id:guid}")]
        public async Task<ActionResult<ApiResponse<UserDto>>> GetUserById(Guid id)
        {
            var result = await _userService.GetUserByIdAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // POST /api/users/login — Public
        [HttpPost("login")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Login([FromBody] LoginDto dto)
        {
            var result = await _userService.LoginAsync(dto);
            if (!result.Success)
            {
                return Unauthorized(result);
            }
            return Ok(result);
        }

        // POST /api/users/logout — Any authenticated user
        [Authorize]
        [HttpPost("logout")]
        public async Task<ActionResult<ApiResponse<bool>>> Logout()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<bool>.FailureResult("Invalid token."));
            }

            var result = await _userService.LogoutAsync(userId);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // POST /api/users/refresh — Public
        [HttpPost("refresh")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> Refresh([FromBody] RefreshTokenDto dto)
        {
            var result = await _userService.RefreshAsync(dto);
            if (!result.Success)
            {
                return Unauthorized(result);
            }
            return Ok(result);
        }

        // POST /api/users/assign-role — Admin only
        [Authorize(Roles = "Admin")]
        [HttpPost("assign-role")]
        public async Task<ActionResult<ApiResponse<bool>>> AssignRole([FromBody] AssignRoleDto dto)
        {
            var result = await _userService.AssignRoleAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
 
        // POST /api/users/change-role — Admin only
        [Authorize(Roles = "Admin")]
        [HttpPost("change-role")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangeRole([FromBody] ChangeRoleDto dto)
        {
            var result = await _userService.ChangeRoleAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
 
        // POST /api/users/remove-role — Admin only
        [Authorize(Roles = "Admin")]
        [HttpPost("remove-role")]
        public async Task<ActionResult<ApiResponse<bool>>> RemoveRole([FromBody] RemoveRoleDto dto)
        {
            var result = await _userService.RemoveRoleAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
 
        // POST /api/users/change-password — Any authenticated user (can change own password)
        [Authorize]
        [HttpPost("change-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ChangePassword([FromBody] ChangePasswordDto dto)
        {
            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(currentUserIdClaim, out var currentUserId))
            {
                return Unauthorized(ApiResponse<bool>.FailureResult("Invalid token."));
            }
 
            if (currentUserId != dto.UserId && !User.IsInRole("Admin"))
            {
                return Forbid();
            }
 
            var result = await _userService.ChangePasswordAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
 
        // POST /api/users/reset-password — Admin only
        [Authorize(Roles = "Admin")]
        [HttpPost("reset-password")]
        public async Task<ActionResult<ApiResponse<bool>>> ResetPassword([FromBody] ResetPasswordDto dto)
        {
            var result = await _userService.ResetPasswordAsync(dto);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // GET /api/users/me — Any authenticated user (get own profile)
        [Authorize]
        [HttpGet("me")]
        public async Task<ActionResult<ApiResponse<AuthResponseDto>>> GetCurrentUser()
        {
            var userIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                return Unauthorized(ApiResponse<AuthResponseDto>.FailureResult("Invalid token."));
            }

            var result = await _userService.GetCurrentUserAsync(userId);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // DELETE /api/users/me — Any authenticated user (delete own account)
        [Authorize]
        [HttpDelete("me")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteCurrentUser()
        {
            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(currentUserIdClaim, out var currentUserId))
            {
                return Unauthorized(ApiResponse<bool>.FailureResult("Invalid token."));
            }
 
            var result = await _userService.DeleteUserAsync(currentUserId, _environment.WebRootPath);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }
 
        // DELETE /api/users/{id} — Admin only
        [Authorize(Roles = "Admin")]
        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteUser(Guid id)
        {
            var result = await _userService.DeleteUserAsync(id, _environment.WebRootPath);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // POST /api/users/{id}/profile-picture — Upload profile picture (self or Admin)
        [Authorize]
        [HttpPost("{id:guid}/profile-picture")]
        public async Task<ActionResult<ApiResponse<CurrentUserDto>>> UploadProfilePicture(Guid id, IFormFile file)
        {
            if (!CanAccessProfile(id))
            {
                return Forbid();
            }

            var result = await _userService.UploadProfilePictureAsync(id, file, _environment.WebRootPath ?? string.Empty);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // PUT /api/users/{id}/profile-picture — Update (replace) profile picture (self or Admin)
        [Authorize]
        [HttpPut("{id:guid}/profile-picture")]
        public async Task<ActionResult<ApiResponse<CurrentUserDto>>> UpdateProfilePicture(Guid id, IFormFile file)
        {
            if (!CanAccessProfile(id))
            {
                return Forbid();
            }

            var result = await _userService.UpdateProfilePictureAsync(id, file, _environment.WebRootPath ?? string.Empty);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // GET /api/users/{id}/profile-picture — Get profile picture path (self or Admin)
        [Authorize]
        [HttpGet("{id:guid}/profile-picture")]
        public async Task<ActionResult<ApiResponse<string>>> GetProfilePicture(Guid id)
        {
            if (!CanAccessProfile(id))
            {
                return Forbid();
            }

            var result = await _userService.GetProfilePicturePathAsync(id);
            if (!result.Success)
            {
                return NotFound(result);
            }
            return Ok(result);
        }

        // DELETE /api/users/{id}/profile-picture — Delete profile picture (self or Admin)
        [Authorize]
        [HttpDelete("{id:guid}/profile-picture")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteProfilePicture(Guid id)
        {
            if (!CanAccessProfile(id))
            {
                return Forbid();
            }

            var result = await _userService.DeleteProfilePictureAsync(id, _environment.WebRootPath ?? string.Empty);
            if (!result.Success)
            {
                return BadRequest(result);
            }
            return Ok(result);
        }

        // Users may only upload/change their own profile picture unless the requester is an Admin
        private bool CanAccessProfile(Guid targetUserId)
        {
            var currentUserIdClaim = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (!Guid.TryParse(currentUserIdClaim, out var currentUserId))
            {
                return false;
            }

            return currentUserId == targetUserId || User.IsInRole("Admin");
        }
    }
}
