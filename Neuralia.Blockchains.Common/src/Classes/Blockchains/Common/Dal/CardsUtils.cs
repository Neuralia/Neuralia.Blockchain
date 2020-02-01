using System;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.AccountSnapshots.Cards;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {

	public interface ICardUtils {
		void Copy(IChainOptionsSnapshot source, IChainOptionsSnapshot destination);
		void Copy(IAccreditationCertificateSnapshot source, IAccreditationCertificateSnapshot destination);
		void Copy(IAccreditationCertificateSnapshotAccount source, IAccreditationCertificateSnapshotAccount destination);
		void Copy(IAccountKeysSnapshot source, IAccountKeysSnapshot destination);
		void Copy(IJointAccountSnapshot source, IJointAccountSnapshot destination);
		void Copy(IStandardAccountSnapshot source, IStandardAccountSnapshot destination);
		void Copy(IAccountSnapshot source, IAccountSnapshot destination);
		void Copy(IAccountAttribute source, IAccountAttribute destination);
		void Copy(ISnapshot source, ISnapshot destination);
		void Copy(IJointMemberAccount source, IJointMemberAccount destination);

		IChainOptionsSnapshot Clone(IChainOptionsSnapshot source);
		IAccreditationCertificateSnapshot Clone(IAccreditationCertificateSnapshot source);
		IAccreditationCertificateSnapshotAccount Clone(IAccreditationCertificateSnapshotAccount source);
		IAccountKeysSnapshot Clone(IAccountKeysSnapshot source);
		IJointAccountSnapshot Clone(IJointAccountSnapshot source);
		IStandardAccountSnapshot Clone(IStandardAccountSnapshot source);
		IAccountSnapshot Clone(IAccountSnapshot source);
		IAccountAttribute Clone(IAccountAttribute source);
		ISnapshot Clone(ISnapshot source);
		IJointMemberAccount Clone(IJointMemberAccount source);
		
		
		
		// account keys utils
		string GenerateCompositeKey(AccountId accountId, byte ordinal);
		string GenerateCompositeKey(long accountId, byte ordinal);
		string GenerateCompositeKey(IAccountKeysSnapshot accountKey);
	}

	public abstract class CardsUtils : ICardUtils {

		public virtual void Copy(IChainOptionsSnapshot source, IChainOptionsSnapshot destination) {
			destination.MaximumVersionAllowed = source.MaximumVersionAllowed;
			destination.MinimumWarningVersionAllowed = source.MinimumWarningVersionAllowed;
			destination.MinimumVersionAllowed = source.MinimumVersionAllowed;
			destination.MaxBlockInterval = source.MaxBlockInterval;
			destination.AllowGossipPresentations = source.AllowGossipPresentations;
		}

		public virtual void Copy(IAccreditationCertificateSnapshot source, IAccreditationCertificateSnapshot destination) {
			destination.CertificateId = source.CertificateId;
			destination.CertificateVersion = source.CertificateVersion;

			destination.CertificateState = source.CertificateState;
			destination.CertificateType = source.CertificateType;
			destination.ApplicationType = source.ApplicationType;

			destination.EmissionDate = source.EmissionDate;
			destination.ValidUntil = source.ValidUntil;

			destination.AssignedAccount = source.AssignedAccount;
			destination.Application = source.Application;
			destination.Organisation = source.Organisation;
			destination.Url = source.Url;

			destination.CertificateAccountPermissionType = source.CertificateAccountPermissionType;
			destination.PermittedAccountCount = source.PermittedAccountCount;

			this.CopyArray(source, destination);
		}

		public virtual void Copy(IAccreditationCertificateSnapshotAccount source, IAccreditationCertificateSnapshotAccount destination) {

			destination.AccountId = source.AccountId;
		}

		public virtual void Copy(IAccountKeysSnapshot source, IAccountKeysSnapshot destination) {
			destination.CompositeKey = source.CompositeKey;
			destination.OrdinalId = source.OrdinalId;
			destination.AccountId = source.AccountId;
			destination.PublicKey = source.PublicKey;
			destination.DeclarationTransactionId = source.DeclarationTransactionId;

			destination.DeclarationBlockId = source.DeclarationBlockId;
		}

		public virtual void Copy(IJointAccountSnapshot source, IJointAccountSnapshot destination) {
			this.Copy(source, (IAccountSnapshot) destination);

			destination.RequiredSignatures = source.RequiredSignatures;

			this.CopyArray<IJointMemberAccount>(source, destination);
		}

		public virtual void Copy(IStandardAccountSnapshot source, IStandardAccountSnapshot destination) {
			this.Copy(source, (IAccountSnapshot) destination);
		}

		public virtual void Copy(IAccountSnapshot source, IAccountSnapshot destination) {
			destination.AccountId = source.AccountId;
			destination.InceptionBlockId = source.InceptionBlockId;
			destination.CorrelationId = source.CorrelationId;
			destination.TrustLevel = source.TrustLevel;

			this.CopyArray(source, destination);
		}

		public virtual void Copy(IAccountAttribute source, IAccountAttribute destination) {
			destination.CorrelationId = source.CorrelationId;
			destination.AttributeType = source.AttributeType;
			
			destination.Start = source.Start;
			destination.Expiration = source.Expiration;

			destination.Context = null;

			if(source.Context != null) {
				destination.Context = new byte[source.Context.Length];
				Buffer.BlockCopy(source.Context, 0, destination.Context, 0, destination.Context.Length);
			}
		}

		public virtual void Copy(IJointMemberAccount source, IJointMemberAccount destination) {
			destination.AccountId = source.AccountId;
			destination.Required = source.Required;
		}

		public virtual void Copy(ISnapshot source, ISnapshot destination) {

			if(source is IStandardAccountSnapshot castedSource1 && destination is IStandardAccountSnapshot castedDestination1) {
				this.Copy(castedSource1, castedDestination1);
			} else if(source is IJointAccountSnapshot castedSource2 && destination is IJointAccountSnapshot castedDestination2) {
				this.Copy(castedSource2, castedDestination2);
			} else if(source is IAccountKeysSnapshot castedSource3 && destination is IAccountKeysSnapshot castedDestination3) {
				this.Copy(castedSource3, castedDestination3);
			} else if(source is IAccreditationCertificateSnapshot castedSource4 && destination is IAccreditationCertificateSnapshot castedDestination4) {
				this.Copy(castedSource4, castedDestination4);
			} else if(source is IChainOptionsSnapshot castedSource5 && destination is IChainOptionsSnapshot castedDestination5) {
				this.Copy(castedSource5, castedDestination5);
			} else if(source is IAccountAttribute castedSource6 && destination is IAccountAttribute castedDestination6) {
				this.Copy(castedSource6, castedDestination6);
			} else if(source is IJointMemberAccount castedSource7 && destination is IJointMemberAccount castedDestination7) {
				this.Copy(castedSource7, castedDestination7);
			} else if(source is IAccreditationCertificateSnapshotAccount castedSource8 && destination is IAccreditationCertificateSnapshotAccount castedDestination8) {
				this.Copy(castedSource8, castedDestination8);
			} else {
				throw new InvalidOperationException();
			}
		}

		public virtual IChainOptionsSnapshot Clone(IChainOptionsSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IAccreditationCertificateSnapshot Clone(IAccreditationCertificateSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IAccountKeysSnapshot Clone(IAccountKeysSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IJointAccountSnapshot Clone(IJointAccountSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IStandardAccountSnapshot Clone(IStandardAccountSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IAccountSnapshot Clone(IAccountSnapshot source) {
			return this.CopyClone(source);
		}

		public virtual IAccountAttribute Clone(IAccountAttribute source) {
			return this.CopyClone(source);
		}

		public virtual IJointMemberAccount Clone(IJointMemberAccount source) {
			return this.CopyClone(source);
		}

		public virtual IAccreditationCertificateSnapshotAccount Clone(IAccreditationCertificateSnapshotAccount source) {
			return this.CopyClone(source);
		}

		public virtual ISnapshot Clone(ISnapshot source) {
			if(source is IStandardAccountSnapshot castedSource) {
				return this.Clone(castedSource);
			}

			if(source is IJointAccountSnapshot castedJointSource) {
				return this.Clone(castedJointSource);
			}

			if(source is IAccountKeysSnapshot castedKeySource) {
				return this.Clone(castedKeySource);
			}

			if(source is IAccreditationCertificateSnapshot castedCertificateSource) {
				return this.Clone(castedCertificateSource);
			}

			if(source is IChainOptionsSnapshot castedOptionsSource) {
				return this.Clone(castedOptionsSource);
			}

			if(source is IAccountAttribute castedSource6) {
				return this.Clone(castedSource6);
			}

			if(source is IJointMemberAccount castedSource7) {
				return this.Clone(castedSource7);
			}

			if(source is IAccreditationCertificateSnapshotAccount castedSource8) {
				return this.Clone(castedSource8);
			}

			throw new InvalidOperationException();
		}

		protected T GetCopy<T>(T source)
			where T : ISnapshot {
			return (T) Activator.CreateInstance(source.GetType());
		}

		protected T CopyClone<T>(T source)
			where T : ISnapshot {
			T destination = this.GetCopy(source);

			this.Copy(source, destination);

			return destination;
		}

		protected void CopyArray<T>(ITypedCollectionExposure<T> source, ITypedCollectionExposure<T> destination)
			where T : ISnapshot {
			destination.ClearCollection();

			foreach(T entry in source.CollectionCopy) {
				destination.CreateNewCollectionEntry(out T newEntry);

				this.Copy(entry, newEntry);

				destination.AddCollectionEntry(newEntry);
			}
		}


		public string GenerateCompositeKey(AccountId accountId, byte ordinal) {
			return this.GenerateCompositeKey(accountId.ToLongRepresentation(), ordinal);
		}
		
		public string GenerateCompositeKey(long accountId, byte ordinal) {
			return accountId.ToString() + ordinal.ToString();
		}
		public string GenerateCompositeKey(IAccountKeysSnapshot accountKey) {
			return this.GenerateCompositeKey(accountKey.AccountId, accountKey.OrdinalId);
		}
	}
}