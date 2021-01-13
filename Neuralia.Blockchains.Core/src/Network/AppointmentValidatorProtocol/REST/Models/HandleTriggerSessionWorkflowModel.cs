
#if NET5_0
using Microsoft.AspNetCore.Mvc;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models {
	public class HandleTriggerSessionWorkflowModel: HandleWorkflowModelBase {
		[FromForm(Name = "secretCode")]
		public int SecretCode { get; set; }
	}
}
#endif