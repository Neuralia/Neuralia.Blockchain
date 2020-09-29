using System;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Versions {

	public class ComponentVersion : IBinarySerializable, ITreeHashable {
		private byte minor;

		public const byte MAX_COMPRESSED_MAJOR_VERSION = 0xF;
		public const byte MAX_COMPRESSED_MINOR_VERSION = 0x7;
		public const byte MAX_MINOR_VERSION = 0x7F;
		public const byte COMPRESSED_FLAG = 0x80;
		public const byte COMPRESSED_MAJOR_BIT_OFFSET = 3;
		
		public ComponentVersion() : this((ushort)0,(byte)0) {

		}

		public ComponentVersion(AdaptiveShort1_2 major, byte minor) : this(major.Value, minor) {

		}

		public ComponentVersion(ushort major, byte minor) {
			this.Major = major;
			this.Minor = minor;
		}

		public ComponentVersion(string version) : this(version.Replace("(", "").Replace(")", "").Split('.')){
			
		}

		public ComponentVersion(string[] version) : this( ushort.Parse(version[0]), byte.Parse(version[1])) {

		}

		public ComponentVersion(int major, int minor) : this((ushort) major, (byte) minor) {
		}

		public AdaptiveShort1_2 Major { get; }

		public byte Minor {
			get { return this.minor; }
			set {
				if(value > MAX_MINOR_VERSION) {
					throw new ArgumentOutOfRangeException($"Minor can not be larger than {MAX_MINOR_VERSION}", nameof(Minor));
				}

				this.minor = value;
			}
		}

		public bool IsVersionSet => (this.Major != 0) || (this.Minor != 0);

		public virtual bool IsNull => !this.IsVersionSet;

		public virtual void Rehydrate(IDataRehydrator rehydrator) {

			byte firstByte = rehydrator.ReadByte();
			
			if((firstByte & COMPRESSED_FLAG) != 1) {
				
				this.Minor = (byte)(firstByte & MAX_COMPRESSED_MINOR_VERSION);
				this.Major.Value = (ushort)((firstByte >> COMPRESSED_MAJOR_BIT_OFFSET) & MAX_COMPRESSED_MAJOR_VERSION);
			} else {
				this.Minor = (byte)(firstByte & ~COMPRESSED_FLAG);
				this.Major.Rehydrate(rehydrator);
			}
		}

		public virtual void Dehydrate(IDataDehydrator dehydrator) {
			
			bool compressed = this.Major.Value <= MAX_COMPRESSED_MAJOR_VERSION && this.Minor <= MAX_COMPRESSED_MINOR_VERSION;

			if(compressed) {
				byte compressedValue = (byte)(this.Minor & MAX_COMPRESSED_MINOR_VERSION);
				compressedValue |= (byte)((this.Major.Value & MAX_COMPRESSED_MAJOR_VERSION) << COMPRESSED_MAJOR_BIT_OFFSET);

				dehydrator.Write(compressedValue);
			} else {
				dehydrator.Write((byte)(this.Minor | COMPRESSED_FLAG));
				this.Major.Dehydrate(dehydrator);
			}
		}

		public virtual HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Major.Value);
			hashNodeList.Add(this.Minor);

			return hashNodeList;
		}

		public static implicit operator ComponentVersion((ushort major, ushort minor) d) {
			return new ComponentVersion(d.major, d.minor);
		}

		public virtual void EnsureEqual(ComponentVersion other) {

			if(!Equals(this.Major, other.Major)) {
				throw new ApplicationException("Invalid major version");
			}

			if(!Equals(this.Minor, other.Minor)) {
				throw new ApplicationException("Invalid minor version");
			}
		}

		public bool Equals(ComponentVersion other) {
			return this.Major.Equals(other.Major) && this.Minor.Equals(other.Minor);
		}

		public override string ToString() {
			return $"{this.Major.Value}.{this.Minor}";
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj is ComponentVersion compoent) {
				return this.Equals(compoent);
			}

			return false;
		}

		public override int GetHashCode() {
			unchecked {
				return ((this.Major != null ? this.Major.GetHashCode() : 0) * 397) ^ (this.Minor != null ? this.Minor.GetHashCode() : 0);
			}
		}

		public static bool operator ==(ComponentVersion c1, (int major, int minor) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.Major == c2.major) && (c1.Minor == c2.minor);
		}

		public static bool operator !=(ComponentVersion c1, (int major, int minor) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentVersion c1, (ushort major, byte minor) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.Major == c2.major) && (c1.Minor == c2.minor);
		}

		public static bool operator !=(ComponentVersion c1, (ushort major, byte minor) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentVersion c1, ComponentVersion c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(ComponentVersion c1, ComponentVersion c2) {
			return !(c1 == c2);
		}
	}

	public class ComponentVersion<T> : ComponentVersion
		where T : SimpleUShort<T>, new() {

		public ComponentVersion() {
			this.Type = new ComponentType<T>();

		}

		public ComponentVersion(ComponentType<T> type, AdaptiveShort1_2 major, AdaptiveShort1_2 minor) : base(major, minor) {
			this.Type = type;
		}

		public ComponentVersion(T type, ushort major, ushort minor) : base(major, minor) {
			this.Type = new ComponentType<T> {Value = type};
		}

		public ComponentVersion(string version) : base(version.Replace("(", "").Replace(")", "").Split('.').Skip(1).ToArray()) {

			string[] entries = version.Replace("(", "").Replace(")", "").Split('.');

			this.Type = new ComponentType<T> {Value = new T {Value = ushort.Parse(entries[0])}};
		}

		public ComponentVersion(T type, int major, int minor) : this(type, (ushort) major, (ushort) minor) {
		}

		public ComponentType<T> Type { get; }

		public bool IsTypeSet => this.Type.Value.Value != 0;

		public override bool IsNull => !this.IsTypeSet || !this.IsVersionSet;

		public override void Rehydrate(IDataRehydrator rehydrator) {

			base.Rehydrate(rehydrator);

			this.Type.Rehydrate(rehydrator);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			this.Type.Dehydrate(dehydrator);
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = base.GetStructuresArray();

			hashNodeList.Add(this.Type.Value.Value);

			return hashNodeList;
		}

		public static implicit operator ComponentVersion<T>((T type, ushort major, ushort minor) d) {
			return new ComponentVersion<T>(d.type, d.major, d.minor);
		}

		public void EnsureEqual(ComponentVersion<T> other) {
			if(!Equals(this.Type, other.Type)) {
				throw new ApplicationException("Invalid type");
			}

			base.EnsureEqual(other);
		}

		public bool Equals(ComponentVersion<T> other) {

			if(ReferenceEquals(null, other)) {
				return false;
			}

			if(ReferenceEquals(this, other)) {
				return true;
			}

			return this.Type.Equals(other.Type) && base.Equals(other);
		}

		public override string ToString() {
			return $"{this.Type.Value.Value}.{this.Major.Value}.{this.Minor}";
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj is ComponentVersion<T> compoent) {
				return this.Equals(compoent);
			}

			return false;
		}

		public override int GetHashCode() {
			unchecked {
				return (base.GetHashCode() * 397) ^ (this.Type != null ? this.Type.GetHashCode() : 0);
			}
		}

		public static bool operator ==(ComponentVersion<T> c1, (int major, int minor) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (ComponentVersion) c1 == c2;
		}

		public static bool operator !=(ComponentVersion<T> c1, (int major, int minor) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentVersion<T> c1, (ushort major, byte minor) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (ComponentVersion) c1 == c2;
		}

		public static bool operator !=(ComponentVersion<T> c1, (ushort major, byte minor) c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentVersion<T> c1, T c2) {
			return !ReferenceEquals(null, c1) && c1.Type.Equals(c2);

		}

		public static bool operator !=(ComponentVersion<T> c1, T c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(ComponentVersion<T> c1, ComponentVersion<T> c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(ComponentVersion<T> c1, ComponentVersion<T> c2) {
			return !(c1 == c2);
		}
	}
}