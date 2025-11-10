using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class Customer : BaseEntity
    {
        [Required, StringLength(200)]
        public string Name { get; set; }

        [Required, StringLength(20)]
        public string PhoneNumber { get; set; }

        // Navigation properties
        public List<Transaction> Transactions { get; set; } = new();
        public List<DailySales> DailySales { get; private set; } = new();
    }
}
