namespace Nop.Plugin.Payments.Stripe
{
    /// <summary>
    /// Represents Authorize.Net payment processor transaction mode
    /// </summary>
    public enum TransactMode : int
    {
        /// <summary>
        /// Authorize
        /// </summary>
        //Authorize = 1,
        /// <summary>
        /// Authorize and capture
        /// </summary>
        AuthorizeAndCapture= 2
    }
}
