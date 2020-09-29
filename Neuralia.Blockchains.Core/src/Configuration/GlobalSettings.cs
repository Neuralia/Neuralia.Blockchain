using System;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Types;

namespace Neuralia.Blockchains.Core.Configuration {
	public sealed class GlobalSettings {


		private bool valueSet;

		public static SoftwareVersion SoftwareVersion => Instance.CurrentSoftwareVersion;
		public static AppSettingsBase ApplicationSettings => Instance.AppSettings;
		public static bool TestingMode { get; set; } = false;

		public AppSettingsBase AppSettings { get; private set; }
		public SoftwareVersion CurrentSoftwareVersion { get; private set; } = new SoftwareVersion();

		public NodeInfo NodeInfo { get; private set; }
		public int NetworkId { get; private set; }

		public string Locale { get; private set; } = GlobalsService.DEFAULT_LOCALE;

		public void SetLocale(string locale) {
			this.Locale = locale;
		}
		
		public void SetValues<OS>(in GlobalSettingsParameters globalSettingsParameters)
			where OS : IOptionsSetter, new() {

			// allow to set the values once. after that they become constant
			if(!this.valueSet) {

				this.CurrentSoftwareVersion = globalSettingsParameters.softwareVersion;
				this.AppSettings = globalSettingsParameters.appSettings;
				this.NodeInfo = globalSettingsParameters.nodeInfo;
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
			public NodeInfo nodeInfo;
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