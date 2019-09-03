using System;

namespace Neuralia.Blockchains.Core.Extensions {
	public static class DateTimeExtensions {

		public static DateTime TrimMilliseconds(this DateTime dateTime) {
			// here we remove the milliseconds
			return dateTime.AddTicks(-dateTime.Ticks % TimeSpan.TicksPerSecond);
		}

		public static bool EqualsNoMilliseconds(this DateTime dateTime, DateTime other) {
			return dateTime.TrimMilliseconds() == other.TrimMilliseconds();
		}
	}
}