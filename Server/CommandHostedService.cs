using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Bsp.Server;

public class CommandHostConfiguration
{
    public int Port { get; init; }
}
class CommandHostedService : IHostedService, IDisposable
{
    private ICommandController _controller;
    private CommandHostConfiguration _config;
    private IHostApplicationLifetime _lifetime;
    private ILogger<CommandHostedService> _logger;
    private Task? _running;
    private CancellationTokenSource _stopper = new CancellationTokenSource();

    public CommandHostedService(ILogger<CommandHostedService> logger, ICommandController controller, IOptions<CommandHostConfiguration> options, IHostApplicationLifetime lifetime)
    {
        _controller = controller;
        _config = options.Value;
        _lifetime = lifetime;
        _logger = logger;
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
            _logger.LogCritical(e, "Terminating...");
            _lifetime.StopApplication();
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