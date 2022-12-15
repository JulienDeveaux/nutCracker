using Docker.DotNet;
using Docker.DotNet.Models;
using nutCracker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<WebsocketService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
    // The default HSTS value is 30 days. You may want to change this for production scenarios, see https://aka.ms/aspnetcore-hsts.
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseWebSockets();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

var dockerService = app.Services.GetService<DockerService>();

try
{
    await app.RunAsync();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}

await dockerService.DeleteService();