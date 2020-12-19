using System;
using System.Text.Json.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.ExternalAPI {

	public class MiningStatisticSet {
		// System.Text.Json.JsonSerializer doesn't serialize properties from derived classes #
		//https://docs.microsoft.com/en-us/dotnet/standard/serialization/system-text-json-how-to#serialize-properties-of-derived-classes
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public MiningStatisticSessionAPI Session {get; set;}
		[System.Text.Json.Serialization.JsonIgnore, Newtonsoft.Json.JsonIgnore]
		public MiningStatisticAggregateAPI Aggregate {get; set;}

		/// <summary>
		/// Object will deserialize base classses
		/// </summary>
		public object SessionObj => this.Session;
		
		/// <summary>
		/// Object will deserialize base classses
		/// </summary>
		public object AggregateObj => this.Aggregate;
	}

	public class MiningStatisticSessionAPI {
		public long BlockStarted { get; set; }
		public long BlocksProcessed { get; set; }
		public long BlocksElected { get; set; }
		public long LastBlockElected { get; set; }
		public double PercentElected { get; set; }
		public DateTime? Start { get; set; }
	}
	
	public class MiningStatisticAggregateAPI {
		public long BlocksProcessed { get; set; }
		public long BlocksElected { get; set; }
		public long LastBlockElected { get; set; }
		public double PercentElected { get; set; }
		public int MiningSessions { get; set; }
	}
	
}