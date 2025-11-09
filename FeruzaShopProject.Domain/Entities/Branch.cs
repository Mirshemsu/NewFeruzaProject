using System;
using System.Collections;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class Branch : BaseEntity
    {
        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(255)]
        public string? Location { get; set; }

        [StringLength(100)]
        public string? ContactInfo { get; private set; }
        public List<Stock> Stocks { get; set; } = new();
        public List<BranchUser> Users { get; private set; } = new();
        
    }
}
