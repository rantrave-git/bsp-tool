// See https://aka.ms/new-console-template for more information
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Bsp;
using Bsp.Geometry;

var cts = new CancellationTokenSource();
if (true)
{
    var v = new CornerData[4];
    Console.WriteLine(MemoryMarshal.Cast<CornerData, byte>(v).Length);
    return;
}
Console.CancelKeyPress += delegate (object? sender, ConsoleCancelEventArgs args)
{
    Console.WriteLine("Interrupted");
    cts.Cancel();
    args.Cancel = true;
};
var controller = new Bsp.CommandController();
try
{
    controller.Start(1212, cts.Token).Wait();
}
catch (AggregateException e) { if (e.InnerExceptions.Any(x => !(x is OperationCanceledException))) throw; }
catch (OperationCanceledException) { }
Console.WriteLine("Done!");
