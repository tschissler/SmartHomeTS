using CookComputing.XmlRpc;

namespace PowerDogLib
{
    public class XmlRpcProxy : XmlRpcClientProtocol
    {
        [XmlRpcMethod("getPowerDogInfo")]
        public Result getPowerDogInfo(string password)
        {
            return (Result)Invoke("getPowerDogInfo", new object[] { password });
        }

        [XmlRpcMethod("getAllCurrentLinearValues")]
        public Result getAllCurrentLinearValues(string password)
        {
            try
            {
                return (Result)Invoke("getAllCurrentLinearValues", new object[] { password });
            }
            catch
            {
                return new Result();
            }
        }
    }
}