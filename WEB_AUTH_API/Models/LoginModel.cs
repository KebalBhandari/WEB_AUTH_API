using System.Reflection.PortableExecutable;

namespace WEB_AUTH_API.Models
{
    public class LoginModel
    {
        public required string Email { get; set; }
        public required string Password { get; set; }
        public required string IpAddress { get; set; }
        public required string UserAgent { get; set; }
    }
}
