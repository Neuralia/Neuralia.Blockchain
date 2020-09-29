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

		string ActiveAccount { get; set; }

		int Major { get; set; }
		int Minor { get; set; }
		int Revision { get; set; }
		int NetworkId { get; set; }
		ushort ChainId { get; set; }

		Dictionary<string, IWalletAccount> Accounts { get; set; }

		void InitializeNewDefaultAccount(BlockchainServiceSet serviceSet, Enums.AccountTypes accountType, IChainTypeCreationFactory typeCreationFactory);

		List<IWalletAccount> GetStandardAccounts();

		bool HasAccount { get; }
		IWalletAccount GetActiveAccount();
		bool SetActiveAccountByName(string name);
		bool SetActiveAccount(string accountCode);

		IWalletAccount GetAccount(string accountCode);
		IWalletAccount GetAccountByName(string accountCode);
		
		IWalletAccount GetAccount(AccountId accountId);
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

		public string ActiveAccount { get; set; } = "";

		public int Major { get; set; } = 1;
		public int Minor { get; set; } = 0;
		public int Revision { get; set; } = 0;
		public int NetworkId { get; set; }
		public ushort ChainId { get; set; }

		public Dictionary<string, IWalletAccount> Accounts { get; set; } = new Dictionary<string, IWalletAccount>();

		public virtual void InitializeNewDefaultAccount(BlockchainServiceSet serviceSet, Enums.AccountTypes accountType, IChainTypeCreationFactory typeCreationFactory) {
			this.CreateNewAccount(DEFAULT_ACCOUNT, accountType, serviceSet, typeCreationFactory);
		}

		public bool HasAccount => this.Accounts.Count != 0;
		public IWalletAccount GetActiveAccount() {
			if(!this.HasAccount) {
				throw new ApplicationException("No user account loaded");
			}

			if(this.ActiveAccount == null) {
				throw new ApplicationException("No active user account selected");
			}

			return this.Accounts[this.ActiveAccount];
		}
		
		public bool SetActiveAccount(string accountCode) {

			if(!this.HasAccount) {
				throw new ApplicationException("No user account loaded");
			}

			IWalletAccount activeAccount = this.GetAccount(accountCode);

			if(activeAccount == null) {
				return false;
			}

			//TODO: should this be saved to disk?
			this.ActiveAccount = activeAccount.AccountCode;

			return true;
		}
		
		public bool SetActiveAccountByName(string name) {

			if(!this.HasAccount) {
				throw new ApplicationException("No user account loaded");
			}

			IWalletAccount activeAccount = this.GetAccountByName(name);

			if(activeAccount == null) {
				return false;
			}

			return this.SetActiveAccount(activeAccount.AccountCode);
		}

		public IWalletAccount GetAccountByName(string name) {
			if(!this.HasAccount) {
				return null;
			}

			return this.Accounts.Values.SingleOrDefault(i => i.FriendlyName == name);
		}
		
		public IWalletAccount GetAccount(string accountCode) {
			if(!this.HasAccount) {
				throw new ApplicationException("No user account loaded");
			}

			if(!this.Accounts.ContainsKey(accountCode)) {
				throw new ApplicationException("The account does not exist");
			}

			return this.Accounts[accountCode];
		}

		public IWalletAccount GetAccount(AccountId accountId) {
			if(!this.HasAccount) {
				return null;
			}

			IWalletAccount result = this.Accounts.Values.SingleOrDefault(i => i.PublicAccountId == accountId);

			if(result == null) {
				// try by the hash if its a presentation transaction
				result = this.Accounts.Values.SingleOrDefault(i => i.PresentationId == accountId);
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
			newAccount.InitializeNew(name, accountType, serviceSet);

			this.Accounts.Add(newAccount.AccountCode, newAccount);
		}
	}
}