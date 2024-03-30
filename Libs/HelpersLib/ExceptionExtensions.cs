using System.Text;

namespace HelpersLib
{
    public static class ExceptionExtensions
    {
        public static string ToDetailedString(this Exception ex)
        {
            StringBuilder sb = new StringBuilder();
            AppendDetails(sb, ex, 0);
            return sb.ToString();
        }

        private static void AppendDetails(StringBuilder sb, Exception ex, int level)
        {
            sb.AppendLine($"{new string('-', level)}Exception Type: {ex.GetType().FullName}");
            sb.AppendLine($"{new string('-', level)}Message: {ex.Message}");
            sb.AppendLine($"{new string('-', level)}Stack Trace: {ex.StackTrace}");

            if (ex.InnerException != null)
            {
                sb.AppendLine($"{new string('-', level)}Inner Exception:");
                AppendDetails(sb, ex.InnerException, level + 1);
            }
        }
    }
}
