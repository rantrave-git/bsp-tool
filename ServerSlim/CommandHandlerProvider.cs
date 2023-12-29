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

interface ICommandHandlerFactory
{
    ICommandHandler CreateCommandHandler();
}

class CommandHandlerProvider : ICommandHandlerProvider
{
    Dictionary<int, ICommandHandlerFactory> _commands = new();
    public CommandHandlerProvider Register(int command, ICommandHandlerFactory factory)
    {
        _commands[command] = factory;
        return this;
    }
    public Dictionary<int, ICommandHandler> GetHandlers()
    {
        return _commands.Select(x => (x.Key, x.Value.CreateCommandHandler()))
            .Where(x => x.Item2 != null).ToDictionary(x => x.Key, x => x.Item2!);
    }
}