using System;
using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseHistory : BaseEntity
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }

        [Required]
        public string Action { get; set; } // "Created", "Accepted", "Registered", "Edited", "FinanceVerified", "Approved", "Rejected", "Cancelled"

        [Required]
        public Guid PerformedByUserId { get; set; }

        public string Details { get; set; } // What changed: "Registered 20 units", "Updated price from $10 to $9.50", etc.

        // Optional: Track which item was affected
        public Guid? PurchaseOrderItemId { get; set; }

        // Navigation Properties
        public PurchaseOrder PurchaseOrder { get; set; }
        public User PerformedByUser { get; set; }
        public PurchaseOrderItem PurchaseOrderItem { get; set; }
    }
}