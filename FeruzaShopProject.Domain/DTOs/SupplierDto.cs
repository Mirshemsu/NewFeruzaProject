using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CreateSupplierDto
    {
        [Required, StringLength(100)]
        public string Name { get; init; }

        [StringLength(100)]
        public string? ContactInfo { get; init; }

        public string? Address { get; init; }
    }

    public class UpdateSupplierDto
    {
        [Required]
        public Guid Id { get; init; }

        [Required, StringLength(100)]
        public string Name { get; init; }

        [StringLength(100)]
        public string? ContactInfo { get; init; }

        public string? Address { get; init; }
    }

    public class SupplierDto
    {
        public Guid Id { get; init; }
        public string Name { get; init; }
        public string? ContactInfo { get; init; }
        public string? Address { get; init; }
        public bool IsActive { get; init; }
        public DateTime CreatedAt { get; init; }
        public DateTime? UpdatedAt { get; init; }
    }
}
