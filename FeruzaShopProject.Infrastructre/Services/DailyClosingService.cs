using AutoMapper;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class DailyClosingService : IDailyClosingService
    {
        private readonly ShopDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILogger<DailyClosingService> _logger;
        private readonly IMapper _mapper;

        public DailyClosingService(
            ShopDbContext context,
            IHttpContextAccessor httpContextAccessor,
            IMapper mapper,
            ILogger<DailyClosingService> logger)
        {
            _context = context;
            _httpContextAccessor = httpContextAccessor;
            _logger = logger;
            _mapper = mapper;
        }

        public async Task<ApiResponse<DailyClosingDto>> CloseDailySalesAsync(CloseDailySalesDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                // Get current user
                var userId = await GetCurrentUserIdAsync();

                // Validate branch
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);
                if (branch == null)
                    return ApiResponse<DailyClosingDto>.Fail("Branch not found");

                // Validate date (cannot close future dates)
                if (dto.ClosingDate.Date > DateTime.UtcNow.Date)
                    return ApiResponse<DailyClosingDto>.Fail("Cannot close future dates");

                // Validate date (cannot close dates older than 7 days without special permission)
                if (dto.ClosingDate.Date < DateTime.UtcNow.Date.AddDays(-7))
                    return ApiResponse<DailyClosingDto>.Fail("Cannot close dates older than 7 days. Contact admin.");

                // Check if already closed
                var existingClosing = await _context.DailyClosings
                    .FirstOrDefaultAsync(dc => dc.BranchId == dto.BranchId &&
                                              dc.ClosingDate.Date == dto.ClosingDate.Date);

                if (existingClosing != null)
                {
                    if (existingClosing.Status == DailyClosingStatus.Approved)
                        return ApiResponse<DailyClosingDto>.Fail($"Date {dto.ClosingDate:yyyy-MM-dd} is already closed and approved");

                    if (existingClosing.Status == DailyClosingStatus.Pending)
                        return ApiResponse<DailyClosingDto>.Fail($"Date {dto.ClosingDate:yyyy-MM-dd} is already closed and pending approval");

                    // If rejected, allow re-closing
                    if (existingClosing.Status == DailyClosingStatus.Rejected)
                    {
                        // Mark old as inactive
                        existingClosing.IsActive = false;
                    }
                }

                // Get all sales for this date
                var dailySales = await _context.DailySales
                    .Where(ds => ds.BranchId == dto.BranchId &&
                                ds.SaleDate.Date == dto.ClosingDate.Date &&
                                ds.IsActive)
                    .ToListAsync();

                if (!dailySales.Any())
                    return ApiResponse<DailyClosingDto>.Fail($"No sales found for date {dto.ClosingDate:yyyy-MM-dd}");

                // Calculate totals
                var totalSales = dailySales.Sum(ds => ds.TotalAmount);
                var totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                var totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                var totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                var totalTransactions = dailySales.Count;

                // Create closing record
                var closing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    BranchId = dto.BranchId,
                    ClosingDate = dto.ClosingDate.Date,
                    ClosedAt = DateTime.UtcNow,
                    ClosedBy = userId,
                    Status = DailyClosingStatus.Pending,
                    Remarks = dto.Remarks,
                    TotalSalesAmount = totalSales,
                    TotalCashAmount = totalCash,
                    TotalBankAmount = totalBank,
                    TotalCreditAmount = totalCredit,
                    TotalTransactions = totalTransactions,
                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.DailyClosings.AddAsync(closing);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<DailyClosingDto>(closing);
                _logger.LogInformation("Daily sales closed for branch {BranchId} on {Date}", dto.BranchId, dto.ClosingDate.Date);

                return ApiResponse<DailyClosingDto>.Success(result, "Daily sales closed successfully. Waiting for finance approval.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error closing daily sales");
                return ApiResponse<DailyClosingDto>.Fail($"Error closing daily sales: {ex.Message}");
            }
        }

        public async Task<ApiResponse<DailyClosingDto>> ApproveDailyClosingAsync(ApproveDailyClosingDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var closing = await _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .FirstOrDefaultAsync(dc => dc.Id == dto.ClosingId && dc.IsActive);

                if (closing == null)
                    return ApiResponse<DailyClosingDto>.Fail("Closing record not found");

                if (closing.Status != DailyClosingStatus.Pending)
                    return ApiResponse<DailyClosingDto>.Fail($"This closing is already {closing.Status}");

                if (dto.IsApproved)
                {
                    closing.Status = DailyClosingStatus.Approved;
                    closing.ApprovedAt = DateTime.UtcNow;
                    closing.ApprovedBy = userId;

                    // OPTIONAL: Lock the DailySales records for this date
                    // You could add an IsLocked flag to DailySales
                }
                else
                {
                    closing.Status = DailyClosingStatus.Rejected;
                    closing.Remarks = dto.Remarks ?? "Rejected by finance";
                }

                closing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var result = _mapper.Map<DailyClosingDto>(closing);
                _logger.LogInformation("Daily closing {ClosingId} {Status}", closing.Id, closing.Status);

                return ApiResponse<DailyClosingDto>.Success(result,
                    dto.IsApproved ? "Daily closing approved" : "Daily closing rejected");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error approving daily closing");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<DailyClosingDto>> ReopenDailySalesAsync(ReopenDailySalesDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                var closing = await _context.DailyClosings
                    .FirstOrDefaultAsync(dc => dc.Id == dto.ClosingId && dc.IsActive);

                if (closing == null)
                    return ApiResponse<DailyClosingDto>.Fail("Closing record not found");

                if (closing.Status != DailyClosingStatus.Approved)
                    return ApiResponse<DailyClosingDto>.Fail("Only approved closings can be reopened");

                // Mark current as inactive
                closing.IsActive = false;
                closing.UpdatedAt = DateTime.UtcNow;

                // Create new pending closing (or just allow edits)
                // For simplicity, we'll just mark as rejected with reopen reason
                closing.Status = DailyClosingStatus.Rejected;
                closing.Remarks = $"REOPENED: {dto.Reason}";
                closing.ApprovedAt = null;
                closing.ApprovedBy = null;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.LogInformation("Daily closing {ClosingId} reopened", closing.Id);
                return ApiResponse<DailyClosingDto>.Success(_mapper.Map<DailyClosingDto>(closing),
                    "Daily sales reopened. You can now edit sales for this date.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error reopening daily sales");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<DailyClosingDto>> GetClosingStatusAsync(Guid branchId, DateTime date)
        {
            try
            {
                var closing = await _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Include(dc => dc.Approver)
                    .Where(dc => dc.BranchId == branchId &&
                                dc.ClosingDate.Date == date.Date &&
                                dc.IsActive)
                    .OrderByDescending(dc => dc.CreatedAt)
                    .FirstOrDefaultAsync();

                if (closing == null)
                    return ApiResponse<DailyClosingDto>.Fail("No closing record found for this date");

                var result = _mapper.Map<DailyClosingDto>(closing);
                return ApiResponse<DailyClosingDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closing status");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<List<DailyClosingDto>>> GetBranchClosingsAsync(Guid branchId, DateTime? fromDate = null, DateTime? toDate = null)
        {
            try
            {
                var query = _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Include(dc => dc.Approver)
                    .Where(dc => dc.BranchId == branchId && dc.IsActive)
                    .AsQueryable();

                if (fromDate.HasValue)
                    query = query.Where(dc => dc.ClosingDate >= fromDate.Value.Date);

                if (toDate.HasValue)
                    query = query.Where(dc => dc.ClosingDate <= toDate.Value.Date);

                var closings = await query
                    .OrderByDescending(dc => dc.ClosingDate)
                    .ToListAsync();

                var result = _mapper.Map<List<DailyClosingDto>>(closings);
                return ApiResponse<List<DailyClosingDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branch closings");
                return ApiResponse<List<DailyClosingDto>>.Fail($"Error: {ex.Message}");
            }
        }

        public async Task<ApiResponse<bool>> IsDateClosedAsync(Guid branchId, DateTime date)
        {
            try
            {
                var isClosed = await _context.DailyClosings
                    .AnyAsync(dc => dc.BranchId == branchId &&
                                   dc.ClosingDate.Date == date.Date &&
                                   dc.IsActive &&
                                   dc.Status == DailyClosingStatus.Approved);

                return ApiResponse<bool>.Success(isClosed);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if date is closed");
                return ApiResponse<bool>.Fail($"Error: {ex.Message}");
            }
        }

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid user");

            return userId;
        }
    }
}
