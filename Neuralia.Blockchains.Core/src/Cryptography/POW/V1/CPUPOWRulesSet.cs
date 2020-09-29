using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.POW.V1.Crypto;
using Neuralia.Blockchains.Core.Cryptography.POW.V1.Hash;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.POW.V1 {
	public class CPUPOWRulesSet : Versionable<SimpleUShort> {

		public static CPUPOWRulesSet ServerPresentationDefaultRulesSet;
		public static CPUPOWRulesSet PresentationDefaultRulesSet;
		public static CPUPOWRulesSet InitiationAppointmentDefaultRulesSet;
		public static CPUPOWRulesSet PuzzleDefaultRuleset;

		static CPUPOWRulesSet() {
			ServerPresentationDefaultRulesSet = new CPUPOWRulesSet();
			ServerPresentationDefaultRulesSet.HashSets.Add(Hashes.SHA3_512);
			ServerPresentationDefaultRulesSet.HashSets.Add(Hashes.SHA2_512);
			
			ServerPresentationDefaultRulesSet.CryptoSets.Add(Cryptos.AES_256);
			ServerPresentationDefaultRulesSet.CryptoSets.Add(Cryptos.AES_GCM);
			//TODO: set this for mainnet!!!
			//PresentationDefaultRulesSet.MainBufferDataSize = 30;
			ServerPresentationDefaultRulesSet.MainBufferDataSize = 20;
			ServerPresentationDefaultRulesSet.CryptoIterations = 10;
			ServerPresentationDefaultRulesSet.FillerHashIterations = 2;
			ServerPresentationDefaultRulesSet.HashTargetDifficulty = HashDifficultyUtils.Default512Difficulty;
			ServerPresentationDefaultRulesSet.L2CacheTarget = 16;

			PresentationDefaultRulesSet = new CPUPOWRulesSet();
			PresentationDefaultRulesSet.HashSets.Add(Hashes.SHA3_512);
			PresentationDefaultRulesSet.HashSets.Add(Hashes.SHA2_512);
			
			PresentationDefaultRulesSet.CryptoSets.Add(Cryptos.AES_256);
			
			//TODO: set this for mainnet!!!
			//PresentationDefaultRulesSet.MainBufferDataSize = 30;
			PresentationDefaultRulesSet.MainBufferDataSize = 20;
			PresentationDefaultRulesSet.CryptoIterations = 3;
			PresentationDefaultRulesSet.FillerHashIterations = 2;
			PresentationDefaultRulesSet.HashTargetDifficulty = HashDifficultyUtils.Default512Difficulty;
			PresentationDefaultRulesSet.L2CacheTarget = 16;
			
			InitiationAppointmentDefaultRulesSet = new CPUPOWRulesSet();
			InitiationAppointmentDefaultRulesSet.HashSets.Add(Hashes.SHA3_512);
			InitiationAppointmentDefaultRulesSet.HashSets.Add(Hashes.SHA2_512);
			
			InitiationAppointmentDefaultRulesSet.CryptoSets.Add(Cryptos.AES_256);
			
			//InitiationAppointmentDefaultRulesSet.MainBufferDataSize = 30;
			InitiationAppointmentDefaultRulesSet.MainBufferDataSize = 20;
			InitiationAppointmentDefaultRulesSet.CryptoIterations = 3;
			InitiationAppointmentDefaultRulesSet.FillerHashIterations = 2;
			InitiationAppointmentDefaultRulesSet.HashTargetDifficulty = HashDifficultyUtils.Default512Difficulty;
			InitiationAppointmentDefaultRulesSet.L2CacheTarget = 16;
			
			
			
			PuzzleDefaultRuleset = new CPUPOWRulesSet();
			PuzzleDefaultRuleset.HashSets.Add(Hashes.SHA3_512);
			PuzzleDefaultRuleset.HashSets.Add(Hashes.SHA2_512);
			
			PuzzleDefaultRuleset.CryptoSets.Add(Cryptos.AES_256);
			
			//InitiationAppointmentDefaultRulesSet.MainBufferDataSize = 30;
			PuzzleDefaultRuleset.MainBufferDataSize = 20;
			PuzzleDefaultRuleset.CryptoIterations = 3;
			PuzzleDefaultRuleset.FillerHashIterations = 2;
			PuzzleDefaultRuleset.HashTargetDifficulty = HashDifficultyUtils.Default512Difficulty;
			PuzzleDefaultRuleset.L2CacheTarget = 16;
			
			
			
		}

		protected bool Equals(CPUPOWRulesSet other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.HashSets.SequenceEqual(other.HashSets) && this.CryptoSets.SequenceEqual(other.CryptoSets) && this.Enabled == other.Enabled && this.MainBufferDataSize == other.MainBufferDataSize && this.CryptoIterations == other.CryptoIterations && this.FillerHashIterations == other.FillerHashIterations && this.HashTargetDifficulty == other.HashTargetDifficulty && this.L2CacheTarget == other.L2CacheTarget;
		}

		public static bool operator ==(CPUPOWRulesSet a, CPUPOWRulesSet b) {
			if(ReferenceEquals(null, b)) {
				return ReferenceEquals(null, a);
			}

			if(ReferenceEquals(null, a)) {
				return false;
			}
			
			return a.Equals(b);
		}

		public static bool operator !=(CPUPOWRulesSet a, CPUPOWRulesSet b) {
			return !(a == b);
		}
		
		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((CPUPOWRulesSet) obj);
		}

		public override int GetHashCode() {
			return HashCode.Combine(this.HashSets, this.CryptoSets, this.Enabled, this.MainBufferDataSize, this.CryptoIterations, this.FillerHashIterations, this.HashTargetDifficulty, this.L2CacheTarget);
		}

		public enum Hashes : byte {
			SHA2_256 = 1,
			SHA2_512 = 2,
			SHA3_256 = 3,
			SHA3_512 = 4
		}
		
		public enum Cryptos : byte {
			AES_256 = 1,
			AES_GCM = 2
		}

		public int MainBufferDataSize { get; set; } = 30; //2^32 = 4GB, 2^31 = 2GB, 2^30 = 1GB. 2^20 = 1MB
		
		/// <summary>
		/// Number of crypto operations when running the crypto transform phase
		/// </summary>
		public int CryptoIterations { get; set; } = 3;
		
		/// <summary>
		/// How many hash iterations to do while hash filling.
		/// </summary>
		public int FillerHashIterations { get; set; } = 2;
		
		public long HashTargetDifficulty { get; set; } = HashDifficultyUtils.Default512Difficulty;
		
		// the size of the chunks in which we split the memory
		public int L2CacheTarget{ get; set; } = 16; // 2 ^ 16 = 64KB
		
		public List<Hashes> HashSets = new List<Hashes>();
		public List<Cryptos> CryptoSets = new List<Cryptos>();

		public bool Enabled = true;

		public CPUPOWRulesSet() {
			
		}
		
		public POWHashSet GenerateHashSet() {
			
			POWHashSet hashSet = new POWHashSet();

			foreach(var hash in this.HashSets) {
				hashSet.AddEntry(hash);
			}
			
			return hashSet;
		}
		
		public POWCryptoSet GenerateCryptoSet() {
			POWCryptoSet cryptoSet = new POWCryptoSet();

			foreach(var crypto in this.CryptoSets) {
				cryptoSet.AddEntry(crypto);
			}
			
			return cryptoSet;
		}
		
		public override HashNodeList GetStructuresArray() {
			var hashNodes = base.GetStructuresArray();

			hashNodes.Add(this.Enabled);
			hashNodes.Add(this.HashSets.Count);
			
			foreach(var entry in this.HashSets) {
				hashNodes.Add((byte)entry);
			}
			
			hashNodes.Add(this.CryptoSets.Count);
			
			foreach(var entry in this.CryptoSets) {
				hashNodes.Add((byte)entry);
			}

			hashNodes.Add(this.HashTargetDifficulty);
			hashNodes.Add(this.MainBufferDataSize);
			hashNodes.Add(this.CryptoIterations);
			hashNodes.Add(this.L2CacheTarget);
			hashNodes.Add(this.FillerHashIterations);
			
			return hashNodes;
		}

		public void Rehydrate(SafeArrayHandle bytes) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
			this.Rehydrate(rehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Enabled = rehydrator.ReadBool();
			
			int count = rehydrator.ReadByte();

			this.HashSets.Clear();
			for(int i = 0; i < count; i++) {
				this.HashSets.Add((Hashes)rehydrator.ReadByte());
			}
			
			count = rehydrator.ReadByte();

			this.CryptoSets.Clear();
			for(int i = 0; i < count; i++) {
				this.CryptoSets.Add((Cryptos)rehydrator.ReadByte());
			}
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			tool.Rehydrate(rehydrator);
			this.HashTargetDifficulty = tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.MainBufferDataSize = (int)tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.CryptoIterations = (int)tool.Value;

			tool.Rehydrate(rehydrator);
			this.L2CacheTarget = (int)tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.FillerHashIterations = (int)tool.Value;
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			dehydrator.Write(this.Enabled);

			dehydrator.Write((byte)this.HashSets.Count);

			foreach(var entry in this.HashSets) {
				dehydrator.Write((byte)entry);
			}
			
			dehydrator.Write((byte)this.CryptoSets.Count);

			foreach(var entry in this.CryptoSets) {
				dehydrator.Write((byte)entry);
			}
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			tool.Value = this.HashTargetDifficulty;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.MainBufferDataSize;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.CryptoIterations;
			tool.Dehydrate(dehydrator);

			tool.Value = this.L2CacheTarget;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.FillerHashIterations;
			tool.Dehydrate(dehydrator);
		}

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);
			
			return dehydrator.ToArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);
			
			jsonDeserializer.SetArray("HashSets", this.HashSets);
			jsonDeserializer.SetArray("CryptoSets", this.CryptoSets);
			
			jsonDeserializer.SetProperty("Enabled", this.Enabled);
			jsonDeserializer.SetProperty("HashTargetDifficulty", this.HashTargetDifficulty);
			jsonDeserializer.SetProperty("MainBufferDataSize", this.MainBufferDataSize);
			jsonDeserializer.SetProperty("CryptoIterations", this.CryptoIterations);
			jsonDeserializer.SetProperty("L2CacheTarget", this.L2CacheTarget);
			jsonDeserializer.SetProperty("L2CacheTarget", this.FillerHashIterations);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (1,0,1);
		}
	}
}