using System;

namespace Neuralia.Blockchains.Core.Workflows.Base {
	public class WorkflowId {

		//TODO: this class and compare searches can be made more efficient with less allocations
		
		public static implicit operator WorkflowId(string id) {

			if(id.Contains(SEPRARATOR)) {
				return new NetworkWorkflowId(id);
			} 
			
			return new WorkflowId(id);
		}

		public uint CorrelationId{ get; protected set; }
		
		public const string SEPRARATOR = "|-|";
		
		private string cachedId = null;
		
		public WorkflowId() {
			
		}
		
		public WorkflowId(string id) {
			
			this.CorrelationId = FromString(id).CorrelationId;
		}
		
		public WorkflowId(uint correlationId) {
			this.CorrelationId = correlationId;
		}

		public static WorkflowId FromString(string id) {
			
			if(string.IsNullOrWhiteSpace(id)) {
				return new WorkflowId();
			}
			
			uint otherCorrelationId = uint.Parse(id);

			return new WorkflowId( otherCorrelationId);
		}
		
		public static bool operator ==(WorkflowId a, WorkflowId b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			if(ReferenceEquals(b, null)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(WorkflowId a, WorkflowId b) {
			return !(a == b);
		}
		
		public override string ToString() {
			if(string.IsNullOrWhiteSpace(this.cachedId)) {

				this.cachedId = $"{this.CorrelationId}";
			}

			return this.cachedId;
		}

		public virtual bool Equals(string id) {
			
			return this.Equals(FromString(id));
		}
		
		public virtual bool Equals(WorkflowId other) {
			
			if(other.GetType() != this.GetType()) {
				return false;
			}
			return this.CorrelationId == other.CorrelationId;
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

			return this.Equals((WorkflowId) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.CorrelationId.GetHashCode();

				return hashCode;
			}
		}

	}

	public class NetworkWorkflowId : WorkflowId {
		public Guid ClientUuid { get;  }
		public uint? SessionId{ get; }

		public static implicit operator NetworkWorkflowId(string id) {
			return new NetworkWorkflowId(id);
		}

		private string cachedId = null;

		public NetworkWorkflowId() {
			
		}
		
		public NetworkWorkflowId(string id) {
			var other = FromStringNetwork(id);
			this.ClientUuid = other.ClientUuid;
			this.CorrelationId = other.CorrelationId;
			this.SessionId = other.SessionId;
		}
		
		public NetworkWorkflowId(Guid clientUuid, uint correlationId, uint? sessionId) : base(correlationId) {
			this.ClientUuid = clientUuid;
			this.SessionId = sessionId;
		}

		public override string ToString() {
			if(string.IsNullOrWhiteSpace(this.cachedId)) {

				this.cachedId = $"{this.ClientUuid}{SEPRARATOR}{this.CorrelationId}{SEPRARATOR}{(this.SessionId.HasValue ? this.SessionId.Value.ToString() : "*")}";
			}

			return this.cachedId;
		}

		public static NetworkWorkflowId FromStringNetwork(string id) {
			
			if(string.IsNullOrWhiteSpace(id)) {
				return new NetworkWorkflowId();
			}
			
			string[] components = id.Split(new string[] {SEPRARATOR}, StringSplitOptions.RemoveEmptyEntries);

			Guid otherClientId = Guid.Parse(components[0]);
			uint otherCorrelationId = uint.Parse(components[1]);

			uint? otherSessionId = null;
			if(components.Length == 3) {
				string session = components[2];

				if(session != "*") {
					otherSessionId = uint.Parse(session);
				}
			}

			return new NetworkWorkflowId(otherClientId, otherCorrelationId, otherSessionId);
		}
		
		public static bool operator ==(NetworkWorkflowId a, NetworkWorkflowId b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			if(ReferenceEquals(b, null)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(NetworkWorkflowId a, NetworkWorkflowId b) {
			return !(a == b);
		}
		
		public static bool operator ==(NetworkWorkflowId a, WorkflowId b) {
			if(ReferenceEquals(a, null)) {
				return ReferenceEquals(b, null);
			}

			if(ReferenceEquals(b, null)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(NetworkWorkflowId a, WorkflowId b) {
			return !(a == b);
		}
		
		public override bool Equals(string id) {
			
			return this.Equals(FromStringNetwork(id));
		}
		
		public override bool Equals(WorkflowId other) {
			if(other is NetworkWorkflowId networkWorkflowId) {
				return this.Equals(networkWorkflowId);
			}

			return base.Equals(other);
		}
		
		public virtual bool Equals(NetworkWorkflowId other) {
			return this.ClientUuid == other.ClientUuid && this.CorrelationId == other.CorrelationId && ((!this.SessionId.HasValue || !other.SessionId.HasValue) || this.SessionId.Value == other.SessionId);
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

			return this.Equals((NetworkWorkflowId) obj);
		}

		public override int GetHashCode() {
			unchecked {
				int hashCode = this.ClientUuid.GetHashCode();
				hashCode = (hashCode * 397) ^ (int) this.CorrelationId;

				return hashCode;
			}
		}
	}
}