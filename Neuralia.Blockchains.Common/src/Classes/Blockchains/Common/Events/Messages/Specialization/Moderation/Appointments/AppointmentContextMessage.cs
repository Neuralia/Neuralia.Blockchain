using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Serialization;
using Neuralia.Blockchains.Core.Cryptography.POW.V1;
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
		CPUPOWRulesSet                    POWRuleSet          { get; set; }
		List<SafeArrayHandle>             Applicants          { get; }
		Dictionary<Guid, SafeArrayHandle> Validators          { get; }
	}

	public abstract class AppointmentContextMessage : AppointmentSliceMessage , IAppointmentContextMessage{
		
		public int PuzzleEngineVersion { get; set; }
		public SafeArrayHandle SecretPuzzles { get; set; }
		public short PuzzleWindow { get; set; }
		public int ValidatorWindow { get; set; }
		public CPUPOWRulesSet POWRuleSet { get; set; } = new CPUPOWRulesSet();
		public List<SafeArrayHandle> Applicants  { get; } = new List<SafeArrayHandle>();
		public Dictionary<Guid, SafeArrayHandle> Validators  { get; } = new Dictionary<Guid, SafeArrayHandle>();

		protected override void RehydrateContents(IDataRehydrator rehydrator, IMessageRehydrationFactory rehydrationFactory) {
			base.RehydrateContents(rehydrator, rehydrationFactory);

			this.PuzzleWindow = rehydrator.ReadShort();
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Rehydrate(rehydrator);
			this.ValidatorWindow     = (int)tool.Value;
			this.PuzzleEngineVersion = rehydrator.ReadInt();
			this.SecretPuzzles       = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			this.POWRuleSet.Rehydrate(rehydrator);
			
			int count = rehydrator.ReadInt();

			this.Applicants.Clear();
			for(int i = 0; i < count; i++) {
				this.Applicants.Add((SafeArrayHandle)rehydrator.ReadNonNullableArray());
			}
			
			count = rehydrator.ReadInt();

			this.Validators.Clear();
			for(int i = 0; i < count; i++) {
				var key = rehydrator.ReadGuid();
				this.Validators.Add(key, (SafeArrayHandle)rehydrator.ReadNonNullableArray());
			}
		}

		protected override void DehydrateContents(IDataDehydrator dehydrator) {
			base.DehydrateContents(dehydrator);

			dehydrator.Write(this.PuzzleWindow);
			
			AdaptiveLong1_9 tool = new AdaptiveLong1_9();
			tool.Value = this.ValidatorWindow;
			tool.Dehydrate(dehydrator);
			
			dehydrator.Write(this.PuzzleEngineVersion);
			dehydrator.WriteNonNullable(this.SecretPuzzles);
			this.POWRuleSet.Dehydrate(dehydrator);

			dehydrator.Write(this.Applicants.Count);

			foreach(var applicant in this.Applicants) {
				dehydrator.WriteNonNullable(applicant);
			}
			
			dehydrator.Write(this.Validators.Count);
			
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
			nodesList.Add(this.POWRuleSet);
			
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
			/// the codes to be sent by open validators to decrypt the secret portion
			/// </summary>
			public readonly List<long> AssignedOpen = new List<long>();
			
			/// <summary>
			/// The secret codes sent by all to begin the process
			/// </summary>
			public readonly List<(int secretCode, ushort secretCodeL2)> SecretCodes = new List<(int secretCode, ushort secretCodeL2)>();

			public void Rehydrate(SafeArrayHandle bytes) {
				using var rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
				this.Rehydrate(rehydrator);
			}
			
			public void Rehydrate(IDataRehydrator rehydrator) {
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				
				this.AssignedOpen.Clear();
				tool.Rehydrate(rehydrator);
				int count = (int)tool.Value;

				for(int j = 0; j < count; j++) {
					
					tool.Rehydrate(rehydrator);
					long index = tool.Value;

					this.AssignedOpen.Add(index);
				}
				
				this.SecretCodes.Clear();
				tool.Rehydrate(rehydrator);
				count = (int)tool.Value;

				for(int j = 0; j < count; j++) {
					int id = rehydrator.ReadInt();
					ushort secret = rehydrator.ReadUShort();
					
					this.SecretCodes.Add((id, secret));
				}
			}

			public SafeArrayHandle Dehydrate() {
				using var dehydrator = DataSerializationFactory.CreateDehydrator();
				this.Dehydrate(dehydrator);

				return dehydrator.ToArray();
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				
				tool.Value = this.AssignedOpen.Count;
				tool.Dehydrate(dehydrator);
				
				foreach(var id in this.AssignedOpen) {
					
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
			public CPUPOWRulesSet PowRulesSet = new CPUPOWRulesSet();
			
			public void Rehydrate(IDataRehydrator rehydrator) {
				
				this.PowRulesSet.Rehydrate(rehydrator);
				
				int count = rehydrator.ReadInt();

				this.Puzzles.Clear();
				for(int i = 0; i < count; i++) {

					var puzzle = new PuzzleEntry();
					puzzle.Rehydrate(rehydrator);
					
					this.Puzzles.Add(puzzle);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				this.PowRulesSet.Dehydrate(dehydrator);
				dehydrator.Write(this.Puzzles.Count);

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
				this.EngineVersion = rehydrator.ReadInt();
				
				int count = rehydrator.ReadInt();

				this.Libraries.Clear();
				for(int i = 0; i < count; i++) {
					this.Libraries.Add(rehydrator.ReadString());
				}
				
				count = rehydrator.ReadInt();

				this.Locales.Clear();
				for(int i = 0; i < count; i++) {
					string key = rehydrator.ReadString();
					string json = rehydrator.ReadString();
					this.Locales.Add(key, json);
				}
				
				count = rehydrator.ReadInt();
				
				this.Instructions.Clear();
				for(int i = 0; i < count; i++) {
					string key = rehydrator.ReadString();
					string instruction = rehydrator.ReadString();
					this.Instructions.Add(key, instruction);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.Write(this.Code);
				dehydrator.Write(this.EngineVersion);
				
				dehydrator.Write(this.Libraries.Count);

				foreach(var library in this.Libraries) {
					dehydrator.Write(library);
				}
				
				dehydrator.Write(this.Locales.Count);

				foreach(var locale in this.Locales) {
					dehydrator.Write(locale.Key);
					dehydrator.Write(locale.Value);
				}

				dehydrator.Write(this.Instructions.Count);

				foreach(var instruction in this.Instructions) {
					dehydrator.Write(instruction.Key);
					dehydrator.Write(instruction.Value);
				}
			}
		}
		

		public class ApplicantEntry : IBinarySerializable{

			public readonly List<PublicValidatorEntry> Validators = new List<PublicValidatorEntry>();
			public readonly SafeArrayHandle Secret = new SafeArrayHandle();

			public void Rehydrate(IDataRehydrator rehydrator) {
				
				int count = rehydrator.ReadInt();

				this.Validators.Clear();
				for(int i = 0; i < count; i++) {

					var validator = new PublicValidatorEntry();
					validator.Rehydrate(rehydrator);
					
					this.Validators.Add(validator);
				}
				
				this.Secret.Entry = rehydrator.ReadNonNullableArray();
			}

			public void Dehydrate(IDataDehydrator dehydrator) {
				
				dehydrator.Write(this.Validators.Count);

				foreach(var validator in this.Validators) {
					validator.Dehydrate(dehydrator);
				}
				
				dehydrator.WriteNonNullable(Secret);
			}
		}

		public abstract class ValidatorEntry : IBinarySerializable{
			
			public Guid IP { get; set; }
			public int? ValidationPort { get; set; }
			
			public virtual void Rehydrate(IDataRehydrator rehydrator) {
				this.IP = rehydrator.ReadGuid();
				this.ValidationPort = rehydrator.ReadNullableInt();
			}

			public virtual void Dehydrate(IDataDehydrator dehydrator) {
				dehydrator.Write(IP);
				dehydrator.Write(ValidationPort);
			}
			
		}
		public class PublicValidatorEntry : ValidatorEntry {
			public SafeArrayHandle SecretCode { get; set; } = new SafeArrayHandle();
			
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
			public readonly List<SecretValidatorEntry> Validators = new List<SecretValidatorEntry>();
			public int SecretCode { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {
				
				this.SecretCode = rehydrator.ReadInt();
				
				int count = rehydrator.ReadInt();

				this.Validators.Clear();
				for(int i = 0; i < count; i++) {

					var validator = new SecretValidatorEntry();
					validator.Rehydrate(rehydrator);
					
					this.Validators.Add(validator);
				}
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.SecretCode);
				
				dehydrator.Write(this.Validators.Count);

				foreach(var validator in this.Validators) {
					validator.Dehydrate(dehydrator);
				}
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
		
		protected override ComponentVersion<BlockchainMessageType> SetIdentity() {
			return (BlockchainMessageTypes.Instance.APPOINTMENT_CONTEXT, 1, 0);
		}
	}
}