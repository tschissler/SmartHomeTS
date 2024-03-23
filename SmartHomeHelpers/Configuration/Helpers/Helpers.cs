using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace SmartHomeHelpers.Configuration.Helpers
{
    internal class EnvironmentVariablesHelper
    {
        internal static void ReadFromEnvironmentVariables(Type type)
        {
            foreach (var field in type.GetProperties())
            {
                if (Environment.GetEnvironmentVariable(field.Name) is string envVariableValue)
                {
                    field.SetValue(null, envVariableValue);
                }
                else
                {
                    throw new Exception($"---> EnvironmentVariable {field.Name} is not set, execution will stop as a mandatory configuration is missing.");
                }
            }
        }
    }
}
