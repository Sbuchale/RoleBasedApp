using System.ComponentModel.DataAnnotations;

namespace RoleBasedApp.DTOs
{
    public class CreateUserRequest
    {
        [Required]
        [StringLength(100)]
        public string Username { get; set; } = "";

        [Required]
        [StringLength(150)]
        public string FullName { get; set; } = "";

        [Required]
        [EmailAddress]
        public string Email { get; set; } = "";

        [Required]
        public int RoleId { get; set; }
    }
}
