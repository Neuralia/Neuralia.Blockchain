using System;
using System.Collections.Generic;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.THS.V1;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.General;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Serialization;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Messages.Specialization.Moderation.Appointments {
	public interface IAppointmentContextMessage : IAppointmentSliceMessage {
		int                               PuzzleEngineVersion { get; set; }
		SafeArrayHandle                   SecretPuzzles       { get; set; }
		short                             PuzzleWindow        { get; set; }
		int                               ValidatorWindow     { get; set; }
		THSRulesSet                    THSRuleSet          { get; set; }
		List<SafeArrayHandle>             Applicants          { get; }
		Dictionary<Guid, SafeArrayHandle> Validators          { get; }
	}

	public abstract class AppointmentContextMessage : AppointmentSliceMessage , IAppointmentContextMessage{
		
		public int PuzzleEngineVersion { get; set; }
		public SafeArrayHandle SecretPuzzles { get; set; }
		public short PuzzleWindow { get; set; }
		public int ValidatorWindow { get; set; }
		public THSRulesSet THSRuleSet { get; set; } = new THSRulesSet();
		public List<SafeArrayHandle> Applicants  { get; } = new List<SafeArrayHandle>();
		public Dictionary<Guid, SafeArrayHandle> Validators  { get; } = new Dictionary<Guid, SafeArrayHandle>();

		protected override void RehydrateCompressedContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateCompressedContents(rehydrator, rehydrationFactory);

			this.PuzzleWindow = rehydrator.ReadShort();
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Rehydrate(rehydrator);
			this.ValidatorWindow     = tool.Value;
			
			tool.Rehydrate(rehydrator);
			this.PuzzleEngineVersion = tool.Value;
			
			this.SecretPuzzles       = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			this.THSRuleSet.Rehydrate(rehydrator);
			
			tool.Rehydrate(rehydrator);
			int count = tool.Value;

			this.Applicants.Clear();
			for(int i = 0; i < count; i++) {
				this.Applicants.Add((SafeArrayHandle)rehydrator.ReadNonNullableArray());
			}
			
			tool.Rehydrate(rehydrator);
			count = tool.Value;

			this.Validators.Clear();
			for(int i = 0; i < count; i++) {
				var key = rehydrator.ReadGuid();
				this.Validators.Add(key, (SafeArrayHandle)rehydrator.ReadNonNullableArray());
			}
		}

		protected override void DehydrateCompressedContents(IDataDehydrator dehydrator) {
			base.DehydrateCompressedContents(dehydrator);

			dehydrator.Write(this.PuzzleWindow);
			
			AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
			tool.Value = this.ValidatorWindow;
			tool.Dehydrate(dehydrator);
			
			tool.Value = this.PuzzleEngineVersion;
			tool.Dehydrate(dehydrator);
			
			dehydrator.WriteNonNullable(this.SecretPuzzles);
			this.THSRuleSet.Dehydrate(dehydrator);

			tool.Value = this.Applicants.Count;
			tool.Dehydrate(dehydrator);

			foreach(var applicant in this.Applicants) {
				dehydrator.WriteNonNullable(applicant);
			}
			
			tool.Value = this.Validators.Count;
			tool.Dehydrate(dehydrator);
			
			foreach(var validator in this.Validators) {
				dehydrator.Write(validator.Key);
				dehydrator.WriteNonNullable(validator.Value);
			}
		}

		public override HashNodeList GetStructuresArray() {
			HashNodeList nodesList = base.GetStructuresArray();

			nodesList.Add(this.PuzzleWindow);
			nodesList.Add(this.ValidatorWindow);
			nodesList.Add(this.PuzzleEngineVersion);
			nodesList.Add(this.SecretPuzzles);
			nodesList.Add(this.THSRuleSet);
			
			nodesList.Add(this.Applicants.Count);

			foreach(var applicant in this.Applicants) {
				nodesList.Add(applicant);
			}
			
			nodesList.Add(this.Validators.Count);

			foreach(var validator in this.Validators) {
				nodesList.Add(validator.Key);
				nodesList.Add(validator.Value);
			}
			
			return nodesList;
		}
		
		public class ValidatorSessionDetails : IBinarySerializable {

			/// <summary>
			/// the indices of the requesters assigned to this validator
			/// </summary>
			public readonly List<int> AssignedIndices = new List<int>();
			
			/// <summary>
			/// The secret codes sent by all to begin the process
			/// </summary>
			public readonly List<(int secretCode, int secretCodeL2)> SecretCodes = new List<(int secretCode, int secretCodeL2)>();

			public void Rehydrate(SafeArrayHandle bytes) {
				using var rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
				this.Rehydrate(rehydrator);
			}
			
			public void Rehydrate(IDataRehydrator rehydrator) {

				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				int count = tool.Value;

				this.AssignedIndices.Clear();

				for(int j = 0; j < count; j++) {
					
					tool.Rehydrate(rehydrator);
					int index = tool.Value;

					this.AssignedIndices.Add(index);
				}
				
				this.SecretCodes.Clear();
				tool.Rehydrate(rehydrator);
				count = tool.Value;

				for(int j = 0; j < count; j++) {
					int id     = rehydrator.ReadInt();
					int secret = rehydrator.ReadInt();
					
					this.SecretCodes.Add((id, secret));
				}
			}

			public SafeArrayHandle Dehydrate() {
				using var dehydrator = DataSerializationFactory.CreateDehydrator();
				this.Dehydrate(dehydrator);

				return dehydrator.ToArray();
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				
				tool.Value = this.AssignedIndices.Count;
				tool.Dehydrate(dehydrator);
				
				foreach(var id in this.AssignedIndices) {
					
					tool.Value = id;
					tool.Dehydrate(dehydrator);
				}
				
				tool.Value = this.SecretCodes.Count;
				tool.Dehydrate(dehydrator);
				
				foreach(var id in this.SecretCodes) {
					dehydrator.Write(id.secretCode);
					dehydrator.Write(id.secretCodeL2);
				}
			}
		}
		
		public class PuzzleContext : IBinarySerializable {

			public readonly List<PuzzleEntry> Puzzles = new List<PuzzleEntry>();
			public THSRulesSet THSRulesSet = new THSRulesSet();
			
			public void Rehydrate(IDataRehydrator rehydrator) {
				
				this.THSRulesSet.Rehydrate(rehydrator);
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				int count = tool.Value;

				this.Puzzles.Clear();
				for(int i = 0; i < count; i++) {

					var puzzle = new PuzzleEntry();
					puzzle.Rehydrate(rehydrator);
					
					this.Puzzles.Add(puzzle);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				this.THSRulesSet.Dehydrate(dehydrator);
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Value = this.Puzzles.Count;
				tool.Dehydrate(dehydrator);
				
				foreach(var puzzle in this.Puzzles) {
					puzzle.Dehydrate(dehydrator);
				}
			}
		}

		public class PuzzleEntry : IBinarySerializable {

			public string Code { get; set; }
			public Dictionary<string, string> Locales { get; set; } = new Dictionary<string, string>();
			public Dictionary<string, string> Instructions { get; set; } = new Dictionary<string, string>();
			public List<string> Libraries { get; set; } = new List<string>();
			public int EngineVersion { get; set; } = 1;
			
			public void Rehydrate(IDataRehydrator rehydrator) {
				this.Code = rehydrator.ReadString();
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				this.EngineVersion = tool.Value;
				
				tool.Rehydrate(rehydrator);
				int count = tool.Value;

				this.Libraries.Clear();
				for(int i = 0; i < count; i++) {
					this.Libraries.Add(rehydrator.ReadString());
				}
				
				tool.Rehydrate(rehydrator);
				count = tool.Value;

				this.Locales.Clear();
				for(int i = 0; i < count; i++) {
					string key = rehydrator.ReadString();
					string json = rehydrator.ReadString();
					this.Locales.Add(key, json);
				}
				
				tool.Rehydrate(rehydrator);
				count = tool.Value;
				
				this.Instructions.Clear();
				for(int i = 0; i < count; i++) {
					string key = rehydrator.ReadString();
					string instruction = rehydrator.ReadString();
					this.Instructions.Add(key, instruction);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.Write(this.Code);
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();

				tool.Value = this.EngineVersion;
				tool.Dehydrate(dehydrator);

				tool.Value = this.Libraries.Count;
				tool.Dehydrate(dehydrator);

				foreach(var library in this.Libraries) {
					dehydrator.Write(library);
				}
				
				tool.Value = this.Locales.Count;
				tool.Dehydrate(dehydrator);

				foreach(var locale in this.Locales) {
					dehydrator.Write(locale.Key);
					dehydrator.Write(locale.Value);
				}

				tool.Value = this.Instructions.Count;
				tool.Dehydrate(dehydrator);

				foreach(var instruction in this.Instructions) {
					dehydrator.Write(instruction.Key);
					dehydrator.Write(instruction.Value);
				}
			}
		}
		

		public class ApplicantEntry : IBinarySerializable{

			public readonly List<PublicValidatorEntry> PublicValidators = new List<PublicValidatorEntry>();

			public readonly SafeArrayHandle Secret = SafeArrayHandle.Create();

			public void Rehydrate(IDataRehydrator rehydrator) {
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				int count = tool.Value;
				
				this.PublicValidators.Clear();
				for(int i = 0; i < count; i++) {

					var validator = new PublicValidatorEntry();
					validator.Rehydrate(rehydrator);
					
					this.PublicValidators.Add(validator);
				}

				this.Secret.Entry = rehydrator.ReadNonNullableArray();
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				
				tool.Value = this.PublicValidators.Count;
				tool.Dehydrate(dehydrator);

				foreach(var validator in this.PublicValidators) {
					validator.Dehydrate(dehydrator);
				}

				dehydrator.WriteNonNullable(Secret);
			}
		}

		public abstract class ValidatorEntry : IBinarySerializable{
			
			public Guid IP { get; set; }
			public int? ValidatorPort { get; set; }
			
			public virtual void Rehydrate(IDataRehydrator rehydrator) {
				this.IP = rehydrator.ReadGuid();
				this.ValidatorPort = rehydrator.ReadNullableInt();
			}

			public virtual void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.Write(IP);
				dehydrator.Write(ValidatorPort);
			}
			
		}
		
		public class SecretValidatorEntry : ValidatorEntry {
			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);
			}
		}
		
		public class PublicValidatorEntry : ValidatorEntry {
			public SafeArrayHandle SecretCode { get; set; } = SafeArrayHandle.Create();
			
			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.SecretCode.Entry = rehydrator.ReadNonNullableArray();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);
				
				dehydrator.WriteNonNullable(SecretCode);
			}
		}
		
		public class ApplicantSecretPackage : IBinarySerializable {
			public readonly List<SecretValidatorEntry> SecretValidators = new List<SecretValidatorEntry>();
			public int SecretCode { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				
				this.SecretCode = rehydrator.ReadInt();
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				tool.Rehydrate(rehydrator);
				int count = tool.Value;

				this.SecretValidators.Clear();
				for(int i = 0; i < count; i++) {

					var validator = new SecretValidatorEntry();
					validator.Rehydrate(rehydrator);
					
					this.SecretValidators.Add(validator);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.SecretCode);
				
				AdaptiveInteger1_5 tool = new AdaptiveInteger1_5();
				
				tool.Value = this.SecretValidators.Count;
				tool.Dehydrate(dehydrator);

				foreach(var validator in this.SecretValidators) {
					validator.Dehydrate(dehydrator);
				}
			}
		}

		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_CONTEXT, 1, 0);
		}
	}
}