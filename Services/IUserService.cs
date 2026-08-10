using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CbeMeetingAttendance.DTOs;
using Microsoft.AspNetCore.Http;

namespace CbeMeetingAttendance.Services
{
    public interface IUserService
    {
        Task<ApiResponse<AuthResponseDto>> RegisterUserAsync(RegisterUserDto dto);
        Task<ApiResponse<IEnumerable<UserDto>>> GetAllUsersAsync();
        Task<ApiResponse<UserDto>> GetUserByIdAsync(Guid id);
        Task<ApiResponse<AuthResponseDto>> LoginAsync(LoginDto dto);
        Task<ApiResponse<bool>> LogoutAsync(Guid userId);
        Task<ApiResponse<AuthResponseDto>> RefreshAsync(RefreshTokenDto dto);
        Task<ApiResponse<bool>> AssignRoleAsync(AssignRoleDto dto);
        Task<ApiResponse<bool>> ChangeRoleAsync(ChangeRoleDto dto);
        Task<ApiResponse<bool>> RemoveRoleAsync(RemoveRoleDto dto);
        Task<ApiResponse<bool>> ChangePasswordAsync(ChangePasswordDto dto);
        Task<ApiResponse<bool>> ResetPasswordAsync(ResetPasswordDto dto);
        Task<ApiResponse<AuthResponseDto>> GetCurrentUserAsync(Guid userId);
        Task<ApiResponse<bool>> DeleteUserAsync(Guid id, string? webRootPath = null);

        // Profile picture endpoints (self or Admin)
        Task<ApiResponse<CurrentUserDto>> UploadProfilePictureAsync(Guid userId, IFormFile file, string webRootPath);
        Task<ApiResponse<CurrentUserDto>> UpdateProfilePictureAsync(Guid userId, IFormFile file, string webRootPath);
        Task<ApiResponse<string?>> GetProfilePicturePathAsync(Guid userId);
        Task<ApiResponse<bool>> DeleteProfilePictureAsync(Guid userId, string webRootPath);
    }
}
