using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using ObsidianLauncher.Utils;
using Serilog;

namespace ObsidianLauncher.Services;

public class HttpManager : IDisposable
{
    private static readonly HttpClient httpClient;
    private readonly ILogger _logger;

    static HttpManager()
    {
        httpClient = new HttpClient();
        httpClient.Timeout = TimeSpan.FromSeconds(30);
        httpClient.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ObsidianLauncher/1.0 (+https://github.com/Advik-B/Obsidian-Launcher)");
    }

    public HttpManager()
    {
        _logger = LogHelper.GetLogger<HttpManager>();
        _logger.Verbose("HttpManager instance created.");
    }

    public void Dispose()
    {
        GC.SuppressFinalize(this);
    }

    public async Task<HttpResponseMessage> GetAsync(
        string url,
        HttpContent? content = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Verbose("HTTP GET: {Url}", url);

        try
        {
            var response = await httpClient.GetAsync(url, cancellationToken).ConfigureAwait(false);
            _logger.Verbose("GET Response: {Url}, Status: {StatusCode}, IsSuccess: {IsSuccessStatusCode}",
                url, response.StatusCode, response.IsSuccessStatusCode);
            return response;
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HTTP GET request failed for {Url}", url);
            return new HttpResponseMessage(HttpStatusCode.ServiceUnavailable)
            {
                ReasonPhrase = $"HttpRequestException: {ex.Message}",
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            };
        }
        catch (TaskCanceledException ex)
        {
            _logger.Warning(ex, "HTTP GET request cancelled or timed out for {Url}", url);
            return new HttpResponseMessage(HttpStatusCode.RequestTimeout)
            {
                ReasonPhrase = $"Request cancelled or timed out: {ex.Message}",
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            };
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during HTTP GET for {Url}", url);
            return new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                ReasonPhrase = $"Unexpected error: {ex.Message}",
                RequestMessage = new HttpRequestMessage(HttpMethod.Get, url)
            };
        }
    }

    public async Task<(HttpResponseMessage Response, string FilePath)> DownloadAsync(
        string url,
        string filePath,
        IProgress<float>? progress = null,
        CancellationToken cancellationToken = default)
    {
        _logger.Verbose("HTTP DOWNLOAD: {Url} -> {FilePath}", url, filePath);

        try
        {
            var directoryPath = Path.GetDirectoryName(filePath);
            if (!string.IsNullOrEmpty(directoryPath))
                Directory.CreateDirectory(directoryPath);

            using var response = await httpClient
                .GetAsync(url, HttpCompletionOption.ResponseHeadersRead, cancellationToken).ConfigureAwait(false);

            if (!response.IsSuccessStatusCode)
            {
                _logger.Error("Download HTTP request failed for {Url}. Status: {StatusCode}. Reason: {ReasonPhrase}",
                    url, response.StatusCode, response.ReasonPhrase);
                return (response, filePath);
            }

            var totalBytes = response.Content.Headers.ContentLength;
            _logger.Verbose("Download started. Total size: {TotalBytes} bytes for {Url}",
                totalBytes?.ToString() ?? "Unknown", url);

            using var contentStream = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
            using var fileStream =
                new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None, 8192, true);

            var buffer = new byte[8192];
            long totalBytesRead = 0;
            int bytesRead;

            while ((bytesRead = await contentStream.ReadAsync(buffer, 0, buffer.Length, cancellationToken)
                       .ConfigureAwait(false)) > 0)
            {
                await fileStream.WriteAsync(buffer, 0, bytesRead, cancellationToken).ConfigureAwait(false);
                totalBytesRead += bytesRead;

                if (progress != null && totalBytes.HasValue && totalBytes.Value > 0)
                    progress.Report((float)totalBytesRead / totalBytes.Value);
            }

            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);
            _logger.Verbose("Download complete: {FilePath}, Bytes read: {TotalBytesRead}", filePath, totalBytesRead);
            return (response, filePath);
        }
        catch (HttpRequestException ex)
        {
            _logger.Error(ex, "HttpRequestException during download for {Url} to {FilePath}", url, filePath);
            DeletePartialFile(filePath, "HttpRequestException");
            return (new HttpResponseMessage(HttpStatusCode.ServiceUnavailable) { ReasonPhrase = ex.Message }, filePath);
        }
        catch (TaskCanceledException ex)
        {
            _logger.Warning(ex, "Download cancelled or timed out for {Url} to {FilePath}", url, filePath);
            DeletePartialFile(filePath, "TaskCanceledException");
            return (new HttpResponseMessage(HttpStatusCode.RequestTimeout) { ReasonPhrase = ex.Message }, filePath);
        }
        catch (Exception ex)
        {
            _logger.Error(ex, "Unexpected error during download for {Url} to {FilePath}", url, filePath);
            DeletePartialFile(filePath, "Unexpected Exception");
            return (new HttpResponseMessage(HttpStatusCode.InternalServerError) { ReasonPhrase = ex.Message },
                filePath);
        }
    }

    private void DeletePartialFile(string filePath, string reason)
    {
        if (File.Exists(filePath))
            try
            {
                File.Delete(filePath);
                _logger.Warning("Deleted partially downloaded file due to {Reason}: {FilePath}", reason, filePath);
            }
            catch (Exception delEx)
            {
                _logger.Error(delEx, "Failed to delete partially downloaded file: {FilePath}", filePath);
            }
    }
}