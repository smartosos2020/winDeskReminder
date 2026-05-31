using System.IO;
using System.IO.Pipes;
using System.Threading;

namespace WinDeskReminder.Services;

public sealed class AppActivationService : IDisposable
{
    private const string MutexName = "WinDeskReminder.SingleInstance";
    private const string PipeName = "WinDeskReminder.ActivationPipe";
    private readonly Mutex _mutex;
    private readonly CancellationTokenSource _shutdown = new();
    private bool _ownsMutex;
    private bool _disposed;

    public AppActivationService()
    {
        _mutex = new Mutex(initiallyOwned: true, MutexName, out _ownsMutex);
    }

    public event Action<string>? CommandReceived;

    public bool IsPrimaryInstance => _ownsMutex;

    public static bool TrySendToPrimary(string[] args)
    {
        if (args.Length == 0)
        {
            return false;
        }

        try
        {
            using var client = new NamedPipeClientStream(".", PipeName, PipeDirection.Out);
            client.Connect(700);
            using var writer = new StreamWriter(client) { AutoFlush = true };
            writer.WriteLine(string.Join(' ', args));
            return true;
        }
        catch
        {
            return false;
        }
    }

    public void Start()
    {
        _ = Task.Run(ListenAsync);
    }

    public void Dispatch(string[] args)
    {
        if (args.Length > 0)
        {
            CommandReceived?.Invoke(string.Join(' ', args));
        }
    }

    public void Dispose()
    {
        if (_disposed)
        {
            return;
        }

        _disposed = true;
        _shutdown.Cancel();
        if (_ownsMutex)
        {
            _mutex.ReleaseMutex();
            _ownsMutex = false;
        }

        _mutex.Dispose();
        _shutdown.Dispose();
    }

    private async Task ListenAsync()
    {
        while (!_shutdown.IsCancellationRequested)
        {
            try
            {
                await using var server = new NamedPipeServerStream(
                    PipeName,
                    PipeDirection.In,
                    maxNumberOfServerInstances: 1,
                    PipeTransmissionMode.Message,
                    PipeOptions.Asynchronous);

                await server.WaitForConnectionAsync(_shutdown.Token);
                using var reader = new StreamReader(server);
                var command = await reader.ReadLineAsync();
                if (!string.IsNullOrWhiteSpace(command))
                {
                    System.Windows.Application.Current.Dispatcher.Invoke(() => CommandReceived?.Invoke(command));
                }
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch
            {
                await Task.Delay(500);
            }
        }
    }
}
