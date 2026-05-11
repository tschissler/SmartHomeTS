using EnphaseConnector.EnphaseRawData;
using System.Collections.Generic;
using Newtonsoft.Json;
using SharedContracts;

namespace EnphaseConnector
{
    public class EnphaseLib
    {
        private const string LocalProductionApiUrl = "https://{devicename}/production.json";
        private const string LocalInventoryApiUrl = "https://{devicename}/ivp/ensemble/inventory";
        private const string MeterReadingsUrl = "https://{devicename}/ivp/meters/readings";
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

        public async Task<(decimal? EnergyFromPV, decimal? EnergyToHouse, decimal? EnergyFromGrid, decimal? EnergyToGrid)> FetchProductionDataAsync(EnphaseLocalToken token, string deviceName)
        {
            try
            {
                var clientHandler = new HttpClientHandler { UseCookies = false };
                clientHandler.ServerCertificateCustomValidationCallback = (sender, cert, chain, sslPolicyErrors) => true;
                var client = new HttpClient(clientHandler);

                var productionRequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(LocalProductionApiUrl.Replace("{devicename}", deviceName)),
                    Headers =
                    {
                        { "cookie", $"sessionId={token.SessionId}" },
                        { "Authorization", $"Bearer {token.Token}" },
                    },
                };
                using var productionResponse = await client.SendAsync(productionRequest);
                Console.WriteLine($"{DateTime.Now} --- production.json [{deviceName}]: HTTP {(int)productionResponse.StatusCode} {productionResponse.StatusCode}");
                if (!productionResponse.IsSuccessStatusCode)
                {
                    var errorBody = await productionResponse.Content.ReadAsStringAsync();
                    Console.WriteLine($"  Response body: {errorBody[..Math.Min(200, errorBody.Length)]}");
                    return (null, null, null, null);
                }
                var productionBody = await productionResponse.Content.ReadAsStringAsync();
                var productionData = JsonConvert.DeserializeObject<EnphaseProductionData>(productionBody);

                var pvProduktion = productionData?.Production?.FirstOrDefault(p => p.Type == "eim");
                var hausverbrauch = productionData?.Consumption?.FirstOrDefault(c => c.MeasurementType == "total-consumption");

                var meterRequest = new HttpRequestMessage
                {
                    Method = HttpMethod.Get,
                    RequestUri = new Uri(MeterReadingsUrl.Replace("{devicename}", deviceName)),
                    Headers =
                    {
                        { "cookie", $"sessionId={token.SessionId}" },
                        { "Authorization", $"Bearer {token.Token}" },
                    },
                };
                using var meterResponse = await client.SendAsync(meterRequest);
                List<EnphaseMeterReading>? meterReadings = null;
                if (meterResponse.IsSuccessStatusCode)
                {
                    var meterBody = await meterResponse.Content.ReadAsStringAsync();
                    meterReadings = JsonConvert.DeserializeObject<List<EnphaseMeterReading>>(meterBody);
                }

                // The grid meter has a different actEnergyDlvd than the PV production meter
                // and has significant energy in both directions (delivered + received > 0)
                var pvDlvd = pvProduktion?.WhLifetime ?? 0;
                var gridMeter = meterReadings?
                    .Where(m => m.ActEnergyDlvd > 1000 && m.ActEnergyRcvd > 1000)
                    .FirstOrDefault(m => Math.Abs(m.ActEnergyDlvd - pvDlvd) > pvDlvd * 0.01);

                Console.WriteLine($"  PV: {pvProduktion?.WhLifetime} Wh | Haus: {hausverbrauch?.WhLifetime} Wh | Netzbezug: {gridMeter?.ActEnergyDlvd} Wh | Einspeisung: {gridMeter?.ActEnergyRcvd} Wh");

                return (
                    pvProduktion != null ? (decimal)pvProduktion.WhLifetime : null,
                    hausverbrauch != null ? (decimal)hausverbrauch.WhLifetime : null,
                    gridMeter != null ? (decimal)gridMeter.ActEnergyDlvd : null,
                    gridMeter != null ? (decimal)gridMeter.ActEnergyRcvd : null
                );
            }
            catch (Exception e)
            {
                Console.WriteLine($"\nException beim Abruf der Produktionsdaten für {deviceName}!");
                Console.WriteLine("Message :{0} ", e);
                return (null, null, null, null);
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

                    return new EnphaseData(
                        DateTimeOffset.FromUnixTimeSeconds(rawData.Meters.Last_Update),
                        rawData.Meters.Pv.Agg_P_Mw,
                        rawData.Meters.Storage.Agg_P_Mw,
                        rawData.Meters.Grid.Agg_P_Mw,
                        rawData.Meters.Load.Agg_P_Mw,
                        BatteryLevel: rawData.Meters.Soc,
                        BatteryEnergy: rawData.Meters.Enc_Agg_Energy
                        );
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
