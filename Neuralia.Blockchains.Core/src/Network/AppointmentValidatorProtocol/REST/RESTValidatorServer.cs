

using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection.Extensions;
#if NET5_0
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.Infrastructure;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;

#endif
using Neuralia.Blockchains.Tools;

namespace Neuralia.Blockchains.Core.Network.AppointmentValidatorProtocol.REST {
 	public class RESTValidatorServer : IDisposableExtended {
	    private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates = new();

	    public enum ServerModes {
		    Regular,Test
	    }

	    private ServerModes mode;
	    private int port;
	    public RESTValidatorServer(int port, ServerModes mode = ServerModes.Regular) {
		    this.mode = mode;
		    this.port = port;
	    }
#if NET5_0
 		private IHost host;
        private Task hostTask;
        
        public void Start() {

	        if(this.hostTask != null && !this.hostTask.IsCompleted) {
		        return;
	        }
	        IHostBuilder builder = Host.CreateDefaultBuilder(Array.Empty<string>()).ConfigureHostConfiguration(config => {
		        config.AddEnvironmentVariables();
	        }).ConfigureWebHostDefaults(webBuilder => {
		        
		        WebHostBuilderKestrelExtensions.UseKestrel(webBuilder, options => {
			        options.AddServerHeader = false;
			        
			        options.Limits.MaxRequestHeadersTotalSize = AppointmentValidatorController.MAX_REQUEST_SIZE;
			        options.Limits.MaxRequestBodySize = AppointmentValidatorController.MAX_REQUEST_SIZE;
			        options.Limits.MaxRequestBufferSize = AppointmentValidatorController.MAX_REQUEST_SIZE;
			        options.Limits.MaxRequestLineSize = AppointmentValidatorController.MAX_REQUEST_SIZE;
			        
			        options.Limits.RequestHeadersTimeout = TimeSpan.FromSeconds(10);
			        options.Listen(IPAddress.Any, this.port, listenOptions => {
			        });
		        }).UseContentRoot(Directory.GetCurrentDirectory()).UseStartup<Startup>(factory => {
			        return new Startup(this.appointmentValidatorDelegates, this.mode);
		        });
	        });

	        this.host = builder.Build();
	        this.hostTask = this.host.RunAsync();
        }

        public async Task Stop() {
	        try {
		        if(this.host != null) {
			        await this.host.StopAsync().ConfigureAwait(false);
		        }

		        if(this.hostTask != null) {
			        await this.hostTask.ConfigureAwait(false);
		        }
	        } catch(Exception ex) {
		        NLog.Default.Error(ex, "Failed to stop validator REST server");
	        }

	        try {
		       this.host?.Dispose();
	        } catch(Exception ex) {
		        NLog.Default.Error(ex, "Failed to stop validator REST server");
	        }
	        
	        this.host = null;
	        this.hostTask = null;
        }
       
        public void RegisterBlockchainDelegate(BlockchainType blockchainType, IAppointmentValidatorDelegate appointmentValidatorDelegate, Func<bool> isInAppointmentWindow) {
	        if(!this.appointmentValidatorDelegates.ContainsKey(blockchainType)) {
		        appointmentValidatorDelegate.Initialize();
		        this.appointmentValidatorDelegates.TryAdd(blockchainType, appointmentValidatorDelegate);
	        }
        }
        
        public void UnregisterBlockchainDelegate(BlockchainType blockchainType) {
	        if(this.appointmentValidatorDelegates.ContainsKey(blockchainType)) {
		        this.appointmentValidatorDelegates.TryRemove(blockchainType, out IAppointmentValidatorDelegate _);
	        }
        }
        
        public class Startup {
	        private readonly ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates;
	        
	        private ServerModes mode;
	        public Startup(ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate> appointmentValidatorDelegates, ServerModes mode = ServerModes.Regular) {
		        this.appointmentValidatorDelegates = appointmentValidatorDelegates;
		        var builder = new ConfigurationBuilder()
			        .AddEnvironmentVariables();
		        this.Configuration = builder.Build();
	        }
 
	        public IConfigurationRoot Configuration { get; }
 
	        // This method gets called by the runtime. Use this method to add services to the container.
	        public void ConfigureServices(IServiceCollection services)
	        {
		        services.AddHttpContextAccessor();
		        services.TryAddSingleton<IActionContextAccessor, ActionContextAccessor>();

		        // Add framework services.
		        services.AddControllers();

		        services.AddSingleton<ConcurrentDictionary<BlockchainType, IAppointmentValidatorDelegate>>(this.appointmentValidatorDelegates);
	        }
 
	        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
	        public void Configure(IApplicationBuilder app, ILoggerFactory loggerFactory)
	        {
		        app.UseRouting();
		        
		       
		        app.UseEndpoints(endpoints => {
			        if(this.mode == ServerModes.Regular) {
				        endpoints.MapControllerRoute("appointments", "{controller=appointments}/{action=Index}/{id?}");
			        } else if(this.mode == ServerModes.Test) {
				        endpoints.MapControllerRoute("appointments-test", "{controller=appointments-test}/{action=Index}/{id?}");
			        }
		        });
	        }
        }
#endif
    #region disposable

	    public bool IsDisposed { get; private set; }

	    public void Dispose() {
		    this.Dispose(true);
		    GC.SuppressFinalize(this);
	    }

	    protected virtual void Dispose(bool disposing) {

		    if(disposing && !this.IsDisposed) {
#if NET5_0
			    this.host?.Dispose();
			    #endif
		    }

		    this.IsDisposed = true;
	    }

	    ~RESTValidatorServer() {
		    this.Dispose(false);
	    }

    #endregion
 	}
}
