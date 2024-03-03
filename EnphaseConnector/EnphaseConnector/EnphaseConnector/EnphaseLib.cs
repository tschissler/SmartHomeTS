using Newtonsoft.Json;
using System.Net.Http;

namespace EnphaseConnector
{
    public class EnphaseLib
    {
        private const string LocalProductionApiUrl = "https://envoy.local/production.json";
        private const string LocalInventoryApiUrl = "https://envoy.local/ivp/ensemble/inventory";
        private const string token = "eyJraWQiOiI3ZDEwMDA1ZC03ODk5LTRkMGQtYmNiNC0yNDRmOThlZTE1NmIiLCJ0eXAiOiJKV1QiLCJhbGciOiJFUzI1NiJ9.eyJhdWQiOiIxMjIyNDQxMDUzMTYiLCJpc3MiOiJFbnRyZXoiLCJlbnBoYXNlVXNlciI6Imluc3RhbGxlciIsImV4cCI6MTcwOTU0NTI3NSwiaWF0IjoxNzA5NTAyMDc1LCJqdGkiOiI1ZjM4MWNlMS0xMWZiLTQ2M2ItOThjNC0wYjU3NzczZjk1YjEiLCJ1c2VybmFtZSI6ImVucGhhc2VAYWdpbGVtYXguZGUifQ.mNae07lFQ1NFr4uCSB0zjyukY5V7scUfNopzhd8H6rIBHPpNO0U8UvZuC1n_IElEyebrrms-Y6eBf1hQmG5i9A";

        public async Task FetchDataAsync()
        {
            try
            {
                var clientHandler = new HttpClientHandler();
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                var httpClient = new HttpClient(clientHandler);
                httpClient.DefaultRequestHeaders.Authorization = new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", token);
                var response = await httpClient.GetAsync(LocalProductionApiUrl);
                response.EnsureSuccessStatusCode();
                var content = await response.Content.ReadAsStringAsync();
                var data = JsonConvert.DeserializeObject<dynamic>(content);

                Console.WriteLine("Data fetched successfully!");
                Console.WriteLine(data);

                response = await httpClient.GetAsync(LocalInventoryApiUrl);
                response.EnsureSuccessStatusCode();
                content = await response.Content.ReadAsStringAsync();
                data = JsonConvert.DeserializeObject<dynamic>(content);

                Console.WriteLine("Data fetched successfully!");
                Console.WriteLine(data);

            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e.Message);
            }
        }
    }
}
