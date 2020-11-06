using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Routing;
using Nop.Web.Framework.Mvc.Routing;

namespace Nop.Plugin.Payments.PlatiOnline
{
	public partial class RouteProvider : IRouteProvider
	{
        public void RegisterRoutes(IEndpointRouteBuilder endpointRouteBuilder)
        {
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PaymentPlatiOnline.Configure", "Plugins/PaymentPlatiOnline/Configure",
                new { controller = "PaymentPlatiOnline", action = "Configure" });
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PaymentPlatiOnline.PaymentInfo", "Plugins/PaymentPlatiOnline/PaymentInfo",
                new { controller = "PaymentPlatiOnline", action = "PaymentInfo" });
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PaymentPlatiOnline.CheckoutCompleted", "Plugins/PaymentPlatiOnline/CheckoutCompleted",
                new { controller = "PaymentPlatiOnline", action = "CheckoutCompleted" });
            endpointRouteBuilder.MapControllerRoute("Plugin.Payments.PaymentPlatiOnline.ITSN", "Plugins/PaymentPlatiOnline/ITSN",
                new { controller = "PaymentPlatiOnline", action = "ITSN" });
		}

        /// <summary>
        /// Gets a priority of route provider
        /// </summary>
        public int Priority => 0;
    }
}
