using InfluxDB.Client.Writes;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace InfluxConnector
{
    public static class PointDataExtensions
    {
        public static PointData SetTags(this PointData point, IDictionary<string, string> tags)
        {
            foreach (var tag in tags)
            {
                point = point.Tag(tag.Key, tag.Value);
            }
            return point;
        }

        public static PointData SetFields(this PointData point, IDictionary<string, object> fields)
        {
            foreach (var field in fields)
            {
                if (field.Value is JValue jValue)
                {
                    switch (jValue.Type)
                    {
                        case JTokenType.Boolean:
                            point = point.Field(field.Key, (bool)jValue.Value);
                            break;
                        case JTokenType.Integer:
                            point = point.Field(field.Key, Convert.ToInt64(jValue.Value));
                            break;
                        case JTokenType.Float:
                            point = point.Field(field.Key, Convert.ToDouble(jValue.Value));
                            break;
                        case JTokenType.String:
                            point = point.Field(field.Key, jValue.Value?.ToString());
                            break;
                        case JTokenType.Date:
                            point = point.Field(field.Key, ((DateTime)jValue.Value).ToString("yyyy-MM-ddTHH:mm:ssZ"));
                            break;
                    }
                }
                else
                {
                    point = point.Field(field.Key, field.Value);
                }
            }
            return point;
        }
    }
}
