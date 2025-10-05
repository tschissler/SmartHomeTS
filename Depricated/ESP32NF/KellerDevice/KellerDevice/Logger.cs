using System;

namespace KellerDevice
{
    public class Logger
    {
        public static void LogInformation(string message)
        {
            Console.WriteLine($"Information:\t{message}");
        }

        public static void LogError(string message)
        {
            Console.WriteLine($"### Error:\t{message}");
        }
    }
}
