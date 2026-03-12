using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class DailyClosing : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime ClosingDate { get; set; } // The date being closed

        [Required]
        public DateTime ClosedAt { get; set; } // When it was closed

        [Required]
        public Guid ClosedBy { get; set; } // User who closed

        public DateTime? ApprovedAt { get; set; } // When finance approved
        public Guid? ApprovedBy { get; set; } // Finance user who approved

        [Required]
        public DailyClosingStatus Status { get; set; } = DailyClosingStatus.Pending;

        [StringLength(500)]
        public string? Remarks { get; set; }

        // Summary fields
        public int TotalTransactions { get; set; }
        public decimal TotalSalesAmount { get; set; }

        // ========== DYNAMIC AMOUNT COLUMNS ==========
        public decimal TotalCashAmount { get; set; }  // Current cash amount (updated by transactions and transfers)
        public decimal TotalBankAmount { get; set; }  // Current bank amount (updated by transactions and transfers)
        public decimal TotalCreditAmount { get; set; } // Credit amount (static)

        // Bank transaction tracking (for audit)
        [StringLength(100)]
        public string? CashBankTransactionId { get; set; } // When cash is deposited to bank

        [StringLength(100)]
        public string? BankTransferTransactionId { get; set; } // When bank transfers occur

        // Navigation properties
        public Branch Branch { get; set; }
        public User Closer { get; set; }
        public User Approver { get; set; }
    }

  
}