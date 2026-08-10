using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace CbeMeetingAttendance.DTOs
{
    public class RegisterUserDto
    {
        [Required(ErrorMessage = "Email is required")]
        [EmailAddress]
        public required string Email { get; set; }

        [Required(ErrorMessage = "UserName is required")]
        public required string UserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        [MinLength(6, ErrorMessage = "Password must be at least 6 characters")]
        public required string Password { get; set; }

        [Required(ErrorMessage = "EmployeeId is required")]
        public required string EmployeeId { get; set; }
    }

    public class LoginDto
    {
        [Required(ErrorMessage = "Email or UserName is required")]
        public required string EmailOrUserName { get; set; }

        [Required(ErrorMessage = "Password is required")]
        public required string Password { get; set; }
    }

    public class RefreshTokenDto
    {
        [Required(ErrorMessage = "RefreshToken is required")]
        public required string RefreshToken { get; set; }
    }

    public class AssignRoleDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "RoleName is required")]
        public required string RoleName { get; set; }
    }

    public class ChangeRoleDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "CurrentRole is required")]
        public required string CurrentRole { get; set; }

        [Required(ErrorMessage = "NewRole is required")]
        public required string NewRole { get; set; }
    }

    public class RemoveRoleDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "RoleName is required")]
        public required string RoleName { get; set; }
    }

    public class ChangePasswordDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "CurrentPassword is required")]
        public required string CurrentPassword { get; set; }

        [Required(ErrorMessage = "NewPassword is required")]
        [MinLength(6)]
        public required string NewPassword { get; set; }
    }

    public class ResetPasswordDto
    {
        [Required]
        public Guid UserId { get; set; }

        [Required(ErrorMessage = "NewPassword is required")]
        [MinLength(6)]
        public required string NewPassword { get; set; }
    }

    public class CurrentUserDto
    {
        public Guid UserId { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public Guid EmployeeId { get; set; }
        public EmployeeDto? Employee { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public string? ProfilePicturePath { get; set; }
    }

    public class AuthResponseDto
    {
        public required string Token { get; set; }
        public DateTime Expiration { get; set; }
        public required CurrentUserDto User { get; set; }

        // Needed so the client can call POST /api/users/refresh
        public string? RefreshToken { get; set; }
    }

    public class UserDto
    {
        public Guid Id { get; set; }
        public string? Email { get; set; }
        public string? UserName { get; set; }
        public Guid EmployeeId { get; set; }
        public EmployeeDto? Employee { get; set; }
        public IList<string> Roles { get; set; } = new List<string>();
        public bool LockoutEnabled { get; set; }
        public DateTimeOffset? LockoutEnd { get; set; }
        public int AccessFailedCount { get; set; }
        public string? ProfilePicturePath { get; set; }
    }
}
