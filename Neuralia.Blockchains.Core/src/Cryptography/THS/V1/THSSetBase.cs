using System;
using System.Collections.Generic;
using Neuralia.Blockchains.Core.Logging;
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Cryptography.THS.V1 {
	public abstract class THSSetBase<T, K> : IDisposableExtended
		where K : class, IDisposable {

		protected readonly Dictionary<T, K> entries = new Dictionary<T, K>();
		protected readonly List<T> tags = new List<T>();
		protected int rollingindex;

		public void Reset() {
			this.rollingindex = 0;
		}

		public void AddEntry(T tag) {

			this.tags.Add(tag);

			if(!this.entries.ContainsKey(tag)) {
				this.entries.Add(tag, this.CreateEntry(tag));
			}
		}

		protected abstract K CreateEntry(T tag);

		public K GetRollingEntry() {
			T tag = this.tags[this.rollingindex];

			this.rollingindex++;

			if(this.rollingindex == this.tags.Count) {
				this.rollingindex = 0;
			}

			return this.entries[tag];
		}

	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				try {
					foreach(K crypto in this.entries.Values) {
						try {
							crypto?.Dispose();
						} catch(Exception ex) {
							NLog.Default.Verbose("error occured", ex);
						}
					}
				} catch(Exception ex) {
					NLog.Default.Error(ex, "failed to dispose");
				}
			}

			this.IsDisposed = true;
		}

		~THSSetBase() {
			this.Dispose(false);
		}

	#endregion

	}
}