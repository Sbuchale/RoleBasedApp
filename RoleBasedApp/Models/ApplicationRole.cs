using Microsoft.AspNetCore.Identity;

namespace RoleBasedApp.Models
{
    public class ApplicationRole : IdentityRole<int>
    {
        public string Description { get; set; } = "";
        public bool IsActive { get; set; } = true;
    }
}
