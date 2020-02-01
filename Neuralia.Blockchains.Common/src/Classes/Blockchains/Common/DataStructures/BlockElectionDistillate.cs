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
using Neuralia.Blockchains.Tools.Data.Arrays;
using System.Text.Json;
using System.Text.Json.Serialization;
using Neuralia.Blockchains.Core;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {

	public abstract class BlockElectionDistillate {
		public readonly SafeArrayHandle blockHash = SafeArrayHandle.Create();
		public string blockHashSerialized;

		public readonly List<string> BlockTransactionIds = new List<string>();

		public ComponentVersion<BlockType> blockType;
		public string blockTypeString;
		public int blockxxHash;
		public long currentBlockId;

		public string DehydratedElectionContext;

		[JsonIgnore]
		public IElectionContext ElectionContext { get; set; }

		public readonly List<FinalElectionResultDistillate> FinalElectionResults = new List<FinalElectionResultDistillate>();

		public bool HasActiveElection = false;

		public AccountId MiningAccountId;

		public readonly List<IntermediaryElectionContextDistillate> IntermediaryElectionResults = new List<IntermediaryElectionContextDistillate>();

		public bool IsElectionContextLoaded => this.ElectionContext != null;

		public void RehydrateElectionContext(IBlockchainEventsRehydrationFactory rehydrationFactory) {
			if((this.ElectionContext == null) && !string.IsNullOrWhiteSpace(this.DehydratedElectionContext)) {

				using(SafeArrayHandle compressed = ByteArray.FromBase64(this.DehydratedElectionContext)) {

					BrotliCompression compressor = new BrotliCompression();
					using(SafeArrayHandle bytes = compressor.Decompress(compressed)) {

						IElectionContextRehydrationFactory electionContextRehydrationFactory = rehydrationFactory.CreateBlockComponentsRehydrationFactory();
						this.ElectionContext = electionContextRehydrationFactory.CreateElectionContext(bytes);
					}
				}
			}

			// if(dehydrateElectionContext) {
			// 	var dehydrator = DataSerializationFactory.CreateDehydrator();
			// 	currentElection.ElectionContext.Dehydrate(dehydrator);
			// 	ArrayWrapper data = dehydrator.ToArray();
			// 	blockElectionContext.DehydratedElectionContext = Compressors.BlockCompressor.Compress(data).ToBase64();
			// 	data.Return();
			// }
		}

		public abstract IntermediaryElectionContextDistillate CreateIntermediateElectionContext();
		public abstract PassiveElectionContextDistillate CreatePassiveElectionContext();
		public abstract FinalElectionResultDistillate CreateFinalElectionResult();
	}

	public abstract class IntermediaryElectionContextDistillate {

		public int BlockOffset;
		
		public ElectionBlockQuestionDistillate SecondTierQuestion;
		public ElectionDigestQuestionDistillate DigestQuestion;
		public ElectionBlockQuestionDistillate FirstTierQuestion;
		
		public PassiveElectionContextDistillate PassiveElectionContextDistillate;
	}
	
	public abstract class ElectionContextDistillate {

		public readonly List<string> TransactionIds = new List<string>();
	}
	
	public abstract class PassiveElectionContextDistillate : ElectionContextDistillate {

		public long electionBlockId;
		public Enums.MiningTiers MiningTier = Enums.MiningTiers.ThirdTier;
	}

	public abstract class FinalElectionResultDistillate : ElectionContextDistillate {

		public int BlockOffset;
		public string DelegateAccountId;
	}

	public abstract class ElectedCandidateResultDistillate {
		public long BlockId;

		public ElectionModes ElectionMode;

		public long? secondTierAnswer;
		public long? digestAnswer;
		public long? firstTierAnswer;

		public Enums.MiningTiers MiningTier = Enums.MiningTiers.ThirdTier;
		public ComponentVersion<BlockType> MatureBlockType;
		public ComponentVersion MatureElectionContextVersion;
		public int MaturityBlockHash;
		public long MaturityBlockId;

		public readonly List<string> SelectedTransactionIds = new List<string>();
	}

	public abstract class ElectionQuestionDistillate {
		
	}
	
	public abstract class ElectionBlockQuestionDistillate : ElectionQuestionDistillate {
		
	}
	
	public abstract class ElectionDigestQuestionDistillate : ElectionQuestionDistillate {
		
	}
	
	public class BlockTransactionSectionQuestionDistillate : ElectionBlockQuestionDistillate{
		public long BlockId { get; set; } 
		public int? TransactionIndex { get; set; }

		public byte SelectedTransactionSection{ get; set; }
		public byte SelectedComponent{ get; set; }
	}
	
	public class BlockBytesetQuestionDistillate : ElectionBlockQuestionDistillate{
		public long BlockId { get; set; } 
		public int Offset { get; set; }
		public byte Length { get; set; }
	}
	
	public class DigestBytesetQuestionDistillate : ElectionDigestQuestionDistillate{
		public int DigestID { get; set; } 
		public int Offset { get; set; }
		public byte Length { get; set; }
	}
}