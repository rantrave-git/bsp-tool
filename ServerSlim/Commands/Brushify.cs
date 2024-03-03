using Bsp.BspFormat;
using Bsp.Server.Abstractions;

namespace Bsp.Server.Commands;

public class Brushify : ICommandHandler
{
    private class Processor : ICommandProcessor
    {
        private readonly MeshContext _meshContext;

        public Processor(MeshContext mesh)
        {
            _meshContext = mesh;
        }

        public async ValueTask<string?> ResponseAsync(Stream stream, IProgressHandler progress)
        {
            var tree = BspNode3D.Build(_meshContext, false);
            var dst = new List<MeshContext>();
            tree.PushLeafBrushes(dst);
            // tree.PushLeafVolumes(dst);
            CommandController.Success(stream);
            stream.Write(dst.Count);
            foreach (var i in dst)
                await stream.WriteMeshAsync(i);
            return null;
        }
    }
    public async ValueTask<ICommandProcessor> ReceiveAsync(Stream stream, CancellationToken cancellationToken = default) =>
        new Processor(await stream.TryReadMesh(cancellationToken) ?? throw new CommandHandlerException("Mesh expected"));

}

public class BrushifyFactory : ICommandHandlerFactory
{
    public ICommandHandler CreateCommandHandler() => new Brushify();
}