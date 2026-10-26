using System.ComponentModel.DataAnnotations;

namespace RoleBasedApp.DTOs
{
    public class RejectRequest
    {
        [Required]
        [StringLength(500, MinimumLength = 3)]
        public string Reason { get; set; } = "";
    }
}
