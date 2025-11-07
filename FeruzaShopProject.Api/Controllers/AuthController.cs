using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using System.Security.Claims;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService ?? throw new ArgumentNullException(nameof(authService));
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var response = await _authService.LoginAsync(request);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var response = await _authService.RegisterAsync(request);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost("logout")]
        [Authorize]
        public async Task<IActionResult> Logout()
        {
            var response = await _authService.LogoutAsync();
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost("forget")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<IActionResult> Forget([FromBody] ForgetRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(ApiResponse<string>.Fail("Current user ID not found in token"));
            }
            var response = await _authService.ForgetAsync(request, currentUserId);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpGet("users")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<IActionResult> ListUsers([FromQuery] string? role = null, [FromQuery] Guid? branchId = null)
        {
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(ApiResponse<List<UserResponseDto>>.Fail("Current user ID not found in token"));
            }
            var response = await _authService.ListUserAsync(role, branchId, currentUserId);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPost("reset-password")]
        [Authorize]
        public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var currentUserId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(currentUserId))
            {
                return Unauthorized(ApiResponse<string>.Fail("Current user ID not found in token"));
            }
            var response = await _authService.ResetPasswordAsync(request, currentUserId);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
    }
}
