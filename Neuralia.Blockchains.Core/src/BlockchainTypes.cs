using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core {

	public class BlockchainType : SimpleUShort<BlockchainType> {

		public BlockchainType() {
		}

		public BlockchainType(ushort value) : base(value) {
		}

		public override bool Equals(BlockchainType other) {

			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.Value == other.Value;
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

			return this.Equals((BlockchainType) obj);
		}

		public static implicit operator BlockchainType(ushort d) {
			return new BlockchainType(d);
		}

		public static bool operator ==(BlockchainType a, BlockchainType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(BlockchainType a, BlockchainType b) {
			return !(a == b);
		}
	}

	public class BlockchainTypes : UShortConstantSet<BlockchainType> {
		public readonly BlockchainType All;

		private readonly List<IBlockchainTypeNameProvider> blockchainTypeNameProviders = new List<IBlockchainTypeNameProvider>();

		public readonly BlockchainType None;

		static BlockchainTypes() {
		}

		protected BlockchainTypes() : base(1000) {
			this.None = 0;
			this.All = this.CreateBaseConstant();
		}

		public static BlockchainTypes Instance { get; } = new BlockchainTypes();

		public static string GetBlockchainTypeName(BlockchainType chainType) {
			return Instance.GetBlockchainTypeStringName(chainType);
		}

		public string GetBlockchainTypeStringName(BlockchainType chainType) {
			IBlockchainTypeNameProvider provider = this.blockchainTypeNameProviders.FirstOrDefault(p => p.MatchesType(chainType));

			if(provider != null) {
				return provider.GetBlockchainTypeName(chainType);
			}

			throw new ApplicationException("BlockchainTypeNameProvider was not set. Could not get name of blockchain type");
		}

		public void AddBlockchainTypeNameProvider(IBlockchainTypeNameProvider blockchainTypeNameProvider) {
			this.blockchainTypeNameProviders.Add(blockchainTypeNameProvider);
		}
	}

	public interface IBlockchainTypeNameProvider {

		string GetBlockchainTypeName(BlockchainType chainType);
		bool MatchesType(BlockchainType chainType);
	}
}