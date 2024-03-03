using System.Diagnostics;

namespace SmartHomeHelpers.Logging
{
    public class ConsoleHelpers
    {
        static ConsoleHelpers()
        {
            Trace.Listeners.Add(new ConsoleTraceListener());
        }

        public static void PrintErrorMessage(string message)
        {
            Trace.TraceError($"{GetTimeStampText()} - {message}");
        }

        public static void PrintInformation(string message)
        {
            Trace.TraceInformation($"{GetTimeStampText()} - {message}");
        }

        public static void PrintMessage(string message)
        {
            Console.WriteLine(message);
        }

        private static string GetTimeStampText()
        {
            return $"{DateTime.Now.ToShortDateString()}-{DateTime.Now.ToLongTimeString()}";
        }
    }
}