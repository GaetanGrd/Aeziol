using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Aeziol.Tests.App;

internal static class WpfTestHost
{
    private static readonly ManualResetEventSlim Ready = new(false);
    private static readonly Thread UiThread = StartUiThread();
    private static Dispatcher? _dispatcher;
    private static Exception? _startupFailure;

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        Ready.Wait();

        if (_startupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(_startupFailure).Throw();
        }

        _dispatcher!.Invoke(action);
    }

    private static Thread StartUiThread()
    {
        var thread = new Thread(RunDispatcher)
        {
            IsBackground = true,
            Name = "Aeziol.Tests.WPF",
        };
        thread.SetApartmentState(ApartmentState.STA);
        thread.Start();
        return thread;
    }

    private static void RunDispatcher()
    {
        try
        {
            var application = new Aeziol.App.App();
            application.InitializeComponent();
            _dispatcher = Dispatcher.CurrentDispatcher;
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
        }
        finally
        {
            Ready.Set();
        }

        if (_startupFailure is null)
        {
            Dispatcher.Run();
        }
    }
}
