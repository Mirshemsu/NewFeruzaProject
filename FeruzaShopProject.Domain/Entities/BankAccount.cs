using System.ComponentModel.DataAnnotations;

namespace FeruzaShopProject.Domain.Entities
{
    public class BankAccount : BaseEntity
    {
        [Required, StringLength(120)]
        public string BankName { get; set; } = string.Empty;

        [Required, StringLength(50)]
        public string AccountNumber { get; set; } = string.Empty;

        [Required, StringLength(150)]
        public string AccountOwner { get; set; } = string.Empty;

        [Required]
        public Guid BranchId { get; set; }

        public Branch Branch { get; set; }
        public List<Transaction> Transactions { get; private set; } = new();
        public List<CreditPayment> CreditPayments { get; private set; } = new();
        public List<DailySales> DailySales { get; private set; } = new();
    }
}
