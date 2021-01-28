using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Neuralia.Blockchains.Core.Configuration;
using Neuralia.Blockchains.Core.Logging;
using Open.Nat;


namespace Neuralia.Blockchains.Core.P2p.Connections
{
    public interface IPortMappingService
    {
        Task<PortMappingStatus> GetPortMappingStatus();
        Task<bool> ConfigurePortMappingMode(bool useUPnP, bool usePmP, int natDeviceIndex);

        Task<bool> DiscoverAndSetup();
    }
    
    public class PortMapping
    {
        public string PublicIp { get; set; }
        public string PrivateIp { get; set; }
        public int PublicPort { get; set; }
        public int PrivatePort { get; set; }
        public string Description { get; set; }
        public DateTime Expiration { get; set; }
        
    }

    public class PortMappingDevice
    {
        
    }
    public class PortMappingStatus
    {
        public bool UsePmP { get; set; }
        public bool UseUPnP { get; set; }
        public List<string> DiscoveredDevicesNames { get; set; } = new List<string>();
        
        public int deviceIndex { get; set; }
        public string PublicIp { get; set; }
        public List<PortMapping> PortMappings { get; set; } = new List<PortMapping>();

        public PortMappingStatus(bool useUPnP, bool usePmP)
        {
            this.UsePmP = usePmP;
            this.UseUPnP = useUPnP;
        }
        
    }
    
    public class PortMappingService: IPortMappingService
    {
        public const string TAG = "[" + nameof(PortMappingService) + "]";

        private PortMappingStatus status;
        
        public int? localPort;
        public int? validatorPort;
        public int? validatorBackupPort;
        
        private NatDevice device;
        
        public PortMappingService()
        {
        }

        public PortMappingStatus Status {
            get {
                if(this.status == null) {
                    this.status = new PortMappingStatus(GlobalSettings.ApplicationSettings.UseUPnP, GlobalSettings.ApplicationSettings.UsePmP);
                }
                return this.status;
            }
            set => this.status = value;
        }

        public int LocalPort {
            get {
                if(!this.localPort.HasValue) {
                    this.localPort = GlobalSettings.ApplicationSettings.Port;
                }
                return this.localPort.Value;
            }
            set => this.localPort = value;
        }
        
        public int ValidatorPort {
            get {
                if(!this.validatorPort.HasValue) {
                    this.validatorPort = GlobalSettings.ApplicationSettings.ValidatorPort;
                }
                return this.validatorPort.Value;
            }
            set => this.validatorPort = value;
        }
        
        public int ValidatorBackupPort {
            get {
                if(!this.validatorBackupPort.HasValue) {
                    this.validatorBackupPort = GlobalSettings.ApplicationSettings.ValidatorHttpPort;
                }
                return this.validatorBackupPort.Value;
            }
            set => this.validatorBackupPort = value;
        }

        private void ResetStatus()
        {
            this.Status = new PortMappingStatus(this.Status.UseUPnP, this.Status.UsePmP);
        }

        public async Task<bool> DiscoverAndSetup()
        {
            this.ResetStatus();
            
            if (!this.Status.UseUPnP && !this.Status.UsePmP)
                return false;
            
            if(this.Status.UseUPnP)
                if(GlobalSettings.ApplicationSettings.EnableAppointmentValidatorBackupProtocol) {
                    NLog.Connections.Information($"You are using UPnP, make sure you open the needed ports in your firewall (UDP IN/OUT 1900:1901 for UPnP, and TCP IN {this.LocalPort}, {this.ValidatorPort} and {this.ValidatorBackupPort} for the Neuralium)");
                } else {
                    NLog.Connections.Information($"You are using UPnP, make sure you open the needed ports in your firewall (UDP IN/OUT 1900:1901 for UPnP, and TCP IN {this.LocalPort} and {this.ValidatorPort} for the Neuralium)");
                }

            if(this.Status.UsePmP)
                if(GlobalSettings.ApplicationSettings.EnableAppointmentValidatorBackupProtocol) {
                    NLog.Connections.Information($"You are using PmP, make sure you open the needed ports in your firewall (UDP IN/OUT 5350:5351 for Pmp, and TCP IN {this.LocalPort}, {this.ValidatorPort} and {this.ValidatorBackupPort} for the Neuralium)");
                } else {
                    NLog.Connections.Information($"You are using PmP, make sure you open the needed ports in your firewall (UDP IN/OUT 5350:5351 for Pmp, and TCP IN {this.LocalPort} and {this.ValidatorPort} for the Neuralium)");
                }
            
            try
            {
                var nat = new NatDiscoverer();
                using var cts = new CancellationTokenSource(5000);
                PortMapper mode = (this.Status.UseUPnP ? PortMapper.Upnp : 0) | (this.Status.UsePmP ? PortMapper.Pmp : 0);
                var devices = (await nat.DiscoverDevicesAsync(mode, cts).ConfigureAwait(false)).ToList();

                if (!devices.Any())
                {
                    NLog.Connections.Verbose($"{TAG}: no nat devices found, aborting...");
                    return false;
                }

                this.Status.DiscoveredDevicesNames = devices.Select(d => d.ToString()).ToList();
                this.Status.deviceIndex = Math.Min(this.Status.deviceIndex, devices.Count - 1);
                NLog.Connections.Verbose($"{TAG}: discovered {devices.Count} nat devices, using index {this.Status.deviceIndex} one");

                this.device = devices[this.Status.deviceIndex];
                
                var externalIp = await this.device.GetExternalIPAsync().ConfigureAwait(false);
                NLog.Connections.Verbose($"{TAG}: your external ip is {externalIp}, as found by device {this.device}");
                this.Status.PublicIp = externalIp.ToString();
                
                await this.device.CreatePortMapAsync(new Mapping(Protocol.Tcp, this.LocalPort, this.LocalPort, $"Neuralium port {this.LocalPort}")).ConfigureAwait(false);
                
                NLog.Connections.Verbose($"{TAG}: port mapping successful: {this.LocalPort}");
                
                await this.device.CreatePortMapAsync(new Mapping(Protocol.Tcp, this.ValidatorPort, this.ValidatorPort, $"Neuralium port {this.ValidatorPort}")).ConfigureAwait(false);
                
                NLog.Connections.Verbose($"{TAG}: port mapping successful: {this.ValidatorPort}");

                if(GlobalSettings.ApplicationSettings.EnableAppointmentValidatorBackupProtocol) {
                    await this.device.CreatePortMapAsync(new Mapping(Protocol.Tcp, this.ValidatorBackupPort, this.ValidatorBackupPort, $"Neuralium port {this.ValidatorBackupPort}")).ConfigureAwait(false);

                    NLog.Connections.Verbose($"{TAG}: port mapping successful: {this.ValidatorBackupPort}");
                }

                var mappings = await this.device.GetAllMappingsAsync().ConfigureAwait(false);

                NLog.Connections.Verbose($"{TAG}: port mapping found: {mappings.Count()}");
						
                foreach (var mapping in mappings)
                    NLog.Connections.Verbose($"{TAG}: port mapping found: {mapping}");

                this.Status.PortMappings = mappings.Select(m => new PortMapping{PrivatePort = m.PrivatePort
                    , PublicPort = m.PublicPort
                    , PrivateIp = m.PrivateIP.ToString()
                    , PublicIp = m.PublicIP.ToString()
                    , Expiration = m.Expiration,
                    Description = m.Description}).ToList();
						
            }
            catch (Exception e)
            {
                string protocol = this.Status.UseUPnP ? "UPnP" : (this.Status.UsePmP ? "PmP" : "No protocol");
                NLog.Connections.Information($"Port mapping unsuccessful, please verify that your router supports the {protocol} protocol and that it is enabled." +
                                             $"Also make sure only one device (and ip) is trying to map ports {this.LocalPort} and {this.ValidatorPort}. " +
                                             $"Finally, remember that you can also skip using a port mapping API and instead configure the port mapping yourself on your router." +
                " In that case, you can put 'UseUPnP' : false in your config.json");
                NLog.Connections.Verbose(e,$"{TAG}: Exception caught during UPnP port mapping attempt");
                
                return false;
            }

            return true;
        }
        public async Task<PortMappingStatus> GetPortMappingStatus()
        {
            return this.Status;
        }

        public async Task<bool> ConfigurePortMappingMode(bool useUPnP, bool usePmP, int natDeviceIndex)
        {
            this.Status.UseUPnP = useUPnP;
            this.Status.UsePmP = usePmP;
            this.Status.deviceIndex = natDeviceIndex;
            return await this.DiscoverAndSetup().ConfigureAwait(false);
        }
    }
}