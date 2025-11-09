using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface IBankAccountService
    {
        Task<ApiResponse<BankAccountResponseDto>> CreateBankAccountAsync(CreateBankAccountDto dto);
        Task<ApiResponse<BankAccountResponseDto>> UpdateBankAccountAsync(UpdateBankAccountDto dto);
        Task<ApiResponse<BankAccountResponseDto>> GetBankAccountByIdAsync(Guid id);
        Task<ApiResponse<List<BankAccountResponseDto>>> GetAllBankAccountsAsync();
        Task<ApiResponse<bool>> DeleteBankAccountAsync(Guid id);
    }
}
