using System;
using System.Collections.Generic;
using System.Linq;

namespace Neuralia.Blockchains.Core.Tools {
	public class SplitDecisionException : ApplicationException {
	}

	public static class ConsensusUtilities {

		public enum ConsensusType {
			Undefined,
			Single,
			Absolute,
			ClearMajority,
			Split
		}

		public static List<(T Value, int Count)> GetConsensusGroupings<T>(IEnumerable<T> values) {

			return values.GroupBy(g => g).Select(g => (Value: g.Key, Count: g.Count())).ToList();
		}

		public static (T result, ConsensusType concensusType) GetConsensus<T, U>(IEnumerable<U> values, Func<U, T> selector) {

			return GetConsensus(values.Select(selector));
		}

		/// <summary>
		///     Here we group on a certain key, but return another value. VEry useful for hashes of byte arrays
		/// </summary>
		/// <param name="values"></param>
		/// <param name="selector"></param>
		/// <typeparam name="T"></typeparam>
		/// <typeparam name="K"></typeparam>
		/// <typeparam name="U"></typeparam>
		/// <returns></returns>
		public static (T result, ConsensusType concensusType) GetConsensus<T, K, U>(IEnumerable<U> values, Func<U, (K, T)> selector) {

			return GetConsensus(values.Select(selector));
		}

		public static (T result, ConsensusType concensusType) GetConsensus<T>(IEnumerable<T> values, Func<T, T> selector) {

			//TODO: create unit tests for these methods
			if(selector != null) {
				values = values.Select(selector);
			}

			if(!values.Any()) {
				return (default, ConsensusType.Undefined);
			}

			if(values.Count() == 1) {
				return (values.Single(), ConsensusType.Single);
			}

			var groups = GetConsensusGroupings(values);

			int groupsTotal = groups.Count;
			int entriesTotal = values.Count();

			if(groupsTotal == 0) {
				return (default, ConsensusType.Undefined);
			}

			// if we have a single entry in agreement, lets return this.
			if(groupsTotal == 1) {
				return (groups.Single().Value, ConsensusType.Absolute);
			}

			if((groupsTotal == 2) && (groups.First().Count == groups.Last().Count)) {
				// thats VERY troubling, we can not chose any between the two since we can't know who is right
				return (default, ConsensusType.Split);
			}

			// now, we got at least a couple. lets see if anything is above 50% which is the majority
			var above50group = groups.Where(e => ((double) e.Count / entriesTotal) > 0.5).Select(g => g.Value).ToArray();

			if(above50group.Any()) {
				return (above50group.Single(), ConsensusType.ClearMajority);
			}

			// thats not so great, its very divided. lets take the highest amount among the lot for now, or the first if all equal
			int max = groups.Max(g => g.Count);

			return (groups.Where(e => e.Count == max).Select(g => g.Value).First(), ConsensusType.Split);
		}

		/// <summary>
		///     Here we group on a certain key, but return another value. VEry useful for hashes of byte arrays
		/// </summary>
		public static (T result, ConsensusType concensusType) GetConsensus<T, K>(IEnumerable<(K, T)> values, Func<(K, T), (K, T)> selector) {

			//TODO: unify what we can with the other version of the similar method. right now its duplicated, not very nice.
			//TODO: create unit tests for these methods
			if(selector != null) {
				values = values.Select(selector);
			}

			if(!values.Any()) {
				return (default, ConsensusType.Undefined);
			}

			if(values.Count() == 1) {
				return (values.Single().Item2, ConsensusType.Single);
			}

			var groups = values.GroupBy(g => g.Item1).Select(g => (Value: g.Key, Other: g.First().Item2, Count: g.Count(), Entries: g)).ToList();

			int total = groups.Count;

			if(total == 0) {
				return (default, ConsensusType.Undefined);
			}

			// if we have a single entry in agreement, lets return this.
			if(total == 1) {
				return (groups.Single().Other, ConsensusType.Absolute);
			}

			if(total == 2) {
				// thats VERY troubling, we can not chose any between the two since we can't know who is right
				return (default, ConsensusType.Split);
			}

			// now, we got at least a couple. lets see if anything is above 50% which is the majority
			var above50group = groups.Where(e => ((double) e.Count / total) > 0.5).Select(g => g.Other).ToArray();

			if(above50group.Any()) {
				return (above50group.Single(), ConsensusType.ClearMajority);
			}

			// thats not so great, its very divided. lets take the highest amount among the lot for now, or the first if all equal
			int max = groups.Max(g => g.Count);

			return (groups.Where(e => e.Count == max).Select(g => g.Other).First(), ConsensusType.Split);
		}

		public static (T result, ConsensusType concensusType) GetConsensus<T>(IEnumerable<T> values) {

			return GetConsensus(values, null);
		}

		/// <summary>
		///     Here we group on a certain key, but return another value. VEry useful for hashes of byte arrays
		/// </summary>
		public static (T result, ConsensusType concensusType) GetConsensus<T, K>(IEnumerable<(K, T)> values) {

			return GetConsensus(values, null);
		}
	}
}