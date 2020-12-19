using System.Collections.Generic;
using Neuralia.Blockchains.Tools.Cryptography;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class ListExtensions {
		public static IList<T> Shuffle<T>(this IList<T> list) {
			int n = list.Count;

			while(n > 1) {
				n--;
				int k = GlobalRandom.GetNext(n + 1);
				T value = list[k];
				list[k] = list[n];
				list[n] = value;
			}

			return list;
		}
	}
}