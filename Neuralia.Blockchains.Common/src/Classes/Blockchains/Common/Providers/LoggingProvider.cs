using System;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools.Locking;
using Serilog.Events;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers {

	public interface ILoggingProvider : IChainProvider, NLog.IPassthroughLogger {

	}

	public interface ILoggingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ILoggingProvider
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {
	}

	public static class LoggingProvider {
	}

	public abstract class LoggingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> : ChainProvider, ILoggingProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> {

		private readonly object locker = new object();
		protected CENTRAL_COORDINATOR CentralCoordinator { get; }

		protected NLog.IPassthroughLogger Default => NLog.Default;

		protected bool ScopedLogging { get; set; } = false;

		public LoggingProvider(CENTRAL_COORDINATOR centralCoordinator) {
			this.CentralCoordinator = centralCoordinator;

			
		}

		public override async Task Initialize(LockContext lockContext) {
			await base.Initialize(lockContext).ConfigureAwait(false);
			
			if(GlobalSettings.ApplicationSettings.ChainScopedLogging == AppSettingsBase.ChainScopedLoggingTypes.Never) {
				this.ScopedLogging = false;
			}
			else if(GlobalSettings.ApplicationSettings.ChainScopedLogging == AppSettingsBase.ChainScopedLoggingTypes.Always) {
				this.ScopedLogging = true;
			}
			else if(GlobalSettings.ApplicationSettings.ChainScopedLogging == AppSettingsBase.ChainScopedLoggingTypes.WhenMany) {
				this.CentralCoordinator.BlockchainServiceSet.GlobalsService.RegisterChainCallback(this.CentralCoordinator.ChainId, entry => {
					this.ScopedLogging = this.CentralCoordinator.BlockchainServiceSet.GlobalsService.SupportedChainsCount > 1;
				});
			}
		}

		private string FormatMessageTemplate(string messageTemplate) {
			if(this.ScopedLogging) {
				messageTemplate = $"{messageTemplate} [Chain: {this.CentralCoordinator.ChainName}]";
			}
			return messageTemplate;
		}

		public void Write(LogEvent logEvent) {
			this.Default.Write(logEvent);
		}

		public void Write(LogEventLevel level, string messageTemplate) {
			this.Default.Write(level, this.FormatMessageTemplate(messageTemplate));
		}

		public void Write<T>(LogEventLevel level, string messageTemplate, T propertyValue) {
			this.Default.Write(level, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Write<T0, T1>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Write(level, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Write<T0, T1, T2>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Write(level, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Write(LogEventLevel level, string messageTemplate, params object[] propertyValues) {
			this.Default.Write(level, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Write(LogEventLevel level, Exception exception, string messageTemplate) {
			this.Default.Write(level, exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Write(level, exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Write(level, exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Write(level, exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Write(level, exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public bool IsEnabled(LogEventLevel level) {
			return this.Default.IsEnabled(level);
		}

		public void Verbose(string messageTemplate) {
			this.Default.Verbose(this.FormatMessageTemplate(messageTemplate));
		}

		public void Verbose<T>(string messageTemplate, T propertyValue) {
			this.Default.Verbose(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Verbose(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Verbose(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Verbose(string messageTemplate, params object[] propertyValues) {
			this.Default.Verbose(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Verbose(Exception exception, string messageTemplate) {
			this.Default.Verbose(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Verbose<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Verbose(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Verbose<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Verbose(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Verbose<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Verbose(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Verbose(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Debug(string messageTemplate) {
			this.Default.Debug(this.FormatMessageTemplate(messageTemplate));
		}

		public void Debug<T>(string messageTemplate, T propertyValue) {
			this.Default.Debug(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Debug(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Debug(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Debug(string messageTemplate, params object[] propertyValues) {
			this.Default.Debug(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Debug(Exception exception, string messageTemplate) {
			this.Default.Debug(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Debug<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Debug(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Debug(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Debug(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Debug(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Debug(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Information(string messageTemplate) {
			this.Default.Information(this.FormatMessageTemplate(messageTemplate));
		}

		public void Information<T>(string messageTemplate, T propertyValue) {
			this.Default.Information(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Information(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Information(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Information(string messageTemplate, params object[] propertyValues) {
			this.Default.Information(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Information(Exception exception, string messageTemplate) {
			this.Default.Information(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Information<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Information(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Information<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Information(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Information(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Information(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Information(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Warning(string messageTemplate) {
			this.Default.Warning(this.FormatMessageTemplate(messageTemplate));
		}

		public void Warning<T>(string messageTemplate, T propertyValue) {
			this.Default.Warning(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Warning(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Warning(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Warning(string messageTemplate, params object[] propertyValues) {
			this.Default.Warning(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Warning(Exception exception, string messageTemplate) {
			this.Default.Warning(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Warning<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Warning(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Warning(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Warning(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Warning(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Warning(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Error(string messageTemplate) {
			this.Default.Error(this.FormatMessageTemplate(messageTemplate));
		}

		public void Error<T>(string messageTemplate, T propertyValue) {
			this.Default.Error(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Error(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Error(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Error(string messageTemplate, params object[] propertyValues) {
			this.Default.Error(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Error(Exception exception, string messageTemplate) {
			this.Default.Error(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Error<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Error(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Error(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Error(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Error(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Error(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Fatal(string messageTemplate) {
			this.Default.Fatal(this.FormatMessageTemplate(messageTemplate));
		}

		public void Fatal<T>(string messageTemplate, T propertyValue) {
			this.Default.Fatal(this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Fatal(this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Fatal(this.FormatMessageTemplate(messageTemplate), this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Fatal(string messageTemplate, params object[] propertyValues) {
			this.Default.Fatal(this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

		public void Fatal(Exception exception, string messageTemplate) {
			this.Default.Fatal(exception, this.FormatMessageTemplate(messageTemplate));
		}

		public void Fatal<T>(Exception exception, string messageTemplate, T propertyValue) {
			this.Default.Fatal(exception, this.FormatMessageTemplate(messageTemplate), propertyValue);
		}

		public void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
			this.Default.Fatal(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1);
		}

		public void Fatal<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
			this.Default.Fatal(exception, this.FormatMessageTemplate(messageTemplate), propertyValue0, propertyValue1, propertyValue2);
		}

		public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) {
			this.Default.Fatal(exception, this.FormatMessageTemplate(messageTemplate), propertyValues);
		}

	}
}