using System;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Versions {
	/// <summary>
	///     Simple structure to represent our client version
	/// </summary>
	public class SoftwareVersion : IBinarySerializable, IEquatable<SoftwareVersion>, ITreeHashable {

		private readonly Func<SoftwareVersion, bool> validateVersion;

		public SoftwareVersion() {

		}

		public SoftwareVersion(string version) {

			string[] parts = version.Split('.', ',');

			int major = int.Parse(parts[0]);
			int minor = int.Parse(parts[1]);
			int revision = int.Parse(parts[2]);

			// split th tag if applicable
			int build = 0;
			string tag = "";

			if(parts[3].Contains("(")) {

				string[] subparts = parts[3].Split('(');

				build = int.Parse(subparts[0]);

				subparts[1] = subparts[1].Replace("(", "").Replace(")", "");

				tag = subparts[1];

			} else {
				build = int.Parse(parts[3]);
			}

			this.SetVersion(major, minor, revision, build, tag);
		}

		public SoftwareVersion(int major, int minor, int revision, int build, Func<SoftwareVersion, SoftwareVersion, bool> versionValidationCallback) : this(major, minor, revision, build, "", versionValidationCallback) {

		}

		public SoftwareVersion(int major, int minor, int revision, int build, string tag = "", Func<SoftwareVersion, SoftwareVersion, bool> versionValidationCallback = null) {
			this.SetVersion(major, minor, revision, build, tag);
			this.VersionValidationCallback = versionValidationCallback;
		}

		public SoftwareVersion(SoftwareVersion other, Func<SoftwareVersion, SoftwareVersion, bool> versionValidationCallback = null) : this(other.Major, other.Minor, other.Revision, other.Build, versionValidationCallback) {
		}

		public Func<SoftwareVersion, SoftwareVersion, bool> VersionValidationCallback { get; }
		public int Major { get; private set; }
		public int Minor { get; private set; }
		public int Build { get; private set; }
		public int Revision { get; private set; }
		public string Tag { get; set; } = "";

		public bool IsEmpty => (this.Major == 0) && (this.Minor == 0) && (this.Revision == 0) && (this.Build == 0);
		public bool IsSet => !this.IsEmpty;

		public void Dehydrate(IDataDehydrator dehydrator) {
			dehydrator.Write(this.Major);
			dehydrator.Write(this.Minor);
			dehydrator.Write(this.Revision);
			dehydrator.Write(this.Build);
			dehydrator.Write(this.Tag);
		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			int major = rehydrator.ReadInt();
			int minor = rehydrator.ReadInt();
			int revision = rehydrator.ReadInt();
			int build = rehydrator.ReadInt();
			string tag = rehydrator.ReadString();

			this.SetVersion(major, minor, revision, build, tag);
		}

		public bool Equals(SoftwareVersion other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			if(ReferenceEquals(this, other)) {
				return true;
			}

			return (this.Build == other.Build) && (this.Major == other.Major) && (this.Revision == other.Revision) && (this.Minor == other.Minor);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.Major);
			hashNodeList.Add(this.Minor);
			hashNodeList.Add(this.Revision);
			hashNodeList.Add(this.Build);

			return hashNodeList;
		}

		public void SetVersion(SoftwareVersion other) {
			this.SetVersion(other.Major, other.Minor, other.Revision, other.Build, other.Tag);
		}

		public void SetVersion(int Major, int Minor, int revision, int build, string tag = "") {
			this.Major = Major;
			this.Minor = Minor;
			this.Revision = revision;
			this.Build = build;
			this.Tag = tag;
		}

		public override string ToString() {
			string version = $"{this.Major}.{this.Minor}.{this.Revision}.{this.Build}";

			if(!string.IsNullOrWhiteSpace(this.Tag)) {
				version += $" ({this.Tag})";
			}

			return version;
		}

		public static bool operator ==(SoftwareVersion left, SoftwareVersion right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Equals(right);
		}

		public static bool operator !=(SoftwareVersion left, SoftwareVersion right) {
			return !(left == right);
		}

		public static bool operator <(SoftwareVersion a, SoftwareVersion b) {
			if(a.Major < b.Major) {
				return true;
			}

			if(a.Major > b.Major) {
				return false;
			}

			if(a.Minor < b.Minor) {
				return true;
			}

			if(a.Minor > b.Minor) {
				return false;
			}

			if(a.Revision < b.Revision) {
				return true;
			}

			if(a.Revision > b.Revision) {
				return false;
			}

			if(a.Build < b.Build) {
				return true;
			}

			if(a.Build > b.Build) {
				return false;
			}

			return false;
		}

		public static bool operator <=(SoftwareVersion a, SoftwareVersion b) {
			return (a == b) || (a < b);
		}

		public static bool operator >(SoftwareVersion a, SoftwareVersion b) {
			if(a.Major < b.Major) {
				return false;
			}

			if(a.Major > b.Major) {
				return true;
			}

			if(a.Minor < b.Minor) {
				return false;
			}

			if(a.Minor > b.Minor) {
				return true;
			}

			if(a.Revision < b.Revision) {
				return false;
			}

			if(a.Revision > b.Revision) {
				return true;
			}

			if(a.Build < b.Build) {
				return false;
			}

			if(a.Build > b.Build) {
				return true;
			}

			return false;
		}

		public static bool operator >=(SoftwareVersion a, SoftwareVersion b) {
			return (a == b) || (a > b);
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

			return this.Equals((SoftwareVersion) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.Major.GetHashCode();
				hashCode = (hashCode * 397) ^ this.Minor.GetHashCode();
				hashCode = (hashCode * 397) ^ this.Revision.GetHashCode();
				hashCode = (hashCode * 397) ^ this.Build.GetHashCode();

				return hashCode;
			}
		}

		/// <summary>
		///     Here we determine if a version is acceptable to ours. over time, older version will be rejected here
		/// </summary>
		/// <param name="version"></param>
		/// <returns></returns>
		public bool IsVersionAcceptable(SoftwareVersion version) {
			return this.VersionValidationCallback(this, version);
		}
	}
}