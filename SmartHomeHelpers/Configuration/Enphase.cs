using SmartHomeHelpers.Configuration.Helpers;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartHomeHelpers.Configuration
{
    public class Enphase
    {
        public static string EnphaseUserName { get; private set; }
        public static string EnphasePassword { get; private set; }
        public static string EnvoyM1Serial { get; private set; }
        public static string EnvoyM3Serial { get; private set; }

        static Enphase()
        {
            EnvironmentVariablesHelper.ReadFromEnvironmentVariables(typeof(Enphase));
        }
    }
}
