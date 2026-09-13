using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class BankAccountService : IBankAccountService
    {
        private readonly ShopDbContext _context;
        private readonly ILogger<BankAccountService> _logger;

        public BankAccountService(ShopDbContext context, ILogger<BankAccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<List<BankAccountDto>>> GetAllAsync(Guid? branchId, DateTime? startDate, DateTime? endDate)
        {
            try
            {
                var query = _context.BankAccounts
                    .Include(a => a.Branch)
                    .AsQueryable();

                if (branchId.HasValue)
                    query = query.Where(a => a.BranchId == branchId.Value);

                var accounts = await query
                    .OrderBy(a => a.Branch.Name)
                    .ThenBy(a => a.BankName)
                    .ToListAsync();

                var result = new List<BankAccountDto>();
                foreach (var account in accounts)
                    result.Add(await MapAccountAsync(account, startDate, endDate));

                return ApiResponse<List<BankAccountDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error loading bank accounts");
                return ApiResponse<List<BankAccountDto>>.Fail("Error loading bank accounts");
            }
        }

        public async Task<ApiResponse<BankAccountDto>> GetByIdAsync(Guid id)
        {
            var account = await _context.BankAccounts
                .IgnoreQueryFilters()
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null)
                return ApiResponse<BankAccountDto>.Fail("Bank account not found");

            return ApiResponse<BankAccountDto>.Success(await MapAccountAsync(account, null, null));
        }

        public async Task<ApiResponse<BankAccountStatementDto>> GetStatementAsync(Guid id, DateTime? startDate, DateTime? endDate)
        {
            var account = await _context.BankAccounts
                .IgnoreQueryFilters()
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (account == null)
                return ApiResponse<BankAccountStatementDto>.Fail("Bank account not found");

            var entries = await QueryBankEntries(id, startDate, endDate)
                .OrderByDescending(e => e.Date)
                .ToListAsync();

            var dto = new BankAccountStatementDto
            {
                Account = await MapAccountAsync(account, startDate, endDate),
                StartDate = startDate,
                EndDate = endDate,
                TotalReceived = entries.Sum(e => e.Amount),
                EntryCount = entries.Count,
                Entries = entries
            };

            return ApiResponse<BankAccountStatementDto>.Success(dto);
        }

        public async Task<ApiResponse<BankAccountDto>> CreateAsync(CreateBankAccountDto dto)
        {
            try
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);
                if (branch == null)
                    return ApiResponse<BankAccountDto>.Fail("Invalid or inactive branch");

                var exists = await _context.BankAccounts.AnyAsync(a =>
                    a.BranchId == dto.BranchId &&
                    a.AccountNumber == dto.AccountNumber.Trim());
                if (exists)
                    return ApiResponse<BankAccountDto>.Fail("This account number already exists for the branch");

                var account = new BankAccount
                {
                    BankName = dto.BankName.Trim(),
                    AccountNumber = dto.AccountNumber.Trim(),
                    AccountOwner = dto.AccountOwner.Trim(),
                    BranchId = dto.BranchId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.BankAccounts.AddAsync(account);
                await _context.SaveChangesAsync();
                account.Branch = branch;

                return ApiResponse<BankAccountDto>.Success(
                    await MapAccountAsync(account, null, null),
                    "Bank account created successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating bank account");
                return ApiResponse<BankAccountDto>.Fail("Error creating bank account");
            }
        }

        public async Task<ApiResponse<BankAccountDto>> UpdateAsync(UpdateBankAccountDto dto)
        {
            try
            {
                var account = await _context.BankAccounts
                    .Include(a => a.Branch)
                    .FirstOrDefaultAsync(a => a.Id == dto.Id);

                if (account == null)
                    return ApiResponse<BankAccountDto>.Fail("Bank account not found");

                if (!string.IsNullOrWhiteSpace(dto.BankName))
                    account.BankName = dto.BankName.Trim();
                if (!string.IsNullOrWhiteSpace(dto.AccountNumber))
                    account.AccountNumber = dto.AccountNumber.Trim();
                if (!string.IsNullOrWhiteSpace(dto.AccountOwner))
                    account.AccountOwner = dto.AccountOwner.Trim();

                if (dto.BranchId.HasValue && dto.BranchId.Value != account.BranchId)
                {
                    var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId.Value && b.IsActive);
                    if (branch == null)
                        return ApiResponse<BankAccountDto>.Fail("Invalid or inactive branch");
                    account.BranchId = dto.BranchId.Value;
                    account.Branch = branch;
                }

                account.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<BankAccountDto>.Success(
                    await MapAccountAsync(account, null, null),
                    "Bank account updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank account");
                return ApiResponse<BankAccountDto>.Fail("Error updating bank account");
            }
        }

        public async Task<ApiResponse<bool>> DeleteAsync(Guid id)
        {
            try
            {
                var account = await _context.BankAccounts.FirstOrDefaultAsync(a => a.Id == id);
                if (account == null)
                    return ApiResponse<bool>.Fail("Bank account not found");

                account.Deactivate();
                await _context.SaveChangesAsync();
                return ApiResponse<bool>.Success(true, "Bank account deactivated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting bank account");
                return ApiResponse<bool>.Fail("Error deleting bank account");
            }
        }

        public async Task<string?> ValidateForPaymentAsync(Guid? bankAccountId, PaymentMethod method, Guid branchId)
        {
            if (method != PaymentMethod.Bank)
                return null;

            if (!bankAccountId.HasValue || bankAccountId.Value == Guid.Empty)
                return "Bank account is required for bank payments";

            var account = await _context.BankAccounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == bankAccountId.Value && a.IsActive);

            if (account == null)
                return "Selected bank account was not found or is inactive";

            if (account.BranchId != branchId)
                return "Selected bank account does not belong to this branch";

            return null;
        }

        private IQueryable<BankAccountStatementEntryDto> QueryBankEntries(Guid accountId, DateTime? startDate, DateTime? endDate)
        {
            var start = startDate?.Date;
            var end = endDate?.Date.AddDays(1);

            return _context.DailySales
                .AsNoTracking()
                .Where(ds => ds.IsActive &&
                             ds.PaymentMethod == PaymentMethod.Bank &&
                             ds.BankAccountId == accountId &&
                             (!start.HasValue || ds.SaleDate >= start.Value) &&
                             (!end.HasValue || ds.SaleDate < end.Value))
                .Select(ds => new BankAccountStatementEntryDto
                {
                    Id = ds.Id,
                    Date = ds.SaleDate,
                    Source = ds.IsCreditPayment ? "Credit payment" : "Bank sale",
                    TransactionId = ds.TransactionId,
                    ProductName = ds.Product != null ? ds.Product.Name : null,
                    ItemCode = ds.Transaction != null ? ds.Transaction.ItemCode : (ds.Product != null ? ds.Product.ItemCode : null),
                    CustomerName = ds.Customer != null ? ds.Customer.Name : null,
                    Amount = ds.TotalAmount,
                    Remark = ds.Remark
                });
        }

        private async Task<BankAccountDto> MapAccountAsync(BankAccount account, DateTime? startDate, DateTime? endDate)
        {
            var entries = await QueryBankEntries(account.Id, startDate, endDate).ToListAsync();
            return new BankAccountDto
            {
                Id = account.Id,
                BankName = account.BankName,
                AccountNumber = account.AccountNumber,
                AccountOwner = account.AccountOwner,
                BranchId = account.BranchId,
                BranchName = account.Branch?.Name ?? string.Empty,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                IsActive = account.IsActive,
                TransactionCount = entries.Count,
                ReceivedAmount = entries.Sum(e => e.Amount)
            };
        }
    }
}
