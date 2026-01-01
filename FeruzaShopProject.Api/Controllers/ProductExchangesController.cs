
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace FeruzaShopProject.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class ProductExchangesController : ControllerBase
    {
        private readonly IProductExchangeService _exchangeService;
        private readonly ILogger<ProductExchangesController> _logger;

        public ProductExchangesController(
            IProductExchangeService exchangeService,
            ILogger<ProductExchangesController> logger)
        {
            _exchangeService = exchangeService;
            _logger = logger;
        }

        /// <summary>
        /// Create a new product exchange
        /// </summary>
        /// <remarks>
        /// Sample request:
        /// {
        ///     "originalTransactionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///     "originalProductId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///     "originalQuantity": 2,
        ///     "newProductId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        ///     "newQuantity": 1,
        ///     "reason": "Customer wanted different product"
        /// }
        /// </remarks>
        [HttpPost]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<ProductExchangeResponseDto>>> CreateExchange(
            [FromBody] CreateProductExchangeDto dto)
        {
            try
            {
                var result = await _exchangeService.CreateExchangeAsync(dto);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating product exchange");
                return StatusCode(500, ApiResponse<ProductExchangeResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get product exchange by ID
        /// </summary>
        [HttpGet("{id}")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<ProductExchangeResponseDto>>> GetExchange(Guid id)
        {
            try
            {
                var result = await _exchangeService.GetExchangeByIdAsync(id);

                if (!result.IsCompletedSuccessfully)
                    return NotFound(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange by ID: {ExchangeId}", id);
                return StatusCode(500, ApiResponse<ProductExchangeResponseDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get all product exchanges with optional filters
        /// </summary>
        [HttpGet]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<ProductExchangeResponseDto>>>> GetAllExchanges(
            [FromQuery] Guid? branchId = null,
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null)
        {
            try
            {
                var result = await _exchangeService.GetAllExchangesAsync(branchId, startDate, endDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all exchanges");
                return StatusCode(500, ApiResponse<List<ProductExchangeResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Delete a product exchange (soft delete)
        /// </summary>
        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteExchange(Guid id)
        {
            try
            {
                var result = await _exchangeService.DeleteExchangeAsync(id);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting exchange: {ExchangeId}", id);
                return StatusCode(500, ApiResponse<bool>.Fail("Internal server error"));
            }
        }

        
        /// <summary>
        /// Get all exchanges for a specific transaction
        /// </summary>
        [HttpGet("transaction/{transactionId}")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<ProductExchangeResponseDto>>>> GetExchangesByTransaction(
            Guid transactionId)
        {
            try
            {
                var result = await _exchangeService.GetExchangesByTransactionAsync(transactionId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchanges for transaction: {TransactionId}", transactionId);
                return StatusCode(500, ApiResponse<List<ProductExchangeResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get all exchanges for a specific customer
        /// </summary>
        [HttpGet("customer/{customerId}")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<ProductExchangeResponseDto>>>> GetExchangesByCustomer(
            Guid customerId)
        {
            try
            {
                var result = await _exchangeService.GetExchangesByCustomerAsync(customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchanges for customer: {CustomerId}", customerId);
                return StatusCode(500, ApiResponse<List<ProductExchangeResponseDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get exchange summary report
        /// </summary>
        [HttpGet("summary")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<ExchangeSummaryDto>>> GetExchangeSummary(
            [FromQuery] DateTime? startDate = null,
            [FromQuery] DateTime? endDate = null,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var result = await _exchangeService.GetExchangeSummaryAsync(startDate, endDate, branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting exchange summary");
                return StatusCode(500, ApiResponse<ExchangeSummaryDto>.Fail("Internal server error"));
            }
        }

       
    }
}