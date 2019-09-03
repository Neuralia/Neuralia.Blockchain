using Neuralia.Blockchains.Core.General.Versions;

namespace Neuralia.Blockchains.Core.Configuration {
	public sealed class GlobalSettings {
		private bool valueSet;

		public static SoftwareVersion SoftwareVersion => Instance.CurrentSoftwareVersion;
		public static AppSettingsBase ApplicationSettings => Instance.AppSettings;
		public static bool TestingMode { get; set; } = false;

		public AppSettingsBase AppSettings { get; private set; }
		public SoftwareVersion CurrentSoftwareVersion { get; private set; }

		public Enums.PeerTypes PeerType { get; private set; }
		public int NetworkId { get; private set; }

		public void SetValues<OS>(in GlobalSettingsParameters globalSettingsParameters)
			where OS : IOptionsSetter, new() {

			// allow to set the values once. after that they become constant
			if(!this.valueSet) {

				this.CurrentSoftwareVersion = globalSettingsParameters.softwareVersion;
				this.AppSettings = globalSettingsParameters.appSettings;
				this.PeerType = globalSettingsParameters.peerType;
				this.NetworkId = globalSettingsParameters.networkId;

				// set the options override
				new OS().SetRuntimeOptions(this.AppSettings, globalSettingsParameters.cmdOptions);

				this.valueSet = true;
			}
		}

		public struct GlobalSettingsParameters {
			public AppSettingsBase appSettings;
			public SoftwareVersion softwareVersion;
			public ICommandLineOptions cmdOptions;
			public Enums.PeerTypes peerType;
			public int networkId;
		}

	#region Singleton

		static GlobalSettings() {
		}

		private GlobalSettings() {
		}

		public static GlobalSettings Instance { get; } = new GlobalSettings();

	#endregion

	}
}