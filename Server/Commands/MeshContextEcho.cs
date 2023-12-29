using Bsp.Common.Geometry;
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
            await stream.WriteMesh(_meshContext);
            return null;
        }
    }
    public async ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default)
        => new Processor(await stream.TryReadMesh(cancellationToken) ?? throw new CommandHandlerException("Failed to read mesh"));

}