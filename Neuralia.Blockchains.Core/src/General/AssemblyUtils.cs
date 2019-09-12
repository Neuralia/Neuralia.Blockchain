using System;
using System.IO;
using System.Reflection;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.General {
	public static class AssemblyUtils {

		public static DateTime GetBuildTimestamp(Type assemblyType) {

			var assembly = Assembly.GetAssembly(assemblyType);

			var buffer = new byte[1024];

			using(var stream = new FileStream( assembly.Location, FileMode.Open, FileAccess.Read)) {
				stream.Read(buffer, 0, buffer.Length);
			}

			var span = buffer.AsSpan();
			TypeSerializer.DeserializeBytes(span.Slice(60, 4), out int offset);
			
			TypeSerializer.DeserializeBytes(span.Slice(offset+8, 4), out int secondsSinceBaseline);
			var baseline = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

			var linkTimeUtc = baseline.AddSeconds(secondsSinceBaseline);

			return TimeZoneInfo.ConvertTimeFromUtc(linkTimeUtc, TimeZoneInfo.Local);
		}
	}
}