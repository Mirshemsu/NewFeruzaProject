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
    public class DailyClosingController : ControllerBase
    {
        private readonly IDailyClosingService _closingService;
        private readonly ILogger<DailyClosingController> _logger;

        public DailyClosingController(
            IDailyClosingService closingService,
            ILogger<DailyClosingController> logger)
        {
            _closingService = closingService;
            _logger = logger;
        }

        /// <summary>
        /// Sales closes the day (creates pending closing)
        /// </summary>
        [HttpPost("close")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<ActionResult<ApiResponse<DailyClosingDto>>> CloseDailySales([FromBody] CloseDailySalesDto dto)
        {
            try
            {
                var result = await _closingService.CloseDailySalesAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error closing daily sales");
                return StatusCode(500, ApiResponse<DailyClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Finance approves the closing
        /// </summary>
        [HttpPost("approve")]
        [Authorize(Roles = "Finance,Manager")]
        public async Task<ActionResult<ApiResponse<DailyClosingDto>>> ApproveClosing([FromBody] ApproveDailyClosingDto dto)
        {
            try
            {
                var result = await _closingService.ApproveDailyClosingAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving daily closing");
                return StatusCode(500, ApiResponse<DailyClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Transfer between cash and bank (can be done anytime before closing)
        /// </summary>
        [HttpPost("transfer")]
        [Authorize(Roles = "Finance,Manager,Sales")]
        public async Task<ActionResult<ApiResponse<DailyClosingDto>>> TransferAmount([FromBody] TransferAmountDto dto)
        {
            try
            {
                var result = await _closingService.TransferAmountAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error transferring amount");
                return StatusCode(500, ApiResponse<DailyClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get closing status for a specific date
        /// </summary>
        [HttpGet("status/{branchId}/{date}")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<DailyClosingDto>>> GetClosingStatus(Guid branchId, DateTime date)
        {
            try
            {
                var result = await _closingService.GetClosingStatusAsync(branchId, date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closing status");
                return StatusCode(500, ApiResponse<DailyClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Get all closings for a branch
        /// </summary>
        [HttpGet("branch/{branchId}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<DailyClosingDto>>>> GetBranchClosings(
            Guid branchId,
            [FromQuery] DateTime? fromDate,
            [FromQuery] DateTime? toDate)
        {
            try
            {
                var result = await _closingService.GetBranchClosingsAsync(branchId, fromDate, toDate);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branch closings");
                return StatusCode(500, ApiResponse<List<DailyClosingDto>>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Reopen a closed date (admin only)
        /// </summary>
        [HttpPost("reopen")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<DailyClosingDto>>> ReopenDailySales([FromBody] ReopenDailySalesDto dto)
        {
            try
            {
                var result = await _closingService.ReopenDailySalesAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error reopening daily sales");
                return StatusCode(500, ApiResponse<DailyClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Check if a date is closed (used by UI)
        /// </summary>
        [HttpGet("is-closed/{branchId}/{date}")]
        [AllowAnonymous]
        public async Task<ActionResult<ApiResponse<bool>>> IsDateClosed(Guid branchId, DateTime date)
        {
            try
            {
                var result = await _closingService.IsDateClosedAsync(branchId, date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking if date is closed");
                return StatusCode(500, ApiResponse<bool>.Fail("Internal server error"));
            }
        }
        /// <summary>
        /// Get preview before closing (for sales/manager)
        /// </summary>
        [HttpGet("preview/{branchId}/{date}")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<ActionResult<ApiResponse<DailyClosingPreviewDto>>> GetClosingPreview(Guid branchId, DateTime date)
        {
            try
            {
                var result = await _closingService.GetClosingPreviewAsync(branchId, date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closing preview");
                return StatusCode(500, ApiResponse<DailyClosingPreviewDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Admin view - get all branches closing for a date
        /// </summary>
        [HttpGet("admin/all-branches/{date}")]
        [Authorize(Roles = "Admin,Finance")]
        public async Task<ActionResult<ApiResponse<AllBranchesClosingDto>>> GetAllBranchesClosing(DateTime date)
        {
            try
            {
                var result = await _closingService.GetAllBranchesClosingAsync(date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all branches closing");
                return StatusCode(500, ApiResponse<AllBranchesClosingDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Admin view - get specific branch closing detail
        /// </summary>
        [HttpGet("admin/branch/{branchId}/{date}")]
        [Authorize(Roles = "Admin,Finance")]
        public async Task<ActionResult<ApiResponse<BranchClosingSummaryDto>>> GetBranchClosingDetail(Guid branchId, DateTime date)
        {
            try
            {
                var result = await _closingService.GetBranchClosingDetailAsync(branchId, date);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting branch closing detail");
                return StatusCode(500, ApiResponse<BranchClosingSummaryDto>.Fail("Internal server error"));
            }
        }

        /// <summary>
        /// Admin view - get closings by date range
        /// </summary>
        [HttpPost("admin/date-range")]
        [Authorize(Roles = "Admin,Finance")]
        public async Task<ActionResult<ApiResponse<List<BranchClosingSummaryDto>>>> GetClosingsByDateRange([FromBody] DateRangeDto dto)
        {
            try
            {
                var result = await _closingService.GetClosingsByDateRangeAsync(dto);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting closings by date range");
                return StatusCode(500, ApiResponse<List<BranchClosingSummaryDto>>.Fail("Internal server error"));
            }
        }
    }
}