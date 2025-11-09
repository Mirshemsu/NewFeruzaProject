using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class BankAccount : BaseEntity
    {
        [Required, StringLength(100)]
        public string BankName { get; private set; }
        [Required, StringLength(50)]
        public string AccountNumber { get; private set; }
        [Required, StringLength(100)]
        public string AccountOwner { get; private set; }
        public Guid? BranchId { get; set; } // Optional, for branch-specific accounts
        public Branch Branch { get; set; }
    }
}
