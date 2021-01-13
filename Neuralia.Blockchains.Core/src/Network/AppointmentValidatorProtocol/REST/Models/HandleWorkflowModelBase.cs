#if NET5_0
using Microsoft.AspNetCore.Mvc;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models {
	public class HandleWorkflowModelBase {
		[FromForm(Name = "appointment")]
		public long Appointment { get; set; }
		
		[FromForm(Name = "chainId")]
		public ushort ChainId { get; set; }
		
		[FromForm(Name = "index")]
		public int Index { get; set; }
	}
}
#endif