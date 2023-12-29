using Bsp.Server.Abstractions;

namespace Bsp.Server;

public class CommandHostConfiguration
{
    public int Port { get; init; }
}
class CommandHostedService : IDisposable
{
    private ICommandController _controller;
    private CommandHostConfiguration _config;
    private Task? _running;
    private CancellationTokenSource _stopper = new CancellationTokenSource();

    public CommandHostedService(ICommandController controller, CommandHostConfiguration config)
    {
        _controller = controller;
        _config = config;
    }
    private async Task Watch(Task task)
    {
        try
        {
            await task;
        }
        catch (OperationCanceledException)
        {
            return;
        }
        catch (Exception e)
        {
            Console.WriteLine(e.Message + "\nTerminating...");
        }
    }
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _running = Watch(_controller.StartAsync(_config.Port, _stopper.Token));
        return Task.CompletedTask;
    }

    public async Task StopAsync(CancellationToken cancellationToken)
    {
        if (_running == null) return;
        _stopper.Cancel();
        await _running;
    }

    public void Dispose() => _stopper.Dispose();
}