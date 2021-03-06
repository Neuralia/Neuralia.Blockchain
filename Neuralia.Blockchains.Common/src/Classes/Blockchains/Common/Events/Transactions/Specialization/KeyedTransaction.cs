﻿using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Serialization.Blockchain.Utils;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Specialization {

	public interface IKeyedTransaction : IIndexedTransaction {
		KeySet Keyset { get; }
	}

	/// <summary>
	///     A special base class for transactions that operate keys
	/// </summary>
	/// <typeparam name="REHYDRATION_FACTORY"></typeparam>
	public abstract class KeyedTransaction : Transaction, IKeyedTransaction {

		public KeySet Keyset { get; } = new KeySet();

		public override HashNodeList GetStructuresArray(Enums.MutableStructureTypes types) {
			HashNodeList nodeList = base.GetStructuresArray(types);

			nodeList.Add(this.Keyset);

			return nodeList;
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			//

			jsonDeserializer.SetProperty("Keyset", this.Keyset);
		}

		protected override void RehydrateHeader(IDataRehydrator rehydrator) {
			base.RehydrateHeader(rehydrator);

			this.Keyset.Rehydrate(rehydrator);
		}

		protected override void DehydrateHeader(IDataDehydrator dehydrator) {
			base.DehydrateHeader(dehydrator);

			this.Keyset.Dehydrate(dehydrator);
		}

		protected override sealed void RehydrateContents(ChannelsEntries<IDataRehydrator> dataChannels, ITransactionRehydrationFactory rehydrationFactory) {

			// in a keyed transaction, nothing goes in the content, we ignore it completely
		}

		protected override sealed void DehydrateContents(ChannelsEntries<IDataDehydrator> dataChannels) {

			// in a keyed transaction, nothing goes in the content, we ignore it completely
		}
	}
}