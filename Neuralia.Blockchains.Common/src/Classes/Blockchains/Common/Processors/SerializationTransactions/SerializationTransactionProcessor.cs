using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions.Operations;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Processors.SerializationTransactions {

	/// <summary>
	///     This processor gives us the ability to stack undo operations to filesystem operations which we can then use to
	///     ensure transactional commits
	/// </summary>
	public class SerializationTransactionProcessor : IDisposableExtended {

		private const string UNDO_FILE_NAME = "SerializationTransaction.undo";

		private readonly string cachePath;
		private readonly FileSystemWrapper fileSystem;
		protected readonly Queue<Func<Task>> Operations = new Queue<Func<Task>>();

		protected readonly Stack<SerializationTransactionOperation> UndoOperations = new Stack<SerializationTransactionOperation>();

		private bool commited;
		private bool restored;

		public SerializationTransactionProcessor(string cachePath, FileSystemWrapper fileSystem) {
			this.cachePath = cachePath;
			this.fileSystem = fileSystem;
		}

		public void Check(IChainDataWriteProvider chainDataWriteProvider) {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.FileExists(filename)) {
				this.LoadUndoOperations(chainDataWriteProvider);

				this.Rollback();
			}
		}

		private string GetUndoFilePath() {
			return Path.Combine(this.cachePath, UNDO_FILE_NAME);
		}

		public void AddOperation(Func<Task> operation, SerializationTransactionOperation undoOperation) {
			if(undoOperation != null) {
				this.UndoOperations.Push(undoOperation);
			}

			this.Operations.Enqueue(operation);
		}

		public async Task Apply() {

			this.SerializeUndoOperations();

			foreach(Func<Task> action in this.Operations.ToArray().Where(a => a != null)) {
				await action().ConfigureAwait(false);
			}

			this.Operations.Clear();
		}

		public void DeleteUndoFile() {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.FileExists(filename)) {
				this.fileSystem.DeleteFile(filename);
			}
		}

		public void SerializeUndoOperations() {
			using IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write(this.UndoOperations.Count);

			foreach(SerializationTransactionOperation entry in this.UndoOperations) {
				entry.Dehydrate(dehydrator);
			}

			using SafeArrayHandle bytes = dehydrator.ToArray();

			this.DeleteUndoFile();

			string filename = this.GetUndoFilePath();

			FileExtensions.EnsureDirectoryStructure(this.cachePath, this.fileSystem);

			FileExtensions.WriteAllBytes(filename, bytes, this.fileSystem);
		}

		public void LoadUndoOperations(IChainDataWriteProvider chainDataWriteProvider) {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.FileExists(filename)) {
				SafeArrayHandle bytes = FileExtensions.ReadAllBytes(filename, this.fileSystem);

				IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

				int count = rehydrator.ReadInt();

				this.UndoOperations.Clear();

				List<SerializationTransactionOperation> loadedOperations = new List<SerializationTransactionOperation>();

				for(int i = 0; i < count; i++) {
					loadedOperations.Add(SerializationTransactionOperationFactory.Rehydrate(rehydrator, chainDataWriteProvider));
				}

				// its a stack, so lets reverse it all
				loadedOperations.Reverse();

				foreach(SerializationTransactionOperation entry in loadedOperations) {
					this.UndoOperations.Push(entry);
				}
			}
		}

		public void RestoreSnapshot() {

			if(this.restored) {
				return;
			}

			this.Operations.Clear();

			List<Action> actions = new List<Action>();

			foreach(SerializationTransactionOperation entry in this.UndoOperations) {
				actions.Add(() => entry.Undo());
			}

			IndependentActionRunner.Run(actions);

			this.UndoOperations.Clear();
			this.restored = true;
		}

		public void Commit() {
			this.commited = true;
		}

		public void Uncommit() {

			this.commited = false;
		}

		public void Rollback() {
			if(!this.commited) {

				this.RestoreSnapshot();

				this.DeleteUndoFile();
			}
		}

	#region Disposable

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {
			if(!this.IsDisposed && disposing) {
				if(this.commited) {
					this.DeleteUndoFile();
				} else {
					this.Rollback();
				}
			}

			this.IsDisposed = true;
		}

		~SerializationTransactionProcessor() {
			this.Dispose(false);
		}

		public bool IsDisposed { get; private set; }

	#endregion

	}
}