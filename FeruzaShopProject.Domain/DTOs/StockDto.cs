using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class StockOnDateDto
    {
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
    }

    // For current stock view
    public class CurrentStockDto
    {
        public DateTime Date { get; set; }
        public List<StockItemDto> Items { get; set; }
        public int TotalItems { get; set; }
        public decimal TotalValue { get; set; }
    }

    public class StockItemDto
    {
        public Guid ProductId { get; set; }
        public string ProductName { get; set; }
        public string ItemCode { get; set; }
        public Guid BranchId { get; set; }
        public string BranchName { get; set; }
        public decimal Quantity { get; set; }
        public decimal UnitPrice { get; set; }
        public decimal TotalValue { get; set; }
    }

    // For stock history
    public class StockHistoryDto
    {
        public DateTime Date { get; set; }
        public decimal Quantity { get; set; }
        public decimal Change { get; set; }
        public string ChangeType { get; set; }
    }
}
