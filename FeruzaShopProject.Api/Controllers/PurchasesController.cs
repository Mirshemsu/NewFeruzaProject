using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace ShopMgtSys.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class PurchasesController : ControllerBase
    {
        private readonly IPurchaseService _purchaseService;
        private readonly ILogger<PurchasesController> _logger;
        private readonly IAuthorizationService _authorizationService;

        public PurchasesController(
            IPurchaseService purchaseService,
            ILogger<PurchasesController> logger,
            IAuthorizationService authorizationService)
        {
            _purchaseService = purchaseService ?? throw new ArgumentNullException(nameof(purchaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
        }

        // ========== 5-STEP PURCHASE WORKFLOW ENDPOINTS ==========

        // STEP 1: Sales creates purchase order
        [HttpPost]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Create([FromBody] CreatePurchaseOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Sales creating purchase order for BranchId: {BranchId}", dto.BranchId);

                // Check branch access
                var requirement = new BranchAccessRequirement(dto.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.CreatePurchaseOrderAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating purchase order for BranchId: {BranchId}", dto.BranchId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while creating purchase order"));
            }
        }

        // STEP 2: Admin accepts/reduces quantities
        [HttpPost("accept-quantities")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> AcceptQuantities([FromBody] AcceptPurchaseQuantitiesDto dto)
        {
            try
            {
                _logger.LogInformation("Admin accepting quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.AcceptQuantitiesByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while accepting quantities"));
            }
        }

        // STEP 3: Sales registers received quantities
        [HttpPost("register-received")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> RegisterReceived([FromBody] RegisterReceivedQuantitiesDto dto)
        {
            try
            {
                _logger.LogInformation("Sales registering received quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.RegisterReceivedQuantitiesAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error registering received quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while registering received quantities"));
            }
        }

        // STEP 4: Finance verification
        [HttpPost("finance-verification")]
        [Authorize(Roles = "Finance,Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> FinanceVerification([FromBody] FinanceVerificationDto dto)
        {
            try
            {
                _logger.LogInformation("Finance verifying purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.FinanceVerificationAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in finance verification for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred during finance verification"));
            }
        }

        // STEP 5: Admin final approval
        [HttpPost("final-approval")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> FinalApproval([FromBody] FinalApprovePurchaseOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Admin final approval for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.FinalApprovalByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error in final approval for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred during final approval"));
            }
        }

        // ========== ADDITIONAL OPERATIONS ==========

        [HttpPost("reject")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Reject([FromBody] RejectPurchaseOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Rejecting purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.RejectPurchaseOrderAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while rejecting purchase order"));
            }
        }

        [HttpPost("cancel")]
        [Authorize(Roles = "Sales,Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancel([FromBody] CancelPurchaseOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Canceling purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(ApiResponse<bool>.Fail("Purchase order not found"));
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.CancelPurchaseOrderAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while canceling purchase order"));
            }
        }

        [HttpPut]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Update([FromBody] UpdatePurchaseOrderDto dto)
        {
            try
            {
                _logger.LogInformation("Updating purchase order: {Id}", dto.Id);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.Id);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {Id} not found", dto.Id);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.UpdatePurchaseOrderAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error updating purchase order: {Id}", dto.Id);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while updating purchase order"));
            }
        }

        // ========== QUERY ENDPOINTS ==========

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Sales,Manager,Finance")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(Guid id)
        {
            try
            {
                var result = await _purchaseService.GetPurchaseOrderByIdAsync(id);
                if (!result.IsCompletedSuccessfully)
                    return NotFound(result);

                // Check branch access
                var requirement = new BranchAccessRequirement(result.Data.BranchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase order by ID: {Id}", id);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while retrieving purchase order"));
            }
        }

        [HttpGet]
        [Authorize(Roles = "Sales,Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetAll()
        {
            try
            {
                // For Sales users, filter by their branch
                if (User.IsInRole(Role.Sales.ToString()))
                {
                    var branchId = User.FindFirst("BranchId")?.Value;
                    _logger.LogInformation("Fetching purchase orders for Sales user with BranchId: {BranchId}", branchId);

                    if (Guid.TryParse(branchId, out var userBranchId))
                    {
                        var result = await _purchaseService.GetPurchaseOrdersByBranchAsync(userBranchId);
                        return Ok(result);
                    }
                    else
                    {
                        return BadRequest(ApiResponse<List<PurchaseOrderDto>>.Fail("Invalid branch ID"));
                    }
                }

                // For Manager/Finance, return all purchase orders
                var allResult = await _purchaseService.GetAllPurchaseOrdersAsync();
                return Ok(allResult);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting all purchase orders");
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders"));
            }
        }

        [HttpGet("status/{status}")]
        [Authorize(Roles = "Sales,Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetByStatus(PurchaseOrderStatus status)
        {
            try
            {
                // For Sales users, filter by their branch
                if (User.IsInRole(Role.Sales.ToString()))
                {
                    var branchId = User.FindFirst("BranchId")?.Value;
                    if (Guid.TryParse(branchId, out var userBranchId))
                    {
                        var branchOrders = await _purchaseService.GetPurchaseOrdersByBranchAsync(userBranchId);
                        if (branchOrders.IsCompletedSuccessfully)
                        {
                            var filtered = branchOrders.Data.Where(po => po.Status == status.ToString()).ToList();
                            return Ok(ApiResponse<List<PurchaseOrderDto>>.Success(filtered));
                        }
                    }
                }

                // For Manager/Finance, filter by status
                var result = await _purchaseService.GetPurchaseOrdersByStatusAsync(status);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase orders by status: {Status}", status);
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders by status"));
            }
        }

        [HttpGet("branch/{branchId:guid}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetByBranch(Guid branchId)
        {
            try
            {
                // Check branch access
                var requirement = new BranchAccessRequirement(branchId);
                var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                if (!authResult.Succeeded)
                    return Forbid();

                var result = await _purchaseService.GetPurchaseOrdersByBranchAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase orders by branch: {BranchId}", branchId);
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders by branch"));
            }
        }

        [HttpGet("creator/{createdBy:guid}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetByCreator(Guid createdBy)
        {
            try
            {
                var result = await _purchaseService.GetPurchaseOrdersByCreatorAsync(createdBy);

                // For Manager users, filter by their branch
                if (User.IsInRole(Role.Manager.ToString()))
                {
                    var branchId = User.FindFirst("BranchId")?.Value;
                    if (Guid.TryParse(branchId, out var userBranchId))
                    {
                        var filtered = result.Data.Where(po => po.BranchId == userBranchId).ToList();
                        return Ok(ApiResponse<List<PurchaseOrderDto>>.Success(filtered));
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase orders by creator: {CreatedBy}", createdBy);
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders by creator"));
            }
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderStatsDto>>> GetStats([FromQuery] Guid? branchId = null)
        {
            try
            {
                // For Manager users, default to their branch if no branchId specified
                if (User.IsInRole(Role.Manager.ToString()) && !branchId.HasValue)
                {
                    var userBranchId = User.FindFirst("BranchId")?.Value;
                    if (Guid.TryParse(userBranchId, out var branchGuid))
                    {
                        branchId = branchGuid;
                    }
                }

                // Check branch access if branchId is specified
                if (branchId.HasValue)
                {
                    var requirement = new BranchAccessRequirement(branchId.Value);
                    var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
                    if (!authResult.Succeeded)
                        return Forbid();
                }

                var result = await _purchaseService.GetPurchaseOrderStatsAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase order statistics for branch: {BranchId}", branchId);
                return StatusCode(500, ApiResponse<PurchaseOrderStatsDto>.Fail("An error occurred while retrieving purchase order statistics"));
            }
        }
    }
}