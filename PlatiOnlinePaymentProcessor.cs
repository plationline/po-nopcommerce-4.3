using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Mvc.Routing;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Net.Http.Headers;
using Nop.Core;
using Nop.Core.Domain.Catalog;
using Nop.Core.Domain.Common;
using Nop.Core.Domain.Customers;
using Nop.Core.Domain.Directory;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Services.Catalog;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Customers;
using Nop.Services.Directory;
using Nop.Services.Localization;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Plugins;
using Nop.Services.Tax;
using Po.Requests.Authorization.Objects;
using Po.Requests.Void.Objects;

namespace Nop.Plugin.Payments.PlatiOnline
{
    /// <summary>
    /// PayPalStandard payment processor
    /// </summary>
    public class PlatiOnlinePaymentProcessor : BasePlugin, IPaymentMethod
    {
        #region Fields

        private readonly CurrencySettings _currencySettings;
        private readonly ICheckoutAttributeParser _checkoutAttributeParser;
        private readonly ICurrencyService _currencyService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly IHttpContextAccessor _httpContextAccessor;
        private readonly ILocalizationService _localizationService;
        private readonly IPaymentService _paymentService;
        private readonly ISettingService _settingService;
        private readonly ITaxService _taxService;
        private readonly IWebHelper _webHelper;
        private readonly IWorkContext _workContext;
        private readonly ICustomerService _customerService;
        private readonly IStateProvinceService _stateProvinceService;
        private readonly ICountryService _countryService;
        private readonly IOrderService _orderService;
        private readonly IProductService _productService;
        //private readonly IUrlHelperFactory _urlHelperFactory;
        //private readonly IActionContextAccessor _actionContextAccessor;
        private readonly PlatiOnlinePaymentSettings _platiOnlinePaymentSettings;

        #endregion

        #region Ctor

        public PlatiOnlinePaymentProcessor(CurrencySettings currencySettings,
            ICheckoutAttributeParser checkoutAttributeParser,
            ICurrencyService currencyService,
            IGenericAttributeService genericAttributeService,
            IHttpContextAccessor httpContextAccessor,
            ILocalizationService localizationService,
            IPaymentService paymentService,
            ISettingService settingService,
            ITaxService taxService,
            IWebHelper webHelper,
            IWorkContext workContext,
            IUrlHelperFactory urlHelperFactory,
            IActionContextAccessor actionContextAccessor,
            ICustomerService customerService,
            IStateProvinceService stateProvinceService,
            ICountryService countryService,
            IOrderService orderService,
            IProductService productService,
            PlatiOnlinePaymentSettings platiOnlinePaymentSettings)
        {
            _currencySettings = currencySettings;
            _checkoutAttributeParser = checkoutAttributeParser;
            _currencyService = currencyService;
            _genericAttributeService = genericAttributeService;
            _httpContextAccessor = httpContextAccessor;
            _localizationService = localizationService;
            _paymentService = paymentService;
            _settingService = settingService;
            _taxService = taxService;
            _webHelper = webHelper;
            _platiOnlinePaymentSettings = platiOnlinePaymentSettings;
            _workContext = workContext;
            _customerService = customerService;
            _stateProvinceService = stateProvinceService;
            _countryService = countryService;
            _orderService = orderService;
            _productService = productService;
        }

        #endregion

        #region Utilities

        public string ObjectIsNullOrEmpty(object obj)
        {
            string rez = "-";
            if (obj != null)
            {
                if (obj.ToString().Trim() != "")
                {
                    rez = obj.ToString().Trim();
                }
            }
            return rez;
        }

        public string PhoneObjectIsNullOrEmpty(object obj)
        {
            string rez = "-";
            if (obj != null)
            {
                if (obj.ToString().Trim() != "")
                {
                    rez = obj.ToString().Trim();
                }
            }
            if (rez.Length < 10) rez = "0000000000";
            return rez;
        }

        #endregion

        #region Methods

        /// <summary>
        /// Process a payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessPayment(ProcessPaymentRequest processPaymentRequest)
        {
            var result = new ProcessPaymentResult();
            result.NewPaymentStatus = Nop.Core.Domain.Payments.PaymentStatus.Pending;

            return result;
        }

        /// <summary>
        /// Post process payment (used by payment gateways that require redirecting to a third-party URL)
        /// </summary>
        /// <param name="postProcessPaymentRequest">Payment info required for an order processing</param>
        public void PostProcessPayment(PostProcessPaymentRequest postProcessPaymentRequest)
        {
            #region process_currency

            var workingCurrency = _workContext.WorkingCurrency;

            if (_platiOnlinePaymentSettings.RON == true && _workContext.WorkingCurrency.CurrencyCode == "RON")
                _workContext.WorkingCurrency = _currencyService.GetCurrencyByCode("RON");
            else
                if (_platiOnlinePaymentSettings.EUR == true && _workContext.WorkingCurrency.CurrencyCode == "EUR")
                _workContext.WorkingCurrency = _currencyService.GetCurrencyByCode("EUR");
            else
                    if (_platiOnlinePaymentSettings.USD == true && _workContext.WorkingCurrency.CurrencyCode == "USD")
                _workContext.WorkingCurrency = _currencyService.GetCurrencyByCode("USD");
            else
                _workContext.WorkingCurrency = _currencyService.GetCurrencyByCode(_platiOnlinePaymentSettings.Curency.ToString());

            #endregion

            Customer customer = _customerService.GetCustomerById(postProcessPaymentRequest.Order.CustomerId);
            
            Address customerBillingAddress = _customerService.GetCustomerBillingAddress(customer);
            Country customersBillingCountry = _countryService.GetCountryById((int)customerBillingAddress.CountryId);
            StateProvince cusomersBillingState = _stateProvinceService.GetStateProvinceById((int)customerBillingAddress.StateProvinceId);

            Address customerShippingAddress = _customerService.GetCustomerShippingAddress(customer);
            Country customersShippingCountry = _countryService.GetCountryById((int)customerShippingAddress.CountryId);
            StateProvince customersShippingState = _stateProvinceService.GetStateProvinceById((int)customerShippingAddress.StateProvinceId);

            IList<OrderItem> orderItems = _orderService.GetOrderItems(postProcessPaymentRequest.Order.Id);

            Po.Po po = new Po.Po();

            #region Merchant settings

            po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
            po.merchant_ivAuth = _platiOnlinePaymentSettings.IvAuth;
            po.merchant_publicKey = _platiOnlinePaymentSettings.Public_Key;
            po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;
            po.merchant_relay_response_f_relay_method = _platiOnlinePaymentSettings.RelayMethod.ToString();
            //po.log_path = @"";

            #endregion

            #region set_authorization_fields

            po.Authorization.f_amount = _currencyService.ConvertFromPrimaryStoreCurrency(postProcessPaymentRequest.Order.OrderTotal, _workContext.WorkingCurrency).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            po.Authorization.f_currency = ObjectIsNullOrEmpty(_workContext.WorkingCurrency.CurrencyCode);
            po.Authorization.f_language = ObjectIsNullOrEmpty(_workContext.WorkingLanguage.UniqueSeoCode);
            po.Authorization.f_order_number = ObjectIsNullOrEmpty(postProcessPaymentRequest.Order.Id);
            po.Authorization.f_test_request = Convert.ToInt16(_platiOnlinePaymentSettings.TestMode).ToString();
            po.Authorization.f_order_string = "Plata comenzii cu id " + po.Authorization.f_order_number + " pe site-ul " + _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL);

            #region card holder info

            po.Authorization.card_holder_info.same_info_as = "0";

            #region contact

            po.Authorization.card_holder_info.contact.f_email = ObjectIsNullOrEmpty(customerBillingAddress.Email);
            po.Authorization.card_holder_info.contact.f_phone = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.card_holder_info.contact.f_mobile_number = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.card_holder_info.contact.f_send_sms = "1";
            po.Authorization.card_holder_info.contact.f_first_name = ObjectIsNullOrEmpty(customerBillingAddress.FirstName);
            po.Authorization.card_holder_info.contact.f_last_name = ObjectIsNullOrEmpty(customerBillingAddress.LastName);

            #endregion

            #region address

            po.Authorization.card_holder_info.address.f_company = ObjectIsNullOrEmpty(customerBillingAddress.Company);
            po.Authorization.card_holder_info.address.f_zip = ObjectIsNullOrEmpty(customerBillingAddress.ZipPostalCode);
            po.Authorization.card_holder_info.address.f_country = customersBillingCountry.Name != null ? ObjectIsNullOrEmpty(customersBillingCountry.Name) : "Romania";
            po.Authorization.card_holder_info.address.f_state = cusomersBillingState != null ? ObjectIsNullOrEmpty(cusomersBillingState.Name) : "-";
            po.Authorization.card_holder_info.address.f_city = ObjectIsNullOrEmpty(customerBillingAddress.City);
            po.Authorization.card_holder_info.address.f_address = ObjectIsNullOrEmpty(customerBillingAddress.Address1);

            #endregion

            #endregion

            #region customer_info

            #region contact
            po.Authorization.customer_info.contact.f_email = ObjectIsNullOrEmpty(customerBillingAddress.Email);
            po.Authorization.customer_info.contact.f_phone = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.customer_info.contact.f_mobile_number = PhoneObjectIsNullOrEmpty(customerBillingAddress.PhoneNumber);
            po.Authorization.customer_info.contact.f_send_sms = "1";
            po.Authorization.customer_info.contact.f_first_name = ObjectIsNullOrEmpty(customerBillingAddress.FirstName);
            po.Authorization.customer_info.contact.f_last_name = ObjectIsNullOrEmpty(customerBillingAddress.LastName);
            #endregion

            #region invoice
            po.Authorization.customer_info.invoice.f_company = ObjectIsNullOrEmpty(customerBillingAddress.Company);
            po.Authorization.customer_info.invoice.f_cui = "-";
            po.Authorization.customer_info.invoice.f_reg_com = "-";
            po.Authorization.customer_info.invoice.f_cnp = "-";
            po.Authorization.customer_info.invoice.f_zip = ObjectIsNullOrEmpty(customerBillingAddress.ZipPostalCode);
            po.Authorization.customer_info.invoice.f_country = customersBillingCountry.Name != null ? ObjectIsNullOrEmpty(customersBillingCountry.Name) : "Romania";
            po.Authorization.customer_info.invoice.f_state = cusomersBillingState != null ? ObjectIsNullOrEmpty(cusomersBillingState.Name) : "-";
            po.Authorization.customer_info.invoice.f_city = ObjectIsNullOrEmpty(customerBillingAddress.City);
            po.Authorization.customer_info.invoice.f_address = ObjectIsNullOrEmpty(customerBillingAddress.Address1);
            #endregion

            #endregion

            #region order_cart

            #region items

            foreach (var orderItem in orderItems)
            {
                var itemPriceExclTax = _currencyService.ConvertFromPrimaryStoreCurrency(orderItem.UnitPriceExclTax, _workContext.WorkingCurrency);
                var itemPriceInclTax = _currencyService.ConvertFromPrimaryStoreCurrency(orderItem.UnitPriceInclTax, _workContext.WorkingCurrency);

                var product = _productService.GetProductById(orderItem.ProductId);

                item item = new item();
                item.prodid = orderItem.ProductId.ToString();
                item.name = ObjectIsNullOrEmpty(product.Name);
                item.description = ObjectIsNullOrEmpty(product.ShortDescription);
                item.qty = ObjectIsNullOrEmpty(orderItem.Quantity);
                item.itemprice = itemPriceExclTax.ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
                item.vat = (orderItem.Quantity * (itemPriceInclTax - itemPriceExclTax)).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
                item.stamp = DateTime.Now.ToString("yyyy-MM-dd");
                item.prodtype_id = orderItem.IsDownloadActivated ? "0" : "1";

                po.Authorization.f_order_cart.item.Add(item);
            }

            #endregion

            #region coupons

            Po.Requests.Authorization.Objects.coupon coupon = new Po.Requests.Authorization.Objects.coupon();

            coupon.key = postProcessPaymentRequest.Order.Id.ToString();
            coupon.value = _currencyService.ConvertFromPrimaryStoreCurrency(postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax, _workContext.WorkingCurrency).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            coupon.percent = "1";
            coupon.workingname = "Discount Code";
            coupon.type = "0";
            coupon.scop = "0";
            coupon.vat = _currencyService.ConvertFromPrimaryStoreCurrency(postProcessPaymentRequest.Order.OrderSubTotalDiscountInclTax - postProcessPaymentRequest.Order.OrderSubTotalDiscountExclTax, _workContext.WorkingCurrency).ToString("F", CultureInfo.CreateSpecificCulture("en-US")); ;


            po.Authorization.f_order_cart.coupon.Add(coupon);


            #endregion

            #region shipping

            po.Authorization.f_order_cart.shipping.name = postProcessPaymentRequest.Order.ShippingMethod;
            po.Authorization.f_order_cart.shipping.price = _currencyService.ConvertFromPrimaryStoreCurrency(postProcessPaymentRequest.Order.OrderShippingExclTax, _workContext.WorkingCurrency).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));
            po.Authorization.f_order_cart.shipping.pimg = "-";
            po.Authorization.f_order_cart.shipping.vat = _currencyService.ConvertFromPrimaryStoreCurrency(postProcessPaymentRequest.Order.OrderShippingInclTax - postProcessPaymentRequest.Order.OrderShippingExclTax, _workContext.WorkingCurrency).ToString("F", CultureInfo.CreateSpecificCulture("en-US"));

            #endregion

            #endregion

            #region shipping info

            po.Authorization.shipping_info.same_info_as = "0";

            #region contact

            po.Authorization.shipping_info.contact.f_email = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Email) : "xxx@xxx.com";
            po.Authorization.shipping_info.contact.f_phone = customerShippingAddress != null ? PhoneObjectIsNullOrEmpty(customerShippingAddress.PhoneNumber) : "0000000000";
            po.Authorization.shipping_info.contact.f_mobile_number = customerShippingAddress != null ? PhoneObjectIsNullOrEmpty(customerShippingAddress.PhoneNumber) : "0000000000";
            po.Authorization.shipping_info.contact.f_send_sms = customerShippingAddress != null ? "1" : "0";
            po.Authorization.shipping_info.contact.f_first_name = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.FirstName) : "-";
            po.Authorization.shipping_info.contact.f_last_name = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.LastName) : "-";

            #endregion

            #region address

            po.Authorization.shipping_info.address.f_company = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Company) : "-";
            po.Authorization.shipping_info.address.f_zip = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.ZipPostalCode) : "-";
            po.Authorization.shipping_info.address.f_country = customersShippingCountry != null && customersShippingCountry.Name != null ? ObjectIsNullOrEmpty(customersShippingCountry.Name) : "Romania";
            po.Authorization.shipping_info.address.f_state = customersShippingState != null && customersShippingState.Name != null ? ObjectIsNullOrEmpty(customersShippingState.Name) : "-";
            po.Authorization.shipping_info.address.f_city = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.City) : "-";
            po.Authorization.shipping_info.address.f_address = customerShippingAddress != null ? ObjectIsNullOrEmpty(customerShippingAddress.Address1) : "-";

            #endregion

            #endregion

            #endregion
            
            try
            {
                #region authorization_request

                po_auth_url_response _po_auth_url_response = po.Authorization.Request<po_auth_url_response>();

                #endregion

                #region process_authorization_response

                if (!po.Authorization.HasError)
                {
                    if (_po_auth_url_response.po_error_code == "0")
                    {
                        _httpContextAccessor.HttpContext.Response.Redirect(_po_auth_url_response.po_redirect_url);

                    }
                    else
                    {
                        var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(_po_auth_url_response.po_error_reason);

                        //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                        if (redirectUrl.Length <= 2048)
                        {
                            _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                            return;
                        }
                    }
                }
                else
                {
                    var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(po.Authorization.GetError().Error);

                    //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                    if (redirectUrl.Length <= 2048)
                    {
                        _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                        return;
                    }
                }

                #endregion
            }
            catch (Exception e)
            {
                var redirectUrl = "../PaymentPlatiOnline/CheckoutCompleted?orderId=" + postProcessPaymentRequest.Order.Id + "&error=" + HttpUtility.UrlEncode(e.Message);

                //ensure redirect URL doesn't exceed 2K chars to avoid "too long URL" exceptionsss
                if (redirectUrl.Length <= 2048)
                {
                    _httpContextAccessor.HttpContext.Response.Redirect(redirectUrl);
                    return;
                }
            }
        }

        /// <summary>
        /// Returns a value indicating whether payment method should be hidden during checkout
        /// </summary>
        /// <param name="cart">Shopping cart</param>
        /// <returns>true - hide; false - display.</returns>
        public bool HidePaymentMethod(IList<ShoppingCartItem> cart)
        {
            //you can put any logic here
            //for example, hide this payment method if all products in the cart are downloadable
            //or hide this payment method if current customer is from certain country
            return false;
        }

        /// <summary>
        /// Captures payment
        /// </summary>
        /// <param name="capturePaymentRequest">Capture payment request</param>
        /// <returns>Capture payment result</returns>
        public CapturePaymentResult Capture(CapturePaymentRequest capturePaymentRequest)
        {
            return new CapturePaymentResult { Errors = new[] { "Capture method not supported" } };
        }

        /// <summary>
        /// Refunds a payment
        /// </summary>
        /// <param name="refundPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public RefundPaymentResult Refund(RefundPaymentRequest refundPaymentRequest)
        {
            return new RefundPaymentResult { Errors = new[] { "Refund method not supported" } };
        }

        /// <summary>
        /// Voids a payment
        /// </summary>
        /// <param name="voidPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public VoidPaymentResult Void(VoidPaymentRequest voidPaymentRequest)
        {
            var result = new VoidPaymentResult();

            try
            {

                Po.Po po = new Po.Po();

                #region Merchant settings

                po.Void.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
                po.Void.merchant_ivAuth = _platiOnlinePaymentSettings.IvAuth;
                po.Void.merchant_publicKey = _platiOnlinePaymentSettings.Public_Key;

                #endregion
                
                #region set_void_fields

                po.Void.f_order_number = ObjectIsNullOrEmpty(voidPaymentRequest.Order.Id);

                #endregion

                #region void_request

                po_void_response _po_void_response = po.Void.Request<po_void_response>();

                #endregion

                #region process_void_response

                if (!po.Void.HasError)
                {
                    if (_po_void_response.po_error_code == "0")
                    {
                        switch (_po_void_response.x_response_code)
                        {
                            case "7":

                                voidPaymentRequest.Order.PaymentStatusId = (int)PaymentStatus.Voided;
                                result.NewPaymentStatus = PaymentStatus.Voided;

                                //order note
                                _orderService.InsertOrderNote(new OrderNote
                                {
                                    OrderId = voidPaymentRequest.Order.Id,
                                    Note = "Order successfully voided!",
                                    DisplayToCustomer = false,
                                    CreatedOnUtc = DateTime.UtcNow
                                });

                                break;
                            case "10":
                                throw new Exception("<h2>Errors occured, transaction NOT VOIDED</h2>");
                        }
                    }
                    else
                    {
                        throw new Exception("<h2>" + _po_void_response.po_error_reason + "</h2>");
                    }
                }
                else
                {
                    throw new Exception(po.Void.GetError().Error);
                }

                #endregion
            }
            catch (Exception e)
            {
                #region OrderNote

                //order note
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = voidPaymentRequest.Order.Id,
                    Note = e.Message,
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                #endregion

                //if there are not the specific errors add exception message
                if (result.Success)
                    result.AddError(e.InnerException != null ? e.InnerException.Message : e.Message);
            }

            return result;
        }

        /// <summary>
        /// Process recurring payment
        /// </summary>
        /// <param name="processPaymentRequest">Payment info required for an order processing</param>
        /// <returns>Process payment result</returns>
        public ProcessPaymentResult ProcessRecurringPayment(ProcessPaymentRequest processPaymentRequest)
        {
            return new ProcessPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Cancels a recurring payment
        /// </summary>
        /// <param name="cancelPaymentRequest">Request</param>
        /// <returns>Result</returns>
        public CancelRecurringPaymentResult CancelRecurringPayment(CancelRecurringPaymentRequest cancelPaymentRequest)
        {
            return new CancelRecurringPaymentResult { Errors = new[] { "Recurring payment not supported" } };
        }

        /// <summary>
        /// Gets a value indicating whether customers can complete a payment after order is placed but not completed (for redirection payment methods)
        /// </summary>
        /// <param name="order">Order</param>
        /// <returns>Result</returns>
        public bool CanRePostProcessPayment(Order order)
        {
            if (order == null)
                throw new ArgumentNullException(nameof(order));

            //let's ensure that at least 5 seconds passed after order is placed
            //P.S. there's no any particular reason for that. we just do it
            if ((DateTime.UtcNow - order.CreatedOnUtc).TotalSeconds < 5)
                return false;

            return true;
        }

        /// <summary>
        /// Validate payment form
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>List of validating errors</returns>
        public IList<string> ValidatePaymentForm(IFormCollection form)
        {
            return new List<string>();
        }

        /// <summary>
        /// Get payment information
        /// </summary>
        /// <param name="form">The parsed form values</param>
        /// <returns>Payment info holder</returns>
        public ProcessPaymentRequest GetPaymentInfo(IFormCollection form)
        {
            return new ProcessPaymentRequest();
        }

        /// <summary>
        /// Gets a configuration page URL
        /// </summary>
        public override string GetConfigurationPageUrl()
        {
            return $"{_webHelper.GetStoreLocation()}Admin/PaymentPlatiOnline/Configure";
        }

        /// <summary>
        /// Gets a name of a view component for displaying plugin in public store ("payment info" checkout step)
        /// </summary>
        /// <returns>View component name</returns>
        public string GetPublicViewComponentName()
        {
            return null;// "Payment.PlatiOnline";
        }

        /// <summary>
        /// Install the plugin
        /// </summary>
        public override void Install()
        {
            //settings
            var settings = new PlatiOnlinePaymentSettings()
            {
                TransactMode = TransactMode.Pending
            };
            _settingService.SaveSetting(settings);

            //locales
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.MerchantId", "Plati Online Merchant Id");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.MerchantId.Hint", "Specify merchant id.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PublicKey", "Mercahnt PublicKey");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PublicKey.Hint", "Specify merchant public key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PrivateKey", "Merchant PrivateKey");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PrivateKey.Hint", "Specify merchant private key.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvAuth", "Merchant IvAuth");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvAuth.Hint", "Specify merchant IvAuth.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvItsn", "Merchant IvItsn");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvItsn.Hint", "Specify merchant IvItsn.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactMode", "After checkout mark payment as");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactMode.Hint", "Specify transaction mode.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RON", "Accepted RON ");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RON.Hint", "Specify accepted RON currency.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.EUR", "Accepted EUR ");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.EUR.Hint", "Specify accepted EUR currency.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.USD", "Accepted USD ");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.USD.Hint", "Specify accepted USD currency.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OtherCurrency", "Other currency ");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OtherCurrency.Hint", "Specify the currency to replace the other unsupported currencies.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL", "Relay response URL");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL.Hint", "Specify the URL address to which the PO server will send the response for the transactions made by your clients");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RelayMethod", "Relay method ");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RelayMethod.Hint", "Specify the method.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TestMode", "Test mode");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TestMode.Hint", "Specify test mode.");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.SSL", "Use SSL");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.SSL.Hint", "Specify use SSL.");

            //used in PaymentInfo.cshtml
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RedirectionTip", "You will be redirected to PlatiOnline site to complete the payment.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RedirectionTip", "Veti fi redirectionat catre site-ul PlatiOnline pentru a finaliza plata.", "ro-RO");

            //used in CheckoutCompleted.cshtml
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.WeAreSorry", "We are sorry", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.WeAreSorry", "Ne pare rau", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs", "Your transaction is", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs", "Tranzactia este", "ro-RO");
            
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview", "Transaction under security review", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview", "Tranzactia necesita verificari suplimentare", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo", "This transaction is not yet approved, it is under review for security reasons. You will be informed of the outcome of this verification within 1-48 business hours of the date and time of this notification.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo", "Tranzactia nu este inca aprobata si necesita verificari suplimentare. Vei fi anuntat de rezultatul acestor verificari in termen de 1-48 ore.", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined", "Your order has been declined", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined", "Comanda dumneavoastra a fost refuzata", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo", "<p>The transaction was rejected for one of the following reasons: </p><p>  - card information has been incorrectly entered </p><p>  - there are insufficient funds in your account </p><p>  - your card issuing bank rejected the transaction due to security restrictions on the card or card type is not supported</p><p>  - the transaction network is currently unavailable </p><p> We recommend you resubmit the transaction in a few moments, after performing the necessary changes. </p>", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo", "<p>Tranzactia a fost refuzata din unul din motivele urmatoare: </p><p>  - datele de card au fost introduse incorect </p><p>  - nu aveti fonduri suficiente </p><p>  - banca dvs. emitenta a refuzat tranzactia datorita unor restrictii de securitate sau tipul de card nu este acceptat </p><p>  - reteaua de procesare nu este disponibila in acest moment </p><p> Iti recomandam sa reincerci efectuarea platii in cateva momenete, dupa ce efectuezi schimbarile necesare. </p>", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionError", "Transaction error", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionError", "A aparaut o eroare in procesare tranzactie", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo", "Please don't resubmit your transaction. Please contact customersupport.", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo", "Va rog sa nu reincercati inainte de a contacta departamentul de releatii cu clientii", "ro-RO");
            
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Authorized", "Authorized", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Authorized", "Autorizata", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OnHold", "OnHold", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OnHold", "In asteptare", "ro-RO");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Declined", "Declined", "en-US");
            _localizationService.AddOrUpdatePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Declined", "Refuzata", "ro-RO");

            base.Install();
        }

        /// <summary>
        /// Uninstall the plugin
        /// </summary>
        public override void Uninstall()
        {
            //settings
            _settingService.DeleteSetting<PlatiOnlinePaymentSettings>();

            //locales
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.MerchantId");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.MerchantId.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PublicKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PublicKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PrivateKey");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.PrivateKey.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvAuth");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvAuth.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvItsn");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.IvItsn.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactMode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactMode.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RON");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RON.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.EUR");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.EUR.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.USD");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.USD.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OtherCurrency");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OtherCurrency.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Relay_Response_URL.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RelayMethod");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RelayMethod.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TestMode");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TestMode.Hint");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.SSL");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.SSL.Hint");

            //used in PaymentInfo.cshtml
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RedirectionTip");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.RedirectionTip");

            //used in CheckoutCompleted.cshtml
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.WeAreSorry");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourTransactionIs");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReview");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionSecurityReviewInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.YourOrderHasBeenDeclined");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionDeclinedInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionError");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.TransactionErrorInfo");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Authorized");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.OnHold");
            _localizationService.DeletePluginLocaleResource("Plugins.Payments.PlatiOnline.Fields.Declined");

            base.Uninstall();
        }

        public decimal GetAdditionalHandlingFee(IList<ShoppingCartItem> cart)
        {
            return 0;
        }

        #endregion

        #region Properties

        /// <summary>
        /// Gets a value indicating whether capture is supported
        /// </summary>
        public bool SupportCapture
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether partial refund is supported
        /// </summary>
        public bool SupportPartiallyRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether refund is supported
        /// </summary>
        public bool SupportRefund
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a value indicating whether void is supported
        /// </summary>
        public bool SupportVoid
        {
            get { return false; }
        }

        /// <summary>
        /// Gets a recurring payment type of payment method
        /// </summary>
        public RecurringPaymentType RecurringPaymentType
        {
            get { return RecurringPaymentType.NotSupported; }
        }

        /// <summary>
        /// Gets a payment method type
        /// </summary>
        public PaymentMethodType PaymentMethodType
        {
            get { return PaymentMethodType.Redirection; }
        }

        /// <summary>
        /// Gets a value indicating whether we should display a payment information page for this plugin
        /// </summary>
        public bool SkipPaymentInfo
        {
            get { return true; }
        }

        /// <summary>
        /// Gets a payment method description that will be displayed on checkout pages in the public store
        /// </summary>
        public string PaymentMethodDescription
        {
            //return description of this payment method to be display on "payment method" checkout step. good practice is to make it localizable
            //for example, for a redirection payment method, description may be like this: "You will be redirected to PlatiOnline site to complete the payment"
            get { return _localizationService.GetResource("Plugins.Payments.PlatiOnline.Fields.RedirectionTip"); }
        }

        #endregion
    }
}