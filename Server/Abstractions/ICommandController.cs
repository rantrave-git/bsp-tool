namespace Bsp.Server.Abstractions;

public interface ICommandController
{
    Task StartAsync(int port, CancellationToken cancellationToken = default);
}
