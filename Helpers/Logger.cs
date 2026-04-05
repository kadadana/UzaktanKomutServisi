namespace UzaktanKomutServisi.Helpers;

public class Logger()
{
    private string? _currentLogFile = null;
    private DateTime _latestLogDate = DateTime.MinValue;
    public void logWithMessage(string message)
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
    
}