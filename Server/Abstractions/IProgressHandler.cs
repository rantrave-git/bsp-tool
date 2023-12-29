namespace Bsp.Server.Abstractions;

public interface IProgressHandler
{
    ValueTask OnProgress(int done, int all);
}
