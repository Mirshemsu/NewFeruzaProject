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
    public class BankAccountController : ControllerBase
    {
        private readonly IBankAccountService _service;

        public BankAccountController(IBankAccountService service)
        {
            _service = service;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<BankAccountDto>>>> GetAll(
            [FromQuery] Guid? branchId,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var scopedBranch = EffectiveBranchId(branchId);
            var result = await _service.GetAllAsync(scopedBranch, startDate, endDate);
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<BankAccountDto>>> GetById(Guid id)
        {
            var result = await _service.GetByIdAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpGet("{id}/statement")]
        public async Task<ActionResult<ApiResponse<BankAccountStatementDto>>> GetStatement(
            Guid id,
            [FromQuery] DateTime? startDate,
            [FromQuery] DateTime? endDate)
        {
            var result = await _service.GetStatementAsync(id, startDate, endDate);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpPost]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<BankAccountDto>>> Create([FromBody] CreateBankAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<BankAccountDto>.Fail("Invalid data"));

            var result = await _service.CreateAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<ActionResult<ApiResponse<BankAccountDto>>> Update([FromBody] UpdateBankAccountDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<BankAccountDto>.Fail("Invalid data"));

            var result = await _service.UpdateAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
        {
            var result = await _service.DeleteAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        private Guid? EffectiveBranchId(Guid? requested)
        {
            if (!User.IsInRole(Role.Sales.ToString()))
                return requested;

            var claim = User.FindFirst("BranchId")?.Value;
            if (Guid.TryParse(claim, out var salesBranch))
                return salesBranch;

            return requested;
        }
    }
}
