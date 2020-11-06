using Nop.Web.Framework.Models;

namespace Nop.Plugin.Payments.PlatiOnline.Models
{
	public partial class CheckoutCompletedModel : BaseNopModel
	{
		public string response_reason_text { get; set; }
		public string order_number { get; set; }
		public string order_status { get; set; }
		public string payment_status { get; set; }
	}
}