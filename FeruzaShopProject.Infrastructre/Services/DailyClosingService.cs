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
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class DailyClosingService : IDailyClosingService
    {
        private readonly ShopDbContext _context;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly IMapper _mapper;
        private readonly ILogger<DailyClosingService> _logger;

        public DailyClosingService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<DailyClosingService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context;
            _mapper = mapper;
            _logger = logger;
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        /// <summary>
        /// Sales closes the day - creates a pending closing record
        /// </summary>
        public async Task<ApiResponse<DailyClosingDto>> CloseDailySalesAsync(CloseDailySalesDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                // Validate branch
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);
                if (branch == null)
                    return ApiResponse<DailyClosingDto>.Fail("Branch not found");

                // Validate date
                if (dto.ClosingDate.Date > DateTime.UtcNow.Date)
                    return ApiResponse<DailyClosingDto>.Fail("Cannot close future dates");

                // Find existing DailyClosing for this date
                var existingClosing = await _context.DailyClosings
                    .FirstOrDefaultAsync(dc => dc.BranchId == dto.BranchId &&
                                              dc.ClosingDate.Date == dto.ClosingDate.Date &&
                                              dc.IsActive);

                // ========== NEW STATUS LOGIC ==========
                if (existingClosing != null)
                {
                    switch (existingClosing.Status)
                    {
                        case DailyClosingStatus.Approved:
                            return ApiResponse<DailyClosingDto>.Fail(
                                $"Date {dto.ClosingDate:yyyy-MM-dd} is already approved and locked.");

                        case DailyClosingStatus.Closed:
                            return ApiResponse<DailyClosingDto>.Fail(
                                $"Date {dto.ClosingDate:yyyy-MM-dd} is already closed. Wait for finance approval.");

                        case DailyClosingStatus.Pending:
                            // This is the normal case - update the existing record to Closed
                            existingClosing.Status = DailyClosingStatus.Closed;
                            existingClosing.ClosedAt = DateTime.UtcNow;
                            existingClosing.ClosedBy = userId;
                            existingClosing.Remarks = dto.Remarks;
                            existingClosing.UpdatedAt = DateTime.UtcNow;

                            await _context.SaveChangesAsync();
                            await transaction.CommitAsync();

                            var result = _mapper.Map<DailyClosingDto>(existingClosing);
                            _logger.LogInformation("Daily sales CLOSED for branch {BranchId} on {Date}",
                                dto.BranchId, dto.ClosingDate.Date);

                            return ApiResponse<DailyClosingDto>.Success(result,
                                "Daily sales closed successfully. Waiting for finance approval.");

                        case DailyClosingStatus.Rejected:
                            // If rejected, we can create a new closing
                            existingClosing.IsActive = false;
                            break;
                    }
                }

                // If no existing closing or it was rejected, get fresh data
                var dailySales = await _context.DailySales
                    .Where(ds => ds.BranchId == dto.BranchId &&
                                ds.SaleDate.Date == dto.ClosingDate.Date &&
                                ds.IsActive)
                    .ToListAsync();

                var totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                var totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                var totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                var totalSales = totalCash + totalBank + totalCredit;
                var totalTransactions = dailySales.Count;

                // Create NEW closing record with status = Closed
                var closing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    BranchId = dto.BranchId,
                    ClosingDate = dto.ClosingDate.Date,
                    ClosedAt = DateTime.UtcNow,
                    ClosedBy = userId,
                    Status = DailyClosingStatus.Closed,  // NEW: Set to Closed, not Pending
                    Remarks = dto.Remarks,

                    TotalTransactions = totalTransactions,
                    TotalSalesAmount = totalSales,
                    TotalCashAmount = totalCash,
                    TotalBankAmount = totalBank,
                    TotalCreditAmount = totalCredit,

                    CreatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.DailyClosings.AddAsync(closing);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var mappedResult = _mapper.Map<DailyClosingDto>(closing);
                _logger.LogInformation("Daily sales CLOSED for branch {BranchId} on {Date}",
                    dto.BranchId, dto.ClosingDate.Date);

                return ApiResponse<DailyClosingDto>.Success(mappedResult,
                    "Daily sales closed successfully. Waiting for finance approval.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error closing daily sales");
                return ApiResponse<DailyClosingDto>.Fail($"Error closing daily sales: {ex.Message}");
            }
        }
        /// <summary>
        /// Finance approves or rejects the closing
        /// </summary>
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

                // ========== NEW: Only Closed status can be approved ==========
                if (closing.Status != DailyClosingStatus.Closed)
                {
                    return ApiResponse<DailyClosingDto>.Fail(
                        $"Only Closed closings can be approved. Current status: {closing.Status}");
                }

                if (dto.IsApproved)
                {
                    if (!string.IsNullOrWhiteSpace(dto.CashBankTransactionId))
                        closing.CashBankTransactionId = dto.CashBankTransactionId;

                    if (!string.IsNullOrWhiteSpace(dto.BankTransferTransactionId))
                        closing.BankTransferTransactionId = dto.BankTransferTransactionId;

                    closing.Status = DailyClosingStatus.Approved;  // Now Approved
                    closing.ApprovedAt = DateTime.UtcNow;
                    closing.ApprovedBy = userId;
                    closing.Remarks = dto.Remarks;
                }
                else
                {
                    closing.Status = DailyClosingStatus.Rejected;  // Back to Rejected
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

        /// <summary>
        /// Transfer amount between cash and bank (can be done anytime before closing)
        /// </summary>
        public async Task<ApiResponse<DailyClosingDto>> TransferAmountAsync(TransferAmountDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var userId = await GetCurrentUserIdAsync();

                // Check if date is already closed and approved
                var isClosed = await IsDateClosedAsync(dto.BranchId, dto.TransferDate);
                if (isClosed.Data)
                {
                    return ApiResponse<DailyClosingDto>.Fail(
                        $"Cannot transfer for {dto.TransferDate:yyyy-MM-dd}. This date is already closed and approved.");
                }

                // ========== FIX: Don't check for existing pending closing ==========
                // We want to allow transfers even if a pending closing exists
                var dailyClosing = await _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Include(dc => dc.Approver)
                    .FirstOrDefaultAsync(dc => dc.BranchId == dto.BranchId &&
                                              dc.ClosingDate.Date == dto.TransferDate.Date &&
                                              dc.IsActive);

                if (dailyClosing != null)
                {
                    // Cannot transfer if Approved
                    if (dailyClosing.Status == DailyClosingStatus.Approved)
                    {
                        return ApiResponse<DailyClosingDto>.Fail(
                            $"Cannot transfer for {dto.TransferDate:yyyy-MM-dd}. This date is already approved and locked.");
                    }

                    // Can transfer if Pending or Closed
                    _logger.LogInformation("Transfer allowed. Current status: {Status}", dailyClosing.Status);
                }

                if (dailyClosing == null)
                {
                    // Calculate amounts from DailySales
                    var dailySales = await _context.DailySales
                        .Where(ds => ds.BranchId == dto.BranchId &&
                                    ds.SaleDate.Date == dto.TransferDate.Date &&
                                    ds.IsActive)
                        .ToListAsync();

                    var totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                    var totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                    var totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                    var totalSales = totalCash + totalBank + totalCredit;
                    var totalTransactions = dailySales.Count;

                    // Get branch for navigation
                    var branch = await _context.Branches.FindAsync(dto.BranchId);

                    // Get user for navigation
                    var user = await _context.Users.FindAsync(userId);

                    // ========== FIX: Create WITHOUT setting status ==========
                    // Status will default to Pending (0) which is correct
                    dailyClosing = new DailyClosing
                    {
                        Id = Guid.NewGuid(),
                        BranchId = dto.BranchId,
                        Branch = branch,
                        ClosingDate = dto.TransferDate.Date,
                        ClosedBy = userId,
                        Closer = user,
                        // Status NOT SET - will use default Pending
                        TotalTransactions = totalTransactions,
                        TotalSalesAmount = totalSales,
                        TotalCashAmount = totalCash,
                        TotalBankAmount = totalBank,
                        TotalCreditAmount = totalCredit,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _context.DailyClosings.AddAsync(dailyClosing);
                    await _context.SaveChangesAsync();
                }

                // Log current amounts
                _logger.LogInformation("Current DailyClosing - Cash: {Cash}, Bank: {Bank}, Status: {Status}",
                    dailyClosing.TotalCashAmount, dailyClosing.TotalBankAmount, dailyClosing.Status);

                // ========== FIX: Check if we can transfer based on status ==========
                // Only prevent transfers if the date is APPROVED (locked)
                // Allow transfers for Pending, Rejected, etc.
                if (dailyClosing.Status == DailyClosingStatus.Approved)
                {
                    return ApiResponse<DailyClosingDto>.Fail(
                        $"Cannot transfer for {dto.TransferDate:yyyy-MM-dd}. This date is already approved and locked.");
                }

                // Perform the transfer
                switch (dto.Direction)
                {
                    case TransferDirection.CashToBank:
                        if (dailyClosing.TotalCashAmount < dto.Amount)
                            return ApiResponse<DailyClosingDto>.Fail(
                                $"Insufficient cash amount. Available: {dailyClosing.TotalCashAmount}, Requested: {dto.Amount}");

                        dailyClosing.TotalCashAmount -= dto.Amount;
                        dailyClosing.TotalBankAmount += dto.Amount;
                        dailyClosing.CashBankTransactionId = dto.BankTransactionId;
                        break;

                    case TransferDirection.BankToCash:
                        if (dailyClosing.TotalBankAmount < dto.Amount)
                            return ApiResponse<DailyClosingDto>.Fail(
                                $"Insufficient bank amount. Available: {dailyClosing.TotalBankAmount}, Requested: {dto.Amount}");

                        dailyClosing.TotalBankAmount -= dto.Amount;
                        dailyClosing.TotalCashAmount += dto.Amount;
                        dailyClosing.BankTransferTransactionId = dto.BankTransactionId;
                        break;
                }

                // ========== FIX: DO NOT change the status ==========
                // Status remains whatever it was (Pending, Rejected, etc.)
                // Only update timestamp and remarks
                dailyClosing.UpdatedAt = DateTime.UtcNow;
                if (!string.IsNullOrWhiteSpace(dto.Remarks))
                {
                    dailyClosing.Remarks = string.IsNullOrEmpty(dailyClosing.Remarks)
                        ? dto.Remarks
                        : dailyClosing.Remarks + " | " + dto.Remarks;
                }

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Refresh with includes before mapping
                var refreshedClosing = await _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Include(dc => dc.Approver)
                    .FirstOrDefaultAsync(dc => dc.Id == dailyClosing.Id);

                var result = _mapper.Map<DailyClosingDto>(refreshedClosing);

                _logger.LogInformation("Transfer completed. Status unchanged: {Status}", refreshedClosing.Status);
                return ApiResponse<DailyClosingDto>.Success(result, "Transfer completed successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error transferring amount");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Update DailyClosing amounts (called by TransactionService)
        /// </summary>
        public async Task UpdateDailyClosingAmountsAsync(Guid branchId, DateTime date, PaymentMethod paymentMethod, decimal amount, bool isAddition)
        {
            try
            {
                // Check if date is already closed
                var isClosed = await IsDateClosedAsync(branchId, date);
                if (isClosed.Data)
                {
                    _logger.LogWarning("Attempted to update closed date {Date} for branch {BranchId}", date, branchId);
                    return;
                }

                var dailyClosing = await _context.DailyClosings
                    .FirstOrDefaultAsync(dc => dc.BranchId == branchId &&
                                              dc.ClosingDate.Date == date.Date &&
                                              dc.IsActive);

                if (dailyClosing == null)
                {
                    // Create new daily closing for tracking
                    dailyClosing = new DailyClosing
                    {
                        Id = Guid.NewGuid(),
                        BranchId = branchId,
                        ClosingDate = date.Date,
                        Status = DailyClosingStatus.Pending,
                        TotalTransactions = 0,
                        TotalSalesAmount = 0,
                        TotalCashAmount = 0,
                        TotalBankAmount = 0,
                        TotalCreditAmount = 0,
                        CreatedAt = DateTime.UtcNow,
                        IsActive = true
                    };
                    await _context.DailyClosings.AddAsync(dailyClosing);
                }

                decimal adjustment = isAddition ? amount : -amount;

                switch (paymentMethod)
                {
                    case PaymentMethod.Cash:
                        dailyClosing.TotalCashAmount += adjustment;
                        break;
                    case PaymentMethod.Bank:
                        dailyClosing.TotalBankAmount += adjustment;
                        break;
                    case PaymentMethod.Credit:
                        dailyClosing.TotalCreditAmount += adjustment;
                        break;
                }

                if (isAddition)
                {
                    dailyClosing.TotalSalesAmount += amount;
                    dailyClosing.TotalTransactions += 1;
                }
                else
                {
                    dailyClosing.TotalSalesAmount -= amount;
                    dailyClosing.TotalTransactions -= 1;
                }

                dailyClosing.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                _logger.LogDebug("Updated DailyClosing for {Date}: Cash={Cash}, Bank={Bank}",
                    date.Date, dailyClosing.TotalCashAmount, dailyClosing.TotalBankAmount);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating daily closing amounts");
                // Don't throw - we don't want to fail the main transaction
            }
        }

        /// <summary>
        /// Get closing status for a specific date
        /// </summary>
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
                {
                    // Return a "not closed" status
                    return ApiResponse<DailyClosingDto>.Success(null, "No closing record found for this date");
                }

                var result = _mapper.Map<DailyClosingDto>(closing);
                return ApiResponse<DailyClosingDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closing status");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Get all closings for a branch
        /// </summary>
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

        /// <summary>
        /// Check if a date is closed
        /// </summary>
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

        /// <summary>
        /// Reopen a closed date (admin only)
        /// </summary>
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

                // ========== FIX: Get the user for navigation property ==========
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                    return ApiResponse<DailyClosingDto>.Fail("Current user not found");

                // ========== FIX: Get branch for navigation property ==========
                var branch = await _context.Branches.FindAsync(closing.BranchId);

                // Create new pending closing with all required fields
                var newClosing = new DailyClosing
                {
                    Id = Guid.NewGuid(),
                    BranchId = closing.BranchId,
                    Branch = branch, // Set navigation property
                    ClosingDate = closing.ClosingDate,
                    ClosedAt = DateTime.UtcNow,
                    ClosedBy = userId, // REQUIRED - set the foreign key
                    Closer = user, // Set navigation property
                    Status = DailyClosingStatus.Pending,
                    Remarks = $"REOPENED: {dto.Reason}",
                    TotalTransactions = closing.TotalTransactions,
                    TotalSalesAmount = closing.TotalSalesAmount,
                    TotalCashAmount = closing.TotalCashAmount,
                    TotalBankAmount = closing.TotalBankAmount,
                    TotalCreditAmount = closing.TotalCreditAmount,
                    CashBankTransactionId = null, // Clear bank references
                    BankTransferTransactionId = null,
                    CreatedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    IsActive = true
                };

                await _context.DailyClosings.AddAsync(newClosing);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                // Refresh with includes for response
                var refreshedClosing = await _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Include(dc => dc.Approver)
                    .FirstOrDefaultAsync(dc => dc.Id == newClosing.Id);

                var result = _mapper.Map<DailyClosingDto>(refreshedClosing);
                _logger.LogInformation("Daily closing {ClosingId} reopened. New closing {NewClosingId}", closing.Id, newClosing.Id);

                return ApiResponse<DailyClosingDto>.Success(result, "Daily sales reopened. You can now edit sales for this date.");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error reopening daily sales");
                return ApiResponse<DailyClosingDto>.Fail($"Error: {ex.Message}");
            }
        }        /// <summary>
                 /// Get preview of daily closing before actually closing
                 /// </summary>
        public async Task<ApiResponse<DailyClosingPreviewDto>> GetClosingPreviewAsync(Guid branchId, DateTime date)
        {
            try
            {
                _logger.LogInformation("Generating closing preview for branch {BranchId} on {Date}", branchId, date);

                // Get branch info with Include
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive);

                if (branch == null)
                    return ApiResponse<DailyClosingPreviewDto>.Fail("Branch not found");

                // Get all daily sales for this date with ALL necessary includes
                var dailySales = await _context.DailySales
                    .Include(ds => ds.Product)
                        .ThenInclude(p => p.Category) // Include category
                    .Include(ds => ds.Branch) // Include branch
                    .Include(ds => ds.Customer) // Include customer
                    .Include(ds => ds.Painter) // Include painter
                    .Include(ds => ds.Transaction) // Include transaction for date
                    .Where(ds => ds.BranchId == branchId &&
                                ds.SaleDate.Date == date.Date &&
                                ds.IsActive)
                    .OrderBy(ds => ds.CreatedAt)
                    .ToListAsync();

                // Calculate totals
                var totalTransactions = dailySales.Count;
                var totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                var totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                var totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                var totalSales = totalCash + totalBank + totalCredit;

                // Check if already closed
                var existingClosing = await _context.DailyClosings
                    .FirstOrDefaultAsync(dc => dc.BranchId == branchId &&
                                              dc.ClosingDate.Date == date.Date &&
                                              dc.IsActive);

                // Calculate transfers (if you have transfer history)
                decimal transferredFromCash = 0;
                decimal transferredFromBank = 0;

                // Map daily sales to DTOs with all fields properly populated
                var transactionDtos = dailySales.Select(ds => new DailySalesItemDto
                {
                    Id = ds.Id,
                    TransactionId = ds.TransactionId,
                    SaleDate = ds.SaleDate,
                    TransactionDate = ds.Transaction?.TransactionDate ?? ds.SaleDate,
                    BranchId = ds.BranchId,
                    BranchName = ds.Branch?.Name,
                    ProductId = ds.ProductId,
                    ProductName = ds.Product?.Name,
                    CategoryName = ds.Product?.Category?.Name,
                    ItemCode = ds.Product?.ItemCode,
                    Quantity = ds.Quantity,
                    UnitPrice = ds.UnitPrice,
                    TotalAmount = ds.TotalAmount,
                    BuyingPrice = ds.Product?.BuyingPrice ?? 0,
                    CostAmount = (ds.Product?.BuyingPrice ?? 0) * ds.Quantity,
                    Profit = ds.TotalAmount - ((ds.Product?.BuyingPrice ?? 0) * ds.Quantity) - ds.CommissionAmount,
                    ProfitMargin = ds.TotalAmount > 0
                        ? ((ds.TotalAmount - ((ds.Product?.BuyingPrice ?? 0) * ds.Quantity) - ds.CommissionAmount) / ds.TotalAmount) * 100
                        : 0,
                    PaymentMethod = ds.PaymentMethod,
                    CommissionRate = ds.CommissionRate,
                    CommissionAmount = ds.CommissionAmount,
                    CommissionPaid = ds.CommissionPaid,
                    CustomerId = ds.CustomerId,
                    CustomerName = ds.Customer?.Name,
                    PainterId = ds.PainterId,
                    PainterName = ds.Painter?.Name,
                    IsPartialPayment = ds.IsPartialPayment,
                    IsCreditPayment = ds.IsCreditPayment,
                    Remark = ds.Remark
                }).ToList();

                var preview = new DailyClosingPreviewDto
                {
                    BranchId = branchId,
                    BranchName = branch.Name,
                    ClosingDate = date.Date,
                    TotalTransactions = totalTransactions,
                    TotalSalesAmount = totalSales,
                    TotalCashAmount = totalCash,
                    TotalBankAmount = totalBank,
                    TotalCreditAmount = totalCredit,
                    TotalTransferredFromCash = transferredFromCash,
                    TotalTransferredFromBank = transferredFromBank,
                    HasPendingClosing = existingClosing != null && existingClosing.Status == DailyClosingStatus.Pending,
                    IsClosed = existingClosing != null && existingClosing.Status == DailyClosingStatus.Approved,
                    CurrentStatus = existingClosing?.Status,
                    Transactions = transactionDtos,
                    TodayTransfers = new List<TransferSummaryDto>() // Populate from transfer history if available
                };

                _logger.LogInformation("Closing preview generated for branch {BranchId}: {Transactions} transactions, Total: {Total}",
                    branchId, totalTransactions, totalSales);

                return ApiResponse<DailyClosingPreviewDto>.Success(preview);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating closing preview");
                return ApiResponse<DailyClosingPreviewDto>.Fail($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Admin view - get closing status for all branches on a specific date
        /// </summary>
        public async Task<ApiResponse<AllBranchesClosingDto>> GetAllBranchesClosingAsync(DateTime date)
        {
            try
            {
                _logger.LogInformation("Admin fetching all branches closing for {Date}", date);

                // Get all active branches
                var branches = await _context.Branches
                    .Where(b => b.IsActive)
                    .ToListAsync();

                var branchSummaries = new List<BranchClosingSummaryDto>();
                var totalSalesAllBranches = 0m;
                var totalCashAllBranches = 0m;
                var totalBankAllBranches = 0m;
                var totalCreditAllBranches = 0m;
                var closedCount = 0;
                var pendingCount = 0;

                foreach (var branch in branches)
                {
                    // ========== FIX: Get closing data separately to avoid complex joins ==========
                    DailyClosing? closing = null;

                    try
                    {
                        closing = await _context.DailyClosings
                            .Include(dc => dc.Closer)
                            .Include(dc => dc.Approver)
                            .Where(dc => dc.BranchId == branch.Id &&
                                        dc.ClosingDate.Date == date.Date &&
                                        dc.IsActive)
                            .OrderByDescending(dc => dc.CreatedAt)
                            .FirstOrDefaultAsync();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error fetching closing for branch {BranchId}", branch.Id);
                        // Continue with null closing
                    }

                    decimal totalCash = 0;
                    decimal totalBank = 0;
                    decimal totalCredit = 0;
                    decimal totalSales = 0;

                    if (closing != null)
                    {
                        // Use the amounts from DailyClosing (these already include transfers)
                        totalCash = closing.TotalCashAmount;
                        totalBank = closing.TotalBankAmount;
                        totalCredit = closing.TotalCreditAmount;
                        totalSales = closing.TotalSalesAmount;

                        // Update status counts
                        if (closing.Status == DailyClosingStatus.Approved)
                            closedCount++;
                        else if (closing.Status == DailyClosingStatus.Pending ||
                                 closing.Status == DailyClosingStatus.Closed)
                            pendingCount++;
                    }
                    else
                    {
                        // If no closing record, fallback to DailySales
                        try
                        {
                            var dailySales = await _context.DailySales
                                .Where(ds => ds.BranchId == branch.Id &&
                                            ds.SaleDate.Date == date.Date &&
                                            ds.IsActive)
                                .ToListAsync();

                            totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                            totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                            totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                            totalSales = totalCash + totalBank + totalCredit;
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Error fetching daily sales for branch {BranchId}", branch.Id);
                            // Use zeros if error
                        }
                    }

                    // Update totals
                    totalSalesAllBranches += totalSales;
                    totalCashAllBranches += totalCash;
                    totalBankAllBranches += totalBank;
                    totalCreditAllBranches += totalCredit;

                    branchSummaries.Add(new BranchClosingSummaryDto
                    {
                        BranchId = branch.Id,
                        BranchName = branch.Name,
                        Date = date.Date,
                        IsClosed = closing != null && closing.Status == DailyClosingStatus.Approved,
                        Status = closing?.Status,
                        ClosedAt = closing?.ClosedAt,
                        ClosedBy = closing?.Closer?.Name,
                        TotalSales = totalSales,
                        TotalCash = totalCash,
                        TotalBank = totalBank,
                        TotalCredit = totalCredit,
                        CashBankTransactionId = closing?.CashBankTransactionId,
                        BankTransferTransactionId = closing?.BankTransferTransactionId
                    });
                }

                var result = new AllBranchesClosingDto
                {
                    Date = date.Date,
                    Branches = branchSummaries,
                    TotalBranches = branches.Count,
                    ClosedBranches = closedCount,
                    PendingBranches = pendingCount,
                    TotalSalesAllBranches = totalSalesAllBranches,
                    TotalCashAllBranches = totalCashAllBranches,
                    TotalBankAllBranches = totalBankAllBranches,
                    TotalCreditAllBranches = totalCreditAllBranches
                };

                return ApiResponse<AllBranchesClosingDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all branches closing");
                return ApiResponse<AllBranchesClosingDto>.Fail($"Error: {ex.Message}");
            }
        }
        /// <summary>
        /// Admin view - get detailed closing for a specific branch
        /// </summary>
        public async Task<ApiResponse<BranchClosingSummaryDto>> GetBranchClosingDetailAsync(Guid branchId, DateTime date)
        {
            try
            {
                var branch = await _context.Branches
                    .FirstOrDefaultAsync(b => b.Id == branchId && b.IsActive);

                if (branch == null)
                    return ApiResponse<BranchClosingSummaryDto>.Fail("Branch not found");

                // Get daily sales
                var dailySales = await _context.DailySales
                    .Where(ds => ds.BranchId == branchId &&
                                ds.SaleDate.Date == date.Date &&
                                ds.IsActive)
                    .ToListAsync();

                var totalCash = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Cash).Sum(ds => ds.TotalAmount);
                var totalBank = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Bank).Sum(ds => ds.TotalAmount);
                var totalCredit = dailySales.Where(ds => ds.PaymentMethod == PaymentMethod.Credit).Sum(ds => ds.TotalAmount);
                var totalSales = totalCash + totalBank + totalCredit;

                // Get closing
                var closing = await _context.DailyClosings
                    .Include(dc => dc.Closer)
                    .Where(dc => dc.BranchId == branchId &&
                                dc.ClosingDate.Date == date.Date &&
                                dc.IsActive)
                    .OrderByDescending(dc => dc.CreatedAt)
                    .FirstOrDefaultAsync();

                var result = new BranchClosingSummaryDto
                {
                    BranchId = branch.Id,
                    BranchName = branch.Name,
                    Date = date.Date,
                    IsClosed = closing != null && closing.Status == DailyClosingStatus.Approved,
                    Status = closing?.Status,
                    ClosedAt = closing?.ClosedAt,
                    ClosedBy = closing?.Closer?.Name,
                    TotalSales = totalSales,
                    TotalCash = totalCash,
                    TotalBank = totalBank,
                    TotalCredit = totalCredit,
                    CashBankTransactionId = closing?.CashBankTransactionId,
                    BankTransferTransactionId = closing?.BankTransferTransactionId
                };

                return ApiResponse<BranchClosingSummaryDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branch closing detail");
                return ApiResponse<BranchClosingSummaryDto>.Fail($"Error: {ex.Message}");
            }
        }

        /// <summary>
        /// Admin view - get closings by date range
        /// </summary>
        public async Task<ApiResponse<List<BranchClosingSummaryDto>>> GetClosingsByDateRangeAsync(DateRangeDto dto)
        {
            try
            {
                var query = _context.DailyClosings
                    .Include(dc => dc.Branch)
                    .Include(dc => dc.Closer)
                    .Where(dc => dc.ClosingDate >= dto.StartDate.Date &&
                                dc.ClosingDate <= dto.EndDate.Date &&
                                dc.IsActive)
                    .AsQueryable();

                if (dto.BranchId.HasValue)
                {
                    query = query.Where(dc => dc.BranchId == dto.BranchId.Value);
                }

                var closings = await query
                    .OrderBy(dc => dc.Branch.Name)
                    .ThenBy(dc => dc.ClosingDate)
                    .ToListAsync();

                var result = closings.Select(c => new BranchClosingSummaryDto
                {
                    BranchId = c.BranchId,
                    BranchName = c.Branch?.Name,
                    Date = c.ClosingDate,
                    IsClosed = c.Status == DailyClosingStatus.Approved,
                    Status = c.Status,
                    ClosedAt = c.ClosedAt,
                    ClosedBy = c.Closer?.Name,
                    TotalSales = c.TotalSalesAmount,
                    TotalCash = c.TotalCashAmount,
                    TotalBank = c.TotalBankAmount,
                    TotalCredit = c.TotalCreditAmount,
                    CashBankTransactionId = c.CashBankTransactionId,
                    BankTransferTransactionId = c.BankTransferTransactionId
                }).ToList();

                return ApiResponse<List<BranchClosingSummaryDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closings by date range");
                return ApiResponse<List<BranchClosingSummaryDto>>.Fail($"Error: {ex.Message}");
            }
        }

        #region Helper Methods

        private async Task<Guid> GetCurrentUserIdAsync()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("sub")?.Value
                ?? _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId))
                throw new UnauthorizedAccessException("Invalid user");

            return userId;
        }

        #endregion
    }
}