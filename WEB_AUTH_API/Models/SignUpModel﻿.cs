using System.ComponentModel.DataAnnotations;

namespace WEB_AUTH_API.Models
{
    public class SignUpModel﻿
    {
        public required string Username { get; set; }
        public required string Email { get; set; }
        public required string Password { get; set; }
       public required string ConfirmPassword { get; set; }
    }
}
