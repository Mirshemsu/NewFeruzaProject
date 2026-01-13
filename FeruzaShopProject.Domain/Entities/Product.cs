    using System;
    using System.Collections;
    using System.Collections.Generic;
    using System.ComponentModel.DataAnnotations;
    using System.Linq;
    using System.Text;
    using System.Threading.Tasks;

    namespace FeruzaShopProject.Domain.Entities
    {
        public class Product : BaseEntity
        {
            [Required]
            public Guid CategoryId { get; set; }

            [Required, StringLength(100)]
            public string Name { get; set; }

            [Required, StringLength(50)]
            public string ItemCode { get; set; }

            [StringLength(500)]
            public string? ItemDescription { get; set; }

            [Required, Range(0, double.MaxValue, ErrorMessage = "Amount must be non-negative.")]
            public decimal Amount { get; set; }

            [Required]
            public UnitType Unit { get; set; }

            [Range(0, double.MaxValue, ErrorMessage = "Buying price must be non-negative.")]
            public decimal BuyingPrice { get; set; }

            [Range(0, double.MaxValue, ErrorMessage = "Selling price must be non-negative.")]
            public decimal UnitPrice { get; set; }

            [Range(0, double.MaxValue, ErrorMessage = "Commission per product must be non-negative.")]
            public decimal CommissionPerProduct { get; set; }

            [Required, Range(0, int.MaxValue)]
            public int ReorderLevel { get; set; }

            public Category Category { get; set; }
            public ICollection<Stock> Stocks { get; set; } = new List<Stock>();
            public List<StockMovement> StockMovements { get; private set; } = new();
            public List<Transaction> Transactions { get; private set; } = new();
            public List<DailySales> DailySales { get; private set; } = new();
            public List<PurchaseOrderItem> PurchaseOrderItems { get; set; } = new();
            public List<ProductExchange> OriginalExchanges { get; set; } = new();
            public List<ProductExchange> NewExchanges { get; set; } = new();
        public void ValidateAmount()
        {
            if (Unit == UnitType.Pcs && Amount % 1 != 0)
                throw new ArgumentException("Amount must be an integer for Pcs unit.");
            if (Amount < 0)
                throw new ArgumentException("Amount cannot be negative.");
        }

    }
    }