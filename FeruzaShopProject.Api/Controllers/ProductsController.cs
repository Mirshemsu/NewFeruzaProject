using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Shared;
using System;
using System.Threading.Tasks;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class ProductsController : ControllerBase
    {
        private readonly IProductService _productService;
        private readonly ILogger<ProductsController> _logger;

        public ProductsController(
            IProductService productService,
            ILogger<ProductsController> logger)
        {
            _productService = productService;
            _logger = logger;
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> Create([FromBody] CreateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            var result = await _productService.CreateProductAsync(dto);
            return result.IsCompletedSuccessfully
                ? CreatedAtAction(nameof(Get), new { id = result.Data?.Id }, result)
                : BadRequest(result);
        }

        [HttpPut]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> Update([FromBody] UpdateProductDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            var result = await _productService.UpdateProductAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpGet("{id:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> Get(Guid id)
        {
            var result = await _productService.GetProductByIdAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetAll()
        {
            var result = await _productService.GetAllProductsAsync();
            return Ok(result);
        }

        [HttpGet("category/{categoryId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByCategory(Guid categoryId)
        {
            var result = await _productService.GetProductsByCategoryAsync(categoryId);
            return Ok(result);
        }

        [HttpGet("branch/{branchId:guid}")]
        [AllowAnonymous]
        public async Task<IActionResult> GetByBranch(Guid branchId)
        {
            var result = await _productService.GetProductsByBranchAsync(branchId);
            return Ok(result);
        }

        [HttpGet("low-stock")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> GetLowStock([FromQuery] int threshold = 10)
        {
            var result = await _productService.GetLowStockProductsAsync(threshold);
            return Ok(result);
        }

        [HttpDelete("{id:guid}")]
        [Authorize(Roles = "Admin")]
        public async Task<IActionResult> Delete(Guid id)
        {
            var result = await _productService.DeleteProductAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPatch("adjust-stock")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> AdjustStock([FromBody] AdjustStockDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            var result = await _productService.AdjustStockAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }
        [HttpPost("add-to-branch")]
        [Authorize(Roles = "Admin,Manager")]
        public async Task<IActionResult> AddProductToBranch([FromBody] AddProductToBranchDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<object>.Fail("Invalid data"));

            var result = await _productService.AddProductToBranchAsync(dto);
            return Ok(result);
        }
        [HttpGet("branch/{branchId:guid}/stock")]
        [Authorize(Roles = "Admin,Manager,Staff")]
        public async Task<IActionResult> GetProductStockByBranch(Guid branchId)
        {
            var result = await _productService.GetProductStockByBranchAsync(branchId);
            return Ok(result);
        }
    }
}