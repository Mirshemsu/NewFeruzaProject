using FeruzaShopProject.Domain.Entities;
using System.ComponentModel.DataAnnotations;



public class ProductExchange : BaseEntity
{
    [Required]
    public Guid OriginalTransactionId { get; set; }

    // Original product info
    [Required]
    public Guid OriginalProductId { get; set; }
    [Required]
    public decimal OriginalQuantity { get; set; }
    [Required]
    public decimal OriginalPrice { get; set; }

    // Return quantity (can be partial)
    [Required]
    public decimal ReturnQuantity { get; set; }

    // New product info (nullable for return-only)
    public Guid? NewProductId { get; set; }
    public decimal? NewQuantity { get; set; }
    public decimal? NewPrice { get; set; }

    // Navigation
    public Transaction OriginalTransaction { get; set; }
    public Product OriginalProduct { get; set; }
    public Product NewProduct { get; set; }

    // Calculated properties
    public decimal TotalOriginal => ReturnQuantity * OriginalPrice;
    public decimal? TotalNew => NewQuantity.HasValue ? NewQuantity.Value * (NewPrice ?? 0) : null;
    public decimal? MoneyDifference => TotalNew - TotalOriginal;
    public bool IsRefund => MoneyDifference < 0;
    public bool IsAdditionalPayment => MoneyDifference > 0;
    public bool IsEvenExchange => MoneyDifference == 0;
    public bool IsReturnOnly => !NewProductId.HasValue || NewQuantity == 0;
    public decimal? Amount => MoneyDifference.HasValue ? Math.Abs(MoneyDifference.Value) : TotalOriginal;
}