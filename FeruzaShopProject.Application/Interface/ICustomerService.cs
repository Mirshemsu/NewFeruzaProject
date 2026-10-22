using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;

namespace FeruzaShopProject.Application.Interface
{
    public interface ICustomerService
    {
        Task<ApiResponse<List<CustomerListItemDto>>> GetAllAsync(Guid? branchId, string? search);
        Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetCreditsAsync(Guid customerId, Guid? branchScope);
        Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetUnlinkedCreditsAsync(Guid? branchId);
        Task<ApiResponse<CustomerListItemDto>> CreateAsync(CreateCustomerDto dto);
        Task<ApiResponse<CustomerListItemDto>> UpdateAsync(UpdateCustomerDto dto, Guid? branchScope);
        Task<ApiResponse<ImportCustomersResultDto>> ImportAsync(ImportCustomersDto dto);
        Task<ApiResponse<bool>> LinkSaleAsync(LinkCustomerSaleDto dto, Guid? branchScope);
        Task<ApiResponse<bool>> DeactivateAsync(Guid id, Guid? branchScope);
    }
}
