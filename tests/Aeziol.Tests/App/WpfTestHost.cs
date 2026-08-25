using System.Runtime.ExceptionServices;
using System.Windows;
using System.Windows.Threading;

namespace Aeziol.Tests.App;

internal static class WpfTestHost
{
    private static readonly TimeSpan StartupTimeout = TimeSpan.FromSeconds(15);
    private static readonly TimeSpan ActionTimeout = TimeSpan.FromSeconds(15);
    private static readonly ManualResetEventSlim Ready = new(false);
    private static readonly Thread UiThread = StartUiThread();
    private static Dispatcher? _dispatcher;
    private static Exception? _startupFailure;

    static WpfTestHost()
    {
        AppDomain.CurrentDomain.ProcessExit += (_, _) => StopDispatcher();
    }

    public static void Run(Action action)
    {
        ArgumentNullException.ThrowIfNull(action);
        if (!Ready.Wait(StartupTimeout))
        {
            throw new TimeoutException("The WPF test host did not start within 15 seconds.");
        }

        if (_startupFailure is not null)
        {
            ExceptionDispatchInfo.Capture(_startupFailure).Throw();
        }

        var operation = _dispatcher!.InvokeAsync(action, DispatcherPriority.Send);
        var completedTask = Task.WhenAny(operation.Task, Task.Delay(ActionTimeout)).GetAwaiter().GetResult();
        if (!ReferenceEquals(completedTask, operation.Task))
        {
            operation.Abort();
            throw new TimeoutException("The WPF test action did not complete within 15 seconds.");
        }

        operation.Task.GetAwaiter().GetResult();
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
            var application = new Aeziol.App.App(suppressProductStartup: true);
            application.InitializeComponent();
            _dispatcher = Dispatcher.CurrentDispatcher;
            _dispatcher.BeginInvoke(
                () => Ready.Set(),
                DispatcherPriority.ApplicationIdle);
        }
        catch (Exception exception)
        {
            _startupFailure = exception;
            Ready.Set();
            return;
        }

        Dispatcher.Run();
    }

    private static void StopDispatcher()
    {
        var dispatcher = _dispatcher;
        if (dispatcher is null || dispatcher.HasShutdownStarted)
        {
            return;
        }

        dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        UiThread.Join(TimeSpan.FromSeconds(5));
    }
}
