using System;
using System.Collections.Generic;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Blocks.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Identifiers;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account.Snapshots;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Types.Specialized;
using Neuralia.Blockchains.Tools.Data;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal {
	/// <summary>
	///     Lite db custom mappers all written here
	/// </summary>
	public static class LiteDBMappers {

		/// <summary>
		///     Register extra mapping types that are important
		/// </summary>
		public static void RegisterBasics() {

			// litedb does not map these unsigned types by default. so lets add them
			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => uint.Parse(bson.RawValue.ToString()));

			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => ulong.Parse(bson.RawValue.ToString()));

			BsonMapper.Global.RegisterType<SafeArrayHandle>(uri => uri.ToExactByteArray(), bson => (SafeArrayHandle)ByteArray.Create((byte[]) bson.RawValue));
			BsonMapper.Global.RegisterType<ByteArray>(uri => uri.ToExactByteArray(), bson => ByteArray.Create((byte[]) bson.RawValue));
			
			RegisterAmount();

			RegisterAccountId();

			RegisterKeyUseIndexSet();

			RegisterTransactionId();

			RegisterTransactionIdExtended();

			RegisterTransactionTimestamp();

			RegisterBlockId();

			RegisterKeyAddress();

			RegisterWalletSnaphostTypes();
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

		public static void RegisterWalletSnaphostTypes() {

			BsonMapper.Global.Entity<IWalletAccountSnapshot>().Id(x => x.AccountId);
		}

		public static void RegisterAmount() {

			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new Amount((decimal) bson.RawValue));
		}

		public static void RegisterBlockId() {

			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new BlockId((long) bson.RawValue));
		}

		public static void RegisterKeyAddress() {

			//			BsonMapper.Global.RegisterType<KeyAddress>
			//			(
			//				(uri) => uri.,
			//				(bson) => new BlockId((long)bson.RawValue));
		}

		public static void RegisterKeyUseIndexSet() {

			BsonMapper.Global.Entity<KeyUseIndexSet>().Ignore(x => x.Clone);
			BsonMapper.Global.RegisterType(uri => uri?.ToString(), bson => new KeyUseIndexSet(bson.AsString));
		}

		public static void RegisterAccountId() {

			BsonMapper.Global.RegisterType(uri => uri.ToLongRepresentation(), bson => AccountId.FromLongRepresentation((long) bson.RawValue));
		}

		public static void RegisterTransactionId() {
			BsonMapper.Global.RegisterType(uri => uri.ToString(), bson => new TransactionId(bson.AsString));
		}

		public static void RegisterTransactionIdExtended() {
			BsonMapper.Global.RegisterType(uri => uri.ToExtendedString(), bson => new TransactionIdExtended(bson.AsString));
		}

		public static void RegisterTransactionTimestamp() {
			BsonMapper.Global.RegisterType(uri => uri.Value, bson => new TransactionTimestamp((long) bson.RawValue));
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

				o.RawValue[key.ToString()] = BsonMapper.Global.ToDocument(value);
			}

			return o;
		}

		private static Dictionary<Guid, T> DeserializeDictionary<T>(BsonDocument value) {
			var result = new Dictionary<Guid, T>();

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