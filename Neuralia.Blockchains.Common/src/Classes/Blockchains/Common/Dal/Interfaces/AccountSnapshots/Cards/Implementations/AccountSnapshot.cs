using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards.Implementations {
	public class AccountSnapshot<ACCOUNT_FEATURE> : IAccountSnapshot<ACCOUNT_FEATURE>
		where ACCOUNT_FEATURE : IAccountFeature {

		public long AccountId { get; set; }
		public long InceptionBlockId { get; set; }
		public byte TrustLevel { get; set; }
		public List<ACCOUNT_FEATURE> AppliedFeatures { get; } = new List<ACCOUNT_FEATURE>();

		public void ClearCollection() {
			this.AppliedFeatures.Clear();
		}

		public void CreateNewCollectionEntry(out IAccountFeature result) {
			TypedCollectionExposureUtil<IAccountFeature>.CreateNewCollectionEntry(this.AppliedFeatures, out result);
		}

		public void AddCollectionEntry(IAccountFeature entry) {
			TypedCollectionExposureUtil<IAccountFeature>.AddCollectionEntry(entry, this.AppliedFeatures);
		}

		public void RemoveCollectionEntry(Func<IAccountFeature, bool> predicate) {
			TypedCollectionExposureUtil<IAccountFeature>.RemoveCollectionEntry(predicate, this.AppliedFeatures);
		}

		public IAccountFeature GetCollectionEntry(Func<IAccountFeature, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountFeature>.GetCollectionEntry(predicate, this.AppliedFeatures);
		}

		public List<IAccountFeature> GetCollectionEntries(Func<IAccountFeature, bool> predicate) {
			return TypedCollectionExposureUtil<IAccountFeature>.GetCollectionEntries(predicate, this.AppliedFeatures);
		}

		public ImmutableList<IAccountFeature> CollectionCopy => TypedCollectionExposureUtil<IAccountFeature>.GetCollection(this.AppliedFeatures);
	}
}