namespace RoleBasedApp.Models
{
    public class UserRole
    {
        public int UserId { get; set; }
        public int RoleId { get; set; }
        public int AssignedByUserId { get; set; }
        public DateTime AssignedAtUtc { get; set; } =
            DateTime.UtcNow;
    }
}
