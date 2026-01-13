using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateTransactionDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        // Existing customer/painter by ID
        public Guid? CustomerId { get; set; }
        public Guid? PainterId { get; set; }

        // New customer/painter details (create if not exists)
        public string? CustomerName { get; set; }
        public string? CustomerPhoneNumber { get; set; }

        public string? PainterName { get; set; }
        public string? PainterPhoneNumber { get; set; }

        [Required]
        public string ItemCode { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required, Range(0.001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public decimal CommissionRate { get; set; }
    }

    public class UpdateTransactionDto
    {
        [Required]
        public Guid Id { get; set; }

        public Guid? CustomerId { get; set; }
        public Guid? PainterId { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        [Range(0.001, double.MaxValue)]
        public decimal? Quantity { get; set; }

        public PaymentMethod? PaymentMethod { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? CommissionRate { get; set; }

        public bool? CommissionPaid { get; set; }
    }

    public class PayCreditDto
    {
        [Required]
        public Guid TransactionId { get; set; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;
    }
    public class TransactionResponseDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public Guid ProductId { get; set; }
        public Guid? CustomerId { get; set; }
        public Guid? PainterId { get; set; }

        public DateTime TransactionDate { get; set; }
        public string ItemCode { get; set; }
        public decimal UnitPrice { get; set; } // This comes from Transaction
        public decimal Quantity { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public decimal CommissionRate { get; set; }
        public bool CommissionPaid { get; set; }

        // From related entities
        public string BranchName { get; set; }
        public string ProductName { get; set; }
        public string CategoryName { get; set; }
        public string UnitType { get; set; } // This comes from Product.Unit
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public string PainterName { get; set; }
        public string PainterPhoneNumber { get; set; }

        // Calculated properties
        public decimal TotalAmount => UnitPrice * Quantity;
        public decimal CommissionAmount => Quantity * CommissionRate;
        public bool IsCredit => PaymentMethod == PaymentMethod.Credit;
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => IsCredit ? TotalAmount - PaidAmount : 0;
        public bool IsFullyPaid => IsCredit && RemainingAmount <= 0;
    }
    public class CreditTransactionHistoryDto
    {
        public Guid TransactionId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string ItemCode { get; set; }
        public string ProductName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal RemainingAmount => TotalAmount - PaidAmount;
        public bool IsFullyPaid => RemainingAmount <= 0;
        public DateTime? LastPaymentDate { get; set; }

        // Customer info
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }

        // Branch info
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
    }
    public class DailySalesReportDto
    {
        public DateTime ReportDate { get; set; }
        public Guid? BranchId { get; set; }
        public string BranchName { get; set; }
        public string PaymentMethod { get; set; }
        public Guid? BankAccountId { get; set; }
        public string BankAccountName { get; set; }

        public int TotalTransactions { get; set; }
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public decimal TotalCommissionAmount { get; set; }
        public decimal TotalPaidCommission { get; set; }
        public decimal TotalPendingCommission { get; set; }

        public List<DailySalesItemDto> SalesItems { get; set; } = new();
        public List<PaymentSummaryDto> PaymentSummaries { get; set; } = new();
    }

    public class DailySalesItemDto
    {
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public string CategoryName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public PaymentMethod PaymentMethod { get; set; }
        public string CustomerName { get; set; }
        public DateTime TransactionDate { get; set; }
    }

    public class PaymentSummaryDto
    {
        public PaymentMethod PaymentMethod { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal Percentage { get; set; }
    }
    public class CreditSummaryDto
    {
        public Guid? CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }

        public int TotalCreditTransactions { get; set; }
        public int PendingCreditTransactions { get; set; }
        public int CompletedCreditTransactions { get; set; }

        public decimal TotalCreditAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalRemainingAmount => TotalCreditAmount - TotalPaidAmount;

        public decimal AverageCreditPerTransaction => TotalCreditTransactions > 0 ? TotalCreditAmount / TotalCreditTransactions : 0;

        public List<CreditCustomerSummaryDto> CustomerSummaries { get; set; } = new();
        public List<CreditTransactionHistoryDto> RecentTransactions { get; set; } = new();
    }
    public class TransactionSummaryDto
    {
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public Guid? BranchId { get; set; }
        public string? BranchName { get; set; }
        public string? PaymentMethod { get; set; }

        // Counts
        public int TotalTransactions { get; set; }
        public int CashTransactions { get; set; }
        public int BankTransactions { get; set; }
        public int CreditTransactions { get; set; }

        // Amounts
        public decimal TotalSalesAmount { get; set; }
        public decimal TotalCashAmount { get; set; }
        public decimal TotalBankAmount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public decimal TotalPaidCreditAmount { get; set; }
        public decimal TotalPendingCreditAmount { get; set; }

        // Commission
        public decimal TotalCommissionAmount { get; set; }
        public decimal TotalPaidCommission { get; set; }
        public decimal TotalPendingCommission { get; set; }

        // Quantities
        public decimal TotalQuantitySold { get; set; }

        // Averages
        public decimal AverageTransactionAmount { get; set; }
        public decimal AverageDailySales { get; set; }

        // Date range info
        public int DaysInPeriod { get; set; }

        // Top products/customers
        public List<TransactionProductSummaryDto> TopProducts { get; set; } = new();
        public List<TransactionCustomerSummaryDto> TopCustomers { get; set; } = new();
        public List<TransactionPainterSummaryDto> TopPainters { get; set; } = new();

        // Recent transactions
        public List<TransactionResponseDto> RecentTransactions { get; set; } = new();
    }
    public class TransactionProductSummaryDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalQuantity { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PercentageOfTotal { get; set; }
    }

    public class TransactionCustomerSummaryDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public decimal PaidCreditAmount { get; set; }
        public decimal PendingCreditAmount { get; set; }
    }

    public class TransactionPainterSummaryDto
    {
        public Guid PainterId { get; set; }
        public string PainterName { get; set; }
        public string PainterPhoneNumber { get; set; }
        public int TransactionCount { get; set; }
        public decimal TotalCommissionAmount { get; set; }
        public decimal PaidCommissionAmount { get; set; }
        public decimal PendingCommissionAmount { get; set; }
    }
    public class CreditCustomerSummaryDto
    {
        public Guid CustomerId { get; set; }
        public string CustomerName { get; set; }
        public string CustomerPhoneNumber { get; set; }
        public int CreditCount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal RemainingAmount => TotalCreditAmount - TotalPaidAmount;
        public DateTime LastCreditDate { get; set; }
    }
}
