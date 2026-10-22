using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CustomerListItemDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PhoneNumber { get; set; } = string.Empty;
        public Guid BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int CreditCount { get; set; }
        public decimal TotalCreditAmount { get; set; }
        public decimal TotalPaidAmount { get; set; }
        public decimal RemainingAmount { get; set; }
    }

    public class CreateCustomerDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class UpdateCustomerDto
    {
        [Required]
        public Guid Id { get; set; }

        [Required, StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required, StringLength(30)]
        public string PhoneNumber { get; set; } = string.Empty;
    }

    public class ImportCustomersDto
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public List<ImportCustomerRowDto> Rows { get; set; } = new();
    }

    public class ImportCustomerRowDto
    {
        public int RowNumber { get; set; }

        public string? Name { get; set; }

        public string? PhoneNumber { get; set; }
    }

    public class ImportCustomersResultDto
    {
        public int AddedCount { get; set; }
        public List<ImportCustomerSkipDto> Skipped { get; set; } = new();
    }

    public class ImportCustomerSkipDto
    {
        public int RowNumber { get; set; }
        public string? Name { get; set; }
        public string? PhoneNumber { get; set; }
        public string Reason { get; set; } = string.Empty;
    }

    public class LinkCustomerSaleDto
    {
        [Required]
        public Guid TransactionId { get; set; }

        [Required]
        public Guid CustomerId { get; set; }
    }
}
