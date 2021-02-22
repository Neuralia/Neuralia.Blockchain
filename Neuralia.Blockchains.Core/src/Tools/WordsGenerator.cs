using System.Collections.Generic;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Core.Tools {
	public static class WordsGenerator {
		
		public static List<string> GenerateRandomWords(int numWords = 5, int wordLengthsMin = 7, int wordLengthsMax = 13) {
			List<string> results = new List<string>();

			for(int i = 0; i < numWords; i++) {
				// Make a word.
				int wordLength = GlobalRandom.GetNext(wordLengthsMin, wordLengthsMax+1);

				using var buffer = ByteArray.Create(wordLength);
				buffer.FillSafeRandom();
				
				results.Add(buffer.ToBase32());
			}

			return results;
		}

		public static string GenerateRandomPhrase(int numWords = 5, int wordLengthsMin = 7, int wordLengthsMax = 13) {
			return string.Join(" ", GenerateRandomWords(numWords, wordLengthsMin, wordLengthsMax));
		}
	}
}