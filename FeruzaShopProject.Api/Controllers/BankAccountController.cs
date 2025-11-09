using FeruzaShopProject.Application.Interface;
using FeruzaShopProject.Domain.DTOs;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace FeruzaShopProject.Api.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    [Authorize]
    public class BankAccountController : ControllerBase
    {
        private readonly IBankAccountService _bankAccountService;

        public BankAccountController(IBankAccountService bankAccountService)
        {
            _bankAccountService = bankAccountService ?? throw new ArgumentNullException(nameof(bankAccountService));
        }

        [HttpPost]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> CreateBankAccount([FromBody] CreateBankAccountDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var response = await _bankAccountService.CreateBankAccountAsync(dto);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpPut]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> UpdateBankAccount([FromBody] UpdateBankAccountDto dto)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }
            var response = await _bankAccountService.UpdateBankAccountAsync(dto);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpGet("{id}")]
        public async Task<IActionResult> GetBankAccountById(Guid id)
        {
            var response = await _bankAccountService.GetBankAccountByIdAsync(id);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpGet]
        public async Task<IActionResult> GetAllBankAccounts()
        {
            var response = await _bankAccountService.GetAllBankAccountsAsync();
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }

        [HttpDelete("{id}")]
        [Authorize(Roles = "Manager")]
        public async Task<IActionResult> DeleteBankAccount(Guid id)
        {
            var response = await _bankAccountService.DeleteBankAccountAsync(id);
            if (!response.IsCompletedSuccessfully)
            {
                return BadRequest(response);
            }
            return Ok(response);
        }
    }
}
