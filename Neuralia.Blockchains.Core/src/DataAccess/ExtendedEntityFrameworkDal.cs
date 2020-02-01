using System;
using Microsoft.EntityFrameworkCore;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.General.Versions;
using Neuralia.Blockchains.Core.Services;
using Neuralia.Blockchains.Core.Tools;

namespace Neuralia.Blockchains.Core.DataAccess {

	public interface IExtendedEntityFrameworkDal<out DBCONTEXT> : IEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : IEntityFrameworkContext {
	}

	public abstract class ExtendedEntityFrameworkDal<DBCONTEXT> : EntityFrameworkDal<DBCONTEXT>, IExtendedEntityFrameworkDal<DBCONTEXT>
		where DBCONTEXT : DbContext, IEntityFrameworkContext {
		protected readonly ServiceSet serviceSet;

		protected readonly ITimeService timeService;

		protected ExtendedEntityFrameworkDal(ServiceSet serviceSet, SoftwareVersion softwareVersion, Func<AppSettingsBase.SerializationTypes, DBCONTEXT> contextInstantiator, AppSettingsBase.SerializationTypes serializationType) : base(softwareVersion, contextInstantiator, serializationType) {
			this.timeService = serviceSet?.TimeService;
			this.serviceSet = serviceSet;
		}
	}
}