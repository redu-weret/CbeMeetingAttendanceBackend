using System.Text;
using System.Text.Json;
using CbeMeetingAttendance.DTOs;
using CbeMeetingAttendance.Services;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace CbeMeetingAttendance.Middleware
{
    /// <summary>
    /// Decrypts application-level encrypted request bodies (hybrid AES-GCM + RSA-OAEP)
    /// before the MVC model binder runs, so every existing controller keeps working
    /// without changes. Only requests explicitly marked with
    /// <c>X-Encrypted-Payload: true</c> are processed; everything else (Swagger,
    /// direct API calls, GET requests, multipart uploads) passes through untouched.
    /// Backend responses are never encrypted.
    /// </summary>
    public class EncryptedRequestBodyMiddleware
    {
        public const string MarkerHeader = "X-Encrypted-Payload";

        /// <summary>
        /// Upper bound for encrypted request bodies. Envelopes are small JSON (the
        /// largest real payloads are a few KB), so this is generous while still
        /// capping memory use for oversized/abusive requests. Multipart uploads
        /// (FormData) are never marked and never pass through this limit.
        /// </summary>
        private const long MaxEncryptedBodyBytes = 2 * 1024 * 1024; // 2 MB

        private readonly RequestDelegate _next;
        private readonly IEncryptionService _encryptionService;
        private readonly ILogger<EncryptedRequestBodyMiddleware> _logger;

        public EncryptedRequestBodyMiddleware(
            RequestDelegate next,
            IEncryptionService encryptionService,
            ILogger<EncryptedRequestBodyMiddleware> logger)
        {
            _next = next;
            _encryptionService = encryptionService;
            _logger = logger;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            var request = context.Request;

            var isMarked = request.Headers.TryGetValue(MarkerHeader, out var marker)
                           && !string.IsNullOrWhiteSpace(marker)
                           && marker.ToString().Trim().Equals("true", StringComparison.OrdinalIgnoreCase);

            if (isMarked)
            {
                if (request.ContentLength > MaxEncryptedBodyBytes)
                {
                    _logger.LogWarning("Rejected oversized encrypted request body: {Size} bytes", request.ContentLength);
                    await WriteErrorAsync(context, "The encrypted request body is too large.");
                    return;
                }

                string bodyJson;
                try
                {
                    using var reader = new StreamReader(request.Body, Encoding.UTF8);
                    bodyJson = await reader.ReadToEndAsync();
                }
                catch (Exception ex)
                {
                    // Never leak payload-derived details
                    _logger.LogWarning("Failed to read encrypted request body: {ErrorType}", ex.GetType().Name);
                    await WriteErrorAsync(context, "Encrypted request body could not be read.");
                    return;
                }

                if (Encoding.UTF8.GetByteCount(bodyJson) > MaxEncryptedBodyBytes)
                {
                    _logger.LogWarning("Rejected oversized encrypted request body.");
                    await WriteErrorAsync(context, "The encrypted request body is too large.");
                    return;
                }

                try
                {
                    var envelope = JsonSerializer.Deserialize<EncryptedRequestEnvelope>(
                        bodyJson,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

                    var plaintext = _encryptionService.DecryptEnvelope(envelope);

                    // Replace the request body with the decrypted JSON so model binding
                    // and every controller behave exactly as if the plaintext was sent.
                    var plaintextBytes = Encoding.UTF8.GetBytes(plaintext);
                    request.Body = new MemoryStream(plaintextBytes);
                    request.ContentLength = plaintextBytes.Length;
                    request.ContentType = "application/json; charset=utf-8";
                }
                catch (InvalidEncryptedRequestException ex)
                {
                    _logger.LogWarning("Rejected invalid encrypted request: {Message}", ex.Message);
                    await WriteErrorAsync(context, ex.Message);
                    return;
                }
                catch (Exception ex)
                {
                    // Never leak cryptographic details, key material or payload content
                    _logger.LogWarning("Encrypted request processing failed: {ErrorType}", ex.GetType().Name);
                    await WriteErrorAsync(context, "The encrypted request is invalid or malformed.");
                    return;
                }
            }

            await _next(context);
        }

        private static async Task WriteErrorAsync(HttpContext context, string message)
        {
            context.Response.StatusCode = StatusCodes.Status400BadRequest;
            context.Response.ContentType = "application/json; charset=utf-8";

            var response = ApiResponse<object>.FailureResult(message);
            await context.Response.WriteAsync(
                JsonSerializer.Serialize(response, new JsonSerializerOptions { PropertyNamingPolicy = JsonNamingPolicy.CamelCase }));
        }
    }
}
