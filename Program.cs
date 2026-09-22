using System.Globalization;
using System.Text;

namespace JasoWatch;

internal static class Program
{
    [STAThread]
    private static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        CultureInfo.DefaultThreadCurrentCulture = CultureInfo.GetCultureInfo("ko-KR");
        CultureInfo.DefaultThreadCurrentUICulture = CultureInfo.GetCultureInfo("ko-KR");

        using var instance = new Mutex(true, @"Local\JasoWatch", out var isFirstInstance);
        if (!isFirstInstance)
        {
            using var existingSignal = EventWaitHandle.OpenExisting(@"Local\JasoWatch.ShowAlreadyRunning");
            existingSignal.Set();
            return;
        }

        using var signal = new EventWaitHandle(false, EventResetMode.AutoReset, @"Local\JasoWatch.ShowAlreadyRunning");
        Application.Run(new TrayApplicationContext(args.Contains("--autostart", StringComparer.OrdinalIgnoreCase), signal));
    }
}
