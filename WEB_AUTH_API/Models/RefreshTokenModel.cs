using System.Diagnostics.CodeAnalysis;

namespace WEB_AUTH_API.Models
{
    public class RefreshTokenModel
    {
        [NotNull]
        public string AccessToken { get; set; }

        [NotNull]
        public string RefreshToken { get; set; }
    }
}
