using System;
using System.Linq;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core.General.Types.Constants {
	/// <summary>
	///     a special class to represent enums with inheritance and an overlap safety
	/// </summary>
	/// <typeparam name="T"></typeparam>
	public abstract class ConstantSet<T, U>
		where T : ISimpleNumeric<T, U>, new()
		where U : struct, IComparable, IConvertible, IFormattable, IComparable<U>, IEquatable<U> {

		public static readonly T ZERO = new T();
		private readonly T baseOffset;

		private bool childStarted;

		private T counter = new T();

		protected ConstantSet(T baseOffset) {
			this.baseOffset = baseOffset;
		}

		protected ConstantSet(U baseOffset) {
			this.baseOffset = new T();
			this.baseOffset.Value = baseOffset;
		}

		protected void SetOffset(T offset = default) {
			// if we wanted a reserved offset, we got it
			if((offset != null) && offset.GreaterThan(ZERO)) {
				this.counter.PlusEqual(offset);
			}
		}

		protected T CreateBaseConstant(T offset = default) {
			if(this.childStarted) {
				throw new ApplicationException("Base constants can not be created anymore once the children constants have begun");
			}

			this.SetOffset(offset);

			this.counter.Increment();

			if(this.counter.GreaterThan(this.baseOffset)) {
				throw new ApplicationException("The number of constants are higher than the base reserved offset");
			}

			return this.counter.Clone();
		}

		protected T CreateChildConstant(T offset = default) {
			if(this.childStarted == false) {
				// make sure we start from the base reserve and go forward from there
				this.counter.Value = this.baseOffset.Value;
			}

			this.childStarted = true;

			// if we wanted a reserved offset, we got it
			if((offset != null) && offset.GreaterThan(ZERO)) {
				this.counter.Add(offset);
			}

			this.counter.Increment();

			// ensure that children
			return this.counter.Clone();
		}

		protected void PrintValues(string terminator = "") {
			bool skipped = false;
			Console.WriteLine("=====================================");
			Console.WriteLine($"printing values for {this.GetType().Name}");
			Console.WriteLine("_____________________________________");

			foreach((string Name, T entry) field in this.GetType().GetFields().Where(f => f.FieldType == typeof(T)).Select(f => (f.Name, entry: (T) f.GetValue(this))).OrderBy(v => v.entry.Value)) {
				if(!skipped && field.entry.GreaterEqualThan(this.baseOffset)) {
					Console.WriteLine("--------Children-----------");
					skipped = true;
				}

				Console.WriteLine($"{field.Name} = {field.entry.Value}{terminator}");
			}

			Console.WriteLine("=====================================");
		}

		public bool IsValueBaseset(T entry) {
			return !this.IsValueChildset(entry);
		}

		public bool IsValueChildset(T entry) {
			return entry.GreaterEqualThan(this.baseOffset);
		}
	}

	//	public abstract class TTdemoBase : ConstantSet<SimpleUShort, ushort> {
	//
	//		public readonly ushort VAL1;
	//		public readonly ushort VAL2;
	//		
	//		public TTdemoBase() : base(100) {
	//			this.VAL1 = this.CreateBaseConstant();
	//			this.VAL2 = this.CreateBaseConstant();
	//		}
	//	}
	//	
	//	public sealed class TTdemo2 : TTdemoBase {
	//
	//		public readonly ushort VAL3;
	//
	//		private TTdemo2() {
	//			this.VAL3 = this.CreateChildConstant();
	//		}
	//		
	//		#region singleton
	//			private static readonly TTdemo2 instance = new TTdemo2();
	//
	//			
	//			
	//			static TTdemo2()
	//			{
	//			}
	//
	//			public static TTdemo2 Instance => instance;
	//
	//		#endregion
	//	}
}