using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace SharedContracts
{
    public class CarStatusData
    {
        [JsonPropertyName("nickname")]
        public string Nickname { get; set; }

        [JsonPropertyName("brand")]
        public string Brand { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("battery")]
        public double Battery { get; set; }

        [JsonPropertyName("chargingStatus")]
        public string ChargingStatus { get; set; }

        [JsonPropertyName("chargingTarget")]
        public double ChargingTarget { get; set; }

        [JsonPropertyName("chargingEndTime")]
        public DateTime? ChargingEndTime { get; set; }

        [JsonPropertyName("chargerConnected")]
        public bool ChargerConnected { get; set; }

        [JsonPropertyName("state")]
        public string State { get; set; }

        [JsonPropertyName("remainingRange")]
        public double RemainingRange { get; set; }

        [JsonPropertyName("mileage")]
        public double Mileage { get; set; }

        [JsonPropertyName("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [JsonPropertyName("position")]
        public GeoPosition? Position { get; set; }

        [JsonPropertyName("moving")]
        public bool Moving { get; set; }


        public CarStatusData()
        {
            Nickname = "";
            Brand = "";
            Name = "";
            Battery = 0;
            ChargingStatus = "";
            ChargingTarget = 0;
            ChargingEndTime = DateTime.MinValue;
            ChargerConnected = false;
            RemainingRange = 0;
            Mileage = 0;
            Moving = false;
            State = "";
            Position = new GeoPosition();
            LastUpdate = DateTime.MinValue;
        }
    }

    public class GeoPosition
    {
        [JsonPropertyName("latitude")]
        public double Latitude { get; set; }
        [JsonPropertyName("longitude")]
        public double Longitude { get; set; }
        public GeoPosition()
        {
            Latitude = 0;
            Longitude = 0;
        }
    }
}
