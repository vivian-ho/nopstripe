using System;
using System.Collections.Specialized;
using System.Globalization;
using System.Net;
using System.Text;
using System.Web.Routing;
using Nop.Core;
using Nop.Core.Domain;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Core.Plugins;
using Nop.Plugin.Payments.Stripe.Controllers;

using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Payments;

using System.Configuration;

using Xamarin.Payments.Stripe;
using Newtonsoft.Json;
using System.Collections.Generic;

namespace Nop.Plugin.Payments.Stripe
{
    /// <summary>
    /// Stripe payment processor
    /// </summary>
    public class StripePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly StripePaymentSettings _stripePaymentSettings;
        private readonly ISettingService _settingService;
        private readonly ICurrencyService _currencyService;
        private readonly ICustomerService _customerService;
        private readonly CurrencySettings _currencySettings;
        private readonly IWebHelper _webHelper;
        private readonly StoreInformationSettings _storeInformationSettings;

        #endregion

        #region Ctor

        public StripePaymentProcessor(StripePaymentSettings stripePaymentSettings,
            ISettingService settingService, ICurrencyService currencyService,
            ICustomerService customerService,
            CurrencySettings currencySettings, IWebHelper webHelper,
            StoreInformationSettings storeInformationSettings)
        {
            this._stripePaymentSettings = stripePaymentSettings;
            this._settingService = settingService;
            this._currencyService = currencyService;
            this._customerService = customerService;
            this._currencySettings = currencySettings;
            this._webHelper = webHelper;
            this._storeInformationSettings = storeInformationSettings;
        }

        #endregion

        #region Utilities


        /// <summary>
        
        /// </summary>
        /// <returns></returns>
        private string GetStripeUrl()
        {
            return "";
        }

        /// <summary>
        /// Gets Stripe.NET API version
        /// </summary>
        private string GetApiVersion()
        {
            return "1.0";
        }

        
        #endregion

        #region Methods


        public  string StripeApiKey()
        {
            return System.Configuration.ConfigurationManager.AppSettings["StripeApiKey"];
        }

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            
            var customer = _customerService.GetCustomerById(processPaymentRequest.CustomerId);
            //var orderTotal = Math.Round(processPaymentRequest.OrderTotal, 2);

            StripeCreditCardInfo cc = new StripeCreditCardInfo();
            cc.CVC = processPaymentRequest.CreditCardCvv2;
            cc.FullName = customer.BillingAddress.FirstName + " " + customer.BillingAddress.LastName;
            
            cc.Number = processPaymentRequest.CreditCardNumber;
            cc.ExpirationMonth = processPaymentRequest.CreditCardExpireMonth;
            cc.ExpirationYear = processPaymentRequest.CreditCardExpireYear;
            cc.AddressLine1 = customer.BillingAddress.Address1;
            cc.AddressLine2 = customer.BillingAddress.Address2;
            if (customer.BillingAddress.Country.TwoLetterIsoCode.ToLower() == "us")
            {
                cc.StateOrProvince = customer.BillingAddress.StateProvince.Abbreviation;
            }
            else
            {
                cc.StateOrProvince = "ot";
            }

            cc.ZipCode = customer.BillingAddress.ZipPostalCode;
            
            cc.Country = customer.BillingAddress.Country.TwoLetterIsoCode;
            
            StripePayment payment = new StripePayment(_stripePaymentSettings.TransactionKey);
            
            try
            {
                StripeCharge charge = payment.Charge((int)(processPaymentRequest.OrderTotal * 100),
                    _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode.ToLower(), 
                    cc, string.Format("charge for {0} - {1}",
                    cc.FullName, processPaymentRequest.PurchaseOrderNumber));
            
                if (charge != null)
                {
                    result.NewPaymentStatus = PaymentStatus.Paid;
                    _stripePaymentSettings.TransactMode = TransactMode.AuthorizeAndCapture;
                    result.AuthorizationTransactionId = charge.ID;
                    result.AuthorizationTransactionResult = StripeChargeStatus.SUCCESS;
                    //need this for refund
                    result.AuthorizationTransactionCode = _stripePaymentSettings.TransactionKey;
                }
            }
            catch (StripeException stripeException)
            {
                result.AuthorizationTransactionResult = stripeException.StripeError.Message;
                result.AuthorizationTransactionCode = stripeException.StripeError.Code;
                result.AuthorizationTransactionId = "-1";
                result.AddError(string.Format("Declined ({0}: {1} - {2})", result.AuthorizationTransactionCode,
                    result.AuthorizationTransactionResult, _currencyService.GetCurrencyById(_currencySettings.PrimaryStoreCurrencyId).CurrencyCode));
            }

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            //nothing
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <param name="cart">Shoping cart</param>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        /// <summary>
        /// Gets additional handling fee
        /// </summary>
        /// <returns>Additional handling fee</returns>
        public decimal GetAdditionalHandlingFee()
        {
            return _stripePaymentSettings.AdditionalFee;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            var result = new CapturePaymentResult();
            

            return result;
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            var result = new RefundPaymentResult();
            result.AddError("Refund method not supported");
            return result;
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();
            result.AddError("Void method not supported");
            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            

            return null;
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            
            return null;
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException("order");
            
            //it's not a redirection payment method. So we always return false
            return false;
        }

        /// <summary>
        /// Gets a route for provider configuration
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetConfigurationRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "Configure";
            controllerName = "PaymentStripe";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Stripe.Controllers" }, { "area", null } };
        }

        /// <summary>
        /// Gets a route for payment info
        /// </summary>
        /// <param name="actionName">Action name</param>
        /// <param name="controllerName">Controller name</param>
        /// <param name="routeValues">Route values</param>
        public void GetPaymentInfoRoute(out string actionName, out string controllerName, out RouteValueDictionary routeValues)
        {
            actionName = "PaymentInfo";
            controllerName = "PaymentStripe";
            routeValues = new RouteValueDictionary() { { "Namespaces", "Nop.Plugin.Payments.Stripe.Controllers" }, { "area", null } };
        }

        public Type GetControllerType()
        {
            return typeof(PaymentStripeController);
        }

        public override void Install()
        {
            //settings
            var settings = new StripePaymentSettings()
            {
                UseSandbox = false,
                TransactMode = TransactMode.AuthorizeAndCapture,
                TransactionKey = StripeApiKey(),
                LoginId = ""
            };

            _settingService.SaveSetting(settings);

            //locales
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Notes", "If you're using this gateway, ensure that your primary store currency is supported by Stripe.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.UseSandbox", "Use Sandbox");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.UseSandbox.Hint", "Check to enable Sandbox (testing environment).");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactModeValues", "Transaction mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactModeValues.Hint", "Choose transaction mode");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactionKey", "Transaction key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactionKey.Hint", "Specify transaction key");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.LoginId", "Login ID");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.LoginId.Hint", "Specify login identifier.");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee", "Additional fee");
            this.AddOrUpdatePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee.Hint", "Enter additional fee to charge your customers.");
            
            base.Install();
        }

        public override void Uninstall()
        {
            //locales
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Notes");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.UseSandbox");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.UseSandbox.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactModeValues");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactModeValues.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactionKey");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.TransactionKey.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.LoginId");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.LoginId.Hint");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee");
            this.DeletePluginLocaleResource("Plugins.Payments.Stripe.Fields.AdditionalFee.Hint");
            
            base.Uninstall();
        }

        #endregion

        #region Properies

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get
            {
                return true;
            }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get
            {
                return false;
            }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get
            {
                return RecurringPaymentType.Manual;
            }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get
            {
                return PaymentMethodType.Standard;
            }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        /// vho 8/3/2014
        public bool SkipPaymentInfo
        {
            get
            {
                return false;
            }
        }

        #endregion
    }
}
