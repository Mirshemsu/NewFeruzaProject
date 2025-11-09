using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.DTOs
{
    public class CategoryDto
    {
        public Guid Id { get; set; }
        public string Name { get; set; }
        public int ProductCount { get; set; }  // Optional: Show linked products
    }
    public class CreateCategoryDto
    {
        [Required, StringLength(100)]
        public string Name { get; set; }
    }
    public class UpdateCategoryDto
    {
        [Required]
        public Guid Id { get; set; }

        [StringLength(100)]
        public string? Name { get; set; }
    }
}
