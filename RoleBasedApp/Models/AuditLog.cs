namespace RoleBasedApp.Models
{
    public class AuditLog
    {
        public long Id { get; set; }

        public int? UserId { get; set; }

        public string Username { get; set; } = "";

        public string Action { get; set; } = "";

        public string EntityName { get; set; } = "";

        public string? EntityId { get; set; }

        public string? OldValuesJson { get; set; }

        public string? NewValuesJson { get; set; }

        public string? IpAddress { get; set; }

        public string? CorrelationId { get; set; }

        public bool Success { get; set; }

        public string? ErrorCode { get; set; }

        public DateTime TimestampUtc { get; set; } =
            DateTime.UtcNow;
    }
}
