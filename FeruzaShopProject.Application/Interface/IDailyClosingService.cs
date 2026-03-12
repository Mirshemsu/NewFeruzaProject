using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IDailyClosingService
    {
        // Preview before closing (for sales/manager)
        Task<ApiResponse<DailyClosingPreviewDto>> GetClosingPreviewAsync(Guid branchId, DateTime date);

        // Admin view all branches
        Task<ApiResponse<AllBranchesClosingDto>> GetAllBranchesClosingAsync(DateTime date);

        // Admin view specific branch
        Task<ApiResponse<BranchClosingSummaryDto>> GetBranchClosingDetailAsync(Guid branchId, DateTime date);

        // Get closings by date range (admin)
        Task<ApiResponse<List<BranchClosingSummaryDto>>> GetClosingsByDateRangeAsync(DateRangeDto dto);

        // Sales closes the day
        Task<ApiResponse<DailyClosingDto>> CloseDailySalesAsync(CloseDailySalesDto dto);

        // Finance approves the closing
        Task<ApiResponse<DailyClosingDto>> ApproveDailyClosingAsync(ApproveDailyClosingDto dto);

        // Transfer between cash and bank
        Task<ApiResponse<DailyClosingDto>> TransferAmountAsync(TransferAmountDto dto);

        // Get closing status for a date
        Task<ApiResponse<DailyClosingDto>> GetClosingStatusAsync(Guid branchId, DateTime date);

        // Get all closings for a branch
        Task<ApiResponse<List<DailyClosingDto>>> GetBranchClosingsAsync(Guid branchId, DateTime? fromDate = null, DateTime? toDate = null);

        // Check if a date is closed for a branch
        Task<ApiResponse<bool>> IsDateClosedAsync(Guid branchId, DateTime date);

        // Reopen a closed date (admin only)
        Task<ApiResponse<DailyClosingDto>> ReopenDailySalesAsync(ReopenDailySalesDto dto);

        // Helper method to update DailyClosing amounts
        Task UpdateDailyClosingAmountsAsync(Guid branchId, DateTime date, PaymentMethod paymentMethod, decimal amount, bool isAddition);
    }
}