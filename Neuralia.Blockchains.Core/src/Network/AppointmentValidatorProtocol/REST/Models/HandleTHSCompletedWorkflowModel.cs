
#if NET5_0
using Microsoft.AspNetCore.Mvc;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models {
	public class HandleTHSCompletedWorkflowModel: HandleWorkflowModelBase {
		[FromForm(Name = "results")]
		public string Results { get; set; }
	}
}
#endif