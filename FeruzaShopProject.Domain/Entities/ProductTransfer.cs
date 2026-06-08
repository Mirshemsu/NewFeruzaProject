using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
      public class ProductTransfer : BaseEntity
        {
            [Required]
            public string TransferNumber { get; set; }

            [Required]
            public Guid ProductId { get; set; }

            [Required]
            public Guid FromBranchId { get; set; }

            [Required]
            public Guid ToBranchId { get; set; }

            [Required]
            public decimal Quantity { get; set; }

            [Required]
            public TransferStatus Status { get; set; }

            // Navigation
            public Product Product { get; set; }
            public Branch FromBranch { get; set; }
            public Branch ToBranch { get; set; }
        }

    // Domain/Entities/ProductTransfer.cs
    public enum TransferStatus
    {
        PendingTransfer = 1,   // Transfer requested, waiting for receive confirmation
        Received = 2,           // Received confirmation done, waiting for finance approval
        Approved = 3,           // Finance approved - STOCK UPDATED
        Rejected = 4,           // Rejected by finance - NO stock change
        Cancelled = 5           // Cancelled - NO stock change
    }
}
