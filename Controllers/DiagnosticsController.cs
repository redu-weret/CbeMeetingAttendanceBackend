using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;

namespace CbeMeetingAttendance.Controllers
{
    [ApiController]
    [Authorize(Roles = "Admin")]
    [Route("api/diagnostics")]
    public class DiagnosticsController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public DiagnosticsController(IConfiguration configuration)
        {
            _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        }

        public class ValidateTokenDto
        {
            public string? Token { get; set; }
        }

        [HttpPost("validate-token")]
        public ActionResult ValidateToken([FromBody] ValidateTokenDto dto)
        {
            if (dto == null || string.IsNullOrWhiteSpace(dto.Token))
            {
                return BadRequest(new { success = false, message = "Token is required" });
            }

            var tokenInput = dto.Token!.Trim();
            if (tokenInput.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                tokenInput = tokenInput.Substring(7).Trim();
            }

            var jwtSection = _configuration.GetSection("JwtSettings");
            var keyValue = jwtSection["Key"] ?? "cbe";
            var issuer = jwtSection["Issuer"] ?? "CbeMeetingAttendance";
            var audiences = jwtSection.GetSection("Audiences").Get<string[]>()
                            ?? new[] { "CBE-Attendance-Web-App" };

            var keyBytes = SHA256.HashData(Encoding.UTF8.GetBytes(keyValue));
            var signingKey = new SymmetricSecurityKey(keyBytes);

            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = true,
                ValidateIssuerSigningKey = true,
                ValidIssuer = issuer,
                ValidAudiences = audiences,
                IssuerSigningKey = signingKey,
                ClockSkew = TimeSpan.Zero
            };

            try
            {
                var principal = tokenHandler.ValidateToken(tokenInput, validationParameters, out var validatedToken);
                var claims = principal.Claims.Select(c => new { c.Type, c.Value });
                return Ok(new { success = true, message = "Token valid.", claims });
            }
            catch (SecurityTokenExpiredException ex)
            {
                return Unauthorized(new { success = false, message = "Token expired.", detail = ex.Message });
            }
            catch (Exception ex)
            {
                return Unauthorized(new { success = false, message = "Token invalid.", detail = ex.Message });
            }
        }
    }
}
