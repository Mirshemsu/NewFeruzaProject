using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class DailyStock : BaseEntity
    {
        [Required]
        public Guid ProductId { get; set; }

        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime Date { get; set; }

        [Required]
        public decimal Quantity { get; set; }

        public Product Product { get; set; }
        public Branch Branch { get; set; }
    }
}
