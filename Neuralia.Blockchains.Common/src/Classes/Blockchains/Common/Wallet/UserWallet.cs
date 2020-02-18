using System;
using System.Collections.Generic;
using System.Linq;
using LiteDB;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Factories;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Tools;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.General.Types;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet {

	public interface IUserWallet {

		[BsonId]
		Guid Id { get; set; }

		Guid ActiveAccount { get; set; }

		int Major { get; set; }
		int Minor { get; set; }
		int Revision { get; set; }
		int NetworkId { get; set; }
		ushort ChainId { get; set; }

		Dictionary<Guid, IWalletAccount> Accounts { get; set; }

		void InitializeNewDefaultAccount(BlockchainServiceSet serviceSet, IChainTypeCreationFactory typeCreationFactory);

		List<IWalletAccount> GetStandardAccounts();

		IWalletAccount GetActiveAccount();
		bool SetActiveAccount(string name);
		bool SetActiveAccount(Guid uuid);
		IWalletAccount GetAccount(Guid uuid);
		IWalletAccount GetAccount(AccountId accountId);
		IWalletAccount GetAccount(string name);
		List<IWalletAccount> GetAccounts();
	}

	public abstract class UserWallet : IUserWallet {

		public const string DEFAULT_ACCOUNT = "Default";

	#region Guid Dictionary Mapping

		static UserWallet() {
			LiteDBMappers.RegisterGuidDictionary<IWalletAccount>();
			BsonMapper.Global.Entity<IUserWallet>().Id(x => x.Id);
			BsonMapper.Global.Entity<UserWallet>().Id(x => x.Id);
		}

	#endregion

		[BsonId]
		public Guid Id { get; set; } = Guid.NewGuid();

		public Guid ActiveAccount { get; set; } = Guid.Empty;

		public int Major { get; set; } = 1;
		public int Minor { get; set; } = 0;
		public int Revision { get; set; } = 0;
		public int NetworkId { get; set; }
		public ushort ChainId { get; set; }

		public Dictionary<Guid, IWalletAccount> Accounts { get; set; } = new Dictionary<Guid, IWalletAccount>();

		public virtual void InitializeNewDefaultAccount(BlockchainServiceSet serviceSet, IChainTypeCreationFactory typeCreationFactory) {
			this.CreateNewAccount(DEFAULT_ACCOUNT, Enums.AccountTypes.Standard, serviceSet, typeCreationFactory);
		}

		public IWalletAccount GetActiveAccount() {
			if(this.Accounts.Count == 0) {
				throw new ApplicationException("No user account loaded");
			}

			if(this.ActiveAccount == null) {
				throw new ApplicationException("No active user account selected");
			}

			return this.Accounts[this.ActiveAccount];
		}

		public bool SetActiveAccount(string name) {

			if(this.Accounts.Count == 0) {
				throw new ApplicationException("No user account loaded");
			}

			IWalletAccount activeAccount = this.GetAccount(name);

			if(activeAccount == null) {
				return false;
			}

			return this.SetActiveAccount(activeAccount.AccountUuid);
		}

		public bool SetActiveAccount(Guid uuid) {

			if(this.Accounts.Count == 0) {
				throw new ApplicationException("No user account loaded");
			}

			IWalletAccount activeAccount = this.GetAccount(uuid);

			if(activeAccount == null) {
				return false;
			}

			this.ActiveAccount = activeAccount.AccountUuid;

			return true;
		}

		public IWalletAccount GetAccount(Guid accountUuid) {
			if(this.Accounts.Count == 0) {
				throw new ApplicationException("No user account loaded");
			}

			if(!this.Accounts.ContainsKey(accountUuid)) {
				throw new ApplicationException("The account does not exist");
			}

			return this.Accounts[accountUuid];
		}

		public IWalletAccount GetAccount(string name) {
			if(this.Accounts.Count == 0) {
				return null;
			}

			return this.Accounts.Values.SingleOrDefault(i => i.FriendlyName == name);
		}

		public IWalletAccount GetAccount(AccountId accountId) {
			if(this.Accounts.Count == 0) {
				return null;
			}

			IWalletAccount result = this.Accounts.Values.SingleOrDefault(i => i.PublicAccountId == accountId);

			if(result == null) {
				// try by the hash if its a presentation transaction
				result = this.Accounts.Values.SingleOrDefault(i => i.AccountUuidHash == accountId);
			}

			return result;
		}

		public List<IWalletAccount> GetAccounts() {
			return this.Accounts.Values.ToList();
		}

		public List<IWalletAccount> GetStandardAccounts() {
			return this.Accounts.Values.ToList();
		}

		protected virtual void CreateNewAccount(string name, Enums.AccountTypes accountType, BlockchainServiceSet serviceSet, IChainTypeCreationFactory typeCreationFactory) {
			if(this.Accounts.Any(i => i.Value.FriendlyName == name)) {
				throw new ApplicationException($"Account {name} already exists");
			}

			IWalletAccount newAccount = typeCreationFactory.CreateNewWalletAccount();
			newAccount.InitializeNew(name, serviceSet, accountType);

			this.Accounts.Add(newAccount.AccountUuid, newAccount);
		}
	}
}