using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class Transaction : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        // Optional references
        public Guid? CustomerId { get; set; }
        public Guid? PainterId { get; set; }

        [Required]
        public DateTime TransactionDate { get; set; } = DateTime.UtcNow;

        [Required, StringLength(50)]
        public string ItemCode { get; set; }

        // Pricing
        [Required, Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required, Range(0.001, double.MaxValue)]
        public decimal Quantity { get; set; }

        // Payment
        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        // Commission
        [Range(0, double.MaxValue)]
        public decimal CommissionRate { get; set; }

        public bool CommissionPaid { get; set; }

        // Navigation properties
        public Branch Branch { get; set; }
        public Product Product { get; set; }
        public Customer Customer { get; set; }
        public Painter Painter { get; set; }
        public List<DailySales> DailySales { get; private set; } = new();
        public List<StockMovement> StockMovements { get; private set; } = new();
        public List<CreditPayment> CreditPayments { get; private set; } = new();

        // Validation method
        public void Validate()
        {
            if (UnitPrice <= 0)
                throw new ArgumentException("Unit price must be greater than zero");

            if (Quantity <= 0)
                throw new ArgumentException("Quantity must be greater than zero");

            if (string.IsNullOrWhiteSpace(ItemCode))
                throw new ArgumentException("Item code is required");
        }
    }

}
