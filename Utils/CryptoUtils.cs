// Utils/CryptoUtils.cs
using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ObsidianLauncher.Utils
{
    public static class CryptoUtils
    {
        private static readonly ILogger _logger = Log.ForContext(typeof(CryptoUtils));

        /// <summary>
        /// Calculates the SHA1 hash of a given file asynchronously.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A hex-encoded string of the SHA1 hash. Returns null on error (e.g., file not found, crypto error).</returns>
        public static async Task<string> CalculateFileSHA1Async(string filePath, CancellationToken cancellationToken = default)
        {
            _logger.Verbose("Calculating SHA1 for file: {FilePath}", filePath);
            if (!File.Exists(filePath))
            {
                _logger.Error("File not found for SHA1 calculation: {FilePath}", filePath);
                return null;
            }

            try
            {
                using var sha1 = SHA1.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true); // true for async
                
                byte[] hash = await sha1.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
                
                // Convert byte array to hex string
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                string hexHash = sb.ToString();
                _logger.Verbose("SHA1 for {FilePath}: {Hash}", filePath, hexHash);
                return hexHash;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("SHA1 calculation cancelled for file: {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating SHA1 for file: {FilePath}", filePath);
                return null;
            }
        }

        /// <summary>
        /// Calculates the SHA256 hash of a given file asynchronously.
        /// </summary>
        /// <param name="filePath">The path to the file.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A hex-encoded string of the SHA256 hash. Returns null on error (e.g., file not found, crypto error).</returns>
        public static async Task<string> CalculateFileSHA256Async(string filePath, CancellationToken cancellationToken = default)
        {
            _logger.Verbose("Calculating SHA256 for file: {FilePath}", filePath);
            if (!File.Exists(filePath))
            {
                _logger.Error("File not found for SHA256 calculation: {FilePath}", filePath);
                return null;
            }

            try
            {
                using var sha256 = SHA256.Create();
                using var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read, 4096, true); // true for async
                
                byte[] hash = await sha256.ComputeHashAsync(stream, cancellationToken).ConfigureAwait(false);
                
                // Convert byte array to hex string
                var sb = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2"));
                }
                string hexHash = sb.ToString();
                _logger.Verbose("SHA256 for {FilePath}: {Hash}", filePath, hexHash);
                return hexHash;
            }
            catch (OperationCanceledException)
            {
                _logger.Warning("SHA256 calculation cancelled for file: {FilePath}", filePath);
                return null;
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error calculating SHA256 for file: {FilePath}", filePath);
                return null;
            }
        }
    }
}