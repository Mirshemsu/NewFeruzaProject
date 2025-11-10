using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class DailySales : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid TransactionId { get; set; }

        [Required]
        public DateTime SaleDate { get; set; }

        [Required, Range(0.001, double.MaxValue)]
        public decimal Quantity { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal TotalAmount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        // Commission info
        [Range(0, double.MaxValue)]
        public decimal CommissionRate { get; set; }

        [Range(0, double.MaxValue)]
        public decimal CommissionAmount { get; set; }

        public bool CommissionPaid { get; set; }

        // Customer/Painter references
        public Guid? CustomerId { get; set; }
        public Guid? PainterId { get; set; }

        // Navigation properties
        public Branch Branch { get; set; }
        public Product Product { get; set; }
        public Transaction Transaction { get; set; }
        public Customer Customer { get; set; }
        public Painter Painter { get; set; }
    }
}
