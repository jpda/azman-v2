using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace azman_v2.Auth
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
}