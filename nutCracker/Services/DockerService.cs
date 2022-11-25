using Docker.DotNet;
using Docker.DotNet.Models;

namespace nutCracker.Services;

public class DockerService
{
    private DockerClient Client { get; }
    
    private SwarmService SlavesService { get; set; }
    
    public DockerService()
    {
        Client = new DockerClientConfiguration(
                new Uri("unix:///var/run/docker.sock"))
            .CreateClient();
    }

    public async Task InitService(bool forceReload = false)
    {
        if (!forceReload && SlavesService is not null)
            return;

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
}