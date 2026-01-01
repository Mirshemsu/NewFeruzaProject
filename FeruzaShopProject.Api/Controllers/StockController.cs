using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Threading.Tasks;

namespace FeruzaShopProject.API.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    //[Authorize]
    public class StockController : ControllerBase
    {
        private readonly IStockService _stockService;
        private readonly ILogger<StockController> _logger;

        public StockController(
            IStockService stockService,
            ILogger<StockController> logger)
        {
            _stockService = stockService;
            _logger = logger;
        }

        /// <summary>
        /// Get stock quantity for a specific date
        /// </summary>
        [HttpGet("on-date")]
        //[Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<decimal>>> GetStockOnDate(
            [FromQuery] Guid productId,
            [FromQuery] Guid branchId,
            [FromQuery] DateTime date)
        {
            try
            {
                if (productId == Guid.Empty)
                    return BadRequest(ApiResponse<decimal>.Fail("Product ID is required"));

                if (branchId == Guid.Empty)
                    return BadRequest(ApiResponse<decimal>.Fail("Branch ID is required"));

                var result = await _stockService.GetStockOnDateAsync(productId, branchId, date);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockOnDate endpoint");
                return StatusCode(500, ApiResponse<decimal>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get current stock (today)
        /// </summary>
        [HttpGet("current")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<CurrentStockDto>>> GetCurrentStock(
            [FromQuery] Guid? branchId = null,
            [FromQuery] Guid? productId = null)
        {
            try
            {
                var result = await _stockService.GetCurrentStockAsync(branchId, productId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCurrentStock endpoint");
                return StatusCode(500, ApiResponse<CurrentStockDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get stock history for a date range
        /// </summary>
        [HttpGet("history")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<StockHistoryDto>>>> GetStockHistory(
            [FromQuery] Guid productId,
            [FromQuery] Guid branchId,
            [FromQuery] DateTime startDate,
            [FromQuery] DateTime endDate)
        {
            try
            {
                if (productId == Guid.Empty)
                    return BadRequest(ApiResponse<List<StockHistoryDto>>.Fail("Product ID is required"));

                if (branchId == Guid.Empty)
                    return BadRequest(ApiResponse<List<StockHistoryDto>>.Fail("Branch ID is required"));

                var result = await _stockService.GetStockHistoryAsync(productId, branchId, startDate, endDate);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockHistory endpoint");
                return StatusCode(500, ApiResponse<List<StockHistoryDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Test endpoint
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok(new
            {
                Message = "Stock API is working",
                Timestamp = DateTime.UtcNow,
                Endpoints = new[]
                {
                    "GET /api/stock/on-date?productId={id}&branchId={id}&date={yyyy-MM-dd}",
                    "GET /api/stock/current?branchId={id}&productId={id}",
                    "GET /api/stock/history?productId={id}&branchId={id}&startDate={yyyy-MM-dd}&endDate={yyyy-MM-dd}"
                }
            });
        }
    }
}