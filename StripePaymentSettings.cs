using Nop.Core.Configuration;

namespace Nop.Plugin.Payments.Stripe
{
    public class StripePaymentSettings : ISettings
    {
        public bool UseSandbox { get; set; }
        public TransactMode TransactMode { get; set; }
        public string TransactionKey { get; set; }
        public string LoginId { get; set; }
        public decimal AdditionalFee { get; set; }
    }

    public static class StripeChargeStatus
    {
        public  const  string SUCCESS = "success";
        public  const string FAILURE = "failure";
    }
}
