using System;
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

        public AccessTokenResponse GetAccessToken(string[] scopes, bool forceRefresh = false)
        {
            // todo: parse the token for expiration? dunno
            var resource = ScopeUtil.GetResourceFromScope(scopes);
            _log.LogTrace($"Fetching access token via MSI for {resource} ({string.Join(',', scopes)}); forcedRefresh: {forceRefresh}");
            return new AccessTokenResponse(resource, _tokenProvider.GetAccessTokenAsync(resource, forceRefresh).Result, DateTimeOffset.UtcNow.AddHours(1));
        }

        public async Task<AccessTokenResponse> GetAccessTokenAsync(string[] scopes, bool forceRefresh = false)
        {
            // todo: parse the token for expiration? dunno
            var resource = ScopeUtil.GetResourceFromScope(scopes);
            _log.LogTrace($"Fetching access token via MSI for {resource} ({string.Join(',', scopes)}); forcedRefresh: {forceRefresh}");
            return new AccessTokenResponse(resource, await _tokenProvider.GetAccessTokenAsync(resource, forceRefresh), DateTimeOffset.UtcNow.AddHours(1));
        }
    }
}