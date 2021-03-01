using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools;
using Neuralia.Blockchains.Core.Configuration;
using Serilog;
using Serilog.Events;

namespace Neuralia.Blockchains.Core.Logging {
	public class NLog {
		/// <summary>
		/// 
		/// </summary>
		public enum LoggerTypes {
			All, 
			Standard, 
			// Standard loggers: (if you add a standard logger, please remember to modify EnableLoggers() accordingly
			Default, Messages,
			// Opt-in loggers:
			IPCrawler, Connections, UPnP
		}
		
		public static readonly HashSet<LoggerTypes> EnabledLoggers = new HashSet<LoggerTypes>();

		public static bool EnableAll { get; set; } = true;
		
		public static IPassthroughLogger Default => GetLogger(LoggerTypes.Default);
		public static IPassthroughLogger IPCrawler => GetLogger(LoggerTypes.IPCrawler);
		public static IPassthroughLogger Connections => GetLogger(LoggerTypes.Connections);
		public static IPassthroughLogger Messages => GetLogger(LoggerTypes.Messages);
		public static IPassthroughLogger UPnP => GetLogger(LoggerTypes.UPnP);

		public static ILoggingBatcher LoggingBatcher => LoggingBatcherProvider.LoggingBatcher;
		
		public static void EnableLoggers(AppSettingsBase appSettings) {

			EnabledLoggers.Clear();
			EnableAll = false;
			
			if(appSettings.EnabledLoggers.Contains(LoggerTypes.All)) {
				EnableAll = true;
				return;
			}

			if (appSettings.EnabledLoggers.Contains(LoggerTypes.Standard)) //default AppSetting
			{
				EnableLogger(LoggerTypes.Default);
				EnableLogger(LoggerTypes.Messages);
			}

			foreach(var s in appSettings.EnabledLoggers.Where(s => s != LoggerTypes.All && s != LoggerTypes.Standard)) {
				EnableLogger(s);
			}
		}
		
		public static void EnableLogger(LoggerTypes loggertype) {
			if(!EnabledLoggers.Contains(loggertype)) {
				EnabledLoggers.Add(loggertype);
			}
		}

		public static IPassthroughLogger GetLogger(LoggerTypes loggertype) {
			if(EnableAll || EnabledLoggers.Contains(loggertype)) {
				return PassthroughLogger.Instance;
			}
			
			return DummyLogger.Instance;
		}
		

		public sealed class PassthroughLogger : IPassthroughLogger {
			public void Write(LogEvent logEvent) {
				Log.Write(logEvent);
			}

			public void Write(LogEventLevel level, string messageTemplate) {
				Log.Write(level, messageTemplate);
			}

			public void Write<T>(LogEventLevel level, string messageTemplate, T propertyValue) {
				Log.Write(level, messageTemplate, propertyValue);
			}

			public void Write<T0, T1>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Write(level, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Write<T0, T1, T2>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Write(level,messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Write(LogEventLevel level, string messageTemplate, params object[] propertyValues) {
				Log.Write(level, messageTemplate,  propertyValues);
			}

			public void Write(LogEventLevel level, Exception exception, string messageTemplate) {
				Log.Write(level, exception, messageTemplate);
			}

			public void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T propertyValue) {
				Log.Write(level, exception, messageTemplate, propertyValue);
			}

			public void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Write(level, exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Write(level, exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Write(level, exception, messageTemplate, propertyValues);
			}

			public bool IsEnabled(LogEventLevel level) {
				return Log.IsEnabled(level);
			}

			public void Verbose(string messageTemplate) {
				Log.Verbose(messageTemplate);
			}

			public void Verbose<T>(string messageTemplate, T propertyValue) {
				Log.Verbose(messageTemplate, propertyValue);
			}

			public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Verbose(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Verbose(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Verbose(string messageTemplate, params object[] propertyValues) {
				Log.Verbose(messageTemplate, propertyValues);
			}

			public void Verbose(Exception exception, string messageTemplate) {
				Log.Verbose(exception, messageTemplate);
			}

			public void Verbose<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Verbose(exception, messageTemplate, propertyValue);
			}

			public void Verbose<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Verbose(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Verbose<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Verbose(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Verbose(exception, messageTemplate, propertyValues);
			}

			public void Debug(string messageTemplate) {
				Log.Debug( messageTemplate);
			}

			public void Debug<T>(string messageTemplate, T propertyValue) {
				Log.Debug(messageTemplate, propertyValue);
			}

			public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Debug(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Debug(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Debug(string messageTemplate, params object[] propertyValues) {
				Log.Debug(messageTemplate, propertyValues);
			}

			public void Debug(Exception exception, string messageTemplate) {
				Log.Debug(exception, messageTemplate);
			}

			public void Debug<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Debug(exception, messageTemplate, propertyValue);
			}

			public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Debug(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Debug(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Debug(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Debug(exception, messageTemplate, propertyValues);
			}

			public void Information(string messageTemplate) {
				Log.Information( messageTemplate);
			}

			public void Information<T>(string messageTemplate, T propertyValue) {
				Log.Information(messageTemplate, propertyValue);
			}

			public void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Information(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Information(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Information(string messageTemplate, params object[] propertyValues) {
				Log.Information(messageTemplate, propertyValues);
			}

			public void Information(Exception exception, string messageTemplate) {
				Log.Information(exception, messageTemplate);
			}

			public void Information<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Information(exception, messageTemplate, propertyValue);
			}

			public void Information<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Information(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Information(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Information(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Information(exception, messageTemplate, propertyValues);
			}

			public void Warning(string messageTemplate) {
				Log.Warning( messageTemplate);
			}

			public void Warning<T>(string messageTemplate, T propertyValue) {
				Log.Warning(messageTemplate, propertyValue);
			}

			public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Warning(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Warning(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Warning(string messageTemplate, params object[] propertyValues) {
				Log.Warning(messageTemplate, propertyValues);
			}

			public void Warning(Exception exception, string messageTemplate) {
				Log.Warning(exception, messageTemplate);
			}

			public void Warning<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Warning(exception, messageTemplate, propertyValue);
			}

			public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Warning(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Warning(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Warning(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Warning(exception, messageTemplate, propertyValues);
			}

			public void Error(string messageTemplate) {
				Log.Error( messageTemplate);
			}

			public void Error<T>(string messageTemplate, T propertyValue) {
				Log.Error(messageTemplate, propertyValue);
			}

			public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Error(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Error(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Error(string messageTemplate, params object[] propertyValues) {
				Log.Error(messageTemplate, propertyValues);
			}

			public void Error(Exception exception, string messageTemplate) {
				Log.Error(exception, messageTemplate);
			}

			public void Error<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Error(exception, messageTemplate, propertyValue);
			}

			public void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Error(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Error(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Error(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Error(exception, messageTemplate, propertyValues);
			}

			public void Fatal(string messageTemplate) {
				Log.Fatal(messageTemplate);
			}

			public void Fatal<T>(string messageTemplate, T propertyValue) {
				Log.Fatal(messageTemplate, propertyValue);
			}

			public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Fatal(messageTemplate, propertyValue0, propertyValue1);
			}

			public void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Fatal(messageTemplate, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Fatal(string messageTemplate, params object[] propertyValues) {
				Log.Fatal(messageTemplate, propertyValues);
			}

			public void Fatal(Exception exception, string messageTemplate) {
				Log.Fatal(exception, messageTemplate);
			}

			public void Fatal<T>(Exception exception, string messageTemplate, T propertyValue) {
				Log.Fatal(exception, messageTemplate, propertyValue);
			}

			public void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				Log.Fatal(exception, messageTemplate, propertyValue0, propertyValue1);
			}

			public void Fatal<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				Log.Fatal(exception, messageTemplate, propertyValue0, propertyValue1, propertyValue2);
			}

			public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) {
				Log.Fatal(exception, messageTemplate, propertyValues);
			}

		#region Singleton

			static PassthroughLogger() {
			}

			private PassthroughLogger() {
			}

			public static IPassthroughLogger Instance { get; } = new PassthroughLogger();

		#endregion

		}

		public sealed class DummyLogger : IPassthroughLogger {

			public void Write(LogEvent logEvent) {
				
			}

			public void Write(LogEventLevel level, string messageTemplate) {
				
			}

			public void Write<T>(LogEventLevel level, string messageTemplate, T propertyValue) {
				
			}

			public void Write<T0, T1>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Write<T0, T1, T2>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Write(LogEventLevel level, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Write(LogEventLevel level, Exception exception, string messageTemplate) {
				
			}

			public void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public bool IsEnabled(LogEventLevel level) {
				return true;
			}

			void IPassthroughLogger.Verbose(string messageTemplate) {

			}

			public void Verbose<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Verbose(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Verbose(Exception exception, string messageTemplate) {
				
			}

			public void Verbose<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Verbose<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Verbose<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Verbose(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Debug(string messageTemplate) {
				
			}

			public void Debug<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Debug(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Debug(Exception exception, string messageTemplate) {
				
			}

			public void Debug<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Debug<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Debug(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Information(string messageTemplate) {
				
			}

			public void Information<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Information(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Information(Exception exception, string messageTemplate) {
				
			}

			public void Information<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Information<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Information(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Warning(string messageTemplate) {
				
			}

			public void Warning<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Warning(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Warning(Exception exception, string messageTemplate) {
				
			}

			public void Warning<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Warning(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Error(string messageTemplate) {
				
			}

			public void Error<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Error(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Error(Exception exception, string messageTemplate) {
				
			}

			public void Error<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Error(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public void Fatal(string messageTemplate) {
				
			}

			public void Fatal<T>(string messageTemplate, T propertyValue) {
				
			}

			public void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Fatal(string messageTemplate, params object[] propertyValues) {
				
			}

			public void Fatal(Exception exception, string messageTemplate) {
				
			}

			public void Fatal<T>(Exception exception, string messageTemplate, T propertyValue) {
				
			}

			public void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1) {
				
			}

			public void Fatal<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2) {
				
			}

			public void Fatal(Exception exception, string messageTemplate, params object[] propertyValues) {
				
			}

			public static void Verbose(string messageTemplate) {

			}

		#region Singleton

			static DummyLogger() {
			}

			private DummyLogger() {
			}

			public static IPassthroughLogger Instance { get; } = new DummyLogger();

		#endregion

		}

		public interface IPassthroughLogger {
			
			void Write(LogEvent logEvent);

			void Write(LogEventLevel level, string messageTemplate);

			void Write<T>(LogEventLevel level, string messageTemplate, T propertyValue);

			void Write<T0, T1>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Write<T0, T1, T2>(LogEventLevel level, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Write(LogEventLevel level, string messageTemplate, params object[] propertyValues);

			void Write(LogEventLevel level, Exception exception, string messageTemplate);

			void Write<T>(LogEventLevel level, Exception exception, string messageTemplate, T propertyValue);

			void Write<T0, T1>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Write<T0, T1, T2>(LogEventLevel level, Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Write(LogEventLevel level, Exception exception, string messageTemplate, params object[] propertyValues);

			bool IsEnabled(LogEventLevel level);

			void Verbose(string messageTemplate);

			void Verbose<T>(string messageTemplate, T propertyValue);

			void Verbose<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Verbose<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Verbose(string messageTemplate, params object[] propertyValues);

			void Verbose(Exception exception, string messageTemplate);

			void Verbose<T>(Exception exception, string messageTemplate, T propertyValue);

			void Verbose<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Verbose<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Verbose(Exception exception, string messageTemplate, params object[] propertyValues);

			void Debug(string messageTemplate);

			void Debug<T>(string messageTemplate, T propertyValue);

			void Debug<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Debug<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Debug(string messageTemplate, params object[] propertyValues);

			void Debug(Exception exception, string messageTemplate);

			void Debug<T>(Exception exception, string messageTemplate, T propertyValue);

			void Debug<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Debug<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Debug(Exception exception, string messageTemplate, params object[] propertyValues);

			void Information(string messageTemplate);

			void Information<T>(string messageTemplate, T propertyValue);

			void Information<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Information<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Information(string messageTemplate, params object[] propertyValues);

			void Information(Exception exception, string messageTemplate);

			void Information<T>(Exception exception, string messageTemplate, T propertyValue);

			void Information<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Information<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Information(Exception exception, string messageTemplate, params object[] propertyValues);

			void Warning(string messageTemplate);

			void Warning<T>(string messageTemplate, T propertyValue);

			void Warning<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Warning<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Warning(string messageTemplate, params object[] propertyValues);

			void Warning(Exception exception, string messageTemplate);

			void Warning<T>(Exception exception, string messageTemplate, T propertyValue);

			void Warning<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Warning<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Warning(Exception exception, string messageTemplate, params object[] propertyValues);

			void Error(string messageTemplate);

			void Error<T>(string messageTemplate, T propertyValue);

			void Error<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Error<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Error(string messageTemplate, params object[] propertyValues);

			void Error(Exception exception, string messageTemplate);

			void Error<T>(Exception exception, string messageTemplate, T propertyValue);

			void Error<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Error<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Error(Exception exception, string messageTemplate, params object[] propertyValues);

			void Fatal(string messageTemplate);

			void Fatal<T>(string messageTemplate, T propertyValue);

			void Fatal<T0, T1>(string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Fatal<T0, T1, T2>(string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Fatal(string messageTemplate, params object[] propertyValues);

			void Fatal(Exception exception, string messageTemplate);

			void Fatal<T>(Exception exception, string messageTemplate, T propertyValue);

			void Fatal<T0, T1>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1);

			void Fatal<T0, T1, T2>(Exception exception, string messageTemplate, T0 propertyValue0, T1 propertyValue1, T2 propertyValue2);

			void Fatal(Exception exception, string messageTemplate, params object[] propertyValues);
		}

	#region methods

	#endregion

	#region Singleton

		static NLog() {
		}

		private NLog() {
		}

	#endregion

	}
}