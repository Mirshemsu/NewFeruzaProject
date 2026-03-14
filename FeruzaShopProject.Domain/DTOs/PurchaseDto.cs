using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.DTOs
{
    // ========== STEP 1: SALES CREATES PURCHASE ORDER ==========
    public class CreatePurchaseOrderDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item is required")]
        public List<CreatePurchaseItemDto> Items { get; set; }
    }

    public class CreatePurchaseItemDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue, ErrorMessage = "Quantity must be greater than 0")]
        public int Quantity { get; set; }
    }

    // STEP 2: Finance verifies and sets prices
    public class FinanceVerificationDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        // ========== INVOICE NUMBER ==========
        [Required]
        [StringLength(50)]
        public string InvoiceNumber { get; set; }

        [Required]
        [MinLength(1, ErrorMessage = "At least one item must be processed")]
        public List<FinanceVerificationItemDto> Items { get; set; }
    }

    public class FinanceVerificationItemDto
    {
        [Required]
        public Guid ItemId { get; set; }

        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Buying price must be greater than 0")]
        public decimal BuyingPrice { get; set; }

        public decimal? SellingPrice { get; set; } // Optional - if not provided, auto-calculated

        public string? SupplierName { get; set; }
    }

    // STEP 3: Manager approves entire purchase
    public class ManagerApprovalDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        public bool IsApproved { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }
    }

    // For editing by sales before finance verification
    public class EditPurchaseOrderBySalesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        public Guid? BranchId { get; set; }

        public List<EditPurchaseItemDto>? Items { get; set; }
    }

    public class EditPurchaseItemDto
    {
        public Guid? ItemId { get; set; } // Null for new items

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        [Range(1, int.MaxValue)]
        public int Quantity { get; set; }
    }

    // For finance to edit prices (before final verification)
    public class EditPurchaseOrderByFinanceDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        // Purchase-level edits
        public string? InvoiceNumber { get; set; }

        // Item-level edits
        public List<EditFinanceItemDto>? Items { get; set; }
    }

    public class EditFinanceItemDto
    {
        [Required]
        public Guid ItemId { get; set; }

        public string? SupplierName { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? BuyingPrice { get; set; }

        [Range(0.01, double.MaxValue)]
        public decimal? SellingPrice { get; set; }
    }

    // Rejection DTO
    public class RejectPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        [Required]
        public string Reason { get; set; }
    }

    // Cancellation DTO
    public class CancelPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        public string? Reason { get; set; }
    }

    // Response DTOs
    public class PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public Guid CreatedBy { get; set; }
        public string CreatedByName { get; set; }
        public PurchaseOrderStatus Status { get; set; }

        // ========== INVOICE NUMBER ==========
        public string? InvoiceNumber { get; set; }

        // ========== APPROVAL TRACKING ==========
        public DateTime? FinanceVerifiedAt { get; set; }
        public string? FinanceVerifiedBy { get; set; }
        public DateTime? ApprovedAt { get; set; }
        public string? ApprovedBy { get; set; }

        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PurchaseOrderItemDto> Items { get; set; }

        // Calculated properties
        public int TotalItems => Items?.Count ?? 0;
        public decimal TotalValue => Items?
            .Where(i => i.BuyingPrice.HasValue)
            .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0;
        public decimal TotalProfit => Items?
            .Where(i => i.BuyingPrice.HasValue && i.UnitPrice.HasValue)
            .Sum(i => (i.UnitPrice.Value - i.BuyingPrice.Value) * i.Quantity) ?? 0;
    }

    public class PurchaseOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int Quantity { get; set; }
        public decimal? BuyingPrice { get; set; }
        public decimal? UnitPrice { get; set; }
        public string? SupplierName { get; set; }
        public decimal? ProfitMargin { get; set; }
    }

    public class RejectResponseDto
    {
        public Guid PurchaseOrderId { get; set; }
        public PurchaseOrderStatus NewStatus { get; set; }
        public string Message { get; set; }
    }

    public class PurchaseOrderStatsDto
    {
        // Total counts
        public int TotalPurchaseOrders { get; set; }

        // Status breakdown
        public int PendingFinanceVerification { get; set; }
        public int PendingManagerApproval { get; set; }
        public int Approved { get; set; }
        public int Rejected { get; set; }
        public int Cancelled { get; set; }

        // Financial summaries
        public decimal TotalBuyingCost { get; set; }
        public decimal TotalSellingValue { get; set; }
        public decimal TotalProfit => TotalSellingValue - TotalBuyingCost;
        public decimal AverageProfitMargin { get; set; }

        // Item statistics
        public int TotalItemsOrdered { get; set; }

        // Time-based statistics
        public int OrdersThisMonth { get; set; }
        public int OrdersLastMonth { get; set; }
        public decimal MonthlyGrowthPercentage { get; set; }

        // Branch specific (if applicable)
        public Guid? BranchId { get; set; }
        public string BranchName { get; set; }
    }

    public class PurchaseOrderDashboardDto
    {
        // Summary cards
        public int TotalPending { get; set; } // PendingFinanceVerification + PendingManagerApproval
        public int TotalCompleted { get; set; } // Approved
        public int TotalRejected { get; set; } // Rejected
        public int TotalCancelled { get; set; } // Cancelled

        // Status breakdown
        public Dictionary<string, int> OrdersByStatus { get; set; } = new();

        // Branch breakdown
        public Dictionary<string, int> OrdersByBranch { get; set; } = new();
        public Dictionary<string, decimal> ValueByBranch { get; set; } = new();

        // Financial summary
        public decimal TotalPurchaseValue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfitMargin { get; set; }

        // Recent orders (for display)
        public List<RecentPurchaseOrderDto> RecentOrders { get; set; } = new();

        // Recent activity (for timeline)
        public List<RecentPurchaseHistoryDto> RecentActivity { get; set; } = new();

        // Monthly trends
        public List<MonthlyTrendDto> MonthlyTrends { get; set; } = new();

        // Top products
        public List<TopProductDto> TopProducts { get; set; } = new();

        // Top suppliers
        public List<TopSupplierDto> TopSuppliers { get; set; } = new();
    }

    public class RecentPurchaseOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; } // Formatted ID (e.g., first 8 chars)
        public string BranchName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
        public string CreatedByName { get; set; }
    }

    public class RecentPurchaseHistoryDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string OrderNumber { get; set; }
        public string Action { get; set; }
        public string PerformedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Details { get; set; }
    }

    public class MonthlyTrendDto
    {
        public string Month { get; set; } // e.g., "Jan 2024"
        public int OrderCount { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public class TopProductDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantityOrdered { get; set; }
        public decimal TotalValue { get; set; }
        public decimal TotalProfit { get; set; }
    }

    public class TopSupplierDto
    {
        public string SupplierName { get; set; }
        public int OrderCount { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
    }
}