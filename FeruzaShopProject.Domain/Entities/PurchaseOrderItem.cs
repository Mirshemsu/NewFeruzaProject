using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrderItem : BaseEntity
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }
        [Required]
        public Guid ProductId { get; set; }
        [Required, Range(1, int.MaxValue)]
        public int QuantityOrdered { get; set; }
        [Range(0, int.MaxValue)]
        public int? QuantityReceived { get; set; }
        [Range(0, int.MaxValue)]
        public int? QuantityApproved { get; set; }
        [Required, Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; set; }
        public PurchaseOrder PurchaseOrder { get; set; }
        public Product Product { get; set; }

        public void SetQuantityReceived(int quantityReceived)
        {
            if (quantityReceived < 0)
                throw new ArgumentException("QuantityReceived cannot be negative");
            QuantityReceived = (QuantityReceived ?? 0) + quantityReceived;
        }

        public void Approve(int quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("Approved quantity cannot be negative");
            if (QuantityReceived == null || quantity > QuantityReceived)
                throw new ArgumentException("Approved quantity cannot exceed received quantity");
            if ((QuantityApproved ?? 0) + quantity > QuantityReceived)
                throw new ArgumentException("Total approved quantity cannot exceed received quantity");
            QuantityApproved = (QuantityApproved ?? 0) + quantity;
        }

        public void Reject(int quantity)
        {
            if (quantity < 0)
                throw new ArgumentException("Rejected quantity cannot be negative");
            if (QuantityReceived == null || quantity > QuantityReceived - (QuantityApproved ?? 0))
                throw new ArgumentException("Rejected quantity cannot exceed unapproved received quantity");
        }
    }
}
