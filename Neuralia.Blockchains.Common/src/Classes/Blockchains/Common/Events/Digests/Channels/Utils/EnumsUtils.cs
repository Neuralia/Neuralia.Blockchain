using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Digests.Channels.Utils {
	public static class EnumsUtils {

		/// <summary>
		///     a simple enum cache to avoid recreating it every time
		/// </summary>
		private static readonly Dictionary<Type, Array> enumsCache = new Dictionary<Type, Array>();

		/// <summary>
		///     a simple enum cache to avoid recreating it every time
		/// </summary>
		private static readonly Dictionary<Type, Array> simpleEnumsCache = new Dictionary<Type, Array>();

		/// <summary>
		///     Check if an enum has a certain flag
		/// </summary>
		/// <param name="flags"></param>
		/// <param name="flag"></param>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static bool HasFlag<T>(T flags, T flag)
			where T : struct, Enum, IConvertible {
			return (flags.ToUInt32(null) & flag.ToUInt32(null)) != 0;
		}

		public static T[] GetAllEntries<T>()
			where T : struct, Enum {

			Type type = typeof(T);

			if(enumsCache.ContainsKey(type)) {
				return (T[]) enumsCache[type];
			}

			// cache it
			enumsCache.Add(type, Enum.GetValues(typeof(T)));

			return (T[]) enumsCache[type];
		}

		/// <summary>
		///     selcet only flag enums that are not groups of others
		/// </summary>
		/// <typeparam name="T"></typeparam>
		/// <returns></returns>
		public static T[] GetSimpleEntries<T>()
			where T : struct, Enum {
			Type type = typeof(T);

			if(simpleEnumsCache.ContainsKey(type)) {
				return (T[]) simpleEnumsCache[type];
			}

			T[] entries = GetAllEntries<T>();

			List<T> simpleEntries = new List<T>();

			// now remove any that are flag combinations of others
			foreach(T entry in entries) {
				// get the others
				bool isComplex = false;

				foreach(T other in entries.Where(e => !Equals(e, entry))) {
					// now if any others flag is contained in this one, we know we have a group and discard it
					if(entry.HasFlag(other)) {
						isComplex = true;

						break;
					}
				}

				if(!isComplex) {
					simpleEntries.Add(entry);
				}
			}

			// cache it
			simpleEnumsCache.Add(type, simpleEntries.ToArray());

			return (T[]) enumsCache[type];
		}

		public static void RunForFlags<T>(T channels, Action<T> action)
			where T : struct, Enum {

			foreach(T type in GetSimpleEntries<T>()) {
				if(channels.HasFlag(type)) {
					action(type);
				}
			}
		}

		private static bool Equals<T>(T value1, T value2) {
			return EqualityComparer<T>.Default.Equals(value1, value2);
		}
	}
}