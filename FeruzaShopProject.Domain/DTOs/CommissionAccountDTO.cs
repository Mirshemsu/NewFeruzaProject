using System.ComponentModel.DataAnnotations;
using FeruzaShopProject.Domain.Entities;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateCommissionAccountDto
    {
        [Required]
        public Guid BranchId { get; set; }
    }

    public class CreateCommissionAllocationDto
    {
        [Required]
        public Guid CommissionAccountId { get; set; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public DateTime EntryDate { get; set; }

        [StringLength(50)]
        public string? CheckNumber { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class UpdateCommissionAllocationDto
    {
        [Required]
        public Guid Id { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? Amount { get; set; }

        public DateTime? EntryDate { get; set; }

        [StringLength(50)]
        public string? CheckNumber { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }
    }

    public class CommissionAccountDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public decimal AllocatedAmount { get; set; }
        public decimal UsedAmount { get; set; }
        public decimal RemainingAmount { get; set; }
        public int AllocationCount { get; set; }
        public int DeductionCount { get; set; }
    }

    public class CommissionLedgerEntryDto
    {
        public Guid Id { get; set; }
        public Guid CommissionAccountId { get; set; }
        public CommissionLedgerEntryType EntryType { get; set; }
        public string EntryTypeName { get; set; } = string.Empty;
        public decimal SignedAmount { get; set; }
        public decimal AbsoluteAmount => Math.Abs(SignedAmount);
        public DateTime EntryDate { get; set; }
        public string? CheckNumber { get; set; }
        public string? Note { get; set; }
        public Guid? TransactionId { get; set; }
        public string? ProductName { get; set; }
        public string? ItemCode { get; set; }
        public Guid? CreatedByUserId { get; set; }
        public string? CreatedByName { get; set; }
        public bool CanEditOrDelete { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CommissionAccountDetailDto
    {
        public CommissionAccountDto Account { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public List<CommissionLedgerEntryDto> Entries { get; set; } = new();
    }
}
