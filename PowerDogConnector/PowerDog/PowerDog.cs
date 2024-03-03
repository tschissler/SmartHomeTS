using CookComputing.XmlRpc;
using SmartHomeHelpers.Logging;
using System.Globalization;

namespace PowerDogLib
{
    public class PowerDog
    {
        private readonly Dictionary<string, string> sensorKeys;
        private readonly string password;
        private readonly XmlRpcProxy proxy;

        public object Lockobject = new object();

        public PowerDog(Dictionary<string, string> sensorKeys, Uri deviceUri, string password)
        {
            this.sensorKeys = sensorKeys;
            this.password = password;
            proxy = new XmlRpcProxy();
            proxy.Url = deviceUri.ToString();
        }

        public PowerDogData ReadSensorsData()
        {
            var data = new PowerDogData();

            if (Monitor.TryEnter(Lockobject, 1000))
            {
                try
                {
                    var result = proxy.getAllCurrentLinearValues(password);
                    if (result.ErrorCode != 0)
                    {
                        ConsoleHelpers.PrintErrorMessage($"Error reading data from PowerDog: {result.ErrorString}");
                    }

                    if (result.Reply == null)
                    {
                        ConsoleHelpers.PrintErrorMessage("Reply is empty, communication with PowerDog failed");
                    }
                    else
                    {
                        double production = ParseSensorValue(result.Reply, sensorKeys["Erzeugung"]);
                        double gridSupply = ParseSensorValue(result.Reply, sensorKeys["lieferung"]);
                        double gridDemand = ParseSensorValue(result.Reply, sensorKeys["Bezug"]);

                        data.PVProduction = production * (5.7 + 5.13) / 5.7;
                        data.GridSupply = gridSupply;
                        data.GridDemand = gridDemand;
                    }
                }
                finally
                {
                    Monitor.Exit(Lockobject);
                }
            }
            return data;
        }

        private double ParseSensorValue(XmlRpcStruct reply, string sensorKey)
        {
            double value;

            if (reply.ContainsKey(sensorKey) &&
                (bool)((XmlRpcStruct)reply[sensorKey])["Valid"] &&
                double.TryParse(((XmlRpcStruct)reply[sensorKey])["Current_Value"].ToString(), NumberStyles.AllowDecimalPoint, CultureInfo.GetCultureInfo("en-US"), out value))
            {
                return value;
            }
            else
            {
                return 0;
            }
        }
    }
}