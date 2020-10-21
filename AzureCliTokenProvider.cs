using System.Diagnostics;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace azman_v2
{
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
                proc.StartInfo.Arguments = $"account get-access-token --resource \"{resource}\"";
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