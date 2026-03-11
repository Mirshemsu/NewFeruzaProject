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

        // Step 1: Sales Registration
        [Required]
        public int Quantity { get; set; }  // Renamed from QuantityRequested

        // Prices (set by finance)
        public decimal? BuyingPrice { get; set; }      // Cost price
        public decimal? UnitPrice { get; set; }        // Selling price
        public string? SupplierName { get; set; }

        // Step 2: Finance Verification
        public bool? FinanceVerified { get; set; } = false;
        public DateTime? FinanceVerifiedAt { get; set; }
        public Guid? FinanceVerifiedBy { get; set; }

        // Price edit tracking
        public DateTime? PriceSetAt { get; set; }
        public Guid? PriceSetBy { get; set; }
        public int PriceEditCount { get; set; }

        // Step 3: Manager Approval
        public DateTime? ApprovedAt { get; set; }
        public Guid? ApprovedBy { get; set; }

        // Simple calculated fields
        public decimal? ProfitMargin =>
            BuyingPrice.HasValue && UnitPrice.HasValue && BuyingPrice.Value > 0
                ? ((UnitPrice.Value - BuyingPrice.Value) / BuyingPrice.Value) * 100
                : null;

        // Status flags for easy checking
        public bool IsFinanceVerified => FinanceVerified == true;
        public bool IsApproved => ApprovedAt.HasValue;
        public bool CanFinanceEdit => !IsFinanceVerified && !IsApproved;
        public bool CanManagerEdit => IsFinanceVerified && !IsApproved;

        // Navigation Properties
        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }
    }
}