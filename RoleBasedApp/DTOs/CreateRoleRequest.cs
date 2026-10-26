using System.ComponentModel.DataAnnotations;

namespace RoleBasedApp.DTOs
{
    public class CreateRoleRequest
    {
        [Required]
        [StringLength(50)]
        public string Name { get; set; } = "";

        [StringLength(250)]
        public string Description { get; set; } = "";
    }
}
