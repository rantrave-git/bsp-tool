using System.Buffers;
using System.Net;
using System.Net.Sockets;
using Bsp.BspFormat;
// using Bsp.Common.Geometry;
using Bsp.Server.Abstractions;
// using Microsoft.Extensions.Logging;

namespace Bsp.Server;

public class CommandController : ICommandController
{
    public enum Status : int
    {
        Success = 0,
        FailedRequest = 0x1,
        FailedProcess = 0x2,
        InternalError = 0x10,
        Timeout = 0x20,
        Progress = 0x10000000,
    }
    private class ProgressHandler : IProgressHandler
    {
        private readonly Stream _stream;

        public ProgressHandler(Stream stream)
        {
            _stream = stream;
        }
        public ValueTask OnProgress(int done, int all)
        {
            _stream.Write((int)Status.Progress);
            _stream.Write(done);
            _stream.Write(all);
            return ValueTask.CompletedTask;
        }
    }

    private TcpListener? _listener;
    private readonly Dictionary<int, ICommandHandler> _handlers;

    public CommandController(ICommandHandlerProvider commandHandlerProvider)
    {
        _handlers = commandHandlerProvider.GetHandlers();
    }
    public static void Success(Stream stream)
    {
        stream.Write((int)Status.Success);
    }
    public static void Fail(Stream stream)
    {
        stream.Write((int)Status.FailedRequest);
    }
    public async Task StartAsync(int port, CancellationToken cancellationToken = default)
    {
        IPAddress localhost = IPAddress.Loopback;
        _listener = new TcpListener(localhost, port);
        _listener.Start();
        Console.WriteLine($"Listening on {port}...");
        try
        {
            while (true)
            {
                cancellationToken.ThrowIfCancellationRequested();
                Console.WriteLine($"Listening for client...");
                var client = await _listener.AcceptTcpClientAsync(cancellationToken);

                _ = Stream(client, cancellationToken);
            }
        }
        catch (SocketException)
        {
            throw;
        }
        finally
        {
            _listener.Stop();
            Console.WriteLine($"Listening on {port} done!");
        }
    }
    private async Task Stream(TcpClient client, CancellationToken cancellationToken = default)
    {
        Console.WriteLine($"Client connected {client.Client.RemoteEndPoint}");
        byte[] recvBuffer = ArrayPool<byte>.Shared.Rent(256);
        var headerLength = sizeof(int);
        try
        {
            var stream = client.GetStream();
            var progress = new ProgressHandler(stream);
            while (!cancellationToken.IsCancellationRequested)
            {
                // read header
                if (!await stream.TryRead(recvBuffer.AsMemory(0, headerLength), cancellationToken)) break;
                var command = BitConverter.ToInt32(recvBuffer.AsSpan(0, headerLength));
                if (_handlers.TryGetValue(command, out var handler))
                {
                    Console.WriteLine($"Received command: {command}");
                    // read mesh
                    ICommandProcessor processor;
                    using (var cts = new CancellationTokenSource(10000))
                    {
                        try
                        {
                            processor = await handler.ReceiveAsync(stream, cts.Token);
                        }
                        catch (OperationCanceledException)
                        {
                            stream.Write((int)Status.Timeout);
                            await stream.WriteMessage($"Failed to complete command [{command}]. Input timed out.");
                            continue;
                        }
                        catch (CommandHandlerException e)
                        {
                            stream.Write((int)Status.FailedRequest);
                            Console.WriteLine($"{e.Message}\nRequest failed [{command}]");
                            await stream.WriteMessage($"Request failed [{command}]. {e.Message}");
                            break;
                        }
                    }
                    string? success = null;
                    try
                    {
                        success = await processor.ResponseAsync(stream, progress);
                    }
                    catch (Exception e)
                    {
                        Console.WriteLine($"Failed [{command}]");
                        stream.Write((int)Status.InternalError);
                        await stream.WriteMessage($"Failed [{command}]. {e.Message}");
                        throw;
                    }
                    if (success != null)
                    {
                        Console.WriteLine($"Process failed [{command}]");
                        stream.Write((int)Status.FailedProcess);
                        await stream.WriteMessage($"Process failed [{command}]. {success}");
                        break;
                    }
                }
                else
                {
                    break;
                }
                await Task.Yield();
            }
        }
        catch (OperationCanceledException) { }
        catch (IndexOutOfRangeException) { }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        finally
        {
            Console.WriteLine($"Client disconnected {client.Client.RemoteEndPoint}");
            client.Dispose();
            ArrayPool<byte>.Shared.Return(recvBuffer);
        }
    }
}