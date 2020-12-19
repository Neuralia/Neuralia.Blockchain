using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class StringExtensions {
		public static string CapitalizeFirstLetter(this string value) {
			if(value == null) {
				throw new ArgumentNullException(nameof(value));
			}

			if(string.IsNullOrWhiteSpace(value)) {
				throw new ArgumentException($"{nameof(value)} must have content", nameof(value));
			}

			return value.First().ToString().ToUpper() + value.Substring(1);
		}
		
		public static SafeArrayHandle GetBytes(this string value) {

			return SafeArrayHandle.WrapAndOwn(Encoding.UTF8.GetBytes(value));
		}
		
		public static string ToSnakeCase(this string input)
		{
			if (string.IsNullOrEmpty(input))
			{
				return input;
			}

			Match startUnderscores = Regex.Match(input, @"^_+");

			return startUnderscores + Regex.Replace(input, @"([a-z0-9])([A-Z])", "$1_$2").ToLower();
		}
		
		public static string Truncate(this string value, int maxLength, bool addDots = false)
		{
			if (string.IsNullOrEmpty(value) || value.Length <= maxLength) 
				return value;
			value = value.Substring(0, maxLength);

			if(addDots) {
				value += "[...]";
			}

			return value;
		}

		private static char[] invalidChars = null;
		public static char[] InvalidChars {
			get {
				if(invalidChars == null) {
					var list = Path.GetInvalidFileNameChars().ToList();
					
					// UPath and nio can not support ':', so we have to remove it
					list.Add(':');

					invalidChars = list.ToArray();
				}

				return invalidChars;
				
			}
		}

		public static string CleanInvalidFileNameCharacters(this string input) {
			return string.Join("_", input.Split(InvalidChars));
		}
	}
	
}