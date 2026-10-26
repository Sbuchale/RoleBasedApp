using System.ComponentModel.DataAnnotations;

namespace RoleBasedApp.DTOs
{
    public class LoginRequest
    {
        [Required]
        public string Username { get; set; } = "";

        [Required]
        public string Password { get; set; } = "";
    }
}
