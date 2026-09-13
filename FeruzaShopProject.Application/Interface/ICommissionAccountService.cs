using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;

namespace FeruzaShopProject.Application.Interface
{
    public interface ICommissionAccountService
    {
        Task<ApiResponse<List<CommissionAccountDto>>> GetAllAsync(Guid? branchId);
        Task<ApiResponse<CommissionAccountDto>> GetByIdAsync(Guid id);
        Task<ApiResponse<CommissionAccountDto>> GetByBranchAsync(Guid branchId);
        Task<ApiResponse<CommissionAccountDetailDto>> GetDetailAsync(Guid id, DateTime? startDate, DateTime? endDate);
        Task<ApiResponse<CommissionAccountDto>> CreateAsync(CreateCommissionAccountDto dto);
        Task<ApiResponse<CommissionLedgerEntryDto>> AddAllocationAsync(CreateCommissionAllocationDto dto, Guid? userId, string? userName);
        Task<ApiResponse<CommissionLedgerEntryDto>> UpdateAllocationAsync(UpdateCommissionAllocationDto dto);
        Task<ApiResponse<bool>> DeleteAllocationAsync(Guid entryId);
        Task<ApiResponse<bool>> SoftDeleteAsync(Guid id);

        /// <summary>
        /// Aligns the sale's commission ledger to <paramref name="newCommissionAmount"/>.
        /// Does not call SaveChanges. Returns an error message when blocked.
        /// </summary>
        Task<string?> ApplySaleCommissionAsync(
            Guid branchId,
            Guid transactionId,
            decimal newCommissionAmount,
            DateTime entryDate,
            string? productName,
            string? itemCode,
            Guid? userId,
            string? userName);
    }
}
