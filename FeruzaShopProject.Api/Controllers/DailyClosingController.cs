using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FeruzaShopProject.Api.Controllers
{
    [ApiController]
    [Route("api/[controller]")]
    [Authorize]
    public class DailyClosingController : ControllerBase
    {
        private readonly IDailyClosingService _closingService;
        private readonly ILogger<DailyClosingController> _logger;

        public DailyClosingController(IDailyClosingService closingService, ILogger<DailyClosingController> logger)
        {
            _closingService = closingService;
            _logger = logger;
        }

        [HttpPost("close")]
        [Authorize(Roles = "Manager,Sales")]
        public async Task<IActionResult> CloseDailySales([FromBody] CloseDailySalesDto dto)
        {
            var result = await _closingService.CloseDailySalesAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("approve")]
        [Authorize(Roles = "Finance,Manager")]
        public async Task<IActionResult> ApproveClosing([FromBody] ApproveDailyClosingDto dto)
        {
            var result = await _closingService.ApproveDailyClosingAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpPost("reopen")]
        [Authorize(Roles = "Finance,Admin")]
        public async Task<IActionResult> ReopenDailySales([FromBody] ReopenDailySalesDto dto)
        {
            var result = await _closingService.ReopenDailySalesAsync(dto);
            return result.IsCompletedSuccessfully ? Ok(result) : BadRequest(result);
        }

        [HttpGet("status/{branchId}/{date}")]
        [Authorize(Roles = "Manager,Sales,Finance")]
        public async Task<IActionResult> GetClosingStatus(Guid branchId, DateTime date)
        {
            var result = await _closingService.GetClosingStatusAsync(branchId, date);
            return Ok(result);
        }

        [HttpGet("branch/{branchId}")]
        [Authorize(Roles = "Manager,Finance")]
        public async Task<IActionResult> GetBranchClosings(Guid branchId, [FromQuery] DateTime? fromDate, [FromQuery] DateTime? toDate)
        {
            var result = await _closingService.GetBranchClosingsAsync(branchId, fromDate, toDate);
            return Ok(result);
        }

        [HttpGet("is-closed/{branchId}/{date}")]
        [AllowAnonymous] // Used by UI to check if date is editable
        public async Task<IActionResult> IsDateClosed(Guid branchId, DateTime date)
        {
            var result = await _closingService.IsDateClosedAsync(branchId, date);
            return Ok(result);
        }
    }
}
