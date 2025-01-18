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
        public int Battery { get; set; }

        [JsonPropertyName("chargingStatus")]
        public string ChargingStatus { get; set; }

        [JsonPropertyName("chargingTarget")]
        public int ChargingTarget { get; set; }

        [JsonPropertyName("chargingEndTime")]
        public DateTime? ChargingEndTime { get; set; }

        [JsonPropertyName("chargerConnected")]
        public bool ChargerConnected { get; set; }

        [JsonPropertyName("remainingRange")]
        public int RemainingRange { get; set; }

        [JsonPropertyName("mileage")]
        public int Mileage { get; set; }

        [JsonPropertyName("lastUpdate")]
        public DateTime? LastUpdate { get; set; }

        [JsonPropertyName("moving")]
        public bool Moving { get; set; }


        public CarStatusData()
        {
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
            LastUpdate = DateTime.MinValue;
        }
    }
}
