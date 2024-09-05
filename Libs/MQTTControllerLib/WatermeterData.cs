using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MQTTControllerLib
{
    internal class WatermeterData
    {
        public string value { get; set; }
        public string raw { get; set; }
        public string pre { get; set; }
        public string error { get; set; }
        public string rate { get; set; }
        public DateTime timestamp { get; set; }
    }
}
