using System;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Serialization;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers {
	
	/// <summary>
	/// 
	/// </summary>
	/// <remarks>takes a bit if zero (most of the time). a single byte from 1 to 127 and then two bytes for up to the max value of 32767</remarks>
	public class TransactionScope : AdaptiveShort1_2, IComparable<TransactionScope> {

		public TransactionScope() {
		}

		public TransactionScope(short value) : base(ConvertUshort(value)) {
		}

		public TransactionScope(ushort value) : this(CapUShort(value)) {
		}
		
		public TransactionScope(TransactionScope other) : this(other.Value) {
		}
		
		public TransactionScope(string scope) : this(ushort.Parse(scope)) {

		}

		public bool IsZero => this.Value == 0;
		public bool IsNotZero => !this.IsZero;
		
		public new short Value {
			get => (short)base.Value;
			set {
				ushort val = ConvertUshort(value);
				this.TestMaxSize(val);
				base.Value = val;
			}
		}

		/// <summary>
		/// voncert to a ushort, ensure negatives a 0
		/// </summary>
		/// <param name="value"></param>
		/// <returns></returns>
		private static ushort ConvertUshort(short value) {
			return (ushort) Math.Max(value, (short) 0);
		}
		
		private static short CapUShort(ushort value) {
			return (short) Math.Min(value, short.MaxValue);
		}
		
		[JsonIgnore]
		public TransactionScope Clone => new TransactionScope(this);

		public int CompareTo(TransactionScope other) {
			return this.Value.CompareTo(other.Value);
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {

			dehydrator.Write(this.IsZero);

			if(this.IsNotZero) {
				base.Dehydrate(dehydrator);
			}
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {

			this.Value = 0;
			bool zero = rehydrator.ReadBool();

			if(!zero) {
				base.Rehydrate(rehydrator);
			}
		}

		public override bool Equals(object obj) {
			if(obj is TransactionScope other) {
				return this.Value == other.Value;
			}

			return base.Equals(obj);
		}

		public override int GetHashCode() {
			return this.Value.GetHashCode();
		}

		public override string ToString() {
			return this.Value.ToString();
		}

		public static implicit operator TransactionScope(short value) {
			return new TransactionScope(value);
		}
		
		public static implicit operator TransactionScope(int value) {
			return new TransactionScope((short)value);
		}

		public static implicit operator TransactionScope(long value) {
			return new TransactionScope((short)value);
		}
		
		public static implicit operator short(TransactionScope value) {
			return value.Value;
		}
		
		public static bool operator ==(TransactionScope left, short right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Value == right;
		}

		public static bool operator !=(TransactionScope left, short right) {
			return !(left == right);
		}
		
		public static bool operator ==(TransactionScope left, ushort right) {
			if(ReferenceEquals(null, left)) {
				return false;
			}

			return left.Value == right;
		}

		public static bool operator !=(TransactionScope left, ushort right) {
			return !(left == right);
		}
	}
}