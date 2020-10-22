using System;

namespace azman_v2.Auth
{
    // named to not conflict with Azure SDK
    public class AccessTokenResponse
    {
        public string Resource { get; set; }
        public string Token { get; set; }
        public DateTimeOffset Expiry { get; set; }

        public AccessTokenResponse(string resource, string token, DateTimeOffset expiry)
        {
            Resource = resource;
            Token = token;
            Expiry = expiry;
        }
    }
}