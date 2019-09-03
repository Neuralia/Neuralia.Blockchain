using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Simple {

	public interface ISimpleBlock : IBlock {
	}

	/// <summary>
	///     a simple block with nothing fancy. Only accepted and rejected transactions.
	/// </summary>
	/// <typeparam name="REHYDRATION_FACTORY"></typeparam>
	public abstract class SimpleBlock : Block, ISimpleBlock {

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

		}

		protected override ComponentVersion<BlockType> SetIdentity() {
			return (BlockTypes.Instance.Simple, 1, 0);
		}
	}
}