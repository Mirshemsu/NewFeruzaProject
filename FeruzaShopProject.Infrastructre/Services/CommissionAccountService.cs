using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Data;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace FeruzaShopProject.Infrastructre.Services
{
    public class CommissionAccountService : ICommissionAccountService
    {
        private readonly ShopDbContext _context;
        private readonly ILogger<CommissionAccountService> _logger;

        public CommissionAccountService(ShopDbContext context, ILogger<CommissionAccountService> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<ApiResponse<List<CommissionAccountDto>>> GetAllAsync(Guid? branchId)
        {
            var query = _context.CommissionAccounts
                .Include(a => a.Branch)
                .Include(a => a.Entries.Where(e => e.IsActive))
                .AsQueryable();

            if (branchId.HasValue)
                query = query.Where(a => a.BranchId == branchId.Value);

            var accounts = await query
                .OrderBy(a => a.Branch.Name)
                .ToListAsync();

            return ApiResponse<List<CommissionAccountDto>>.Success(
                accounts.Select(MapAccount).ToList());
        }

        public async Task<ApiResponse<CommissionAccountDto>> GetByIdAsync(Guid id)
        {
            var account = await LoadAccountAsync(id, includeInactive: true);
            if (account == null)
                return ApiResponse<CommissionAccountDto>.Fail("Commission account not found");

            return ApiResponse<CommissionAccountDto>.Success(MapAccount(account));
        }

        public async Task<ApiResponse<CommissionAccountDto>> GetByBranchAsync(Guid branchId)
        {
            var account = await _context.CommissionAccounts
                .Include(a => a.Branch)
                .Include(a => a.Entries.Where(e => e.IsActive))
                .FirstOrDefaultAsync(a => a.BranchId == branchId && a.IsActive);

            if (account == null)
                return ApiResponse<CommissionAccountDto>.Fail("No active commission account for this branch");

            return ApiResponse<CommissionAccountDto>.Success(MapAccount(account));
        }

        public async Task<ApiResponse<CommissionAccountDetailDto>> GetDetailAsync(Guid id, DateTime? startDate, DateTime? endDate)
        {
            var account = await LoadAccountAsync(id, includeInactive: true);
            if (account == null)
                return ApiResponse<CommissionAccountDetailDto>.Fail("Commission account not found");

            var remaining = Remaining(account);
            var start = startDate?.Date;
            var end = endDate?.Date.AddDays(1);

            var entries = account.Entries
                .Where(e => e.IsActive)
                .Where(e => !start.HasValue || e.EntryDate >= start.Value)
                .Where(e => !end.HasValue || e.EntryDate < end.Value)
                .OrderByDescending(e => e.EntryDate)
                .ThenByDescending(e => e.CreatedAt)
                .Select(e => MapEntry(e, remaining))
                .ToList();

            return ApiResponse<CommissionAccountDetailDto>.Success(new CommissionAccountDetailDto
            {
                Account = MapAccount(account),
                StartDate = startDate,
                EndDate = endDate,
                Entries = entries
            });
        }

        public async Task<ApiResponse<CommissionAccountDto>> CreateAsync(CreateCommissionAccountDto dto)
        {
            try
            {
                var branch = await _context.Branches.FirstOrDefaultAsync(b => b.Id == dto.BranchId && b.IsActive);
                if (branch == null)
                    return ApiResponse<CommissionAccountDto>.Fail("Invalid or inactive branch");

                var exists = await _context.CommissionAccounts.AnyAsync(a => a.BranchId == dto.BranchId && a.IsActive);
                if (exists)
                    return ApiResponse<CommissionAccountDto>.Fail("This branch already has an active commission account");

                var account = new CommissionAccount
                {
                    BranchId = dto.BranchId,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CommissionAccounts.AddAsync(account);
                await _context.SaveChangesAsync();
                account.Branch = branch;

                return ApiResponse<CommissionAccountDto>.Success(MapAccount(account), "Commission account created");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating commission account");
                return ApiResponse<CommissionAccountDto>.Fail("Error creating commission account");
            }
        }

        public async Task<ApiResponse<CommissionLedgerEntryDto>> AddAllocationAsync(CreateCommissionAllocationDto dto, Guid? userId, string? userName)
        {
            try
            {
                var account = await LoadAccountAsync(dto.CommissionAccountId, includeInactive: false);
                if (account == null)
                    return ApiResponse<CommissionLedgerEntryDto>.Fail("Commission account not found or is inactive");

                var entry = new CommissionLedgerEntry
                {
                    CommissionAccountId = account.Id,
                    EntryType = CommissionLedgerEntryType.Allocation,
                    SignedAmount = decimal.Round(dto.Amount, 2),
                    EntryDate = dto.EntryDate.Date,
                    CheckNumber = string.IsNullOrWhiteSpace(dto.CheckNumber) ? null : dto.CheckNumber.Trim(),
                    Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim(),
                    CreatedByUserId = userId,
                    CreatedByName = userName,
                    IsActive = true,
                    CreatedAt = DateTime.UtcNow
                };

                await _context.CommissionLedgerEntries.AddAsync(entry);
                account.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<CommissionLedgerEntryDto>.Success(
                    MapEntry(entry, Remaining(account) + entry.SignedAmount),
                    "Allocation added");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error adding commission allocation");
                return ApiResponse<CommissionLedgerEntryDto>.Fail("Error adding allocation");
            }
        }

        public async Task<ApiResponse<CommissionLedgerEntryDto>> UpdateAllocationAsync(UpdateCommissionAllocationDto dto)
        {
            try
            {
                var entry = await _context.CommissionLedgerEntries
                    .Include(e => e.CommissionAccount)
                        .ThenInclude(a => a.Entries.Where(x => x.IsActive))
                    .FirstOrDefaultAsync(e => e.Id == dto.Id && e.IsActive);

                if (entry == null)
                    return ApiResponse<CommissionLedgerEntryDto>.Fail("Allocation not found");

                if (entry.EntryType != CommissionLedgerEntryType.Allocation)
                    return ApiResponse<CommissionLedgerEntryDto>.Fail("Only allocations can be edited");

                var remaining = Remaining(entry.CommissionAccount);
                if (remaining < entry.SignedAmount)
                    return ApiResponse<CommissionLedgerEntryDto>.Fail("This deposit has already been used and cannot be edited");

                if (dto.Amount.HasValue)
                {
                    var newAmount = decimal.Round(dto.Amount.Value, 2);
                    var projected = remaining - entry.SignedAmount + newAmount;
                    if (projected < 0)
                        return ApiResponse<CommissionLedgerEntryDto>.Fail("Reducing this deposit would make the remaining balance negative");
                    entry.SignedAmount = newAmount;
                }

                if (dto.EntryDate.HasValue)
                    entry.EntryDate = dto.EntryDate.Value.Date;
                if (dto.CheckNumber != null)
                    entry.CheckNumber = string.IsNullOrWhiteSpace(dto.CheckNumber) ? null : dto.CheckNumber.Trim();
                if (dto.Note != null)
                    entry.Note = string.IsNullOrWhiteSpace(dto.Note) ? null : dto.Note.Trim();

                entry.UpdatedAt = DateTime.UtcNow;
                await _context.SaveChangesAsync();

                return ApiResponse<CommissionLedgerEntryDto>.Success(
                    MapEntry(entry, Remaining(entry.CommissionAccount)),
                    "Allocation updated");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating commission allocation");
                return ApiResponse<CommissionLedgerEntryDto>.Fail("Error updating allocation");
            }
        }

        public async Task<ApiResponse<bool>> DeleteAllocationAsync(Guid entryId)
        {
            try
            {
                var entry = await _context.CommissionLedgerEntries
                    .Include(e => e.CommissionAccount)
                        .ThenInclude(a => a.Entries.Where(x => x.IsActive))
                    .FirstOrDefaultAsync(e => e.Id == entryId && e.IsActive);

                if (entry == null)
                    return ApiResponse<bool>.Fail("Allocation not found");

                if (entry.EntryType != CommissionLedgerEntryType.Allocation)
                    return ApiResponse<bool>.Fail("Only unused allocations can be deleted");

                var remaining = Remaining(entry.CommissionAccount);
                if (remaining < entry.SignedAmount)
                    return ApiResponse<bool>.Fail("This deposit has already been used and cannot be deleted");

                entry.Deactivate();
                await _context.SaveChangesAsync();
                return ApiResponse<bool>.Success(true, "Allocation removed");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting commission allocation");
                return ApiResponse<bool>.Fail("Error deleting allocation");
            }
        }

        public async Task<ApiResponse<bool>> SoftDeleteAsync(Guid id)
        {
            try
            {
                var account = await _context.CommissionAccounts.FirstOrDefaultAsync(a => a.Id == id && a.IsActive);
                if (account == null)
                    return ApiResponse<bool>.Fail("Commission account not found");

                account.Deactivate();
                await _context.SaveChangesAsync();
                return ApiResponse<bool>.Success(true, "Commission account deactivated. History is kept.");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deactivating commission account");
                return ApiResponse<bool>.Fail("Error deactivating commission account");
            }
        }

        public async Task<string?> ApplySaleCommissionAsync(
            Guid branchId,
            Guid transactionId,
            decimal newCommissionAmount,
            DateTime entryDate,
            string? productName,
            string? itemCode,
            Guid? userId,
            string? userName)
        {
            newCommissionAmount = decimal.Round(Math.Max(0, newCommissionAmount), 2);

            var existingForSale = _context.CommissionLedgerEntries.Local
                .Where(e => e.TransactionId == transactionId && e.IsActive)
                .ToList();

            var persisted = await _context.CommissionLedgerEntries
                .Where(e => e.TransactionId == transactionId && e.IsActive)
                .ToListAsync();

            foreach (var entry in persisted)
            {
                if (existingForSale.All(e => e.Id != entry.Id))
                    existingForSale.Add(entry);
            }

            var postedNet = existingForSale.Sum(e => e.SignedAmount);
            var targetNet = -newCommissionAmount;
            var delta = decimal.Round(targetNet - postedNet, 2);

            if (delta == 0)
                return null;

            CommissionAccount? account = null;
            var accountId = existingForSale.FirstOrDefault()?.CommissionAccountId;
            if (accountId.HasValue)
            {
                account = await _context.CommissionAccounts
                    .IgnoreQueryFilters()
                    .Include(a => a.Entries.Where(e => e.IsActive))
                    .FirstOrDefaultAsync(a => a.Id == accountId.Value);
            }

            if (account == null)
            {
                account = await _context.CommissionAccounts
                    .Include(a => a.Entries.Where(e => e.IsActive))
                    .FirstOrDefaultAsync(a => a.BranchId == branchId && a.IsActive);
            }

            if (newCommissionAmount > 0 && (account == null || !account.IsActive) && existingForSale.Count == 0)
                return "No active commission account for this branch. Create one before entering commission.";

            if (account == null)
                return null;

            var remaining = RemainingIncludingLocal(account);
            if (remaining + delta < 0)
            {
                return $"Insufficient commission balance. Remaining: {remaining:0.##}, requested: {newCommissionAmount:0.##}";
            }

            CommissionLedgerEntryType type;
            if (postedNet == 0 && newCommissionAmount > 0)
                type = CommissionLedgerEntryType.Deduction;
            else if (newCommissionAmount == 0 && postedNet < 0)
                type = CommissionLedgerEntryType.Reversal;
            else
                type = CommissionLedgerEntryType.Adjustment;

            var ledger = new CommissionLedgerEntry
            {
                CommissionAccountId = account.Id,
                EntryType = type,
                SignedAmount = delta,
                EntryDate = entryDate.Date,
                TransactionId = transactionId,
                ProductName = productName,
                ItemCode = itemCode,
                Note = type switch
                {
                    CommissionLedgerEntryType.Deduction => "Commission paid on sale",
                    CommissionLedgerEntryType.Reversal => "Sale deleted or commission cleared",
                    _ => "Sale commission adjusted"
                },
                CreatedByUserId = userId,
                CreatedByName = userName,
                IsActive = true,
                CreatedAt = DateTime.UtcNow
            };

            await _context.CommissionLedgerEntries.AddAsync(ledger);
            account.UpdatedAt = DateTime.UtcNow;
            return null;
        }

        private async Task<CommissionAccount?> LoadAccountAsync(Guid id, bool includeInactive)
        {
            var query = includeInactive
                ? _context.CommissionAccounts.IgnoreQueryFilters()
                : _context.CommissionAccounts.AsQueryable();

            return await query
                .Include(a => a.Branch)
                .Include(a => a.Entries.Where(e => e.IsActive))
                .FirstOrDefaultAsync(a => a.Id == id && (includeInactive || a.IsActive));
        }

        private static decimal Remaining(CommissionAccount account)
        {
            return account.Entries.Where(e => e.IsActive).Sum(e => e.SignedAmount);
        }

        private decimal RemainingIncludingLocal(CommissionAccount account)
        {
            var persisted = account.Entries?.Where(e => e.IsActive).Sum(e => e.SignedAmount) ?? 0;
            var localAdded = _context.ChangeTracker.Entries<CommissionLedgerEntry>()
                .Where(e => e.State == EntityState.Added &&
                            e.Entity.CommissionAccountId == account.Id &&
                            e.Entity.IsActive)
                .Sum(e => e.Entity.SignedAmount);
            return persisted + localAdded;
        }

        private static CommissionAccountDto MapAccount(CommissionAccount account)
        {
            var active = account.Entries?.Where(e => e.IsActive).ToList() ?? new List<CommissionLedgerEntry>();
            var allocated = active
                .Where(e => e.EntryType == CommissionLedgerEntryType.Allocation)
                .Sum(e => e.SignedAmount);
            var remaining = active.Sum(e => e.SignedAmount);
            var used = allocated - remaining;

            return new CommissionAccountDto
            {
                Id = account.Id,
                BranchId = account.BranchId,
                BranchName = account.Branch?.Name ?? string.Empty,
                IsActive = account.IsActive,
                CreatedAt = account.CreatedAt,
                UpdatedAt = account.UpdatedAt,
                AllocatedAmount = allocated,
                UsedAmount = used,
                RemainingAmount = remaining,
                AllocationCount = active.Count(e => e.EntryType == CommissionLedgerEntryType.Allocation),
                DeductionCount = active.Count(e => e.EntryType == CommissionLedgerEntryType.Deduction)
            };
        }

        private static CommissionLedgerEntryDto MapEntry(CommissionLedgerEntry entry, decimal remaining)
        {
            var unusedAllocation = entry.EntryType == CommissionLedgerEntryType.Allocation &&
                                   remaining >= entry.SignedAmount;

            return new CommissionLedgerEntryDto
            {
                Id = entry.Id,
                CommissionAccountId = entry.CommissionAccountId,
                EntryType = entry.EntryType,
                EntryTypeName = entry.EntryType.ToString(),
                SignedAmount = entry.SignedAmount,
                EntryDate = entry.EntryDate,
                CheckNumber = entry.CheckNumber,
                Note = entry.Note,
                TransactionId = entry.TransactionId,
                ProductName = entry.ProductName,
                ItemCode = entry.ItemCode,
                CreatedByUserId = entry.CreatedByUserId,
                CreatedByName = entry.CreatedByName,
                CanEditOrDelete = unusedAllocation,
                CreatedAt = entry.CreatedAt
            };
        }
    }
}
