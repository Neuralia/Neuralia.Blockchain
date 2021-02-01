using System;
using System.Collections.Generic;
using System.Net;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Types.Dynamic;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.V1 {
	public class ValidatorProtocol1 : IValidatorProtocol {
		public const ushort PROTOCOL_VERSION = 1;
		private BlockchainType blockchainType;

		private Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate;
		private int timeout;

		public ValidatorProtocol1() {

		}

		public ValidatorProtocol1(BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate = null, int timeout = 10000) {
			this.Initialize(blockchainType, getValidatorDelegate, timeout);
		}

		public void Initialize(BlockchainType blockchainType, Func<BlockchainType, IAppointmentValidatorDelegate> getValidatorDelegate = null, int timeout = 10000) {
			this.getValidatorDelegate = getValidatorDelegate;
			this.blockchainType = blockchainType;
			this.timeout = timeout;
		}

		public async Task<bool> HandleServerExchange(ValidatorConnectionSet connectionSet, ByteArray operationBytes, CancellationToken? ct = null) {

			Protocol1Envelope envelope = new();
			envelope.Rehydrate(operationBytes, true);

			ValidatorProtocol1Tools.ValidatorOperation operation = envelope.operation;

			return await this.HandleServerExchange(operation, connectionSet, ct).ConfigureAwait(false);
		}

		public async Task<(SafeArrayHandle validatorCode, bool hasConnected)> RequestCodeTranslation(DateTime appointment, int index, SafeArrayHandle validatorCode, IPAddress address, int? port = null) {

			CodeTranslationRequestOperation operation = new();

			operation.Appointment = appointment;
			operation.ValidatorCode = validatorCode;
			operation.Index = index;

			using ITcpValidatorConnection connection = await ValidatorProtocol1Tools.Connect(operation, blockchainType.Value, address, port).ConfigureAwait(false);

			Task<CodeTranslationResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<CodeTranslationResponseOperation>(connection, default);
			using CancellationTokenSource tokenSource = new();
			await Task.WhenAny(task, Task.Delay(this.timeout, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				CodeTranslationResponseOperation resultOperation = task.Result;

				return (resultOperation.ValidatorCode, connection.HasConnected);
			}

			throw new InvalidValidatorConnectionException(connection.HasConnected);
		}

		public async Task<(int secretCodeL2, bool hasConnected)> TriggerSession(DateTime appointment, int index, int code, IPAddress address, int? port = null) {

			TriggerSessionOperation operation = new();

			operation.Appointment = appointment;
			operation.SecretCode = code;
			operation.Index = index;

			using ITcpValidatorConnection connection = await ValidatorProtocol1Tools.Connect(operation, blockchainType.Value, address, port).ConfigureAwait(false);

			Task<TriggerSessionResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<TriggerSessionResponseOperation>(connection, default);
			using CancellationTokenSource tokenSource = new();
			await Task.WhenAny(task, Task.Delay(this.timeout, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				TriggerSessionResponseOperation resultOperation = task.Result;

				return (resultOperation.SecretCodeL2, connection.HasConnected);
			}

			throw new InvalidValidatorConnectionException(connection.HasConnected);
		}

		public async Task<(bool completed, bool hasConnected)> RecordPuzzleCompleted(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, IPAddress address, int? port = null) {

			PuzzleCompletedOperation operation = new();

			operation.Appointment = appointment;
			operation.Index = index;

			foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in results) {
				operation.Results.Add(key, value);
			}

			using ITcpValidatorConnection connection = await ValidatorProtocol1Tools.Connect(operation, blockchainType.Value, address, port).ConfigureAwait(false);

			Task<PuzzleCompletedResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<PuzzleCompletedResponseOperation>(connection, default);
			using CancellationTokenSource tokenSource = new();
			await Task.WhenAny(task, Task.Delay(this.timeout, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				PuzzleCompletedResponseOperation resultOperation = task.Result;

				return (resultOperation.Result, connection.HasConnected);
			}

			throw new InvalidValidatorConnectionException(connection.HasConnected);
		}

		public async Task<(bool completed, bool hasConnected)> RecordTHSCompleted(DateTime appointment, int index, SafeArrayHandle thsResults, IPAddress address, int? port = null) {

			THSCompletedOperation operation = new();

			operation.Appointment = appointment;
			operation.Index = index;
			operation.THSResults.Entry = thsResults?.Entry;

			using ITcpValidatorConnection connection = await ValidatorProtocol1Tools.Connect(operation, blockchainType.Value, address, port).ConfigureAwait(false);

			Task<THSCompletedResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<THSCompletedResponseOperation>(connection, default);
			using CancellationTokenSource tokenSource = new();
			await Task.WhenAny(task, Task.Delay(this.timeout, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				THSCompletedResponseOperation resultOperation = task.Result;

				return (resultOperation.Result, connection.HasConnected);
			}

			throw new InvalidValidatorConnectionException(connection.HasConnected);
		}

		protected virtual async Task<bool> HandleServerExchange(ValidatorProtocol1Tools.ValidatorOperation operation, ValidatorConnectionSet connectionSet, CancellationToken? ct = null) {

			if(operation.OperationId == CodeTranslationRequestOperation.CODE_TRANSLATION_REQUEST_OPERATION_ID) {
				// this is the workflow
				return await this.HandleCodeTranslationWorkflow((CodeTranslationRequestOperation) operation, connectionSet, ct).ConfigureAwait(false);
			}

			if(operation.OperationId == TriggerSessionOperation.TRIGGER_SESSION_OPERATION_ID) {
				// this is the workflow
				return await this.HandleTriggerSessionWorkflow((TriggerSessionOperation) operation, connectionSet, ct).ConfigureAwait(false);
			}

			if(operation.OperationId == PuzzleCompletedOperation.PUZZLE_COMPLETED_OPERATION_ID) {
				// this is the workflow
				return await this.HandlePuzzleCompletedWorkflow((PuzzleCompletedOperation) operation, connectionSet, ct).ConfigureAwait(false);
			}

			if(operation.OperationId == THSCompletedOperation.THS_COMPLETED_OPERATION_ID) {
				// this is the workflow
				return await this.HandleTHSCompletedWorkflow((THSCompletedOperation) operation, connectionSet, ct).ConfigureAwait(false);
			}

			IAppointmentValidatorDelegate validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			//blacklist
			BanForADay(connectionSet, "Bad handler");

			throw new InvalidValidatorConnectionException();
		}

		public static void BanForADay(ValidatorConnectionSet connectionSet, string details, IPMarshall.QuarantineReason reason = IPMarshall.QuarantineReason.ValidationFailed) {
			IPEndPoint endpoint = (IPEndPoint) connectionSet.Socket.RemoteEndPoint;
			BanForADay(endpoint.Address, details, reason);
		}
		
		public static void BanForADay(IPAddress address, string details, IPMarshall.QuarantineReason reason = IPMarshall.QuarantineReason.ValidationFailed) {
			if(GlobalSettings.ApplicationSettings.EnableAppointmentValidatorIPMarshall) {
				IPMarshall.ValidationInstance.Quarantine(address, reason, DateTimeEx.CurrentTime.AddDays(1), details, GlobalsService.APPOINTMENT_STRIKE_COUNT, TimeSpan.MaxValue);
			}
		}

		private async Task<bool> HandleCodeTranslationWorkflow(CodeTranslationRequestOperation operation, ValidatorConnectionSet connectionSet, CancellationToken? ct = null) {

			IAppointmentValidatorDelegate validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			bool returnResult = false;
			bool operationValid = false;
			bool operationNull = false;
			
			try {
				CodeTranslationResponseOperation resultOperation = null;

				if(validatorDelegate != null) {
					(resultOperation, operationValid) = await validatorDelegate.HandleCodeTranslationWorkflow(operation).ConfigureAwait(false);
				}

				operationNull = resultOperation == null;

				if(!operationNull) {
					returnResult = ValidatorProtocol1Tools.SendOperation(resultOperation, connectionSet);
				}

			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleCodeTranslationWorkflow)}");
				operationValid = false;
			}
			
			if(!operationValid) {
				// that was bad
				BanForADay(connectionSet, $"failed in {nameof(this.HandleCodeTranslationWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return returnResult;
		}

		private async Task<bool> HandleTriggerSessionWorkflow(TriggerSessionOperation operation, ValidatorConnectionSet connectionSet, CancellationToken? ct = null) {

			IAppointmentValidatorDelegate validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			
			bool returnResult = false;
			bool operationValid = false;
			bool operationNull = false;
			
			try {

				TriggerSessionResponseOperation resultOperation = null;
				if(validatorDelegate != null) {
					(resultOperation, operationValid) = await validatorDelegate.HandleTriggerSessionWorkflow(operation).ConfigureAwait(false);
				}

				operationNull = resultOperation == null;

				if(!operationNull) {
					returnResult = ValidatorProtocol1Tools.SendOperation(resultOperation, connectionSet);
				}
				
			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleTriggerSessionWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				BanForADay(connectionSet, $"failed in {nameof(this.HandleTriggerSessionWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return returnResult;
		}

		private async Task<bool> HandlePuzzleCompletedWorkflow(PuzzleCompletedOperation operation, ValidatorConnectionSet connectionSet, CancellationToken? ct = null) {

			IAppointmentValidatorDelegate validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			
			bool returnResult = false;
			bool operationValid = false;
			bool operationNull = false;
			
			try {

				PuzzleCompletedResponseOperation resultOperation = null;
				if(validatorDelegate != null) {
					(resultOperation, operationValid) = await validatorDelegate.HandlePuzzleCompletedWorkflow(operation).ConfigureAwait(false);
				}
				
				operationNull = resultOperation == null;

				if(!operationNull) {
					returnResult = ValidatorProtocol1Tools.SendOperation(resultOperation, connectionSet);
				}

			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandlePuzzleCompletedWorkflow)}");
				operationValid = false;
			}

			if(!operationValid) {
				// that was bad
				BanForADay(connectionSet, $"failed in {nameof(this.HandlePuzzleCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return returnResult;
		}

		private async Task<bool> HandleTHSCompletedWorkflow(THSCompletedOperation operation, ValidatorConnectionSet connectionSet, CancellationToken? ct = null) {

			IAppointmentValidatorDelegate validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			bool returnResult = false;
			bool operationValid = false;
			bool operationNull = false;
			
			try {

				THSCompletedResponseOperation resultOperation = null;
				if(validatorDelegate != null) {
					(resultOperation, operationValid) = await validatorDelegate.HandleTHSCompletedWorkflow(operation).ConfigureAwait(false);
				}
				
				operationNull = resultOperation == null;

				if(!operationNull) {
					returnResult = ValidatorProtocol1Tools.SendOperation(resultOperation, connectionSet);
				}
				
			} catch(Exception ex) {
				NLog.LoggingBatcher.Verbose(ex, $"failed in {nameof(this.HandleTHSCompletedWorkflow)}");
				operationValid = false;
			}
			
			if(!operationValid) {
				// that was bad
				BanForADay(connectionSet, $"failed in {nameof(this.HandleTHSCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}");
			}
			
			return returnResult;
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

				if(operationId == PuzzleCompletedOperation.PUZZLE_COMPLETED_OPERATION_ID) {
					this.operation = new PuzzleCompletedOperation();
				}

				if(operationId == PuzzleCompletedResponseOperation.PUZZLE_COMPLETED_RESPONSE_OPERATION_ID) {
					this.operation = new PuzzleCompletedResponseOperation();
				}

				if(operationId == THSCompletedOperation.THS_COMPLETED_OPERATION_ID) {
					this.operation = new THSCompletedOperation();
				}

				if(operationId == THSCompletedResponseOperation.THS_COMPLETED_RESPONSE_OPERATION_ID) {
					this.operation = new THSCompletedResponseOperation();
				}

				if(this.operation == null) {
					throw new InvalidValidatorConnectionException();
				}

				this.operation.Rehydrate(rehydrator);
			}

			public void Dehydrate(IDataDehydrator dehydrator) {

				dehydrator.Write(this.operation.OperationId);
			}

			public void Rehydrate(ByteArray bytes, bool skipSize = false) {
				using IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

				if(skipSize) {
					// the bytes of the operation size, since we dont need them
					rehydrator.Skip(sizeof(ushort));
				}

				this.Rehydrate(rehydrator);
			}
		}

	#region Code Translation

		public class CodeTranslationRequestOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte CODE_TRANSLATION_REQUEST_OPERATION_ID = 1;

			public DateTime Appointment { get; set; }
			public SafeArrayHandle ValidatorCode { get; set; }
			public int Index { get; set; }

			public override byte OperationId => CODE_TRANSLATION_REQUEST_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Appointment = rehydrator.ReadDateTime();
				this.ValidatorCode = (SafeArrayHandle) rehydrator.ReadNonNullableArray();

				AdaptiveLong1_9 tool = new();
				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.Appointment);
				dehydrator.WriteNonNullable(this.ValidatorCode);

				AdaptiveLong1_9 tool = new();
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

				this.ValidatorCode = (SafeArrayHandle) rehydrator.ReadNonNullableArray();
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
			public int Index { get; set; }

			public override byte OperationId => TRIGGER_SESSION_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Appointment = rehydrator.ReadDateTime();
				this.SecretCode = rehydrator.ReadInt();

				AdaptiveLong1_9 tool = new();
				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.Appointment);
				dehydrator.Write(this.SecretCode);

				AdaptiveLong1_9 tool = new();
				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}
		}

		public class TriggerSessionResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte TRIGGER_SESSION_RESPONSE_OPERATION_ID = 4;

			public int SecretCodeL2 { get; set; }
			public override byte OperationId => TRIGGER_SESSION_RESPONSE_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.SecretCodeL2 = rehydrator.ReadInt();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				dehydrator.Write(this.SecretCodeL2);
			}
		}

	#endregion

	#region Puzzle Completed

		public class PuzzleCompletedOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte PUZZLE_COMPLETED_OPERATION_ID = 5;

			public DateTime Appointment { get; set; }

			public Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> Results { get; } = new();
			public int Index { get; set; }
			public override byte OperationId => PUZZLE_COMPLETED_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				AdaptiveLong1_9 tool = new();

				this.Appointment = rehydrator.ReadDateTime();

				tool.Rehydrate(rehydrator);
				int count = (int) tool.Value;

				this.Results.Clear();

				for(int i = 0; i < count; i++) {

					tool.Rehydrate(rehydrator);
					Enums.AppointmentsResultTypes key = (Enums.AppointmentsResultTypes) tool.Value;
					SafeArrayHandle value = (SafeArrayHandle) rehydrator.ReadArray();

					this.Results.Add(key, value);
				}

				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				AdaptiveLong1_9 tool = new();

				dehydrator.Write(this.Appointment);

				tool.Value = this.Results.Count;
				tool.Dehydrate(dehydrator);

				foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in this.Results) {

					tool.Value = (int) key;
					tool.Dehydrate(dehydrator);

					dehydrator.Write(value);
				}

				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);
			}
		}

		public class PuzzleCompletedResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte PUZZLE_COMPLETED_RESPONSE_OPERATION_ID = 6;

			public bool Result { get; set; }
			public override byte OperationId => PUZZLE_COMPLETED_RESPONSE_OPERATION_ID;

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

	#region THS Completed

		public class THSCompletedOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte THS_COMPLETED_OPERATION_ID = 7;

			public DateTime Appointment { get; set; }

			public SafeArrayHandle THSResults { get; } = SafeArrayHandle.Create();
			public int Index { get; set; }
			public override byte OperationId => THS_COMPLETED_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				AdaptiveLong1_9 tool = new();

				this.Appointment = rehydrator.ReadDateTime();

				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;

				this.THSResults.Entry = rehydrator.ReadNullEmptyArray();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				AdaptiveLong1_9 tool = new();

				dehydrator.Write(this.Appointment);

				tool.Value = this.Index;
				tool.Dehydrate(dehydrator);

				dehydrator.Write(this.THSResults);
			}
		}

		public class THSCompletedResponseOperation : ValidatorProtocol1Tools.ValidatorOperation {

			public const byte THS_COMPLETED_RESPONSE_OPERATION_ID = 8;

			public bool Result { get; set; }
			public override byte OperationId => THS_COMPLETED_RESPONSE_OPERATION_ID;

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