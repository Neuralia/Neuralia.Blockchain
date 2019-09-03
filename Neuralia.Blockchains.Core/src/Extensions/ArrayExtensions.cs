using System;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class ArrayExtensions {

		/// <summary>
		///     remove trailing zeros of a span
		/// </summary>
		/// <param name="input"></param>
		/// <returns></returns>
		public static Span<byte> TrimEnd(this ref Span<byte> input) {

			int length = 0;

			for(int i = input.Length - 1; i != 0; i--) {
				if(input[i] != 0) {
					break;
				}

				length++;
			}

			return input.Slice(0, input.Length - length);
		}
	}
}