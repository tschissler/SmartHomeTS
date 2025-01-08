using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{
    public class Emeter
    {
        public double Power { get; set; }
        public double Pf { get; set; }
        public double Current { get; set; }
        public double Voltage { get; set; }
        public bool IsValid { get; set; }
        public double Total { get; set; }
        public double TotalReturned { get; set; }
    }

    public class ShellyEmeterData
    {
        public List<Emeter> Emeters { get; set; }
    }
}
