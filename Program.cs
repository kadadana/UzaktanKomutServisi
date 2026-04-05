using System.Diagnostics;
using Microsoft.Extensions.Hosting;
using UzaktanKomutServisi;

if (args.Contains("--install"))
{
    InstallService();
    return;
}
// ---------------------------------

IHost host = Host.CreateDefaultBuilder(args)
    .UseWindowsService()
    .ConfigureServices(services =>
    {
        services.AddHostedService<Worker>();
    })
    .Build();

await host.RunAsync();

static void InstallService()
{
    string exePath = Process.GetCurrentProcess().MainModule!.FileName;
    string serviceName = "UzaktanKomutServisi";

    ProcessStartInfo psi = new ProcessStartInfo
    {
        FileName = "cmd.exe",
        Arguments = $"/c sc create \"{serviceName}\" binPath= \"{exePath}\" start= auto & sc start \"{serviceName}\"",
        Verb = "runas",
        CreateNoWindow = true,
        WindowStyle = ProcessWindowStyle.Hidden
    };

    Process.Start(psi);
    Console.WriteLine("Servis kuruldu ve başlatılıyor...");
}