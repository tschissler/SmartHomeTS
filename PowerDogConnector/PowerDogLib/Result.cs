using CookComputing.XmlRpc;

namespace PowerDogLib
{
    public struct Result
    {
        public string ErrorString;
        public XmlRpcStruct Reply;
        public int ErrorCode;
    }
}