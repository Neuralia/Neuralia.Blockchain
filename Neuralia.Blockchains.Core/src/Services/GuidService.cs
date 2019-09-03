using System;
using RT.Comb;

namespace Neuralia.Blockchains.Core.Services {
	public interface IGuidService {
		Guid Create();

		DateTime GetTimestamp(Guid guid);
	}

	public class GuidService : IGuidService {
		protected readonly ITimeService timeService;

		private long activeTimestamp;
		private byte lastAssignedScope;

		public GuidService(ITimeService timeService) {
			this.timeService = timeService;
		}

		public Guid Create() {
			DateTime time = this.timeService.CurrentRealTime;

			//TODO: this is for debug purposes only, ensure to remove this below in prod
			// debug, change the datetime here
			//time = time.AddDays(-1).AddHours(4);

			return Provider.PostgreSql.Create(time);
		}

		public DateTime GetTimestamp(Guid guid) {
			return Provider.PostgreSql.GetTimestamp(guid);
		}

		/// <summary>
		///     Here we determine the valid scope for this transaction. We allow up to 255 transactions per seconds max. its more
		///     than enough, so in the same second, we increment the scope value to always be unique.
		/// </summary>
		/// <param name="timestamp"></param>
		/// <returns></returns>
		/// <exception cref="InvalidOperationException"></exception>
		protected byte GetValidScope(long timestamp) {
			if(this.activeTimestamp == timestamp) {
				// we already emitted transactions in this second, so we increment the scope
				if(this.lastAssignedScope == byte.MaxValue) {
					throw new InvalidOperationException($"we have reached the maximum amount of {byte.MaxValue} transactions per second.");
				}

				this.lastAssignedScope += 1;
			} else {
				//This is a new second, so we reset the scope
				this.activeTimestamp = timestamp;
				this.lastAssignedScope = 0; // yes, 0 is the first scope
			}

			return this.lastAssignedScope;
		}
	}
}