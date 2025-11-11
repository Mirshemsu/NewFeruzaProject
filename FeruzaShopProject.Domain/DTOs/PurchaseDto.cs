using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public record CreatePurchaseOrderDto
    {
        [Required]
        public Guid BranchId { get; init; }

        [Required]
        public Guid SupplierId { get; init; }

        [Required, MinLength(1)]
        public List<CreatePurchaseOrderItemDto> Items { get; init; }
    }

    public record CreatePurchaseOrderItemDto
    {
        [Required]
        public Guid ProductId { get; init; }

        [Required, Range(1, int.MaxValue)]
        public int QuantityOrdered { get; init; }

        [Required, Range(0.01, double.MaxValue)]
        public decimal UnitPrice { get; init; }
    }

    public record UpdatePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required]
        public Guid SupplierId { get; init; }

        [Required, MinLength(1)]
        public List<CreatePurchaseOrderItemDto> Items { get; init; }
    }

    public record ReceivePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<ReceivePurchaseOrderItemDto> Items { get; init; }
    }

    public record ReceivePurchaseOrderItemDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, Range(0, int.MaxValue)]
        public int QuantityReceived { get; init; }
    }

    public record ApprovePurchaseOrderDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, MinLength(1)]
        public List<ApprovePurchaseOrderItemDto> Items { get; init; }
    }

    public class ApprovePurchaseOrderItemDto
    {
        [Required]
        public Guid Id { get; set; }
        [Required, Range(1, int.MaxValue)]
        public int QuantityApproved { get; set; }
    }

    public record PurchaseOrderDto
    {
        public Guid Id { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public Guid SupplierId { get; set; }
        public string SupplierName { get; set; }
        public Guid CreatedBy { get; set; }
        public string CreatorName { get; set; }
        public string Status { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<PurchaseOrderItemDto> Items { get; set; }
    }

    public class PurchaseOrderItemDto
    {
        public Guid Id { get; set; }
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public int QuantityOrdered { get; set; }
        public int? QuantityReceived { get; set; }
        public int? QuantityApproved { get; set; }
        public decimal UnitPrice { get; set; }
    }

}
