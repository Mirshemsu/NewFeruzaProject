using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrderItem : BaseEntity
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        // Requested by Sales
        [Required, Range(1, int.MaxValue)]
        public int QuantityRequested { get; set; }

        // Accepted by Admin
        [Range(0, int.MaxValue)]
        public int? QuantityAccepted { get; set; }

        // Registered by Sales after receiving
        [Range(0, int.MaxValue)]
        public int? QuantityRegistered { get; set; }

        // Finance cross-check confirmation (true/false)
        public bool? FinanceVerified { get; set; }=false;

        // Buying Price (from supplier) - Added by Finance
        [Range(0.01, double.MaxValue)]
        public decimal? BuyingPrice { get; set; }

        // Selling Price - Can be calculated or manually set
        [Range(0.01, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        // Calculate profit margin percentage
        public decimal? ProfitMarginPercentage =>
            BuyingPrice.HasValue && UnitPrice.HasValue && BuyingPrice.Value > 0
                ? ((UnitPrice.Value - BuyingPrice.Value) / BuyingPrice.Value) * 100
                : null;

        public decimal? TotalCost => BuyingPrice.HasValue && QuantityRegistered.HasValue
            ? BuyingPrice.Value * QuantityRegistered.Value
            : null;

        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }
    }
}