using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateBankAccountDto
    {
        [Required, StringLength(120)]
        public string BankName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string AccountOwner { get; set; } = string.Empty;

        [Required]
        public Guid BranchId { get; set; }
    }

    public class UpdateBankAccountDto
    {
        [Required]
        public Guid Id { get; set; }

        [StringLength(120)]
        public string? BankName { get; set; }

        [StringLength(50)]
        public string? AccountNumber { get; set; }

        [StringLength(150)]
        public string? AccountOwner { get; set; }

        public Guid? BranchId { get; set; }
    }

    public class BankAccountDto
    {
        public Guid Id { get; set; }
        public string BankName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountOwner { get; set; } = string.Empty;
        public Guid? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsActive { get; set; }
        public int TransactionCount { get; set; }
        public decimal ReceivedAmount { get; set; }
    }

    public class BankAccountStatementDto
    {
        public BankAccountDto Account { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public decimal TotalReceived { get; set; }
        public int EntryCount { get; set; }
        public List<BankAccountStatementEntryDto> Entries { get; set; } = new();
    }

    public class BankAccountStatementEntryDto
    {
        public Guid Id { get; set; }
        public DateTime Date { get; set; }
        public string Source { get; set; } = string.Empty;
        public Guid? TransactionId { get; set; }
        public string? ProductName { get; set; }
        public string? ItemCode { get; set; }
        public string? CustomerName { get; set; }
        public decimal Amount { get; set; }
        public string? Remark { get; set; }
    }
}
