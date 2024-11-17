namespace SharedContracts
{
    public class IluminationSituation
    {
        public IluminationSettings Left { get; set; }
        public IluminationSettings Right { get; set; }
        public IluminationSituation()
        {
            Left = new();
            Right = new();
        }
        public bool LampOn { get; set; }
    }
    
    public class IluminationSettings
    {
        public byte Red { get; set; }
        public byte Green { get; set; }
        public byte Blue { get; set; }
        public int Density { get; set; }
        public bool On { get; set; }
    }
}