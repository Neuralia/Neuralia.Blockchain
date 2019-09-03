using System;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.MessageRegistry;
using Neuralia.Blockchains.Core.DataAccess.Interfaces.PeerRegistry;
using Neuralia.Blockchains.Core.DataAccess.Sqlite.MessageRegistry;
using Neuralia.Blockchains.Core.DataAccess.Sqlite.PeerRegistry;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess {

	public interface IDalCreationFactory {
		Func<string, ServiceSet, IMessageRegistryDal> CreateMessageRegistryDAL { get; }
		Func<AppSettingsBase.SerializationTypes, IMessageRegistryContext> CreateMessageRegistryContext { get; }

		Func<string, ServiceSet, IPeerRegistryDal> CreatePeerRegistryDAL { get; }
		Func<AppSettingsBase.SerializationTypes, IPeerRegistryContext> CreatePeerRegistryContext { get; }
	}

	/// <summary>
	///     a non chain version of the Dal creation factory
	/// </summary>
	public class DalCreationFactory : IDalCreationFactory {
		public virtual Func<string, ServiceSet, IMessageRegistryDal> CreateMessageRegistryDAL => (folderPath, serviceSet) => new MessageRegistrySqliteDal(folderPath, serviceSet, this, GlobalSettings.ApplicationSettings.SerializationType);
		public virtual Func<AppSettingsBase.SerializationTypes, IMessageRegistryContext> CreateMessageRegistryContext => EntityFrameworkContext.CreateContext<MessageRegistrySqliteContext>;
		public virtual Func<string, ServiceSet, IPeerRegistryDal> CreatePeerRegistryDAL => (folderPath, serviceSet) => new PeerRegistrySqliteDal(folderPath, serviceSet, this, GlobalSettings.ApplicationSettings.SerializationType);
		public virtual Func<AppSettingsBase.SerializationTypes, IPeerRegistryContext> CreatePeerRegistryContext => EntityFrameworkContext.CreateContext<PeerRegistrySqliteContext>;
	}
}