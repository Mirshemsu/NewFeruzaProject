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
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public int TotalTransactions { get; set; }

        // Navigation properties
        public Branch Branch { get; set; }
        public User Closer { get; set; }
        public User Approver { get; set; }
    }
}
