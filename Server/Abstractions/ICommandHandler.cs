namespace Bsp.Server.Abstractions;


public interface ICommandProcessor
{
    ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress);
}
public interface ICommandHandler
{
    ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default);
}
