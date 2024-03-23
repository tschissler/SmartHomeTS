using SmartHomeHelpers.Configuration;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection.Emit;
using System.Text;
using System.Threading.Tasks;

namespace EnphaseConnector.EnphaseRawData
{
    public record EnphaseLiveData(
        Connection Connection,
        Meters Meters,
        Tasks Tasks,
        Counters Counters,
        Dictionary<string, DryContact> Dry_Contacts
);

    public record Connection(
        string Mqtt_State,
        string Prov_State,
        string Auth_State,
        string Sc_Stream,
        string Sc_Debug
    );

    public record Meters(
        long Last_Update,
        int Soc,
        int Main_Relay_State,
        int Gen_Relay_State,
        int Backup_Bat_Mode,
        int Backup_Soc,
        int Is_Split_Phase,
        int Phase_Count,
        int Enc_Agg_Soc,
        int Enc_Agg_Energy,
        int Acb_Agg_Soc,
        int Acb_Agg_Energy,
        ElectricityMeter Pv,
        ElectricityMeter Storage,
        ElectricityMeter Grid,
        ElectricityMeter Load,
        ElectricityMeter Generator
    );

    public record ElectricityMeter(
        long Agg_P_Mw,
        long Agg_S_Mva,
        long Agg_P_Ph_A_Mw,
        long Agg_P_Ph_B_Mw,
        long Agg_P_Ph_C_Mw,
        long Agg_S_Ph_A_Mva,
        long Agg_S_Ph_B_Mva,
        long Agg_S_Ph_C_Mva
    );

    public record Tasks(
        int Task_Id,
        long Timestamp
    );

    public record Counters(
    );

    public record DryContact(
        string Dry_Contact_Id,
        string Dry_Contact_Type,
        string Dry_Contact_Load_Name,
        int Dry_Contact_Status
    );


}
