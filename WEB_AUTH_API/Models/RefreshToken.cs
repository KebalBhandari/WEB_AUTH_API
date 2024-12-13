namespace WEB_AUTH_API.Models
{
    public class RefreshToken
    {
        public int Id { get; set; }
        public string Username { get; set; }
        public string Token { get; set; }
        public DateTime Expiration { get; set; }
        public string CreatedByUser { get; set; }
        public bool IsActive { get; set; }
    }
}
