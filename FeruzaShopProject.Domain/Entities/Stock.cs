using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class Stock : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }
        [Required]
        public Guid ProductId { get; set; }
        [Required, Range(0, int.MaxValue)]
        public Decimal Quantity { get; set; }

        public Branch Branch { get; set; }
        public Product Product { get; set; }

        public void UpdateQuantity(int newQuantity)
        {
            Quantity = newQuantity;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
