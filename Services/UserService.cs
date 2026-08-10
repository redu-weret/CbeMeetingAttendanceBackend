using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.IO;
using System.Linq;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Mappers;
using CbeMeetingAttendance.Models;
using CbeMeetingAttendance.Repositories;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;

namespace CbeMeetingAttendance.Services
{
    public class UserService : IUserService
    {
        private const string RefreshTokenProvider = "CbeMeetingAttendance";
        private const string RefreshTokenName = "RefreshToken";

        // Profile picture settings
        private static readonly string[] AllowedImageExtensions = { ".jpg", ".jpeg", ".png", ".webp" };
        private static readonly long MaxProfilePictureBytes = 5 * 1024 * 1024; // 5 MB
        private const string ProfilesFolderName = "Profiles";

        private readonly UserManager<User> _userManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IUserRepository _userRepository;
        private readonly IEmployeeRepository _employeeRepository;
        private readonly IConfiguration _configuration;

        public UserService(
            UserManager<User> userManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IUserRepository userRepository,
            IEmployeeRepository employeeRepository,
            IConfiguration configuration)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _userRepository = userRepository ?? throw new ArgumentNullException(nameof(userRepository));
            _employeeRepository = employeeRepository ?? throw new ArgumentNullException(nameof(employeeRepository));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public async Task<ApiResponse<AuthResponseDto>> RegisterUserAsync(RegisterUserDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            Employee? employee = null;
            if (Guid.TryParse(dto.EmployeeId, out var employeeGuid))
            {
                employee = await _employeeRepository.GetByIdAsync(employeeGuid);
            }
            else
            {
                employee = await _employeeRepository.GetByEmployeeIdAsync(dto.EmployeeId.Trim());
            }

            if (employee == null)
            {
                return ApiResponse<AuthResponseDto>.FailureResult("Employee not found. Register the employee first or use the correct employee code.");
            }

            if (await _userRepository.ExistsByEmployeeIdAsync(employee.Id))
            {
                return ApiResponse<AuthResponseDto>.FailureResult("A user account is already linked to this employee.");
            }

            if (await _userRepository.ExistsByEmailAsync(dto.Email))
            {
                return ApiResponse<AuthResponseDto>.FailureResult($"User with email '{dto.Email}' already exists.");
            }

            var existingUserName = await _userManager.FindByNameAsync(dto.UserName);
            if (existingUserName != null)
            {
                return ApiResponse<AuthResponseDto>.FailureResult($"UserName '{dto.UserName}' is already taken.");
            }

            var user = new User
            {
                Id = Guid.NewGuid(),
                UserName = dto.UserName.Trim(),
                Email = dto.Email.Trim(),
                EmployeeId = employee.Id,
                EmailConfirmed = true
            };

            var result = await _userManager.CreateAsync(user, dto.Password);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<AuthResponseDto>.FailureResult($"Registration failed: {errors}");
            }

            // Generate JWT token and return auth response (so frontend can auto-login)
            var authResponse = await BuildAuthResponseAsync(user);
            return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "User registered successfully.");
        }

        public async Task<ApiResponse<IEnumerable<UserDto>>> GetAllUsersAsync()
        {
            var users = (await _userRepository.GetAllAsync()).ToList();
            var dtos = new List<UserDto>();

            foreach (var user in users)
            {
                var roles = await _userManager.GetRolesAsync(user);
                dtos.Add(user.ToDto(roles));
            }

            return ApiResponse<IEnumerable<UserDto>>.SuccessResult(dtos, "Users retrieved successfully.", dtos.Count);
        }

        public async Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id)
        {
            var user = await _userRepository.GetByIdAsync(id);
            if (user == null)
            {
                return ApiResponse<UserDto>.FailureResult("User not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            return ApiResponse<UserDto>.SuccessResult(user.ToDto(roles), "User retrieved successfully.");
        }

        
        public async Task<ApiResponse<AuthResponseDto>> GetCurrentUserAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<AuthResponseDto>.FailureResult("User not found.");
            }

            var roles = await _userManager.GetRolesAsync(user);
            var employee = user.Employee ?? await _employeeRepository.GetByIdAsync(user.EmployeeId);

            return ApiResponse<AuthResponseDto>.SuccessResult(new AuthResponseDto
            {
                Token = string.Empty,
                RefreshToken = null,
                Expiration = DateTime.UtcNow,
                User = user.ToCurrentUserDto(roles, employee)
            }, "Current user retrieved successfully.");
        }

        public async Task<ApiResponse<bool>> DeleteUserAsync(Guid id, string? webRootPath = null)
        {
            // Use UserManager to delete so Identity cleans up related data (roles, tokens, logins)
            var user = await _userManager.FindByIdAsync(id.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var result = await _userManager.DeleteAsync(user);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to delete user: {errors}");
            }

            // Clean up the profile picture file only AFTER the user row is gone,
            // so a failed delete never leaves the DB pointing at a missing file.
            if (!string.IsNullOrEmpty(user.ProfilePicturePath) && !string.IsNullOrEmpty(webRootPath))
            {
                DeletePhysicalFileIfExists(webRootPath, user.ProfilePicturePath);
            }

            return ApiResponse<bool>.SuccessResult(true, "User deleted successfully.");
        }

        public async Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userRepository.GetByEmailAsync(dto.EmailOrUserName)
                       ?? await _userRepository.GetByUserNameAsync(dto.EmailOrUserName);

            if (user == null)
            {
                return ApiResponse<AuthResponseDto>.FailureResult("Invalid email/username or password.");
            }

            if (await _userManager.IsLockedOutAsync(user))
            {
                return ApiResponse<AuthResponseDto>.FailureResult(
                    $"Account is locked. Try again after {user.LockoutEnd?.LocalDateTime:yyyy-MM-dd HH:mm:ss}.");
            }

            var passwordValid = await _userManager.CheckPasswordAsync(user, dto.Password);
            if (!passwordValid)
            {
                await _userManager.AccessFailedAsync(user);
                var updatedUser = await _userManager.FindByIdAsync(user.Id.ToString());

                if (updatedUser != null && await _userManager.IsLockedOutAsync(updatedUser))
                {
                    return ApiResponse<AuthResponseDto>.FailureResult(
                        "Account has been locked due to too many failed login attempts.");
                }

                return ApiResponse<AuthResponseDto>.FailureResult(
                    $"Invalid email/username or password. Failed attempts: {updatedUser?.AccessFailedCount ?? user.AccessFailedCount}.");
            }

            // Successful login: clear failed attempts, then issue JWT
            await _userManager.ResetAccessFailedCountAsync(user);

            var authResponse = await BuildAuthResponseAsync(user);
            return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Login successful.");
        }

        public async Task<ApiResponse<bool>> LogoutAsync(Guid userId)
        {
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
            return ApiResponse<bool>.SuccessResult(true, "Logout successful.");
        }

        public async Task<ApiResponse<AuthResponseDto>> RefreshAsync(RefreshTokenDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var users = await _userRepository.GetAllAsync();
            User? matchedUser = null;

            foreach (var user in users)
            {
                var storedToken = await _userManager.GetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
                if (!string.IsNullOrEmpty(storedToken) && storedToken == dto.RefreshToken)
                {
                    matchedUser = user;
                    break;
                }
            }

            if (matchedUser == null)
            {
                return ApiResponse<AuthResponseDto>.FailureResult("Invalid refresh token.");
            }

            if (await _userManager.IsLockedOutAsync(matchedUser))
            {
                return ApiResponse<AuthResponseDto>.FailureResult("Account is locked.");
            }

            var authResponse = await BuildAuthResponseAsync(matchedUser);
            return ApiResponse<AuthResponseDto>.SuccessResult(authResponse, "Token refreshed successfully.");
        }

        public async Task<ApiResponse<bool>> AssignRoleAsync(AssignRoleDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var roleName = dto.RoleName.Trim();
            if (!await _roleManager.RoleExistsAsync(roleName))
            {
                var createRole = await _roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = roleName,
                    NormalizedName = roleName.ToUpperInvariant()
                });

                if (!createRole.Succeeded)
                {
                    var errors = string.Join(" | ", createRole.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.FailureResult($"Failed to create role: {errors}");
                }
            }

            if (await _userManager.IsInRoleAsync(user, roleName))
            {
                return ApiResponse<bool>.FailureResult($"User already has role '{roleName}'.");
            }

            var result = await _userManager.AddToRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to assign role: {errors}");
            }

            return ApiResponse<bool>.SuccessResult(true, $"Role '{roleName}' assigned successfully.");
        }

        public async Task<ApiResponse<bool>> ChangeRoleAsync(ChangeRoleDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var currentRole = dto.CurrentRole.Trim();
            var newRole = dto.NewRole.Trim();

            if (!await _userManager.IsInRoleAsync(user, currentRole))
            {
                return ApiResponse<bool>.FailureResult($"User does not have role '{currentRole}'.");
            }

            if (!await _roleManager.RoleExistsAsync(newRole))
            {
                var createRole = await _roleManager.CreateAsync(new IdentityRole<Guid>
                {
                    Id = Guid.NewGuid(),
                    Name = newRole,
                    NormalizedName = newRole.ToUpperInvariant()
                });

                if (!createRole.Succeeded)
                {
                    var errors = string.Join(" | ", createRole.Errors.Select(e => e.Description));
                    return ApiResponse<bool>.FailureResult($"Failed to create new role: {errors}");
                }
            }

            var removeResult = await _userManager.RemoveFromRoleAsync(user, currentRole);
            if (!removeResult.Succeeded)
            {
                var errors = string.Join(" | ", removeResult.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to remove current role: {errors}");
            }

            var addResult = await _userManager.AddToRoleAsync(user, newRole);
            if (!addResult.Succeeded)
            {
                await _userManager.AddToRoleAsync(user, currentRole);
                var errors = string.Join(" | ", addResult.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to assign new role: {errors}");
            }

            return ApiResponse<bool>.SuccessResult(true, $"Role changed from '{currentRole}' to '{newRole}'.");
        }

        public async Task<ApiResponse<bool>> RemoveRoleAsync(RemoveRoleDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var roleName = dto.RoleName.Trim();
            if (!await _userManager.IsInRoleAsync(user, roleName))
            {
                return ApiResponse<bool>.FailureResult($"User does not have role '{roleName}'.");
            }

            var result = await _userManager.RemoveFromRoleAsync(user, roleName);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to remove role: {errors}");
            }

            return ApiResponse<bool>.SuccessResult(true, $"Role '{roleName}' removed successfully.");
        }

        public async Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var result = await _userManager.ChangePasswordAsync(user, dto.CurrentPassword, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to change password: {errors}");
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            return ApiResponse<bool>.SuccessResult(true, "Password changed successfully.");
        }

        public async Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto)
        {
            if (dto == null) throw new ArgumentNullException(nameof(dto));

            var user = await _userManager.FindByIdAsync(dto.UserId.ToString());
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, dto.NewPassword);
            if (!result.Succeeded)
            {
                var errors = string.Join(" | ", result.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to reset password: {errors}");
            }

            await _userManager.ResetAccessFailedCountAsync(user);
            await _userManager.SetLockoutEndDateAsync(user, null);
            return ApiResponse<bool>.SuccessResult(true, "Password reset successfully.");
        }

        public Task<ApiResponse<CurrentUserDto>> UploadProfilePictureAsync(Guid userId, IFormFile file, string webRootPath)
        {
            return SaveProfilePictureAsync(userId, file, webRootPath, "uploaded");
        }

        public Task<ApiResponse<CurrentUserDto>> UpdateProfilePictureAsync(Guid userId, IFormFile file, string webRootPath)
        {
            return SaveProfilePictureAsync(userId, file, webRootPath, "updated");
        }

        private async Task<ApiResponse<CurrentUserDto>> SaveProfilePictureAsync(
            Guid userId,
            IFormFile file,
            string webRootPath,
            string verb)
        {
            var validation = ValidateImageFile(file);
            if (validation != null)
            {
                return ApiResponse<CurrentUserDto>.FailureResult(validation);
            }

            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<CurrentUserDto>.FailureResult("User not found.");
            }

            var imageBytes = await ReadFileBytesAsync(file);
            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            var fileName = $"{Guid.NewGuid():N}{extension}";
            var relativePath = $"{ProfilesFolderName}/{fileName}";
            var profilesDirectory = Path.Combine(webRootPath, ProfilesFolderName);

            Directory.CreateDirectory(profilesDirectory);
            var physicalPath = Path.Combine(profilesDirectory, fileName);

            // Save the new physical file first, then commit the DB change.
            await System.IO.File.WriteAllBytesAsync(physicalPath, imageBytes);

            // Remember the previous file so we can remove it only AFTER the DB commit succeeds.
            var previousPath = user.ProfilePicturePath;

            user.ProfilePicture = imageBytes;
            user.ProfilePicturePath = relativePath;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                // Roll back the new physical file; the previous file is untouched in the DB.
                DeletePhysicalFileIfExists(webRootPath, relativePath);
                var errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                return ApiResponse<CurrentUserDto>.FailureResult($"Failed to save profile picture: {errors}");
            }

            // DB commit succeeded — only now remove the orphaned previous file.
            if (!string.IsNullOrEmpty(previousPath))
            {
                DeletePhysicalFileIfExists(webRootPath, previousPath);
            }

            var roles = await _userManager.GetRolesAsync(user);
            var employee = user.Employee ?? await _employeeRepository.GetByIdAsync(user.EmployeeId);
            return ApiResponse<CurrentUserDto>.SuccessResult(
                user.ToCurrentUserDto(roles, employee),
                $"Profile picture {verb} successfully.");
        }

        public async Task<ApiResponse<string?>> GetProfilePicturePathAsync(Guid userId)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<string?>.FailureResult("User not found.");
            }

            if (string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                return ApiResponse<string?>.FailureResult("This user has no profile picture.");
            }

            return ApiResponse<string?>.SuccessResult(user.ProfilePicturePath, "Profile picture path retrieved.");
        }

        public async Task<ApiResponse<bool>> DeleteProfilePictureAsync(Guid userId, string webRootPath)
        {
            var user = await _userRepository.GetByIdAsync(userId);
            if (user == null)
            {
                return ApiResponse<bool>.FailureResult("User not found.");
            }

            // Remove the physical file if it exists
            if (!string.IsNullOrEmpty(user.ProfilePicturePath))
            {
                DeletePhysicalFileIfExists(webRootPath, user.ProfilePicturePath);
            }

            user.ProfilePicture = null;
            user.ProfilePicturePath = null;

            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                var errors = string.Join(" | ", updateResult.Errors.Select(e => e.Description));
                return ApiResponse<bool>.FailureResult($"Failed to delete profile picture: {errors}");
            }

            return ApiResponse<bool>.SuccessResult(true, "Profile picture deleted successfully.");
        }

        private static string? ValidateImageFile(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                return "No file was uploaded.";
            }

            if (file.Length > MaxProfilePictureBytes)
            {
                return "Profile picture must be 5 MB or smaller.";
            }

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (string.IsNullOrEmpty(extension) || !AllowedImageExtensions.Contains(extension))
            {
                return "Only .jpg, .jpeg, .png and .webp images are allowed.";
            }

            var contentType = file.ContentType?.ToLowerInvariant();
            if (string.IsNullOrEmpty(contentType) || !contentType.StartsWith("image/"))
            {
                return "The uploaded file is not a valid image.";
            }

            return null;
        }

        private static async Task<byte[]> ReadFileBytesAsync(IFormFile file)
        {
            using var memoryStream = new MemoryStream();
            await file.CopyToAsync(memoryStream);
            return memoryStream.ToArray();
        }

        private static void DeletePhysicalFileIfExists(string webRootPath, string relativePath)
        {
            try
            {
                var fullPath = Path.Combine(webRootPath, relativePath);
                if (System.IO.File.Exists(fullPath))
                {
                    System.IO.File.Delete(fullPath);
                }
            }
            catch
            {
                // Deleting an orphaned file must never fail the request
            }
        }

        private async Task<AuthResponseDto> BuildAuthResponseAsync(User user)
        {
            var roles = await _userManager.GetRolesAsync(user);
            var employee = user.Employee ?? await _employeeRepository.GetByIdAsync(user.EmployeeId);
            var (token, expiration) = GenerateJwtToken(user, roles);
            var refreshToken = await CreateAndStoreRefreshTokenAsync(user);

            return new AuthResponseDto
            {
                Token = token,
                RefreshToken = refreshToken,
                Expiration = expiration,
                User = user.ToCurrentUserDto(roles, employee)
            };
        }

        private (string Token, DateTime Expiration) GenerateJwtToken(User user, IList<string> roles)
        {
            var jwtSection = _configuration.GetSection("JwtSettings");
            var keyValue = jwtSection["Key"] ?? "cbe";
            var issuer = jwtSection["Issuer"] ?? "CbeMeetingAttendance";
            var audiences = jwtSection.GetSection("Audiences").Get<string[]>()
                            ?? new[] { "CBE-Attendance-Web-App" };
            var expiryMinutes = int.TryParse(jwtSection["ExpiryMinutes"], out var mins) ? mins : 15;

            // Hash short keys (e.g. "cbe") so HMAC-SHA256 has a valid 256-bit key
            var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyValue));
            var signingKey = new SymmetricSecurityKey(keyBytes);
            var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);

            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName ?? string.Empty),
                new Claim(ClaimTypes.Email, user.Email ?? string.Empty),
                new Claim("EmployeeId", user.EmployeeId.ToString())
            };

            foreach (var role in roles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }

            foreach (var audience in audiences)
            {
                claims.Add(new Claim(JwtRegisteredClaimNames.Aud, audience));
            }

            var expiration = DateTime.UtcNow.AddMinutes(expiryMinutes);
            var token = new JwtSecurityToken(
                issuer: issuer,
                claims: claims,
                expires: expiration,
                signingCredentials: credentials);

            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            return (tokenString, expiration);
        }

        private async Task<string> CreateAndStoreRefreshTokenAsync(User user)
        {
            var refreshToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(64));
            await _userManager.RemoveAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName);
            await _userManager.SetAuthenticationTokenAsync(user, RefreshTokenProvider, RefreshTokenName, refreshToken);
            return refreshToken;
        }
    }
}
