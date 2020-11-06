using Microsoft.AspNetCore.Mvc;
using Nop.Web.Framework.Components;

namespace Nop.Plugin.Payments.PlatiOnline.Components
{
    [ViewComponent(Name = "PlatiOnline")]
    public class PlatiOnlineViewComponent : NopViewComponent
    {
        public IViewComponentResult Invoke()
        {
            return View("~/Plugins/Payments.PlatiOnline/Views/PaymentInfo.cshtml");
        }
    }
}
