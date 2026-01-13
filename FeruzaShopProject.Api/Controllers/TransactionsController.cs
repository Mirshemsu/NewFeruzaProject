using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeruzaShopProject.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class TransactionsController : ControllerBase
    {
        private readonly ITransactionService _transactionService;
        private readonly ILogger<TransactionsController> _logger;

        public TransactionsController(
            ITransactionService transactionService,
            ILogger<TransactionsController> logger)
        {
            _transactionService = transactionService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new sales transaction
        /// </summary>
        [HttpPost]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> CreateTransaction([FromBody] CreateTransactionDto dto)
        {
            try
            {
                var result = await _transactionService.CreateTransactionAsync(dto);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating transaction");
                return StatusCode(500, ApiResponse<TransactionResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get transaction by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> GetTransaction(Guid id)
        {
            try
            {
                var result = await _transactionService.GetTransactionByIdAsync(id);

                if (!result.IsCompletedSuccessfully)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction by ID: {TransactionId}", id);
                return StatusCode(500, ApiResponse<TransactionResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get all transactions with optional date and branch filters
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<TransactionResponseDto>>>> GetAllTransactions(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var result = await _transactionService.GetAllTransactionsAsync(startDate, endDate, branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all transactions");
                return StatusCode(500, ApiResponse<List<TransactionResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get transactions by specific date with optional filters
        /// </summary>
        [HttpGet("by-date")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<TransactionResponseDto>>>> GetTransactionsByDate(
            [FromQuery] DateTime date,
            [FromQuery] Guid? branchId = null,
            [FromQuery] Guid? customerId = null,
            [FromQuery] Guid? productId = null,
            [FromQuery] string? paymentMethod = null)
        {
            try
            {
                var result = await _transactionService.GetTransactionsByDateAsync(date, branchId, customerId, productId, paymentMethod);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions for date: {Date}", date);
                return StatusCode(500, ApiResponse<List<TransactionResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get transactions by date range with optional filters
        /// </summary>
        [HttpGet("by-date-range")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<TransactionResponseDto>>>> GetTransactionsByDateRange(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? branchId = null,
            [FromQuery] Guid? customerId = null,
            [FromQuery] Guid? productId = null,
            [FromQuery] string? paymentMethod = null)
        {
            try
            {
                var result = await _transactionService.GetTransactionsByDateRangeAsync(
                    startDate, endDate, branchId, customerId, productId, paymentMethod);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transactions by date range: Start={StartDate}, End={EndDate}",
                    startDate, endDate);
                return StatusCode(500, ApiResponse<List<TransactionResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get transaction summary with analytics
        /// </summary>
        [HttpGet("summary")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<TransactionSummaryDto>>> GetTransactionSummary(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? branchId = null,
            [FromQuery] string? paymentMethod = null)
        {
            try
            {
                var result = await _transactionService.GetTransactionSummaryAsync(
                    startDate, endDate, branchId, paymentMethod);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transaction summary: Start={StartDate}, End={EndDate}",
                    startDate, endDate);
                return StatusCode(500, ApiResponse<TransactionSummaryDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Update transaction
        /// </summary>
        [HttpPut]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> UpdateTransaction([FromBody] UpdateTransactionDto dto)
        {
            try
            {
                var result = await _transactionService.UpdateTransactionAsync(dto);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating transaction: {TransactionId}", dto.Id);
                return StatusCode(500, ApiResponse<TransactionResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Delete transaction
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteTransaction(Guid id)
        {
            try
            {
                var result = await _transactionService.DeleteTransactionAsync(id);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting transaction: {TransactionId}", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Process credit payment
        /// </summary>
        [HttpPost("pay-credit")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<TransactionResponseDto>>> PayCredit([FromBody] PayCreditDto dto)
        {
            try
            {
                var result = await _transactionService.PayCreditAsync(dto);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error processing credit payment for transaction: {TransactionId}", dto.TransactionId);
                return StatusCode(500, ApiResponse<TransactionResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get credit transaction history
        /// </summary>
        [HttpGet("credit/history")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<CreditTransactionHistoryDto>>>> GetCreditHistory([FromQuery] Guid? customerId)
        {
            try
            {
                var result = await _transactionService.GetCreditTransactionHistoryAsync(customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit history for customer: {CustomerId}", customerId);
                return StatusCode(500, ApiResponse<List<CreditTransactionHistoryDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get pending credit transactions
        /// </summary>
        [HttpGet("credit/pending")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<CreditTransactionHistoryDto>>>> GetPendingCreditTransactions(
            [FromQuery] Guid? customerId = null,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var result = await _transactionService.GetPendingCreditTransactionsAsync(customerId, branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending credit transactions");
                return StatusCode(500, ApiResponse<List<CreditTransactionHistoryDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Generate daily sales report
        /// </summary>
        [HttpGet("reports/daily")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<DailySalesReportDto>>> GenerateDailySalesReport(
            [FromQuery] DateTime date,
            [FromQuery] Guid? branchId = null,
            [FromQuery] string? paymentMethod = null,
            [FromQuery] Guid? bankAccountId = null)
        {
            try
            {
                var result = await _transactionService.GenerateDailySalesReportAsync(date, branchId, paymentMethod, bankAccountId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating daily sales report for date: {Date}", date);
                return StatusCode(500, ApiResponse<DailySalesReportDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get credit summary
        /// </summary>
        [HttpGet("credit/summary")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<CreditSummaryDto>>> GetCreditSummary([FromQuery] Guid? customerId = null)
        {
            try
            {
                var result = await _transactionService.GetCreditSummaryAsync(customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting credit summary for customer: {CustomerId}", customerId);
                return StatusCode(500, ApiResponse<CreditSummaryDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Mark commission as paid
        /// </summary>
        [HttpPatch("{id}/mark-commission-paid")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> MarkCommissionAsPaid(Guid id)
        {
            try
            {
                var result = await _transactionService.MarkCommissionAsPaidAsync(id);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking commission as paid for transaction: {TransactionId}", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Internal server error"));
            }
        }
    }
}