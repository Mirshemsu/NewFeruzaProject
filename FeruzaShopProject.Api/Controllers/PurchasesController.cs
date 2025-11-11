using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Security.Claims;
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
        private readonly IHttpContextAccessor _httpContextAccessor;

        public PurchasesController(
            IPurchaseService purchaseService,
            ILogger<PurchasesController> logger,
            IAuthorizationService authorizationService,
            IHttpContextAccessor httpContextAccessor)
        {
            _purchaseService = purchaseService ?? throw new ArgumentNullException(nameof(purchaseService));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
            _authorizationService = authorizationService ?? throw new ArgumentNullException(nameof(authorizationService));
            _httpContextAccessor = httpContextAccessor ?? throw new ArgumentNullException(nameof(httpContextAccessor));
        }

        [HttpPost]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Create([FromBody] CreatePurchaseOrderDto dto)
        {
            _logger.LogInformation($"Sales creating purchase order for BranchId: {dto.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(dto.BranchId);
            var result = await _purchaseService.CreatePurchaseOrderAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/accept-admin")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> AcceptByAdmin(Guid id)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }

            _logger.LogInformation($"Admin accepting purchase order {id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            var result = await _purchaseService.AcceptByAdminAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("receive")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Receive([FromBody] ReceivePurchaseOrderDto dto)
        {
            _logger.LogInformation($"Receiving purchase order {dto.Id}");

            // 1️⃣ Fetch purchase order first to get BranchId
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.Id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {dto.Id} not found");
                return NotFound(purchaseOrder);
            }

            // 2️⃣ Create requirement with actual BranchId
            var requirement = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            // 3️⃣ Manually authorize
            var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);
            if (!authResult.Succeeded)
                return Forbid();

            // 4️⃣ Proceed to receive
            var result = await _purchaseService.ReceivePurchaseOrderAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/checkout-finance")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> CheckoutByFinance(Guid id)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }

            _logger.LogInformation($"Finance checking out purchase order {id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            var result = await _purchaseService.CheckoutByFinanceAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/final-approve-admin")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> FinalApproveByAdmin(Guid id)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }

            _logger.LogInformation($"Admin final approval for purchase order {id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            var result = await _purchaseService.FinalApproveByAdminAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("{id:guid}/reject")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Reject(Guid id, [FromBody] string reason)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }

            _logger.LogInformation($"Rejecting purchase order {id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            var result = await _purchaseService.RejectPurchaseOrderAsync(id, reason);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> Update([FromBody] UpdatePurchaseOrderDto dto)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(dto.Id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {dto.Id} not found");
                return NotFound(purchaseOrder);
            }
            _logger.LogInformation($"Updating purchase order {dto.Id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
            var result = await _purchaseService.UpdatePurchaseOrderAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> GetById(Guid id)
        {
            var result = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!result.IsCompletedSuccessfully)
                return NotFound(result);

            var requirement = new BranchAccessRequirement(result.Data.BranchId);
            var authResult = await _authorizationService.AuthorizeAsync(User, null, requirement);

            if (!authResult.Succeeded)
                return Forbid();

            return Ok(result);
        }

        [HttpGet]
        [Authorize(Roles = "Sales")]
        public async Task<ActionResult<ApiResponse<List<PurchaseOrderDto>>>> GetAll()
        {
            if (User.IsInRole(Role.Sales.ToString()))
            {
                var branchId = User.FindFirst("BranchId")?.Value;
                _logger.LogInformation($"Fetching all purchase orders for Sales user with BranchId: {branchId}");
                if (Guid.TryParse(branchId, out var userBranchId))
                {
                    HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(userBranchId);
                }
            }
            var result = await _purchaseService.GetAllPurchaseOrdersAsync();
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> Cancel(Guid id)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }
            _logger.LogInformation($"Canceling purchase order {id} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);
            var result = await _purchaseService.CancelPurchaseOrderAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut("{id:guid}/status")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<PurchaseOrderDto>>> UpdateStatus(Guid id, [FromBody] PurchaseOrderStatus status)
        {
            var purchaseOrder = await _purchaseService.GetPurchaseOrderByIdAsync(id);
            if (!purchaseOrder.IsCompletedSuccessfully)
            {
                _logger.LogWarning($"Purchase order {id} not found");
                return NotFound(purchaseOrder);
            }

            _logger.LogInformation($"Updating status of purchase order {id} to {status} for BranchId: {purchaseOrder.Data.BranchId}");
            HttpContext.Items["BranchAccessRequirement"] = new BranchAccessRequirement(purchaseOrder.Data.BranchId);

            var result = await _purchaseService.UpdatePurchaseOrderStatusAsync(id, status);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }
    }
}