using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class RegisterRequest
    {

        public Guid? BranchId { get; set; } // Required for BranchUser, null for GlobalUser
        [Required, StringLength(100)]
        public string Username { get; set; }
        [Required, StringLength(100)]
        public string Password { get; set; }
        [StringLength(100)]
        public string? Name { get; set; }
        [StringLength(100)]
        public string? ContactInfo { get; set; }
        [Required]
        public Role Role { get; set; } // Manager, Sales, Finance
    }

    public class LoginRequest
    {
        [Required, StringLength(100)]
        public string Username { get; set; }
        [Required, StringLength(100)]
        public string Password { get; set; }
    }

    public class LoginResponse
    {
        [Required]
        public string Token { get; set; }
        [Required]
        public Guid UserId { get; set; }
        [Required]
        public Role Role { get; set; }
        public Guid? BranchId { get; set; } // Null for GlobalUser
    }

    public class UserResponseDto
    {
        [Required]
        public Guid Id { get; set; }
        [Required, StringLength(100)]
        public string Username { get; set; }
        [StringLength(100)]
        public string Name { get; set; }
        [StringLength(100)]
        public string ContactInfo { get; set; }
        [Required]
        public Role Role { get; set; }
        public Guid? BranchId { get; set; } // Null for GlobalUser
        [Required]
        public bool IsActive { get; set; }
    }

    public class ResetPasswordRequest
    {
        [Required]
        public Guid UserId { get; set; }
        [Required, StringLength(100)]
        public string NewPassword { get; set; }
        [StringLength(100)]
        public string? CurrentPassword { get; set; } // Required for self-reset
    }
    public class UserProfileDto
    {
        public Guid Id { get; set; }
        public string? UserName { get; set; }
        public string? Name { get; set; }
        public string? ContactInfo { get; set; }
        public Role Role { get; set; }
        public Guid? BranchId { get; set; }
        public string? Email { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class UpdateProfileRequest
    {
        public string? Name { get; set; }
        public string? ContactInfo { get; set; }
        public string? PhoneNumber { get; set; }
    }

    public class ChangePasswordRequest
    {
        public string CurrentPassword { get; set; }
        public string NewPassword { get; set; }
        public string ConfirmNewPassword { get; set; }
    }

    public class DeactivateUserRequest
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
