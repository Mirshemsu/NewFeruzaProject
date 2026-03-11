using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IDailyClosingService
    {
        // Sales closes the day
        Task<ApiResponse<DailyClosingDto>> CloseDailySalesAsync(CloseDailySalesDto dto);

        // Finance approves the closing
        Task<ApiResponse<DailyClosingDto>> ApproveDailyClosingAsync(ApproveDailyClosingDto dto);

        // Finance/Admin reopens a closed day (for corrections)
        Task<ApiResponse<DailyClosingDto>> ReopenDailySalesAsync(ReopenDailySalesDto dto);

        // Get closing status for a date
        Task<ApiResponse<DailyClosingDto>> GetClosingStatusAsync(Guid branchId, DateTime date);

        // Get all closings for a branch
        Task<ApiResponse<List<DailyClosingDto>>> GetBranchClosingsAsync(Guid branchId, DateTime? fromDate = null, DateTime? toDate = null);

        // Check if a date is closed for a branch
        Task<ApiResponse<bool>> IsDateClosedAsync(Guid branchId, DateTime date);
    }
}
