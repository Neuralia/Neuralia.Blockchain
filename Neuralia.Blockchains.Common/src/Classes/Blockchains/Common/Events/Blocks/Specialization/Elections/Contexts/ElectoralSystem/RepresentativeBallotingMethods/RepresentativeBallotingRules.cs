using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Specialization.Elections.Contexts.ElectoralSystem.RepresentativeBallotingMethods {
	public interface IRepresentativeBallotingRules : IVersionableSerializable {
		ushort Amount { get; set; }
	}

	public interface IRepresentativeBallotingRules<T> : IVersionable<T>, IRepresentativeBallotingRules
		where T : SimpleUShort<T>, new() {
	}

	/// <summary>
	///     By what method do we select who will get to be the prime elected candidate and the representative of the election
	///     and by what rules should we operate
	/// </summary>
	public abstract class RepresentativeBallotingRules<T> : Versionable<T>, IRepresentativeBallotingRules<T>
		where T : SimpleUShort<T>, new() {

		public RepresentativeBallotingRules() {

		}

		public RepresentativeBallotingRules(ushort amount) {
			this.Amount = amount;
		}

		public ushort Amount { get; set; } = 100;

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodeList = base.GetStructuresArray();

			nodeList.Add(this.Amount);

			return nodeList;
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			this.Amount = rehydrator.ReadUShort();
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			throw new NotSupportedException();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetProperty("Amount", this.Amount);
		}

		public ISerializableCombo BaseVersion => this.Version;
	}
}