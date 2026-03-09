using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IStockService
    {
        // Get stock on a specific date
        Task<ApiResponse<StockOnDateDto>> GetStockOnDateAsync(Guid productId, Guid branchId, DateTime date);

        // Get current stock with credit information
        Task<ApiResponse<CurrentStockDto>> GetCurrentStockAsync(Guid? branchId = null, Guid? productId = null);

        // Get stock history with detailed breakdown
        Task<ApiResponse<List<StockHistoryDto>>> GetStockHistoryAsync(
            Guid productId, Guid branchId, DateTime startDate, DateTime endDate);

        // Get credit stock summary (unpaid credit items)
        Task<ApiResponse<StockCreditSummaryDto>> GetCreditStockSummaryAsync(
            Guid? branchId = null, Guid? customerId = null);

        // Get stock alerts (low stock, out of stock, overdue credit)
        Task<ApiResponse<StockAlertDto>> GetStockAlertsAsync(Guid? branchId = null);

        // Get detailed stock for a specific product in a branch
        Task<ApiResponse<StockItemDetailDto>> GetProductStockDetailAsync(
            Guid productId, Guid branchId);
    }
}