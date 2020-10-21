using System.Threading.Tasks;

namespace azman_v2
{
    public interface ITokenProvider
    {
        Task<string> GetAccessTokenAsync(string resource = "https://graph.microsoft.com", bool forceRefresh = false);
    }
}