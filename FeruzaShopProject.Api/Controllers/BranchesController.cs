using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using FeruzaShopProject.Domain.Entities;
using FeruzaShopProject.Domain.Shared;
using FeruzaShopProject.Infrastructre.Authorization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class BranchesController : ControllerBase
    {
        private readonly IBranchService _branchService;
        private readonly ILogger<BranchesController> _logger;
        private readonly IAuthorizationService _authorizationService;

        public BranchesController(
            IBranchService branchService,
            ILogger<BranchesController> logger,
            IAuthorizationService authorizationService)
        {
            _branchService = branchService;
            _logger = logger;
            _authorizationService = authorizationService;
        }

        [HttpGet]
        public async Task<ActionResult<ApiResponse<List<BranchDto>>>> GetAll()
        {
            // Only Managers/Finance can list all branches
            if (!User.IsInRole(Role.Manager.ToString()) && !User.IsInRole(Role.Finance.ToString()))
            {
                return Forbid();
            }

            var result = await _branchService.GetAllBranchesAsync();
            return Ok(result);
        }

        [HttpGet("{id}")]
        public async Task<ActionResult<ApiResponse<BranchDto>>> GetById(Guid id)
        {
            // Check branch access dynamically
            var authResult = await _authorizationService.AuthorizeAsync(
                User,
                new BranchAccessRequirement(id),
                "BranchAccess");

            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var result = await _branchService.GetBranchByIdAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }

        
        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<BranchDto>>> Create([FromBody] CreateBranchDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<BranchDto>.Fail("Invalid data"));

            var result = await _branchService.CreateBranchAsync(dto);
            return result.IsCompletedSuccessfully
                ? CreatedAtAction(nameof(GetById), new { id = result.Data.Id }, result)
                : BadRequest(result);
        }

        [HttpPut]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<BranchDto>>> Update([FromBody] UpdateBranchDto dto)
        {
            if (!ModelState.IsValid)
                return BadRequest(ApiResponse<BranchDto>.Fail("Invalid data"));

            var result = await _branchService.UpdateBranchAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<ActionResult<ApiResponse<bool>>> Delete(Guid id)
        {
            var result = await _branchService.DeleteBranchAsync(id);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpGet("my-branch")]
        [Authorize(Roles = "Sales")] // Sales-specific endpoint
        public async Task<ActionResult<ApiResponse<BranchDto>>> GetMyBranch()
        {
            // Get branchId from the user's claims
            var branchId = User.FindFirst("BranchId")?.Value;
            if (string.IsNullOrEmpty(branchId) || !Guid.TryParse(branchId, out var parsedId))
            {
                return BadRequest(ApiResponse<BranchDto>.Fail("Invalid branch assignment"));
            }

            var result = await _branchService.GetBranchByIdAsync(parsedId);
            return result.IsCompletedSuccessfully ? Ok(result) : NotFound(result);
        }
    }
}