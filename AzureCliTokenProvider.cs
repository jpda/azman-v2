using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Azure.Core;
using Microsoft.Extensions.Logging;

namespace azman_v2
{
    public class AzureCliTokenProvider : ITokenProvider
    {
        private readonly ConcurrentDictionary<string, AccessTokenResponse> _tokens;
        private readonly ILogger<AzureCliTokenProvider> _log;

        public AzureCliTokenProvider(ILoggerFactory loggerFactory)
        {
            _log = loggerFactory.CreateLogger<AzureCliTokenProvider>();
            _tokens = new ConcurrentDictionary<string, AccessTokenResponse>();
        }

        private AccessTokenResponse GetToken(string resource)
        {
            var proc = new Process();
            proc.StartInfo.FileName = @"C:\Program Files (x86)\Microsoft SDKs\Azure\CLI2\wbin\az.cmd ";
            proc.StartInfo.Arguments = $"account get-access-token --resource \"{resource}\"";
            proc.StartInfo.RedirectStandardOutput = true;
            proc.Start();
            string output = proc.StandardOutput.ReadToEnd();
            _log.LogTrace($"Got a token for {resource}, refreshing...");
            var doc = System.Text.Json.JsonDocument.Parse(output);
            var token = doc.RootElement.GetProperty("accessToken").GetString();
            var expiry = DateTimeOffset.ParseExact(doc.RootElement.GetProperty("expiresOn").GetString(), "yyyy-MM-dd HH:mm:ss.ffffff", CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal);
            proc.WaitForExit();
            proc.Close();
            return new AccessTokenResponse(resource, token, expiry);
        }

        public AccessTokenResponse GetAccessToken(string[] scopes, bool forceRefresh = false)
        {
            var resource = ScopeUtil.GetResourceFromScope(scopes);
            _log.LogTrace($"Fetching access token via CLI for {resource} ({string.Join(',', scopes)}); forcedRefresh: {forceRefresh}");
            // todo: make sure i'm not missing something here
            if (!_tokens.ContainsKey(resource) || forceRefresh)
            {
                _tokens.AddOrUpdate(resource, x => GetToken(resource), (y, z) => GetToken(resource));
            }
            return _tokens.GetOrAdd(resource, x => GetToken(resource));
        }

        public Task<AccessTokenResponse> GetAccessTokenAsync(string[] scopes, bool forceRefresh = false)
        {
            return Task.FromResult(GetAccessToken(scopes));
        }
    }

    public class ExternalAzureTokenCredential : TokenCredential
    {
        private readonly ITokenProvider _tokenProvider;
        public ExternalAzureTokenCredential(ITokenProvider provider)
        {
            _tokenProvider = provider;
        }

        public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var accessToken = _tokenProvider.GetAccessToken(requestContext.Scopes);
            return new Azure.Core.AccessToken(accessToken.Token, accessToken.Expiry);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(requestContext.Scopes);
            return new Azure.Core.AccessToken(accessToken.Token, accessToken.Expiry);
        }
    }

    public class AccessTokenResponse
    {
        public string Resource { get; set; }
        public string Token { get; set; }
        public DateTimeOffset Expiry { get; set; }

        public AccessTokenResponse(string resource, string token, DateTimeOffset expiry)
        {
            this.Resource = resource;
            this.Token = token;
            this.Expiry = expiry;
        }
    }

    // see: https://github.com/Azure/azure-sdk-for-net/blob/master/sdk/identity/Azure.Identity/src/ScopeUtilities.cs
    public static class ScopeUtil
    {
        public static string GetResourceFromScope(string[] scopes)
        {
            var defaultSuffix = "/.default";

            if (!scopes.Any())
            {
                throw new ArgumentNullException(nameof(scopes));
            }

            if (scopes.Length > 1)
            {
                throw new ArgumentException(nameof(scopes));
            }

            var scope = scopes[0];

            if (!scope.EndsWith(defaultSuffix, StringComparison.Ordinal))
            {
                return scope;
            }
            return scope.Remove(scope.LastIndexOf(defaultSuffix, StringComparison.Ordinal));
        }
    }
}