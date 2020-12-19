using System.Collections.Generic;
using System.Runtime.InteropServices;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Tools {
	public abstract class AppointmentsResultTypeSerializer {
		
		public static SafeArrayHandle SerializeResultSet(Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results) {

			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			tool.Value = results.Count;
			tool.Dehydrate(dehydrator);
			
			foreach(var entry in results) {
				tool.Value = (int)entry.Key;
				tool.Dehydrate(dehydrator);

				dehydrator.Write(entry.Value);
			}
	
			return dehydrator.ToArray();
		}
		
		public static Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> DeserializeResultSet(SafeArrayHandle bytes) {
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			tool.Rehydrate(rehydrator);
			int count = (int)tool.Value;

			Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results = new Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle>();
			for(int i = 0; i < count; i++) {
				
				tool.Rehydrate(rehydrator);
				Enums.AppointmentsResultTypes key = (Enums.AppointmentsResultTypes)tool.Value;
				var value = (SafeArrayHandle)rehydrator.ReadArray();
				
				results.Add(key, value);
			}
			
			return results;
		}
		
		
		public static SafeArrayHandle SerializePuzzleResult(List<int> results) {
			
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();

			tool.Value = results.Count;
			tool.Dehydrate(dehydrator);

			foreach(var answer in results) {
				dehydrator.Write(answer);
			}

			return dehydrator.ToArray();
		}
		
		public static List<int> DeserializePuzzleResult(SafeArrayHandle bytes) {
			
			using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			
			tool.Rehydrate(rehydrator);
			int count = (int)tool.Value;

			List<int> answers = new List<int>();
			for(int i = 0; i < count; i++) {
				answers.Add(rehydrator.ReadInt());
			}
			return answers;
		}
		
		public static SafeArrayHandle SerializeTHS(THSSolutionSet solution) {
			
			return solution.Dehydrate();
		}
		
		public static THSSolutionSet DeserializeTHS(SafeArrayHandle bytes) {
			
			THSSolutionSet solutionSet = new THSSolutionSet();
			solutionSet.Rehydrate(bytes);
			
			return solutionSet;
		}
		
		public static SafeArrayHandle SerializeSecretCodeL2(int secretCodeL2) {
			
			SafeArrayHandle bytes = SafeArrayHandle.Create(sizeof(int));

			TypeSerializer.Serialize(secretCodeL2, bytes.Span);

			return bytes;
		}
		
		public static int DeserializeSecretCodeL2(SafeArrayHandle bytes) {
			
			TypeSerializer.Deserialize(bytes.Span, out int secretCodeL2);
			
			return secretCodeL2;
		}
	}
}