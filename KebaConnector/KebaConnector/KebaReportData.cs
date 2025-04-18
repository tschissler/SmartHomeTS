using Newtonsoft.Json;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading.Tasks;
using static System.Collections.Specialized.BitVector32;

namespace KebaConnector
{
    public class KebaReportData
    {
        public int ID { get; set; }
        [JsonProperty("Session ID")]
        public int SessionID { get; set; }
        [JsonProperty("Curr HW")]
        public int CurrHW { get; set; }
        [JsonProperty("E start")]
        public int Estart { get; set; }
        [JsonProperty("E pres")]
        public int Epres { get; set; }
        [JsonProperty("started[s]")]
        public int Started_sec { get; set; }
        [JsonProperty("ended[s]")]
        public int Ended_sec { get; set; }
        [JsonProperty("started")]
        public string? Started { get; set; }
        [JsonProperty("ended")]
        public string? Ended { get; set; }
        /// <summary>
        ///  0 = Charging session has not ended.
        ///  1 = Charging session was terminated by unplugging.
        /// 10 = Charging session was terminated via deauthorization 
        ///      with the RFID card used for starting the session.
        /// </summary>
        [JsonProperty("reason")]
        public int Reason { get; set; }
        /// <summary>
        /// 0 = Not synced time 
        /// X = Strong synced time 
        /// 2 = Weak synced time
        /// </summary>
        [JsonProperty("timeQ")]
        public int TimeQ { get; set; }
        [JsonProperty("RFID tag")]
        public string RfidTag { get; set; } = String.Empty;
        [JsonProperty("RFID class")]
        public string RfidClass { get; set; } = String.Empty;
        public string Serial { get; set; } = String.Empty;
    }
}
