﻿namespace WEB_AUTH_API.Models
{
    public class InvalidateSessionRequest
    {
        public string RefreshToken { get; set; } = string.Empty;
        public string Token { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;

    }
}
