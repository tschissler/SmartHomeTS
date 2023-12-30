namespace SmartHomeHelpers.Configuration
{
    public class Storage
    {
        public static string SmartHomeStorageKey { get ;  }

        public static string SmartHomeStorageUri { get; }

        static Storage()
        {
            foreach (var field in typeof(Storage).GetProperties())
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
