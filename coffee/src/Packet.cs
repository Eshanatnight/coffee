namespace Latte
{
    public class Packet
    {
        public required string symbol { get; set; }
        public char buySellIndicator { get; set; }
        public int quantity { get; set; }
        public int price { get; set; }
        public int packetSequence { get; set; }
    }
}
