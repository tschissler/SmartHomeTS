using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ShellyConnector.DataContracts
{
    public class Emeter
    {
        public decimal Power { get; set; }
        public decimal Pf { get; set; }
        public decimal Current { get; set; }
        public decimal Voltage { get; set; }
        public bool Is_Valid { get; set; }
        public decimal Total { get; set; }
        public decimal TotalReturned { get; set; }
    }

    public class ShellyEmeterData
    {
        public List<Emeter> Emeters { get; set; }
    }


    public class ShellyEmeterGen3PowerData
    {
        public int id { get; set; }
        public decimal a_current { get; set; }
        public decimal a_voltage { get; set; }
        public decimal a_act_power { get; set; }
        public decimal a_aprt_power { get; set; }
        public decimal a_pf { get; set; }
        public decimal a_freq { get; set; }
        public decimal b_current { get; set; }
        public decimal b_voltage { get; set; }
        public decimal b_act_power { get; set; }
        public decimal b_aprt_power { get; set; }
        public decimal b_pf { get; set; }
        public decimal b_freq { get; set; }
        public decimal c_current { get; set; }
        public decimal c_voltage { get; set; }
        public decimal c_act_power { get; set; }
        public decimal c_aprt_power { get; set; }
        public decimal c_pf { get; set; }
        public decimal c_freq { get; set; }
        public object n_current { get; set; }
        public decimal total_current { get; set; }
        public decimal total_act_power { get; set; }
        public decimal total_aprt_power { get; set; }
        public object[] user_calibrated_phase { get; set; }
    }


    public class ShellyEmeterGen3EnergyData
    {
        public int id { get; set; }
        public decimal a_total_act_energy { get; set; }
        public decimal a_total_act_ret_energy { get; set; }
        public decimal b_total_act_energy { get; set; }
        public decimal b_total_act_ret_energy { get; set; }
        public decimal c_total_act_energy { get; set; }
        public decimal c_total_act_ret_energy { get; set; }
        public decimal total_act { get; set; }
        public decimal total_act_ret { get; set; }
    }

}
