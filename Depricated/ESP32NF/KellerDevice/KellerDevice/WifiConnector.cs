using nanoFramework.Networking;
using System;
using System.Device.Wifi;
using System.Text;
using System.Threading;

namespace KellerDevice
{
    public class WifiConnector
    {
        private WifiAdapter wifi;

        public bool IsConnected = false;
        public string ConnectedSsid = string.Empty;

        public WifiConnector()
        {
            Logger.LogInformation("Connecting to wifi.");
            wifi = WifiAdapter.FindAllAdapters()[0];
            wifi.AvailableNetworksChanged += WifiAdapter_AvailableNetworksChanged;
            wifi.ScanAsync();
        }

        private void WifiAdapter_AvailableNetworksChanged(WifiAdapter sender, object e)
        {
            while (true)
            {
                var networks = wifi.NetworkReport;
                if (networks != null && networks.AvailableNetworks != null && networks.AvailableNetworks.Length > 0)
                {
                    var strongestNetwork = networks.AvailableNetworks[0];
                    if (Secrets.WifiSecrets.WifiPasswords.Contains(strongestNetwork.Ssid))
                    {
                        var connectionResult = WifiNetworkHelper.ConnectDhcp(
                            strongestNetwork.Ssid,
                            Secrets.WifiSecrets.WifiPasswords[strongestNetwork.Ssid].ToString(),
                            WifiReconnectionKind.Automatic,
                            true);
                        if (connectionResult)
                        {
                            IsConnected = true;
                            ConnectedSsid = strongestNetwork.Ssid;
                            Logger.LogInformation($"Connected to {strongestNetwork.Ssid}");
                            break;
                        }
                        Logger.LogError($"Failed to connect to {strongestNetwork.Ssid}");
                        if (WifiNetworkHelper.HelperException != null)
                        {
                            Logger.LogError($"{WifiNetworkHelper.HelperException}");
                        }
                    }
                }
                Thread.Sleep(60000);
                Logger.LogInformation("Retrying to connect to Wifi");
            }
        }
    }
}
