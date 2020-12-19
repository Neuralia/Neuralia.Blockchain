using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Interfaces.Gates;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Components.Blocks;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.DataAccess.Sqlite;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Data.Arrays;
using Neuralia.Blockchains.Tools.Serialization;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Sqlite.Gates {

	public interface IGatesSqliteDal<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : ISplitSqliteDal<GatesSqliteContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>>, IGatesDal<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>
		where STANDARD_ACCOUNT_GATES : StandardAccountGatesSqlite 
		where JOINT_ACCOUNT_GATES : JointAccountGatesSqlite{
	}

	public abstract class GatesSqliteDal<GATES_CONTEXT, STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES> : SplitSqliteDal<GATES_CONTEXT>, IGatesSqliteDal<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>
		where GATES_CONTEXT : DbContext, IGatesSqliteContext<STANDARD_ACCOUNT_GATES, JOINT_ACCOUNT_GATES>
		where STANDARD_ACCOUNT_GATES : StandardAccountGatesSqlite, new() 
		where JOINT_ACCOUNT_GATES : JointAccountGatesSqlite{

		protected GatesSqliteDal(string folderPath, ServiceSet serviceSet, SoftwareVersion softwareVersion, IChainDalCreationFactory chainDalCreationFactory, AppSettingsBase.SerializationTypes serializationType) : base(folderPath, serviceSet, softwareVersion, chainDalCreationFactory.CreateGatesContext<GATES_CONTEXT>, serializationType) {
		}
		
		public async Task SetKeyGate(AccountId accountId, IdKeyUseIndexSet keyIndexLock) {

			if(keyIndexLock == null) {
				return;
			}
			long accountIdLong = accountId.ToLongRepresentation();

			using var dehydrator = DataSerializationFactory.CreateDehydrator();
			keyIndexLock.KeyUseIndexSet.Dehydrate(dehydrator);
			using var bytes = dehydrator.ToArray();
			byte[] dbBytes = bytes.ToExactByteArrayCopy();
			int ordinal = keyIndexLock.Ordinal;

			if(await this.Any(e => e.AccountId == accountIdLong, db => db.StandardGates).ConfigureAwait(false)) {
				
				await this.UpdateOne(e => e.AccountId == accountIdLong, entry => {

					if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
						entry.TransactionKeyGate = dbBytes;
					}
					else if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
						entry.MessageKeyGate = dbBytes;
					}
					else if(ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
						entry.ChangeKeyGate = dbBytes;
					}
					else if(ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
						entry.SuperKeyGate = dbBytes;
					}
					else if(ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID) {
						entry.ValidatorSignatureKeyGate = dbBytes;
					}
					else if(ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
						entry.ValidatorSecretKeyGate = dbBytes;
					}
					
				}, db=> db.StandardGates).ConfigureAwait(false);

			} else {
				
				var entry = new STANDARD_ACCOUNT_GATES();
				entry.AccountId = accountIdLong;
				
				if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
					entry.TransactionKeyGate = dbBytes;
				}
				else if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
					entry.MessageKeyGate = dbBytes;
				}
				else if(ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
					entry.ChangeKeyGate = dbBytes;
				}
				else if(ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
					entry.SuperKeyGate = dbBytes;
				}
				else if(ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID) {
					entry.ValidatorSignatureKeyGate = dbBytes;
				}
				else if(ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
					entry.ValidatorSecretKeyGate = dbBytes;
				}
				
				await this.InsertEntry(entry, db=> db.StandardGates).ConfigureAwait(false);
			}
		}

		public async Task SetKeyGates(List<(AccountId AccountId, IdKeyUseIndexSet keyGate)> keyGates) {
			
			//TODO: this can be optimized by a single pass update/add per file
			foreach(var gate in keyGates) {
				await SetKeyGate(gate.AccountId, gate.keyGate).ConfigureAwait(false);
			}
		}

		public async Task<KeyUseIndexSet> GetKeyGate(AccountId accountId, byte ordinal) {
			
			long accountIdLong = accountId.ToLongRepresentation();
			var entry = await SelectOne(e => e.AccountId == accountIdLong, db=> db.StandardGates).ConfigureAwait(false);

			byte[] bytes = null;
			if(entry != null) {
				if(ordinal == GlobalsService.TRANSACTION_KEY_ORDINAL_ID) {
					bytes = entry.TransactionKeyGate;
				}
				else if(ordinal == GlobalsService.MESSAGE_KEY_ORDINAL_ID) {
					bytes = entry.MessageKeyGate;
				}
				else if(ordinal == GlobalsService.CHANGE_KEY_ORDINAL_ID) {
					bytes = entry.ChangeKeyGate;
				}
				else if(ordinal == GlobalsService.SUPER_KEY_ORDINAL_ID) {
					bytes = entry.SuperKeyGate;
				}
				else if(ordinal == GlobalsService.VALIDATOR_SIGNATURE_KEY_ORDINAL_ID) {
					bytes = entry.ValidatorSignatureKeyGate;
				}
				else if(ordinal == GlobalsService.VALIDATOR_SECRET_KEY_ORDINAL_ID) {
					bytes = entry.ValidatorSecretKeyGate;
				}

				if(bytes != null) {
					using var rehydrator = DataSerializationFactory.CreateRehydrator(SafeArrayHandle.Wrap(bytes));
					KeyUseIndexSet keyUseIndexSet = new KeyUseIndexSet();
					keyUseIndexSet.Rehydrate(rehydrator);

					return keyUseIndexSet;
				}
			}
			
			return new KeyUseIndexSet();
		}
		
		public Task ClearKeyGates(List<AccountId> accountIds) {
			var accountIdLongs = accountIds.Select(e => e.ToLongRepresentation()).ToList();

			return this.DeleteAll(a=>  accountIdLongs.Contains(a.AccountId), db=> db.StandardGates);
		}
	}
}