// See https://aka.ms/new-console-template for more information
using Bsp.Server;
using Bsp.Server.Commands;

enum Command : int
{
    EchoMesh = 0x1,
    // MeshConvexSplit = 0x2,
    MeshTest = 0x3,
    Brushify = 0x4,
}

static class Application
{
    public static async Task Main(string[] args)
    {
        var chp = new CommandHandlerProvider();
        chp.Register((int)Command.EchoMesh, new MeshContextEchoFactory());
        // chp.Register((int)Command.MeshConvexSplit, new MeshSubdivideToBrushesFactory());
        chp.Register((int)Command.MeshTest, new MeshContextTestFactory());
        chp.Register((int)Command.Brushify, new BrushifyFactory());
        var cc = new CommandController(chp);
        using var cts = new CancellationTokenSource();
        AppDomain.CurrentDomain.ProcessExit += (sender, ev) =>
        {
            cts.Cancel();
            Console.WriteLine("Stopping..");
        };
        var svc = new CommandHostedService(cc, new CommandHostConfiguration()
        {
            Port = 2232
        });
        await svc.Run(cts.Token);
        Console.WriteLine("Done");
    }
}