using FeruzaShopProject.Domain.Entities;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class ProductResponseDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ItemCode { get; set; }
        public string ItemDescription { get; set; }
        public string Quantity { get; set; } // e.g., "25 kg", "1 Pcs"
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal CommissionPerProduct { get; set; }
        public int ReorderLevel { get; set; }
        public int TotalStock { get; set; }
        public Guid CategoryId { get; set; }
        public string CategoryName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class CreateProductDto
    {
        [Required, StringLength(100)]
        public string Name { get; set; }

        [Required, StringLength(50)]
        public string ItemCode { get; set; }

        [StringLength(500)]
        public string ItemDescription { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Required]
        public UnitType Unit { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal BuyingPrice { get; set; }

        [Required, Range(0, double.MaxValue)]
        public decimal UnitPrice { get; set; } // This maps to SellingPrice in Product entity

        [Required, Range(0, double.MaxValue)]
        public decimal CommissionPerProduct { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int ReorderLevel { get; set; }

        [Required]
        public Guid CategoryId { get; set; }

        // Remove the single BranchId property since we're using BranchStocks
        // [Required] - REMOVE THIS
        // public Guid BranchId { get; set; }

        [Required, MinLength(1, ErrorMessage = "At least one branch stock entry is required")]
        public List<ProductBranchStockDto> BranchStocks { get; set; } = new();
    }

    public class ProductBranchStockDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required, Range(0, int.MaxValue)]
        public int Quantity { get; set; }
    }

    public class UpdateProductDto
    {
        [Required]
        public Guid Id { get; set; }

        [StringLength(100)]
        public string Name { get; set; }

        [StringLength(50)]
        public string ItemCode { get; set; }

        [StringLength(500)]
        public string ItemDescription { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? Amount { get; set; }

        public UnitType? Unit { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? BuyingPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? SellingPrice { get; set; }

        [Range(0, double.MaxValue)]
        public decimal? CommissionPerProduct { get; set; }

        [Range(0, int.MaxValue)]
        public int? ReorderLevel { get; set; }

        public Guid? CategoryId { get; set; }
    }

    public class ProductLowStockDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public string ItemDescription { get; set; }
        public string Quantity { get; set; } // e.g., "25 kg", "1 Pcs"
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public int QuantityRemaining { get; set; }
        public int ReorderLevel { get; set; }
    }

    public class ProductStockDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public string ItemDescription { get; set; }
        public string Quantity { get; set; } // e.g., "25 kg", "1 Pcs"
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public int QuantityRemaining { get; set; }
    }

    public class AdjustStockDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public int QuantityChange { get; set; }
    }
    public class AddProductToBranchDto
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid BranchId { get; set; }

        [Required]
        [Range(0, int.MaxValue, ErrorMessage = "Initial stock must be non-negative")]
        public int InitialStock { get; set; }
    }
}