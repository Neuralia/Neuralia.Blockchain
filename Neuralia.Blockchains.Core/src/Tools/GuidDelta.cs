using System;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.General.ExclusiveOptions;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Tools {
	
	/// <summary>
	/// a class that will split a guid into a random 2 part set. good to scramble data
	/// </summary>
	public class GuidDelta{
		
		/// <summary>
		/// create a random delta, store both in the same byte array
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static ByteArray CreateMap(Guid input) {

			Span<byte> bytes = stackalloc byte[16];
			input.TryWriteBytes(bytes);
			var result = ByteArray.Create(16+16);
			Span<byte> source = result.Span.Slice(16, 16);
			
			// randomize the ops table
			for(int i = 0; i < 16; i++) {

				bool operation = GlobalRandom.GetNextBool();
				sbyte delta = 0;
				byte deltaVal = 0;
				if(operation) {
					// we add, so here we subtract the negative
					
					deltaVal = (byte)Math.Min(bytes[i], Math.Abs((int) sbyte.MinValue));
				} else {
					// we subtract, so here we add the negative
					
					deltaVal = (byte)Math.Min((byte.MaxValue - bytes[i]), sbyte.MaxValue);
				}

				if(deltaVal > 0) {
					delta = (sbyte)((operation?1:-1)*GlobalRandom.GetNext(0, deltaVal));
				}
				
				source[i] = (byte)(bytes[i] - delta);
				result[i] = (byte)delta;
			}
			
			return result;
		}

		public static ByteArray CreateMap(Guid input, long nonce1, long nonce2) {
			Span<byte> source = stackalloc byte[16];
			TypeSerializer.Serialize(nonce1, source.Slice(0, 8));
			TypeSerializer.Serialize(nonce2, source.Slice(8, 8));

			return CreateMap(input, new Guid(source));
		}
		

		public static Guid Rebuild(ByteArray input, long nonce1, long nonce2) {
			Span<byte> source = stackalloc byte[16];
			TypeSerializer.Serialize(nonce1, source.Slice(0, 8));
			TypeSerializer.Serialize(nonce2, source.Slice(8, 8));

			return Rebuild(input, new Guid(source));
		}
		
		/// <summary>
		/// create a delta from a known starting point
		/// </summary>
		/// <param name="input"></param>
		/// <param name="nonce1"></param>
		/// <param name="nonce2"></param>
		/// <returns></returns>
		public static ByteArray CreateMap(Guid input, Guid nonce) {

			Span<byte> bytes = stackalloc byte[16];
			input.TryWriteBytes(bytes);
			var result = ByteArray.Create(2+16);
			Span<byte> delta = result.Span.Slice(2, 16);
			Span<byte> source = stackalloc byte[16];
			nonce.TryWriteBytes(source);
			
			
			UShortExclusiveOption map = new UShortExclusiveOption();
			// randomize the ops table
			for(int i = 0; i < 16; i++) {

				bool operation = false;
				
				if(bytes[i] < source[i]) {
					// we add, so here we subtract the negative
					operation = false;
					delta[i] = (byte)(source[i] - bytes[i]);
				} else if(bytes[i] > source[i]) {
					// we subtract, so here we add the negative
					delta[i] = (byte)(bytes[i] - source[i]);
					operation = true;
				}
				
				map.SetOption((ushort)((operation?1:0) << i));
			}

			TypeSerializer.Serialize(map.Value, result.Span.Slice(0, 2));

			return result;
		}
		
		public static Guid Rebuild(ByteArray input, Guid nonce) {
			Span<byte> result = stackalloc byte[16];
			Span<byte> delta = input.Span.Slice(2, 16);
			
			Span<byte> source = stackalloc byte[16];
			nonce.TryWriteBytes(source);

			TypeSerializer.Deserialize(input.Span.Slice(0, 2), out ushort options);
			UShortExclusiveOption map = new UShortExclusiveOption(options);
			
			for(int i = 0; i < 16; i++) {

				bool operation = map.HasOption((ushort) (1 << i));

				if(operation) {
					// add
					result[i] = (byte)(source[i] + delta[i]);
				} else {
					// subtract
					result[i] = (byte)(source[i] - delta[i]);
				}
			}
			
			return new Guid(result);
		}
		
		public static Guid Rebuild(ByteArray input) {
			Span<byte> result = stackalloc byte[16];
			Span<byte> delta = input.Span.Slice(0, 16);
			Span<byte> source = input.Span.Slice(16, 16);
			
			for(int i = 0; i < 16; i++) {

				result[i] = (byte)(source[i] + (sbyte)delta[i]);
			}
			
			return new Guid(result);
		}
	}
}