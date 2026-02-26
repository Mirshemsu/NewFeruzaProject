using System;
using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrderItem : BaseEntity
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        // Step 1: Sales Request
        [Required]
        public int QuantityRequested { get; set; }

        // Step 2: Admin Accepts
        public int? QuantityAccepted { get; set; }
        public DateTime? AcceptedAt { get; set; }
        public Guid? AcceptedBy { get; set; }
        public string? SupplierName { get; set; }

        // Step 3: Sales Registers (can be updated multiple times before finance)
        public int? QuantityRegistered { get; set; }
        public DateTime? RegisteredAt { get; set; }
        public Guid? RegisteredBy { get; set; }

        // Track registration edits
        public int RegistrationEditCount { get; set; }
        public DateTime? LastRegistrationEditAt { get; set; }

        // Step 4: Finance Verification
        public bool? FinanceVerified { get; set; } = false;
        public DateTime? FinanceVerifiedAt { get; set; }
        public Guid? FinanceVerifiedBy { get; set; }

        // Prices (can be edited by admin anytime before approval)
        public decimal? BuyingPrice { get; set; }      // Cost price
        public decimal? UnitPrice { get; set; }        // Selling price
        public DateTime? PriceSetAt { get; set; }
        public Guid? PriceSetBy { get; set; }
        public int PriceEditCount { get; set; }

        // Step 5: Final Approval
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }

        // Simple calculated fields
        public decimal? ProfitMargin =>
            BuyingPrice.HasValue && UnitPrice.HasValue && BuyingPrice.Value > 0
                ? ((UnitPrice.Value - BuyingPrice.Value) / BuyingPrice.Value) * 100
                : null;

        // Status flags for easy checking
        public bool IsAccepted => QuantityAccepted > 0;
        public bool IsRegistered => QuantityRegistered > 0;
        public bool IsFinanceVerified => FinanceVerified == true;
        public bool IsApproved => ApprovedAt.HasValue;
        public bool CanEdit => !IsApproved; // Can edit until approved

        // Navigation Properties
        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }
    }
}