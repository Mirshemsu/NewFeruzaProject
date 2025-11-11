using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrder : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid SupplierId { get; set; }

        [Required]
        public Guid CreatedBy { get; set; }

        [Required, EnumDataType(typeof(PurchaseOrderStatus))]
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.PendingAdminAcceptance;

        public Branch Branch { get; set; }
        public Supplier Supplier { get; set; }
        public User Creator { get; set; }
        public List<PurchaseOrderItem> Items { get; set; } = new();
    }
}
