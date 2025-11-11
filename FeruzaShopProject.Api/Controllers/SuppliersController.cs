using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class SuppliersController : ControllerBase
    {
        private readonly ISupplierService _supplierService;

        public SuppliersController(ISupplierService supplierService)
        {
            _supplierService = supplierService ?? throw new ArgumentNullException(nameof(supplierService));
        }

        [HttpPost]
        [Authorize(Policy = "RequireManagerOrFinanceRole")]
        public async Task<ActionResult<ApiResponse<SupplierDto>>> Create([FromBody] CreateSupplierDto dto)
        {
            var result = await _supplierService.CreateSupplierAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPut]
        [Authorize(Policy = "RequireManagerOrFinanceRole")]
        public async Task<ActionResult<ApiResponse<SupplierDto>>> Update([FromBody] UpdateSupplierDto dto)
        {
            var result = await _supplierService.UpdateSupplierAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id:guid}")]
        [Authorize(Policy = "RequireManagerOrFinanceRole")]
        public async Task<ActionResult<ApiResponse<SupplierDto>>> GetById(Guid id)
        {
            var result = await _supplierService.GetSupplierByIdAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpGet]
        [Authorize(Policy = "RequireManagerOrFinanceRole")]
        public async Task<ActionResult<ApiResponse<List<SupplierDto>>>> GetAll()
        {
            var result = await _supplierService.GetAllSuppliersAsync();
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Policy = "RequireManagerOrFinanceRole")]
        public async Task<ActionResult<ApiResponse<bool>>> Deactivate(Guid id)
        {
            var result = await _supplierService.DeactivateSupplierAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }
    }
}
