using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Encodings;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types {
	/// <summary>
	///     Account IDs on the blockchain.  we can go as high as 0xFFFFFFFFFFFFFFL.
	/// </summary>
	[JsonConverter(typeof(AccountIdJsonConverter))]
	public class AccountId : IBinarySerializable, ITreeHashable, IComparable<AccountId> {

		private const long SEQUENCE_MASK = 0xFFFFFFFFFFFFFFL; // the maximum amount of accounts we support by type
		
		public const string REGEX = @"^[{]?(?:(?:USER|SERVER|MODERATOR|JOINT)\:|(?:USR|SVR|JT|MOD)\:|[USJM*][:]?)(?:[0-9A-Z][-]?)+[}]?$";
		public const string GROUPED_REGEX = @"(?<prefix>^[{]?)(?<type>(?:(?:USER|SERVER|MODERATOR|JOINT)\:|(?:USR|SVR|JT|MOD)\:|[USJM*][:]?))(?<code>(?:[0-9A-Z][-]?)+)(?<suffix>[}]?$)";

		public const string REGEX_PRESENTATION = "[{]?[*]";
		public const string REGEX_VALID_NO_PRESENTATION_CORE = @"[{]?(?:(?:USER|SERVER|MODERATOR|JOINT)\:|(?:USR|SVR|JT|MOD)\:|USJM][:]?)(?:[0-9A-Z][-]?)+[}]?";
		public const string REGEX_VALID_NO_PRESENTATION = "^"+ REGEX_VALID_NO_PRESENTATION_CORE + "$";
		

		public const byte MAX_ACCOUNT_TYPE = 0x7F; // up to 127 account types.
		private const byte ACCOUNT_TYPE_MASK = MAX_ACCOUNT_TYPE;
		
		private const int CHUNK_SIZE = 4;
		public const char DIVIDER = '-';
		public const char ACCOUNT_TYPE_DIVIDER = ':';

		public const char DEFAULT_USER_ACCOUNT_TOKEN = 'U';
		public const char DEFAULT_SERVER_ACCOUNT_TOKEN = 'S';
		public const char DEFAULT_JOINT_ACCOUNT_TOKEN = 'J';
		public const char DEFAULT_MODERATOR_ACCOUNT_TOKEN = 'M';
		public const char DEFAULT_PRESENTATION_TOKEN = '*';
		
		public static readonly string[] USER_ACCOUNT_TOKENS = {DEFAULT_USER_ACCOUNT_TOKEN.ToString(), "USR", "USER", ((byte) Enums.AccountTypes.User).ToString()};
		public static readonly string[] SERVER_ACCOUNT_TOKENS = {DEFAULT_SERVER_ACCOUNT_TOKEN.ToString(), "SVR", "SERVER", ((byte) Enums.AccountTypes.Server).ToString()};
		public static readonly string[] JOINT_ACCOUNT_TOKENS = {DEFAULT_JOINT_ACCOUNT_TOKEN.ToString(), "JT", "JOINT", ((byte) Enums.AccountTypes.Joint).ToString()};
		public static readonly string[] PRESENTATION_ACCOUNT_TOKENS = {DEFAULT_PRESENTATION_TOKEN.ToString(), ((byte) Enums.AccountTypes.Presentation).ToString()};
		public static readonly string[] MODERATOR_ACCOUNT_TOKENS = {DEFAULT_MODERATOR_ACCOUNT_TOKEN.ToString(), "MOD", "MODERATOR", ((byte) Enums.AccountTypes.Moderator).ToString()};
		
		private readonly AdaptiveLong2_9 sequenceId = new AdaptiveLong2_9();

		static AccountId() {

		}

		public AccountId(long sequenceId, Enums.AccountTypes accountType) {
			this.SequenceId = sequenceId;
			this.AccountType = accountType;
		}

		public AccountId(long sequenceId, byte accountType) : this(sequenceId, (Enums.AccountTypes) accountType) {

		}

		public AccountId(AccountId other) : this(other?.SequenceId??0, other?.AccountType??Enums.AccountTypes.Unknown ) {
		}

		public AccountId(string stringAccountId) : this(FromString(stringAccountId)) {
		}

		public AccountId() {
		}

		public long SequenceId {
			get => this.sequenceId.Value;
			set => this.sequenceId.Value = value & SEQUENCE_MASK;
		}

		public Enums.AccountTypes AccountType { get; set; } = Enums.AccountTypes.Unknown;

		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public byte AccountTypeRaw {
			get => (byte) this.AccountType;
			set => this.AccountType = (Enums.AccountTypes) value;
		}
		
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public (long sequenceId, Enums.AccountTypes accountType) Components => (this.SequenceId, this.AccountType);

		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public Tuple<long, Enums.AccountTypes> Tuple => new Tuple<long, Enums.AccountTypes>(this.SequenceId, this.AccountType);

		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public Tuple<long, byte> TupleRaw => new Tuple<long, byte>(this.SequenceId, this.AccountTypeRaw);
		
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public AccountId Clone => new AccountId(this);

		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public static AccountId LargestAddress => new AccountId(long.MaxValue & SEQUENCE_MASK, Enums.AccountTypes.Unknown);

		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsValid => this.AccountType != Enums.AccountTypes.Unknown && IsAcceptedAccountType(this.AccountType);
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsPresentation => IsPresentationAccountType(this.AccountType);
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsUser => IsUserAccountType(this.AccountType);
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsServer => IsServerAccountType(this.AccountType);
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsModerator => IsModeratorAccountType(this.AccountType);
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsJoint => IsJointAccountType(this.AccountType);

		public static bool IsUserAccountType(Enums.AccountTypes accountType) {
			return accountType == Enums.AccountTypes.User;
		}
		
		public static bool IsServerAccountType(Enums.AccountTypes accountType) {
			return accountType == Enums.AccountTypes.Server;
		}
		
		public static bool IsJointAccountType(Enums.AccountTypes accountType) {
			return accountType == Enums.AccountTypes.Joint;
		}
		
		public static bool IsPresentationAccountType(Enums.AccountTypes accountType) {
			return accountType == Enums.AccountTypes.Presentation;
		}
		
		public static bool IsModeratorAccountType(Enums.AccountTypes accountType) {
			return accountType == Enums.AccountTypes.Moderator;
		}
		
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public bool IsStandard => IsStandardAccountType(this.AccountType);
		

		public static bool IsStandardAccountType(Enums.AccountTypes accountType) {
			return StandardAccountTypesSet.Contains(accountType);
		}
		
		public static bool IsAcceptedAccountType(Enums.AccountTypes accountType) {
			return AcceptedAccountTypesSet.Contains(accountType);
		}
		
		public static HashSet<Enums.AccountTypes> StandardAccountTypesSet => new []{ Enums.AccountTypes.User, Enums.AccountTypes.Server, Enums.AccountTypes.Moderator}.ToHashSet();
		public static HashSet<Enums.AccountTypes> AcceptedAccountTypesSet => new []{ Enums.AccountTypes.User, Enums.AccountTypes.Server, Enums.AccountTypes.Moderator, Enums.AccountTypes.Joint, Enums.AccountTypes.Presentation}.ToHashSet();
		public static Enums.AccountTypes[] StandardAccountTypes => StandardAccountTypesSet.ToArray();
		
		
		public void Dehydrate(IDataDehydrator dehydrator) {

			bool simpleType = this.AccountType == Enums.AccountTypes.Unknown;

			if(dehydrator == null) {
				throw new ArgumentNullException(nameof(dehydrator));
			}

			dehydrator.Write(simpleType);

			if(!simpleType) {
				dehydrator.Write((byte) this.AccountType);
			}

			this.sequenceId.Dehydrate(dehydrator);

		}

		public void Rehydrate(IDataRehydrator rehydrator) {
			if(rehydrator == null) {
				throw new ArgumentNullException(nameof(rehydrator));
			}

			bool simpleType = rehydrator.ReadBool();

			if(!simpleType) {
				this.AccountType = rehydrator.ReadByteEnum<Enums.AccountTypes>();
			}

			this.sequenceId.Rehydrate(rehydrator);
		}

		public int CompareTo(AccountId other) {
			if(ReferenceEquals(null, other)) {
				return 1;
			}

			if(this.AccountType == other.AccountType) {
				return this.SequenceId.CompareTo(other.SequenceId);
			}

			return this.AccountTypeRaw.CompareTo(other.AccountTypeRaw);
		}

		public HashNodeList GetStructuresArray() {
			HashNodeList hashNodeList = new HashNodeList();

			hashNodeList.Add(this.SequenceId);
			hashNodeList.Add(this.AccountType);

			return hashNodeList;
		}

		public static explicit operator AccountId((long sequenceId, Enums.AccountTypes accountType) entry) {
			return new AccountId(entry.sequenceId, entry.accountType);
		}

		public static explicit operator AccountId((BigInteger sequenceId, Enums.AccountTypes accountType) entry) {
			return new AccountId((long) entry.sequenceId, entry.accountType);
		}

		public static explicit operator (long sequenceId, Enums.AccountTypes accountType)(AccountId entry) {
			return (entry.SequenceId, entry.AccountType);
		}
		
		public static implicit operator AccountId(string value) {
			return FromString(value);
		}

		/// <summary>
		/// If the string is a presentation account
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool IsStringPresentation(string value) {
			if (string.IsNullOrEmpty(value)) 
				return false;
			
			try {
				var regex = new Regex(REGEX_PRESENTATION, RegexOptions.IgnoreCase);
				return regex.IsMatch(value);
			}
			catch {
				// nothing to do
			}
			return false;
		}
		
		/// <summary>
		/// If the string is a valid account Id, but NOT a presentation account
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		public static bool IsStringValidNoPresentation(string value) {
			if (string.IsNullOrEmpty(value)) 
				return false;
			
			try {
				var regex = new Regex(REGEX_VALID_NO_PRESENTATION, RegexOptions.IgnoreCase);
				return regex.IsMatch(Base32.Prepare(value));
			}
			catch {
				// nothing to do
			}
			return false;
		}

		public static bool IsValidString(string value) {
			if (string.IsNullOrEmpty(value)) 
				return false;
			
			try {
				var replaced = Regex.Replace(value, GROUPED_REGEX, m => {
					return m.Groups["prefix"].Value + m.Groups["type"].Value + Base32.Prepare(m.Groups["code"].Value) + m.Groups["suffix"].Value;
				}, RegexOptions.IgnoreCase);
				
				var regex = new Regex(REGEX, RegexOptions.IgnoreCase);
				return regex.IsMatch(replaced);
			}
			catch {
				// nothing to do
			}
			return false;
		}

		private static Enums.AccountTypes GetAccountType(string type) {
			if(string.IsNullOrWhiteSpace(type)) {
				return Enums.AccountTypes.Unknown;
			}
			type = type.ToUpper();
			if(USER_ACCOUNT_TOKENS.Contains(type)) {
				return Enums.AccountTypes.User;
			}else if(SERVER_ACCOUNT_TOKENS.Contains(type)) {
				return Enums.AccountTypes.Server;
			}  else if(JOINT_ACCOUNT_TOKENS.Contains(type)) {
				return Enums.AccountTypes.Joint;
			} else if(PRESENTATION_ACCOUNT_TOKENS.Contains(type)) {
				return Enums.AccountTypes.Presentation;
			} else if(MODERATOR_ACCOUNT_TOKENS.Contains(type)) {
				return Enums.AccountTypes.Moderator;
			} else {
				return Enums.AccountTypes.Unknown;
			}
		}
		public static AccountId FromString(string value) {
			if(!IsValidString(value)) {
				return null;
			}

			Regex regex = new Regex(GROUPED_REGEX, RegexOptions.IgnoreCase);
			Match match = regex.Match(value);

			var type = match.Groups["type"].Value.Replace(ACCOUNT_TYPE_DIVIDER.ToString(), "");
			var code = match.Groups["code"].Value.Replace(DIVIDER.ToString(), "");

			if(string.IsNullOrWhiteSpace(type) || string.IsNullOrWhiteSpace(code)) {
				return new AccountId(0, Enums.AccountTypes.Unknown);
			}

			using SafeArrayHandle array = SafeArrayHandle.FromBase32(code);

			Span<byte> fullArray = stackalloc byte[sizeof(long)];
			array.Span.CopyTo(fullArray);
			TypeSerializer.Deserialize(fullArray, out long sequenceId);

			return new AccountId(sequenceId, GetAccountType(type));
		}

		public override string ToString() {

			return this.ToString(true, true);
		}

		public string ToString(bool includeDividers, bool includeAccolades) {
			
			// display accountId as a base32 string, divided in groups of 4 characters

			string result = "";

			if(this.SequenceId != 0) {
				// skip all high entries that are 0
				string base32 = NumberBaser.ToBase32(this.SequenceId);

				char[] chars = base32.ToCharArray().ToArray();
				char? divider = null;

				if(includeDividers) {
					divider = DIVIDER;
				}

				string splitBase32 = string.Join(divider == null ? "" : divider.ToString(), Enumerable.Range(0, (int) Math.Ceiling((double) base32.Length / CHUNK_SIZE)).Select(i => new string(chars.Skip(i * CHUNK_SIZE).Take(CHUNK_SIZE).ToArray())));

				result = $"{this.GetAccountIdentifier()}{new string(splitBase32.ToCharArray().ToArray())}";
			}

			if(includeAccolades) {
				result = $"{{{result}}}";
			}

			return result;
		}

		private string GetAccountIdentifier() {
			if(this.IsUser) {
				return DEFAULT_USER_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.IsServer) {
				return DEFAULT_SERVER_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.IsJoint) {
				return DEFAULT_JOINT_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.IsPresentation) {
				return DEFAULT_PRESENTATION_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.IsModerator) {
				return DEFAULT_MODERATOR_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			throw new ApplicationException("Invalid account type");
		}

		/// <summary>
		///     a compact representation of the transaction Id. not meant for humans but for machines only
		/// </summary>
		/// <remarks>since this is used for Id only, we do not include the extended components</remarks>
		/// <returns></returns>
		public string ToCompactString() {

			return $"{this.GetAccountIdentifier()}{NumberBaser.ToBase94(this.SequenceId)}";

		}

		/// <summary>
		///     Parse a compact string representation
		/// </summary>
		/// <param name="compact"></param>
		/// <returns></returns>
		public static AccountId FromCompactString(string compact) {
			if(string.IsNullOrWhiteSpace(compact)) {
				return null;
			}
			
			Enums.AccountTypes accountType = Enums.AccountTypes.Unknown;
			
			string accountSequence = compact.Substring(1, compact.Length - 1);

			using SafeArrayHandle buffer = SafeArrayHandle.FromBase94(accountSequence);
			Span<byte> fullbuffer = stackalloc byte[sizeof(long)];
			buffer.Entry.CopyTo(fullbuffer);

			TypeSerializer.Deserialize(fullbuffer, out long resoultSequenceId);

			return new AccountId(resoultSequenceId, GetAccountType(char.ToUpper(compact[0], CultureInfo.InvariantCulture).ToString()));
		}

		protected bool Equals(AccountId other) {
			return Equals(this.sequenceId, other.sequenceId) && (this.AccountType == other.AccountType);
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
			
			if(obj is string str) {
				return this == str;
			}
			
			if(obj is long longValue) {
				return this == longValue;
			}

			return this.Equals((AccountId) obj);
		}

		public override int GetHashCode() {
			unchecked {
				return ((this.sequenceId != null ? this.sequenceId.GetHashCode() : 0) * 397) ^ (int) this.AccountType;
			}
		}

		/// <summary>
		///     Converts our two components into a single long
		/// </summary>
		/// <returns></returns>
		public long ToLongRepresentation() {
			long longForm = this.SequenceId & SEQUENCE_MASK;

			long type = this.AccountTypeRaw & ACCOUNT_TYPE_MASK;

			longForm |= type << (8 * 7);

			return longForm;
		}

		public static long? ToNLongRepresentation(AccountId accountId) {
			if((accountId == default(AccountId)) || (accountId.SequenceId == 0)) {
				return null;
			}

			return accountId.ToLongRepresentation();
		}

		public static AccountId FromLongRepresentation(long? longForm) {
			if(!longForm.HasValue) {
				return null;
			}

			return FromLongRepresentation(longForm.Value);
		}

		public static AccountId FromLongRepresentation(long longForm) {
			if(longForm == 0) {
				return new AccountId(0, Enums.AccountTypes.Unknown);
			}

			long sequence = longForm & SEQUENCE_MASK;

			byte accountType = (byte) (((longForm & ~SEQUENCE_MASK) >> (8 * 7)) & byte.MaxValue);

			if(accountType == 0) {
				throw new InvalidOperationException("Account type must be a valid value");
			}

			return new AccountId(sequence, (Enums.AccountTypes) accountType);
		}

		public static bool operator ==(AccountId c1, AccountId c2) {
			if(ReferenceEquals(null, c1)) {
				return ReferenceEquals(null, c2);
			}

			if(ReferenceEquals(null, c2)) {
				return false;
			}

			return c1.Equals(c2);

		}

		public static bool operator !=(AccountId c1, AccountId c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(AccountId c1, (long sequenceId, Enums.AccountTypes accountType) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.SequenceId == c2.sequenceId) && (c1.AccountType == c2.accountType);
		}

		public static bool operator !=(AccountId c1, (long sequenceId, Enums.AccountTypes accountType) c2) {
			return !(c1 == c2);
		}
		
		public static bool operator ==(AccountId c1, long c2) {
			
			return (c1?.ToLongRepresentation()??0) == c2;
		}

		public static bool operator !=(AccountId c1,long c2) {
			return !(c1 == c2);
		}
		
		public static bool operator ==(AccountId c1, string c2) {

			return c1 == FromString(c2);
		}

		public static bool operator !=(AccountId c1, string c2) {
			return !(c1 == c2);
		}

		public static bool operator ==(AccountId c1, (int sequenceId, Enums.AccountTypes accountType) c2) {
			if(ReferenceEquals(null, c1)) {
				return false;
			}

			return (c1.SequenceId == c2.sequenceId) && (c1.AccountType == c2.accountType);
		}

		public static bool operator !=(AccountId c1, (int sequenceId, Enums.AccountTypes accountType) c2) {
			return !(c1 == c2);
		}

		public static bool operator <(AccountId a, AccountId b) {

			return a.ToLongRepresentation() < b.ToLongRepresentation();
		}

		public static bool operator <=(AccountId a, AccountId b) {
			return (a == b) || (a < b);
		}

		public static bool operator >(AccountId a, AccountId b) {
			return a.ToLongRepresentation() > b.ToLongRepresentation();
		}

		public static bool operator >=(AccountId a, AccountId b) {
			return (a == b) || (a > b);
		}

		public static (long sequenceId, Enums.AccountTypes accountType) To(AccountId left, AccountId right) {
			throw new NotImplementedException();
		}
	}

	public static class AccountIdExtensions {
		public static long? ToNLongRepresentation(this AccountId accountId) {
			return AccountId.ToNLongRepresentation(accountId);
		}

		public static AccountId ToAccountId(this long value) {
			return AccountId.FromLongRepresentation(value);
		}

		public static AccountId ToAccountId(this long? value) {
			return AccountId.FromLongRepresentation(value);
		}
	}
}