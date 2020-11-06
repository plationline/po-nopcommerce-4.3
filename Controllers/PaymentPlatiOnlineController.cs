using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Xml;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nop.Core;
using Nop.Core.Domain.Orders;
using Nop.Core.Domain.Payments;
using Nop.Plugin.Payments.PlatiOnline;
using Nop.Plugin.Payments.PlatiOnline.Models;
using Nop.Services;
using Nop.Services.Common;
using Nop.Services.Configuration;
using Nop.Services.Localization;
using Nop.Services.Logging;
using Nop.Services.Messages;
using Nop.Services.Orders;
using Nop.Services.Payments;
using Nop.Services.Security;
using Nop.Web.Framework;
using Nop.Web.Framework.Controllers;
using Nop.Web.Framework.Mvc.Filters;
using Po.Requests.Authorization.Objects;
using Po.Requests.Itsn.Objects;
using Po.Requests.Query.Objects;

namespace Nop.Plugin.Payments.PayPalStandard.Controllers
{
    public class PaymentPlatiOnlineController : BasePaymentController
    {
        #region Fields

        private readonly IWorkContext _workContext;
        private readonly ISettingService _settingService;
        private readonly IPaymentService _paymentService;
        private readonly IOrderService _orderService;
        private readonly IOrderProcessingService _orderProcessingService;
        private readonly IPermissionService _permissionService;
        private readonly IGenericAttributeService _genericAttributeService;
        private readonly ILocalizationService _localizationService;
        private readonly IStoreContext _storeContext;
        private readonly ILogger _logger;
        private readonly IWebHelper _webHelper;
        private readonly INotificationService _notificationService;
        private readonly ShoppingCartSettings _shoppingCartSettings;
        private readonly PlatiOnlinePaymentSettings _platiOnlinePaymentSettings;


        #endregion

        #region Ctor

        public PaymentPlatiOnlineController(IWorkContext workContext,
            ISettingService settingService,
            IPaymentService paymentService,
            IOrderService orderService,
            IOrderProcessingService orderProcessingService,
            IPermissionService permissionService,
            IGenericAttributeService genericAttributeService,
            ILocalizationService localizationService,
            IStoreContext storeContext,
            ILogger logger,
            IWebHelper webHelper,
            INotificationService notificationService,
            ShoppingCartSettings shoppingCartSettings,
            PlatiOnlinePaymentSettings platiOnlinePaymentSettings)
        {
            this._workContext = workContext;
            this._settingService = settingService;
            this._paymentService = paymentService;
            this._orderService = orderService;
            this._orderProcessingService = orderProcessingService;
            this._permissionService = permissionService;
            this._genericAttributeService = genericAttributeService;
            this._localizationService = localizationService;
            this._storeContext = storeContext;
            this._logger = logger;
            this._webHelper = webHelper;
            this._notificationService = notificationService;
            this._shoppingCartSettings = shoppingCartSettings;
            this._platiOnlinePaymentSettings = platiOnlinePaymentSettings;
        }

        #endregion

        #region Methods

        [HttpGet]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure()
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var platiOnlinePaymentSettings = _settingService.LoadSetting<PlatiOnlinePaymentSettings>(storeScope);

            var model = new ConfigurationModel()
            {

                Merchant_Id = platiOnlinePaymentSettings.Merchant_Id,
                Public_Key = platiOnlinePaymentSettings.Public_Key,
                Private_Key = platiOnlinePaymentSettings.Private_Key,
                IvItsn = platiOnlinePaymentSettings.IvItsn,
                IvAuth = platiOnlinePaymentSettings.IvAuth,
                TransactModeId = Convert.ToInt32(platiOnlinePaymentSettings.TransactMode),
                TransactModeValues = platiOnlinePaymentSettings.TransactMode.ToSelectList(),
                RON = platiOnlinePaymentSettings.RON,
                EUR = platiOnlinePaymentSettings.EUR,
                USD = platiOnlinePaymentSettings.USD,
                OtherCurrencyId = Convert.ToInt32(_platiOnlinePaymentSettings.Curency),
                OtherCurrency = platiOnlinePaymentSettings.Curency.ToSelectList(),
                Relay_Response_URL = platiOnlinePaymentSettings.Relay_Response_URL,
                RelayMethodId = Convert.ToInt32(platiOnlinePaymentSettings.RelayMethod),
                RelayMethod = platiOnlinePaymentSettings.RelayMethod.ToSelectList(),
                TestMode = platiOnlinePaymentSettings.TestMode,
                SSL = platiOnlinePaymentSettings.SSL,
                ActiveStoreScopeConfiguration = storeScope
            };

            if (storeScope > 0)
            {
                model.Merchant_Id_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.Merchant_Id, storeScope);
                model.Public_Key_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.Public_Key, storeScope);
                model.Private_Key_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.Private_Key, storeScope);
                model.IvItsn_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.IvItsn, storeScope);
                model.IvAuth_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.IvAuth, storeScope);
                model.TransactModeId_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.TransactMode, storeScope);
                model.RON_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.RON, storeScope);
                model.EUR_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.EUR, storeScope);
                model.USD_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.USD, storeScope);
                model.OtherCurrencyId_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.Curency, storeScope);
                model.Relay_Response_URL_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.Relay_Response_URL, storeScope);
                model.RelayMethodId_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.RelayMethod, storeScope);
                model.TestMode_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.TestMode, storeScope);
                model.SSL_OverrideForStore = _settingService.SettingExists(platiOnlinePaymentSettings, x => x.SSL, storeScope);
            }
          

            return View("~/Plugins/Payments.PlatiOnline/Views/Configure.cshtml", model);
        }

        [HttpPost]
        [AuthorizeAdmin]
        [Area(AreaNames.Admin)]
        public IActionResult Configure(ConfigurationModel model)
        {
            if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();

            if (!ModelState.IsValid)
                return Configure(); 

            //load settings for a chosen store scope
            var storeScope = _storeContext.ActiveStoreScopeConfiguration;
            var platiOnlinePaymentSettings = _settingService.LoadSetting<PlatiOnlinePaymentSettings>(storeScope);

            //save settings
            platiOnlinePaymentSettings.Merchant_Id = model.Merchant_Id;
            platiOnlinePaymentSettings.Public_Key = model.Public_Key;
            platiOnlinePaymentSettings.Private_Key = model.Private_Key;
            platiOnlinePaymentSettings.TransactMode = (TransactMode)model.TransactModeId;
            platiOnlinePaymentSettings.RON = model.RON;
            platiOnlinePaymentSettings.EUR = model.EUR;
            platiOnlinePaymentSettings.USD = model.USD;
            platiOnlinePaymentSettings.Curency = (Currency)model.OtherCurrencyId;
            platiOnlinePaymentSettings.Relay_Response_URL = model.Relay_Response_URL;
            platiOnlinePaymentSettings.RelayMethod = (RelayMethod)model.RelayMethodId;
            platiOnlinePaymentSettings.IvAuth = model.IvAuth;
            platiOnlinePaymentSettings.IvItsn = model.IvItsn;
            platiOnlinePaymentSettings.TestMode = model.TestMode;
            platiOnlinePaymentSettings.SSL = model.SSL;

            /* We do not clear cache after each setting update.
             * This behavior can increase performance because cached settings will not be cleared 
             * and loaded from database after each update */
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.Merchant_Id, model.Merchant_Id_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.Public_Key, model.Public_Key_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.Private_Key, model.Private_Key_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.IvItsn, model.IvItsn_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.IvAuth, model.IvAuth_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.TransactMode, model.TransactModeId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.RON, model.RON_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.EUR, model.EUR_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.USD, model.USD_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.Curency, model.OtherCurrencyId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.Relay_Response_URL, model.Relay_Response_URL_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.RelayMethod, model.RelayMethodId_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.TestMode, model.TestMode_OverrideForStore, storeScope, false);
            _settingService.SaveSettingOverridablePerStore(platiOnlinePaymentSettings, x => x.SSL, model.SSL_OverrideForStore, storeScope, false);
            //now clear settings cache
            _settingService.ClearCache();

            //now clear settings cache
            _settingService.ClearCache();

            _notificationService.SuccessNotification(_localizationService.GetResource("Admin.Plugins.Saved"));

            return Configure();
        }

        public IActionResult CancelOrder()
        {
            var order = _orderService.SearchOrders(storeId: _storeContext.CurrentStore.Id,
                customerId: _workContext.CurrentCustomer.Id, pageSize: 1).FirstOrDefault();
            if (order != null)
                return RedirectToRoute("OrderDetails", new { orderId = order.Id });

            return RedirectToRoute("HomePage");
        }

        //[HttpPost]
        public IActionResult CheckoutCompleted()
        {
            /*if (!_permissionService.Authorize(StandardPermissionProvider.ManagePaymentMethods))
                return AccessDeniedView();*/

            Int64 orderId = Convert.ToInt64(_webHelper.QueryString<string>("orderId"));
            string error = _webHelper.QueryString<string>("error");

            CheckoutCompletedModel model = new CheckoutCompletedModel();

            OrderNote orderNote = new OrderNote();
            orderNote.CreatedOnUtc = DateTime.Now;
            orderNote.DisplayToCustomer = false;

            Order order = new Order();
            String note = "";

            if (error == null)
            {
                Po.Po po = new Po.Po();

                #region Merchant settings

                po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
                po.merchant_ivItsn = _platiOnlinePaymentSettings.IvItsn;
                po.merchant_privateKey = _platiOnlinePaymentSettings.Private_Key;
                po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;
                //po.log_path = @"";

                #endregion

                try
                {

                    switch (_platiOnlinePaymentSettings.RelayMethod.ToString())
                    {
                        #region 0.PTOR
                        case "PTOR":  //POST using JavaScript

                            string f_relay_message0 = Request.Form["f_relay_message"];
                            string f_crypt_message0 = Request.Form["f_crypt_message"];

                            po_auth_response response0 = (po_auth_response)po.Authorization.Response(f_relay_message0, f_crypt_message0);

                            order = _orderService.GetOrderById(Convert.ToInt32(response0.f_order_number));

                            //order_status
                            switch (response0.x_response_code)
                            {
                                case "2": //authorized
                                    /* order0.PaymentStatus = PaymentStatus.Authorized;		- code changed - */
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    /* order0.OrderStatus = OrderStatus.Authorized;				- code changed - */
                                    order.OrderStatusId = (int)OrderStatus.Authorized;
                                    break;
                                case "8": //declined
                                    /* order0.PaymentStatus = PaymentStatus.Declined;			- code changed - */
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    /* order0.OrderStatus = OrderStatus.PaymentDeclined;		- code changed - */
                                    order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                    break;
                                case "10": // error
                                    /* order0.PaymentStatus = PaymentStatus.Error;				- code changed - */
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    break;
                                case "13": //on hold
                                    /* order0.PaymentStatus = PaymentStatus.OnHold;				- code changed - */
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    /* order0.OrderStatus = OrderStatus.OnHold;					- code changed - */
                                    order.OrderStatusId = (int)OrderStatus.OnHold;
                                    break;
                            }

                            //order_note
                            if (response0.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response0.x_response_reason_text;
                            }

                            //order note
                            _orderService.InsertOrderNote(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            //model
                            model.order_number = response0.f_order_number;
                            /* model.order_status = order0.OrderStatus.ToString();			- code changed - */
                            model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                            /* model.payment_status = order0.PaymentStatus.ToString();		- code changed - */
                            model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                            model.response_reason_text = response0.x_response_reason_text;

                            return View("Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);

                        #endregion

                        #region 1.POST_S2S_PO_PAGE
                        case "POST_S2S_PO_PAGE": //POST server PO to merchant server, customer get the PO template
                            
                            string f_relay_message1 = Request.Form["f_relay_message"];
                            string f_crypt_message1 = Request.Form["f_crypt_message"];

                            po_auth_response response1 = (po_auth_response)po.Authorization.Response(f_relay_message1, f_crypt_message1);

                            order = _orderService.GetOrderById(Convert.ToInt32(response1.f_order_number));

                            bool raspuns_procesat1 = true;

                            switch (response1.x_response_code)
                            {
                                case "2":
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    order.OrderStatusId = (int)OrderStatus.Authorized;
                                    break;
                                case "8":
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                    break;
                                case "10":
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    break;
                                case "13":
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    order.OrderStatusId = (int)OrderStatus.OnHold;
                                    break;
                                default:
                                    raspuns_procesat1 = false;
                                    break;
                            }

                            //order_note
                            if (response1.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response1.x_response_reason_text;
                            }

                            //order note
                            _orderService.InsertOrderNote(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for POST_S2S_PO_PAGE
                            // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                            // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing
                            if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                            {
                                ///_webHelper.AppendHeader("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");

                                if (raspuns_procesat1)
                                {
                                   // Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                }
                                else
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                }
                            }
                            return new EmptyResult();
                        #endregion

                        #region 2.POST_S2S_MT_PAGE
                        case "POST_S2S_MT_PAGE": //POST server PO to merchant server, customer get the Merchant template

                            string f_relay_message2 = Request.Form["f_relay_message"];
                            string f_crypt_message2 = Request.Form["f_crypt_message"];

                            po_auth_response response2 = (po_auth_response)po.Authorization.Response(f_relay_message2, f_crypt_message2);

                            order = _orderService.GetOrderById(Convert.ToInt32(response2.f_order_number));

                            bool raspuns_procesat2 = true;

                            switch (response2.x_response_code)
                            {
                                case "2": //authorized
                                    order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                    order.OrderStatusId = (int)OrderStatus.Authorized;
                                    break;
                                case "13": //	on hold
                                    order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                    order.OrderStatusId = (int)OrderStatus.OnHold;
                                    _orderService.UpdateOrder(order);
                                    break;
                                case "8":   //	declined
                                    order.PaymentStatusId = (int)PaymentStatus.Declined;
                                    order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                    _orderService.UpdateOrder(order);
                                    break;
                                case "10": //	error
                                    order.PaymentStatusId = (int)PaymentStatus.Error;
                                    _orderService.UpdateOrder(order);
                                    break;
                                default:
                                    raspuns_procesat2 = false;
                                    break;
                            }

                            //order_note
                            if (response2.x_response_code != "10")
                            {
                                note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                            }
                            else
                            {
                                note = "An error was encountered in PlatiOnline authorization process: " + response2.x_response_reason_text;
                            }

                            //order note
                            _orderService.InsertOrderNote(new OrderNote
                            {
                                OrderId = order.Id,
                                Note = note,
                                DisplayToCustomer = false,
                                CreatedOnUtc = DateTime.UtcNow
                            });

                            // instead of sending a <h2> tag using echo, you can send an HTML code, based on X_RESPONSE_CODE
                            // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for POST_S2S_MT_PAGE
                            // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                            // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing
                            if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                            {
                                //Response.AppendHeader("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");

                                if (raspuns_procesat2)
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                }
                                else
                                {
                                    //Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                }
                            }

                            //model
                            model.order_number = response2.f_order_number;
                            model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                            model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                            model.response_reason_text = response2.x_response_reason_text;
                            
                            return View("Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);

                        #endregion

                        #region 3.SOAP_PO_PAGE
                        case "SOAP_PO_PAGE": //POST SOAP server PO to merchant server, customer get the PO template

                            throw new NotImplementedException();
                           /*                             string po_soap_response3 = "";

                            * using (StreamReader rd = new StreamReader(Request.InputStream))
                            {
                                po_soap_response3 = rd.ReadToEnd();
                            }
                            if (po_soap_response3.Length > 0)
                            {
                                po_relay_response _po_relay_response3 = po.Authorization.SoapResponse(po_soap_response3);

                                po_auth_response response3 = (po_auth_response)po.Authorization.Response(_po_relay_response3.f_relay_message, _po_relay_response3.f_crypt_message);

                                Order order = _orderService.GetOrderById(Convert.ToInt32(response3.f_order_number));
                                orderNote.Order = order;
                                orderNote.OrderId = order.Id;

                                bool raspuns_procesat3 = true;

                                switch (response3.x_response_code)
                                {
                                    case "2": //authorized
                                        order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                        order.OrderStatusId = (int)OrderStatus.Authorized;
                                        break;
                                    case "13": //	on hold
                                        order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                        order.OrderStatusId = (int)OrderStatus.OnHold;
                                        break;
                                    case "8":   //	declined
                                        order.PaymentStatusId = (int)PaymentStatus.Declined;
                                        order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                        break;
                                    case "10": // error
                                        order.PaymentStatusId = (int)PaymentStatus.Error;
                                        break;
                                    default:
                                        raspuns_procesat3 = false;
                                        break;
                                }

                                //order_note
                                if (response3.x_response_code != "10")
                                {
                                    orderNote.Note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                                }
                                else
                                {
                                    orderNote.Note = "An error was encountered in PlatiOnline authorization process: " + response3.x_response_reason_text;
                                }

                                order.OrderNotes.Add(orderNote);
                                _orderService.UpdateOrder(order);

                                // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for SOAP_PO_PAGE
                                // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                                // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing
                                if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                                {
                                    Response.AppendHeader("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");

                                    if (raspuns_procesat3)
                                    {
                                        Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                    }
                                    else
                                    {
                                        Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                    }
                                }
                            }
                            return new EmptyResult();*/
                        #endregion

                        #region 4.SOAP_MT_PAGE
                        case "SOAP_MT_PAGE"://POST SOAP server PO to merchant server, customer get the Merchant template
                            throw new NotImplementedException();

                            /*string po_soap_response4 = "";
                            using (StreamReader rd = new StreamReader(HttpContext.Current.Request.InputStream))
                            {
                                po_soap_response4 = rd.ReadToEnd();
                            }
                            if (po_soap_response4.Length > 0)
                            {
                                po_relay_response _po_relay_response4 = po.Authorization.SoapResponse(po_soap_response4);
                                po_auth_response response4 = (po_auth_response)po.Authorization.Response(_po_relay_response4.f_relay_message, _po_relay_response4.f_crypt_message);

                                Order order = _orderService.GetOrderById(Convert.ToInt32(response4.f_order_number));
                                orderNote.Order = order;
                                orderNote.OrderId = order.Id;

                                bool raspuns_procesat4 = true;

                                switch (response4.x_response_code)
                                {
                                    case "2": //authorized
                                        order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                        order.OrderStatusId = (int)OrderStatus.Authorized;
                                        break;
                                    case "13": // on hold
                                        order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                        order.OrderStatusId = (int)OrderStatus.OnHold;
                                        break;
                                    case "8": // declined
                                        order.PaymentStatusId = (int)PaymentStatus.Declined;
                                        order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                        break;
                                    case "10": // error
                                        order.PaymentStatusId = (int)PaymentStatus.Error;
                                        break;
                                    default:
                                        raspuns_procesat4 = false;
                                        break;
                                }

                                //order_note
                                if (response4.x_response_code != "10")
                                {
                                    orderNote.Note = "PlatiOnline transaction status : " + order.PaymentStatus.ToString();
                                }
                                else
                                {
                                    orderNote.Note = "An error was encountered in PlatiOnline authorization process: " + response4.x_response_reason_text;
                                }

                                order.OrderNotes.Add(orderNote);
                                _orderService.UpdateOrder(order);

                                // instead of sending a <h2> tag using echo, you can send an HTML code, based on X_RESPONSE_CODE

                                // this works for f_relay_handshake = 1 in authorization request. I want HANDSHAKE between merchant server and PO server for POST_S2S_MT_PAGE
                                // if the response was processed, I send TRUE to PO server for PO_Transaction_Response_Processing
                                // if the response was not processed and I want the PO server to resend the transaction status, I send RETRY to PO server for PO_Transaction_Response_Processing

                                if (po.Authorization.transaction_relay_response.f_relay_handshake == "1")
                                {
                                    Response.AppendHeader("User-Agent", "Mozilla/5.0 (Plati Online Relay Response Service)");

                                    if (raspuns_procesat4)
                                    {
                                        Response.AppendHeader("PO_Transaction_Response_Processing", "true");
                                    }
                                    else
                                    {
                                        Response.AppendHeader("PO_Transaction_Response_Processing", "retry");
                                    }
                                }

                                //model
                                model.order_number = response4.f_order_number;
                                model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                                model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                                model.response_reason_text = response4.x_response_reason_text;
                            }

                            return View("~/Plugins/Payments.PlatiOnline/Views/PaymentPlatiOnline/CheckoutCompleted.cshtml", model);*/

                            #endregion

                        #region 5.GET
                        //DISABLED imposible to use Plati Online 5.x
                        #endregion
                    }
                }
                catch (Exception e)
                {
                    model.order_number = orderId.ToString();
                    model.order_status = OrderStatus.Pending.ToString();
                    model.payment_status = PaymentStatus.Error.ToString();
                    model.response_reason_text = "An error was encountered in PlatiOnline authorization process: " + HttpUtility.UrlDecode(e.Message);
                }
            }
            else
            {
                //order
                order = _orderService.GetOrderById(Convert.ToInt32(orderId));
                order.PaymentStatusId = (int)PaymentStatus.Error;
                order.OrderStatusId = (int)OrderStatus.Pending;
                _orderService.UpdateOrder(order);

                //order note
                _orderService.InsertOrderNote(new OrderNote
                {
                    OrderId = order.Id,
                    Note = "An error was encountered in PlatiOnline authorization process: " + HttpUtility.UrlDecode(error),
                    DisplayToCustomer = false,
                    CreatedOnUtc = DateTime.UtcNow
                });

                model.order_number = orderId.ToString();
                model.order_status = Enum.GetName(typeof(OrderStatus), order.OrderStatusId);
                model.payment_status = Enum.GetName(typeof(PaymentStatus), order.PaymentStatusId);
                model.response_reason_text = error;
            }

            return View("Plugins/Payments.PlatiOnline/Views/CheckoutCompleted.cshtml", model);
            //return View(model);
        }

        [HttpPost]
        public EmptyResult ITSN()
        {
            Po.Po po = new Po.Po();

            #region Merchant settings

            po.merchant_f_login = _platiOnlinePaymentSettings.Merchant_Id;
            po.merchant_ivAuth = _platiOnlinePaymentSettings.IvAuth;
            po.merchant_ivItsn = _platiOnlinePaymentSettings.IvItsn;
            po.merchant_privateKey = _platiOnlinePaymentSettings.Private_Key;
            po.merchant_publicKey = _platiOnlinePaymentSettings.Public_Key;
            po.merchant_relay_response_f_relay_response_url = _webHelper.GetStoreLocation(_platiOnlinePaymentSettings.SSL) + _platiOnlinePaymentSettings.Relay_Response_URL;

            #endregion

            #region get_itsn_request

            string f_relay_message = Request.Form["f_itsn_message"];
            string f_crypt_message = Request.Form["f_crypt_message"];

            #endregion

            try
            {
                #region process_itsn_request

                po_itsn _po_itsn = (po_itsn)po.Itsn.Response(f_relay_message, f_crypt_message);

                #endregion

                #region set_query_fields(for itsn)

                po.Query.f_order_number = _po_itsn.f_order_number;
                po.Query.x_trans_id = _po_itsn.x_trans_id;

                #endregion

                #region query_request(for itsn)

                po_query_response _po_query_response = po.Query.Request<po_query_response>();

                #endregion

                #region process_query_response(for itsn)

                if (!po.Query.HasError)
                {
                    if (_po_query_response.po_error_code == "0")
                    {
                        #region Update order status

                        var order = _orderService.GetOrderById(Convert.ToInt32(_po_query_response.order.f_order_number));

                        //order note
                        _orderService.InsertOrderNote(new OrderNote
                        {
                            OrderId = order.Id,
                            Note = "[ITSN] Notification: transaction status was changed!",
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        string f_response_code = "1";

                        switch (_po_query_response.order.tranzaction.status_fin1.code)
                        {
                            case "13":
                                order.PaymentStatusId = (int)PaymentStatus.OnHold;
                                order.OrderStatusId = (int)OrderStatus.OnHold;
                                break;
                            case "2":
                                order.PaymentStatusId = (int)PaymentStatus.Authorized;
                                order.OrderStatusId = (int)OrderStatus.Authorized;
                                break;
                            case "8":
                                order.PaymentStatusId = (int)PaymentStatus.Declined;
                                order.OrderStatusId = (int)OrderStatus.PaymentDeclined;
                                break;
                            case "3":
                                order.PaymentStatusId = (int)PaymentStatus.PendingSettle;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "5":
                                switch (_po_query_response.order.tranzaction.status_fin2.code)
                                {
                                    case "1":
                                        order.PaymentStatusId = (int)PaymentStatus.PendingRefund;
                                        order.OrderStatusId = (int)OrderStatus.Pending;
                                        break;
                                    case "2":
                                        order.PaymentStatusId = (int)PaymentStatus.Refunded;
                                        order.OrderStatusId = (int)OrderStatus.PaymentRefunded;
                                        break;
                                    case "3":
                                        order.PaymentStatusId = (int)PaymentStatus.Refused;
                                        order.OrderStatusId = (int)OrderStatus.PaymentRefused;
                                        break;
                                    case "4":
                                        order.PaymentStatusId = (int)PaymentStatus.Settle;
                                        order.OrderStatusId = (int)OrderStatus.PaymentSettled;
                                        break;
                                }
                                break;
                            case "6":
                                order.PaymentStatusId = (int)PaymentStatus.PendingVoid;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "7":
                                order.PaymentStatusId = (int)PaymentStatus.Voided;
                                order.OrderStatusId = (int)OrderStatus.PaymentVoided;
                                break;
                            case "9":
                                order.PaymentStatusId = (int)PaymentStatus.Expired;
                                order.OrderStatusId = (int)OrderStatus.PaymentExpired;
                                break;
                            case "10":
                                order.PaymentStatusId = (int)PaymentStatus.Error;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            case "1":
                                order.PaymentStatusId = (int)PaymentStatus.PendingAuthorization;
                                order.OrderStatusId = (int)OrderStatus.Pending;
                                break;
                            default:
                                f_response_code = "0";
                                break;
                        }

                        //order note
                        _orderService.InsertOrderNote(new OrderNote
                        {
                            OrderId = order.Id,
                            Note = "[ITSN] PlatiOnline transaction status : " + order.PaymentStatus,
                            DisplayToCustomer = false,
                            CreatedOnUtc = DateTime.UtcNow
                        });

                        #endregion

                        #region Send ITSN response

                        XmlDocument doc = po.Itsn.ItsnResponse(f_response_code, _po_query_response.order.tranzaction.x_trans_id);
                        //Response.Body.Write(doc.OuterXml.AsReadOnlySpan());
                       //);
                        
                        #endregion
                    }
                    else//1 - an error occurred parsing the '''Query Request XML_Message''' and PlatiOnline will not process the request;
                    {
                       // Response.Write(_po_query_response.po_error_reason);
                    }
                }
                else
                {
                    //Response.Write(po.Query.GetError().Error);
                }

                #endregion

            }
            catch //(Exception e)
            {
                //Response.Write(e.Message);
            }

            return new EmptyResult();
        }
  
        #endregion
    }
}