using EnphaseConnector.EnphaseRawData;
using Newtonsoft.Json;
using SharedContracts;

namespace EnphaseConnector
{
    public class EnphaseLib
    {
        private const string LocalProductionApiUrl = "https://{devicename}/production.json";
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

        public async Task<(decimal? ErzeugteLebensenergie, decimal? VerbrauchteHausenergie)> FetchProductionDataAsync(EnphaseLocalToken token, string deviceName)
        {
            try
            {
                var clientHandler = new HttpClientHandler { UseCookies = false };
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;

                var client = new HttpClient(clientHandler);
                var request = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(LocalProductionApiUrl.Replace("{devicename}", deviceName)),
                    Headers =
                    {
                        { "cookie", $"sessionId={token.SessionId}" },
                        { "Authorization", $"Bearer {token.Token}" },
                    },
                };
                using var response = await client.SendAsync(request);
                response.EnsureSuccessStatusCode();
                var body = await response.Content.ReadAsStringAsync();
                var rawData = JsonConvert.DeserializeObject<EnphaseProductionData>(body);

                var pvProduktion = rawData?.Production?.FirstOrDefault(p => p.Type == "eim");
                var hausverbrauch = rawData?.Consumption?.FirstOrDefault(c => c.MeasurementType == "total-consumption");

                return (
                    pvProduktion != null ? (decimal)pvProduktion.WhLifetime : null,
                    hausverbrauch != null ? (decimal)hausverbrauch.WhLifetime : null
                );
            }
            catch (HttpRequestException e)
            {
                Console.WriteLine("\nException beim Abruf der Produktionsdaten!");
                Console.WriteLine("Message :{0} ", e);
                return (null, null);
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
                            rawData.Meters.Pv.Agg_P_Mw,
                            rawData.Meters.Storage.Agg_P_Mw,
                            rawData.Meters.Grid.Agg_P_Mw,
                            rawData.Meters.Load.Agg_P_Mw
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
