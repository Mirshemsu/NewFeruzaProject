using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

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

        // Summary fields
        public int TotalTransactions { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }

        // Bank transaction tracking
        public string? CashBankTransactionId { get; set; }
        public string? BankTransferTransactionId { get; set; }
    }

    public class CloseDailySalesDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime ClosingDate { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class ApproveDailyClosingDto
    {
        [Required]
        public Guid ClosingId { get; set; }

        public bool IsApproved { get; set; }

        [StringLength(100)]
        public string? CashBankTransactionId { get; set; }

        [StringLength(100)]
        public string? BankTransferTransactionId { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    public class TransferAmountDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime TransferDate { get; set; } // The date of the transfer (for DailyClosing lookup)

        [Required]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public TransferDirection Direction { get; set; }

        [StringLength(100)]
        public string? BankTransactionId { get; set; }

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

    public class DailyClosingSummaryDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime ClosingDate { get; set; }

        // Sales summary
        public decimal TotalSales { get; set; }
        public decimal TotalCash { get; set; }
        public decimal TotalBank { get; set; }
        public decimal TotalCredit { get; set; }

        // Bank references
        public string? CashBankTransactionId { get; set; }
        public string? BankTransferTransactionId { get; set; }
    }

    public enum TransferDirection
    {
        CashToBank = 0,
        BankToCash = 1
    }

    public class DailyClosingPreviewDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime ClosingDate { get; set; }

        // Summary
        public int TotalTransactions { get; set; }
        public decimal TotalSalesAmount { get; set; }

        // Breakdown by payment method
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }

        // Today's transfers (if any)
        public decimal TotalTransferredFromCash { get; set; }
        public decimal TotalTransferredFromBank { get; set; }

        // Current status
        public bool HasPendingClosing { get; set; }
        public bool IsClosed { get; set; }
        public DailyClosingStatus? CurrentStatus { get; set; }

        // Detailed transaction list
        public List<DailySalesItemDto> Transactions { get; set; } = new();

        // Transfer history for today
        public List<TransferSummaryDto> TodayTransfers { get; set; } = new();
    }

    public class TransferSummaryDto
    {
        public DateTime Timestamp { get; set; }
        public string Direction { get; set; }
        public decimal Amount { get; set; }
        public string? BankTransactionId { get; set; }
        public string? Remarks { get; set; }
    }

    // For branch selection in admin view
    public class BranchClosingSummaryDto
    {
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public DateTime Date { get; set; }

        // Closing status
        public bool IsClosed { get; set; }
        public DailyClosingStatus? Status { get; set; }
        public DateTime? ClosedAt { get; set; }
        public string? ClosedBy { get; set; }

        // Financial summary
        public decimal TotalSales { get; set; }
        public decimal TotalCash { get; set; }
        public decimal TotalBank { get; set; }
        public decimal TotalCredit { get; set; }

        // Banking info
        public string? CashBankTransactionId { get; set; }
        public string? BankTransferTransactionId { get; set; }
    }

    // For admin dashboard
    public class AllBranchesClosingDto
    {
        public DateTime Date { get; set; }
        public List<BranchClosingSummaryDto> Branches { get; set; } = new();

        // Overall totals
        public int TotalBranches { get; set; }
        public int ClosedBranches { get; set; }
        public int PendingBranches { get; set; }

        // Financial totals across all branches
        public decimal TotalSalesAllBranches { get; set; }
        public decimal TotalCashAllBranches { get; set; }
        public decimal TotalBankAllBranches { get; set; }
        public decimal TotalCreditAllBranches { get; set; }
    }

    // For date range selection
    public class DateRangeDto
    {
        [Required]
        public DateTime StartDate { get; set; }

        [Required]
        public DateTime EndDate { get; set; }

        public Guid? BranchId { get; set; } // Optional - null for all branches
    }
}