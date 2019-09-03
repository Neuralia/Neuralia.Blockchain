//using System;
//using System.Collections.Generic;
//using System.IO.Abstractions;
//using Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Index;
//using Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
//using Neuralia.Blockchains.Tools.Data;
//
//namespace Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Specialization {
//	public interface ISqlite2KeysDigestChannel : IDigestChannel {
//		IByteArray GetKey(long accountSequenceId, Enums.AccountTypes accountType, byte ordinal);
//	}
//	
//	public abstract class Sqlite2KeysDigestChannel<ACCREDITATION_CARD>: DigestChannel<Sqlite2KeysDigestChannel.Sqlite2KeysDigestChannelBands, ACCREDITATION_CARD, ulong, (ulong id, byte ordinal), (uint id, byte ordinal)>, ISqlite2KeysDigestChannel
//		where ACCREDITATION_CARD : class, IAccountKeysDigestChannelCard, new() {
//
//		public enum FileTypes:int {
//			Keys = 1 
//		}
//		protected const string KEYS_CHANNEL = "keys";
//		protected const string KEYS_BAND_NAME = "keys";
//		protected readonly int groupSize;
//		public Sqlite2KeysDigestChannel(int groupSize, string folder): base(folder, KEYS_CHANNEL)  {
//			this.groupSize = groupSize;
//		}
//
//		protected override void BuildBandsIndices() {
//
//			this.channelBandIndexSet.AddIndex(1, new GroupSplitSqliteMultiKeyChannelBandIndex<Sqlite2KeysDigestChannel.Sqlite2KeysDigestChannelBands, ACCREDITATION_CARD, byte>(KEYS_BAND_NAME, this.baseFolder, this.scopeFolder, this.groupSize, Sqlite2KeysDigestChannel.Sqlite2KeysDigestChannelBands.Keys, new FileSystem(), entry => new {entry.Scope, entry.Ordinal}, entry => new object[]{entry.id, entry.ordinal}));
//		}
//
//		public override DigestChannelTypes ChannelType => DigestChannelTypes.AccountKeys;
//
//		protected override void SetIdentity() {
//			this.Type = DigestChannelTypes.AccountKeys;
//			this.Major = 1;
//			this.Minor = 1;
//		}
//
//		public IByteArray GetKey(long accountSequenceId, Enums.AccountTypes accountType, byte ordinal) {
//
////			//entry => new object[]{entry.id, entry.ordinal}
////			var f = this.channelBandIndexSet.QueryCard((accountId, ordinal));
////
////			return (ByteArray)f.Key;
//			throw new NotImplementedException();
//		}
//		
//
//	}
//	
//	public static class Sqlite2KeysDigestChannel {
//		[Flags]
//		public enum Sqlite2KeysDigestChannelBands:int { Keys=1 }
//	}
//}

