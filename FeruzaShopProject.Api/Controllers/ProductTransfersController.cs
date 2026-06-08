using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductTransfersController : ControllerBase
    {
        private readonly IProductTransferService _transferService;
        private readonly ILogger<ProductTransfersController> _logger;

        public ProductTransfersController(
            IProductTransferService transferService,
            ILogger<ProductTransfersController> logger)
        {
            _transferService = transferService;
            _logger = logger;
        }

        // ========== STEP 1: SALES TRANSFER (Initiate) ==========
        [HttpPost("initiate")]
        [Authorize(Roles = "Admin,Manager,Sales")]
        public async Task<IActionResult> InitiateTransfer([FromBody] InitiateTransferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            try
            {
                var userId = GetCurrentUserId();
                var result = await _transferService.InitiateTransferAsync(dto, userId);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error initiating transfer");
                return StatusCode(500, ApiResponse<TransferResponseDto>.Fail("An error occurred while initiating transfer"));
            }
        }

        // ========== STEP 2: SALES RECEIVE ==========
        [HttpPost("receive")]
        [Authorize(Roles = "Admin,Manager,Sales")]
        public async Task<IActionResult> ReceiveTransfer([FromBody] ReceiveTransferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            try
            {
                var userId = GetCurrentUserId();
                var result = await _transferService.ReceiveTransferAsync(dto, userId);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error receiving transfer");
                return StatusCode(500, ApiResponse<TransferResponseDto>.Fail("An error occurred while receiving transfer"));
            }
        }

        // ========== STEP 3: FINANCE APPROVE ==========
        [HttpPost("approve")]
        [Authorize(Roles = "Admin,Manager,Finance")]
        public async Task<IActionResult> ApproveTransfer([FromBody] ApproveTransferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            try
            {
                var userId = GetCurrentUserId();
                var result = await _transferService.ApproveTransferAsync(dto, userId);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error approving transfer");
                return StatusCode(500, ApiResponse<TransferResponseDto>.Fail("An error occurred while approving transfer"));
            }
        }

        // ========== CANCEL TRANSFER ==========
        [HttpPost("cancel")]
        [Authorize(Roles = "Admin,Manager,Sales")]
        public async Task<IActionResult> CancelTransfer([FromBody] CancelTransferDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            try
            {
                var userId = GetCurrentUserId();
                var result = await _transferService.CancelTransferAsync(dto, userId);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error cancelling transfer");
                return StatusCode(500, ApiResponse<TransferResponseDto>.Fail("An error occurred while cancelling transfer"));
            }
        }

        // ========== GET TRANSFER BY ID ==========
        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Admin,Manager,Sales,Finance")]
        public async Task<IActionResult> GetTransfer(Guid id)
        {
            try
            {
                var result = await _transferService.GetTransferByIdAsync(id);
                return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transfer {TransferId}", id);
                return StatusCode(500, ApiResponse<TransferResponseDto>.Fail("An error occurred while retrieving transfer"));
            }
        }

        // ========== GET TRANSFERS BY BRANCH ==========
        [HttpGet("branch/{branchId:guid}")]
        [Authorize(Roles = "Admin,Manager,Sales,Finance")]
        public async Task<IActionResult> GetTransfersByBranch(Guid branchId)
        {
            try
            {
                _logger.LogInformation("Getting transfers for branch: {BranchId}", branchId);
                var result = await _transferService.GetTransfersByBranchAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting transfers for branch: {BranchId}", branchId);
                return StatusCode(500, ApiResponse<System.Collections.Generic.List<TransferResponseDto>>.Fail("An error occurred while retrieving transfers"));
            }
        }

        // ========== GET PENDING RECEIVES FOR BRANCH ==========
        [HttpGet("pending-receives/branch/{branchId:guid}")]
        [Authorize(Roles = "Admin,Manager,Sales")]
        public async Task<IActionResult> GetPendingReceivesByBranch(Guid branchId)
        {
            try
            {
                // You can implement this method in service if needed
                // var result = await _transferService.GetPendingReceivesByBranchAsync(branchId);
                // return Ok(result);

                // Or filter from existing method
                var result = await _transferService.GetTransfersByBranchAsync(branchId);
                if (result.IsCompletedSuccessfully && result.Data != null)
                {
                    var pendingReceives = result.Data.FindAll(t => t.Status == "PendingTransfer" 
                        /* You would need to check if this branch is destination */);
                    return Ok(ApiResponse<System.Collections.Generic.List<TransferResponseDto>>.Success(pendingReceives));
                }
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting pending receives for branch: {BranchId}", branchId);
                return StatusCode(500, ApiResponse<System.Collections.Generic.List<TransferResponseDto>>.Fail("An error occurred"));
            }
        }

        // Helper method to get current user ID from JWT or session
        private Guid GetCurrentUserId()
        {
            // Implement based on your authentication
            // Example from JWT claim:
            var userIdClaim = User.FindFirst("userId")?.Value ?? User.FindFirst("sub")?.Value;
            if (string.IsNullOrEmpty(userIdClaim))
                return Guid.Empty;

            return Guid.Parse(userIdClaim);
        }
    }
}