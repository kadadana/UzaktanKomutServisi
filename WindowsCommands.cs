using System.Diagnostics;
using System.Text;


namespace UzaktanKomutServisi;

public class WindowsCommands
{
    public static string RunCommand(string? command, bool usePowerShell = false)
    {

        try
        {
            ProcessStartInfo psi = new ProcessStartInfo
            {
                FileName = usePowerShell ? "powershell.exe" : "cmd.exe",
                Arguments = usePowerShell ? $"-NoProfile -ExecutionPolicy Bypass -Command \"{command}\"" : $"/C {command}",
                RedirectStandardOutput = true,
                RedirectStandardError = true,
                StandardOutputEncoding = Encoding.UTF8,
                StandardErrorEncoding = Encoding.UTF8,
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi)!;

            string output = process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();

            process.WaitForExit();

            return string.IsNullOrWhiteSpace(error)
                ? (string.IsNullOrWhiteSpace(output) ? "Program baslatildi." : output)
                : $"HATA: {error}";
        }
        catch (Exception ex)
        {
            return $"HATA: {ex.Message}";
        }
    }

    
}
