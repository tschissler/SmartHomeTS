using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{
    public class ShellySwitch
    {
        public decimal apower { get; set; }
        public decimal voltage { get; set; }
        public ShellyEnergy aenergy { get; set; }
    }

    public class ShellyEnergy
    {
        public decimal total { get; set; }
    }

    public class ShellyPlugPlusMeterData
    {
        [JsonProperty("switch:0")]
        public ShellySwitch Switch0 { get; set; }
    }
}
