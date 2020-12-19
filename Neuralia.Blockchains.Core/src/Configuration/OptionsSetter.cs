namespace Neuralia.Blockchains.Core.Configuration {

	public interface IOptionsSetter {
		void SetRuntimeOptions(IAppSettingsBase appSettings, ICommandLineOptions cmdOptions);
	}

	public class OptionsSetter<A, O> : IOptionsSetter
		where A : class, IAppSettingsBase
		where O : ICommandLineOptions {

		public void SetRuntimeOptions(IAppSettingsBase appSettings, ICommandLineOptions cmdOptions) {
			this.SetOptions((A) appSettings, (O) cmdOptions);
		}

		public virtual void SetOptions(A appSettings, O cmdOptions) {

		}
	}
}