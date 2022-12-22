using System.Text.RegularExpressions;
using CurrieTechnologies.Razor.SweetAlert2;
using Microsoft.AspNetCore.Components;
using Microsoft.AspNetCore.Components.Web;
using Microsoft.EntityFrameworkCore;
using nutCracker.Database;
using nutCracker.Models;
using nutCracker.Services;

namespace nutCracker.Views.BlazorComponents;

public partial class Index
{
    private const int MaxMaxPaswordLength = 20;
    private const int MinMaxPaswordLength = 1;

    [Inject] private WebsocketService WebsocketService { get; set; }
    [Inject] private DockerService DockerService { get; set; }
    [Inject] private NutCrackerContext Database { get; set; }
    [Inject] private SweetAlertService Swal { get; set; }

    private string Hash { get; set; } = string.Empty;
    private int MaxPasswordLength { get; set; } = 4;
    private AlgoPower Power { get; set; } = AlgoPower.Classic;

    private string ConstantMessage { get; set; } = string.Empty;

    private async Task InputKeyEvent(KeyboardEventArgs e)
    {
        if (e.Key == "Enter")
            await DeHash();
    }

    private async Task DeHash()
    {
        if (!Regex.IsMatch(Hash, "^[a-f0-9]{32}$"))
        {
            _ = Swal.FireAsync(new SweetAlertOptions
            {
                Title = "Erreur",
                Icon = SweetAlertIcon.Error,
                Text = "Le hash n'est pas valide"
            });

            return;
        }

        _ = Swal.FireAsync(new SweetAlertOptions
        {
            Title = "Déhashage en cours",
            Icon = SweetAlertIcon.Info,
            ShowLoaderOnConfirm = true,
            PreConfirm = new PreConfirmCallback(async () =>
            {
                string mdp;
                
                Console.WriteLine($"check if hash {Hash} is already cracked");
                var result = await Database.HashResults.FirstOrDefaultAsync(hashResult => hashResult.Hash == Hash);

                if (result != null)
                {
                    Console.WriteLine("found in database");

                    mdp = result.Result;
                }
                else
                {
                    if (WebsocketService.GetNbSlaves(SlaveStatus.Ready) < Power.Value())
                    {
                        var nbSlaves = WebsocketService.GetNbSlaves(SlaveStatus.Ready);

                        await DockerService.AddSlaves(Power.Value() - nbSlaves);

                        Console.WriteLine("awaiting new slave");

                        while (WebsocketService.GetNbSlaves(SlaveStatus.Ready) < Power.Value())
                        {
                            await Task.Delay(1000);
                        }

                        Console.WriteLine("new slaves available");
                    }

                    mdp = await WebsocketService.Crack(Hash,
                        Math.Max(MinMaxPaswordLength, Math.Min(MaxMaxPaswordLength, MaxPasswordLength)));

                }
                
                if (mdp == null)
                {
                    _ = Swal.FireAsync(new SweetAlertOptions
                    {
                        Title = "Erreur",
                        Icon = SweetAlertIcon.Error,
                        Text = "Une erreur est survenue"
                    });

                    return string.Empty;
                }

                if (string.IsNullOrWhiteSpace(mdp))
                {
                    _ = Swal.FireAsync(new SweetAlertOptions
                    {
                        Title = "Erreur",
                        Icon = SweetAlertIcon.Error,
                        Text = "Le hash n'a pas été trouvé"
                    });

                    return string.Empty;
                }

                _ = Swal.FireAsync(new SweetAlertOptions
                {
                    Title = "Succès",
                    Icon = SweetAlertIcon.Success,
                    Text = $"Résultat: {mdp}"
                });

                ConstantMessage = $"Dernier résultat: {mdp}";
                _ = InvokeAsync(StateHasChanged);

                return null;
            })
        });

        await Swal.ClickConfirmAsync();
    }
}