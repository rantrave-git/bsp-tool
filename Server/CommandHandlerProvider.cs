using Bsp.Server.Abstractions;

namespace Bsp.Server;

[Serializable]
public class CommandHandlerException : Exception
{
    public CommandHandlerException() { }
    public CommandHandlerException(string message) : base(message) { }
    public CommandHandlerException(string message, System.Exception inner) : base(message, inner) { }
    protected CommandHandlerException(
        System.Runtime.Serialization.SerializationInfo info,
        System.Runtime.Serialization.StreamingContext context) : base(info, context) { }
}

class CommandHandlerProvider : ICommandHandlerProvider
{
    Dictionary<int, Type> _commands = new();
    public CommandHandlerProvider Register<T>(int command) where T : ICommandHandler
    {
        _commands[command] = typeof(T);
        return this;
    }
    public Dictionary<int, ICommandHandler> GetHandlers(IServiceProvider serviceProvider)
    {
        return _commands.Select(x => (x.Key, serviceProvider.GetService(x.Value) as ICommandHandler))
            .Where(x => x.Item2 != null).ToDictionary(x => x.Key, x => x.Item2!);
    }
}