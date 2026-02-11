using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class StockMovement : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid BranchId { get; set; }
        public Guid? TransactionId { get; set; }
        public Guid? PurchaseOrderId { get; set; }

        [Required]
        public StockMovementType MovementType { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        [Required]
        public decimal PreviousQuantity { get; set; }

        [Required]
        public decimal NewQuantity { get; set; }

        public string Reason { get; set; }

        public DateTime MovementDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Product Product { get; set; }
        public Branch Branch { get; set; }
        public Transaction Transaction { get; set; }
    }
}
