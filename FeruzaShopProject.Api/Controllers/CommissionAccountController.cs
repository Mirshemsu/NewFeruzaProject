using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class CommissionAccountController : ControllerBase
    {
        private readonly ICommissionAccountService _service;

        public CommissionAccountController(ICommissionAccountService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CommissionAccountDto>>>> GetAll([FromQuery] Guid? branchId)
        {
            var result = await _service.GetAllAsync(EffectiveBranchId(branchId));
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<CommissionAccountDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpGet("{id}/detail")]
        public async Task<ActionResult<ApiResponse<CommissionAccountDetailDto>>> GetDetail(
            Guid id,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var result = await _service.GetDetailAsync(id, startDate, endDate);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpGet("branch/{branchId}")]
        public async Task<ActionResult<ApiResponse<CommissionAccountDto>>> GetByBranch(Guid branchId)
        {
            var scoped = EffectiveBranchId(branchId) ?? branchId;
            var result = await _service.GetByBranchAsync(scoped);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<CommissionAccountDto>>> Create([FromBody] CreateCommissionAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<CommissionAccountDto>.Fail("Invalid data"));

            var result = await _service.CreateAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("allocation")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<CommissionLedgerEntryDto>>> AddAllocation(
            [FromBody] CreateCommissionAllocationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<CommissionLedgerEntryDto>.Fail("Invalid data"));

            var result = await _service.AddAllocationAsync(dto, CurrentUserId(), CurrentUserName());
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut("allocation")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<CommissionLedgerEntryDto>>> UpdateAllocation(
            [FromBody] UpdateCommissionAllocationDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<CommissionLedgerEntryDto>.Fail("Invalid data"));

            var result = await _service.UpdateAllocationAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("allocation/{id}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<bool>>> DeleteAllocation(Guid id)
        {
            var result = await _service.DeleteAllocationAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> SoftDelete(Guid id)
        {
            var result = await _service.SoftDeleteAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        private Guid? EffectiveBranchId(Guid? requested)
        {
            if (!User.IsInRole(Role.Sales.ToString()))
                return requested;

            var claim = User.FindFirst("BranchId")?.Value;
            return Guid.TryParse(claim, out var salesBranch) ? salesBranch : requested;
        }

        private Guid? CurrentUserId()
        {
            var raw = User.FindFirst("sub")?.Value
                ?? User.FindFirst("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/nameidentifier")?.Value;
            return Guid.TryParse(raw, out var id) ? id : null;
        }

        private string? CurrentUserName()
        {
            return User.FindFirst("name")?.Value
                ?? User.FindFirst("Name")?.Value
                ?? User.Identity?.Name;
        }
    }
}
