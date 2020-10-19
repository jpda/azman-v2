using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;

namespace azman_v2
{
    public interface ITokenProvider
    {
        Task<string> GetAccessTokenAsync(string resource = "https://graph.microsoft.com", bool forceRefresh = false);
    }

    public class AzureManagedIdentityServiceTokenProvider : ITokenProvider
    {
        private readonly AzureServiceTokenProvider _tokenProvider;
        private readonly ILogger<AzureManagedIdentityServiceTokenProvider> _log;

        public AzureManagedIdentityServiceTokenProvider(AzureServiceTokenProvider provider, ILoggerFactory loggerFactory)
        {
            _tokenProvider = provider;
            _log = loggerFactory.CreateLogger<AzureManagedIdentityServiceTokenProvider>();
        }

        public async Task<string> GetAccessTokenAsync(string resource = "https://graph.microsoft.com", bool forceRefresh = false)
        {
            _log.LogTrace($"Fetching access token via MSI for {resource}; forcedRefresh: {forceRefresh}");
            return await _tokenProvider.GetAccessTokenAsync(resource, forceRefresh);
        }
    }

    public class AzureCliTokenProvider : ITokenProvider
    {
        private string? _token;
        private readonly ILogger<AzureCliTokenProvider> _log;

        public AzureCliTokenProvider(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<AzureCliTokenProvider>();
        }

        public Task<string> GetAccessTokenAsync(string resource = "https://graph.microsoft.com", bool forceRefresh = false)
        {
            _log.LogTrace($"Fetching access token via CLI for {resource}; forcedRefresh: {forceRefresh}");
            if (string.IsNullOrEmpty(_token) || forceRefresh)
            {
                var proc = new Process();
                proc.StartInfo.FileName = @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd ";
                proc.StartInfo.Arguments = $"account get-access-token --resource {resource}";
                proc.StartInfo.RedirectStandardOutput = true;
                proc.Start();
                string output = proc.StandardOutput.ReadToEnd();
                _log.LogTrace("Got a token, refreshing...");
                var doc = System.Text.Json.JsonDocument.Parse(output);
                var token = doc.RootElement.GetProperty("accessToken").GetString();
                proc.WaitForExit();
                proc.Close();
                _token = token;
            }
            return Task.FromResult(_token);
        }
    }
}