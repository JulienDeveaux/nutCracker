using Docker.DotNet;
using Docker.DotNet.Models;

namespace nutCracker.Services;

public class DockerService
{
    private DockerClient Client { get; }
    
    private SwarmService SlavesService { get; set; }
    
    public DockerService()
    {
        /*
         * windows with docker desktop: npipe://./pipe/docker_engine
         * linux: unix:///var/run/docker.sock
        */

        var socketSystem = Environment.OSVersion.Platform switch
        {
            PlatformID.Unix => "unix:///var/run/docker.sock",
            PlatformID.Win32S or PlatformID.Win32Windows or PlatformID.Win32NT or PlatformID.WinCE => "npipe://./pipe/docker_engine",
            _ => throw new PlatformNotSupportedException()
        };
        
        Client = new DockerClientConfiguration(new Uri(socketSystem)).CreateClient();
    }

    public async Task InitService(bool forceReload = false)
    {


        if (SlavesService is not null)
        {
            try
            {
                await Client.Swarm.InspectServiceAsync(SlavesService.ID);
                    
                if(!forceReload)
                    return;
            }
            catch (Exception)
            {
                // ignored
            }
        }

        var services = (await Client.Swarm.ListServicesAsync(new ServicesListParameters
        {
            Filters = new ServiceFilter
            {
                Name = new[] { "NutCracker" }
            }
        })).ToList();

        if (services.Count == 0)
        {
            var response = await Client.Swarm.CreateServiceAsync(new ServiceCreateParameters
            {
                Service = new ServiceSpec
                {
                    Name = "NutCracker",
                    TaskTemplate = new TaskSpec
                    {
                        ContainerSpec = new ContainerSpec
                        {
                            Image = "servuc/hash_extractor",
                            Command = new List<string>
                            {
                                "./hash_extractor", "s", "ws://nut_cracker/ws"
                            }
                        }
                    },
                    Networks = new List<NetworkAttachmentConfig>
                    {
                        new NetworkAttachmentConfig
                        {
                            Target = "nutcracker_nut_cracker"
                        }
                    }
                }
            });

            services.Add(await Client.Swarm.InspectServiceAsync(response.ID));
        }

        SlavesService = services.FirstOrDefault();
    }

    public async Task AddNewSlave()
    {
        await AddSlaves(1);
    }
    
    public async Task AddSlaves(int nbSlavesToAdd)
    {
        var serviceSpec = SlavesService.Spec;

        serviceSpec.Mode.Replicated.Replicas += Convert.ToUInt64(nbSlavesToAdd);
        
        var response = await Client.Swarm.UpdateServiceAsync(SlavesService.ID, new ServiceUpdateParameters
        {
            Service = serviceSpec,
            Version = (long) SlavesService.Version.Index
        });
        
        SlavesService = await Client.Swarm.InspectServiceAsync(SlavesService.ID);
    }

    public async Task DeleteService()
    {
        await Client.Swarm.RemoveServiceAsync(SlavesService.ID);
    }
}