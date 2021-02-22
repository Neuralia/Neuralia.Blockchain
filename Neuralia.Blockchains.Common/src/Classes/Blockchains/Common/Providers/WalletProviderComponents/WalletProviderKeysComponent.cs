using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Dal.Wallet.Extra;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.DataStructures.Types;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Events.Transactions.Tags;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Account;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Wallet.Keys;
using Neuralia.Blockchains.Common.Classes.Blockchains.Common.Workflows.Creation.Transactions;
using Neuralia.Blockchains.Common.Classes.Configuration;
using Neuralia.Blockchains.Components.Transactions.Identifiers;
using Neuralia.Blockchains.Core;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Cryptography;
using Neuralia.Blockchains.Core.Cryptography.Encryption;
using Neuralia.Blockchains.Core.Cryptography.Encryption.Asymetrical;
using Neuralia.Blockchains.Core.Cryptography.Keys;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.NTRUPrime;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.Providers;
using Neuralia.Blockchains.Core.Cryptography.PostQuantum.XMSS.XMSS;
using Neuralia.Blockchains.Core.Cryptography.Trees;
using Neuralia.Blockchains.Core.Cryptography.Utils;
using Neuralia.Blockchains.Core.Exceptions;
using Neuralia.Blockchains.Core.Extensions;
using Neuralia.Blockchains.Core.General.Types;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Tools;
using Neuralia.Blockchains.Tools.Cryptography;
using Neuralia.Blockchains.Tools.Data;
using Neuralia.Blockchains.Tools.Locking;
using Neuralia.Blockchains.Tools.Serialization;
using Org.BouncyCastle.Security;

namespace Neuralia.Blockchains.Common.Classes.Blockchains.Common.Providers.WalletProviderComponents {


	public interface IWalletProviderKeysComponentUtility {
		void HashKey(IWalletKey key);

		IWalletKey CreateBasicKey(string name, CryptographicKeyType keyType);

		T CreateBasicKey<T>(string name, CryptographicKeyType keyType)
			where T : IWalletKey;

		Task GenerateXmssKeyIndexNodeCache(string accountCode, byte ordinal, long index, LockContext lockContext = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, int hashBits, WalletProvider.HashTypes HashType, int backupHashBits, WalletProvider.HashTypes backupHashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, Enums.KeyHashType hashbits, Enums.KeyHashType backupHashbits, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<IXmssMTWalletKey> CreateXmssmtKey(string name, byte treeHeight, byte treeLayers, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null);
		Task<INTRUPrimeWalletKey> CreateNTRUPrimeKey(string name, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strength);
		Task<SafeArrayHandle> CreateNTRUPrimeAppointmentRequestKey(LockContext lockContext);
	}

	public interface IWalletProviderKeysComponentReadonly {
		Task<IdKeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext);
	}

	public interface IWalletProviderKeysComponentWrite {

		Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext);
		
		Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, string accountCode, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);

		
		Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, string accountCode, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false);


		Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssmtCryptographicSignature(IXmssMTWalletKey xmssMTWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssCryptographicSignature(IXmssWalletKey keyxmssWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Action<XMSSProvider> callback = null, Func<int, int ,int, Task> progressCallback = null);
		
		Task<SafeArrayHandle> PerformCryptographicSignature(string accountCode, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
		Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false);
	}

	public interface IWalletProviderKeysComponent :  IWalletProviderKeysComponentReadonly, IWalletProviderKeysComponentWrite, IWalletProviderKeysComponentUtility {

	}

	public abstract class WalletProviderKeysComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER> : WalletProviderComponent<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER, WALLET_PROVIDER>, IWalletProviderComponent, IWalletProviderKeysComponent
		where CENTRAL_COORDINATOR : ICentralCoordinator<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER>
		where CHAIN_COMPONENT_PROVIDER : IChainComponentProvider<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> 
		where WALLET_PROVIDER : IWalletProviderInternal{

		public bool IsXmssBoosted { get; set; }

		public Enums.ThreadMode XmssThreadMode {
			get {
				var mode = GlobalSettings.ApplicationSettings.XmssThreadMode;

				if(this.IsXmssBoosted && (mode == Enums.ThreadMode.Half || mode == Enums.ThreadMode.ThreeQuarter)) {
					mode = (Enums.ThreadMode)(((int)mode)+1);
				}

				return mode;
			}
		}
		
		public IWalletKey CreateBasicKey(string name, CryptographicKeyType keyType) {
			IWalletKey key = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.ChainTypeCreationFactoryBase.CreateNewWalletKey(keyType);

			key.Id = Guid.NewGuid();
			key.Name = name;
			key.CreatedTime = DateTimeEx.CurrentTime.Ticks;

			return key;

		}

		public T CreateBasicKey<T>(string name, CryptographicKeyType keyType)
			where T : IWalletKey {

			T key = (T) this.CreateBasicKey(name, keyType);

			return key;
		}

		public virtual async Task GenerateXmssKeyIndexNodeCache(string accountCode, byte ordinal, long index, LockContext lockContext = null) {

			using(IXmssWalletKey key = await this.MainWalletProvider.LoadKey<IXmssWalletKey>(k => {
				return k;
			}, accountCode, ordinal, lockContext).ConfigureAwait(false)) {

				if(key.Index.KeyUseIndex != index) {
					// key is out of date, so let's just forget it
					return;
				}

				this.CentralCoordinator.Log.Information($"generating xmss key index node cache for index {index} and tree height {key.TreeHeight}");
				using(XMSSProvider provider = new XMSSProvider(key.HashType, key.BackupHashType, key.TreeHeight, this.XmssThreadMode, key.NoncesExponent)) {

					provider.Initialize();

					key.NextKeyNodeCache.Entry = (await provider.GenerateIndexNodeCache(key.PrivateKey, (layer, height, pct) => {

							                             if(pct > 0) {
								                             this.CentralCoordinator.Log.Verbose($"node cache generation progress: {pct}% - layer {layer} of {height}");
							                             }

							                             return Task.CompletedTask;
							                         
						                             }).ConfigureAwait(false)).Entry;
				}

				await this.MainWalletProvider.UpdateKey(key, lockContext).ConfigureAwait(false);
				
				this.CentralCoordinator.Log.Verbose($"xmss key index node cache generation completed and key updated.");
			}
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, Func<int, Task> progressCallback = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			byte treeHeight = WalletProvider.MINIMAL_XMSS_KEY_HEIGHT;
			float xmssKeyWarningLevel = 0.7F;
			float xmssKeyChangeLevel = 0.9F;
			int keyHashType = 0;
			int? seedSize = null;

			byte noncesExponent = 4;
			WalletProvider.HashTypes hashType = WalletProvider.HashTypes.Sha2;
			
			int keyBackupHashType = 0;
			WalletProvider.HashTypes backupHashType = WalletProvider.HashTypes.Sha3;

			WalletProvider.HashTypes GetHashType(ChainConfigurations.HashTypes source) {
				switch(source) {
					case ChainConfigurations.HashTypes.Sha2:
						return WalletProvider.HashTypes.Sha2;
					case ChainConfigurations.HashTypes.Sha3:
						return WalletProvider.HashTypes.Sha3;
				}

				return WalletProvider.HashTypes.Sha3;
			}

			if(name == GlobalsService.TRANSACTION_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.TransactionXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.TransactionXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.TransactionXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.TransactionXmssKeyHashType);
				
				keyHashType = WalletProvider.TRANSACTION_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.TransactionXmssKeyBackupHashType); 
				keyBackupHashType = WalletProvider.TRANSACTION_KEY_HASH_BITS;
				seedSize = chainConfiguration.TransactionXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.TransactionXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.TRANSACTION_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.MESSAGE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.MessageXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.MessageXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.MessageXmssKeyChangeLevel;
				hashType = GetHashType(chainConfiguration.MessageXmssKeyHashType);
				keyHashType = WalletProvider.MESSAGE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.MessageXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.MESSAGE_KEY_HASH_BITS;
				seedSize = chainConfiguration.MessageXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.MessageXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.MESSAGE_KEY_NONCES_EXPONENT;
			}

			if(name == GlobalsService.CHANGE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.ChangeXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ChangeXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ChangeXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.ChangeXmssKeyHashType);
				keyHashType = WalletProvider.CHANGE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.ChangeXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.CHANGE_KEY_HASH_BITS;
				seedSize = chainConfiguration.ChangeXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.ChangeXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.CHANGE_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.SUPER_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.SuperXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.SuperXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.SuperXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.SuperXmssKeyHashType);
				keyHashType = WalletProvider.SUPER_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.SuperXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.SUPER_KEY_HASH_BITS;
				seedSize = chainConfiguration.SuperXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.SuperXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.SUPER_KEY_NONCES_EXPONENT;
			}
			
			if(name == GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME) {
				treeHeight = Math.Max(chainConfiguration.ValidatorSignatureXmssKeyHeight, WalletProvider.MINIMAL_XMSS_KEY_HEIGHT);
				xmssKeyWarningLevel = chainConfiguration.ValidatorSignatureXmssKeyWarningLevel;
				xmssKeyChangeLevel = chainConfiguration.ValidatorSignatureXmssKeyChangeLevel;

				hashType = GetHashType(chainConfiguration.ValidatorSignatureXmssKeyHashType);
				keyHashType = WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS;
				
				backupHashType = GetHashType(chainConfiguration.ValidatorSignatureXmssKeyBackupHashType);
				keyBackupHashType = WalletProvider.VALIDATOR_SIGNATURE_KEY_HASH_BITS;
				seedSize = chainConfiguration.ValidatorSignatureXmssKeySeedSize.HasValue?Math.Max(chainConfiguration.ValidatorSignatureXmssKeySeedSize.Value, WalletProvider.MINIMAL_KEY_SEED_SIZE):(int?)null;
				
				noncesExponent = WalletProvider.VALIDATOR_SIGNATURE_NONCES_EXPONENT;
			}

			DateTime start = DateTime.Now;
			int lastTenth = -1;
			
			return this.CreateXmssKey(name, treeHeight, keyHashType, hashType, keyBackupHashType, backupHashType, xmssKeyWarningLevel, xmssKeyChangeLevel, async percentage => {
				TimeSpan remaining = TimeSpan.Zero;
				var passed = DateTime.Now - start;
				
				if(percentage > 0) {
					var total = TimeSpan.FromSeconds(100 * passed.TotalSeconds / percentage);
					remaining = total - passed;
				}

				int tenth = percentage / 10;

				string message = $"Generation {percentage}% completed for key {name}.  {passed:hh\\:mm\\:ss} elapsed and {remaining:hh\\:mm\\:ss} remaining";

				if(lastTenth != tenth) {
					lastTenth = tenth;
					this.CentralCoordinator.Log.Information(message);
				}
				else{
					this.CentralCoordinator.Log.Verbose(message);
				}
				
				if(progressCallback != null) {
					await progressCallback(percentage).ConfigureAwait(false);
				}

			}, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}

		public Task<IXmssWalletKey> CreateXmssKey(string name, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			return this.CreateXmssKey(name, XMSSProvider.DEFAULT_XMSS_TREE_HEIGHT, WalletProvider.DEFAULT_KEY_HASH_BITS, WalletProvider.HashTypes.Sha2, WalletProvider.DEFAULT_KEY_BACKUP_HASH_BITS, WalletProvider.HashTypes.Sha3, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		
		public Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, int hashBits, WalletProvider.HashTypes hashType, int backupHashBits, WalletProvider.HashTypes backupHashType, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, CryptographicKeyTypes.Instance.XMSS);

			Enums.KeyHashType fullHashbits = WalletProvider.ConvertFullHashType(hashBits, hashType);
			Enums.KeyHashType fullBackupHashbits = WalletProvider.ConvertFullHashType(backupHashBits, backupHashType);
			
			return this.CreateXmssKey(name, treeHeight, fullHashbits, fullBackupHashbits,  warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		
		public async Task<IXmssWalletKey> CreateXmssKey(string name, byte treeHeight, Enums.KeyHashType hashbits, Enums.KeyHashType backupHashbits, float warningLevel, float changeLevel, Func<int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSProvider> prepare = null) {
			IXmssWalletKey key = this.CreateBasicKey<IXmssWalletKey>(name, CryptographicKeyTypes.Instance.XMSS);
			
			using(XMSSProvider provider = new XMSSProvider(hashbits, backupHashbits, treeHeight, this.XmssThreadMode, noncesExponent)) {

				if(enableCache.HasValue) {
					provider.EnableCache = enableCache.Value;
				}

				provider.Initialize();

				if(prepare != null) {
					prepare(provider);
				}
				this.CentralCoordinator.Log.Information($"Creating a new XMSS key named '{name}' with tree height {treeHeight} and hashType {provider.HashType} and good for {provider.MaximumHeight} signatures.");

				(SafeArrayHandle privateKey, SafeArrayHandle publicKey) = await provider.GenerateKeys(seedSize, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);

				key.HashType = provider.HashTypeEnum;
				key.BackupHashType = provider.BackupHashTypeEnum;
				key.TreeHeight = provider.TreeHeight;
				key.NoncesExponent = provider.NoncesExponent;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;

				key.PrivateKey.Entry = privateKey.Entry;
				key.PublicKey.Entry = publicKey.Entry;
				
				// now set right away the next index cache
				key.NextKeyNodeCache.Entry = (await provider.GenerateIndexNodeCache(privateKey, (layer, height, pct) => {
							
                         if(pct > 0) {
                             this.CentralCoordinator.Log.Verbose($"node cache generation progress: {pct}% - layer {layer} of {height}");
                         }                   
                         return Task.CompletedTask;
                         
                     }).ConfigureAwait(false)).Entry;

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			this.CentralCoordinator.Log.Information($"XMSS Key '{name}' created");

			return key;
		}
		
		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {

			float warningLevel = 0;
			float changeLevel = 0;
			DateTime start = DateTime.Now;
			return this.CreateXmssmtKey(name, warningLevel, changeLevel, async (treePct, layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers) => {
				
				TimeSpan remaining = TimeSpan.Zero;
				var passed = DateTime.Now - start;
				
				if(totalPct > 0) {
					var total = TimeSpan.FromSeconds((100 * passed.TotalSeconds / totalPct));
					remaining = total - passed;
				}
				
				Console.WriteLine($"key name: {name}, current tree {treePct}%, tree: {tree} of {layerTrees} trees in layer and out of {totalTrees} total trees, layer: {layer} of {totalLayers} layers: {layerPct}% in layer, {totalPct}% remaining. {passed:hh\\:mm\\:ss} elapsed and {remaining:hh\\:mm\\:ss} remaining");
				
				if(progressCallback != null) {
					await progressCallback(treePct, layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers).ConfigureAwait(false);
				}
			}, seedSize, cacheMode, cacheLevels, noncesExponent,enableCache, prepare);
		}

		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="warningLevel"></param>
		/// <param name="changeLevel"></param>
		/// <param name="progressCallback">layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers</param>
		/// <param name="seedSize"></param>
		/// <param name="cacheMode"></param>
		/// <param name="cacheLevels"></param>
		/// <param name="noncesExponent"></param>
		/// <returns></returns>
		public Task<IXmssMTWalletKey> CreateXmssmtKey(string name, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			return this.CreateXmssmtKey(name, XMSSMTProvider.DEFAULT_XMSSMT_TREE_HEIGHT, XMSSMTProvider.DEFAULT_XMSSMT_TREE_LAYERS, XMSSProvider.DEFAULT_HASH_BITS, XMSSProvider.DEFAULT_BACKUP_HASH_BITS, warningLevel, changeLevel, progressCallback, seedSize, cacheMode, cacheLevels, noncesExponent, enableCache, prepare);
		}
		/// <summary>
		/// 
		/// </summary>
		/// <param name="name"></param>
		/// <param name="treeHeight"></param>
		/// <param name="treeLayers"></param>
		/// <param name="hashType"></param>
		/// <param name="backupHashType"></param>
		/// <param name="warningLevel"></param>
		/// <param name="changeLevel"></param>
		/// <param name="progressCallback">layerPct, totalPct, tree, layerTrees, totalTrees, layer, totalLayers</param>
		/// <param name="seedSize"></param>
		/// <param name="cacheMode"></param>
		/// <param name="cacheLevels"></param>
		/// <param name="noncesExponent"></param>
		/// <returns></returns>

		public async Task<IXmssMTWalletKey> CreateXmssmtKey(string name, byte treeHeight, byte treeLayers, Enums.KeyHashType hashType, Enums.KeyHashType backupHashType, float warningLevel, float changeLevel, Func<int, int, int, long, long, long, int, int, Task> progressCallback = null, int? seedSize = null, XMSSNodeCache.XMSSCacheModes cacheMode = XMSSNodeCache.XMSSCacheModes.Automatic, byte cacheLevels = XMSSNodeCache.LEVELS_TO_CACHE_ABSOLUTELY, byte noncesExponent = XMSSEngine.DEFAULT_NONCES_EXPONENT, bool? enableCache = null, Action<XMSSMTProvider> prepare = null) {
			IXmssMTWalletKey key = this.CreateBasicKey<IXmssMTWalletKey>(name, CryptographicKeyTypes.Instance.XMSSMT);

			using(XMSSMTProvider provider = new XMSSMTProvider(hashType, backupHashType, treeHeight, treeLayers, this.XmssThreadMode, noncesExponent)) {
				
				if(enableCache.HasValue) {
					provider.EnableCache = enableCache.Value;
				}
				
				provider.Initialize();

				if(prepare != null) {
					prepare(provider);
				}
				
				this.CentralCoordinator.Log.Information($"Creating a new XMSS^MT key named '{name}' with tree height {treeHeight}, tree layers {treeLayers} and hashType {provider.HashType} and good for {provider.MaximumHeight} signatures.");

				(SafeArrayHandle privateKey, SafeArrayHandle publicKey) = await provider.GenerateKeys(seedSize, true, progressCallback, cacheMode, cacheLevels).ConfigureAwait(false);

				key.HashType = provider.HashTypeEnum;
				key.BackupHashType = provider.BackupHashTypeEnum;
				key.TreeHeight = provider.TreeHeight;
				key.TreeLayers = provider.TreeLayers;
				key.NoncesExponent = provider.NoncesExponent;
				key.WarningHeight = provider.GetKeyUseThreshold(warningLevel);
				key.ChangeHeight = provider.GetKeyUseThreshold(changeLevel);
				key.MaximumHeight = provider.MaximumHeight;

				key.PrivateKey.Entry = privateKey.Entry;
				key.PublicKey.Entry = publicKey.Entry;

				privateKey.Return();
				publicKey.Return();
			}

			this.HashKey(key);

			this.CentralCoordinator.Log.Information("XMSS^MT Key created");

			return key;
		}

		public async Task<SafeArrayHandle> CreateNTRUPrimeAppointmentRequestKey(LockContext lockContext) {
			
			var account = await this.MainWalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);

			using NTRUPrimeEncryptor ntruDecryptor = new NTRUPrimeEncryptor(NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761);
			var keyPair = ntruDecryptor.GenerateKeyPair();
			
			// set the private key in the wallet
			account.AccountAppointment.AppointmentPrivateKey = keyPair.privateKey;

			return keyPair.publicKey;
		}
		
		public async Task<INTRUPrimeWalletKey> CreateNTRUPrimeKey(string name, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes strength) {
			INTRUPrimeWalletKey key = this.CreateBasicKey<INTRUPrimeWalletKey>(name, CryptographicKeyTypes.Instance.NTRUPrime);

			using NTRUPrimeEncryptor ntruPrimeEncryptor = new NTRUPrimeEncryptor(strength);
			
			this.CentralCoordinator.Log.Information($"Creating a new NTRU Prime key named '{name} of strength {strength}'");

			(SafeArrayHandle publicKey, SafeArrayHandle privateKey) = await Task.Run(() => {
				return ntruPrimeEncryptor.GenerateKeyPair();
			}).ConfigureAwait(false);

			key.PrivateKey.Entry = privateKey.Entry;
			key.PublicKey.Entry = publicKey.Entry;
			

			key.Strength = strength;
			
			this.HashKey(key);

			this.CentralCoordinator.Log.Information("NTRU Prime Key created");

			return key;
		}
		
		public Task<INTRUPrimeWalletKey> CreateValidatorSecretKey() {

			BlockChainConfigurations chainConfiguration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;
			
			return this.CreateNTRUPrimeKey(GlobalsService.VALIDATOR_SECRET_KEY_NAME, NTRUPrimeUtils.NTRUPrimeKeyStrengthTypes.SIZE_761);		
		}
		
		public void HashKey(IWalletKey key) {

			// lets generate the hash of this key. this hash can be used as a unique key in public uses. Still, data must be encrypted!

			using HashNodeList nodeList = new HashNodeList();

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			nodeList.Add(key.GetStructuresArray());

			// lets add three random nonces
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());
			nodeList.Add(GlobalRandom.GetNextLong());

			key.Hash = HashingUtils.HashxxTree(nodeList);

		}
		
		public virtual async Task<bool> UpdateWalletKeyLog(IAccountFileInfo accountFile, IWalletAccount account, SynthesizedBlock synthesizedBlock, LockContext lockContext) {

			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.KeyLogMode == AppSettingsBase.KeyLogModes.Disabled) {
				return false;
			}
			
			bool changed = false;

			WalletAccountChainState chainState = await accountFile.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false);

			if(((WalletAccountChainState.BlockSyncStatuses) chainState.BlockSyncStatus).HasFlag(WalletAccountChainState.BlockSyncStatuses.KeyLogSynced) == false){

				this.CentralCoordinator.Log.Verbose($"Update Wallet Key Logs for block {synthesizedBlock.BlockId} and account code {account.AccountCode}...");

				AccountId accountId = account.GetAccountId();
				
				if(synthesizedBlock.AccountScoped.ContainsKey(accountId)) {
					SynthesizedBlock.SynthesizedBlockAccountSet scopedSynthesizedBlock = synthesizedBlock.AccountScoped[accountId];
				
					foreach(KeyValuePair<TransactionId, ITransaction> transactionId in scopedSynthesizedBlock.ConfirmedLocalTransactions) {
				
						ITransaction transaction = transactionId.Value;
				
						if(transaction.Version.Type == TransactionTypes.Instance.STANDARD_PRESENTATION) {
							// the presentation trnasaction is a special case, which we never sign with a key in our wallet, so we just ignore it
							continue;
						}
				
						IdKeyUseIndexSet idKeyUseIndexSet = transaction.TransactionMeta.KeyUseIndex;
				
						if(transaction is IJointTransaction jointTransaction) {
							// ok, we need to check if we are not the main sender but still a cosinger
				
						}
						ChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();
						
						if(!await accountFile.WalletKeyLogsInfo.ConfirmKeyLogTransactionEntry(transaction.TransactionId, transaction.TransactionMeta.KeyUseIndex, synthesizedBlock.BlockId, lockContext).ConfigureAwait(false)) {
							// ok, this transction was not in our key log. this means we might have a bad wallet. this is very serious adn we alert the user
							//TODO: what to do with this?
							
							string message = $"Block {synthesizedBlock.BlockId} has our transaction {transaction.TransactionId} which belongs to us but is NOT in our keylog. We might have an old wallet.";
				
							this.ThrowStrictModeReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG, message, new string[] {synthesizedBlock.BlockId.ToString(), transaction.TransactionId.ToString()});

							using IXmssWalletKey key = await this.MainWalletProvider.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.TRANSACTION_KEY_NAME, lockContext).ConfigureAwait(false);
				
							if(key.KeyAddress.KeyUseIndex < transaction.TransactionMeta.KeyUseIndex) {
								this.ThrowStrictModeReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_NOT_IN_KEYLOG, message, new string[] {synthesizedBlock.BlockId.ToString(), transaction.TransactionId.ToString()});
								
								if(IdKeyUseIndexSet.CanBeForwarded(key.KeyAddress.KeyUseIndex, transaction.TransactionMeta.KeyUseIndex)){
								
									await this.UpdateKeyIndex(key, transaction.TransactionMeta.KeyUseIndex, lockContext).ConfigureAwait(false);
								}  
							}
						}
					}
				}
				
				(await accountFile.WalletChainStatesInfo.ChainState(lockContext).ConfigureAwait(false)).BlockSyncStatus |= (int) WalletAccountChainState.BlockSyncStatuses.KeyLogSynced;
				changed = true;
			}

			return changed;
		}
		
		/// <summary>
		/// for the index of the key to be updated to this index value
		/// </summary>
		/// <param name="key"></param>
		/// <param name="index"></param>
		/// <param name="lockContext"></param>
		/// <returns></returns>
		private async Task UpdateKeyIndex(IWalletKey key, KeyUseIndexSet index, LockContext lockContext, bool saveCache = true) {

			if(!KeyUseIndexSet.IsSameSequenceId(key.Index, index)) {
				throw new ApplicationException("The key has a different sequence Id. the index can not be fast forwarded.");
			}
			
			if(!KeyUseIndexSet.CanBeForwarded(key.Index, index)) {
				return;
			}

			if(key is IXmssWalletKey xmssWalletKey) {
				using(XMSSProvider provider = new XMSSProvider(xmssWalletKey.HashType, xmssWalletKey.BackupHashType, xmssWalletKey.TreeHeight, this.XmssThreadMode, xmssWalletKey.NoncesExponent)) {

					provider.Initialize();

					KeyUseIndexSet.Forward(key.Index, index);
					key.PrivateKey.Entry = provider.SetPrivateKeyIndex(index.KeyUseIndex, key.PrivateKey).Entry;
				}
			}

			await MainWalletProvider.UpdateKey(key, lockContext).ConfigureAwait(true);
					
			await this.UpdateLocalChainStateKeyHeight(key, lockContext).ConfigureAwait(false);

			if(saveCache) {
				await this.SaveAccountKeyIndiciesCaches(key.AccountCode, lockContext).ConfigureAwait(true);
			}
		}

		private class WalletKeyIndexCacheEntry {
			public WalletKeyIndexCache localWalletKeyIndexCache;
			public WalletKeyIndexCache globalWalletKeyIndexCache;
			public SafeArrayHandle password;
			public SafeArrayHandle salt;

			public bool changed;
		}
		
		Dictionary<string, WalletKeyIndexCacheEntry> walletKeyIndexCaches = new Dictionary<string, WalletKeyIndexCacheEntry>();
		
		public virtual string GetLocalWalletKeyIndexCachePath() {
			return this.MainWalletProvider.GetWalletKeysCachePath();
		}
		
		public virtual string GetLocalWalletKeyIndexCacheFileName(Guid filename) {
			return Path.Combine(this.GetLocalWalletKeyIndexCachePath(), filename.ToString());
		}
		
		public virtual async Task CompareAccountKeyIndiciesCaches(string accountCode, LockContext lockContext) {
			this.MainWalletProvider.EnsureWalletIsLoaded();

			var configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(configuration.KeyLogMode == AppSettingsBase.KeyLogModes.Disabled) {
				return;
			}

			var account = await this.MainWalletProvider.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			WalletKeyIndexCacheEntry cacheEntry = null;
			if(!this.walletKeyIndexCaches.ContainsKey(accountCode)) {

				cacheEntry = new WalletKeyIndexCacheEntry();

				WalletKeyLogFileInfo keyLogFileInfo = this.MainWalletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;
				
				WalletAccountKeyLogMetadata keyLogMetadata = await keyLogFileInfo.GetKeyLogMetadata(lockContext).ConfigureAwait(false);

				if(keyLogMetadata == null) {
					keyLogMetadata = this.MainWalletProvider.CreateNewWalletAccountKeyLogMetadata(lockContext);
					keyLogMetadata.Id = 1;
					keyLogMetadata.KeyIndexEncryptionFileName = Guid.NewGuid();
					keyLogMetadata.KeyIndexEncryptionKey = CryptoUtil.GenerateCodeBuffer();
					
					await keyLogFileInfo.UpdateKeyLogMetadata(keyLogMetadata, lockContext).ConfigureAwait(false);
				}

				(cacheEntry.password, cacheEntry.salt) = CryptoUtil.GeneratePasswordSalt(keyLogMetadata.KeyIndexEncryptionKey);
				
				// now load the cache entries
				
				cacheEntry.localWalletKeyIndexCache = new WalletKeyIndexCache();
				cacheEntry.globalWalletKeyIndexCache = new WalletKeyIndexCache();
				
				// first, the local wallet one
				string localWalletCachePath = this.GetLocalWalletKeyIndexCacheFileName(keyLogMetadata.KeyIndexEncryptionFileName);
				
				FileExtensions.EnsureDirectoryStructure(this.GetLocalWalletKeyIndexCachePath(), this.CentralCoordinator.FileSystem);
				
				if(this.CentralCoordinator.FileSystem.FileExists(localWalletCachePath)) {
					using var encryptedBytes = await FileExtensions.ReadAllBytesFastAsync(localWalletCachePath, this.CentralCoordinator.FileSystem).ConfigureAwait(false);
					using var decryptedBytes = CryptoUtil.Decrypt(encryptedBytes, cacheEntry.password, cacheEntry.salt);
					using var rehydrator = DataSerializationFactory.CreateRehydrator(decryptedBytes);
					
					cacheEntry.localWalletKeyIndexCache.Rehydrate(rehydrator);
				}
				
				// second the global security copy
				using var encryptedBytes2 = await this.CentralCoordinator.ChainComponentProvider.ChainDataLoadProviderBase.LoadGlobalWalletKeyIndexCacheFile(keyLogMetadata.KeyIndexEncryptionFileName).ConfigureAwait(false);
				if(encryptedBytes2 != null && !encryptedBytes2.IsZero) {
					using var decryptedBytes = CryptoUtil.Decrypt(encryptedBytes2, cacheEntry.password, cacheEntry.salt);
					using var rehydrator = DataSerializationFactory.CreateRehydrator(decryptedBytes);

					cacheEntry.globalWalletKeyIndexCache.Rehydrate(rehydrator);
				}

				this.walletKeyIndexCaches.Add(account.AccountCode, cacheEntry);
			}

			cacheEntry = this.walletKeyIndexCaches[accountCode];
			
			// ok, do the compare
			
			WalletChainStateFileInfo walletChainStateInfo = this.MainWalletProvider.WalletFileInfo.Accounts[accountCode].WalletChainStatesInfo;
			
			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			foreach(var key in account.Keys) {
				
				// chainstate entry
				IWalletAccountChainStateKey keyChainState = chainState.Keys[key.Ordinal];

				WalletKeyIndexCache.WalletKeyIndexCacheEntry cacheConsensus = null;
				WalletKeyIndexCache.WalletKeyIndexCacheEntry localKeyCache = null;
				WalletKeyIndexCache.WalletKeyIndexCacheEntry globalKeyCache = null;
				
				if(cacheEntry.localWalletKeyIndexCache.keys.ContainsKey(key.Ordinal)) {
					localKeyCache = cacheEntry.localWalletKeyIndexCache.keys[key.Ordinal];
				}
				
				if(cacheEntry.globalWalletKeyIndexCache.keys.ContainsKey(key.Ordinal)) {
					globalKeyCache = cacheEntry.globalWalletKeyIndexCache.keys[key.Ordinal];
				}

				if(localKeyCache != null && globalKeyCache != null) {
					// get the best of both
					cacheConsensus = new WalletKeyIndexCache.WalletKeyIndexCacheEntry();

					if(localKeyCache.KeyIndex == globalKeyCache.KeyIndex || localKeyCache.KeyIndex >= globalKeyCache.KeyIndex) {
						cacheConsensus.KeyIndex = localKeyCache.KeyIndex.Clone2();
					}
					else{
						cacheConsensus.KeyIndex = globalKeyCache.KeyIndex.Clone2();
					}
				}
				else if(localKeyCache != null) {
					cacheConsensus = localKeyCache;
				}else if(globalKeyCache != null) {
					cacheConsensus = globalKeyCache;
				}

				if(cacheConsensus != null) {
					if(cacheConsensus.KeyIndex != keyChainState.LocalIdKeyUse) {

						if(cacheConsensus.KeyIndex > keyChainState.LocalIdKeyUse) {
							this.ThrowStrictModeReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_DIFFERENT, "Out key was lower than the security caches");

							if(!IdKeyUseIndexSet.CanBeForwarded(keyChainState.LocalIdKeyUse, cacheConsensus.KeyIndex)) {
								this.ThrowReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER, "The key sequence from the cache was higher than our own.");
							}

							// ok, lets update the key to the best index
							IdKeyUseIndexSet.Forward(keyChainState.LocalIdKeyUse, cacheConsensus.KeyIndex);
							using IXmssWalletKey walletKey = await this.MainWalletProvider.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.TRANSACTION_KEY_NAME, lockContext).ConfigureAwait(false);
							await this.UpdateKeyIndex(walletKey, keyChainState.LocalIdKeyUse, lockContext, false).ConfigureAwait(false);

						} else if(cacheConsensus.KeyIndex < keyChainState.LocalIdKeyUse) {
							// we dont need to report anything. cache could just be stale. as long as we are ahead
						}
					}
				}
			}
		}
		
		public virtual async Task SaveAccountKeyIndiciesCaches(string accountCode, LockContext lockContext) {
			this.MainWalletProvider.EnsureWalletIsLoaded();

			var configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration;

			if(configuration.KeyLogMode == AppSettingsBase.KeyLogModes.Disabled || !this.walletKeyIndexCaches.ContainsKey(accountCode)) {
				return;
			}

			var account = await this.MainWalletProvider.GetWalletAccount(accountCode, lockContext).ConfigureAwait(false);

			WalletKeyIndexCacheEntry cacheEntry = this.walletKeyIndexCaches[accountCode];

			// ok, do the compare
			
			WalletChainStateFileInfo walletChainStateInfo = this.MainWalletProvider.WalletFileInfo.Accounts[accountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			foreach(var key in account.Keys) {
				
				// chainstate entry
				IWalletAccountChainStateKey keyChainState = chainState.Keys[key.Ordinal];

				if(!cacheEntry.localWalletKeyIndexCache.keys.ContainsKey(key.Ordinal)) {
					cacheEntry.localWalletKeyIndexCache.keys.Add(key.Ordinal, new WalletKeyIndexCache.WalletKeyIndexCacheEntry());
				}
				if(!cacheEntry.globalWalletKeyIndexCache.keys.ContainsKey(key.Ordinal)) {
					cacheEntry.globalWalletKeyIndexCache.keys.Add(key.Ordinal, new WalletKeyIndexCache.WalletKeyIndexCacheEntry());
				}

				void SetCacheIndex(WalletKeyIndexCache.WalletKeyIndexCacheEntry entry) {
					if(keyChainState.LocalIdKeyUse != entry.KeyIndex) {
						entry.KeyIndex = keyChainState.LocalIdKeyUse;
						entry.Timestamp = this.CentralCoordinator.BlockchainServiceSet.BlockchainTimeService.GetChainDateTimeOffset(this.CentralCoordinator.ChainComponentProvider.ChainStateProviderBase.ChainInception);
						cacheEntry.changed = true;
					}
				}
				
				SetCacheIndex(cacheEntry.localWalletKeyIndexCache.keys[key.Ordinal]);
				SetCacheIndex(cacheEntry.globalWalletKeyIndexCache.keys[key.Ordinal]);
			}

			if(cacheEntry.changed) {
				
				// ok, lets update it all
				WalletKeyLogFileInfo keyLogFileInfo = this.MainWalletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletKeyLogsInfo;

				WalletAccountKeyLogMetadata keyLogMetadata = await keyLogFileInfo.GetKeyLogMetadata(lockContext).ConfigureAwait(false);
				
				using var dehydrator = DataSerializationFactory.CreateDehydrator();
				cacheEntry.localWalletKeyIndexCache.Dehydrate(dehydrator);

				using var bytes = dehydrator.ToArray();
				using var encryptedBytes = CryptoUtil.Encrypt(bytes, cacheEntry.password, cacheEntry.salt);
				
				string localWalletCachePath = this.GetLocalWalletKeyIndexCacheFileName(keyLogMetadata.KeyIndexEncryptionFileName);
				
				FileExtensions.EnsureDirectoryStructure(this.GetLocalWalletKeyIndexCachePath(), this.CentralCoordinator.FileSystem);
				await FileExtensions.WriteAllBytesAsync(localWalletCachePath, encryptedBytes.Span, this.CentralCoordinator.FileSystem).ConfigureAwait(false);

				await CentralCoordinator.ChainComponentProvider.ChainDataWriteProviderBase.WriteGlobalWalletKeyIndexCacheFile(keyLogMetadata.KeyIndexEncryptionFileName, encryptedBytes).ConfigureAwait(false);
			}
		}

		private void ThrowStrictModeReportableError(ReportableErrorType errorType, string message, string[] parameters = null) {
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.KeyLogMode == AppSettingsBase.KeyLogModes.Strict) {
				// here we launch a serious alert!
				this.ThrowReportableError(errorType, message, parameters);
			}
		}
		
		private void ThrowReportableError(ReportableErrorType errorType, string message, string[] parameters = null) {

			// here we launch a serious alert!
			throw new ReportableException(errorType, ReportableException.PriorityLevels.Warning, ReportableException.ReportLevels.Modal, this.CentralCoordinator.ChainId, this.CentralCoordinator.ChainName, message, parameters);
		}
		
		public async Task<IdKeyUseIndexSet> GetChainStateLastSyncedKeyHeight(IWalletKey key, LockContext lockContext) {
			this.MainWalletProvider.EnsureWalletIsLoaded();

			IWalletAccount account = (await this.MainWalletProvider.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountCode);

			WalletChainStateFileInfo walletChainStateInfo = this.MainWalletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;

			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);

			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];

			return keyChainState.LatestBlockSyncIdKeyUse;

		}

		public async Task UpdateLocalChainStateKeyHeight(IWalletKey key, LockContext lockContext) {
			this.MainWalletProvider.EnsureWalletIsLoaded();
			
			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.KeyLogMode == AppSettingsBase.KeyLogModes.Disabled) {
				return;
			}

			IWalletAccount account = (await this.MainWalletProvider.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(key.AccountCode);
			
			WalletChainStateFileInfo walletChainStateInfo = this.MainWalletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;
			
			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);
			
			IWalletAccountChainStateKey keyChainState = chainState.Keys[key.KeyAddress.OrdinalId];
			
			// lets check essential security. if we have had a higher key index, this is catastrophic and we stop no matter what
			if((keyChainState.LocalIdKeyUse?.IsSet??false) && keyChainState.LocalIdKeyUse > key.KeyAddress.KeyUseIndex) {
				this.ThrowReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER, "The key sequence is lower than the one we have in the chain state");
			}
			
			if((keyChainState.LatestBlockSyncIdKeyUse?.IsSet??false) && keyChainState.LatestBlockSyncIdKeyUse > key.KeyAddress.KeyUseIndex) {
				this.ThrowReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER, "The key sequence is lower than the last synced block value");
			}
			
			if(key is IXmssWalletKey xmssWalletKey) {
				
				keyChainState.LocalIdKeyUse.KeyUseIndex = key.KeyAddress.KeyUseIndex.KeyUseIndexSet.KeyUseIndex;
			}
			
			keyChainState.LocalIdKeyUse.KeyUseSequenceId = key.KeyAddress.KeyUseIndex.KeyUseIndexSet.KeyUseSequenceId;
		}

		/// <summary>
		///     update the key chain state with the highest key use we have found in the block.
		/// </summary>
		/// <param name="accountCode"></param>
		/// <param name="highestIdKeyUse"></param>
		/// <exception cref="ApplicationException"></exception>
		public async Task UpdateLocalChainStateTransactionKeyLatestSyncHeight(string accountCode, IdKeyUseIndexSet highestIdKeyUse, LockContext lockContext) {

			if(this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.ChainConfiguration.KeyLogMode == AppSettingsBase.KeyLogModes.Disabled) {
				return;
			}

			this.MainWalletProvider.EnsureWalletIsLoaded();
			IWalletAccount account = (await this.MainWalletProvider.WalletBase(lockContext).ConfigureAwait(false)).GetAccount(accountCode);
			
			WalletChainStateFileInfo walletChainStateInfo = this.MainWalletProvider.WalletFileInfo.Accounts[account.AccountCode].WalletChainStatesInfo;
			
			IWalletAccountChainState chainState = await walletChainStateInfo.ChainState(lockContext).ConfigureAwait(false);
			
			IWalletAccountChainStateKey keyChainState = chainState.Keys[highestIdKeyUse.Ordinal];
			
			if((keyChainState.LatestBlockSyncIdKeyUse?.IsSet??false) && (highestIdKeyUse < keyChainState.LatestBlockSyncIdKeyUse)) {
				this.ThrowStrictModeReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER, "The last synced block transaction key sequence is lower than the value in our wallet. We may have a corrupt wallet and can not use it safely.");
			}
			
			if((keyChainState.LocalIdKeyUse?.IsSet??false) && highestIdKeyUse > keyChainState.LocalIdKeyUse) {
				if(IdKeyUseIndexSet.IsSequenceHigher(highestIdKeyUse, keyChainState.LocalIdKeyUse)) {
					this.ThrowReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_HIGHER, "The last synced block transaction key sequence is higher than the value in our wallet. We may have an out of date wallet and can not use it safely.");
				}
			
				if(IdKeyUseIndexSet.CanBeForwarded(keyChainState.LocalIdKeyUse, highestIdKeyUse)) {
					
					using IXmssWalletKey key = await this.MainWalletProvider.LoadKey<IXmssWalletKey>(account.AccountCode, GlobalsService.TRANSACTION_KEY_NAME, lockContext).ConfigureAwait(false);
			
					await this.UpdateKeyIndex(key, highestIdKeyUse, lockContext).ConfigureAwait(false);
				}
			}
			
			keyChainState.LatestBlockSyncIdKeyUse = highestIdKeyUse;
		}
		
		
		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false){
			
			return SignEvent(transactionHash, accountCode, keyName, lockContext, allowPassKeyLimit);
		}
		
		public async Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false){
		
			IWalletAccount activeAccount = await this.MainWalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			return await SignTransaction(transactionHash, activeAccount.AccountCode, keyName, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}
		
		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, LockContext lockContext, bool allowPassKeyLimit = false){
			return SignTransaction(transactionHash, GlobalsService.TRANSACTION_KEY_NAME, lockContext, allowPassKeyLimit);
		}

		public Task<SafeArrayHandle> SignTransaction(SafeArrayHandle transactionHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return SignEvent(transactionHash, key, lockContext, allowPassKeyLimit);
		}
		
		public async Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			
			IWalletAccount activeAccount = await this.MainWalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			
			return await this.SignMessage(message, activeAccount.AccountCode, GlobalsService.MESSAGE_KEY_NAME, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public async Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, string accountCode, LockContext lockContext, bool allowPassKeyLimit = false) {
			
			return await this.SignMessage(message, accountCode, GlobalsService.MESSAGE_KEY_NAME, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}
		public Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, string accountCode, LockContext lockContext, bool allowPassKeyLimit = false) {
			return SignMessage(message,accountCode, GlobalsService.VALIDATOR_SIGNATURE_KEY_NAME, lockContext, allowPassKeyLimit);
		}

		public async Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {
			
			IWalletAccount activeAccount = await this.MainWalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			return await SignValidatorMessage(message, activeAccount.AccountCode, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public Task<SafeArrayHandle> SignValidatorMessage(SafeArrayHandle message, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {
			return SignMessage(message, key, lockContext, allowPassKeyLimit);
		}
		
		public async Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false) {

			if(key == null) {
				throw new ApplicationException($"The key named '{key.Name}' could not be loaded. Make sure it is available before progressing.");
			}
			
			var signature = await this.SignEvent(message, key, lockContext, allowPassKeyLimit).ConfigureAwait(false);
			
			// for messages, we never get confirmation, so we update the key height right away
			await this.UpdateLocalChainStateKeyHeight(key, lockContext).ConfigureAwait(false);

			// and confirmation too
			await this.UpdateLocalChainStateTransactionKeyLatestSyncHeight(key.AccountCode, key.KeyAddress.KeyUseIndex, lockContext).ConfigureAwait(false);

			return signature;
		}
		
		public async Task<SafeArrayHandle> SignMessage(SafeArrayHandle message, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			
			using IXmssWalletKey key = await this.MainWalletProvider.LoadKey<IXmssWalletKey>(k => k, accountCode, keyName, lockContext).ConfigureAwait(false);

			return await SignMessage(message, key, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public async Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			IWalletAccount activeAccount = await this.MainWalletProvider.GetActiveAccount(lockContext).ConfigureAwait(false);
			return await SignEvent(eventHash, activeAccount.AccountCode, keyName, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public async Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, string accountCode, string keyName, LockContext lockContext, bool allowPassKeyLimit = false) {
			using IXmssWalletKey key = await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.LoadKey<IXmssWalletKey>(keyName, lockContext).ConfigureAwait(false);

			return await SignEvent(eventHash, key, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public async Task<SafeArrayHandle> SignEvent(SafeArrayHandle eventHash, IWalletKey key, LockContext lockContext, bool allowPassKeyLimit = false){
			this.MainWalletProvider.EnsureWalletIsLoaded();

			//TODO: make sure we confirm our signature height in the wallet with the recorded one on chain. To prevent mistaken wallet copies.
			
			// thats it, lets perform the signature
			//TODO: we would want to do it for (potentially) sphincs and xmssmt too
			ChainConfigurations configuration = this.CentralCoordinator.ChainComponentProvider.ChainConfigurationProviderBase.GetChainConfiguration();
			
			if(configuration.KeyLogMode != AppSettingsBase.KeyLogModes.Disabled) {
				
				IdKeyUseIndexSet lastSyncedIdKeyUse = await this.GetChainStateLastSyncedKeyHeight(key, lockContext).ConfigureAwait(false);
				
				if(lastSyncedIdKeyUse.IsSet && key.KeyAddress.KeyUseIndex < lastSyncedIdKeyUse) {
					
					string message = $"Your key height for your key named {key.Name} is lower than the blockchain key use height. This is a very serious security issue. You might be using an older copy of your regular wallet.";
					if(!KeyUseIndexSet.CanBeForwarded(key.KeyAddress.KeyUseIndex, lastSyncedIdKeyUse)) {
				
						this.ThrowStrictModeReportableError(ReportableErrorTypes.Instance.BLOCKCHAIN_TRANSACTION_KEY_SEQUENCE_LOWER_THAN_DETECTED, message, new string[] {key.Name, key.Ordinal.ToString()});
					}
					
					// ok, we fast forward the key
					await this.UpdateKeyIndex(key, lastSyncedIdKeyUse, lockContext).ConfigureAwait(false);
					
					this.CentralCoordinator.Log.Warning(message + " As per configuration, we will still proceed.");
				}
			}

			// thats it, lets perform the signature
			return await PerformCryptographicSignature(key, eventHash, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}
		

		/// <summary>
		///     Here, we sign a message with the
		/// </summary>
		/// <param name="key"></param>
		/// <param name="message"></param>
		/// <returns></returns>
		/// <exception cref="ApplicationException"></exception>
		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(string accountCode, string keyName, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.MainWalletProvider.EnsureWalletIsLoaded();
			await this.MainWalletProvider.EnsureWalletKeyIsReady(accountCode, keyName, lockContext).ConfigureAwait(false);

			IWalletKey key = await this.MainWalletProvider.LoadKey<IWalletKey>(k => {
				return k;
			}, accountCode, keyName, lockContext).ConfigureAwait(false);

			if(key == null) {
				throw new ApplicationException($"The key named '{keyName}' could not be loaded. Make sure it is available before progressing.");
			}

			return await this.PerformCryptographicSignature(key, message, lockContext, allowPassKeyLimit).ConfigureAwait(false);
		}

		public virtual async Task<SafeArrayHandle> PerformCryptographicSignature(IWalletKey key, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			this.MainWalletProvider.EnsureWalletIsLoaded();
			await this.MainWalletProvider.EnsureWalletKeyIsReady(key.AccountCode, key.Name, lockContext).ConfigureAwait(false);

			SafeArrayHandle signature = null;

			if(key is IXmssWalletKey xmssWalletKey) {

				// check if we reached the maximum use of our key
				bool keyStillUsable = (xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex < xmssWalletKey.ChangeHeight) || allowPassKeyLimit;
				bool keyMaxedOut = xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex > xmssWalletKey.MaximumHeight;

				if(keyStillUsable && !keyMaxedOut) {

					(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey) result;
					
					if(key is IXmssMTWalletKey xmssMTWalletKey && (key.Version.Type == CryptographicKeyTypes.Instance.XMSSMT)) {
						result = await this.PerformXmssmtCryptographicSignature(xmssMTWalletKey, message, lockContext).ConfigureAwait(false);
					}
					else{
						result = await this.PerformXmssCryptographicSignature(xmssWalletKey, message, lockContext).ConfigureAwait(false);
					}
					
					signature = result.signature;

					// now we increment our private key
					xmssWalletKey.PrivateKey.Entry = result.nextPrivateKey.Entry;
					
					// reset the next node cache entry, we did not generate it yet
					xmssWalletKey.NextKeyNodeCache.Entry = null;
					
					xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex += 1;

					result.nextPrivateKey.Return();

					// save the key change
					await this.MainWalletProvider.UpdateKey(key, lockContext).ConfigureAwait(false);
				}

				List<(string accountCode, string name)> forcedKeys = new List<(string accountCode, string name)>();

				// we are about to use this key, let's make sure we check it to eliminate any applicable timeouts
				forcedKeys.Add((key.AccountCode, key.Name));

				await this.MainWalletProvider.ResetAllTimedOut(lockContext, forcedKeys).ConfigureAwait(false);

				if(key.Status != Enums.KeyStatus.Changing) {
					// Here we trigger the key change workflow, we must change the key, its time adn we wont trust the user to do it in time at this point. they were warned already

					if(keyMaxedOut) {
						this.CentralCoordinator.Log.Fatal($"Key named {key.Name} has reached end of life. It must be changed with a super key.");
					} else if(xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex >= xmssWalletKey.ChangeHeight) {
						this.CentralCoordinator.Log.Warning($"Key named {key.Name} has reached end of life. An automatic key change is being performed. You can not use the key until the change is fully confirmed.");

						this.KeyUseMaximumLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					} else if(xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex >= xmssWalletKey.WarningHeight) {
						this.CentralCoordinator.Log.Warning($"Key named {key.Name}  nearing its end of life. An automatic key change is being performed. You can keep using it until the change is fully confirmed.");
						this.KeyUseWarningLevelReached(key.KeyAddress.OrdinalId, xmssWalletKey.KeyAddress.KeyUseIndex.KeyUseIndex, xmssWalletKey.WarningHeight, xmssWalletKey.ChangeHeight, new CorrelationContext());
					}
				}

				if(!keyStillUsable) {
					// we have reached the maximum use amount for this key. we can't sign anything else until a key change happens
					throw new ApplicationException("Your xmss key has reached it's full use. A key change must now be performed!");
				}

				// ok, now we add an asynchronous prefetch for the key nodes caching
				List<Func<LockContext, Task>> transactionalSuccessActions = new List<Func<LockContext, Task>>();
				transactionalSuccessActions.Add(lc => {
					
					var factory = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase;
					var workflow = factory.CreateGenerateXmssKeyIndexNodeCacheWorkflow(xmssWalletKey.AccountCode, xmssWalletKey.Ordinal, xmssWalletKey.Index.KeyUseIndex, new CorrelationContext());
					this.CentralCoordinator.PostWorkflow(workflow);
					
					return Task.CompletedTask;
				});

				await this.CentralCoordinator.ChainComponentProvider.WalletProviderBase.AddTransactionSuccessActions(transactionalSuccessActions, lockContext).ConfigureAwait(false);
			}  else {
				throw new ApplicationException("Invalid key type provided");
			}

			return signature;
		}

		public virtual Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssCryptographicSignature(IXmssWalletKey keyxmssWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false, bool buildOptimizedSignature = false, XMSSSignaturePathCache xmssSignaturePathCache = null, SafeArrayHandle extraNodeCache = null, Action<XMSSProvider> callback = null, Func<int, int ,int, Task> progressCallback = null) {

			using XMSSProvider provider = new XMSSProvider(keyxmssWalletKey.HashType, keyxmssWalletKey.BackupHashType, keyxmssWalletKey.TreeHeight, this.XmssThreadMode, keyxmssWalletKey.NoncesExponent);
			provider.Initialize();

			if(callback != null) {
				callback(provider);
			}
			return provider.Sign(message, keyxmssWalletKey.PrivateKey, buildOptimizedSignature, xmssSignaturePathCache, extraNodeCache, progressCallback);
		}
		
		public virtual Task<(SafeArrayHandle signature, SafeArrayHandle nextPrivateKey)> PerformXmssmtCryptographicSignature(IXmssMTWalletKey xmssMTWalletKey, SafeArrayHandle message, LockContext lockContext, bool allowPassKeyLimit = false) {

			using XMSSMTProvider provider = new XMSSMTProvider(xmssMTWalletKey.HashType, xmssMTWalletKey.BackupHashType, xmssMTWalletKey.TreeHeight, xmssMTWalletKey.TreeLayers, this.XmssThreadMode, xmssMTWalletKey.NoncesExponent);
			provider.Initialize();
			
			return provider.Sign(message, xmssMTWalletKey.PrivateKey);
		}

		protected virtual void KeyUseWarningLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			// do nothing
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void KeyUseMaximumLevelReached(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			this.LaunchChangeKeyWorkflow(changeKeyOrdinal, keyUseIndex, warningHeight, maximumHeight, correlationContext);
		}

		protected virtual void LaunchChangeKeyWorkflow(byte changeKeyOrdinal, long keyUseIndex, long warningHeight, long maximumHeight, CorrelationContext correlationContext) {
			ICreateChangeKeyTransactionWorkflow<CENTRAL_COORDINATOR, CHAIN_COMPONENT_PROVIDER> changeKeyTransactionWorkflow = this.CentralCoordinator.ChainComponentProvider.ChainFactoryProviderBase.WorkflowFactoryBase.CreateChangeKeyTransactionWorkflow(changeKeyOrdinal, "automatically triggered keychange", correlationContext);

			this.CentralCoordinator.PostWorkflow(changeKeyTransactionWorkflow);
		}
	}
}