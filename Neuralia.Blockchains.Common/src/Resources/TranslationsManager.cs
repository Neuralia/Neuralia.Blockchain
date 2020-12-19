using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Common.Resources {
	public class TranslationsManager {

		private readonly ResourceReader resourceReader;

		static TranslationsManager() {
		}

		private TranslationsManager() {
			this.resourceReader = new ResourceReader(typeof(TranslationsManager));
		}

		public static TranslationsManager Instance { get; } = new TranslationsManager();

	#region KEYS

		public string Tos => this.resourceReader.GetString("TOS");

	#endregion

	}
}