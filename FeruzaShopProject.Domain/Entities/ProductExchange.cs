using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class ProductExchange : BaseEntity
    {
        [Required]
        public Guid OriginalTransactionId { get; set; }

        // Original product info (from transaction)
        [Required]
        public Guid OriginalProductId { get; set; }
        [Required]
        public decimal OriginalQuantity { get; set; }
        [Required]
        public decimal OriginalPrice { get; set; }

        // New product info
        [Required]
        public Guid NewProductId { get; set; }
        [Required]
        public decimal NewQuantity { get; set; }
        [Required]
        public decimal NewPrice { get; set; }

        // Navigation properties
        public Transaction OriginalTransaction { get; set; }
        public Product OriginalProduct { get; set; }
        public Product NewProduct { get; set; }

        // Calculated properties
        public decimal TotalOriginal => OriginalQuantity * OriginalPrice;
        public decimal TotalNew => NewQuantity * NewPrice;
        public decimal MoneyDifference => TotalNew - TotalOriginal;
        public bool IsRefund => MoneyDifference < 0;
        public bool IsAdditionalPayment => MoneyDifference > 0;
        public bool IsEvenExchange => MoneyDifference == 0;
        public decimal Amount => Math.Abs(MoneyDifference);

        // Quick checks
        public bool IsReturnOnly => NewQuantity == 0;
        public bool IsExchange => NewQuantity > 0;
    }
}
