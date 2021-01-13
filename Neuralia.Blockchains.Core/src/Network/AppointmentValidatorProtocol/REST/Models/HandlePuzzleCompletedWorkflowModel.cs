
#if NET5_0
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models {
	public class HandlePuzzleCompletedWorkflowModel: HandleWorkflowModelBase {
		
		[FromForm(Name = "resultKeys")]
		public string ResultKeys { get; set; }
		
		[FromForm(Name = "results")]
		public string Results { get; set; }
	}
}
#endif