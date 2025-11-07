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

    public class ForgetRequest
    {
        [Required]
        public Guid UserId { get; set; }
    }
}
