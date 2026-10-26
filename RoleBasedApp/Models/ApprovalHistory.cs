namespace RoleBasedApp.Models
{
    public class ApprovalHistory
    {
        public long Id { get; set; }

        public int RequestId { get; set; }

        public int MakerUserId { get; set; }

        public int CheckerUserId { get; set; }

        public ApprovalStatus Decision { get; set; }

        public string? Comments { get; set; }

        public DateTime DecisionAtUtc { get; set; } =
            DateTime.UtcNow;
    }
}
