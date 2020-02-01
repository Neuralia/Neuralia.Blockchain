using System;
using System.Diagnostics;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Genesis {
	public interface IGenesisBlock : IBlock {
		DateTime Inception { get; set; }
		string Name { get; set; }
		string Moto { get; set; }
		string Banner { get; set; }
	}

	[DebuggerDisplay("BlockId: {BlockId}")]
	public abstract class GenesisBlock : Block, IGenesisBlock {

		public DateTime Inception { get; set; }
		public string Name { get; set; }
		public string Moto { get; set; }
		public string Banner { get; set; }

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Name", this.Name);
			jsonDeserializer.SetProperty("Inception", this.Inception);
			jsonDeserializer.SetProperty("Moto", this.Moto);
			jsonDeserializer.SetProperty("Banner", this.Banner);
		}

		protected override ComponentVersion<BlockType> SetIdentity() {
			return (BlockTypes.Instance.Genesis, 1, 0);
		}

		protected override void RehydrateBody(IDataRehydrator rehydratorBody, IBlockRehydrationFactory rehydrationFactory) {
			this.Inception = rehydratorBody.ReadDateTime();
			this.Name = rehydratorBody.ReadString();
			this.Moto = rehydratorBody.ReadString();
			this.Banner = rehydratorBody.ReadString();
		}
	}
}