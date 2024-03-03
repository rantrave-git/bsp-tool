using Bsp.BspFormat;
using Bsp.Server.Abstractions;

namespace Bsp.Server.Commands;

public class MeshContextEcho : ICommandHandler
{
    private class Processor : ICommandProcessor
    {
        private readonly MeshContext _meshContext;

        public Processor(MeshContext meshContext)
        {
            _meshContext = meshContext;
        }

        public async ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress)
        {
            CommandController.Success(stream);
            await stream.WriteMeshAsync(_meshContext);
            return null;
        }
    }
    public async ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default)
        => new Processor(await stream.TryReadMesh(cancellationToken) ?? throw new CommandHandlerException("Failed to read mesh"));

}

public class MeshContextEchoFactory : ICommandHandlerFactory
{
    public ICommandHandler CreateCommandHandler() => new MeshContextEcho();
}