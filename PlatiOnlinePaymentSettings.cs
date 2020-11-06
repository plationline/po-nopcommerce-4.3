using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.PlatiOnline
{
    public class PlatiOnlinePaymentSettings : ISettings
    {
        public string Merchant_Id { get; set; }
        public string Public_Key { get; set; }
        public string Private_Key { get; set; }
        public string IvAuth { get; set; }
        public string IvItsn { get; set; }
        public bool RON { get; set; }
        public bool EUR { get; set; }
        public bool USD { get; set; }
        public Currency Curency { get; set; }
        public TransactMode TransactMode { get; set; }
        public string Relay_Response_URL { get; set; }
        public RelayMethod RelayMethod { get; set; }
        public decimal AdditionalFee { get; set; }
        public bool TestMode { get; set; }
        public bool SSL { get; set; }
    }
}
