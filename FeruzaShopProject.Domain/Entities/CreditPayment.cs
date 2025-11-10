using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class CreditPayment : BaseEntity
    {
        [Required]
        public Guid TransactionId { get; set; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public PaymentMethod PaymentMethod { get; set; }

        public DateTime PaymentDate { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public Transaction Transaction { get; set; }
    }
}
