namespace RoleBasedApp.Models
{
    public enum ChangeRequestType
    {
        Create = 1,
        Update = 2,
        RoleChange = 3,
        Activate = 4,
        Deactivate = 5
    }

    public enum ApprovalStatus
    {
        Pending = 1,
        Approved = 2,
        Rejected = 3
    }
    public class UserChangeRequest
    {
        public int Id { get; set; }

        public int? TargetUserId { get; set; }

        public ChangeRequestType RequestType { get; set; }

        public ApprovalStatus Status { get; set; } =
            ApprovalStatus.Pending;

        public int MakerUserId { get; set; }

        public int? CheckerUserId { get; set; }

        public string PayloadJson { get; set; } = "";

        public string? RejectionReason { get; set; }

        public DateTime CreatedAtUtc { get; set; } =
            DateTime.UtcNow;

        public DateTime? DecidedAtUtc { get; set; }
    }
}
