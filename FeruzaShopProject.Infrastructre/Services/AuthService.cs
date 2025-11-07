using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using System;
using System.Collections.Generic;
using System.IdentityModel.Tokens.Jwt;
using System.Linq;
using System.Security.Claims;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class AuthService : IAuthService
    {
        private readonly UserManager<User> _userManager;
        private readonly SignInManager<User> _signInManager;
        private readonly RoleManager<IdentityRole<Guid>> _roleManager;
        private readonly IConfiguration _configuration;
        private readonly ILogger<AuthService> _logger;

        public AuthService(
            UserManager<User> userManager,
            SignInManager<User> signInManager,
            RoleManager<IdentityRole<Guid>> roleManager,
            IConfiguration configuration,
            ILogger<AuthService> logger)
        {
            _userManager = userManager ?? throw new ArgumentNullException(nameof(userManager));
            _signInManager = signInManager ?? throw new ArgumentNullException(nameof(signInManager));
            _roleManager = roleManager ?? throw new ArgumentNullException(nameof(roleManager));
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        public async Task<ApiResponse<LoginResponse>> LoginAsync(LoginRequest request)
        {
            _logger.LogInformation($"Attempting login for user: {request.Username}");
            var user = await _userManager.FindByNameAsync(request.Username);
            if (user == null)
            {
                _logger.LogWarning($"User {request.Username} not found or inactive");
                return ApiResponse<LoginResponse>.Fail("Invalid username or user is inactive");
            }
            var result = await _signInManager.CheckPasswordSignInAsync(user, request.Password, false);
            if (!result.Succeeded)
            {
                _logger.LogWarning($"Invalid password for user {request.Username}");
                return ApiResponse<LoginResponse>.Fail("Invalid password");
            }
            var token = await GenerateJwtToken(user);
            var response = new LoginResponse
            {
                Token = token,
                UserId = user.Id,
                Role = user.Role,
                BranchId = user is BranchUser branchUser ? branchUser.BranchId : null
            };
            _logger.LogInformation($"Login successful for user {request.Username}");
            return ApiResponse<LoginResponse>.Success(response, "Login successful");
        }

        public async Task<ApiResponse<string>> RegisterAsync(RegisterRequest request)
        {
            _logger.LogInformation($"Registering user: {request.Username}, Role: {request.Role}");
            if (!Enum.IsDefined(typeof(Role), request.Role))
            {
                _logger.LogWarning($"Invalid role: {request.Role}");
                return ApiResponse<string>.Fail("Invalid role");
            }
            if (request.Role == Role.Sales && !request.BranchId.HasValue)
            {
                _logger.LogWarning("BranchId is required for Sales role");
                return ApiResponse<string>.Fail("BranchId is required for Sales role");
            }
            if (request.Role != Role.Sales && request.BranchId.HasValue)
            {
                _logger.LogWarning("BranchId must be null for Manager or Finance roles");
                return ApiResponse<string>.Fail("BranchId must be null for Manager or Finance roles");
            }
            User user = request.Role == Role.Sales
                ? new BranchUser
                {
                    BranchId = request.BranchId.Value,
                    UserName = request.Username,
                    Name = request.Name,
                    ContactInfo = request.ContactInfo,
                    Role = request.Role,
                }
                : new GlobalUser
                {
                    UserName = request.Username,
                    Name = request.Name,
                    ContactInfo = request.ContactInfo,
                    Role = request.Role,
                };
            var createResult = await _userManager.CreateAsync(user, request.Password);
            if (!createResult.Succeeded)
            {
                _logger.LogWarning($"User creation failed for {request.Username}: {string.Join(", ", createResult.Errors.Select(e => e.Description))}");
                return ApiResponse<string>.Fail(createResult.Errors.Select(e => e.Description));
            }
            try
            {
                var roleName = request.Role.ToString();
                if (!await _roleManager.RoleExistsAsync(roleName))
                {
                    await _roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                    _logger.LogInformation($"Created role: {roleName}");
                }
                var addToRoleResult = await _userManager.AddToRoleAsync(user, roleName);
                if (!addToRoleResult.Succeeded)
                {
                    await _userManager.DeleteAsync(user);
                    _logger.LogWarning($"Failed to add role {roleName} to user {request.Username}: {string.Join(", ", addToRoleResult.Errors.Select(e => e.Description))}");
                    return ApiResponse<string>.Fail(addToRoleResult.Errors.Select(e => e.Description));
                }
                _logger.LogInformation($"User {request.Username} registered successfully with role {roleName}");
                return ApiResponse<string>.Success(user.Id.ToString(), "User registered successfully");
            }
            catch (Exception ex)
            {
                await _userManager.DeleteAsync(user);
                _logger.LogError(ex, $"Registration failed for {request.Username}");
                return ApiResponse<string>.Fail($"Registration failed: {ex.Message}");
            }
        }

        public async Task<ApiResponse<string>> LogoutAsync()
        {
            _logger.LogInformation("User logout requested");
            await _signInManager.SignOutAsync();
            _logger.LogInformation("User logged out successfully");
            return ApiResponse<string>.Success(null, "Logout successful");
        }

        public async Task<ApiResponse<string>> ForgetAsync(Guid userId, string currentUserId)
        {
            _logger.LogInformation($"Attempting to deactivate user {userId} by {currentUserId}");
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
            {
                _logger.LogWarning($"Current user {currentUserId} not found or inactive");
                return ApiResponse<string>.Fail("Current user not found or inactive");
            }
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            if (!currentUserRoles.Contains(Role.Manager.ToString()) && !currentUserRoles.Contains(Role.Finance.ToString()))
            {
                _logger.LogWarning($"User {currentUserId} lacks permission to deactivate users");
                return ApiResponse<string>.Fail("Only Manager or Finance roles can deactivate users");
            }
            var user = await _userManager.FindByIdAsync(userId.ToString());
            if (user == null)
            {
                _logger.LogWarning($"User {userId} not found or already inactive");
                return ApiResponse<string>.Fail("User not found or already inactive");
            }
            if (user.Id == Guid.Parse(currentUserId))
            {
                _logger.LogWarning($"User {currentUserId} cannot deactivate themselves");
                return ApiResponse<string>.Fail("Cannot deactivate yourself");
            }
            var activeManagers = await _userManager.GetUsersInRoleAsync(Role.Manager.ToString());
            var activeFinance = await _userManager.GetUsersInRoleAsync(Role.Finance.ToString());
            if ((user.Role == Role.Manager && activeManagers.Count() <= 1) ||
                (user.Role == Role.Finance && activeFinance.Count() <= 1))
            {
                _logger.LogWarning($"Cannot deactivate user {userId}: Last active {user.Role}");
                return ApiResponse<string>.Fail($"Cannot deactivate the last active {user.Role}");
            }
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning($"Failed to deactivate user {userId}: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                return ApiResponse<string>.Fail(updateResult.Errors.Select(e => e.Description));
            }
            _logger.LogInformation($"User {userId} deactivated successfully by {currentUserId}");
            return ApiResponse<string>.Success(userId.ToString(), "User deactivated successfully");
        }

        public async Task<ApiResponse<List<UserResponseDto>>> ListUserAsync(string? role = null, Guid? branchId = null, string currentUserId = null)
        {
            _logger.LogInformation($"Listing users requested by {currentUserId}, Role filter: {role}, BranchId filter: {branchId}");
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
            {
                _logger.LogWarning($"Current user {currentUserId} not found or inactive");
                return ApiResponse<List<UserResponseDto>>.Fail("Current user not found or inactive");
            }
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            if (!currentUserRoles.Contains(Role.Manager.ToString()) && !currentUserRoles.Contains(Role.Finance.ToString()))
            {
                _logger.LogWarning($"User {currentUserId} lacks permission to list users");
                return ApiResponse<List<UserResponseDto>>.Fail("Only Manager or Finance roles can list users");
            }
            var query = _userManager.Users.AsQueryable();
            if (!string.IsNullOrEmpty(role) && Enum.TryParse<Role>(role, true, out var parsedRole))
            {
                query = query.Where(u => u.Role == parsedRole);
            }
            if (branchId.HasValue)
            {
                query = query.OfType<BranchUser>()
                             .Where(bu => bu.BranchId == branchId.Value);
            }

            var users = await query.ToListAsync();
            var userDtos = users.Select(u => new UserResponseDto
            {
                Id = u.Id,
                Username = u.UserName,
                Name = u.Name,
                ContactInfo = u.ContactInfo,
                Role = u.Role,
                BranchId = u is BranchUser bu ? bu.BranchId : null,
            }).ToList();
            _logger.LogInformation($"Listed {userDtos.Count} users for {currentUserId}");
            return ApiResponse<List<UserResponseDto>>.Success(userDtos, "Users retrieved successfully");
        }

        public async Task<ApiResponse<string>> ResetPasswordAsync(ResetPasswordRequest request, string currentUserId)
        {
            _logger.LogInformation($"Password reset requested for user {request.UserId} by {currentUserId}");
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
            {
                _logger.LogWarning($"Current user {currentUserId} not found or inactive");
                return ApiResponse<string>.Fail("Current user not found or inactive");
            }
            var targetUser = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (targetUser == null)
            {
                _logger.LogWarning($"Target user {request.UserId} not found or inactive");
                return ApiResponse<string>.Fail("Target user not found or inactive");
            }
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            bool isSelfReset = currentUser.Id == request.UserId;
            if (!isSelfReset && !currentUserRoles.Contains(Role.Manager.ToString()) && !currentUserRoles.Contains(Role.Finance.ToString()))
            {
                _logger.LogWarning($"User {currentUserId} lacks permission to reset password for {request.UserId}");
                return ApiResponse<string>.Fail("Only Manager or Finance roles can reset passwords for other users");
            }
            if (isSelfReset && string.IsNullOrEmpty(request.CurrentPassword))
            {
                _logger.LogWarning($"Current password required for self-reset by {currentUserId}");
                return ApiResponse<string>.Fail("Current password is required for self-reset");
            }
            if (isSelfReset)
            {
                var checkPassword = await _userManager.CheckPasswordAsync(currentUser, request.CurrentPassword);
                if (!checkPassword)
                {
                    _logger.LogWarning($"Invalid current password for user {currentUserId}");
                    return ApiResponse<string>.Fail("Invalid current password");
                }
            }
            var token = await _userManager.GeneratePasswordResetTokenAsync(targetUser);
            var resetResult = await _userManager.ResetPasswordAsync(targetUser, token, request.NewPassword);
            if (!resetResult.Succeeded)
            {
                _logger.LogWarning($"Password reset failed for user {request.UserId}: {string.Join(", ", resetResult.Errors.Select(e => e.Description))}");
                return ApiResponse<string>.Fail(resetResult.Errors.Select(e => e.Description));
            }
            _logger.LogInformation($"Password reset successful for user {request.UserId} by {currentUserId}");
            return ApiResponse<string>.Success(targetUser.Id.ToString(), "Password reset successfully");
        }
        public async Task<ApiResponse<string>> ForgetAsync(ForgetRequest request, string currentUserId)
        {
            _logger.LogInformation($"Attempting to deactivate user {request.UserId} by {currentUserId}");
            var currentUser = await _userManager.FindByIdAsync(currentUserId);
            if (currentUser == null)
            {
                _logger.LogWarning($"Current user {currentUserId} not found or inactive");
                return ApiResponse<string>.Fail("Current user not found or inactive");
            }
            var currentUserRoles = await _userManager.GetRolesAsync(currentUser);
            if (!currentUserRoles.Contains(Role.Manager.ToString()) && !currentUserRoles.Contains(Role.Finance.ToString()))
            {
                _logger.LogWarning($"User {currentUserId} lacks permission to deactivate users");
                return ApiResponse<string>.Fail("Only Manager or Finance roles can deactivate users");
            }
            var user = await _userManager.FindByIdAsync(request.UserId.ToString());
            if (user == null)
            {
                _logger.LogWarning($"User {request.UserId} not found or already inactive");
                return ApiResponse<string>.Fail("User not found or already inactive");
            }
            if (user.Id == Guid.Parse(currentUserId))
            {
                _logger.LogWarning($"User {currentUserId} cannot deactivate themselves");
                return ApiResponse<string>.Fail("Cannot deactivate yourself");
            }
            var activeManagers = await _userManager.GetUsersInRoleAsync(Role.Manager.ToString());
            var activeFinance = await _userManager.GetUsersInRoleAsync(Role.Finance.ToString());
            if ((user.Role == Role.Manager && activeManagers.Count() <= 1) ||
                (user.Role == Role.Finance && activeFinance.Count() <= 1))
            {
                _logger.LogWarning($"Cannot deactivate user {request.UserId}: Last active {user.Role}");
                return ApiResponse<string>.Fail($"Cannot deactivate the last active {user.Role}");
            }
            var updateResult = await _userManager.UpdateAsync(user);
            if (!updateResult.Succeeded)
            {
                _logger.LogWarning($"Failed to deactivate user {request.UserId}: {string.Join(", ", updateResult.Errors.Select(e => e.Description))}");
                return ApiResponse<string>.Fail(updateResult.Errors.Select(e => e.Description));
            }
            _logger.LogInformation($"User {request.UserId} deactivated successfully by {currentUserId}");
            return ApiResponse<string>.Success(request.UserId.ToString(), "User deactivated successfully");
        }
        private async Task<string> GenerateJwtToken(User user)
        {
            _logger.LogInformation($"Generating JWT token for user: {user.UserName}");
            var userRoles = await _userManager.GetRolesAsync(user);
            var claims = new List<Claim>
            {
                new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
                new Claim(JwtRegisteredClaimNames.Name, user.UserName),
                new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.UserName)
            };
            foreach (var role in userRoles)
            {
                claims.Add(new Claim(ClaimTypes.Role, role));
            }
            claims.Add(new Claim("Role", user.Role.ToString()));
            if (user is BranchUser branchUser)
            {
                claims.Add(new Claim("BranchId", branchUser.BranchId.ToString()));
            }
            _logger.LogInformation($"Claims for {user.UserName}: {string.Join(", ", claims.Select(c => $"{c.Type}: {c.Value}"))}");
            var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                _configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT Key is not configured")));
            var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
            var token = new JwtSecurityToken(
                issuer: _configuration["Jwt:Issuer"],
                audience: _configuration["Jwt:Audience"],
                claims: claims,
                expires: DateTime.UtcNow.AddHours(Convert.ToDouble(_configuration["Jwt:ExpireHours"])),
                signingCredentials: creds);
            var tokenString = new JwtSecurityTokenHandler().WriteToken(token);
            _logger.LogInformation($"Generated JWT token for {user.UserName}");
            return tokenString;
        }
    }
}