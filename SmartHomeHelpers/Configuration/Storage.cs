namespace SmartHomeHelpers.Configuration
{
    public class Storage
    {
        public static string SmartHomeStorageKey { get; private set; }

        public static string SmartHomeStorageUri { get; private set; }

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
