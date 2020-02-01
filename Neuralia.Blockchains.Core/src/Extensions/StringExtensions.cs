using System;
using System.Linq;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class StringExtensions {
		public static string CapitallizeFirstLetter(this string value) {
			if(value == null) {
				throw new ArgumentNullException(nameof(value));
			}

			if(string.IsNullOrWhiteSpace(value)) {
				throw new ArgumentException($"{nameof(value)} must have content", nameof(value));
			}

			return value.First().ToString().ToUpper() + value.Substring(1);
		}
	}
}