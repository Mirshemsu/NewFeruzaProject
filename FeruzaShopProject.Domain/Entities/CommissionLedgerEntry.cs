using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class CommissionLedgerEntry : BaseEntity
    {
        [Required]
        public Guid CommissionAccountId { get; set; }

        [Required]
        public CommissionLedgerEntryType EntryType { get; set; }

        /// <summary>
        /// Positive increases remaining (allocation, reversal, return adjustment).
        /// Negative decreases remaining (deduction, extra-deduct adjustment).
        /// </summary>
        public decimal SignedAmount { get; set; }

        public DateTime EntryDate { get; set; } = DateTime.UtcNow;

        [StringLength(50)]
        public string? CheckNumber { get; set; }

        [StringLength(500)]
        public string? Note { get; set; }

        public Guid? TransactionId { get; set; }

        [StringLength(200)]
        public string? ProductName { get; set; }

        [StringLength(50)]
        public string? ItemCode { get; set; }

        public Guid? CreatedByUserId { get; set; }

        [StringLength(100)]
        public string? CreatedByName { get; set; }

        public CommissionAccount CommissionAccount { get; set; }
        public Transaction Transaction { get; set; }
    }
}
