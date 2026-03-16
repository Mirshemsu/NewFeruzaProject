using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IAuthService
    {
        Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request);
        Task<ApiResponse<string>> RegisterAsync(RegisterRequest request);
        Task<ApiResponse<string>> LogoutAsync();
        Task<ApiResponse<string>> DeactivateUserAsync(DeactivateUserRequest request, string currentUserId);
        Task<ApiResponse<List<UserResponseDto>>> ListUserAsync(string? role = null, Guid? branchId = null, string currentUserId = null);
        Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordRequest request, string currentUserId);

        // New Profile Methods
        Task<ApiResponse<UserProfileDto>> GetProfileAsync(string currentUserId);
        Task<ApiResponse<string>> UpdateProfileAsync(UpdateProfileRequest request, string currentUserId);
        Task<ApiResponse<string>> ChangePasswordAsync(ChangePasswordRequest request, string currentUserId);
    }
}
