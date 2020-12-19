using System;

namespace Neuralia.Blockchains.Core.Exceptions {
	
	/// <summary>
	/// a special exception that is meant to report information to the user about an internal error that may affect them
	/// </summary>
	public class ReportableException : BlockchainException{

		public enum ReportLevels:int {
			Default = 1,
			Modal = 2
		}
		
		public enum PriorityLevels:int {
			Verbose = 1,
			Information = 2,
			Warning = 3,
			Fatal = 4
		}

		public ReportableErrorType ErrorType { get; private set; }
		public PriorityLevels PriorityLevel { get; private set; }
		public ReportLevels ReportLevel { get; private set; }
		public string[] Parameters { get; private set; }
		
		public ReportableException(ReportableErrorType errorType, PriorityLevels priorityLevel, ReportLevels reportLevel, BlockchainType blockchainType, string chainName, string defaultMessage, string[] parameters = null, Exception exception = null) : base(defaultMessage, blockchainType, chainName, exception) {
			this.ErrorType = errorType;
			this.PriorityLevel = priorityLevel;
			this.ReportLevel = reportLevel;
			this.Parameters = parameters;
		}
		
	}
}