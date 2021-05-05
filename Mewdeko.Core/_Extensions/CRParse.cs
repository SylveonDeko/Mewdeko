namespace Mewdeko.Extensions
{
    public class CReacts
    {
        public CRs[] Reacts { get; set; }
    }

    public class CRs
    {
        public string Trigger { get; set; }
        public Respons[] Responses { get; set; }
    }

    public class Respons
    {
        public int id { get; set; }
        public string text { get; set; }
    }
}