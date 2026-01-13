using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Application.Interface
{
    public interface ITransactionService
    {
        Task<ApiResponse<TransactionResponseDto>> CreateTransactionAsync(CreateTransactionDto dto);
        Task<ApiResponse<TransactionResponseDto>> GetTransactionByIdAsync(Guid id);
        Task<ApiResponse<List<TransactionResponseDto>>> GetAllTransactionsAsync(
                    DateTime? startDate = null,
                    DateTime? endDate = null,
                    Guid? branchId = null);
        Task<ApiResponse<TransactionResponseDto>> UpdateTransactionAsync(UpdateTransactionDto dto);
        Task<ApiResponse<bool>> DeleteTransactionAsync(Guid id);
        Task<ApiResponse<TransactionResponseDto>> PayCreditAsync(PayCreditDto dto);
        Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetCreditTransactionHistoryAsync(Guid? customerId);
        Task<ApiResponse<List<CreditTransactionHistoryDto>>> GetPendingCreditTransactionsAsync(Guid? customerId = null, Guid? branchId = null);
        Task<ApiResponse<DailySalesReportDto>> GenerateDailySalesReportAsync(DateTime date, Guid? branchId = null, string? paymentMethod = null, Guid? bankAccountId = null);
        Task<ApiResponse<CreditSummaryDto>> GetCreditSummaryAsync(Guid? customerId = null);
        Task<ApiResponse<bool>> MarkCommissionAsPaidAsync(Guid transactionId);
        Task<ApiResponse<List<TransactionResponseDto>>> GetTransactionsByDateRangeAsync(
                   DateTime? startDate = null,
                   DateTime? endDate = null,
                   Guid? branchId = null,
                   Guid? customerId = null,
                   Guid? productId = null,
                   string? paymentMethod = null);

        Task<ApiResponse<List<TransactionResponseDto>>> GetTransactionsByDateAsync(
            DateTime date,
            Guid? branchId = null,
            Guid? customerId = null,
            Guid? productId = null,
            string? paymentMethod = null);

        Task<ApiResponse<TransactionSummaryDto>> GetTransactionSummaryAsync(
            DateTime? startDate = null,
            DateTime? endDate = null,
            Guid? branchId = null,
            string? paymentMethod = null);
    }
}
