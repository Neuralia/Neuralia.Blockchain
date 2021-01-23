using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Crypto;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Hash;
using Neuralia.Blockchains.Core.Cryptography.THS.V1.Prng;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1 {
	public class THSRulesSet : Versionable<SimpleUShort> {

		public enum Cryptos : byte {
			AES_256 = 1,
			AES_GCM = 2,
			XCHACHA_20 = 3,
			XCHACHA_40 = 4
		}

		public enum Hashes : byte {
			SHA2_256 = 1,
			SHA2_512 = 2,
			SHA3_256 = 3,
			SHA3_512 = 4,
			XX_HASH = 5
		}

		public enum Prngs : byte {
			SFC_256 = 1,
			JSF_256 = 2,
			MULBERRY_32 = 3,
			SPLITMIX_64 = 4,
			XOSHIRO_256_SS = 5
		}
		
		[Flags]
		public enum THSFeatures : int {
			HashPages = 1 << 0,
			ScramblePages = 1 << 1,
			FillFast = 1 << 2,
			FillFull = 1 << 3
		}

		public static THSRulesSet ServerPresentationDefaultRulesSet;
		public static THSRulesSet PresentationDefaultRulesSet;
		public static THSRulesSet InitiationAppointmentDefaultRulesSet;
		public static THSRulesSet PuzzleDefaultRuleset;
		public static THSRulesSet TestRuleset;

		public static THSRulesSetDescriptor ServerPresentationDefaultRulesSetDescriptor;
		public static THSRulesSetDescriptor PresentationDefaultRulesSetDescriptor;
		public static THSRulesSetDescriptor InitiationAppointmentDefaultRulesSetDescriptor;
		public static THSRulesSetDescriptor PuzzleDefaultRulesetDescriptor;
		public static THSRulesSetDescriptor TestRulesetDescriptor;

		static THSRulesSet() {

			// server presentation
			
#if TESTING
			ServerPresentationDefaultRulesSetDescriptor = new THSRulesSetDescriptor();
			ServerPresentationDefaultRulesSetDescriptor.TargetTimespan = TimeSpan.FromSeconds(5);
			ServerPresentationDefaultRulesSetDescriptor.EstimatedIterationTime = TimeSpan.FromSeconds(1);
			ServerPresentationDefaultRulesSetDescriptor.AverageRounds = 3;
			ServerPresentationDefaultRulesSetDescriptor.MaxRounds = 27;
			
			ServerPresentationDefaultRulesSet = new THSRulesSet();

			ServerPresentationDefaultRulesSet.AddHashSet(Hashes.SHA2_512, 5);
			ServerPresentationDefaultRulesSet.AddHashSet(Hashes.SHA3_512);
			

			ServerPresentationDefaultRulesSet.AddCryptoSet(Cryptos.AES_256, 5);
			ServerPresentationDefaultRulesSet.AddCryptoSet(Cryptos.XCHACHA_20);
			
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.SPLITMIX_64, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.MULBERRY_32, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.SFC_256, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.JSF_256);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.XOSHIRO_256_SS);

			ServerPresentationDefaultRulesSet.MainBufferDataSize = THSEngine.THSMemorySizes.THS_256_MB;
			ServerPresentationDefaultRulesSet.CryptoIterations = 1;
			ServerPresentationDefaultRulesSet.CryptoSuccessRate = 3;

			ServerPresentationDefaultRulesSet.HashIterations = 1;
			ServerPresentationDefaultRulesSet.HashingSuccessRate = 3;

			ServerPresentationDefaultRulesSet.Features = THSFeatures.HashPages | THSFeatures.ScramblePages;
			ServerPresentationDefaultRulesSet.PageSize = THSEngine.THSMemorySizes.THS_64_KB;

#else

			ServerPresentationDefaultRulesSetDescriptor = new THSRulesSetDescriptor();
			ServerPresentationDefaultRulesSetDescriptor.TargetTimespan = TimeSpan.FromHours(24);
			ServerPresentationDefaultRulesSetDescriptor.EstimatedIterationTime = TimeSpan.FromSeconds(6);
			ServerPresentationDefaultRulesSetDescriptor.AverageRounds = 9;
			ServerPresentationDefaultRulesSetDescriptor.MaxRounds = 27;
			
			ServerPresentationDefaultRulesSet = new THSRulesSet();

			ServerPresentationDefaultRulesSet.AddHashSet(Hashes.SHA2_512, 5);
			ServerPresentationDefaultRulesSet.AddHashSet(Hashes.SHA3_512);
			

			ServerPresentationDefaultRulesSet.AddCryptoSet(Cryptos.AES_256, 5);
			ServerPresentationDefaultRulesSet.AddCryptoSet(Cryptos.XCHACHA_20);
			
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.SPLITMIX_64, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.MULBERRY_32, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.SFC_256, 5);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.JSF_256);
			ServerPresentationDefaultRulesSet.AddPrngSet(Prngs.XOSHIRO_256_SS);

			ServerPresentationDefaultRulesSet.MainBufferDataSize = THSEngine.THSMemorySizes.THS_2_GB;
			ServerPresentationDefaultRulesSet.CryptoIterations = 1;
			ServerPresentationDefaultRulesSet.CryptoSuccessRate = 3;

			ServerPresentationDefaultRulesSet.HashIterations = 1;
			ServerPresentationDefaultRulesSet.HashingSuccessRate = 3;

			ServerPresentationDefaultRulesSet.Features = THSFeatures.HashPages | THSFeatures.ScramblePages;
			ServerPresentationDefaultRulesSet.PageSize = THSEngine.THSMemorySizes.THS_64_KB;

#endif

			// user presentation
			PresentationDefaultRulesSetDescriptor = new THSRulesSetDescriptor();
			PresentationDefaultRulesSetDescriptor.TargetTimespan = TimeSpan.FromMinutes(3);
			PresentationDefaultRulesSetDescriptor.EstimatedIterationTime = TimeSpan.FromMilliseconds(250);
			PresentationDefaultRulesSetDescriptor.AverageRounds = 3;
			PresentationDefaultRulesSetDescriptor.MaxRounds = 13;

			PresentationDefaultRulesSet = new THSRulesSet();
			PresentationDefaultRulesSet.AddHashSet(Hashes.SHA3_512);
			PresentationDefaultRulesSet.AddHashSet(Hashes.SHA2_512);

			PresentationDefaultRulesSet.AddCryptoSet(Cryptos.XCHACHA_20);
			PresentationDefaultRulesSet.AddCryptoSet(Cryptos.AES_256);
			
			PresentationDefaultRulesSet.AddPrngSet(Prngs.SPLITMIX_64);
			PresentationDefaultRulesSet.AddPrngSet(Prngs.MULBERRY_32);
			PresentationDefaultRulesSet.AddPrngSet(Prngs.SFC_256);
			
			PresentationDefaultRulesSet.MainBufferDataSize = THSEngine.THSMemorySizes.THS_512_MB;
			PresentationDefaultRulesSet.CryptoIterations = 1;
			PresentationDefaultRulesSet.CryptoSuccessRate = 13;

			PresentationDefaultRulesSet.HashIterations = 1;
			PresentationDefaultRulesSet.HashingSuccessRate = 9;
			
			PresentationDefaultRulesSet.Features = THSFeatures.FillFast;
			PresentationDefaultRulesSet.PageSize = THSEngine.THSMemorySizes.THS_64_MB;
			
			// initiation appointment request

			InitiationAppointmentDefaultRulesSetDescriptor = new THSRulesSetDescriptor();
			InitiationAppointmentDefaultRulesSetDescriptor.TargetTimespan = TimeSpan.FromMinutes(3);
			InitiationAppointmentDefaultRulesSetDescriptor.EstimatedIterationTime = TimeSpan.FromMilliseconds(250);
			InitiationAppointmentDefaultRulesSetDescriptor.AverageRounds = 3;
			InitiationAppointmentDefaultRulesSetDescriptor.MaxRounds = 13;

			InitiationAppointmentDefaultRulesSet = new THSRulesSet();
			InitiationAppointmentDefaultRulesSet.AddHashSet(Hashes.SHA3_512);
			InitiationAppointmentDefaultRulesSet.AddHashSet(Hashes.SHA2_512);

			InitiationAppointmentDefaultRulesSet.AddCryptoSet(Cryptos.XCHACHA_20);
			InitiationAppointmentDefaultRulesSet.AddCryptoSet(Cryptos.AES_256);
			
			InitiationAppointmentDefaultRulesSet.AddPrngSet(Prngs.SPLITMIX_64);
			InitiationAppointmentDefaultRulesSet.AddPrngSet(Prngs.MULBERRY_32);
			InitiationAppointmentDefaultRulesSet.AddPrngSet(Prngs.SFC_256);
			
			InitiationAppointmentDefaultRulesSet.MainBufferDataSize = THSEngine.THSMemorySizes.THS_512_MB;
			InitiationAppointmentDefaultRulesSet.CryptoIterations = 1;
			InitiationAppointmentDefaultRulesSet.CryptoSuccessRate = 13;

			InitiationAppointmentDefaultRulesSet.HashIterations = 1;
			InitiationAppointmentDefaultRulesSet.HashingSuccessRate = 9;
			
			InitiationAppointmentDefaultRulesSet.Features = THSFeatures.FillFast;
			InitiationAppointmentDefaultRulesSet.PageSize = THSEngine.THSMemorySizes.THS_64_MB;

			// puzzle
			PuzzleDefaultRulesetDescriptor = new THSRulesSetDescriptor();
			PuzzleDefaultRulesetDescriptor.TargetTimespan = TimeSpan.FromSeconds(30);
			PuzzleDefaultRulesetDescriptor.EstimatedIterationTime = TimeSpan.FromMilliseconds(250);
			PuzzleDefaultRulesetDescriptor.AverageRounds = 3;
			PuzzleDefaultRulesetDescriptor.MaxRounds = 13;

			PuzzleDefaultRuleset = new THSRulesSet();
			PuzzleDefaultRuleset.AddHashSet(Hashes.SHA3_512);
			PuzzleDefaultRuleset.AddHashSet(Hashes.SHA2_512);

			PuzzleDefaultRuleset.AddCryptoSet(Cryptos.XCHACHA_20);
			PuzzleDefaultRuleset.AddCryptoSet(Cryptos.AES_256);
			
			PuzzleDefaultRuleset.AddPrngSet(Prngs.SPLITMIX_64);
			PuzzleDefaultRuleset.AddPrngSet(Prngs.MULBERRY_32);
			PuzzleDefaultRuleset.AddPrngSet(Prngs.SFC_256);
			
			PuzzleDefaultRuleset.MainBufferDataSize = THSEngine.THSMemorySizes.THS_512_MB;
			PuzzleDefaultRuleset.CryptoIterations = 1;
			PuzzleDefaultRuleset.CryptoSuccessRate = 13;

			PuzzleDefaultRuleset.HashIterations = 1;
			PuzzleDefaultRuleset.HashingSuccessRate = 9;
			
			PuzzleDefaultRuleset.Features = THSFeatures.FillFast;
			PuzzleDefaultRuleset.PageSize = THSEngine.THSMemorySizes.THS_64_MB;
			
			// for testing
			
			TestRulesetDescriptor = new THSRulesSetDescriptor();
			TestRulesetDescriptor.TargetTimespan = TimeSpan.FromSeconds(40);
			TestRulesetDescriptor.EstimatedIterationTime = TimeSpan.FromSeconds(6);
			TestRulesetDescriptor.AverageRounds = 3;
			TestRulesetDescriptor.MaxRounds = 13;
			

			TestRuleset = new THSRulesSet();
			TestRuleset.AddHashSet(Hashes.SHA3_512);
			TestRuleset.AddHashSet(Hashes.SHA2_512);

			TestRuleset.AddCryptoSet(Cryptos.XCHACHA_20);
			TestRuleset.AddCryptoSet(Cryptos.AES_256);
			
			TestRuleset.AddPrngSet(Prngs.SPLITMIX_64);
			TestRuleset.AddPrngSet(Prngs.MULBERRY_32);
			TestRuleset.AddPrngSet(Prngs.SFC_256);
			
			TestRuleset.MainBufferDataSize = THSEngine.THSMemorySizes.THS_512_MB;
			TestRuleset.CryptoIterations = 1;
			TestRuleset.CryptoSuccessRate = 13;

			TestRuleset.HashIterations = 1;
			TestRuleset.HashingSuccessRate = 9;
			
			TestRuleset.Features = THSFeatures.FillFast;
			TestRuleset.PageSize = THSEngine.THSMemorySizes.THS_64_MB;
		}

		public THSEngine.THSMemorySizes MainBufferDataSize { get; set; } = THSEngine.THSMemorySizes.THS_1_GB;

		/// <summary>
		///     Number of crypto operations when running the crypto transform phase
		/// </summary>
		public int CryptoIterations { get; set; } = 3;

		/// <summary>
		///     perform a crypto operation one in X times.
		/// </summary>
		public int CryptoSuccessRate { get; set; } = 2;
		
		/// <summary>
		/// advanced mode will ensure a more complex operation, at the cost of more time
		/// </summary>
		public THSFeatures Features { get; set; }

		/// <summary>
		///     Number of hashing operations when running the hashing transform phase
		/// </summary>
		public int HashIterations { get; set; } = 1;

		/// <summary>
		///     perform a hashing operation one in X times.
		/// </summary>
		public int HashingSuccessRate { get; set; } = 2;
		
		// the size of the chunks in which we split the memory
		public THSEngine.THSMemorySizes PageSize { get; set; } = THSEngine.THSMemorySizes.THS_64_KB;

		public List<Hashes> HashSets { get; } = new List<Hashes>();
		public List<Cryptos> CryptoSets { get; } = new List<Cryptos>();
		public List<Prngs> PrngSets { get; } = new List<Prngs>();

		public bool Enabled { get; set; } = true;
		
		protected bool Equals(THSRulesSet other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.HashSets.SequenceEqual(other.HashSets) && this.CryptoSets.SequenceEqual(other.CryptoSets) && this.PrngSets.SequenceEqual(other.PrngSets) && (this.Enabled == other.Enabled) && (this.MainBufferDataSize == other.MainBufferDataSize) && (this.CryptoIterations == other.CryptoIterations) && (this.CryptoSuccessRate == other.CryptoSuccessRate) && (this.HashIterations == other.HashIterations) && (this.HashingSuccessRate == other.HashingSuccessRate) && (this.PageSize == other.PageSize);
		}

		public static bool operator ==(THSRulesSet a, THSRulesSet b) {
			if(ReferenceEquals(null, b)) {
				return ReferenceEquals(null, a);
			}

			if(ReferenceEquals(null, a)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(THSRulesSet a, THSRulesSet b) {
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

			return this.Equals((THSRulesSet) obj);
		}

		public override int GetHashCode() {
			return HashCode.Combine(base.GetHashCode(), this.HashSets, this.CryptoSets, this.PrngSets, this.Enabled, this.MainBufferDataSize, HashCode.Combine(this.CryptoIterations, this.CryptoSuccessRate, this.HashIterations, this.HashingSuccessRate, this.Features), this.PageSize);
		}

		public void AddHashSet(Hashes hash) {
			this.AddHashSet(hash, 1);
		}

		public void AddHashSet(Hashes hash, int count) {
			for(int i = 0; i < count; i++) {
				this.HashSets.Add(hash);
			}
		}

		public void AddCryptoSet(Cryptos crypto) {
			this.AddCryptoSet(crypto, 1);
		}

		public void AddCryptoSet(Cryptos crypto, int count) {
			for(int i = 0; i < count; i++) {
				this.CryptoSets.Add(crypto);
			}
		}

		public void AddPrngSet(Prngs prng) {
			this.AddPrngSet(prng, 1);
		}

		public void AddPrngSet(Prngs prng, int count) {
			for(int i = 0; i < count; i++) {
				this.PrngSets.Add(prng);
			}
		}

		public THSHashSet GenerateHashSet() {

			THSHashSet hashSet = new THSHashSet();

			foreach(Hashes hash in this.HashSets) {
				hashSet.AddEntry(hash);
			}

			return hashSet;
		}

		public THSCryptoSet GenerateCryptoSet() {
			THSCryptoSet cryptoSet = new THSCryptoSet();

			foreach(Cryptos crypto in this.CryptoSets) {
				cryptoSet.AddEntry(crypto);
			}

			return cryptoSet;
		}

		public THSPrngSet GeneratePrngSet() {
			THSPrngSet prngSet = new THSPrngSet();

			foreach(Prngs crypto in this.PrngSets) {
				prngSet.AddEntry(crypto);
			}

			return prngSet;
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodes = base.GetStructuresArray();

			hashNodes.Add(this.Enabled);

			hashNodes.Add(this.HashSets.Count);

			foreach(Hashes entry in this.HashSets) {
				hashNodes.Add((byte) entry);
			}

			hashNodes.Add(this.CryptoSets.Count);

			foreach(Cryptos entry in this.CryptoSets) {
				hashNodes.Add((byte) entry);
			}
			
			hashNodes.Add(this.PrngSets.Count);

			foreach(var entry in this.PrngSets) {
				hashNodes.Add((byte) entry);
			}

			hashNodes.Add(this.MainBufferDataSize);
			hashNodes.Add(this.CryptoIterations);
			hashNodes.Add(this.CryptoSuccessRate);
			hashNodes.Add(this.HashIterations);
			hashNodes.Add(this.HashingSuccessRate);
			hashNodes.Add(this.PageSize);
			hashNodes.Add(this.Features);
			
			return hashNodes;
		}

		public void Rehydrate(SafeArrayHandle bytes) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
			this.Rehydrate(rehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			this.Enabled = rehydrator.ReadBool();
			
			tool.Rehydrate(rehydrator);
			this.Features = (THSFeatures)tool.Value;
			
			int count = rehydrator.ReadByte();

			this.HashSets.Clear();

			for(int i = 0; i < count; i++) {
				this.AddHashSet(rehydrator.ReadByteEnum<Hashes>());
			}

			count = rehydrator.ReadByte();

			this.CryptoSets.Clear();

			for(int i = 0; i < count; i++) {
				this.AddCryptoSet(rehydrator.ReadByteEnum<Cryptos>());
			}

			count = rehydrator.ReadByte();

			this.PrngSets.Clear();

			for(int i = 0; i < count; i++) {
				this.AddPrngSet(rehydrator.ReadByteEnum<Prngs>());
			}
			
			tool.Rehydrate(rehydrator);
			this.MainBufferDataSize = (THSEngine.THSMemorySizes) tool.Value;

			tool.Rehydrate(rehydrator);
			this.CryptoIterations = (int) tool.Value;

			tool.Rehydrate(rehydrator);
			this.CryptoSuccessRate = (int) tool.Value;

			tool.Rehydrate(rehydrator);
			this.HashIterations = (int) tool.Value;

			tool.Rehydrate(rehydrator);
			this.HashingSuccessRate = (int) tool.Value;

			tool.Rehydrate(rehydrator);
			this.PageSize = (THSEngine.THSMemorySizes) tool.Value;

		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			dehydrator.Write(this.Enabled);
			
			tool.Value = (int)this.Features;
			tool.Dehydrate(dehydrator);
			
			dehydrator.Write((byte) this.HashSets.Count);

			foreach(Hashes entry in this.HashSets) {
				dehydrator.Write((byte) entry);
			}

			dehydrator.Write((byte) this.CryptoSets.Count);

			foreach(Cryptos entry in this.CryptoSets) {
				dehydrator.Write((byte) entry);
			}
			
			dehydrator.Write((byte) this.PrngSets.Count);

			foreach(var entry in this.PrngSets) {
				dehydrator.Write((byte) entry);
			}
			
			tool.Value = (int)this.MainBufferDataSize;
			tool.Dehydrate(dehydrator);

			tool.Value = this.CryptoIterations;
			tool.Dehydrate(dehydrator);

			tool.Value = this.CryptoSuccessRate;
			tool.Dehydrate(dehydrator);

			tool.Value = this.HashIterations;
			tool.Dehydrate(dehydrator);

			tool.Value = this.HashingSuccessRate;
			tool.Dehydrate(dehydrator);

			tool.Value = (int)this.PageSize;
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
			jsonDeserializer.SetArray("PrngSets", this.PrngSets);
			
			jsonDeserializer.SetProperty("Enabled", this.Enabled);
			jsonDeserializer.SetProperty("MainBufferDataSize", this.MainBufferDataSize);
			jsonDeserializer.SetProperty("CryptoIterations", this.CryptoIterations);
			jsonDeserializer.SetProperty("CryptoSuccessRate", this.CryptoSuccessRate);
			jsonDeserializer.SetProperty("HashIterations", this.HashIterations);
			jsonDeserializer.SetProperty("HashingSuccessRate", this.HashingSuccessRate);
			jsonDeserializer.SetProperty("PageSize", this.PageSize);
			jsonDeserializer.SetProperty("Mode", this.Features);
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (1, 0, 1);
		}
	}

	public class THSRulesSetDescriptor {

		public const double MAXIMUM_BOUND_MULTIPLIER = 1.5;

		public THSRulesSetDescriptor() {

		}

		public THSRulesSetDescriptor(TimeSpan estimatedIterationTime, int averageRounds, int maxRounds) {
			this.EstimatedIterationTime = estimatedIterationTime;
			this.AverageRounds = averageRounds;
			this.MaxRounds = maxRounds;
		}

		public TimeSpan TargetTimespan { get; set; } = TimeSpan.FromSeconds(1);
		public TimeSpan EstimatedIterationTime { get; set; } = TimeSpan.FromSeconds(1);
		public TimeSpan EstimatedNonceTime => this.EstimatedIterationTime * this.AverageRounds;

		public TimeSpan EstimatedHigherRoundTime => this.EstimatedIterationTime * MAXIMUM_BOUND_MULTIPLIER;
		public TimeSpan EstimatedHigherNonceTime => this.EstimatedNonceTime * MAXIMUM_BOUND_MULTIPLIER;

		public long NonceTarget => (long) (this.TargetTimespan.TotalSeconds / this.EstimatedIterationTime.TotalSeconds);
		public long HashTargetDifficulty => (long) (((decimal) this.TargetTimespan.TotalSeconds / (decimal) this.EstimatedIterationTime.TotalSeconds) * HashDifficultyUtils.Default512Difficulty) / this.AverageRounds;

		// how many rounds do we expect in this set
		public int AverageRounds { get; set; } = 1;
		public int MaxRounds { get; set; } = 10;
		
	}
}