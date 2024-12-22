using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Data.SqlClient;
using Newtonsoft.Json.Linq;
using System.Data;
using WEB_AUTH_API.DataAccess;
using WEB_AUTH_API.Models;

namespace WEB_AUTH_API.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly DataHandeler _dataHandler;
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _dataHandler = new DataHandeler();
            _configuration = configuration;
        }

        [HttpPost("Login")]
        public async Task<IActionResult> Login([FromBody] LoginModel request)
        {
            try
            {
                var parameters = new SqlParameter[]
                {
            new SqlParameter("@UserEmail", request.Email),
            new SqlParameter("@Password", request.Password),
            new SqlParameter("@IP_Address", request.IpAddress),
            new SqlParameter("@User_Agent", request.UserAgent)
                };

                DataTable dt = await Task.Run(() => _dataHandler.ReadData("sp_ValidateLoginAndUpdateSession", parameters, CommandType.StoredProcedure));

                if (dt.Rows.Count > 0)
                {
                    if (dt.Rows[0]["RefreshToken"].ToString() == null || dt.Rows[0]["RefreshToken"].ToString() == "")
                    {
                        return BadRequest(new { Status = "ERROR", Message = "Login Failed, Try Again", jWTToken = "Null" });
                    }
                    else
                    {
                        var jwtSettings = _configuration.GetSection("Jwt");
                        var token = JwtTokenHelper.GenerateJwtTokenWithRefresh(
                            key: jwtSettings["Key"],
                            issuer: jwtSettings["Issuer"],
                            audience: jwtSettings["Audience"],
                            expirationMinutes: int.Parse(jwtSettings["TokenExpiryInMinutes"]),
                            subject: dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null,
                            roles: ["User"]
                        );

                        RefreshToken refreshToken = new RefreshToken();
                        refreshToken.Username = dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null;
                        refreshToken.Token = token.RefreshToken;
                        refreshToken.CreatedByUser = request.Email;
                        refreshToken.Expiration = DateTime.UtcNow.AddDays(int.Parse(jwtSettings["RefreshTokenExpiryInDay"]));

                        InsertRefreshToken(refreshToken);

                        return Ok(new
                        {
                            Status = dt.Rows[0]["Status"].ToString(),
                            Message = dt.Rows[0]["Message"].ToString(),
                            RefreshToken = dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null,
                            JWTRefreshToken = token.RefreshToken,
                            JWTToken = token.AccessToken
                        });
                    }
                }
                else
                {

                    return BadRequest(new { Status = "ERROR", Message = "No response from stored procedure", jWTToken = "Null" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }


        [HttpPost("LoginOAuth")]
        public async Task<IActionResult> LoginOAuth([FromBody] KeyCloakAuthModel request)
        {
            try
            {
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@UserEmail", request.Email),
                    new SqlParameter("@Username", request.Username),
                    new SqlParameter("@IP_Address", request.IpAddress),
                    new SqlParameter("@UserIdd", request.UserId),
                    new SqlParameter("@TokenNo", request.TokenNo),
                    new SqlParameter("@User_Agent", request.UserAgent)
                };

                DataTable dt = await Task.Run(() => _dataHandler.ReadData("sp_ValidateLoginAndUpdateSessionOAuth", parameters, CommandType.StoredProcedure));

                if (dt.Rows.Count > 0)
                {
                    if (dt.Rows[0]["RefreshToken"].ToString() == null || dt.Rows[0]["RefreshToken"].ToString() == "")
                    {
                        return BadRequest(new { Status = "ERROR", Message = "Login Failed, Try Again", jWTToken = "Null" });
                    }
                    else
                    {
                        var jwtSettings = _configuration.GetSection("Jwt");
                        var token = JwtTokenHelper.GenerateJwtTokenWithRefresh(
                            key: jwtSettings["Key"],
                            issuer: jwtSettings["Issuer"],
                            audience: jwtSettings["Audience"],
                            expirationMinutes: int.Parse(jwtSettings["TokenExpiryInMinutes"]),
                            subject: dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null,
                            roles: ["User"]
                        );

                        RefreshToken refreshToken = new RefreshToken();
                        refreshToken.Username = dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null;
                        refreshToken.Token = token.RefreshToken;
                        refreshToken.CreatedByUser = request.Email;
                        refreshToken.Expiration = DateTime.UtcNow.AddDays(int.Parse(jwtSettings["RefreshTokenExpiryInDay"]));

                        InsertRefreshToken(refreshToken);

                        return Ok(new
                        {
                            Status = dt.Rows[0]["Status"].ToString(),
                            Message = dt.Rows[0]["Message"].ToString(),
                            RefreshToken = dt.Columns.Contains("RefreshToken") ? dt.Rows[0]["RefreshToken"].ToString() : null,
                            JWTRefreshToken = token.RefreshToken,
                            JWTToken = token.AccessToken
                        });
                    }
                }
                else
                {

                    return BadRequest(new { Status = "ERROR", Message = "No response from stored procedure", jWTToken = "Null" });
                }
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }


        [HttpPost("Register")]
        public IActionResult Register([FromBody] SignUpModel﻿ request)
        {
            try
            {
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@Email", request.Email),
                    new SqlParameter("@Password", request.Password),
                    new SqlParameter("@Username", request.Username),
                    new SqlParameter("@RoleName", "Guest"),
                    new SqlParameter("@CreatedByUserId", 0)
                };

                DataTable dt = _dataHandler.ReadData("sp_SelfCreatedUser", parameters, CommandType.StoredProcedure);

                if (dt.Rows.Count > 0)
                {
                    return Ok(new
                    {
                        Status = dt.Rows[0]["Status"].ToString(),
                        Message = dt.Rows[0]["Message"].ToString()
                    });
                }

                return BadRequest(new { Status = "ERROR", Message = "No response from stored procedure" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }

        [HttpPost("InvalidateSession")]
        public IActionResult InvalidateSession([FromBody] InvalidateSessionRequest request)
        {
            try
            {
                var parameters = new SqlParameter[]
                {
                    new SqlParameter("@PlainRefreshToken", request.RefreshToken)
                };

                DataTable dt = _dataHandler.ReadData("sp_InvalidateSession", parameters, CommandType.StoredProcedure);

                if (dt.Rows.Count > 0 && dt.Rows[0]["Status"].ToString() == "SUCCESS")
                {
                    DeactivateRefreshToken(request.RefreshToken, request.Token, request.Email);
                    return Ok(new { Status = "SUCCESS", Message = "Session invalidated successfully" });
                }

                return BadRequest(new { Status = "ERROR", Message = "Failed to invalidate session" });
            }
            catch (Exception ex)
            {
                return StatusCode(500, new { Status = "ERROR", Message = ex.Message });
            }
        }


        [HttpPost("RefreshToken")]
        public IActionResult RefreshToken([FromBody] RefreshToken request)
        {
            var isValid = ValidateRefreshToken(request.Token, request.Username);

            if (!isValid)
            {
                return Unauthorized(new { Status = "ERROR", Message = "Invalid or expired refresh token" });
            }

            var jwtSettings = _configuration.GetSection("Jwt");
            var token = JwtTokenHelper.GenerateJwtTokenWithRefresh(
                key: jwtSettings["Key"],
                issuer: jwtSettings["Issuer"],
                audience: jwtSettings["Audience"],
                expirationMinutes: int.Parse(jwtSettings["TokenExpiryInMinutes"]),
                subject: request.Username,
                roles: ["User"]
            );

            // Update the refresh token
            UpdateRefreshToken(request.Username, token.RefreshToken, DateTime.UtcNow.AddMinutes(30), "System");

            return Ok(new
            {
                Status = "SUCCESS",
                JWTToken = token.AccessToken,
                JWTRefreshToken = token.RefreshToken
            });
        }


        public int InsertRefreshToken(RefreshToken refreshToken)
        {
            var sql = "InsertRefreshToken"; // Name of your stored procedure
            SqlParameter[] parameters =
            {
        new SqlParameter("@Username", refreshToken.Username),
        new SqlParameter("@Token", refreshToken.Token),
        new SqlParameter("@Expiration", refreshToken.Expiration),
        new SqlParameter("@CreatedByUser", refreshToken.CreatedByUser)
    };

            var dataHandler = new DataHandeler();
            return dataHandler.Insert(sql, parameters, CommandType.StoredProcedure);
        }


        public int UpdateRefreshToken(string username, string newToken, DateTime newExpiration, string updatedByUser)
        {
            var sql = "UpdateRefreshToken"; // Name of your stored procedure
            SqlParameter[] parameters =
            {
        new SqlParameter("@Username", username),
        new SqlParameter("@NewToken", newToken),
        new SqlParameter("@NewExpiration", newExpiration),
        new SqlParameter("@UpdatedByUser", updatedByUser)
    };

            var dataHandler = new DataHandeler();
            return dataHandler.Update(sql, parameters, CommandType.StoredProcedure);
        }

        public RefreshToken FetchRefreshToken(string username, string token = null)
        {
            var sql = token == null ? "FetchLatestRefreshTokenForUser" : "FetchRefreshToken";
            SqlParameter[] parameters = token == null
                ? new SqlParameter[] { new SqlParameter("@Username", username) }
                : new SqlParameter[]
                {
            new SqlParameter("@Username", username),
            new SqlParameter("@Token", token)
                };

            var dataTable = _dataHandler.ReadData(sql, parameters, CommandType.StoredProcedure);

            if (dataTable.Rows.Count == 0)
                return null;

            var row = dataTable.Rows[0];
            return new RefreshToken
            {
                Id = Convert.ToInt32(row["Id"]),
                Username = row["Username"].ToString(),
                Token = row["Token"].ToString(),
                Expiration = Convert.ToDateTime(row["Expiration"]),
                IsActive = Convert.ToBoolean(row["IsActive"]),
                CreatedByUser = row["CreatedByUser"].ToString()
            };
        }


        public int DeactivateRefreshToken(string username, string token, string updatedByUser)
        {
            var sql = "DeactivateRefreshToken"; // Name of your stored procedure
            SqlParameter[] parameters =
            {
        new SqlParameter("@Username", username),
        new SqlParameter("@Token", token),
        new SqlParameter("@UpdatedByUser", updatedByUser)
    };

            var dataHandler = new DataHandeler();
            return dataHandler.ExecuteNonQuery(sql, parameters, CommandType.StoredProcedure);
        }

        public int CleanUpExpiredTokens()
        {
            var sql = "CleanUpExpiredTokens"; // Name of your stored procedure
            var dataHandler = new DataHandeler();
            return dataHandler.ExecuteNonQuery(sql, null, CommandType.StoredProcedure);
        }


        public bool ValidateRefreshToken(string refreshToken, string username)
        {
            // Fetch the stored refresh token from the database
            var storedToken = FetchRefreshToken(username, null);

            // Validate if the token exists, matches the provided token, and is not expired
            return storedToken != null
                && storedToken.Token == refreshToken
                && storedToken.Expiration > DateTime.UtcNow
                && storedToken.IsActive;
        }
    }
}
