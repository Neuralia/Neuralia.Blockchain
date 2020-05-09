using System;
using System.Globalization;
using System.Linq;
using System.Numerics;
using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Json.Converters;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Cryptography.Encodings;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General.Types {
	/// <summary>
	///     Account IDs on the blockchain.  we can go as high as 0xFFFFFFFFFFFFFFL.
	/// </summary>
	[JsonConverter(typeof(AccountIdJsonConverter))]
	public class AccountId : IBinarySerializable, ITreeHashable, IComparable<AccountId> {

		private const long SEQUENCE_MASK = 0xFFFFFFFFFFFFFFL; // the maximum amount of accounts we support by type
		
		public const string REGEX = @"^[{]?(?:(?:STANDARD|JOINT)\:|(?:ST|JT)\:|[SJ*][:]?)(?:[0-9A-Z][-]?)+[}]?$";
		public const string REGEX_PRESENTATION = "[{]?[*]";
		public const string REGEX_VALID_NO_PRESENTATION_CORE = @"[{]?(?:(?:STANDARD|JOINT)\:|(?:ST|JT)\:|[SJ][:]?)(?:[0-9A-Z][-]?)+[}]?";
		public const string REGEX_VALID_NO_PRESENTATION = "^"+ REGEX_VALID_NO_PRESENTATION_CORE + "$";
		

		public const byte MAX_ACCOUNT_TYPE = 0x7F; // up to 127 account types.
		private const byte ACCOUNT_TYPE_MASK = MAX_ACCOUNT_TYPE;
		
		private const int CHUNK_SIZE = 4;
		public const char DIVIDER = '-';
		public const char ACCOUNT_TYPE_DIVIDER = ':';

		public const char DEFAULT_STANDARD_ACCOUNT_TOKEN = 'S';
		public const char DEFAULT_JOINT_ACCOUNT_TOKEN = 'J';
		public const char DEFAULT_PRESENTATION_TOKEN = '*';

		public static readonly char[] STANDARD_ACCOUNT_TOKENS_CHAR = {DEFAULT_STANDARD_ACCOUNT_TOKEN, ((byte) Enums.AccountTypes.Standard).ToString()[0]};
		public static readonly char[] JOINT_ACCOUNT_TOKENS_CHAR = {DEFAULT_JOINT_ACCOUNT_TOKEN, ((byte) Enums.AccountTypes.Joint).ToString()[0]};
		public static readonly char[] PRESENTATION_CHAR = {DEFAULT_PRESENTATION_TOKEN, ((byte) Enums.AccountTypes.Presentation).ToString()[0]};
		
		public static readonly string[] STANDARD_ACCOUNT_TOKENS = {DEFAULT_STANDARD_ACCOUNT_TOKEN.ToString(), "ST", "STANDARD", ((byte) Enums.AccountTypes.Standard).ToString()};
		public static readonly string[] JOINT_ACCOUNT_TOKENS = {DEFAULT_JOINT_ACCOUNT_TOKEN.ToString(), "JT", "JOINT", ((byte) Enums.AccountTypes.Joint).ToString()};
		public static readonly string[] DEFAULT_PRESENTATION_TOKENS = {DEFAULT_PRESENTATION_TOKEN.ToString(), ((byte) Enums.AccountTypes.Presentation).ToString()};

		private static readonly Regex ACCOUNT_ID_PATTERN = new Regex($"[\\{{\\}}\\[\\]{DIVIDER}]");

		private readonly AdaptiveLong2_9 sequenceId = new AdaptiveLong2_9();

		static AccountId() {

		}

		public AccountId(long sequenceId, Enums.AccountTypes accountType) {
			this.SequenceId = sequenceId;
			this.AccountType = accountType;
		}

		public AccountId(long sequenceId, byte accountType) : this(sequenceId, (Enums.AccountTypes) accountType) {

		}

		public AccountId(AccountId other) : this(other?.SequenceId ?? 0, other?.AccountType ?? Enums.AccountTypes.Standard) {
		}

		public AccountId(string stringAccountId) : this(FromString(stringAccountId)) {
		}

		public AccountId() {
		}

		public long SequenceId {
			get => this.sequenceId.Value;
			set => this.sequenceId.Value = value & SEQUENCE_MASK;
		}

		public Enums.AccountTypes AccountType { get; set; } = Enums.AccountTypes.Standard;

		public byte AccountTypeRaw {
			get => (byte) this.AccountType;
			set => this.AccountType = (Enums.AccountTypes) value;
		}

		public (long sequenceId, Enums.AccountTypes accountType) Components => (this.SequenceId, this.AccountType);

		public Tuple<long, Enums.AccountTypes> Tuple => new Tuple<long, Enums.AccountTypes>(this.SequenceId, this.AccountType);

		public Tuple<long, byte> TupleRaw => new Tuple<long, byte>(this.SequenceId, this.AccountTypeRaw);

		public AccountId Clone => new AccountId(this);

		public static AccountId LargestAddress => new AccountId(long.MaxValue & SEQUENCE_MASK, Enums.AccountTypes.Standard);

		public bool IsPresentation => this.AccountType == Enums.AccountTypes.Presentation;
		
		public void Dehydrate(IDataDehydrator dehydrator) {

			bool simpleType = this.AccountType == Enums.AccountTypes.Standard;

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
				this.AccountType = (Enums.AccountTypes) rehydrator.ReadByte();
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

		public static bool IsValid(string value) {
			if (string.IsNullOrEmpty(value)) 
				return false;
			
			try {
				var regex = new Regex(REGEX, RegexOptions.IgnoreCase);
				return regex.IsMatch(Base32.Prepare(value));
			}
			catch {
				// nothing to do
			}
			return false;
		}
		
		public static AccountId FromString(string value) {
			if(!IsValid(value)) {
				return null;
			}

			string rawString = ACCOUNT_ID_PATTERN.Replace(value, "");

			string accountSequence = "";
			Enums.AccountTypes accountType = Enums.AccountTypes.Standard;

			if(rawString.Contains(":")) {
				// ok, we have a full size type token, let's split
				string[] components = rawString.Split(ACCOUNT_TYPE_DIVIDER);

				if(STANDARD_ACCOUNT_TOKENS.Contains(components[0])) {
					accountType = Enums.AccountTypes.Standard;
				} else if(JOINT_ACCOUNT_TOKENS.Contains(components[0])) {
					accountType = Enums.AccountTypes.Joint;
				} else if(DEFAULT_PRESENTATION_TOKENS.Contains(components[0])) {
					accountType = Enums.AccountTypes.Presentation;
				} else {
					throw new ApplicationException("Invalid account type");
				}

				accountSequence = components[1];
			} else {
				char firstChar = char.ToUpper(rawString[0], CultureInfo.InvariantCulture);

				if(STANDARD_ACCOUNT_TOKENS_CHAR.Contains(firstChar)) {
					accountType = Enums.AccountTypes.Standard;
				} else if(JOINT_ACCOUNT_TOKENS_CHAR.Contains(firstChar)) {
					accountType = Enums.AccountTypes.Joint;
				}else if(PRESENTATION_CHAR.Contains(firstChar)) {
					accountType = Enums.AccountTypes.Presentation;
				} else {
					throw new ApplicationException("Invalid account type");
				}

				accountSequence = rawString.Substring(1, rawString.Length - 1);
			}

			SafeArrayHandle array = ByteArray.FromBase32(accountSequence);

			Span<byte> fullArray = stackalloc byte[sizeof(long)];
			array.Span.CopyTo(fullArray);
			TypeSerializer.Deserialize(fullArray, out long sequenceId);

			return new AccountId(sequenceId, accountType);
		}

		public override string ToString() {

			return this.ToString(true, true);
		}

		public string ToString(bool includeDividers, bool includeAccolades) {
			
			// display accountId as a base32 string, divided in groups of 4 characters

			// skip all high entries that are 0
			string base32 = NumberBaser.ToBase32(this.SequenceId);

			char[] chars = base32.ToCharArray().ToArray();
			char? divider = null;

			if(includeDividers) {
				divider = DIVIDER;
			}

			string splitBase32 = string.Join(divider==null?"":divider.ToString(), Enumerable.Range(0, (int) Math.Ceiling((double) base32.Length / CHUNK_SIZE)).Select(i => new string(chars.Skip(i * CHUNK_SIZE).Take(CHUNK_SIZE).ToArray())));

			string result = $"{this.GetAccountIdentifier()}{new string(splitBase32.ToCharArray().ToArray())}";

			if(includeAccolades) {
				result = $"{{{result}}}";
			}

			return result;
		}

		private string GetAccountIdentifier() {
			if(this.AccountType == Enums.AccountTypes.Standard) {
				return DEFAULT_STANDARD_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.AccountType == Enums.AccountTypes.Joint) {
				return DEFAULT_JOINT_ACCOUNT_TOKEN.ToString(CultureInfo.InvariantCulture);
			}
			else if(this.AccountType == Enums.AccountTypes.Presentation) {
				return DEFAULT_PRESENTATION_TOKEN.ToString(CultureInfo.InvariantCulture);
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

			char firstChar = char.ToUpper(compact[0], CultureInfo.InvariantCulture);

			string accountSequence = "";
			Enums.AccountTypes accountType = Enums.AccountTypes.Standard;

			if(STANDARD_ACCOUNT_TOKENS_CHAR.Contains(firstChar)) {
				accountType = Enums.AccountTypes.Standard;
			} else if(JOINT_ACCOUNT_TOKENS_CHAR.Contains(firstChar)) {
				accountType = Enums.AccountTypes.Joint;
			} else if(PRESENTATION_CHAR.Contains(firstChar)) {
				accountType = Enums.AccountTypes.Presentation;
			} else {
				throw new ApplicationException("Invalid account type");
			}

			accountSequence = compact.Substring(1, compact.Length - 1);

			SafeArrayHandle buffer = ByteArray.FromBase94(accountSequence);
			Span<byte> fullbuffer = stackalloc byte[sizeof(long)];
			buffer.Entry.CopyTo(fullbuffer);

			TypeSerializer.Deserialize(fullbuffer, out long resoultSequenceId);

			return new AccountId(resoultSequenceId, accountType);
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
				throw new InvalidOperationException("Account type must be a valid value");
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