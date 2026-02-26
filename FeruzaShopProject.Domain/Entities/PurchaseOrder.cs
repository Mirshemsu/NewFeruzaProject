using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;

namespace FeruzaShopProject.Domain.Entities
{
    public class PurchaseOrder : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public Guid CreatedBy { get; set; }

        [Required]
        public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.PendingAdminAcceptance;

        // Simple progress tracking
        public int TotalItems => Items?.Count ?? 0;
        public int AcceptedItems => Items?.Count(i => i.QuantityAccepted > 0) ?? 0;
        public int RegisteredItems => Items?.Count(i => i.QuantityRegistered > 0) ?? 0;
        public int FinanceVerifiedItems => Items?.Count(i => i.FinanceVerified == true) ?? 0;
        public int ApprovedItems => Items?.Count(i => i.ApprovedAt != null) ?? 0;

        // Navigation Properties
        public Branch Branch { get; set; }
        public User Creator { get; set; }
        public List<PurchaseOrderItem> Items { get; set; } = new();
        public List<PurchaseHistory> History { get; set; } = new();
    }
}