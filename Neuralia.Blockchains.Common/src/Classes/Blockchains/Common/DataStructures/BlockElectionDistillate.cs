using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Compression;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Newtonsoft.Json;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {

	public abstract class BlockElectionDistillate {
		public IByteArray blockHash;
		public string blockHash64;

		public List<string> BlockTransactionIds;

		public ComponentVersion<BlockType> blockType;
		public string blockTypeString;
		public int blockxxHash;
		public long currentBlockId;

		public string DehydratedElectionContext;

		[JsonIgnore] public IElectionContext electionContext;

		public List<FinalElectionResultDistillate> FinalElectionResults = new List<FinalElectionResultDistillate>();

		public bool HasActiveElection = false;

		public AccountId MiningAccountId;

		public List<IntermediaryElectionContextDistillate> IntermediaryElectionResults = new List<IntermediaryElectionContextDistillate>();

		public bool IsElectionContextLoaded => this.electionContext != null;

		public void RehydrateElectionContext(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if((this.electionContext == null) && !string.IsNullOrWhiteSpace(this.DehydratedElectionContext)) {

				IByteArray compressed = (ByteArray) Convert.FromBase64String(this.DehydratedElectionContext);

				GzipCompression compressor = new GzipCompression();
				IByteArray bytes = compressor.Decompress(compressed);

				IElectionContextRehydrationFactory electionContextRehydrationFactory = rehydrationFactory.CreateBlockComponentsRehydrationFactory();
				this.electionContext = electionContextRehydrationFactory.CreateElectionContext(bytes);

				compressed.Return();
				bytes.Return();
			}

			// if(dehydrateElectionContext) {
			// 	var dehydrator = DataSerializationFactory.CreateDehydrator();
			// 	currentElection.ElectionContext.Dehydrate(dehydrator);
			// 	IByteArray data = dehydrator.ToArray();
			// 	blockElectionContext.DehydratedElectionContext = Compressors.BlockCompressor.Compress(data).ToBase64();
			// 	data.Return();
			// }
		}

		public abstract IntermediaryElectionContextDistillate CreateIntermediateElectionContext();
		public abstract PassiveElectionContextDistillate CreatePassiveElectionContext();
		public abstract FinalElectionResultDistillate CreateFinalElectionResult();
	}

	public abstract class IntermediaryElectionContextDistillate {

		public ElectionQuestionDistillate SimpleQuestion;
		public ElectionQuestionDistillate HardQuestion;
		
		public PassiveElectionContextDistillate PassiveElectionContextDistillate;
	}
	
	public abstract class PassiveElectionContextDistillate {

		public long electionBlockId;
		public List<string> TransactionIds;
	}

	public abstract class FinalElectionResultDistillate {

		public int BlockOffset;
		public string DelegateAccountId;
		public List<string> TransactionIds;
	}

	public abstract class ElectedCandidateResultDistillate {
		public long BlockId;

		public ElectionModes ElectionMode;

		public long? simpleAnswer;
		public long? hardAnswer;

		public ComponentVersion<BlockType> MatureBlockType;
		public ComponentVersion MatureElectionContextVersion;
		public int MaturityBlockHash;
		public long MaturityBlockId;

		public List<string> SelectedTransactionIds;
	}

	public abstract class ElectionQuestionDistillate {
		
	}
	
	public class QuestionTransactionSectionDistillate : ElectionQuestionDistillate{
		public BlockId BlockId { get; set; } 
		public int? TransactionIndex { get; set; }

		public byte SelectedTransactionSection{ get; set; }
		public byte SelectedComponent{ get; set; }
	}
}