using System.Security.Cryptography;
using System.Text;
using CbeMeetingAttendance.DTOs;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace CbeMeetingAttendance.Services
{
    public interface IEncryptionService
    {
        /// <summary>Returns the RSA public key in SPKI PEM format (safe to send to clients).</summary>
        string GetPublicKeyPem();

        /// <summary>
        /// Decrypts an <see cref="EncryptedRequestEnvelope"/>: unwraps the AES key with the
        /// backend's RSA private key, then decrypts the AES-GCM ciphertext to recover the
        /// original JSON payload. Throws <see cref="InvalidEncryptedRequestException"/> on
        /// any tampering or malformed input.
        /// </summary>
        string DecryptEnvelope(EncryptedRequestEnvelope? envelope);
    }

    /// <summary>Thrown when an encrypted request cannot be safely decrypted.</summary>
    public class InvalidEncryptedRequestException : Exception
    {
        public InvalidEncryptedRequestException(string message) : base(message) { }

        public InvalidEncryptedRequestException(string message, Exception innerException)
            : base(message, innerException) { }
    }

    /// <summary>
    /// Manages the backend RSA key pair used for application-level payload encryption.
    /// The private key is generated on first startup, persisted only on the backend
    /// filesystem, and is NEVER exposed through any API, log, or response.
    /// </summary>
    public class EncryptionService : IEncryptionService
    {
        private const string PrivateKeyFileName = "rsa_private_key.pem";
        private const string PublicKeyFileName = "rsa_public_key.pem";

        private readonly RSA _rsa;
        private readonly string _publicKeyPem;

        public EncryptionService(IConfiguration configuration, IHostEnvironment environment)
        {
            var keySize = configuration.GetValue("Encryption:KeySize", 2048);
            if (keySize < 2048)
            {
                keySize = 2048;
            }

            var storagePath = configuration["Encryption:KeyStoragePath"] ?? "App_Data/Keys";
            var keysDirectory = Path.IsPathRooted(storagePath)
                ? storagePath
                : Path.Combine(environment.ContentRootPath, storagePath);

            Directory.CreateDirectory(keysDirectory);

            var privateKeyPath = Path.Combine(keysDirectory, PrivateKeyFileName);
            var publicKeyPath = Path.Combine(keysDirectory, PublicKeyFileName);

            _rsa = RSA.Create(keySize);

            if (File.Exists(privateKeyPath) && File.Exists(publicKeyPath))
            {
                // Load the existing key pair so the public key stays stable across restarts
                _rsa.ImportFromPem(File.ReadAllText(privateKeyPath));
                _publicKeyPem = File.ReadAllText(publicKeyPath);

                // Ensure restrictive permissions even for pre-existing key files
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(privateKeyPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }
            }
            else
            {
                // Generate a fresh key pair and persist it on the backend only
                var privateKeyPem = _rsa.ExportPkcs8PrivateKeyPem();
                var publicKeyPem = _rsa.ExportSubjectPublicKeyInfoPem();

                File.WriteAllText(privateKeyPath, privateKeyPem);
                File.WriteAllText(publicKeyPath, publicKeyPem);

                // Restrict private-key file permissions (owner read/write only) on Unix
                if (!OperatingSystem.IsWindows())
                {
                    File.SetUnixFileMode(privateKeyPath,
                        UnixFileMode.UserRead | UnixFileMode.UserWrite);
                }

                _publicKeyPem = publicKeyPem;
            }
        }

        public string GetPublicKeyPem() => _publicKeyPem;

        public string DecryptEnvelope(EncryptedRequestEnvelope? envelope)
        {
            if (envelope is null ||
                string.IsNullOrWhiteSpace(envelope.EncryptedKey) ||
                string.IsNullOrWhiteSpace(envelope.Nonce) ||
                string.IsNullOrWhiteSpace(envelope.Ciphertext) ||
                string.IsNullOrWhiteSpace(envelope.Tag))
            {
                throw new InvalidEncryptedRequestException("Encrypted request is missing required fields.");
            }

            byte[] aesKey;
            byte[] nonce;
            byte[] ciphertext;
            byte[] tag;

            try
            {
                // 1) Recover the AES key by decrypting the wrapped key with the RSA private key
                aesKey = _rsa.Decrypt(
                    Convert.FromBase64String(envelope.EncryptedKey),
                    RSAEncryptionPadding.OaepSHA256);

                // 2) Decode the AES-GCM parameters (nonce, ciphertext, auth tag). Decoding
                //    failures are treated like any other tampering.
                nonce = Convert.FromBase64String(envelope.Nonce);
                ciphertext = Convert.FromBase64String(envelope.Ciphertext);
                tag = Convert.FromBase64String(envelope.Tag);
            }
            catch (Exception ex)
            {
                throw new InvalidEncryptedRequestException("Could not unwrap the encrypted session key.", ex);
            }

            // AES-GCM requires a 12-byte nonce; reject wrong sizes explicitly
            if (nonce.Length != 12)
            {
                throw new InvalidEncryptedRequestException("The encrypted request has an invalid nonce.");
            }

            // 3) AES-GCM decrypt the payload (tag authentication rejects tampering)
            var plaintextBytes = new byte[ciphertext.Length];
            try
            {
                using var aes = new AesGcm(aesKey, 16);
                aes.Decrypt(nonce, ciphertext, tag, plaintextBytes);
            }
            catch (Exception ex)
            {
                throw new InvalidEncryptedRequestException("The encrypted payload failed authentication.", ex);
            }

            var plaintext = Encoding.UTF8.GetString(plaintextBytes);
            if (string.IsNullOrWhiteSpace(plaintext))
            {
                throw new InvalidEncryptedRequestException("The decrypted payload is empty.");
            }

            return plaintext;
        }
    }
}
