using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol {
	public interface IAppointmentValidatorDelegate {

		void Initialize();
		
		Task<(ValidatorProtocol1.CodeTranslationResponseOperation operation, bool valid)> HandleCodeTranslationWorkflow(ValidatorProtocol1.CodeTranslationRequestOperation operation);

		Task<(ValidatorProtocol1.TriggerSessionResponseOperation operation, bool valid)> HandleTriggerSessionWorkflow(ValidatorProtocol1.TriggerSessionOperation operation);

		Task<(ValidatorProtocol1.PuzzleCompletedResponseOperation operation, bool valid)> HandlePuzzleCompletedWorkflow(ValidatorProtocol1.PuzzleCompletedOperation operation);
		
		Task<(ValidatorProtocol1.THSCompletedResponseOperation operation, bool valid)> HandleTHSCompletedWorkflow(ValidatorProtocol1.THSCompletedOperation operation);
	}
}