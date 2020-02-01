using Neuralia.Blockchains.Core.Configuration;

namespace Neuralia.Blockchains.Core.Types {
	public struct NodeShareType {

		public Enums.ChainSharingTypes SharingType { get; private set; }
		
		public NodeShareType( byte value ) : this((Enums.ChainSharingTypes)value){

		}
		
		public NodeShareType( Enums.ChainSharingTypes chainSharingTypes ){
			this.SharingType = chainSharingTypes;
		}

		public NodeShareType( AppSettingsBase.BlockSavingModes blockSavingMode ) : this(ConvertBlockSavingModes(blockSavingMode)){

		}

		public static Enums.ChainSharingTypes ConvertBlockSavingModes(AppSettingsBase.BlockSavingModes blockSavingMode) {

			switch(blockSavingMode) {
				case AppSettingsBase.BlockSavingModes.None:
				case AppSettingsBase.BlockSavingModes.NoneBySync:
					return Enums.ChainSharingTypes.None;
				case AppSettingsBase.BlockSavingModes.BlockOnly:
					return Enums.ChainSharingTypes.BlockOnly;
				case AppSettingsBase.BlockSavingModes.DigestThenBlocks:
					return Enums.ChainSharingTypes.DigestThenBlocks;
				case AppSettingsBase.BlockSavingModes.DigestAndBlocks:
					return Enums.ChainSharingTypes.DigestAndBlocks;
			}
			
			return Enums.ChainSharingTypes.None;
		}
		
		public bool Shares => this.HasBlocks;
		public bool DoesNotShare => !this.Shares;
		
		public bool HasDigests => UsesDigests(this.SharingType);
		public bool AllBlocks => UsesAllBlocks(this.SharingType);
		public bool OnlyBlocks => UsesOnlyBlocks(this.SharingType);
		public bool PartialBlocks => UsesPartialBlocks(this.SharingType);
		public bool HasBlocks => UsesBlocks(this.SharingType);
		public bool HasDigestsAndBlocks => UsesDigestsAndBlocks(this.SharingType);
		public bool HasDigestsThenBlocks => this.PartialBlocks;
		
		public static bool UsesDigests(Enums.ChainSharingTypes chainSharingTypes) {
			return (chainSharingTypes == Enums.ChainSharingTypes.DigestAndBlocks) || (chainSharingTypes == Enums.ChainSharingTypes.DigestThenBlocks);
		}

		public static bool UsesOnlyBlocks(Enums.ChainSharingTypes chainSharingTypes) {
			return (chainSharingTypes == Enums.ChainSharingTypes.BlockOnly);
		}

		public static bool UsesAllBlocks(Enums.ChainSharingTypes chainSharingTypes) {
			return UsesOnlyBlocks(chainSharingTypes) || (chainSharingTypes == Enums.ChainSharingTypes.DigestAndBlocks);
		}

		public static bool UsesPartialBlocks(Enums.ChainSharingTypes chainSharingTypes) {
			return chainSharingTypes == Enums.ChainSharingTypes.DigestThenBlocks;
		}
		
		/// <summary>
		///     Do we even use blocks at all?
		/// </summary>
		/// <param name="chainSharingTypes"></param>
		/// <returns></returns>
		public static bool UsesBlocks(Enums.ChainSharingTypes chainSharingTypes) {
			return UsesAllBlocks(chainSharingTypes) || UsesPartialBlocks(chainSharingTypes);
		}
		
		public static bool UsesDigestsAndBlocks(Enums.ChainSharingTypes chainSharingTypes) {
			return UsesBlocks(chainSharingTypes) && UsesDigests(chainSharingTypes);
		}
		
		public static bool SharesAnything(Enums.ChainSharingTypes chainSharingTypes) {
			return chainSharingTypes != Enums.ChainSharingTypes.None;
		}

		
		public static implicit operator NodeShareType(byte value) {
			return new NodeShareType(value);
		}
		
		public static implicit operator byte(NodeShareType nodeShareType) {
			return (byte)nodeShareType.SharingType;
		}
		
		public static implicit operator NodeShareType(Enums.ChainSharingTypes chainSharingTypes) {
			return new NodeShareType(chainSharingTypes);
		}
		
		public static implicit operator NodeShareType(AppSettingsBase.BlockSavingModes blockSavingMode) {
			return new NodeShareType(blockSavingMode);
		}
		
		public static implicit operator Enums.ChainSharingTypes(NodeShareType nodeShareType) {
			return nodeShareType.SharingType;
		}

		public override string ToString() {
			return this.SharingType.ToString();
		}
		
		public bool Equals(NodeShareType other) {

			return this.SharingType == other.SharingType;
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((NodeShareType) obj);
		}

		
		public static bool operator ==(NodeShareType a, NodeShareType b) {
			return a.Equals(b);
		}

		public static bool operator !=(NodeShareType a, NodeShareType b) {
			return !(a == b);
		}
		
		public override int GetHashCode() {
			return (int) this.SharingType.GetHashCode();
		}

		public static readonly NodeShareType Full = Enums.ChainSharingTypes.Full;
		public static readonly NodeShareType None = Enums.ChainSharingTypes.None;
	}
	
	public static class NodeShareTypeExtensions
	{
		public static NodeShareType NodeShareType(this ChainConfigurations configuration) {
			return new NodeShareType(configuration.BlockSavingMode);
		}
	}  
	
	
}