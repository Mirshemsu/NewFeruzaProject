using Microsoft.AspNetCore.Identity;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FeruzaShopProject.Infrastructre.Data
{
    public class RoleSeeder
    {
        public static async Task SeedRolesAsync(RoleManager<IdentityRole<Guid>> roleManager)
        {
            // List of roles to seed
            var roles = new List<string> { "Manager", "Sales", "Finance" };

            foreach (var roleName in roles)
            {
                // Check if role already exists
                var roleExist = await roleManager.RoleExistsAsync(roleName);
                if (!roleExist)
                {
                    // Create the role if it doesn't exist
                    await roleManager.CreateAsync(new IdentityRole<Guid>(roleName));
                }
            }
        }
    }
}
