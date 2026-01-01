using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IStockService
    {
        Task<ApiResponse<decimal>> GetStockOnDateAsync(Guid productId, Guid branchId, DateTime date);
        Task<ApiResponse<CurrentStockDto>> GetCurrentStockAsync(Guid? branchId = null, Guid? productId = null);
        Task<ApiResponse<List<StockHistoryDto>>> GetStockHistoryAsync(Guid productId, Guid branchId, DateTime startDate, DateTime endDate);
    }

}
