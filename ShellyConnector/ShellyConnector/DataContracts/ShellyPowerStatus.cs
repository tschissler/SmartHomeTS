using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{

    public class ShellyPowerStatus
    {
        public object name { get; set; }
        public string id { get; set; }
        public string mac { get; set; }
        public int slot { get; set; }
        public string model { get; set; }
        public int gen { get; set; }
        public string fw_id { get; set; }
        public string ver { get; set; }
        public string app { get; set; }
        public string type { get; set; }
        public bool auth_en { get; set; }
        public object auth_domain { get; set; }
    }
}