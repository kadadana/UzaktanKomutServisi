using System.Diagnostics;
using System.Reflection;

namespace UzaktanKomutServisi;

public static class WindowsCommands
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
                UseShellExecute = false,
                CreateNoWindow = true
            };

            using Process process = Process.Start(psi)!;
            string output = string.IsNullOrWhiteSpace(process.StandardOutput.ReadToEnd()) ? "Program baslatildi." : process.StandardOutput.ReadToEnd();
            string error = process.StandardError.ReadToEnd();
            process.WaitForExit();

            return string.IsNullOrWhiteSpace(error) ? output : $"HATA: {error}";
        }
        catch (Exception ex)
        {
            return $"HATA: {ex.Message}";
        }
    }
}