using System.Xml;
using System.Text.Json;
using System.Text;
using UzaktanKomutServisi.Models;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR.Client;
using UzaktanKomutServisi.Helpers;
using System.Timers;
namespace UzaktanKomutServisi;

public class Worker : BackgroundService
{
    UpdateWorker _updateWorker;
    System.Timers.Timer updateTimer = new System.Timers.Timer();
    Logger logger = new Logger();
    private HubConnection _connection;
    private readonly string _hubUrl = "http://192.168.1.210:5105/commandHub";
    private readonly string _serverUrl = "http://192.168.1.210:5105/api/command";
    private string? _compName;
    readonly HttpClient _httpClient = new HttpClient();

    public Worker()
    {
        _updateWorker = new UpdateWorker();

        _compName = GetCompName();

        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();
        _connection.On<KomutModel>("ReceiveCommand", async (komut) =>
        {
            logger.logWithMessage($"Yeni komut tetiklendi: {komut.Command}");
            await ProcessCommand(komut);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.logWithMessage("Servis baslatildi.");

        updateTimer.Interval = 1000 * 60 * 60;
        updateTimer.Elapsed += UpdateTimerElapsed;
        updateTimer.Start();

        logger.logWithMessage("Bekleyen komutlar kontrol ediliyor...");
        await GetCommands(_serverUrl);

        try
        {
            await _connection.StartAsync(stoppingToken);
            logger.logWithMessage("SignalR baglantisi basariyla kuruldu.");
            await _connection.InvokeAsync("JoinComputerGroup", _compName, stoppingToken);
        }
        catch (Exception ex)
        {
            logger.logWithMessage($"SignalR baglantisi kurulurken hata: {ex.Message}\n" +
            $"Hub adres: {_hubUrl}");
            System.Console.WriteLine("Hub adresi: " + _hubUrl);
        }


        bool isDisconnectedLogged = false;
        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                if (!isDisconnectedLogged)
                {
                    logger.logWithMessage("Bağlantı koptu, SignalR otomatik yeniden bağlanmayı deneyecek...");
                    isDisconnectedLogged = true;
                }
            }
            else
            {
                isDisconnectedLogged = false;
            }
            await Task.Delay(10000, stoppingToken);
        }
    }
    private void UpdateTimerElapsed(object? sender, ElapsedEventArgs e)
    {
        _ = Task.Run(async () =>
        {
            try
            {
                await _updateWorker.CheckUpdateSilently();
            }
            catch (Exception ex)
            {
                logger.logWithMessage($"Güncelleme kontrolü başarısız: {ex.Message}");
            }
        });
    }

    private async Task ProcessCommand(KomutModel komut)
    {
        try
        {
            string returnOfCmd = WindowsCommands.RunCommand(komut.Command, false);

            Console.WriteLine("KOMUT:\n" + komut.Command);
            Console.WriteLine("----------------------------------------");
            Console.WriteLine("OUTPUT:\n" + returnOfCmd);

            komut.Response = returnOfCmd;
            komut.DateApplied = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
            komut.IsApplied = "TRUE";

            await UpdateCommand(_serverUrl, komut);
        }
        catch (Exception ex)
        {
            logger.logWithMessage($"HATA: {ex.Message}");
            logger.logWithMessage($"Inner Exception: {ex.InnerException?.Message}");
            logger.logWithMessage($"Stack Trace: {ex.StackTrace}");
        }
    }
    private async Task GetCommands(string serverUrl)
    {
        serverUrl = serverUrl + "/" + _compName;
        logger.logWithMessage(serverUrl + " sunucusundan komutlar alinmaya calisiliyor.");

        try
        {
            var response = await _httpClient.GetAsync(serverUrl);

            if (response.IsSuccessStatusCode)
            {
                var jsonKomut = await response.Content.ReadAsStringAsync();
                var options = new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                };

                List<KomutModel>? komutList = JsonSerializer.Deserialize<List<KomutModel>>(jsonKomut, options);

                if (komutList == null || komutList.Count == 0)
                {
                    logger.logWithMessage("Komut listesi bos.");
                    return;
                }

                var komutModel = komutList
                    .FirstOrDefault(k => string.IsNullOrWhiteSpace(k.IsApplied)
                                         || k.IsApplied.ToUpper() != "TRUE");

                if (komutModel == null)
                {
                    logger.logWithMessage("Uygulanacak komut bulunamadi.");
                    return;
                }
                foreach (var k in komutList)
                {
                    string returnOfCmd = WindowsCommands.RunCommand(k.Command, false);
                    k.Response = returnOfCmd;

                    Console.WriteLine("KOMUT:\n" + k.Command);
                    Console.WriteLine("----------------------------------------");
                    Console.WriteLine("OUTPUT:\n" + returnOfCmd);

                    k.DateApplied = DateTime.Now.ToString("dd.MM.yyyy HH:mm:ss");
                    k.IsApplied = "TRUE";

                    await UpdateCommand(_serverUrl, k);
                }

            }
            else
            {
                logger.logWithMessage($"Komut alinamadi. Hata kodu: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            logger.logWithMessage($"HATA: {ex.Message}");
            logger.logWithMessage($"Inner Exception: {ex.InnerException?.Message}");
            logger.logWithMessage($"Stack Trace: {ex.StackTrace}");
        }
    }
    private async Task UpdateCommand(string serverUrl, KomutModel komutModel)
    {

        try
        {
            var jsonKomut = komutModel.ToJson();
            var content = new StringContent(jsonKomut, Encoding.UTF8, "application/json");
            logger.logWithMessage($"-------------------------------------\n"
                + jsonKomut +
                "\nKomut guncellenmeye calisiliyor." +
                "\n-------------------------------------");
            var response = await _httpClient.PutAsync(serverUrl, content);

            if (response.IsSuccessStatusCode)
            {
                logger.logWithMessage("Komut basariyla guncellendi.");
            }
        }
        catch (Exception ex)
        {
            logger.logWithMessage($"HATA: {ex.Message}");
            logger.logWithMessage($"Inner Exception: {ex.InnerException?.Message}");
            logger.logWithMessage($"Stack Trace: {ex.StackTrace}");
        }
    }

    public string? GetCompName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            string? computerName = null;
            try
            {
                ManagementObjectSearcher compSearcher = new ManagementObjectSearcher("SELECT Name FROM Win32_ComputerSystem");
                foreach (ManagementObject obj in compSearcher.Get().Cast<ManagementObject>())
                {
                    computerName = obj["Name"]?.ToString();
                }
                return computerName;
            }
            catch (Exception ex)
            {
                logger.logWithMessage($"Hata: {ex.Message}");
                return "HATA";
            }
        }
        else
        {
            return "OS_ERROR";
        }
    }

}
