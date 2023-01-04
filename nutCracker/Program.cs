using CurrieTechnologies.Razor.SweetAlert2;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using nutCracker.Database;
using nutCracker.Services;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();
builder.Services.AddServerSideBlazor(o => o.DetailedErrors = true);
builder.Services.AddSweetAlert2();

builder.Services.AddDbContext<NutCrackerContext>(opts => 
    opts.UseSqlServer(builder.Configuration.GetConnectionString("NutCrackerContext")), 
    ServiceLifetime.Scoped, 
    ServiceLifetime.Scoped);

builder.Services.AddSingleton<DockerService>();
builder.Services.AddSingleton<WebsocketService>();

builder.Services.AddHostedService<SlaveVerifService>();

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();
app.UseWebSockets();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.MapBlazorHub();

var dockerService = app.Services.GetService<DockerService>();

for(var i = 0; i < 5; i++)
{
    try
    {
        var connection = new SqlConnection(builder.Configuration.GetConnectionString("NutCrackerContext"));
        await connection.OpenAsync();
        
        if (connection.State == System.Data.ConnectionState.Open)
        {
            await connection.CloseAsync();
            break;
        }
    }
    catch (Exception)
    {
        Console.WriteLine($"{i} - Retry connecting to database...");
    }
    
    await Task.Delay(1500);
}

try
{
    await app.RunAsync();
}
catch (Exception e)
{
    Console.Error.WriteLine(e);
}

await dockerService.DeleteService();