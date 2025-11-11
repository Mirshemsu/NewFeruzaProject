using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseHistory : BaseEntity
    {
        [Required]
        public Guid PurchaseOrderId { get; set; }
        [Required]
        public string Action { get; set; } // e.g., "Submitted", "Received", "Approved"
        [Required]
        public Guid PerformedByUserId { get; set; }
        public string Details { get; set; } // e.g., "Received 100 items", "Approved 100 items"
        public PurchaseOrder PurchaseOrder { get; set; }
        public User PerformedByUser { get; set; }
    }
}
