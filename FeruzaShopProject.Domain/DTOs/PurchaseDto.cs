using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.DTOs
{
    // ========== STEP 1: SALES CREATES PURCHASE ORDER ==========
    public record CreatePurchaseOrderDto
    {
        [Required]
        public Guid BranchId { get; init; }

        [Required, MinLength(1)]
        public List<CreatePurchaseOrderItemDto> Items { get; init; }
    }

    public record CreatePurchaseOrderItemDto
    {
        [Required]
        public Guid ProductId { get; init; }

        [Required, Range(1, int.MaxValue)]
        public int QuantityRequested { get; init; }
    }

    // ========== STEP 2: ADMIN ACCEPTS QUANTITIES ==========
    public record AcceptPurchaseQuantitiesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<AcceptQuantityItemDto> Items { get; init; }
    }

    public record AcceptQuantityItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityAccepted { get; init; }
    }

    // ========== STEP 3: SALES REGISTERS RECEIVED QUANTITIES (MULTIPLE TIMES) ==========
    public record RegisterReceivedQuantitiesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<RegisterQuantityItemDto> Items { get; init; }

        public string? Notes { get; init; }
    }

    public record RegisterQuantityItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(1, int.MaxValue)]
        public int QuantityRegistered { get; init; }
    }

    // ========== STEP 4: FINANCE VERIFICATION (PARTIAL SUPPORTED) ==========
    public record FinanceVerificationDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        public Guid? SupplierId { get; init; }

        [Required, MinLength(1)]
        public List<FinanceVerificationItemDto> Items { get; init; }
    }

    public record FinanceVerificationItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal BuyingPrice { get; init; }  // Now required for verified items

        [Range(0.01, double.MaxValue)]
        public decimal? SellingPrice { get; init; } // Optional - will auto-calculate with markup

        public bool IsVerified { get; init; } = true; // Mark as verified
    }

    // ========== STEP 5: ADMIN FINAL APPROVAL (PARTIAL SUPPORTED) ==========
    public record FinalApprovePurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        // If null, approve all verified items. If provided, approve specific items
        public List<Guid>? ItemIds { get; init; }

        public string? Notes { get; init; }
    }

    // ========== NEW: SALES EDIT OPERATIONS ==========

    /// <summary>
    /// Sales can edit their purchase order only when status is PendingAdminAcceptance
    /// </summary>
    public record EditPurchaseOrderBySalesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        public Guid? BranchId { get; init; } // Optional: change branch

        [Required, MinLength(1)]
        public List<EditPurchaseOrderItemBySalesDto> Items { get; init; }
    }

    public record EditPurchaseOrderItemBySalesDto
    {
        public Guid? ItemId { get; init; } // Null for new items

        [Required]
        public Guid ProductId { get; init; }

        [Required, Range(1, int.MaxValue)]
        public int QuantityRequested { get; init; }
    }

    /// <summary>
    /// Sales can delete their own purchase order (only in PendingAdminAcceptance)
    /// </summary>
    public record DeletePurchaseOrderBySalesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        public string? Reason { get; init; }
    }

    /// <summary>
    /// Sales can edit registered quantities before finance verification
    /// </summary>
    public record EditRegisteredQuantitiesBySalesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<EditRegisteredQuantityItemDto> Items { get; init; }

        [Required]
        public string Reason { get; init; } // Why editing?
    }

    public record EditRegisteredQuantityItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityRegistered { get; init; }
    }

    // ========== NEW: ADMIN EDIT OPERATIONS ==========

    /// <summary>
    /// Admin can edit any purchase order at any stage (except FullyApproved)
    /// </summary>
    public record EditPurchaseOrderByAdminDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        public Guid? BranchId { get; init; }

        public Guid? SupplierId { get; init; }

        public List<AdminEditPurchaseOrderItemDto>? Items { get; init; }

        public List<Guid>? ItemIdsToRemove { get; init; }

        [Required]
        public string Reason { get; init; } // Why editing?
    }

    public record AdminEditPurchaseOrderItemDto
    {
        public Guid? ItemId { get; init; } // Null for new items

        public Guid? ProductId { get; init; } // Required for new items

        public int? QuantityRequested { get; init; }

        public int? QuantityAccepted { get; init; }

        public int? QuantityRegistered { get; init; }

        public decimal? BuyingPrice { get; init; }

        public decimal? SellingPrice { get; init; }

        public bool? FinanceVerified { get; init; }
    }

    /// <summary>
    /// Admin can edit only accepted quantities
    /// </summary>
    public record EditAcceptedQuantitiesByAdminDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<EditAcceptedQuantityItemDto> Items { get; init; }

        [Required]
        public string Reason { get; init; }
    }

    public record EditAcceptedQuantityItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityAccepted { get; init; }
    }

    /// <summary>
    /// Admin can edit only registered quantities
    /// </summary>
    public record EditRegisteredQuantitiesByAdminDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<EditRegisteredQuantityItemDto> Items { get; init; }

        [Required]
        public string Reason { get; init; }
    }

    /// <summary>
    /// Admin can edit prices at any time before final approval
    /// </summary>
    public record EditPricesByAdminDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<EditPriceItemDto> Items { get; init; }

        [Required]
        public string Reason { get; init; }
    }

    public record EditPriceItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal BuyingPrice { get; init; }

        [Range(0.01, double.MaxValue)]
        public decimal? SellingPrice { get; init; }
    }

    /// <summary>
    /// Admin can delete any purchase order (except FullyApproved)
    /// </summary>
    public record DeletePurchaseOrderByAdminDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required]
        public string Reason { get; init; }
    }

    // ========== REJECT/CANCEL OPERATIONS ==========
    public record RejectPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public string Reason { get; init; }

        public List<Guid>? ItemIds { get; init; } // If rejecting specific items only
    }

    public record CancelPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public string Reason { get; init; }
    }

    // ========== UPDATE PURCHASE ORDER (Legacy/Compatibility) ==========
    public record UpdatePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<CreatePurchaseOrderItemDto> Items { get; init; }
    }

    // ========== RESPONSE DTOS ==========
    public record PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public Guid CreatedBy { get; set; }
        public string CreatorName { get; set; }
        public Guid? SupplierId { get; set; }
        public string SupplierName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }

        // Progress tracking
        public int TotalItems { get; set; }
        public int AcceptedItems { get; set; }
        public int RegisteredItems { get; set; }
        public int FinanceVerifiedItems { get; set; }
        public int ApprovedItems { get; set; }

        // Financials
        public decimal? TotalBuyingCost { get; set; }
        public decimal? TotalSellingValue { get; set; }
        public decimal? TotalProfit { get; set; }

        public List<PurchaseOrderItemDto> Items { get; set; }
    }

    public class PurchaseOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }

        // Step 1: Requested
        public int QuantityRequested { get; set; }

        // Step 2: Accepted
        public int? QuantityAccepted { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public string AcceptedBy { get; set; }

        // Step 3: Registered
        public int? QuantityRegistered { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public string RegisteredBy { get; set; }
        public int RegistrationEditCount { get; set; }

        // Step 4: Finance
        public bool? FinanceVerified { get; set; }
        public DateTime? FinanceVerifiedAt { get; set; }
        public string FinanceVerifiedBy { get; set; }
        public decimal? BuyingPrice { get; set; }
        public decimal? UnitPrice { get; set; }
        public int PriceEditCount { get; set; }

        // Step 5: Approved
        public DateTime? ApprovedAt { get; set; }
        public string ApprovedBy { get; set; }

        // Calculated
        public decimal? TotalCost => BuyingPrice.HasValue && QuantityRegistered.HasValue
            ? BuyingPrice.Value * QuantityRegistered.Value
            : null;

        public decimal? TotalRevenue => UnitPrice.HasValue && QuantityRegistered.HasValue
            ? UnitPrice.Value * QuantityRegistered.Value
            : null;

        public decimal? ProfitMargin =>
            BuyingPrice.HasValue && UnitPrice.HasValue && BuyingPrice.Value > 0
                ? ((UnitPrice.Value - BuyingPrice.Value) / BuyingPrice.Value) * 100
                : null;

        // Status flags
        public bool IsAccepted => QuantityAccepted > 0;
        public bool IsRegistered => QuantityRegistered > 0;
        public bool IsFinanceVerified => FinanceVerified == true;
        public bool IsApproved => ApprovedAt.HasValue;
    }

    // ========== STATISTICS DTO ==========
    public record PurchaseOrderStatsDto
    {
        public int TotalPurchaseOrders { get; init; }
        public int PendingAdminAcceptance { get; init; }
        public int AcceptedByAdmin { get; init; }
        public int PartiallyRegistered { get; init; }
        public int CompletelyRegistered { get; init; }
        public int PartiallyFinanceProcessed { get; init; }
        public int FullyFinanceProcessed { get; init; }
        public int PartiallyApproved { get; init; }
        public int FullyApproved { get; init; }
        public int Rejected { get; init; }
        public int Cancelled { get; init; }

        public decimal TotalBuyingCost { get; init; }
        public decimal TotalSellingValue { get; init; }
        public decimal TotalProfit => TotalSellingValue - TotalBuyingCost;

        // Computed property
        public decimal AverageOrderValue =>
            TotalPurchaseOrders > 0 ? TotalBuyingCost / TotalPurchaseOrders : 0;
    }

    // ========== BACKWARD COMPATIBILITY DTOs ==========
    public record ReceivePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<ReceivePurchaseOrderItemDto> Items { get; init; }
    }

    public record ReceivePurchaseOrderItemDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityReceived { get; init; }
    }

    public record ApprovePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<ApprovePurchaseOrderItemDto> Items { get; init; }
    }

    public class ApprovePurchaseOrderItemDto
    {
        [Required]
        public Guid Id { get; set; }
        [Required, Range(1, int.MaxValue)]
        public int QuantityApproved { get; set; }
    }
    // Add to your DTOs file
    public record PurchaseOrderDashboardDto
    {
        // Summary counts
        public int TotalPending { get; set; }
        public int TotalInProgress { get; set; }
        public int TotalCompleted { get; set; }
        public int TotalRejected { get; set; }

        // Monthly trends
        public Dictionary<string, int> OrdersByMonth { get; set; }
        public Dictionary<string, decimal> ValueByMonth { get; set; }

        // Status breakdown
        public Dictionary<string, int> OrdersByStatus { get; set; }

        // Branch breakdown (if no branch filter)
        public Dictionary<string, int> OrdersByBranch { get; set; }
        public Dictionary<string, decimal> ValueByBranch { get; set; }

        // Recent activity
        public List<RecentPurchaseOrderDto> RecentOrders { get; set; }
        public List<RecentPurchaseHistoryDto> RecentActivity { get; set; }

        // Financial summary
        public decimal TotalPurchaseValue { get; set; }
        public decimal AverageOrderValue { get; set; }
        public decimal TotalProfit { get; set; }
        public decimal AverageProfitMargin { get; set; }
    }

    public record RecentPurchaseOrderDto
    {
        public Guid Id { get; set; }
        public string OrderNumber { get; set; }
        public string BranchName { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
        public int ItemCount { get; set; }
        public decimal TotalValue { get; set; }
    }

    public record RecentPurchaseHistoryDto
    {
        public Guid PurchaseOrderId { get; set; }
        public string OrderNumber { get; set; }
        public string Action { get; set; }
        public string PerformedBy { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Details { get; set; }
    }
}