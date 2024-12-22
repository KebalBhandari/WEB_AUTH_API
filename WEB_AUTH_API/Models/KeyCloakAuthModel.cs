namespace WEB_AUTH_API.Models
{
    public class KeyCloakAuthModel
    {
        public string Username { get; set; }
        public string Email { get; set; }
        public string UserId { get; set; }
        public string TokenNo { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }
}
