using System;
using System.Threading.Tasks;
using Microsoft.Azure.Services.AppAuthentication;
using Microsoft.Extensions.Logging;
using Azure.Identity;
using Azure.Core;
using System.Threading;

namespace azman_v2.Auth
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

    public class AzureIdentityTokenProvider : ITokenProvider
    {
        private readonly TokenCredential _cred;
        private readonly ILogger<AzureIdentityTokenProvider> _log;

        public AzureIdentityTokenProvider(ILoggerFactory loggerFactory) : this(loggerFactory, new ManagedIdentityCredential()) { }

        public AzureIdentityTokenProvider(ILoggerFactory loggerFactory, TokenCredential cred)
        {
            _log = loggerFactory.CreateLogger<AzureIdentityTokenProvider>();
            _cred = cred;
        }

        public AccessTokenResponse GetAccessToken(string[] scopes, bool forceRefresh = false)
        {
            _log.LogInformation($"Getting token via {_cred.GetType()} for {string.Join(',', scopes)}");
            var b = _cred.GetToken(new Azure.Core.TokenRequestContext(scopes), CancellationToken.None);
            var resource = ScopeUtil.GetResourceFromScope(scopes);
            return new AccessTokenResponse(resource, b.Token, b.ExpiresOn);
        }
        public async Task<AccessTokenResponse> GetAccessTokenAsync(string[] scopes, bool forceRefresh = false)
        {
            _log.LogInformation($"Getting token via {_cred.GetType()} for {string.Join(',', scopes)}");
            var b = await _cred.GetTokenAsync(new Azure.Core.TokenRequestContext(scopes), CancellationToken.None);
            var resource = ScopeUtil.GetResourceFromScope(scopes);
            return new AccessTokenResponse(resource, b.Token, b.ExpiresOn);
        }
    }
}