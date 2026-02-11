// PurchaseOrderItem.cs
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
        public bool? FinanceVerified { get; set; }

        // Price added by Finance
        [Range(0.01, double.MaxValue)]
        public decimal? UnitPrice { get; set; }

        public decimal? TotalPrice => UnitPrice.HasValue && QuantityRegistered.HasValue
            ? UnitPrice.Value * QuantityRegistered.Value
            : null;

        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }
    }
}