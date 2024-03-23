using SmartHomeHelpers.Configuration.Helpers;

namespace SmartHomeHelpers.Configuration
{
    public class Storage
    {
        public static string SmartHomeStorageKey { get; private set; }

        public static string SmartHomeStorageUri { get; private set; }

        static Storage()
        {
            EnvironmentVariablesHelper.ReadFromEnvironmentVariables(typeof(Storage));
        }
    }
}
