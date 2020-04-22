using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Serilog;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1 {
	public class SimpleHashCandidatureMethod : CandidatureMethod {

		public override SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, SafeArrayHandle currentBlockHash, AccountId miningAccount) {
			var candidacy = HashingUtils.HashSha512(hasher => {
			
				// first combine the original context declaring block Id with the mature block hash.
				using var blockIntermediaryHash = HashingUtils.HashSha256(hasher256 => hasher256.HashTwo(currentBlockHash, matureBlockElectionDistillate.electionBockId));
				
				// and now we hash this together with the account Id
				return hasher.HashTwo(blockIntermediaryHash, miningAccount.ToLongRepresentation());
			});
			
			Log.Verbose($"Hashing block hash {currentBlockHash.Entry.ToBase58()} with original block Id {matureBlockElectionDistillate.electionBockId} and account Id {miningAccount}. Result: {candidacy.Entry.ToBase58()}");

			return candidacy;
		}

		public override SafeArrayHandle DetermineCandidacy(BlockElectionDistillate matureBlockElectionDistillate, BlockElectionDistillate currentBlockElectionDistillate, AccountId miningAccount) {

			return this.DetermineCandidacy(matureBlockElectionDistillate, currentBlockElectionDistillate.blockHash, miningAccount);
		}

		protected override ComponentVersion<CandidatureMethodType> SetIdentity() {
			return (CandidatureMethodTypes.Instance.SimpleHash, 1, 0);

		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			return nodeList;
		}
		
		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetProperty("Name", "SimpleHash");
		}
	}
}