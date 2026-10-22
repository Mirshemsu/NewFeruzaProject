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
    public class CustomersController : ControllerBase
    {
        private readonly ICustomerService _customers;

        public CustomersController(ICustomerService customers)
        {
            _customers = customers;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<CustomerListItemDto>>>> GetAll(
            [FromQuery] Guid? branchId,
            [FromQuery] string? search)
        {
            if (!TryScope(ref branchId, out var error))
                return error!;
            return Ok(await _customers.GetAllAsync(branchId, search));
        }

        [HttpGet("unlinked-credits")]
        public async Task<ActionResult<ApiResponse<List<CreditTransactionHistoryDto>>>> Unlinked(
            [FromQuery] Guid? branchId)
        {
            if (!TryScope(ref branchId, out var error))
                return error!;
            if (User.IsInRole(Role.Sales.ToString()) && branchId == null)
                return BadRequest(ApiResponse<List<CreditTransactionHistoryDto>>.Fail("No branch assigned."));
            return Ok(await _customers.GetUnlinkedCreditsAsync(branchId));
        }

        [HttpGet("{id:guid}/credits")]
        public async Task<ActionResult<ApiResponse<List<CreditTransactionHistoryDto>>>> Credits(Guid id)
        {
            Guid? branchId = null;
            if (!TryScope(ref branchId, out var error))
                return error!;
            var result = await _customers.GetCreditsAsync(id, branchId);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpPost]
        public async Task<ActionResult<ApiResponse<CustomerListItemDto>>> Create([FromBody] CreateCustomerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<CustomerListItemDto>.Fail("Invalid data"));

            var branchId = (Guid?)dto.BranchId;
            if (!TryScope(ref branchId, out var error))
                return error!;
            if (branchId == null)
                return BadRequest(ApiResponse<CustomerListItemDto>.Fail("Branch is required."));
            dto.BranchId = branchId.Value;
            var result = await _customers.CreateAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        public async Task<ActionResult<ApiResponse<CustomerListItemDto>>> Update([FromBody] UpdateCustomerDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<CustomerListItemDto>.Fail("Invalid data"));
            Guid? branchId = null;
            if (!TryScope(ref branchId, out var error))
                return error!;
            var result = await _customers.UpdateAsync(dto, branchId);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("import")]
        public async Task<ActionResult<ApiResponse<ImportCustomersResultDto>>> Import([FromBody] ImportCustomersDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<ImportCustomersResultDto>.Fail("Invalid data"));

            var branchId = (Guid?)dto.BranchId;
            if (!TryScope(ref branchId, out var error))
                return error!;
            if (branchId == null)
                return BadRequest(ApiResponse<ImportCustomersResultDto>.Fail("Branch is required."));
            dto.BranchId = branchId.Value;
            var result = await _customers.ImportAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("link")]
        public async Task<ActionResult<ApiResponse<bool>>> Link([FromBody] LinkCustomerSaleDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<bool>.Fail("Invalid data"));
            Guid? branchId = null;
            if (!TryScope(ref branchId, out var error))
                return error!;
            var result = await _customers.LinkSaleAsync(dto, branchId);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id:guid}")]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(Guid id)
        {
            Guid? branchId = null;
            if (!TryScope(ref branchId, out var error))
                return error!;
            var result = await _customers.DeactivateAsync(id, branchId);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        private bool TryScope(ref Guid? branchId, out ActionResult? error)
        {
            error = null;
            if (!User.IsInRole(Role.Sales.ToString()))
                return true;

            var claim = User.FindFirst("BranchId")?.Value;
            if (!Guid.TryParse(claim, out var salesBranch))
            {
                error = BadRequest(ApiResponse<string>.Fail("No branch assigned."));
                return false;
            }

            branchId = salesBranch;
            return true;
        }
    }
}
