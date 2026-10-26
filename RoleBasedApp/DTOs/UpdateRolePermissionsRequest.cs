using System.ComponentModel.DataAnnotations;

namespace RoleBasedApp.DTOs
{
    public class UpdateRolePermissionsRequest
    {
        [Required]
        public List<RolePermissionItem> Permissions { get; set; } =
     new();
    }

    public class RolePermissionItem
    {
        [Required]
        public int MenuId { get; set; }

        [Required]
        public string PermissionCode { get; set; } = "";

        public bool IsAllowed { get; set; }
    }
}
