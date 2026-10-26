namespace RoleBasedApp.Models
{
    public class RolePermission
    {
        public int RoleId { get; set; }
        public int MenuId { get; set; }
        public int PermissionId { get; set; }
        public bool IsAllowed { get; set; } = true;
    }
}
