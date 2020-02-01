using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Abstractions;
using System.Linq;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Managers;
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
		private readonly IFileSystem fileSystem;
		protected readonly Queue<Action> Operations = new Queue<Action>();

		protected readonly Stack<SerializationTransactionOperation> UndoOperations = new Stack<SerializationTransactionOperation>();

		private bool commited;
		private bool restored = false;
		
		public SerializationTransactionProcessor(string cachePath, IFileSystem fileSystem) {
			this.cachePath = cachePath;
			this.fileSystem = fileSystem;
		}

		public void Check(IChainDataWriteProvider chainDataWriteProvider) {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.File.Exists(filename)) {
				this.LoadUndoOperations(chainDataWriteProvider);

				this.Rollback();
			}
		}
		private string GetUndoFilePath() {
			return Path.Combine(this.cachePath, UNDO_FILE_NAME);
		}

		public void AddOperation(Action operation, SerializationTransactionOperation undoOperation) {
			if(undoOperation != null) {
				this.UndoOperations.Push(undoOperation);
			}

			this.Operations.Enqueue(operation);
		}

		public void Apply() {

			this.SerializeUndoOperations();

			foreach(Action action in this.Operations.ToArray().Where(a => a != null)) {
				action();
			}

			this.Operations.Clear();
		}

		public void DeleteUndoFile() {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.File.Exists(filename)) {
				this.fileSystem.File.Delete(filename);
			}
		}

		public void SerializeUndoOperations() {
			IDataDehydrator dehydrator = DataSerializationFactory.CreateDehydrator();

			dehydrator.Write(this.UndoOperations.Count);

			foreach(SerializationTransactionOperation entry in this.UndoOperations) {
				entry.Dehydrate(dehydrator);
			}

			SafeArrayHandle bytes = dehydrator.ToArray();

			this.DeleteUndoFile();

			string filename = this.GetUndoFilePath();

			FileExtensions.EnsureDirectoryStructure(this.cachePath, this.fileSystem);

			FileExtensions.WriteAllBytes(filename, bytes, this.fileSystem);

			bytes.Return();
		}

		public void LoadUndoOperations(IChainDataWriteProvider chainDataWriteProvider) {
			string filename = this.GetUndoFilePath();

			if(this.fileSystem.File.Exists(filename)) {
				SafeArrayHandle bytes = FileExtensions.ReadAllBytes(filename, this.fileSystem);

				IDataRehydrator rehydrator = DataSerializationFactory.CreateRehydrator(bytes);

				int count = rehydrator.ReadInt();

				this.UndoOperations.Clear();

				var loadedOperations = new List<SerializationTransactionOperation>();

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