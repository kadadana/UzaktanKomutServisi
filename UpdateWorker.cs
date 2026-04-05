using UzaktanKomutServisi.Helpers;
using System.Diagnostics;
using System.IO.Compression;

public class UpdateWorker
{
    Logger logger = new Logger();
    private static readonly string DownloadUrl = "http://192.168.1.210:9000/deployments/UzaktanKomutServisi/latest/UzaktanKomutServisi.zip";
    private static readonly string VersionUrl = "http://192.168.1.210:9000/deployments/UzaktanKomutServisi/version.txt";
    private static readonly string ServiceName = "UzaktanKomutServisi";
    public async Task CheckUpdateSilently()
    {
        try
        {
            using (HttpClient client = new HttpClient())
            {
                client.DefaultRequestHeaders.Add("User-Agent", "UzaktanKomutServisi-Updater");

                logger.logWithMessage("Versiyon kontrolü yapılıyor...");
                string latestVersionStr = (await client.GetStringAsync(VersionUrl)).Trim();

                Version latest = new Version(latestVersionStr);

                Version current = System.Reflection.Assembly.GetExecutingAssembly().GetName().Version!;

                logger.logWithMessage($"Sunucu Versiyonu: {latest} | Yerel Versiyon: {current}");

                if (latest > current)
                {
                    logger.logWithMessage("Yeni güncelleme bulundu! İşlem başlıyor...");

                    string servicePath = AppDomain.CurrentDomain.BaseDirectory;
                    string zipPath = Path.Combine(servicePath, "update.zip");
                    string extractPath = Path.Combine(servicePath, "temp_update");

                    byte[] zipBytes = await client.GetByteArrayAsync(DownloadUrl);
                    File.WriteAllBytes(zipPath, zipBytes);

                    if (Directory.Exists(extractPath)) Directory.Delete(extractPath, true);
                    ZipFile.ExtractToDirectory(zipPath, extractPath);

                    ApplyZipUpdate(servicePath, zipPath, extractPath);
                }
                else
                {
                    logger.logWithMessage("Yeni güncelleme yok!");
                }
            }
        }
        catch (Exception ex)
        {
            logger.logWithMessage("Güncelleme akışında hata: " + ex.Message);
        }
    }
    private void ApplyZipUpdate(string servicePath, string zipPath, string extractPath)
    {
        string currentExe = Path.Combine(servicePath, "UzaktanKomutServisi.exe");

        string cmdCommands = $"/c \"net stop {ServiceName} & " +
                     $"taskkill /f /im UzaktanKomutServisi.exe & " +
                     $"timeout /t 3 /nobreak & " +
                     $"xcopy /y /s /e \"{extractPath}\\*\" \"{servicePath}\" & " +
                     $"net start {ServiceName}\"";

        ProcessStartInfo psi = new ProcessStartInfo("cmd.exe", cmdCommands)
        {
            CreateNoWindow = true,
            UseShellExecute = true,
            WindowStyle = ProcessWindowStyle.Hidden
        };

        logger.logWithMessage("Servis durduruluyor ve dosyalar güncelleniyor...");
        Process.Start(psi);

        Environment.Exit(0);
    }
}