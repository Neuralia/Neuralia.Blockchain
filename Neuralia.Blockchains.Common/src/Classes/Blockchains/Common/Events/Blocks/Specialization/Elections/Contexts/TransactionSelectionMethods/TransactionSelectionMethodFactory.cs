using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods.V1;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Common.Classes.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.TransactionSelectionMethods {

	public interface ITransactionSelectionMethodFactory {
		ITransactionSelectionMethod CreateTransactionSelectionMethod(TransactionSelectionMethodType type, long blockId, BlockElectionDistillate blockElectionDistillate, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, BlockchainServiceSet serviceSet);
	}

	public class TransactionSelectionMethodFactory : ITransactionSelectionMethodFactory {

		public virtual ITransactionSelectionMethod CreateTransactionSelectionMethod(TransactionSelectionMethodType type, long blockId, BlockElectionDistillate blockElectionDistillate, BlockChainConfigurations configuration, IChainStateProvider chainStateProvider, IWalletProvider walletProvider, BlockchainServiceSet serviceSets) {

			if(type == TransactionSelectionMethodTypes.Instance.Automatic) {
				// ok, this one is meant to be automatic. we wlil try to find the best method
				//TODO: make this more elaborate. Try to response to the various cues we can use

				type = TransactionSelectionMethodTypes.Instance.Random;
			}

			if(type == TransactionSelectionMethodTypes.Instance.CreationTime) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new CreationTimeTransactionSelectionMethod(blockId, configuration, chainStateProvider, walletProvider, blockElectionDistillate.ElectionContext, configuration.CreationTimeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.TransationTypes) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new TransactionTypeTransactionSelectionMethod(blockId, configuration, chainStateProvider, walletProvider, blockElectionDistillate.ElectionContext, configuration.TransactionTypeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.Size) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new SizeTransactionSelectionMethod(blockId, configuration, chainStateProvider, walletProvider, blockElectionDistillate.ElectionContext, configuration.SizeTransactionSelectionStrategySettings);
			}

			if(type == TransactionSelectionMethodTypes.Instance.Random) {

				// ok, nothing special here, lets just maximize profits by choosing the highest paying transactions
				return new RandomTransactionSelectionMethod(blockId, configuration, chainStateProvider, walletProvider, blockElectionDistillate.ElectionContext, configuration.RandomTransactionSelectionStrategySettings);
			}

			return null;
		}
	}
}