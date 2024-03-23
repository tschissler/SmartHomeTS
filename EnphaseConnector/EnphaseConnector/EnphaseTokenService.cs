using IdentityModel.Client;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Net.WebRequestMethods;

namespace EnphaseConnector
{
    public class EnphaseTokenService
    {
        private static readonly string enphaseOAuthEndpoint = "https://api.enphaseenergy.com/oauth/token";
        private static readonly string redirectUri = "https://api.enphaseenergy.com/oauth/redirect_uri";

        // https://api.enphaseenergy.com/oauth/token?grant_type=authorization_code&redirect_uri=https%3A%2F%2Fapi.enphaseenergy.com%2Foauth%2Fredirect_uri&code={startToken}

        public static async Task<EnphaseToken> GetTokenAsync(string clientId, string clientSecret, string refreshToken, string code = "")
        {
            var token = new EnphaseToken();
            var client = new HttpClient();

            if (string.IsNullOrEmpty(refreshToken))
            {
                if (string.IsNullOrEmpty(code))
                    throw new Exception("The refreshtoken is empty and no one-time code provided, cannot gather access token for Enphase API");

                var response = await client.RequestAuthorizationCodeTokenAsync(new AuthorizationCodeTokenRequest
                {
                    Address = enphaseOAuthEndpoint,
                    ClientId = clientId,
                    ClientSecret = clientSecret,

                    Code = code,
                    RedirectUri = redirectUri,
                });

                if (response.HttpStatusCode == HttpStatusCode.Unauthorized || response.HttpStatusCode == HttpStatusCode.BadRequest)
                {
                    throw new Exception("Cannot gather initial access token for Enphase API for the given code. The code moste likely expired. Please provide a new code by going to https://api.enphaseenergy.com/oauth/authorize?response_type=code&client_id=4b8f643ea5d25d39bdeef4c4c01a5da7&redirect_uri=https://api.enphaseenergy.com/oauth/redirect_uri");
                }
                token.AccessToken = response.AccessToken;
                token.RefreshToken = response.RefreshToken;
                token.AccessTokenExpiryTime = DateTime.Now.AddSeconds(response.ExpiresIn).AddSeconds(-120);
            }
            else
            {
                var response = await client.RequestRefreshTokenAsync(new RefreshTokenRequest
                {
                    Address = enphaseOAuthEndpoint,
                    ClientId = clientId,
                    ClientSecret = clientSecret,

                    RefreshToken = refreshToken
                });

                token.AccessToken = response.AccessToken;
                token.RefreshToken = response.RefreshToken;
                token.AccessTokenExpiryTime = DateTime.Now.AddSeconds(response.ExpiresIn).AddSeconds(-120);
            }
            return token;
        }
    }

    public class EnphaseToken
    {
        public string AccessToken { get; set; }
        public string RefreshToken { get; set; }
        public DateTime AccessTokenExpiryTime { get; set; }
    }
}
