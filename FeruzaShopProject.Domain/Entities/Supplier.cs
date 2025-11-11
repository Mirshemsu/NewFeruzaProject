using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class Supplier : BaseEntity
    {
        [Required, StringLength(100)]
        public string Name { get; set; }

        [StringLength(100)]
        public string? ContactInfo { get; set; }

        public string? Address { get; set; }

        public List<PurchaseOrder> PurchaseOrders { get; set; } = new();

        public void Update(string name, string? contactInfo, string? address)
        {
            Name = name ?? throw new ArgumentNullException(nameof(name));
            ContactInfo = contactInfo;
            Address = address;
            UpdatedAt = DateTime.UtcNow;
        }
    }
}
