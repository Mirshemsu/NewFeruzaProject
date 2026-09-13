using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;

namespace FeruzaShopProject.Application.Interface
{
    public interface IBankAccountService
    {
        Task<ApiResponse<List<BankAccountDto>>> GetAllAsync(Guid? branchId, DateTime? startDate, DateTime? endDate);
        Task<ApiResponse<BankAccountDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<BankAccountStatementDto>> GetStatementAsync(Guid id, DateTime? startDate, DateTime? endDate);
        Task<ApiResponse<BankAccountDto>> CreateAsync(CreateBankAccountDto dto);
        Task<ApiResponse<BankAccountDto>> UpdateAsync(UpdateBankAccountDto dto);
        Task<ApiResponse<bool>> DeleteAsync(Guid id);
        Task<string?> ValidateForPaymentAsync(Guid? bankAccountId, PaymentMethod method, Guid branchId);
    }
}
