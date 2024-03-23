using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EnphaseConnector
{
    using System;
    using System.Net.Http;
    using System.Text.Json;
    using System.Text;
    using System.Threading.Tasks;

    public class EnphaseLocalAuth
    {
        private readonly HttpClient httpClient;

        public EnphaseLocalAuth()
        {
            httpClient = new HttpClient();
        }

        public async Task<EnphaseLocalToken> GetTokenAsync(string userName, string password, string envoySerial)
        {
            var token = new EnphaseLocalToken();
            try
            {
                // Login to get session_id
                var loginData = new FormUrlEncodedContent(new[]
                {
                new KeyValuePair<string, string>("user[email]", userName),
                new KeyValuePair<string, string>("user[password]", password),
            });
                var loginResponse = await httpClient.PostAsync("https://enlighten.enphaseenergy.com/login/login.json?", loginData);
                var loginResponseData = await JsonSerializer.DeserializeAsync<Dictionary<string, object>>(await loginResponse.Content.ReadAsStreamAsync());
                var sessionId = loginResponseData["session_id"].ToString();

                // Use session_id to get the token
                var requestData = new
                {
                    session_id = sessionId,
                    serial_num = envoySerial,
                    username = userName
                };
                var requestContent = new StringContent(JsonSerializer.Serialize(requestData), Encoding.UTF8, "application/json");
                var response = await httpClient.PostAsync("https://entrez.enphaseenergy.com/tokens", requestContent);
                var tokenRaw = await response.Content.ReadAsStringAsync();

                token.Token = tokenRaw;
                token.SessionId = sessionId;

                return token;
                //return loginResponseData["manager_token"].ToString();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"An error occurred: {ex.Message}");
                return null;
            }
        }
    }

    public class EnphaseLocalToken
    {
        public string SessionId { get; set; }
        public string Token { get; set; }
    }
}
