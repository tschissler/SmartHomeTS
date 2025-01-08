using EnphaseConnector.EnphaseRawData;
using Newtonsoft.Json;
using SharedContracts;

namespace EnphaseConnector
{
    public class EnphaseLib
    {
        private const string LocalProductionApiUrl = "https://envoym3/production.json";
        private const string LocalInventoryApiUrl = "https://envoy.local/ivp/ensemble/inventory";
        private const string LiveDataUrl = "https://{devicename}/ivp/livedata/status";
        private const string AKtiviereLiveDataStreamUrl = "https://{devicename}/ivp/livedata/stream";
        private Dictionary<string, DateTime> AktivierungszeitpunkteFuerLiveDataStream;

        public EnphaseLib()
        {
            AktivierungszeitpunkteFuerLiveDataStream = new();
        }

        private async Task AktiviereLiveDataStream(EnphaseLocalToken token, string deviceName)
        {
            var clientHandler = new HttpClientHandler
            {
                UseCookies = false,
            };
            clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

            var client = new HttpClient(clientHandler);
            var request = new HttpRequestMessage
            {
                Method = HttpMethod.Post,
                RequestUri = new Uri(AKtiviereLiveDataStreamUrl.Replace("{devicename}", deviceName)),
                Headers =
                {
                    { "cookie", $"sessionId={token.SessionId}" },
                    { "Authorization", $"Bearer {token.Token}" },
                },
                Content = new StringContent("{\"enable\": 1}")
            };
            using (var response = await client.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
            }
        }

        public async Task<EnphaseData> FetchDataAsync(EnphaseLocalToken token, string deviceName)
        {
            try
            {
                if (!AktivierungszeitpunkteFuerLiveDataStream.ContainsKey(deviceName))
                {
                    await AktiviereLiveDataStream(token, deviceName).ConfigureAwait(false);
                    AktivierungszeitpunkteFuerLiveDataStream.Add(deviceName, DateTime.Now);
                }
                if(DateTime.Now.Subtract(AktivierungszeitpunkteFuerLiveDataStream[deviceName]).TotalMinutes > 10)
                {
                    await AktiviereLiveDataStream(token, deviceName).ConfigureAwait(false);
                    AktivierungszeitpunkteFuerLiveDataStream[deviceName] = DateTime.Now;
                }
                
                var clientHandler = new HttpClientHandler
                {
                    UseCookies = false,
                };
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                var client = new HttpClient(clientHandler);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(LiveDataUrl.Replace("{devicename}", deviceName)),
                    Headers =
                    {
                        { "cookie", $"sessionId={token.SessionId}" },
                        { "Authorization", $"Bearer {token.Token}" },
                    },
                };
                using (var response = await client.SendAsync(request))
                {
                    response.EnsureSuccessStatusCode();
                    var body = await response.Content.ReadAsStringAsync();
                    var rawData = JsonConvert.DeserializeObject<EnphaseLiveData>(body);

                    if (deviceName == "envoym1")
                    {
                        return new EnphaseData(
                            DateTimeOffset.FromUnixTimeSeconds(rawData.Meters.Last_Update),
                            rawData.Meters.Soc,
                            rawData.Meters.Enc_Agg_Energy,
                            rawData.Meters.Pv.Agg_P_Mw / 1000,
                            rawData.Meters.Storage.Agg_P_Mw / 1000,
                            rawData.Meters.Grid.Agg_P_Mw / 1000,
                            rawData.Meters.Load.Agg_P_Mw / 1000
                            );
                    }
                    else
                    {
                        return new EnphaseData(
                            DateTimeOffset.FromUnixTimeSeconds(rawData.Meters.Last_Update),
                            rawData.Meters.Soc,
                            rawData.Meters.Enc_Agg_Energy,
                            rawData.Meters.Pv.Agg_P_Mw,
                            rawData.Meters.Storage.Agg_P_Mw,
                            rawData.Meters.Grid.Agg_P_Mw,
                            rawData.Meters.Load.Agg_P_Mw
                            );
                    }
                }
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException Caught!");
                Console.WriteLine("Message :{0} ", e);
            }

            return null;
        }
    }
}
