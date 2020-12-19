using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Types.Simple;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1 {
	public class THSSolutionSet : Versionable<SimpleUShort> {

		public const int MAX_SOLUTIONS = 10;

		public THSSolutionSet(List<(int solution, long nonce)> solutions) {
			this.Solutions.AddRange(solutions);
		}

		public THSSolutionSet(int solution, long nonce) {
			this.AddSolution(solution, nonce);
		}

		public THSSolutionSet() {

		}

		public List<(int solution, long nonce)> Solutions { get; } = new List<(int solution, long nonce)>();

		public bool IsEmpty => !this.Solutions.Any() || this.Solutions.Any(s => (s.nonce == 0) || (s.solution == 0));
		public bool IsValid => this.Solutions.Any() && this.Solutions.All(s => (s.nonce != 0) && (s.solution != 0));

		public void AddSolution(int solution, long nonce) {
			this.Solutions.Add((solution, nonce));

			if(this.Solutions.Count > MAX_SOLUTIONS) {
				throw new ApplicationException($"Number of solutions has passed the maximum count of {MAX_SOLUTIONS}");
			}
		}

		protected bool Equals(THSSolutionSet other) {
			if(ReferenceEquals(null, other)) {
				return false;
			}

			return this.Solutions.SequenceEqual(other.Solutions);
		}

		public static bool operator ==(THSSolutionSet a, THSSolutionSet b) {
			if(ReferenceEquals(null, b)) {
				return ReferenceEquals(null, a);
			}

			if(ReferenceEquals(null, a)) {
				return false;
			}

			return a.Equals(b);
		}

		public static bool operator !=(THSSolutionSet a, THSSolutionSet b) {
			return !(a == b);
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

			return this.Equals((THSSolutionSet) obj);
		}

		public override int GetHashCode() {
			return this.Solutions.GetHashCode();
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList hashNodes = base.GetStructuresArray();

			hashNodes.Add(this.Solutions.Count);

			foreach((int solution, long nonce) in this.Solutions) {
				hashNodes.Add(solution);
				hashNodes.Add(nonce);
			}

			return hashNodes;
		}

		public void Rehydrate(SafeArrayHandle bytes) {

			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
			this.Rehydrate(rehydrator);
		}

		public override void Rehydrate(IDataRehydrator rehydrator) {
			base.Rehydrate(rehydrator);

			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			AdaptiveLong1_9 longTool = new AdaptiveLong1_9();

			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			if(count > MAX_SOLUTIONS) {
				throw new ApplicationException("Invalid solutions count");
			}

			this.Solutions.Clear();

			for(int i = 0; i < count; i++) {
				int solution = rehydrator.ReadInt();
				longTool.Rehydrate(rehydrator);

				this.Solutions.Add((solution, longTool.Value));
			}
		}

		public override void Dehydrate(IDataDehydrator dehydrator) {
			base.Dehydrate(dehydrator);

			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			AdaptiveLong1_9 longTool = new AdaptiveLong1_9();

			if(this.Solutions.Count > MAX_SOLUTIONS) {
				throw new ApplicationException("Invalid solutions count");
			}

			tool.Value = this.Solutions.Count;
			tool.Dehydrate(dehydrator);

			foreach((int solution, long nonce) in this.Solutions) {
				dehydrator.Write(solution);
				longTool.Value = nonce;
				longTool.Dehydrate(dehydrator);
			}
		}

		public SafeArrayHandle Dehydrate() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			this.Dehydrate(dehydrator);

			return dehydrator.ToArray();
		}

		public override void JsonDehydrate(JsonDeserializer jsonDeserializer) {
			base.JsonDehydrate(jsonDeserializer);

			jsonDeserializer.SetArray("Solutions", this.Solutions, (s, e) => {
				jsonDeserializer.SetProperty("Solution", e.solution);
				jsonDeserializer.SetProperty("Nonce", e.nonce);
			});
		}

		protected override ComponentVersion<SimpleUShort> SetIdentity() {
			return (1, 0, 1);
		}
	}
}