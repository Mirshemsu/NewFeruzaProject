using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class BranchUser : User
    {
        [Required]
        public Guid BranchId { get; set; }

        public Branch Branch { get; set; }
    }
}
