using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class BranchDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public string? Location { get; set; }
        public string? ContactInfo { get; set; }
        public int UserCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class CreateBranchDto
    {
        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [StringLength(100)]
        public string? ContactInfo { get; set; }
    }

    public class UpdateBranchDto
    {
        [Required]
        public Guid Id { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [StringLength(100)]
        public string? ContactInfo { get; set; }
    }

    public class BranchSummaryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int TotalSales { get; set; }
        public Decimal Revenue { get; set; }
    }
}
