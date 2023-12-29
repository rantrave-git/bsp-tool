namespace Bsp.Server.Abstractions;

public interface ICommandHandlerProvider
{
    Dictionary<int, ICommandHandler> GetHandlers(IServiceProvider serviceProvider);
}
