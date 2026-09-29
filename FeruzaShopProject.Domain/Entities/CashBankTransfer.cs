using System;
using System.ComponentModel.DataAnnotations;
using FeruzaShopProject.Domain.DTOs;

namespace FeruzaShopProject.Domain.Entities
{
    /// <summary>
    /// One cash-to-bank or bank-to-cash move for a branch on a sale date.
    /// Closing cash and bank are sales for that method plus these rows.
    /// </summary>
    public class CashBankTransfer : BaseEntity
    {
        [Required]
        public Guid BranchId { get; set; }

        [Required]
        public DateTime TransferDate { get; set; }

        [Required]
        public TransferDirection Direction { get; set; }

        [Required]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? BankReference { get; set; }

        [StringLength(500)]
        public string? Remarks { get; set; }

        [Required]
        public Guid CreatedByUserId { get; set; }

        public Branch Branch { get; set; }
        public User CreatedByUser { get; set; }
    }
}
