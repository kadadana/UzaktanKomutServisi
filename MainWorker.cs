using System.Xml;
using System.Text.Json;
using System.Text;
using UzaktanKomutServisi.Models;
using System.Management;
using System.Runtime.InteropServices;

namespace UzaktanKomutServisi;

public class Worker : BackgroundService
{
    private DateTime _lastLogDate = DateTime.MinValue;
    private string? _currentLogFile = null;
    private string? compName;
    readonly HttpClient _httpClient = new HttpClient();
    static string programYolu = AppDomain.CurrentDomain.BaseDirectory.ToString();
    string xmlPath = programYolu + "\\appconfig.xml";
    string getCommandServerUrl;
    string updateCommandServerUrl;

    public Worker()
    {
        compName = GetCompName();
        getCommandServerUrl = ServerFromXml(xmlPath) + "/GetCommand?compName=" + compName;
        updateCommandServerUrl = ServerFromXml(xmlPath) + "/UpdateCommand?isUpdate=true";
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {

            Logger(DateTime.Now + " Servis calisiyor.");
            if (File.Exists(xmlPath))
            {
                await GetCommands(getCommandServerUrl);
            }
            else
            {
                Logger("Sunucu yolu bulunamadi.");
            }
            await Task.Delay(5000, stoppingToken);
        }
    }
    private async Task GetCommands(string serverUrl)
    {
        Logger(serverUrl + " sunucusundan komutlar alinmaya calisiliyor.");

        try
        {
            var response = await _httpClient.GetAsync(serverUrl);

            if (response.IsSuccessStatusCode)
            {
                var jsonKomut = await response.Content.ReadAsStringAsync();
                Logger($"-------------------------------------\n"
                 + jsonKomut +
                "\nKomut sunucudan basariyla alindi." +
                "\n-------------------------------------");

                if (!string.IsNullOrWhiteSpace(jsonKomut) && jsonKomut != "Sirada bekleyen komut yok.")
                {
                    KomutModel? komutModel = JsonSerializer.Deserialize<KomutModel>(jsonKomut);
                    if (komutModel != null && !string.IsNullOrWhiteSpace(komutModel.Command))
                    {
                        string returnOfCmd = WindowsCommands.RunCommand(komutModel.Command, false);
                        komutModel.Response = returnOfCmd;
                        System.Console.WriteLine("KOMUT:\n" + komutModel.Command);
                        System.Console.WriteLine("----------------------------------------");
                        System.Console.WriteLine("OUTPUT:\n" + returnOfCmd);
                        komutModel.DateApplied = DateTime.Now.ToString();
                        komutModel.IsApplied = "TRUE";
                        await UpdateCommand(updateCommandServerUrl, komutModel);
                    }
                    else
                    {
                        Logger("Geçersiz komut veya komut boş.");
                        return;
                    }

                }
                else
                {
                    Logger(jsonKomut);
                    return;
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
            var response = await _httpClient.PostAsync(serverUrl, content);

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

        if (_lastLogDate != today)
        {
            string logDir = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
            Directory.CreateDirectory(logDir);

            _currentLogFile = Path.Combine(logDir, $"log_{today:yyyy-MM-dd}.txt");

            _lastLogDate = today;

        }
        File.AppendAllText(_currentLogFile!, $"{DateTime.Now:HH:mm:ss} {message}{Environment.NewLine}");

    }
    private static string ServerFromXml(string xmlFilePath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlFilePath);

        XmlNode? node = xmlDoc.SelectSingleNode("/config/serverIp");

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
