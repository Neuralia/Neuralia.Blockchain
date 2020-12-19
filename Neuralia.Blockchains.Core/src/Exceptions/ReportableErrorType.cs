using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.General.Types.Constants;
using Neuralia.Blockchains.Core.General.Types.Simple;

namespace Neuralia.Blockchains.Core {

	public class ReportableErrorType : NamedSimpleUShort<ReportableErrorType> {

		public ReportableErrorType() {
		}

		public ReportableErrorType(ushort value) : base(value) {
		}

		public override bool Equals(ReportableErrorType other) {

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

			return this.Equals((ReportableErrorType) obj);
		}

		public static implicit operator ReportableErrorType(ushort d) {
			return new ReportableErrorType(d);
		}

		public static bool operator ==(ReportableErrorType a, ReportableErrorType b) {
			if(ReferenceEquals(null, a)) {
				return ReferenceEquals(null, b);
			}

			return a.Equals(b);
		}

		public static bool operator !=(ReportableErrorType a, ReportableErrorType b) {
			return !(a == b);
		}

		public override string ErrorPrefix => "BCT";
	}

	public interface IReportableErrorTypeNameProvider {

		string GetReportableErrorTypeName(ReportableErrorType chainType);
		bool MatchesType(ReportableErrorType chainType);
	}
}