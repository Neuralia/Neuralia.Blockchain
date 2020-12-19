using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures {
	
	public class ReportableMessageType : NamedSimpleUShort<ReportableMessageType> {

		public ReportableMessageType() {
		}

		public ReportableMessageType(ushort value) : base(value) {
		}

		public override bool Equals(ReportableMessageType other) {

			if(ReferenceEquals(null, other)) {
				return false;
			}

			return base.Equals(other);
		}

		public override bool Equals(object obj) {
			if(ReferenceEquals(null, obj)) {
				return false;
			}

			if(ReferenceEquals(this, obj)) {
				return true;
			}

			if(obj.GetType() != this.GetType()) {
				return false;
			}

			return this.Equals((ReportableMessageType) obj);
		}

		public static implicit operator ReportableMessageType(ushort d) {
			return new ReportableMessageType(d);
		}

		public static bool operator ==(ReportableMessageType a, ReportableMessageType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(ReportableMessageType a, ReportableMessageType b) {
			return !(a == b);
		}

		public override string ErrorPrefix => "BCT";
	}

	public interface IReportableMessageTypeNameProvider {

		string GetReportableMessageTypeName(ReportableMessageType chainType);
		bool MatchesType(ReportableMessageType chainType);
	}
	
	public class ReportableMessageTypes : NamedUShortConstantSet<ReportableMessageType> {
		
		public readonly ReportableMessageType BLOCK_HAS_NO_ELECTION;

		private readonly List<IReportableMessageTypeNameProvider> ReportableMessageTypeNameProviders = new List<IReportableMessageTypeNameProvider>();


		static ReportableMessageTypes() {
		}

		protected ReportableMessageTypes() : base(10000) {
			this.CreateBaseConstant(ref this.BLOCK_HAS_NO_ELECTION, nameof(this.BLOCK_HAS_NO_ELECTION));
		}

		public static ReportableMessageTypes Instance { get; } = new ReportableMessageTypes();

		public static string GetReportableMessageTypeName(ReportableMessageType chainType) {
			return Instance.GetReportableMessageTypeStringName(chainType);
		}

		public string GetReportableMessageTypeStringName(ReportableMessageType chainType) {
			IReportableMessageTypeNameProvider provider = this.ReportableMessageTypeNameProviders.FirstOrDefault(p => p.MatchesType(chainType));

			if(provider != null) {
				return provider.GetReportableMessageTypeName(chainType);
			}

			throw new ApplicationException("ReportableMessageTypeNameProvider was not set. Could not get name of blockchain type");
		}

		public void AddReportableMessageTypeNameProvider(IReportableMessageTypeNameProvider ReportableMessageTypeNameProvider) {
			this.ReportableMessageTypeNameProviders.Add(ReportableMessageTypeNameProvider);
		}
	}
}