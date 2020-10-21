using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;

namespace azman_v2
{
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
}