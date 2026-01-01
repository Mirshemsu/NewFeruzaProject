using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IProductExchangeService
    {
        Task<ApiResponse<ProductExchangeResponseDto>> CreateExchangeAsync(CreateProductExchangeDto dto);
        Task<ApiResponse<ProductExchangeResponseDto>> GetExchangeByIdAsync(Guid id);
        Task<ApiResponse<List<ProductExchangeResponseDto>>> GetAllExchangesAsync(Guid? branchId = null, DateTime? startDate = null, DateTime? endDate = null);
        Task<ApiResponse<bool>> DeleteExchangeAsync(Guid id);
        Task<ApiResponse<List<ProductExchangeResponseDto>>> GetExchangesByTransactionAsync(Guid transactionId);
        Task<ApiResponse<List<ProductExchangeResponseDto>>> GetExchangesByCustomerAsync(Guid customerId);
        Task<ApiResponse<ExchangeSummaryDto>> GetExchangeSummaryAsync(DateTime? startDate = null, DateTime? endDate = null, Guid? branchId = null);
    }
}
