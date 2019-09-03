using System.Collections.Generic;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core.Tools {
	public static class WordsGenerator {

		// the base 30 character set
		private static readonly char[] characters = "23456789abcdefghjkmnprstuvwxyz".ToCharArray();

		public static List<string> GenerateRandomWords(int numWords = 5, int wordLengthsMin = 7, int wordLengthsMax = 13) {
			var results = new List<string>();

			for(int i = 0; i < numWords; i++) {
				// Make a word.
				string word = "";

				int wordLength = GlobalRandom.GetNext(wordLengthsMin, wordLengthsMax);

				for(int j = 0; j < wordLength; j++) {
					int characterIndex = GlobalRandom.GetNext(0, characters.Length - 1);

					word += characters[characterIndex];
				}

				results.Add(word);
			}

			return results;
		}
	}
}