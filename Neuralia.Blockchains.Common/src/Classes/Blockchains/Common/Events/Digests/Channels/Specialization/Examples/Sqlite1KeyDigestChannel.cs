//using System;
//using System.Collections.Generic;
//
//using Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Index;
//using Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Specialization.Cards;
//
//namespace Neuralia.Blockchains.Common.Classes.Neuralia.Blockchains.Common.Events.Digests.Channels.Specialization {
//
//	public interface ISqlite1KeyDigestChannel : IDigestChannel {
//		
//	}
//	public abstract class Sqlite1KeyDigestChannel<ACCREDITATION_CARD>: DigestChannel<Sqlite1KeyDigestChannel.Sqlite1KeyDigestChannelBands, ACCREDITATION_CARD, ulong, ulong, uint>,ISqlite1KeyDigestChannel
//		where ACCREDITATION_CARD : class, IAccountSnapshotDigestChannelCard, new(){
//
//		protected const string ACCOUNTS_CHANNEL = "accounts";
//		protected const string ACCOUNTS_BAND_NAME = "accounts";
//		
//		protected readonly int groupSize;
//		
//		public enum FileTypes:int {
//			Accounts = 1 
//		}
//		
//		public Sqlite1KeyDigestChannel(int groupSize, string folder): base(folder, ACCOUNTS_CHANNEL)  {
//			this.groupSize = groupSize;
//		}
//		public override DigestChannelTypes ChannelType => DigestChannelTypes.AccountSnapshot;
//
//		protected override void BuildBandsIndices() {
//
//			this.channelBandIndexSet.AddIndex(1, new GroupSplitSqliteChannelBandIndex<Sqlite1KeyDigestChannel.Sqlite1KeyDigestChannelBands, ACCREDITATION_CARD>(ACCOUNTS_BAND_NAME, this.baseFolder, this.scopeFolder, this.groupSize, Sqlite1KeyDigestChannel.Sqlite1KeyDigestChannelBands.Accounts, FileSystemWrapper.CreatePhysical()));
//		}
//
//		protected override void SetIdentity() {
//			this.Type = DigestChannelTypes.AccountSnapshot;
//			this.Major = 1;
//			this.Minor = 1;
//		}
//		
//		
//	}
//	
//	public static class Sqlite1KeyDigestChannel {
//		[Flags]
//		public enum Sqlite1KeyDigestChannelBands:int { Accounts=1 }
//	}
//}

