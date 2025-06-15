using System.Xml;
using System.Management;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Text;
using System.CodeDom;
using EnvanterApiProjesi.Models;


namespace UzaktanKomutServisi;

public class Worker : BackgroundService
{
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


                if (!string.IsNullOrWhiteSpace(jsonKomut))
                {
                    KomutModel? komutModel = JsonSerializer.Deserialize<KomutModel>(jsonKomut);
                    if (komutModel != null && !string.IsNullOrWhiteSpace(komutModel.Command))
                    {
                        string returnOfCmd = WindowsCommands.RunCommand(komutModel.Command, false);
                        komutModel.Response = returnOfCmd;
                        komutModel.DateApplied = DateTime.Now.ToString();
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
                    Logger("Komut bulunamadi.");
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
    private static void Logger(string mesaj)
    {
        string dosyaYolu = AppDomain.CurrentDomain.BaseDirectory + "\\Logs";
        if (!Directory.Exists(dosyaYolu))
        {
            Directory.CreateDirectory(dosyaYolu);
        }

        string textYolu = dosyaYolu + "\\Log.txt";

        if (!File.Exists(textYolu))
        {
            using (StreamWriter sw = File.CreateText(textYolu))
            {
                sw.WriteLine(mesaj);
            }
        }
        else
        {
            using (StreamWriter sw = File.AppendText(textYolu))
            {
                sw.WriteLine(mesaj);
            }
        }
    }
    private static string ServerFromXml(string xmlFilePath)
    {
        XmlDocument xmlDoc = new XmlDocument();
        xmlDoc.Load(xmlFilePath);

        XmlNode? node = xmlDoc.SelectSingleNode("/config/serverIp");

        return node?.InnerText.Trim() ?? "Bulunamadi";
    }
    private string? GetCompName()
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
