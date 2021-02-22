using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types.Constants;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	public class ReportableErrorTypes : NamedUShortConstantSet<ReportableErrorType> {
		
		public readonly ReportableErrorType BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG;
		public readonly ReportableErrorType BLOCKCHAIN_TRANSACTION_KEY_DIFFERENT;
		public readonly ReportableErrorType BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER;
		public readonly ReportableErrorType BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER;
		public readonly ReportableErrorType BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER_THAN_DETECTED;

		private readonly List<IReportableErrorTypeNameProvider> ReportableErrorTypeNameProviders = new List<IReportableErrorTypeNameProvider>();


		static ReportableErrorTypes() {
		}

		protected ReportableErrorTypes() : base(10000) {
			this.CreateBaseConstant(ref this.BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG, nameof(this.BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG));
			this.CreateBaseConstant(ref this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER, nameof(this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER));
			this.CreateBaseConstant(ref this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER, nameof(this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER));
			this.CreateBaseConstant(ref this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER_THAN_DETECTED, nameof(this.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER_THAN_DETECTED));
			this.CreateBaseConstant(ref this.BLOCKCHAIN_TRANSACTION_KEY_DIFFERENT, nameof(this.BLOCKCHAIN_TRANSACTION_KEY_DIFFERENT));

			
		}

		public static ReportableErrorTypes Instance { get; } = new ReportableErrorTypes();

		public static string GetReportableErrorTypeName(ReportableErrorType chainType) {
			return Instance.GetReportableErrorTypeStringName(chainType);
		}

		public string GetReportableErrorTypeStringName(ReportableErrorType chainType) {
			IReportableErrorTypeNameProvider provider = this.ReportableErrorTypeNameProviders.FirstOrDefault(p => p.MatchesType(chainType));

			if(provider != null) {
				return provider.GetReportableErrorTypeName(chainType);
			}

			throw new ApplicationException("ReportableErrorTypeNameProvider was not set. Could not get name of blockchain type");
		}

		public void AddReportableErrorTypeNameProvider(IReportableErrorTypeNameProvider ReportableErrorTypeNameProvider) {
			this.ReportableErrorTypeNameProviders.Add(ReportableErrorTypeNameProvider);
		}
	}
}