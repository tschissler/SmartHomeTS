namespace EnphaseConnector
{
    public class EnphaseSettings
    {
        public string EnphaseUserName { get; set; } = string.Empty;
        public string EnphasePassword { get; set; } = string.Empty;
        public string EnvoyM1Serial { get; set; } = string.Empty;
        public string EnvoyM3Serial { get; set; } = string.Empty;
        public string MqttBroker { get; set; } = "mosquitto.intern";
        public int MqttPort { get; set; } = 1883;
        public int ReadIntervalMs { get; set; } = 1000;
        public int HealthCheckPort { get; set; } = 8080;
    }
}
