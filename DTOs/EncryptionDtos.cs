namespace CbeMeetingAttendance.DTOs
{
    /// <summary>
    /// Envelope for application-level encrypted request payloads.
    /// Structure: RSA-OAEP-wrapped AES key + AES-GCM nonce, ciphertext and auth tag.
    /// The backend unwraps the AES key with its RSA private key (never sent to clients),
    /// then decrypts the ciphertext to recover the original JSON payload.
    /// </summary>
    public class EncryptedRequestEnvelope
    {
        /// <summary>The AES key encrypted with the backend's RSA public key (base64).</summary>
        public string? EncryptedKey { get; set; }

        /// <summary>AES-GCM nonce/IV (base64, 12 bytes).</summary>
        public string? Nonce { get; set; }

        /// <summary>AES-GCM ciphertext of the JSON payload (base64).</summary>
        public string? Ciphertext { get; set; }

        /// <summary>AES-GCM authentication tag (base64, 16 bytes).</summary>
        public string? Tag { get; set; }
    }
}
