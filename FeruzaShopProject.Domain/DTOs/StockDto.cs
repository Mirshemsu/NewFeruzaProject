using System;
using System.Collections.Generic;

namespace FeruzaShopProject.Domain.DTOs
{
    // For stock on a specific date
    public class StockOnDateDto
    {
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public decimal? CreditQuantity { get; set; }
        public decimal? NetQuantity { get; set; }
    }

    // For current stock view with credit information
    public class CurrentStockDto
    {
        public DateTime Date { get; set; }
        public List<StockItemDetailDto> Items { get; set; }
        public int TotalItems { get; set; }

        // Summary totals
        public decimal TotalActualStock { get; set; }
        public decimal TotalCreditStock { get; set; }
        public decimal TotalNetStock { get; set; }
        public decimal TotalActualValue { get; set; }
        public decimal TotalCreditValue { get; set; }
        public decimal TotalNetValue { get; set; }

        // Status counts
        public int InStockItems { get; set; }
        public int LowStockItems { get; set; }
        public int OutOfStockItems { get; set; }
    }

    // Enhanced stock item with credit information
    public class StockItemDetailDto
    {
        // Product Info
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public string CategoryName { get; set; }

        // Branch Info
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }

        // Stock Quantities
        public decimal ActualQuantity { get; set; }      // Physical stock in warehouse
        public decimal CreditQuantity { get; set; }      // Items sold on credit but unpaid
        public decimal NetQuantity { get; set; }         // Available for sale (Actual - Credit)

        // Financial Values
        public decimal BuyingPrice { get; set; }
        public decimal SellingPrice { get; set; }
        public decimal ActualValue { get; set; }         // Actual * BuyingPrice
        public decimal CreditValue { get; set; }         // Credit * BuyingPrice
        public decimal NetValue { get; set; }            // Net * BuyingPrice

        // Status
        public string StockStatus { get; set; }          // "In Stock", "Low Stock", "Out of Stock"
        public int ReorderLevel { get; set; }

        // Unit Info
        public decimal UnitAmount { get; set; }
        public string UnitType { get; set; }
        public string DisplayQuantity => $"{UnitAmount} {UnitType}";
    }

    // For stock history with detailed changes
    public class StockHistoryDto
    {
        public DateTime Date { get; set; }
        public decimal OpeningBalance { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal NetChange { get; set; }
        public string ChangeType { get; set; }

        // Breakdown of changes
        public decimal Purchases { get; set; }
        public decimal Sales { get; set; }
        public decimal Returns { get; set; }
        public decimal Adjustments { get; set; }
        public decimal Damages { get; set; }

        // Credit stock info
        public decimal CreditStock { get; set; }
        public decimal NetAvailable { get; set; }

        // Movements on this day
        public List<StockMovementSummaryDto> Movements { get; set; }
    }

    public class StockMovementSummaryDto
    {
        public Guid MovementId { get; set; }
        public string MovementType { get; set; }
        public decimal Quantity { get; set; }
        public string Reason { get; set; }
        public DateTime MovementDate { get; set; }
        public decimal PreviousQuantity { get; set; }
        public decimal NewQuantity { get; set; }
    }

    // For credit stock summary - THIS WAS MISSING
    public class StockCreditSummaryDto
    {
        public DateTime GeneratedAt { get; set; }
        public List<CreditStockItemDto> Items { get; set; }
        public int TotalCustomers { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal TotalPendingAmount { get; set; }
    }

    public class CreditStockItemDto
    {
        public Guid TransactionId { get; set; }
        public DateTime TransactionDate { get; set; }
        public string CustomerName { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal PaidAmount { get; set; }
        public decimal PendingAmount { get; set; }
        public decimal PendingQuantity { get; set; }
        public string BranchName { get; set; }
        public DateTime? LastPaymentDate { get; set; }
        public int DaysOverdue { get; set; }
    }

    // For stock alert
    public class StockAlertDto
    {
        public List<StockItemDetailDto> LowStockItems { get; set; }
        public List<StockItemDetailDto> OutOfStockItems { get; set; }
        public List<CreditStockItemDto> OverdueCreditItems { get; set; }
        public int AlertCount { get; set; }
    }
}