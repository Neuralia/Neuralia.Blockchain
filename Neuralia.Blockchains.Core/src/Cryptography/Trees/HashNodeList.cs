#if DEBUG

//used for debugging the source of hash errors
//#define LOG_SOURCE
#endif

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Core.Cryptography.Trees {
	public class HashNodeList : IHashNodeList, IDisposableExtended {

		public interface IHashNode : IDisposableExtended {
			SafeArrayHandle Data { get; }
			bool IsEmpty { get; }
		}

		public abstract class HashNode : IHashNode {

			public abstract SafeArrayHandle Data { get; }
			public abstract bool IsEmpty { get; }

		#region Dispose

			public bool IsDisposed { get; private set; }

			public void Dispose() {
				this.Dispose(true);
				GC.SuppressFinalize(this);
			}

			private void Dispose(bool disposing) {

				if(disposing && !this.IsDisposed) {
					this.DisposeAll();
				}

				this.IsDisposed = true;
			}

			protected virtual void DisposeAll() {

			}

			~HashNode() {
				this.Dispose(false);
			}

		#endregion

		}

		private class ArrayHashNode : HashNode {

			public ArrayHashNode(SafeArrayHandle data) {
				this.Data = data;
			}

			public override SafeArrayHandle Data { get; }
			public override bool IsEmpty => this.Data?.IsEmpty ?? true;

			protected override void DisposeAll() {
				base.DisposeAll();

				this.Data?.Dispose();
			}
		}

		public class LazyHashNode<T, S> : HashNode
			where S : class {

			private readonly Func<T, S, SafeArrayHandle> action;
			private readonly T entry;
			private readonly S state;
#if DEBUG
			private bool alreadyCalled;
#endif

			public LazyHashNode(T entry, Func<T, S, SafeArrayHandle> action, S state = null) {
				this.action = action;
				this.state = state;
				this.entry = entry;
			}

			public override SafeArrayHandle Data {
				get {
#if DEBUG
					if(this.alreadyCalled) {
						throw new InvalidOperationException("This operation was called twice, this is VERY inefficient!");
					}

					this.alreadyCalled = true;
#endif
					return this.action(this.entry, this.state);
				}
			}

			public override bool IsEmpty => this.action == null;
		}

		public HashNodeList() {

#if LOG_SOURCE
		Console.WriteLine("WARNING!!! HashNodeList is writing stacktrace!!!");
			NLog.Default.Warning("WARNING!!! HashNodeList is writing stacktrace!!!");
#endif
		}

		private readonly List<IHashNode> nodes = new List<IHashNode>();

#if LOG_SOURCE
		public List<IHashNode> Nodes => this.nodes;
		public readonly List<string> Sources = new List<string>();

#endif
		public SafeArrayHandle this[int i] => this.nodes[i].Data;

		public int Count => this.nodes.Count;

		public HashNodeList Add(byte value) {
			return this.AddOwn(new[] {value});
		}

		public HashNodeList Add(byte? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(IHashNode value) {

			this.nodes.Add(value);

#if LOG_SOURCE
			this.Sources.Add(System.Environment.StackTrace.ToString());
#endif

			return this;
		}

		public HashNodeList Add(short value) {

			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(short? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(ushort value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(ushort? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(int value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(int? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(uint value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(uint? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(long value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(long? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(ulong value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(ulong? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(double value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(double? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(decimal value) {
			return this.AddOwn(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(decimal? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(bool value) {
			return this.Add(TypeSerializer.Serialize(value));
		}

		public HashNodeList Add(bool? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(Guid value) {
			return this.AddOwn(value.ToByteArray());
		}

		public HashNodeList Add(Guid? value) {
			return this.Add(value ?? Guid.Empty);
		}

		public HashNodeList Add(string value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.AddOwn(Encoding.UTF8.GetBytes(value));
			}

			return this;
		}

		public HashNodeList Add(DateTime value) {
			return this.Add(value.Ticks);
		}

		public HashNodeList Add(DateTime? value) {
			if(value == null) {
				this.AddNull();
			} else {
				this.Add(value.Value);
			}

			return this;
		}

		public HashNodeList Add(SafeArrayHandle array) {
			if(array == null) {
				this.AddNull();
			} else {
				this.Add(new ArrayHashNode(array.Branch()));
			}

			return this;
		}

		public HashNodeList AddOwn(Span<byte> array) {
			return this.AddOwn(array.ToArray());
		}

		public HashNodeList Add(Span<byte> array) {
			return this.Add(array.ToArray());
		}

		private HashNodeList AddOwn(byte[] array) {
			this.Add(new ArrayHashNode(ByteArray.Create(array)));

			return this;
		}

		public HashNodeList Add(byte[] array) {
			this.Add(new ArrayHashNode(ByteArray.Wrap(array)));

			return this;
		}

		public HashNodeList Add(ByteArray array) {
			this.Add(new ArrayHashNode(ByteArray.Wrap(array)));

			return this;
		}

		public HashNodeList AddOwn(ByteArray array) {
			this.Add(new ArrayHashNode(ByteArray.Create(array)));

			return this;
		}

		public HashNodeList Add(ref byte[] array, int length) {
			this.Add(new ArrayHashNode(ByteArray.Create(array, length)));

			return this;
		}

		public HashNodeList Add(byte[] array, int length) {

			byte[] bytes = array;

			return this.Add(ref bytes, length);
		}

		public HashNodeList Add(Enum entry) {
			return this.Add(entry.ToString());
		}

		public HashNodeList Add(object obj) {
			if(obj == null) {
				return this.AddNull();
			}

			if(obj is byte b1) {
				return this.Add(b1);
			}

			if(obj is short s1) {
				return this.Add(s1);
			}

			if(obj is ushort @ushort) {
				return this.Add(@ushort);
			}

			if(obj is int i) {
				return this.Add(i);
			}

			if(obj is uint u) {
				return this.Add(u);
			}

			if(obj is long @long) {
				return this.Add(@long);
			}

			if(obj is ulong @ulong) {
				return this.Add(@ulong);
			}

			if(obj is double d) {
				return this.Add(d);
			}

			if(obj is decimal dec) {
				return this.Add(dec);
			}

			if(obj is bool b) {
				return this.Add(b);
			}

			if(obj is Guid guid) {
				return this.Add(guid);
			}

			if(obj is string s) {
				return this.Add(s);
			}

			if(obj is DateTime time) {
				return this.Add(time);
			}

			if(obj is SafeArrayHandle array) {
				return this.Add(array);
			}

			if(obj is ByteArray byteArray) {
				return this.Add(byteArray);
			}

			if(obj is byte[] bytes) {
				return this.Add(bytes);
			}

			if(obj is Enum @enum) {
				return this.Add(@enum);
			}

			throw new ApplicationException("Unsupported object type");
		}

		public HashNodeList AddNull() {
			this.Add(ByteArray.Empty());

			return this;
		}

		public HashNodeList Add<T, U>(KeyValuePair<T, U> kv)
			where T : ITreeHashable {

			this.Add(kv.Key);
			this.Add(kv.Value);

			return this;
		}

		public HashNodeList Add<T, U>(IDictionary<T, U> nodes)
			where T : ITreeHashable {

			this.Add(nodes.Count);

			foreach(KeyValuePair<T, U> entry in nodes) {
				this.Add(entry);
			}

			return this;
		}

		public HashNodeList Add<T>(IOrderedEnumerable<T> nodes)
			where T : ITreeHashable {

			return this.Add((IEnumerable<T>) nodes);
		}

		public HashNodeList Add<T>(IEnumerable<T> nodes)
			where T : ITreeHashable {

			this.Add(nodes.Count());

			foreach(T node in nodes) {
				this.Add(node);
			}

			return this;
		}

		public HashNodeList Add<T>(List<T> nodes)
			where T : ITreeHashable {

			return this.Add((IEnumerable<T>) nodes);
		}

		public HashNodeList Add(ITreeHashable treeHashable) {

			return this.Add<ITreeHashable>(treeHashable);
		}

		public HashNodeList Add<T>(T treeHashable)
			where T : ITreeHashable {

			if(treeHashable != null) {
				this.Add(treeHashable.GetStructuresArray());
			} else {
				this.AddNull();
			}

			return this;
		}

		public HashNodeList Add(HashNodeList nodeList) {
			if(nodeList != null) {
				this.nodes.AddRange(nodeList.nodes.Where(n => (n != null) && !n.IsEmpty));

#if LOG_SOURCE
				var indices = nodeList.nodes.Where(n => (n != null) && !n.IsEmpty).Select((e, i) => i).ToList();

				foreach(var index in indices) {
					this.Sources.Add(nodeList.Sources[index]);
				}
#endif
			} else {
				this.AddNull();
			}

			return this;
		}

	#region Dispose

		public bool IsDisposed { get; private set; }

		public void Dispose() {
			this.Dispose(true);
			GC.SuppressFinalize(this);
		}

		private void Dispose(bool disposing) {

			if(disposing && !this.IsDisposed) {

				foreach(IHashNode entry in this.nodes) {
					entry?.Dispose();
				}
			}

			this.IsDisposed = true;
		}

		~HashNodeList() {
			this.Dispose(false);
		}

	#endregion

	}
}