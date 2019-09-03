using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {

	public interface ITransactionSelectionMethodFactory {
		ITransactionSelectionMethod CreateTransactionSelectionMethod(TransactionSelectionMethodType type, long blockId, BlockElectionDistillate blockElectionDistillate, IWalletProvider walletProvider, BlockChainConfigurations blockChainConfigurations, BlockchainServiceSet serviceSet);
	}

	public class TransactionSelectionMethodFactory : ITransactionSelectionMethodFactory {

		public virtual ITransactionSelectionMethod CreateTransactionSelectionMethod(TransactionSelectionMethodType type, long blockId, BlockElectionDistillate blockElectionDistillate, IWalletProvider walletProvider, BlockChainConfigurations blockChainConfigurations, BlockchainServiceSet serviceSet) {

			if(type == TransactionSelectionMethodTypes.Instance.Automatic) {
				// ok, this one is meant to be automatic. we wlil try to find the best method
				//TODO: make this more elaborate. Try to response to the various cues we can use

				type = TransactionSelectionMethodTypes.Instance.Random;
			}

			if(type == TransactionSelectionMethodTypes.Instance.CreationTime) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new CreationTimeTransactionSelectionMethod(blockId, walletProvider, blockElectionDistillate.electionContext.MaximumElectedTransactionCount, blockChainConfigurations.CreationTimeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.TransationTypes) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new TransactionTypeTransactionSelectionMethod(blockId, walletProvider, blockElectionDistillate.electionContext.MaximumElectedTransactionCount, blockChainConfigurations.TransactionTypeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.Size) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new SizeTransactionSelectionMethod(blockId, walletProvider, blockElectionDistillate.electionContext.MaximumElectedTransactionCount, blockChainConfigurations.SizeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.Random) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new RandomTransactionSelectionMethod(blockId, walletProvider, blockElectionDistillate.electionContext.MaximumElectedTransactionCount, blockChainConfigurations.RandomTransactionSelectionStrategySettings);
			}

			return null;
		}
	}
}