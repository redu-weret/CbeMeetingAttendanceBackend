using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace CbeMeetingAttendance.Controllers
{
    [ApiController]
    [Route("api/security")]
    public class SecurityController : ControllerBase
    {
        private readonly IEncryptionService _encryptionService;

        public SecurityController(IEncryptionService encryptionService)
        {
            _encryptionService = encryptionService ?? throw new ArgumentNullException(nameof(encryptionService));
        }

        /// <summary>
        /// GET /api/security/public-key — Public endpoint that returns ONLY the RSA public key
        /// used by clients to wrap the per-request AES key. The private key never leaves the backend.
        /// </summary>
        [AllowAnonymous]
        [HttpGet("public-key")]
        public ActionResult<ApiResponse<string>> GetPublicKey()
        {
            var publicKey = _encryptionService.GetPublicKeyPem();
            return Ok(ApiResponse<string>.SuccessResult(publicKey, "RSA public key retrieved"));
        }
    }
}
