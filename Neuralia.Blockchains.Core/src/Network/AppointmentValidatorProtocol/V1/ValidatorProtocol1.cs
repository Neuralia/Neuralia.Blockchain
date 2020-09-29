using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1 {
	public class ValidatorProtocol1 : IValidatorProtocol {
		public const ushort PROTOCOL_VERSION = 1;

		private readonly Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate;
		private readonly BlockchainType blockchainType;
		
		public ValidatorProtocol1(BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate = null) {
			this.getValidatorDelegate = getValidatorDelegate;
			this.blockchainType = blockchainType;
		}

		public async Task HandleServerExchange(ITcpValidatorConnection connection, CancellationToken ct) {

			ValidatorProtocol1Tools.ValidatorOperation operation = await ValidatorProtocol1Tools.ReceiveOperation<ValidatorProtocol1Tools.ValidatorOperation>(connection, ct).ConfigureAwait(false);

			if(operation.OperationId == CodeTranslationRequestOperation.CODE_TRANSLATION_REQUEST_OPERATION_ID) {
				// this is the workflow
				await this.HandleCodeTranslationWorkflow((CodeTranslationRequestOperation) operation, connection, ct).ConfigureAwait(false);
			}

			else if(operation.OperationId == TriggerSessionOperation.TRIGGER_SESSION_OPERATION_ID) {
				// this is the workflow
				await this.HandleTriggerSessionWorkflow((TriggerSessionOperation) operation, connection, ct).ConfigureAwait(false);
			}

			else if(operation.OperationId == CompleteSessionOperation.COMPLETE_SESSION_OPERATION_ID) {
				// this is the workflow
				await this.HandleCompleteSessionWorkflow((CompleteSessionOperation) operation, connection, ct).ConfigureAwait(false);
			} else {
				//blacklist
				var endpoint = (IPEndPoint) connection.RemoteEndPoint;
				IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.ValidationFailed, DateTimeEx.CurrentTime.AddDays(6));

				throw new InvalidOperationException();
			}
			
		}
		
		public async Task<SafeArrayHandle> RequestCodeTranslation(DateTime appointment, long index, SafeArrayHandle validatorCode, IPAddress address, int? port = null) {

			var operation = new CodeTranslationRequestOperation();

			operation.Appointment = appointment;
			operation.ValidatorCode = validatorCode;
			operation.Index = index;

			using ITcpValidatorConnection connection = ValidatorProtocol1Tools.Connect(operation, this.blockchainType.Value, address, port);

			Task<CodeTranslationResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<CodeTranslationResponseOperation>(connection, default);
			using var tokenSource = new CancellationTokenSource();
			await Task.WhenAny(task, Task.Delay(10000, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				CodeTranslationResponseOperation resultOperation = task.Result;

				return resultOperation.ValidatorCode;
			}

			throw new InvalidOperationException();
		}

		public async Task<ushort> TriggerSession(DateTime appointment, long index, int code, IPAddress address, int? port = null) {

			var operation = new TriggerSessionOperation();

			operation.Appointment = appointment;
			operation.SecretCode = code;
			operation.Index = index;

			using ITcpValidatorConnection connection = ValidatorProtocol1Tools.Connect(operation, this.blockchainType.Value, address, port);

			Task<TriggerSessionResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<TriggerSessionResponseOperation>(connection, default);
			using var tokenSource = new CancellationTokenSource();
			await Task.WhenAny(task, Task.Delay(10000, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				TriggerSessionResponseOperation resultOperation = task.Result;

				return resultOperation.SecretCodeL2;
			}

			throw new InvalidOperationException();
		}

		public async Task<bool> CompleteSession(DateTime appointment, long index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results , IPAddress address, int? port = null) {

			var operation = new CompleteSessionOperation();

			operation.Appointment = appointment;
			operation.Index = index;
			
			foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in results) {
				operation.Results.Add(key, value);
			}
			
			using ITcpValidatorConnection connection = ValidatorProtocol1Tools.Connect(operation, this.blockchainType.Value, address, port);

			Task<CompleteSessionResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<CompleteSessionResponseOperation>(connection, default);
			using var tokenSource = new CancellationTokenSource();
			await Task.WhenAny(task, Task.Delay(10000, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				CompleteSessionResponseOperation resultOperation = task.Result;

				return resultOperation.Result;
			}

			throw new InvalidOperationException();
		}

		private async Task HandleCodeTranslationWorkflow(CodeTranslationRequestOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {

			var validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			if(validatorDelegate != null) {
				CodeTranslationResponseOperation result = null;

				try {
					result = await validatorDelegate.HandleCodeTranslationWorkflow(operation).ConfigureAwait(false);
				} catch {
					
				}

				if(result == null) {
					// blacklist
					this.BanForAWeek(connection);
					return;
				}
				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection);
			}
		}

		private void BanForAWeek(ITcpValidatorConnection connection, IPMarshall.QuarantineReason reason = IPMarshall.QuarantineReason.ValidationFailed)
		{
			var endpoint = (IPEndPoint) connection.RemoteEndPoint;
			IPMarshall.ValidationInstance.Quarantine(endpoint.Address, reason, DateTimeEx.CurrentTime.AddDays(7).Subtract(TimeSpan.FromMinutes(5)));
		}
		
		private async Task HandleTriggerSessionWorkflow(TriggerSessionOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {

			var validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			if(validatorDelegate != null) {

				TriggerSessionResponseOperation result = null;

				try {
					result = await validatorDelegate.HandleTriggerSessionWorkflow(operation).ConfigureAwait(false);
				} catch {
					
				}

				if(result == null) {
					// blacklist
					this.BanForAWeek(connection);
					return;
				}
				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection);
			}
		}

		private async Task HandleCompleteSessionWorkflow(CompleteSessionOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {
			
			var validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			if(validatorDelegate != null) {
				CompleteSessionResponseOperation result = null;

				try {
					result = await validatorDelegate.HandleCompleteSessionWorkflow(operation).ConfigureAwait(false);
				} catch {
					
				}

				if(result == null) {
					// blacklist
					this.BanForAWeek(connection);
					return;
				}
				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection);
			}
		}

		public class Protocol1Envelope : IBinarySerializable {

			public ValidatorProtocol1Tools.ValidatorOperation operation { get; set; }

			public void Rehydrate(IDataRehydrator rehydrator) {

				byte operationId = rehydrator.ReadByte();

				if(operationId == CodeTranslationRequestOperation.CODE_TRANSLATION_REQUEST_OPERATION_ID) {
					this.operation = new CodeTranslationRequestOperation();
				}

				if(operationId == CodeTranslationResponseOperation.CODE_TRANSLATION_RESPONSE_OPERATION_ID) {
					this.operation = new CodeTranslationResponseOperation();
				}

				if(operationId == TriggerSessionOperation.TRIGGER_SESSION_OPERATION_ID) {
					this.operation = new TriggerSessionOperation();
				}

				if(operationId == TriggerSessionResponseOperation.TRIGGER_SESSION_RESPONSE_OPERATION_ID) {
					this.operation = new TriggerSessionResponseOperation();
				}

				if(operationId == CompleteSessionOperation.COMPLETE_SESSION_OPERATION_ID) {
					this.operation = new CompleteSessionOperation();
				}

				if(operationId == CompleteSessionResponseOperation.COMPLETE_SESSION_RESPONSE_OPERATION_ID) {
					this.operation = new CompleteSessionResponseOperation();
				}

				if(this.operation == null) {
					throw new InvalidOperationException();
				}

				this.operation.Rehydrate(rehydrator);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.operation.OperationId);
			}

			public void Rehydrate(ByteArray bytes) {
				using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);
				this.Rehydrate(rehydrator);
			}
		}

	#region Code Translation

		public class CodeTranslationRequestOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte CODE_TRANSLATION_REQUEST_OPERATION_ID = 1;

			public DateTime Appointment { get; set; }
			public SafeArrayHandle ValidatorCode { get; set; }
			public long Index { get; set; }

			public override byte OperationId => CODE_TRANSLATION_REQUEST_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Appointment = rehydrator.ReadDateTime();
				this.ValidatorCode = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Rehydrate(rehydrator);
				this.Index = tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.Appointment);
				dehydrator.WriteNonNullable(this.ValidatorCode);
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}
		}

		public class CodeTranslationResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte CODE_TRANSLATION_RESPONSE_OPERATION_ID = 2;

			public SafeArrayHandle ValidatorCode { get; set; }
			public override byte OperationId => CODE_TRANSLATION_RESPONSE_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.ValidatorCode = (SafeArrayHandle)rehydrator.ReadNonNullableArray();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.WriteNonNullable(this.ValidatorCode);
			}
		}

	#endregion

	#region Trigger Session

		public class TriggerSessionOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte TRIGGER_SESSION_OPERATION_ID = 3;

			public DateTime Appointment { get; set; }
			public int SecretCode { get; set; }
			public long Index { get; set; }
			
			public override byte OperationId => TRIGGER_SESSION_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Appointment = rehydrator.ReadDateTime();
				this.SecretCode = rehydrator.ReadInt();
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Rehydrate(rehydrator);
				this.Index = tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.Appointment);
				dehydrator.Write(this.SecretCode);
				
				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}
		}

		public class TriggerSessionResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte TRIGGER_SESSION_RESPONSE_OPERATION_ID = 4;

			public ushort SecretCodeL2 { get; set; }
			public override byte OperationId => TRIGGER_SESSION_RESPONSE_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.SecretCodeL2 = rehydrator.ReadUShort();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);
				
				dehydrator.Write(this.SecretCodeL2);
			}
		}

	#endregion

	#region Trigger Session

		public class CompleteSessionOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte COMPLETE_SESSION_OPERATION_ID = 5;

			public DateTime Appointment { get; set; }
			
			public Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> Results { get; }= new Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle>();
			public long Index { get; set; }
			public override byte OperationId => COMPLETE_SESSION_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				
				this.Appointment = rehydrator.ReadDateTime();
				
				int count = rehydrator.ReadInt();

				this.Results.Clear();
				for(int i = 0; i < count; i++) {
					
					tool.Rehydrate(rehydrator);
					var key = (Enums.AppointmentsResultTypes)tool.Value;
					var value = (SafeArrayHandle)rehydrator.ReadArray();
					
					this.Results.Add(key, value);
				}
				
				tool.Rehydrate(rehydrator);
				this.Index = tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				
				dehydrator.Write(this.Appointment);
				
				dehydrator.Write(this.Results.Count);

				foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in this.Results) {

					tool.Value = (int)key;
					tool.Dehydrate(dehydrator);
					
					dehydrator.Write(value);
				}
				
				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}
		}

		public class CompleteSessionResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte COMPLETE_SESSION_RESPONSE_OPERATION_ID = 6;

			public bool Result { get; set; }
			public override byte OperationId => COMPLETE_SESSION_RESPONSE_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Result = rehydrator.ReadBool();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.Result);
			}
		}

	#endregion

	}
}