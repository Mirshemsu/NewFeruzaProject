using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateBankAccountDto
    {
        [Required, StringLength(100)]
        public string BankName { get; set; }
        [Required, StringLength(50)]
        public string AccountNumber { get; set; }
        [Required, StringLength(100)]
        public string AccountOwner { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class UpdateBankAccountDto
    {
        [Required]
        public Guid Id { get; set; }
        [StringLength(100)]
        public string? BankName { get; set; }
        [StringLength(50)]
        public string? AccountNumber { get; set; }
        [StringLength(100)]
        public string? AccountOwner { get; set; }
        public Guid? BranchId { get; set; }
    }

    public class BankAccountResponseDto
    {
        public Guid Id { get; set; }
        public string BankName { get; set; }
        public string AccountNumber { get; set; }
        public string AccountOwner { get; set; }
        public Guid? BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public int TransactionCount { get; set; }
    }
}