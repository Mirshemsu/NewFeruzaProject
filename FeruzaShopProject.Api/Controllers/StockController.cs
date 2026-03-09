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
        /// Get stock quantity for a specific date with credit information
        /// </summary>
        [HttpGet("on-date")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<StockOnDateDto>>> GetStockOnDate(
            [FromQuery] Guid productId,
            [FromQuery] Guid branchId,
            [FromQuery] DateTime date)
        {
            try
            {
                if (productId == Guid.Empty)
                    return BadRequest(ApiResponse<StockOnDateDto>.Fail("Product ID is required"));

                if (branchId == Guid.Empty)
                    return BadRequest(ApiResponse<StockOnDateDto>.Fail("Branch ID is required"));

                var result = await _stockService.GetStockOnDateAsync(productId, branchId, date);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockOnDate endpoint");
                return StatusCode(500, ApiResponse<StockOnDateDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get current stock with credit information (actual vs credit)
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
        /// Get stock history for a date range with detailed movement breakdown
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
        /// Get credit stock summary (unpaid credit items)
        /// </summary>
        [HttpGet("credit-summary")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<StockCreditSummaryDto>>> GetCreditStockSummary(
            [FromQuery] Guid? branchId = null,
            [FromQuery] Guid? customerId = null)
        {
            try
            {
                var result = await _stockService.GetCreditStockSummaryAsync(branchId, customerId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetCreditStockSummary endpoint");
                return StatusCode(500, ApiResponse<StockCreditSummaryDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get stock alerts (low stock, out of stock, overdue credit)
        /// </summary>
        [HttpGet("alerts")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<StockAlertDto>>> GetStockAlerts(
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var result = await _stockService.GetStockAlertsAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockAlerts endpoint");
                return StatusCode(500, ApiResponse<StockAlertDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get detailed stock for a specific product in a branch
        /// </summary>
        [HttpGet("product-detail")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<StockItemDetailDto>>> GetProductStockDetail(
            [FromQuery] Guid productId,
            [FromQuery] Guid branchId)
        {
            try
            {
                if (productId == Guid.Empty)
                    return BadRequest(ApiResponse<StockItemDetailDto>.Fail("Product ID is required"));

                if (branchId == Guid.Empty)
                    return BadRequest(ApiResponse<StockItemDetailDto>.Fail("Branch ID is required"));

                var result = await _stockService.GetProductStockDetailAsync(productId, branchId);

                if (!result.IsCompletedSuccessfully)
                    return BadRequest(result);

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetProductStockDetail endpoint");
                return StatusCode(500, ApiResponse<StockItemDetailDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get stock summary by branch (simplified)
        /// </summary>
        [HttpGet("branch-summary/{branchId}")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<CurrentStockDto>>> GetStockByBranch(Guid branchId)
        {
            try
            {
                if (branchId == Guid.Empty)
                    return BadRequest(ApiResponse<CurrentStockDto>.Fail("Branch ID is required"));

                var result = await _stockService.GetCurrentStockAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockByBranch endpoint");
                return StatusCode(500, ApiResponse<CurrentStockDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get stock summary by product (simplified)
        /// </summary>
        [HttpGet("product-summary/{productId}")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<CurrentStockDto>>> GetStockByProduct(Guid productId)
        {
            try
            {
                if (productId == Guid.Empty)
                    return BadRequest(ApiResponse<CurrentStockDto>.Fail("Product ID is required"));

                var result = await _stockService.GetCurrentStockAsync(null, productId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetStockByProduct endpoint");
                return StatusCode(500, ApiResponse<CurrentStockDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get overdue credit items (older than 30 days)
        /// </summary>
        [HttpGet("overdue-credit")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<CreditStockItemDto>>>> GetOverdueCredit(
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var alerts = await _stockService.GetStockAlertsAsync(branchId);

                if (alerts.IsCompletedSuccessfully && alerts.Data != null)
                {
                    return Ok(ApiResponse<List<CreditStockItemDto>>.Success(
                        alerts.Data.OverdueCreditItems,
                        "Overdue credit items retrieved"));
                }

                return Ok(ApiResponse<List<CreditStockItemDto>>.Success(
                    new List<CreditStockItemDto>(),
                    "No overdue credit items found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOverdueCredit endpoint");
                return StatusCode(500, ApiResponse<List<CreditStockItemDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get low stock items (below reorder level)
        /// </summary>
        [HttpGet("low-stock")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<StockItemDetailDto>>>> GetLowStock(
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var alerts = await _stockService.GetStockAlertsAsync(branchId);

                if (alerts.IsCompletedSuccessfully && alerts.Data != null)
                {
                    return Ok(ApiResponse<List<StockItemDetailDto>>.Success(
                        alerts.Data.LowStockItems,
                        "Low stock items retrieved"));
                }

                return Ok(ApiResponse<List<StockItemDetailDto>>.Success(
                    new List<StockItemDetailDto>(),
                    "No low stock items found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetLowStock endpoint");
                return StatusCode(500, ApiResponse<List<StockItemDetailDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get out of stock items
        /// </summary>
        [HttpGet("out-of-stock")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<List<StockItemDetailDto>>>> GetOutOfStock(
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                var alerts = await _stockService.GetStockAlertsAsync(branchId);

                if (alerts.IsCompletedSuccessfully && alerts.Data != null)
                {
                    return Ok(ApiResponse<List<StockItemDetailDto>>.Success(
                        alerts.Data.OutOfStockItems,
                        "Out of stock items retrieved"));
                }

                return Ok(ApiResponse<List<StockItemDetailDto>>.Success(
                    new List<StockItemDetailDto>(),
                    "No out of stock items found"));
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in GetOutOfStock endpoint");
                return StatusCode(500, ApiResponse<List<StockItemDetailDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Test endpoint to verify API is working
        /// </summary>
        [HttpGet("test")]
        [AllowAnonymous]
        public IActionResult Test()
        {
            return Ok(new
            {
                Message = "Stock API is working",
                Timestamp = DateTime.UtcNow,
                Version = "1.0.0",
                Endpoints = new[]
                {
                    "GET /api/stock/on-date?productId={id}&branchId={id}&date={yyyy-MM-dd} - Get stock with credit info on specific date",
                    "GET /api/stock/current?branchId={id}&productId={id} - Get current stock with actual vs credit",
                    "GET /api/stock/history?productId={id}&branchId={id}&startDate={yyyy-MM-dd}&endDate={yyyy-MM-dd} - Get detailed stock history",
                    "GET /api/stock/credit-summary?branchId={id}&customerId={id} - Get credit stock summary",
                    "GET /api/stock/alerts?branchId={id} - Get all stock alerts (low stock, out of stock, overdue credit)",
                    "GET /api/stock/product-detail?productId={id}&branchId={id} - Get detailed stock for specific product",
                    "GET /api/stock/branch-summary/{branchId} - Get stock summary by branch",
                    "GET /api/stock/product-summary/{productId} - Get stock summary by product",
                    "GET /api/stock/overdue-credit?branchId={id} - Get overdue credit items",
                    "GET /api/stock/low-stock?branchId={id} - Get low stock items",
                    "GET /api/stock/out-of-stock?branchId={id} - Get out of stock items"
                }
            });
        }
    }
}