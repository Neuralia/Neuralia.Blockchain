using System;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Tools {
	
	/// <summary>
	/// Serialize numbers to bases in minimal form
	/// </summary>
	public static class NumberBaser{
		#region tools	

		private static string transform(ByteArray bytes, Func<ByteArray, string> transformer) {
			
			return transformer(ByteArray.Wrap(bytes.Span.TrimEnd().ToArray()));
		}
		#endregion
			
		#region long
		private static string transform(long value, Func<ByteArray, string> transformer) {
			
			using var bytes = ByteArray.Create(sizeof(long));
			TypeSerializer.Serialize(value, bytes.Span);
			
			return transform(bytes, transformer);
		}
		
		public static string ToBase30(long value) {

			return transform(value, a => a.ToBase30());
		}
		
		public static string ToBase32(long value) {
			
			return transform(value, a => a.ToBase32());
		}
		
		public static string ToBase58(long value) {
			
			return transform(value, a => a.ToBase58());
		}
		
		public static string ToBase64(long value) {
			
			return transform(value, a => a.ToBase64());
		}
		
		public static string ToBase85(long value) {
			
			return transform(value, a => a.ToBase85());
		}
		public static string ToBase94(long value) {
			
			return transform(value, a => a.ToBase94());
		}

		private static long transformLong(string value, Func<string, SafeArrayHandle> transformer) {
			using var bytes = transformer(value);
			Span<byte> buffer = stackalloc byte[8];
			bytes.Span.CopyTo(buffer);
			TypeSerializer.Deserialize(buffer, out long result);
				
			return result;
		}
		
		public static long FromBase30ToLong(string value) {

			return transformLong(value, ByteArray.FromBase30);
		}

		public static long FromBase32ToLong(string value) {

			return transformLong(value, ByteArray.FromBase32);
		}

		public static long FromBase58ToLong(string value) {

			return transformLong(value, ByteArray.FromBase58);
		}
		
		public static long FromBase64ToLong(string value) {

			return transformLong(value, ByteArray.FromBase64);
		}
		
		public static long FromBase85ToLong(string value) {

			return transformLong(value, ByteArray.FromBase85);
		}
		public static long FromBase94ToLong(string value) {

			return transformLong(value, ByteArray.FromBase94);
		}

		#endregion
		
		#region ulong
			private static string transform(ulong value, Func<ByteArray, string> transformer) {
			
				using var bytes = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(value, bytes.Span);
			
				return transform(bytes, transformer);
			}
		
			public static string ToBase30(ulong value) {

				return transform(value, a => a.ToBase30());
			}
		
			public static string ToBase32(ulong value) {
			
				return transform(value, a => a.ToBase32());
			}
		
			public static string ToBase58(ulong value) {
			
				return transform(value, a => a.ToBase58());
			}
			
			public static string ToBase64(ulong value) {
			
				return transform(value, a => a.ToBase64());
			}
		
			public static string ToBase85(ulong value) {
			
				return transform(value, a => a.ToBase85());
			}
			public static string ToBase94(ulong value) {
			
				return transform(value, a => a.ToBase94());
			}
			
			private static ulong transformULong(string value, Func<string, SafeArrayHandle> transformer) {
				using var bytes = transformer(value);
				Span<byte> buffer = stackalloc byte[8];
				bytes.Span.CopyTo(buffer);
				TypeSerializer.Deserialize(buffer, out ulong result);
				
				return result;
			}
		
			public static ulong FromBase30ToULong(string value) {

				return transformULong(value, ByteArray.FromBase30);
			}

			public static ulong FromBase32ToULong(string value) {

				return transformULong(value, ByteArray.FromBase32);
			}

			public static ulong FromBase58ToULong(string value) {

				return transformULong(value, ByteArray.FromBase58);
			}
		
			public static ulong FromBase64ToULong(string value) {

				return transformULong(value, ByteArray.FromBase64);
			}
		
			public static ulong FromBase85ToULong(string value) {

				return transformULong(value, ByteArray.FromBase85);
			}
			public static ulong FromBase94ToULong(string value) {

				return transformULong(value, ByteArray.FromBase94);
			}
		#endregion
			
		#region int
			private static string transform(int value, Func<ByteArray, string> transformer) {
			
				using var bytes = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(value, bytes.Span);
			
				return transform(bytes, transformer);
			}
		
			public static string ToBase30(int value) {

				return transform(value, a => a.ToBase30());
			}
		
			public static string ToBase32(int value) {
			
				return transform(value, a => a.ToBase32());
			}
		
			public static string ToBase58(int value) {
			
				return transform(value, a => a.ToBase58());
			}
			
			public static string ToBase64(int value) {
			
				return transform(value, a => a.ToBase64());
			}
		
			public static string ToBase85(int value) {
			
				return transform(value, a => a.ToBase85());
			}
			public static string ToBase94(int value) {
			
				return transform(value, a => a.ToBase94());
			}
			
			private static int transformInt(string value, Func<string, SafeArrayHandle> transformer) {
				using var bytes = transformer(value);
				Span<byte> buffer = stackalloc byte[4];
				bytes.Span.CopyTo(buffer);
				TypeSerializer.Deserialize(buffer, out int result);
				
				return result;
			}
		
			public static int  FromBase30ToInt(string value) {

				return transformInt(value, ByteArray.FromBase30);
			}

			public static int  FromBase32ToInt(string value) {

				return transformInt(value, ByteArray.FromBase32);
			}

			public static int  FromBase58ToInt(string value) {

				return transformInt(value, ByteArray.FromBase58);
			}
		
			public static int  FromBase64ToInt(string value) {

				return transformInt(value, ByteArray.FromBase64);
			}
		
			public static int  FromBase85ToInt(string value) {

				return transformInt(value, ByteArray.FromBase85);
			}
			public static int  FromBase94ToInt(string value) {

				return transformInt(value, ByteArray.FromBase94);
			}
		#endregion
		
		#region uint
			private static string transform(uint value, Func<ByteArray, string> transformer) {
			
				using var bytes = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(value, bytes.Span);
			
				return transform(bytes, transformer);
			}
		
			public static string ToBase30(uint value) {

				return transform(value, a => a.ToBase30());
			}
		
			public static string ToBase32(uint value) {
			
				return transform(value, a => a.ToBase32());
			}
		
			public static string ToBase58(uint value) {
			
				return transform(value, a => a.ToBase58());
			}
			
			public static string ToBase64(uint value) {
			
				return transform(value, a => a.ToBase64());
			}
		
			public static string ToBase85(uint value) {
			
				return transform(value, a => a.ToBase85());
			}
			public static string ToBase94(uint value) {
			
				return transform(value, a => a.ToBase94());
			}
			
			private static uint transformUInt(string value, Func<string, SafeArrayHandle> transformer) {
				using var bytes = transformer(value);
				Span<byte> buffer = stackalloc byte[4];
				bytes.Span.CopyTo(buffer);
				TypeSerializer.Deserialize(buffer, out uint result);
				
				return result;
			}
		
			public static uint   FromBase30ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase30);
			}

			public static uint   FromBase32ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase32);
			}

			public static uint   FromBase58ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase58);
			}
		
			public static uint   FromBase64ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase64);
			}
		
			public static uint   FromBase85ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase85);
			}
			public static uint   FromBase94ToUInt(string value) {

				return transformUInt(value, ByteArray.FromBase94);
			}
		#endregion
			
		#region short
			private static string transform(short value, Func<ByteArray, string> transformer) {
			
				using var bytes = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(value, bytes.Span);
			
				return transform(bytes, transformer);
			}
		
			public static string ToBase30(short value) {

				return transform(value, a => a.ToBase30());
			}
		
			public static string ToBase32(short value) {
			
				return transform(value, a => a.ToBase32());
			}
		
			public static string ToBase58(short value) {
			
				return transform(value, a => a.ToBase58());
			}
			
			public static string ToBase64(short value) {
			
				return transform(value, a => a.ToBase64());
			}
		
			public static string ToBase85(short value) {
			
				return transform(value, a => a.ToBase85());
			}
			public static string ToBase94(short value) {
			
				return transform(value, a => a.ToBase94());
			}
			
			private static short transformShort(string value, Func<string, SafeArrayHandle> transformer) {
				using var bytes = transformer(value);
				Span<byte> buffer = stackalloc byte[2];
				bytes.Span.CopyTo(buffer);
				TypeSerializer.Deserialize(buffer, out short result);
				
				return result;
			}
		
			public static short   FromBase30ToShort(string value) {

				return transformShort(value, ByteArray.FromBase30);
			}

			public static short   FromBase32ToShort(string value) {

				return transformShort(value, ByteArray.FromBase32);
			}

			public static short   FromBase58ToShort(string value) {

				return transformShort(value, ByteArray.FromBase58);
			}
		
			public static short   FromBase64ToShort(string value) {

				return transformShort(value, ByteArray.FromBase64);
			}
		
			public static short   FromBase85ToShort(string value) {

				return transformShort(value, ByteArray.FromBase85);
			}
			public static short   FromBase94ToShort(string value) {

				return transformShort(value, ByteArray.FromBase94);
			}
		#endregion
		
		#region ushort
			private static string transform(ushort value, Func<ByteArray, string> transformer) {
			
				using var bytes = ByteArray.Create(sizeof(long));
				TypeSerializer.Serialize(value, bytes.Span);
			
				return transform(bytes, transformer);
			}
		
			public static string ToBase30(ushort value) {

				return transform(value, a => a.ToBase30());
			}
		
			public static string ToBase32(ushort value) {
			
				return transform(value, a => a.ToBase32());
			}
		
			public static string ToBase58(ushort value) {
			
				return transform(value, a => a.ToBase58());
			}
			
			public static string ToBase64(ushort value) {
			
				return transform(value, a => a.ToBase64());
			}
		
			public static string ToBase85(ushort value) {
			
				return transform(value, a => a.ToBase85());
			}
			public static string ToBase94(ushort value) {
			
				return transform(value, a => a.ToBase94());
			}
			
			private static ushort transformUshort(string value, Func<string, SafeArrayHandle> transformer) {
				using var bytes = transformer(value);
				Span<byte> buffer = stackalloc byte[2];
				bytes.Span.CopyTo(buffer);
				TypeSerializer.Deserialize(buffer, out ushort result);
				
				return result;
			}
		
			public static ushort  FromBase30ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase30);
			}

			public static ushort   FromBase32ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase32);
			}

			public static ushort   FromBase58ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase58);
			}
		
			public static ushort   FromBase64ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase64);
			}
		
			public static ushort   FromBase85ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase85);
			}
			public static ushort   FromBase94ToUShort(string value) {

				return transformUshort(value, ByteArray.FromBase94);
			}
		#endregion
		
	}
}