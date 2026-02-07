using System.Xml;
using System.Text.Json;
using System.Text;
using UzaktanKomutServisi.Models;
using System.Management;
using System.Runtime.InteropServices;
using Microsoft.AspNetCore.SignalR.Client;

namespace UzaktanKomutServisi;

public class Worker : BackgroundService
{
    private string? _currentLogFile = null;
    private DateTime _latestLogDate = DateTime.MinValue;

    private HubConnection _connection;
    private readonly string _hubUrl;
    private string? _compName;
    readonly HttpClient _httpClient = new HttpClient();
    string xmlPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "appconfig.xml");

    public Worker()
    {
        _compName = GetCompName();
        _hubUrl = HubServerFromXml(xmlPath) + "/komutHub";
        _connection = new HubConnectionBuilder()
            .WithUrl(_hubUrl)
            .WithAutomaticReconnect()
            .Build();
        _connection.On<KomutModel>("ReceiveCommand", async (komut) =>
        {
            Logger($"Yeni komut tetiklendi: {komut.Command}");
            await ProcessCommand(komut);
        });
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger("Servis baslatildi, bekleyen komutlar kontrol ediliyor...");
        await GetCommands(ServerFromXml(xmlPath));

        try
        {
            await _connection.StartAsync(stoppingToken);
            Logger("SignalR baglantisi basariyla kuruldu.");
            await _connection.InvokeAsync("JoinComputerGroup", _compName, stoppingToken);
        }
        catch (Exception ex)
        {
            Logger($"SignalR baglantisi kurulurken hata: {ex.Message}\n" +
            $"Hub adres: {_hubUrl}");
            System.Console.WriteLine("Hub adresi: " + _hubUrl);
        }


        while (!stoppingToken.IsCancellationRequested)
        {
            if (_connection.State == HubConnectionState.Disconnected)
            {
                Logger("Bağlantı koptu, yeniden deneniyor...");
            }

            await Task.Delay(10000, stoppingToken);
        }
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

            await UpdateCommand(ServerFromXml(xmlPath), komut);
        }
        catch (Exception ex)
        {
            Logger($"HATA: {ex.Message}");
            Logger($"Inner Exception: {ex.InnerException?.Message}");
            Logger($"Stack Trace: {ex.StackTrace}");
        }
    }
    private async Task GetCommands(string serverUrl)
    {
        serverUrl = serverUrl + "/" + _compName;
        Logger(serverUrl + " sunucusundan komutlar alinmaya calisiliyor.");

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
                    Logger("Komut listesi bos.");
                    return;
                }

                var komutModel = komutList
                    .FirstOrDefault(k => string.IsNullOrWhiteSpace(k.IsApplied)
                                         || k.IsApplied.ToUpper() != "TRUE");

                if (komutModel == null)
                {
                    Logger("Uygulanacak komut bulunamadi.");
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

                    await UpdateCommand(ServerFromXml(xmlPath), k);
                }

            }
            else
            {
                Logger($"Komut alinamadi. Hata kodu: {response.StatusCode}");
            }
        }
        catch (Exception ex)
        {
            Logger($"HATA: {ex.Message}");
            Logger($"Inner Exception: {ex.InnerException?.Message}");
            Logger($"Stack Trace: {ex.StackTrace}");
        }
    }
    private async Task UpdateCommand(string serverUrl, KomutModel komutModel)
    {

        try
        {
            var jsonKomut = komutModel.ToJson();
            var content = new StringContent(jsonKomut, Encoding.UTF8, "application/json");
            Logger($"-------------------------------------\n"
                + jsonKomut +
                "\nKomut guncellenmeye calisiliyor." +
                "\n-------------------------------------");
            var response = await _httpClient.PutAsync(serverUrl, content);

            if (response.IsSuccessStatusCode)
            {
                Logger("Komut basariyla guncellendi.");
            }
        }
        catch (Exception ex)
        {
            Logger($"HATA: {ex.Message}");
            Logger($"Inner Exception: {ex.InnerException?.Message}");
            Logger($"Stack Trace: {ex.StackTrace}");
        }
    }
    public void Logger(string message)
    {
        var today = DateTime.Now.Date;

        if (_latestLogDate != today)
        {
            string logDir = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            Directory.CreateDirectory(logDir);

            _currentLogFile = Path.Combine(logDir, $"log_{today:yyyy-MM-dd}.txt");

            _latestLogDate = today;

        }
        File.AppendAllText(_currentLogFile!, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");

    }
    private static string ServerFromXml(string xmlFilePath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlFilePath);

        XmlNode? node = xmlDoc.SelectSingleNode("/config/serverUrl");

        return node?.InnerText.Trim() ?? "Bulunamadi";
    }
    private static string HubServerFromXml(string xmlFilePath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlFilePath);

        XmlNode? node = xmlDoc.SelectSingleNode("/config/hubUrl");

        return node?.InnerText.Trim() ?? "Bulunamadi";
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
                Logger($"Hata: {ex.Message}");
                return "HATA";
            }
        }
        else
        {
            return "OS_ERROR";
        }
    }

}
