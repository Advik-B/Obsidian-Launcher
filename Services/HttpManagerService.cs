// Services/HttpManagerService.cs
using System;
using System.IO;
using System.Net.Http;
using System.Net.Http.Headers; // For User-Agent
using System.Threading;
using System.Threading.Tasks;
using Serilog;

namespace ObsidianLauncher.Services
{
    public class HttpManagerService : IDisposable
    {
        // HttpClient is designed to be instantiated once and reused throughout the life of an application.
        // Instantiating an HttpClient class for every request will exhaust the number of sockets available under heavy loads.
        private static readonly HttpClient httpClient;
        private readonly ILogger _logger;

        static HttpManagerService() // Static constructor to initialize HttpClient once
        {
            // If you need custom SSL handling (like ignoring errors, NOT recommended for production):
            // var handler = new HttpClientHandler
            // {
            //     ServerCertificateCustomValidationCallback = HttpClientHandler.DangerousAcceptAnyServerCertificateValidator
            // };
            // _httpClient = new HttpClient(handler);

            // For most cases, the default handler is fine and uses system CA certs.
            httpClient = new HttpClient();

            // Set a default timeout if desired (e.g., 30 seconds)
            httpClient.Timeout = TimeSpan.FromSeconds(30);

            // Set a default User-Agent
            httpClient.DefaultRequestHeaders.UserAgent.ParseAdd("ObsidianLauncher/1.0 (+https://github.com/Advik-B/Obsidian-Launcher)");
        }

        public HttpManagerService()
        {
            _logger = Log.ForContext<HttpManagerService>(); // Get a logger specific to this service
            _logger.Verbose("HttpManagerService instance created.");
        }

        /// <summary>
        /// Performs an HTTP GET request.
        /// </summary>
        /// <param name="url">The URL to request.</param>
        /// <param name="parameters">Optional query parameters (will be appended to the URL).</param>
        /// <param name="headers">Optional custom headers for the request.</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>The HttpResponseMessage from the server.</returns>
        public async Task<HttpResponseMessage> GetAsync(
            string url,
            HttpContent content = null, // C++ had Parameters and Header, here we generalize a bit
                                        // For GET, parameters are usually in URL; headers are separate.
                                        // HttpContent is more for POST/PUT but can be adapted.
            CancellationToken cancellationToken = default)
        {
            _logger.Verbose("HTTP GET: {Url}", url);

            // If CPR Parameters were used for GET, they'd typically be query strings.
            // The C++ version didn't show how it used cpr::Parameters in Get,
            // so this C# version assumes parameters are already in the 'url' string.
            // If they need to be built dynamically:
            // var uriBuilder = new UriBuilder(url);
            // var query = HttpUtility.ParseQueryString(uriBuilder.Query);
            // query["param1"] = "value1";
            // uriBuilder.Query = query.ToString();
            // url = uriBuilder.ToString();

            try
            {
                // HttpClient doesn't have a direct equivalent of CPR's Parameters for GET in the same way.
                // They are usually part of the URL. Headers can be set per request or on HttpClient.DefaultRequestHeaders.
                // For simplicity, this example assumes parameters are in the URL string and headers are not explicitly passed per GET call here
                // (they can be added to HttpRequestMessage if needed).

                // If `HttpContent` is provided for a GET (unusual, but possible if the server supports it),
                // you might construct HttpRequestMessage manually.
                // However, the C++ version's Get(url, parameters) likely meant URL query parameters.

                HttpResponseMessage response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);

                _logger.Verbose("GET Response: {Url}, Status: {StatusCode}, IsSuccess: {IsSuccessStatusCode}",
                    url, response.StatusCode, response.IsSuccessStatusCode);

                return response;
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HTTP GET request failed for {Url}", url);
                // Return a synthetic HttpResponseMessage for caller to check IsSuccessStatusCode
                return new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable)
                {
                    ReasonPhrase = $"HttpRequestException: {ex.Message}",
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, url) // Associate the original request
                };
            }
            catch (TaskCanceledException ex) // Handles both timeout and explicit cancellation
            {
                _logger.Warning(ex, "HTTP GET request cancelled or timed out for {Url}", url);
                return new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout)
                {
                    ReasonPhrase = $"Request cancelled or timed out: {ex.Message}",
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, url)
                };
            }
            catch (Exception ex) // Catch-all for other unexpected errors
            {
                _logger.Error(ex, "Unexpected error during HTTP GET for {Url}", url);
                return new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError)
                {
                    ReasonPhrase = $"Unexpected error: {ex.Message}",
                    RequestMessage = new HttpRequestMessage(HttpMethod.Get, url)
                };
            }
        }

        /// <summary>
        /// Downloads a file from the specified URL to the given file path.
        /// This method streams the content directly to the file.
        /// </summary>
        /// <param name="url">The URL to download from.</param>
        /// <param name="filePath">The local path where the file will be saved.</param>
        /// <param name="progress">Optional progress reporter (0.0 to 1.0).</param>
        /// <param name="cancellationToken">A token to cancel the operation.</param>
        /// <returns>A tuple containing the HttpResponseMessage and the final file path. The response might indicate failure.</returns>
        public async Task<(HttpResponseMessage Response, string FilePath)> DownloadAsync(
            string url,
            string filePath,
            IProgress<float> progress = null,
            CancellationToken cancellationToken = default)
        {
            _logger.Information("HTTP DOWNLOAD: {Url} -> {FilePath}", url, filePath);

            try
            {
                string directoryPath = Path.GetDirectoryName(filePath);
                if (!string.IsNullOrEmpty(directoryPath))
                {
                    Directory.CreateDirectory(directoryPath); // Ensure directory exists
                }

                // Use GetAsync with HttpCompletionOption.ResponseHeadersRead to avoid loading the whole content into memory first.
                using HttpResponseMessage response = await httpClient.GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

                if (!response.IsSuccessStatusCode)
                {
                    _logger.Error("Download HTTP request failed for {Url}. Status: {StatusCode}. Reason: {ReasonPhrase}",
                        url, response.StatusCode, response.ReasonPhrase);
                    // Optionally read error content if small: string errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                    return (response, filePath); // Return the failed response
                }

                long? totalBytes = response.Content.Headers.ContentLength;
                _logger.Verbose("Download started. Total size: {TotalBytes} bytes for {Url}", totalBytes?.ToString() ?? "Unknown", url);

                using Stream contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                using FileStream fileStream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true); // true for async

                byte[] buffer = new byte[8192]; // Standard buffer size
                long totalBytesRead = 0;
                int bytesRead;

                while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken).ConfigureAwait(false)) > 0)
                {
                    await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                    totalBytesRead += bytesRead;

                    if (progress != null && totalBytes.HasValue && totalBytes.Value > 0)
                    {
                        progress.Report((float)totalBytesRead / totalBytes.Value);
                    }
                }

                await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false); // Ensure all data is written
                _logger.Information("Download complete: {FilePath}, Bytes read: {TotalBytesRead}", filePath, totalBytesRead);
                return (response, filePath);
            }
            catch (HttpRequestException ex)
            {
                _logger.Error(ex, "HttpRequestException during download for {Url} to {FilePath}", url, filePath);
                DeletePartialFile(filePath, "HttpRequestException");
                return (new HttpResponseMessage(System.Net.HttpStatusCode.ServiceUnavailable) { ReasonPhrase = ex.Message }, filePath);
            }
            catch (TaskCanceledException ex) // Handles both timeout and explicit cancellation
            {
                _logger.Warning(ex, "Download cancelled or timed out for {Url} to {FilePath}", url, filePath);
                DeletePartialFile(filePath, "TaskCanceledException");
                return (new HttpResponseMessage(System.Net.HttpStatusCode.RequestTimeout) { ReasonPhrase = ex.Message }, filePath);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Unexpected error during download for {Url} to {FilePath}", url, filePath);
                DeletePartialFile(filePath, "Unexpected Exception");
                return (new HttpResponseMessage(System.Net.HttpStatusCode.InternalServerError) { ReasonPhrase = ex.Message }, filePath);
            }
        }

        private void DeletePartialFile(string filePath, string reasonForDeletion)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                    _logger.Warning("Deleted partially downloaded file due to {Reason}: {FilePath}", reasonForDeletion, filePath);
                }
                catch (Exception delEx)
                {
                    _logger.Error(delEx, "Failed to delete partially downloaded file: {FilePath}", filePath);
                }
            }
        }

        /// <summary>
        /// Disposes the HttpManagerService. Currently, this does nothing as HttpClient is static.
        /// </summary>
        public void Dispose()
        {
            // HttpClient is static and managed by the runtime for its lifetime.
            // If HttpClient were an instance field, you would dispose it here:
            // _httpClient?.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}