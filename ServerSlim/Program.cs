// See https://aka.ms/new-console-template for more information
using Bsp.Server;
using Bsp.Server.Commands;

enum Command : int
{
    EchoMesh = 0x1,
    MeshConvexSplit = 0x2,
}

static class Application
{
    public static async Task Main(string[] args)
    {
        // var builder = Host.CreateDefaultBuilder(args);
        // var builder = Host.CreateApplicationBuilder(args);
        // var config = new ConfigurationBuilder().SetBasePath(builder.Environment.ContentRootPath)
        //         .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
        //         .AddJsonFile($"appsettings.{builder.Environment.EnvironmentName}.json", optional: true, reloadOnChange: false)
        //         .AddEnvironmentVariables().Build();

        // var s = builder.Services;
        // builder.Logging.AddConsole();
        // // builder.ConfigureServices(s =>
        // {
        //     s.Configure<CommandHostConfiguration>(config.GetSection("CommandHost"));
        //     s.AddHostedService<CommandHostedService>();
        //     s.AddScoped<MeshContextEcho>();
        //     s.AddScoped<MeshSubdivideToBrushes>();

        //     s.AddScoped<ICommandHandlerProvider, CommandHandlerProvider>(s =>
        //         ActivatorUtilities.CreateInstance<CommandHandlerProvider>(s)
        //             .Register<MeshContextEcho>((int)Command.EchoMesh)
        //             .Register<MeshSubdivideToBrushes>((int)Command.MeshConvexSplit)
        //     );
        //     s.AddScoped<ICommandController, CommandController>();
        // }//)
        // builder.Build().Run();
        var chp = new CommandHandlerProvider();
        chp.Register((int)Command.EchoMesh, new MeshContextEchoFactory());
        chp.Register((int)Command.MeshConvexSplit, new MeshSubdivideToBrushesFactory());
        var cc = new CommandController(chp);
        var svc = new CommandHostedService(cc, new CommandHostConfiguration()
        {
            Port = 2232
        });
        await svc.StartAsync(CancellationToken.None);
    }
}