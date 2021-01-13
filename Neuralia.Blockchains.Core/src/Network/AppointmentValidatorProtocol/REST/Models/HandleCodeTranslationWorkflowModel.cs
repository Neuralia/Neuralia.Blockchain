
#if NET5_0
using Microsoft.AspNetCore.Mvc;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST.Models {
	public class HandleCodeTranslationWorkflowModel : HandleWorkflowModelBase{
		
		[FromForm(Name = "validatorCode")]
		public string ValidatorCode { get; set; }
	}
}
#endif