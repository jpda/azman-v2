using System.Threading.Tasks;

namespace azman_v2.Auth
{
    public interface ITokenProvider
    {
        AccessTokenResponse GetAccessToken(string[] scopes, bool forceRefresh = false);
        Task<AccessTokenResponse> GetAccessTokenAsync(string[] scopes, bool forceRefresh = false);
    }
}