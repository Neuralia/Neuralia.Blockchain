using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Hash;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.CandidatureMethods.V1 {
	public class SimpleHashCandidatureMethod : CandidatureMethod {
		
		public SafeArrayHandle DetermineCandidacy(BlockElectionDistillate blockElectionDistillate, AccountId miningAccount) {
			return HashingUtils.HashSha512(hasher => hasher.HashTwo(blockElectionDistillate.blockHash, miningAccount.ToLongRepresentation()));
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