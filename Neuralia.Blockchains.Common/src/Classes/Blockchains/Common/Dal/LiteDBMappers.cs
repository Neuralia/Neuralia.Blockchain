using System;
using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	/// <summary>
	///     Lite db custom mappers all written here
	/// </summary>
	public static class LiteDBMappers {

		/// <summary>
		///     Register extra mapping types that are important
		/// </summary>
		public static void RegisterBasics() {

			BsonMapper.Global.SerializeNullValues = true;
			
			// litedb does not map these unsigned types by default. so lets add them
			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => uint.Parse(bson.AsString.ToString()));

			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => ulong.Parse(bson.AsString.ToString()));
			
			BsonMapper.Global.RegisterType(uri => uri.Ticks, bson => TimeSpan.FromTicks(bson.AsInt64));
			BsonMapper.Global.RegisterType(uri => uri.HasValue?(long)uri.Value.Ticks:(long?)null, bson => {
				if(bson.IsNull) {
					return (TimeSpan?)null;
				}

				return (TimeSpan?) TimeSpan.FromTicks(bson.AsInt64);
			});
			
			RegisterArrayTypes();

			RegisterAmount();

			RegisterAccountId();

			RegisterKeyUseIndexSet();

			RegisterTransactionId();

			RegisterTransactionTimestamp();

			RegisterBlockId();

			RegisterKeyAddress();

			RegisterWalletSnapshotTypes();
		}

		//		/// <summary>
		//		/// Register a complex object so that we can serialize tricky types correctly
		//		/// </summary>
		//		/// <typeparam name="T"></typeparam>
		//		/// <typeparam name="TImp"></typeparam>
		//		public static void RegisterComplexObject<T, TImp>() 
		//			where TImp : T, new() {
		//			
		//			BsonMapper.Global.RegisterType(dict => SerializeComplexObject<T>(dict), bson => DeserializeComplexObject<TImp>(bson.AsDocument));
		//		}

		public static void RegisterArrayTypes() {
			
			BsonMapper.Global.RegisterType<SafeArrayHandle>(uri => {
			
				if(uri != null && uri.HasData) {
					return uri.ToExactByteArray();
				}

				// this trick is needed to avoid a bug where when null, this method is not called. by setting array length 0, it is called and just as good as null
				return Array.Empty<byte>();
			}, bson => {
				var bytes = bson.AsBinary;
				if(bytes.Length == 0) {
					bytes = null;
				}
				// if null, here we return an empty safe array handle with no array entry
				return SafeArrayHandle.WrapAndOwn(bytes);
			});

			BsonMapper.Global.RegisterType<ByteArray>(uri => {

				return uri?.ToExactByteArray();
			}, bson => ByteArray.Create(bson.AsBinary));
		}

		public static void RegisterWalletSnapshotTypes() {

			BsonMapper.Global.Entity<IWalletAccountSnapshot>().Id(x => x.AccountId);
		}

		public static void RegisterAmount() {

			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new Amount(bson.AsDecimal));
		}

		public static void RegisterBlockId() {
			BlockId.RegisterBlockId();
		}

		public static void RegisterKeyAddress() {

			//			BsonMapper.Global.RegisterType<KeyAddress>
			//			(
			//				(uri) => uri.,
			//				(bson) => new BlockId((long)bson.RawValue));
		}

		public static void RegisterPublishedAddress() {

			//			BsonMapper.Global.RegisterType<PublishedAddress>
			//			(
			//				(uri) => uri.,
			//				(bson) => new BlockId((long)bson.RawValue));
		}

		public static void RegisterKeyUseIndexSet() {

			BsonMapper.Global.RegisterType(uri => uri?.ToString(), bson => new KeyUseIndexSet(bson.AsString));
			
			BsonMapper.Global.RegisterType(uri => uri?.ToString(), bson => new IdKeyUseIndexSet(bson.AsString));
		}

		public static void RegisterAccountId() {

			BsonMapper.Global.RegisterType(uri => uri.ToLongRepresentation(), bson => AccountId.FromLongRepresentation(bson.AsInt64));
		}

		public static void RegisterTransactionId() {
			TransactionId.RegisterTransactionId();
		}

		public static void RegisterTransactionTimestamp() {
			TransactionTimestamp.RegisterTransactionTimestamp();
		}

		/// <summary>
		///     register a mapper for Dictionaries that have a guid as key.static for some reasone, LiteDb does not like
		///     this.static
		///     So, we need to convert it to string.static
		///     call like this: LiteDBMappers.RegisterGuidDictionary<WALLET_KEY_HISTORY>();
		/// </summary>
		public static void RegisterGuidDictionary<T>() {
			BsonMapper.Global.RegisterType(SerializeDictionary, bson => DeserializeDictionary<T>(bson.AsDocument));
		}

		private static BsonDocument SerializeDictionary<T>(Dictionary<Guid, T> dict) {
			BsonDocument o = new BsonDocument();

			foreach(Guid key in dict.Keys) {
				T value = dict[key];

				o[key.ToString()] = BsonMapper.Global.ToDocument(value);
			}

			return o;
		}

		private static Dictionary<Guid, T> DeserializeDictionary<T>(BsonDocument value) {
			Dictionary<Guid, T> result = new Dictionary<Guid, T>();

			foreach(string key in value.Keys) {
				Guid k = Guid.Parse(key);
				T v = (T) BsonMapper.Global.ToObject(typeof(T), value[key].AsDocument);

				result.Add(k, v);
			}

			return result;
		}

		//		private static BsonDocument SerializeComplexObject<T>(T entry) {
		//			BsonDocument o = new BsonDocument();
		//
		//			PropertyInfo[] properties = entry.GetType().GetProperties(BindingFlags.Instance | BindingFlags.Public);
		//			
		//			foreach(PropertyInfo property in properties) {
		//				object value = property.GetValue(entry);
		//
		//				// now convert the problematic types
		//				if(value is UInt32) {
		//					value = value.ToString();
		//				}
		//				if(value is UInt64) {
		//					value = value.ToString();
		//				}
		//				//Had bugs here with values versus documents. this needs debugging
		//
		//				try {
		//					var document = BsonMapper.Global.ToDocument(value);
		//					o.Add(property.Name, document);
		//				} catch(Exception e) {
		//					// ok, that did not work, try a simple value
		//					o.Add(property.Name, new BsonValue(value));
		//				}
		//				
		//			}
		//
		//			return o;
		//		}
		//
		//		private static T DeserializeComplexObject<T>(BsonDocument value) where T : new() {
		//			T entry = new T();
		//
		//			Type entryType = typeof(T);
		//			
		//			foreach(string key in value.Keys) {
		//
		//				object propertyValue = value[key].AsDocument;
		//				
		//				PropertyInfo property = entryType.GetProperty(key, BindingFlags.Instance | BindingFlags.Public);
		//
		//				if(property.PropertyType == typeof(UInt32)) {
		//					propertyValue = UInt32.Parse(propertyValue.ToString());
		//				}
		//				if(property.PropertyType == typeof(UInt64)) {
		//					propertyValue = UInt64.Parse(propertyValue.ToString());
		//				} 
		//				
		//				property.SetValue(entry, propertyValue);
		//			}
		//
		//			return entry;
		//		}
	}
}