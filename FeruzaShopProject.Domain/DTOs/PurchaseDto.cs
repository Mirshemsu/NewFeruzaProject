using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.DTOs
{
    // Step 1: Sales creates purchase order with requested quantities only
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

    // Step 2: Admin accepts/reduces quantities
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

    // Step 3: Sales registers received quantities after purchasing
    public record RegisterReceivedQuantitiesDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public List<RegisterQuantityItemDto> Items { get; init; }
    }

    public record RegisterQuantityItemDto
    {
        [Required]
        public Guid ItemId { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityRegistered { get; init; }
    }

    // Step 4: Finance adds supplier, prices, and verifies quantities
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

        public bool? FinanceVerified { get; init; }

        [Range(0.01, double.MaxValue)]
        public decimal? UnitPrice { get; init; }
    }

    // Step 5: Admin gives final approval
    public record FinalApprovePurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }
    }

    // Reject purchase order
    public record RejectPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }

        [Required, MinLength(1)]
        public string Reason { get; init; }
    }

    // Cancel purchase order
    public record CancelPurchaseOrderDto
    {
        [Required]
        public Guid PurchaseOrderId { get; init; }
    }

    // Update purchase order (only in PendingAdminAcceptance status)
    public record UpdatePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<CreatePurchaseOrderItemDto> Items { get; init; }
    }

    // Response DTOs
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
        public decimal? TotalPrice { get; set; }
        public List<PurchaseOrderItemDto> Items { get; set; }
    }

    public class PurchaseOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantityRequested { get; set; }
        public int? QuantityAccepted { get; set; }
        public int? QuantityRegistered { get; set; }
        public bool? FinanceVerified { get; set; }
        public decimal? UnitPrice { get; set; }
        public decimal? TotalPrice { get; set; }
    }

    // For backward compatibility (if needed)
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
    public record PurchaseOrderStatsDto
    {
        public int TotalPurchaseOrders { get; init; }
        public int PendingAdminAcceptance { get; init; }
        public int AcceptedByAdmin { get; init; }
        public int PendingRegistration { get; init; }
        public int CompletelyRegistered { get; init; }
        public int PendingFinanceProcessing { get; init; }
        public int ProcessedByFinance { get; init; }
        public int FullyApproved { get; init; }
        public int Rejected { get; init; }
        public int Cancelled { get; init; }
        public decimal TotalPurchaseValue { get; init; }

        // Computed property - no setter needed
        public decimal AveragePurchaseValue =>
            TotalPurchaseOrders > 0 ? TotalPurchaseValue / TotalPurchaseOrders : 0;
    }
}