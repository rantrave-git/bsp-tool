// See https://aka.ms/new-console-template for more information
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Bsp.Server;
using Microsoft.Extensions.Logging;
using Bsp.Server.Abstractions;
using Bsp.Server.Commands;

enum Command : int
{
    EchoMesh = 0x1,
    MeshConvexSplit = 0x2,
}

static class Application
{
    public static void Main(string[] args)
    {
        Environment.SetEnvironmentVariable("DOTNET_HOSTBUILDER__RELOADCONFIGONCHANGE", "false");
        // var builder = Host.CreateDefaultBuilder(args);
        var builder = Host.CreateApplicationBuilder(args);
        var config = new ConfigurationBuilder().SetBasePath(builder.Environment.ContentRootPath)
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
                .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
                .AddEnvironmentVariables()
                .Build();

        var s = builder.Services;
        builder.Logging.AddConsole();
        // builder.ConfigureServices(s =>
        {
            s.Configure<CommandHostConfiguration>(config.GetSection("CommandHost"));
            s.AddHostedService<CommandHostedService>();
            s.AddScoped<MeshContextEcho>();
            s.AddScoped<MeshSubdivideToBrushes>();

            s.AddScoped<ICommandHandlerProvider, CommandHandlerProvider>(s =>
                ActivatorUtilities.CreateInstance<CommandHandlerProvider>(s)
                    .Register<MeshContextEcho>((int)Command.EchoMesh)
                    .Register<MeshSubdivideToBrushes>((int)Command.MeshConvexSplit)
            );
            s.AddScoped<ICommandController, CommandController>();
        }//)
        builder.Build().Run();
    }
}