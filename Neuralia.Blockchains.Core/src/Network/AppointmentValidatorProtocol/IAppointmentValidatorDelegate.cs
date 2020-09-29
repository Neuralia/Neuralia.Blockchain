using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public interface IAppointmentValidatorDelegate {

		void Initialize();
		
		Task<ValidatorProtocol1.CodeTranslationResponseOperation> HandleCodeTranslationWorkflow(ValidatorProtocol1.CodeTranslationRequestOperation operation);

		Task<ValidatorProtocol1.TriggerSessionResponseOperation> HandleTriggerSessionWorkflow(ValidatorProtocol1.TriggerSessionOperation operation);

		Task<ValidatorProtocol1.CompleteSessionResponseOperation> HandleCompleteSessionWorkflow(ValidatorProtocol1.CompleteSessionOperation operation);
	}
}