//using System;
//using BTree.Collections;
//using BTree.Serialization;
//using BTree.Synchronization;
//
//namespace Blockchains.Classes.General.DAL {
//	public abstract class BTreeDAL<TKey, TValue> {
//		protected readonly BPlusTree<TKey, TValue>.OptionsV2 options;
//
//		public BTreeDAL(string filename, int valueSize, ISerializer<TKey> keySerializer, ISerializer<TValue> valueSerializer) {
//			//http://help.csharptest.net/?BTree.BPlusTree%7EBTree.Collections.BPlusTree%602.html
//
//			this.options = new BPlusTree<TKey, TValue>.OptionsV2(keySerializer, valueSerializer);
//			this.options.CalcBTreeOrder(16, valueSize);
//			this.options.StoragePerformance = StoragePerformance.Fastest;
//
//			this.options.FileBlockSize = 4096;
//			this.options.CreateFile = CreatePolicy.IfNeeded;
//			this.options.FileName = filename;
//
//			this.options.CacheKeepAliveTimeout = 10000;
//			this.options.CacheKeepAliveMinimumHistory = 0;
//			this.options.CacheKeepAliveMaximumHistory = 200;
//			this.options.CachePolicy = CachePolicy.Recent;
//
//			this.options.CallLevelLock = new IgnoreLocking();
//			this.options.LockingFactory = new LockFactory<IgnoreLocking>(); //LockFactory<SimpleReadWriteLocking>();
//			this.options.LockTimeout = 10000;
//		}
//
//		private void Open(Action<BPlusTree<TKey, TValue>> process) {
//			using(BPlusTree<TKey, TValue> tree = new BPlusTree<TKey, TValue>(this.options)) {
//				process(tree);
//			}
//		}
//
//		protected void OpenReadWrite(Action<BPlusTree<TKey, TValue>> process) {
//			this.options.CreateFile = CreatePolicy.IfNeeded;
//			this.options.ReadOnly = false;
//
//			this.Open(process);
//		}
//
//		protected void OpenRead(Action<BPlusTree<TKey, TValue>> process) {
//			this.options.CreateFile = CreatePolicy.Never;
//			this.options.ReadOnly = true;
//
//			this.Open(process);
//		}
//	}
//}

