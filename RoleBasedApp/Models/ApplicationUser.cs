using Microsoft.AspNetCore.Identity;

namespace RoleBasedApp.Models
{
    public class ApplicationUser : IdentityUser<int>
    {
        public string FullName { get; set; } = "";
        public bool IsActive { get; set; } = true;
        public DateTime CreatedAtUtc { get; set; } =
            DateTime.UtcNow;
    }
}
