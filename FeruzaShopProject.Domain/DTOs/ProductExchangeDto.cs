using System;
using System.Collections.Generic;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateProductExchangeDto
    {
        public Guid OriginalTransactionId { get; set; }
        public Guid NewProductId { get; set; }
        public decimal NewQuantity { get; set; }
    }

    public class ProductExchangeResponseDto
    {
        public Guid Id { get; set; }
        public Guid OriginalTransactionId { get; set; }
        public ProductDto OriginalProduct { get; set; }
        public ProductDto NewProduct { get; set; }
        public decimal OriginalQuantity { get; set; }
        public decimal NewQuantity { get; set; }
        public decimal OriginalTotal { get; set; }
        public decimal NewTotal { get; set; }
        public decimal MoneyDifference { get; set; }
        public bool IsRefund { get; set; }
        public bool IsAdditionalPayment { get; set; }
        public decimal Amount { get; set; }
        public string Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class ExchangeSummaryDto
    {
        public int TotalExchanges { get; set; }
        public int RefundExchanges { get; set; }
        public int AdditionalPaymentExchanges { get; set; }
        public int EvenExchanges { get; set; }
        public decimal TotalRefundAmount { get; set; }
        public decimal TotalAdditionalPayment { get; set; }
        public List<ProductExchangeResponseDto> RecentExchanges { get; set; } = new();
    }

    public class ProductDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string ItemCode { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal BuyingPrice { get; set; }
    }
}