using System.Threading.Tasks;

namespace azman_v2
{
    public interface ITokenProvider
    {
        AccessTokenResponse GetAccessToken(string[] scopes, bool forceRefresh = false);
        Task<AccessTokenResponse> GetAccessTokenAsync(string[] scopes, bool forceRefresh = false);
    }
}