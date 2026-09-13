using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class CommissionAccount : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        public Branch Branch { get; set; }
        public List<CommissionLedgerEntry> Entries { get; private set; } = new();
    }
}
