using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class DailyClosingDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime ClosingDate { get; set; }
        public DateTime ClosedAt { get; set; }
        public string ClosedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }
        public string Status { get; set; }
        public string Remarks { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public int TotalTransactions { get; set; }
    }

    public class CloseDailySalesDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime ClosingDate { get; set; } // Should be today or yesterday max

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class ApproveDailyClosingDto
    {
        [Required]
        public Guid ClosingId { get; set; }

        public bool IsApproved { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class ReopenDailySalesDto
    {
        [Required]
        public Guid ClosingId { get; set; }

        [Required]
        public string Reason { get; set; }
    }
}
