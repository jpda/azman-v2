using System.Threading;
using System.Threading.Tasks;
using Azure.Core;

namespace azman_v2.Auth
{
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
            return new AccessToken(accessToken.Token, accessToken.Expiry);
        }

        public override async ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken)
        {
            var accessToken = await _tokenProvider.GetAccessTokenAsync(requestContext.Scopes);
            return new AccessToken(accessToken.Token, accessToken.Expiry);
        }
    }
}