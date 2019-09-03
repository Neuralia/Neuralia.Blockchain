using System;
using System.Net;

namespace Neuralia.Blockchains.Core.Network {

	public sealed class NetworkEndPoint {

		public NetworkEndPoint(EndPoint endPoint, IPMode mode = IPMode.IPv4) {
			if(endPoint is IPEndPoint ipEndPoint) {
				this.EndPoint = ipEndPoint;
				this.IPMode = mode;
			} else {
				throw new ArgumentException("Endpoint must be an IP enpoint");
			}
		}

		public NetworkEndPoint(IPEndPoint endPoint, IPMode mode = IPMode.IPv4) {
			this.EndPoint = endPoint;
			this.IPMode = mode;
		}

		public NetworkEndPoint(IPAddress address, int port, IPMode mode = IPMode.IPv4) : this(new IPEndPoint(address, port), mode) {
		}

		public NetworkEndPoint(string IP, int port, IPMode mode = IPMode.IPv4) : this(IPAddress.Parse(IP), port, mode) {
		}

		public IPEndPoint EndPoint { get; set; }

		public IPMode IPMode { get; set; }

		private bool Equals(NetworkEndPoint other) {
			return Equals(this.EndPoint, other.EndPoint);
		}

		public override bool Equals(object obj) {
			return ReferenceEquals(this, obj) || (obj is NetworkEndPoint other && this.Equals(other));
		}

		public override int GetHashCode() {
			return this.EndPoint != null ? this.EndPoint.GetHashCode() : 0;
		}

		/// <inheritdoc />
		public override string ToString() {
			return $"{this.EndPoint}-{this.IPMode}";
		}
	}
}