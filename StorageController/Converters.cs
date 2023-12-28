using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace StorageController
{
    public class Converters
    {
        public static string ConvertDateTimeToReverseRowKey(DateTime time)
        {
            Int64 timeInt = 0;
            Int64.TryParse(time.ToUniversalTime().ToString("yyyyMMddHHmmss"), out timeInt);
            var timeValue = 99999999999999 - timeInt;
            return timeValue.ToString();
        }
    }
}
