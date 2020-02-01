using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Versions {

	public interface IVersionableSerializable :  IBinarySerializable, ITreeHashable {
		IBinarySerializable BaseVersion { get; }
	}

	public interface IBaseVersionable<V> :  ISerializableCombo
		where V : ComponentVersion {
		V Version { get; }
	}

	public interface IBaseVersionable : IBaseVersionable<ComponentVersion> {
	}

	public interface IBaseTypedVersionable<T> : IBaseVersionable<ComponentVersion<T>>
		where T : SimpleUShort<T>, new() {
	}

	public interface IVersionable : IBaseVersionable {
	}

	public interface IVersionable<T> : IBaseTypedVersionable<T>
		where T : SimpleUShort<T>, new() {
	}

	/// <summary>
	///     a base class for versioned components
	/// </summary>
	public abstract class VersionableBase<V> : IBaseVersionable<V>
		where V : ComponentVersion, new() {

		public VersionableBase() {
			this.Version = this.SetIdentity();

			if(this.Version.IsNull) {
				throw new ApplicationException("Version has not been set for this component");
			}
		}

		public V Version { get; protected set; }

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			V rehydratedVersion = rehydrator.Rehydrate<V>();
			this.Version.EnsureEqual(rehydratedVersion);
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList nodeList = new HashNodeList();

			nodeList.Add(this.Version);

			return nodeList;
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			this.Version.Dehydrate(dehydrator);
		}

		public virtual void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			jsonDeserializer.SetProperty("Version", this.Version);
		}

		protected abstract V SetIdentity();
	}

	/// <summary>
	///     a base class for versioned components
	/// </summary>
	public abstract class Versionable<T> : VersionableBase<ComponentVersion<T>>, IVersionable<T>
		where T : SimpleUShort<T>, new() {
	}

	public abstract class Versionable : VersionableBase<ComponentVersion>, IVersionable {
	}
}