using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Domain.Entities
{
    public class User : IdentityUser<Guid>
    {
        [StringLength(100)]
        public string? Name { get; set; }

        [StringLength(100)]
        public string? ContactInfo { get; set; }
        public Role Role { get; set; }


    }
}
