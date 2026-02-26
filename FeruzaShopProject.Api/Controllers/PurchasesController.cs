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

        // ========== HELPER METHODS ==========
        private async Task<bool> HasBranchAccessAsync(Guid branchId)
        {
            var requirement = new BranchAccessRequirement(branchId);
            var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
            return authResult.Succeeded;
        }

        private Guid? GetUserBranchId()
        {
            var branchIdClaim = User.FindFirst("BranchId")?.Value;
            if (Guid.TryParse(branchIdClaim, out var branchId))
                return branchId;
            return null;
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
                if (!await HasBranchAccessAsync(dto.BranchId))
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        // STEP 3: Sales registers received quantities (can be done multiple times)
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        // STEP 4: Finance verification (partial supported)
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        // STEP 5: Admin final approval (partial supported)
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        // ========== SALES EDIT/DELETE OPERATIONS ==========

        /// <summary>
        /// Sales can edit their purchase order only when status is PendingAdminAcceptance
        /// </summary>
        [HttpPut("sales-edit")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditBySales([FromBody] EditPurchaseOrderBySalesDto dto)
        {
            try
            {
                _logger.LogInformation("Sales editing purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditPurchaseOrderBySalesAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing purchase order by sales: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing purchase order"));
            }
        }

        /// <summary>
        /// Sales can delete their own purchase order only when status is PendingAdminAcceptance
        /// </summary>
        [HttpDelete("sales-delete/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteBySales(Guid purchaseOrderId, [FromQuery] string? reason = null)
        {
            try
            {
                _logger.LogInformation("Sales deleting purchase order: {PurchaseOrderId}", purchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(purchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", purchaseOrderId);
                    return NotFound(ApiResponse<bool>.Fail("Purchase order not found"));
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.DeletePurchaseOrderBySalesAsync(purchaseOrderId, reason);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting purchase order by sales: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while deleting purchase order"));
            }
        }

        /// <summary>
        /// Sales can edit registered quantities before finance verification
        /// </summary>
        [HttpPut("edit-registered-quantities")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditRegisteredQuantitiesBySales([FromBody] EditRegisteredQuantitiesBySalesDto dto)
        {
            try
            {
                _logger.LogInformation("Sales editing registered quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditRegisteredQuantitiesBySalesAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing registered quantities by sales: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing registered quantities"));
            }
        }

        // ========== ADMIN EDIT/DELETE OPERATIONS ==========

        /// <summary>
        /// Admin can edit any purchase order at any stage (except FullyApproved)
        /// </summary>
        [HttpPut("admin-edit")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditByAdmin([FromBody] EditPurchaseOrderByAdminDto dto)
        {
            try
            {
                _logger.LogInformation("Admin editing purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditPurchaseOrderByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing purchase order by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing purchase order"));
            }
        }

        /// <summary>
        /// Admin can edit only accepted quantities
        /// </summary>
        [HttpPut("edit-accepted-quantities")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditAcceptedQuantitiesByAdmin([FromBody] EditAcceptedQuantitiesByAdminDto dto)
        {
            try
            {
                _logger.LogInformation("Admin editing accepted quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditAcceptedQuantitiesByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing accepted quantities by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing accepted quantities"));
            }
        }

        /// <summary>
        /// Admin can edit only registered quantities
        /// </summary>
        [HttpPut("edit-registered-quantities-admin")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditRegisteredQuantitiesByAdmin([FromBody] EditRegisteredQuantitiesByAdminDto dto)
        {
            try
            {
                _logger.LogInformation("Admin editing registered quantities for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditRegisteredQuantitiesByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing registered quantities by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing registered quantities"));
            }
        }

        /// <summary>
        /// Admin can edit prices at any time before final approval
        /// </summary>
        [HttpPut("edit-prices")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> EditPricesByAdmin([FromBody] EditPricesByAdminDto dto)
        {
            try
            {
                _logger.LogInformation("Admin editing prices for purchase order: {PurchaseOrderId}", dto.PurchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.PurchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", dto.PurchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.EditPricesByAdminAsync(dto);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error editing prices by admin: {PurchaseOrderId}", dto.PurchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while editing prices"));
            }
        }

        /// <summary>
        /// Admin can delete any purchase order (except FullyApproved)
        /// </summary>
        [HttpDelete("admin-delete/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteByAdmin(Guid purchaseOrderId, [FromQuery] string reason)
        {
            try
            {
                _logger.LogInformation("Admin deleting purchase order: {PurchaseOrderId}", purchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(purchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", purchaseOrderId);
                    return NotFound(ApiResponse<bool>.Fail("Purchase order not found"));
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.DeletePurchaseOrderByAdminAsync(purchaseOrderId, reason);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error deleting purchase order by admin: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while deleting purchase order"));
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        [HttpPost("reject-simple/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> RejectSimple(Guid purchaseOrderId, [FromQuery] string reason)
        {
            try
            {
                _logger.LogInformation("Rejecting purchase order: {PurchaseOrderId}", purchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(purchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", purchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.RejectPurchaseOrderAsync(purchaseOrderId, reason);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error rejecting purchase order: {PurchaseOrderId}", purchaseOrderId);
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        [HttpPost("cancel-simple/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Sales,Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> CancelSimple(Guid purchaseOrderId, [FromQuery] string? reason = null)
        {
            try
            {
                _logger.LogInformation("Canceling purchase order: {PurchaseOrderId}", purchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(purchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", purchaseOrderId);
                    return NotFound(ApiResponse<bool>.Fail("Purchase order not found"));
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.CancelPurchaseOrderAsync(purchaseOrderId, reason);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error canceling purchase order: {PurchaseOrderId}", purchaseOrderId);
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
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
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

        // ========== CONVENIENCE/HELPER ENDPOINTS ==========

        [HttpPost("accept-all/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> AcceptAll(Guid purchaseOrderId)
        {
            try
            {
                _logger.LogInformation("Admin accepting all quantities for purchase order: {PurchaseOrderId}", purchaseOrderId);

                var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(purchaseOrderId);
                if (!purchaseOrder.IsCompletedSuccessfully)
                {
                    _logger.LogWarning("Purchase order {PurchaseOrderId} not found", purchaseOrderId);
                    return NotFound(purchaseOrder);
                }

                // Check branch access
                if (!await HasBranchAccessAsync(purchaseOrder.Data.BranchId))
                    return Forbid();

                var result = await _purchaseService.AcceptAllByAdminAsync(purchaseOrderId);
                return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error accepting all quantities for purchase order: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, ApiResponse<PurchaseOrderDto>.Fail("An error occurred while accepting all quantities"));
            }
        }

        [HttpGet("can-edit/{purchaseOrderId:guid}")]
        [Authorize(Roles = "Sales,Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> CanEdit(Guid purchaseOrderId)
        {
            try
            {
                var userIdClaim = User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
                if (!Guid.TryParse(userIdClaim, out var userId))
                {
                    return Unauthorized(ApiResponse<bool>.Fail("Invalid user"));
                }

                var result = await _purchaseService.CanUserEditAsync(purchaseOrderId, userId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error checking edit permission for purchase order: {PurchaseOrderId}", purchaseOrderId);
                return StatusCode(500, ApiResponse<bool>.Fail("An error occurred while checking edit permission"));
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
                if (!await HasBranchAccessAsync(result.Data.BranchId))
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
                if (User.IsInRole("Sales"))
                {
                    var branchId = GetUserBranchId();
                    if (branchId.HasValue)
                    {
                        _logger.LogInformation("Fetching purchase orders for Sales user with BranchId: {BranchId}", branchId);
                        var result = await _purchaseService.GetPurchaseOrdersByBranchAsync(branchId.Value);
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
                if (User.IsInRole("Sales"))
                {
                    var branchId = GetUserBranchId();
                    if (branchId.HasValue)
                    {
                        var branchOrders = await _purchaseService.GetPurchaseOrdersByBranchAsync(branchId.Value);
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
                if (!await HasBranchAccessAsync(branchId))
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
                if (User.IsInRole("Manager"))
                {
                    var branchId = GetUserBranchId();
                    if (branchId.HasValue)
                    {
                        var filtered = result.Data.Where(po => po.BranchId == branchId.Value).ToList();
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

        [HttpGet("date-range")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetByDateRange(
            [FromQuery] DateTime fromDate,
            [FromQuery] DateTime toDate,
            [FromQuery] Guid? branchId = null)
        {
            try
            {
                // Check branch access if branchId is specified
                if (branchId.HasValue && !await HasBranchAccessAsync(branchId.Value))
                    return Forbid();

                // For Manager users, default to their branch if no branchId specified
                if (User.IsInRole("Manager") && !branchId.HasValue)
                {
                    branchId = GetUserBranchId();
                }

                var result = await _purchaseService.GetPurchaseOrdersByDateRangeAsync(fromDate, toDate, branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase orders by date range");
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders by date range"));
            }
        }

        [HttpGet("supplier/{supplierId:guid}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetBySupplier(Guid supplierId)
        {
            try
            {
                var result = await _purchaseService.GetPurchaseOrdersBySupplierAsync(supplierId);

                // For Manager users, filter by their branch
                if (User.IsInRole("Manager"))
                {
                    var branchId = GetUserBranchId();
                    if (branchId.HasValue)
                    {
                        var filtered = result.Data.Where(po => po.BranchId == branchId.Value).ToList();
                        return Ok(ApiResponse<List<PurchaseOrderDto>>.Success(filtered));
                    }
                }

                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase orders by supplier: {SupplierId}", supplierId);
                return StatusCode(500, ApiResponse<List<PurchaseOrderDto>>.Fail("An error occurred while retrieving purchase orders by supplier"));
            }
        }

        [HttpGet("stats")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderStatsDto>>> GetStats([FromQuery] Guid? branchId = null)
        {
            try
            {
                // For Manager users, default to their branch if no branchId specified
                if (User.IsInRole("Manager") && !branchId.HasValue)
                {
                    branchId = GetUserBranchId();
                }

                // Check branch access if branchId is specified
                if (branchId.HasValue && !await HasBranchAccessAsync(branchId.Value))
                    return Forbid();

                var result = await _purchaseService.GetPurchaseOrderStatsAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase order statistics for branch: {BranchId}", branchId);
                return StatusCode(500, ApiResponse<PurchaseOrderStatsDto>.Fail("An error occurred while retrieving purchase order statistics"));
            }
        }

        [HttpGet("dashboard")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDashboardDto>>> GetDashboard([FromQuery] Guid? branchId = null)
        {
            try
            {
                // For Manager users, default to their branch if no branchId specified
                if (User.IsInRole("Manager") && !branchId.HasValue)
                {
                    branchId = GetUserBranchId();
                }

                // Check branch access if branchId is specified
                if (branchId.HasValue && !await HasBranchAccessAsync(branchId.Value))
                    return Forbid();

                var result = await _purchaseService.GetPurchaseOrderDashboardAsync(branchId);
                return Ok(result);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting purchase order dashboard");
                return StatusCode(500, ApiResponse<PurchaseOrderDashboardDto>.Fail("An error occurred while retrieving purchase order dashboard"));
            }
        }
    }
}