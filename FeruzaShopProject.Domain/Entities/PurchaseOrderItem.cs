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
        public int Quantity { get; set; }

        // Prices (set by finance per item)
        public decimal? BuyingPrice { get; set; }     
        public decimal? UnitPrice { get; set; }        
        public string? SupplierName { get; set; }

        // Simple calculated fields
        public decimal? ProfitMargin =>
            BuyingPrice.HasValue && UnitPrice.HasValue && BuyingPrice.Value > 0
                ? ((UnitPrice.Value - BuyingPrice.Value) / BuyingPrice.Value) * 100
                : null;

        // Navigation Properties
        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }
    }
}