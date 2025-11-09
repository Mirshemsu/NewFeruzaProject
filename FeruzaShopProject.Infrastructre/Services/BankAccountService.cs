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
    public class BankAccountService : IBankAccountService
    {
        private readonly ShopDbContext _context;
        private readonly IMapper _mapper;
        private readonly ILogger<BankAccountService> _logger;
        private readonly IHttpContextAccessor _httpContextAccessor;

        public BankAccountService(
            ShopDbContext context,
            IMapper mapper,
            ILogger<BankAccountService> logger,
            IHttpContextAccessor httpContextAccessor)
        {
            _context = context ?? throw new ArgumentNullException(nameof(context));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        private async Task<bool> IsUserManagerAsync()
        {
            var userIdClaim = _httpContextAccessor.HttpContext?.User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            if (!Guid.TryParse(userIdClaim, out var userId))
            {
                _logger.LogWarning("Invalid user ID in JWT: {UserIdClaim}", userIdClaim);
                return false;
            }
            var user = await _context.Users.FirstOrDefaultAsync(u => u.Id == userId);
            return user != null && user.Role == Role.Manager;
        }

        public async Task<ApiResponse<BankAccountResponseDto>> CreateBankAccountAsync(CreateBankAccountDto dto)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!await IsUserManagerAsync())
                {
                    _logger.LogWarning("Unauthorized attempt to create bank account");
                    return ApiResponse<BankAccountResponseDto>.Fail("Only Manager can create bank accounts");
                }

                if (dto.BranchId.HasValue)
                {
                    var branch = await _context.Branches.FindAsync(dto.BranchId);
                    if (branch == null || !branch.IsActive)
                    {
                        _logger.LogWarning("Invalid or inactive branch: {BranchId}", dto.BranchId);
                        return ApiResponse<BankAccountResponseDto>.Fail("Invalid or inactive branch");
                    }
                }

                if (await _context.BankAccounts.AnyAsync(ba => ba.BankName == dto.BankName && ba.AccountNumber == dto.AccountNumber && ba.IsActive))
                {
                    _logger.LogWarning("Bank account already exists: {BankName}, {AccountNumber}", dto.BankName, dto.AccountNumber);
                    return ApiResponse<BankAccountResponseDto>.Fail("Bank account with this name and number already exists");
                }

                var bankAccount = _mapper.Map<BankAccount>(dto);
                bankAccount.Id = Guid.NewGuid();
                bankAccount.CreatedAt = DateTime.UtcNow;
                bankAccount.IsActive = true;
                await _context.BankAccounts.AddAsync(bankAccount);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                var result = _mapper.Map<BankAccountResponseDto>(bankAccount);
                _logger.LogInformation("Created bank account {BankAccountId}", bankAccount.Id);
                return ApiResponse<BankAccountResponseDto>.Success(result, "Bank account created successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error creating bank account: {@Dto}", dto);
                return ApiResponse<BankAccountResponseDto>.Fail("Failed to create bank account");
            }
        }

        public async Task<ApiResponse<BankAccountResponseDto>> UpdateBankAccountAsync(UpdateBankAccountDto dto)
        {
            try
            {
                if (!await IsUserManagerAsync())
                {
                    _logger.LogWarning("Unauthorized attempt to update bank account");
                    return ApiResponse<BankAccountResponseDto>.Fail("Only Manager can update bank accounts");
                }

                var bankAccount = await _context.BankAccounts
                    .FirstOrDefaultAsync(ba => ba.Id == dto.Id && ba.IsActive);
                if (bankAccount == null)
                {
                    _logger.LogWarning("Bank account not found: {BankAccountId}", dto.Id);
                    return ApiResponse<BankAccountResponseDto>.Fail("Bank account not found");
                }

                if (dto.BranchId.HasValue)
                {
                    var branch = await _context.Branches.FindAsync(dto.BranchId);
                    if (branch == null || !branch.IsActive)
                    {
                        _logger.LogWarning("Invalid or inactive branch: {BranchId}", dto.BranchId);
                        return ApiResponse<BankAccountResponseDto>.Fail("Invalid or inactive branch");
                    }
                }

                if (dto.BankName != null && dto.AccountNumber != null &&
                    await _context.BankAccounts.AnyAsync(ba => ba.BankName == dto.BankName && ba.AccountNumber == dto.AccountNumber && ba.Id != dto.Id && ba.IsActive))
                {
                    _logger.LogWarning("Bank account already exists: {BankName}, {AccountNumber}", dto.BankName, dto.AccountNumber);
                    return ApiResponse<BankAccountResponseDto>.Fail("Bank account with this name and number already exists");
                }

                _mapper.Map(dto, bankAccount);
                bankAccount.UpdatedAt = DateTime.UtcNow;
                _context.BankAccounts.Update(bankAccount);
                await _context.SaveChangesAsync();
                var result = _mapper.Map<BankAccountResponseDto>(bankAccount);
                _logger.LogInformation("Updated bank account {BankAccountId}", bankAccount.Id);
                return ApiResponse<BankAccountResponseDto>.Success(result, "Bank account updated successfully");
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating bank account: {@Dto}", dto);
                return ApiResponse<BankAccountResponseDto>.Fail("Failed to update bank account");
            }
        }

        public async Task<ApiResponse<BankAccountResponseDto>> GetBankAccountByIdAsync(Guid id)
        {
            try
            {
                var bankAccount = await _context.BankAccounts
                    .Include(ba => ba.Branch)
                    .FirstOrDefaultAsync(ba => ba.Id == id && ba.IsActive);
                if (bankAccount == null)
                {
                    _logger.LogWarning("Bank account not found: {BankAccountId}", id);
                    return ApiResponse<BankAccountResponseDto>.Fail("Bank account not found");
                }
                var result = _mapper.Map<BankAccountResponseDto>(bankAccount);
                return ApiResponse<BankAccountResponseDto>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving bank account: {BankAccountId}", id);
                return ApiResponse<BankAccountResponseDto>.Fail("Failed to retrieve bank account");
            }
        }

        public async Task<ApiResponse<List<BankAccountResponseDto>>> GetAllBankAccountsAsync()
        {
            try
            {
                var bankAccounts = await _context.BankAccounts
                    .Include(ba => ba.Branch)
                    .Where(ba => ba.IsActive)
                    .ToListAsync();
                var result = _mapper.Map<List<BankAccountResponseDto>>(bankAccounts);
                return ApiResponse<List<BankAccountResponseDto>>.Success(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error retrieving all bank accounts");
                return ApiResponse<List<BankAccountResponseDto>>.Fail("Failed to retrieve bank accounts");
            }
        }

        public async Task<ApiResponse<bool>> DeleteBankAccountAsync(Guid id)
        {
            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                if (!await IsUserManagerAsync())
                {
                    _logger.LogWarning("Unauthorized attempt to delete bank account");
                    return ApiResponse<bool>.Fail("Only Manager can delete bank accounts");
                }

                var bankAccount = await _context.BankAccounts
                    .FirstOrDefaultAsync(ba => ba.Id == id && ba.IsActive);
                if (bankAccount == null)
                {
                    _logger.LogWarning("Bank account not found: {BankAccountId}", id);
                    return ApiResponse<bool>.Fail("Bank account not found");
                }


                bankAccount.IsActive = false;
                bankAccount.UpdatedAt = DateTime.UtcNow;
                _context.BankAccounts.Update(bankAccount);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
                _logger.LogInformation("Deleted bank account {BankAccountId}", id);
                return ApiResponse<bool>.Success(true, "Bank account deleted successfully");
            }
            catch (Exception ex)
            {
                await transaction.RollbackAsync();
                _logger.LogError(ex, "Error deleting bank account: {BankAccountId}", id);
                return ApiResponse<bool>.Fail("Failed to delete bank account");
            }
        }
    }
}