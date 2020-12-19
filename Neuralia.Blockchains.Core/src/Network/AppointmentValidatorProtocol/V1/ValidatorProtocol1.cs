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

			await this.HandleServerExchange(operation, connection, ct).ConfigureAwait(false);
		}

		protected virtual async Task HandleServerExchange(ValidatorProtocol1Tools.ValidatorOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {

			if(operation.OperationId == CodeTranslationRequestOperation.CODE_TRANSLATION_REQUEST_OPERATION_ID) {
				// this is the workflow
				await this.HandleCodeTranslationWorkflow((CodeTranslationRequestOperation) operation, connection, ct).ConfigureAwait(false);
			} else if(operation.OperationId == TriggerSessionOperation.TRIGGER_SESSION_OPERATION_ID) {
				// this is the workflow
				await this.HandleTriggerSessionWorkflow((TriggerSessionOperation) operation, connection, ct).ConfigureAwait(false);
			} else if(operation.OperationId == PuzzleCompletedOperation.PUZZLE_COMPLETED_OPERATION_ID) {
				// this is the workflow
				await this.HandlePuzzleCompletedWorkflow((PuzzleCompletedOperation) operation, connection, ct).ConfigureAwait(false);
			} else if(operation.OperationId == THSCompletedOperation.THS_COMPLETED_OPERATION_ID) {
				// this is the workflow
				await this.HandleTHSCompletedWorkflow((THSCompletedOperation) operation, connection, ct).ConfigureAwait(false);
			} else {
				//blacklist
				var endpoint = (IPEndPoint) connection.RemoteEndPoint;
				IPMarshall.ValidationInstance.Quarantine(endpoint.Address, IPMarshall.QuarantineReason.ValidationFailed, DateTimeEx.CurrentTime.AddDays(6));

				throw new InvalidOperationException();
			}

		}

		public async Task<SafeArrayHandle> RequestCodeTranslation(DateTime appointment, int index, SafeArrayHandle validatorCode, IPAddress address, int? port = null) {

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

		public async Task<int> TriggerSession(DateTime appointment, int index, int code, IPAddress address, int? port = null) {

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

		public async Task<bool> RecordPuzzleCompleted(DateTime appointment, int index, Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> results, IPAddress address, int? port = null) {

			var operation = new PuzzleCompletedOperation();

			operation.Appointment = appointment;
			operation.Index = index;

			foreach((Enums.AppointmentsResultTypes key, SafeArrayHandle value) in results) {
				operation.Results.Add(key, value);
			}

			using ITcpValidatorConnection connection = ValidatorProtocol1Tools.Connect(operation, this.blockchainType.Value, address, port);

			Task<PuzzleCompletedResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<PuzzleCompletedResponseOperation>(connection, default);
			using var tokenSource = new CancellationTokenSource();
			await Task.WhenAny(task, Task.Delay(10000, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				PuzzleCompletedResponseOperation resultOperation = task.Result;

				return resultOperation.Result;
			}

			throw new InvalidOperationException();
		}

		public async Task<bool> RecordTHSCompleted(DateTime appointment, int index, SafeArrayHandle thsResults, IPAddress address, int? port = null) {

			var operation = new THSCompletedOperation();

			operation.Appointment = appointment;
			operation.Index = index;
			operation.THSResults.Entry = thsResults?.Entry;

			using ITcpValidatorConnection connection = ValidatorProtocol1Tools.Connect(operation, this.blockchainType.Value, address, port);

			Task<THSCompletedResponseOperation> task = ValidatorProtocol1Tools.ReceiveOperation<THSCompletedResponseOperation>(connection, default);
			using var tokenSource = new CancellationTokenSource();
			await Task.WhenAny(task, Task.Delay(10000, tokenSource.Token)).ConfigureAwait(false);

			if(task.IsCompletedSuccessfully) {
				THSCompletedResponseOperation resultOperation = task.Result;

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
					this.BanForAWeek(connection, $"failed in {nameof(this.HandleCodeTranslationWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and validator code {operation.ValidatorCode}");

					return;
				}

				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection, $"failed in {nameof(this.HandleCodeTranslationWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and validator code {operation.ValidatorCode}");
			}
		}

		private void BanForAWeek(ITcpValidatorConnection connection, string details, IPMarshall.QuarantineReason reason = IPMarshall.QuarantineReason.ValidationFailed) {
			var endpoint = (IPEndPoint) connection.RemoteEndPoint;
			IPMarshall.ValidationInstance.Quarantine(endpoint.Address, reason, DateTimeEx.CurrentTime.AddDays(7).Subtract(TimeSpan.FromMinutes(5)), details);
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
					this.BanForAWeek(connection, $"failed in {nameof(this.HandleTriggerSessionWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and secret code {operation.SecretCode}");

					return;
				}

				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection, $"failed in {nameof(this.HandleTriggerSessionWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and secret code {operation.SecretCode}");
			}
		}

		private async Task HandlePuzzleCompletedWorkflow(PuzzleCompletedOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {

			var validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			if(validatorDelegate != null) {
				PuzzleCompletedResponseOperation result = null;

				try {
					result = await validatorDelegate.HandlePuzzleCompletedWorkflow(operation).ConfigureAwait(false);
				} catch {

				}

				if(result == null) {
					// blacklist
					this.BanForAWeek(connection, $"failed in {nameof(this.HandlePuzzleCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and results count {operation.Results.Count}");

					return;
				}

				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection, $"failed in {nameof(this.HandlePuzzleCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index} and results count {operation.Results.Count}");
			}
		}

		private async Task HandleTHSCompletedWorkflow(THSCompletedOperation operation, ITcpValidatorConnection connection, CancellationToken ct) {

			var validatorDelegate = this.getValidatorDelegate(this.blockchainType);

			if(validatorDelegate != null) {
				THSCompletedResponseOperation result = null;

				try {
					result = await validatorDelegate.HandleTHSCompletedWorkflow(operation).ConfigureAwait(false);
				} catch {

				}

				if(result == null) {
					// blacklist
					this.BanForAWeek(connection, $"failed in {nameof(this.HandleTHSCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}.");

					return;
				}

				ValidatorProtocol1Tools.SendOperation(result, connection);
			} else {
				// blacklist
				this.BanForAWeek(connection, $"failed in {nameof(this.HandleTHSCompletedWorkflow)} for appointment {operation.Appointment} and index {operation.Index}.");
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
			public int Index { get; set; }

			public override byte OperationId => CODE_TRANSLATION_REQUEST_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				this.Appointment = rehydrator.ReadDateTime();
				this.ValidatorCode = (SafeArrayHandle) rehydrator.ReadNonNullableArray();

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
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

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();
				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
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

			public Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle> Results { get; } = new Dictionary<Enums.AppointmentsResultTypes, SafeArrayHandle>();
			public int Index { get; set; }
			public override byte OperationId => PUZZLE_COMPLETED_OPERATION_ID;

			public override void Rehydrate(IDataRehydrator rehydrator) {
				base.Rehydrate(rehydrator);

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();

				this.Appointment = rehydrator.ReadDateTime();

				tool.Rehydrate(rehydrator);
				int count = (int) tool.Value;

				this.Results.Clear();

				for(int i = 0; i < count; i++) {

					tool.Rehydrate(rehydrator);
					var key = (Enums.AppointmentsResultTypes) tool.Value;
					var value = (SafeArrayHandle) rehydrator.ReadArray();

					this.Results.Add(key, value);
				}

				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();

				dehydrator.Write(this.Appointment);

				tool.Value =  this.Results.Count;
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

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();

				this.Appointment = rehydrator.ReadDateTime();

				tool.Rehydrate(rehydrator);
				this.Index = (int) tool.Value;

				this.THSResults.Entry = rehydrator.ReadNullEmptyArray();
			}

			public override void Dehydrate(IDataDehydrator dehydrator) {
				base.Dehydrate(dehydrator);

				AdaptiveLong1_9 tool = new AdaptiveLong1_9();

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