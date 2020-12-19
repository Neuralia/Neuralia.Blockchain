using System;
using System.Collections.Generic;
using System.IO;

namespace Neuralia.Blockchains.Core.Debugging {
	public static class DebugUtils {
		public static string GetBlockTypeName(byte transactiontype) {
			//			if(transactiontype == TransactionTypes.GENESIS) {
			//				return "Genesis Primus";
			//			}
			//
			//			if(transactiontype == TransactionTypes.GENESIS_SECUNDUS) {
			//				return "Genesis Secundus";
			//			}
			//
			//			if(transactiontype == TransactionTypes.PRESENTATION) {
			//				return "Presentation";
			//			}
			//
			//			if(transactiontype == TransactionTypes.KEY_CHANGE) {
			//				return "Key Change";
			//			}
			//
			//			if(transactiontype == TransactionTypes.GENERIC) {
			//				return "Generic";
			//			}
			//
			//			if(transactiontype == TransactionTypes.MODERATION_KEY_CHANGE) {
			//				return "Moderation Key Change";
			//			}
			//
			//			if(transactiontype == TransactionTypes.MODERATION_GENERIC) {
			//				return "Moderation Generic";
			//			}

			return "Unknown";
		}

		public static List<string> SearchDirectoryForFiles(string sDir, string filename) {
			List<string> files = new List<string>();

			try {
				foreach(string d in Directory.GetDirectories(sDir)) {
					foreach(string f in Directory.GetFiles(d)) {
						if(Path.GetFileName(f) == filename) {
							files.Add(f);
						}
					}

					files.AddRange(SearchDirectoryForFiles(d, filename));
				}
			} catch(Exception excpt) {
				Console.WriteLine(excpt.Message);
			}

			return files;
		}
	}
}