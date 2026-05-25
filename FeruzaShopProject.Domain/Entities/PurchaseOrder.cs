using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrder : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid CreatedBy { get; set; }

        [Required]
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.PendingFinanceVerification;

        // ========== INVOICE NUMBER (Added by Finance) ==========
        [StringLength(50)]
        public string? InvoiceNumber { get; set; }

        // ========== REJECTION REASON ==========
        [StringLength(500)]
        public string? RejectionReason { get; set; }  // Only this field - no who or when

        // ========== APPROVAL TRACKING (Per Purchase) ==========
        public DateTime? FinanceVerifiedAt { get; set; }
        public Guid? FinanceVerifiedBy { get; set; }

        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }

        // Simple progress tracking
        public int TotalItems => Items?.Count ?? 0;

        // Calculated totals
        public decimal TotalBuyingCost => Items?
            .Where(i => i.BuyingPrice.HasValue)
            .Sum(i => i.BuyingPrice.Value * i.Quantity) ?? 0;

        public decimal TotalSellingValue => Items?
            .Where(i => i.UnitPrice.HasValue)
            .Sum(i => i.UnitPrice.Value * i.Quantity) ?? 0;

        public decimal TotalProfit => TotalSellingValue - TotalBuyingCost;

        // Navigation Properties
        public Branch Branch { get; set; }
        public User Creator { get; set; }
        public User FinanceVerifier { get; set; }
        public User Approver { get; set; }
        public List<PurchaseOrderItem> Items { get; set; } = new();
        public List<PurchaseHistory> History { get; set; } = new();
    }
}