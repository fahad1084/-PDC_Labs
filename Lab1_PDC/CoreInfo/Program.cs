using System.Diagnostics;

Console.WriteLine($"Logical processors available: {Environment.ProcessorCount}");

// Windows: query physical cores, logical processors, and clock speed via PowerShell
var psi = new ProcessStartInfo("powershell",
    "-Command \"Get-CimInstance Win32_Processor | Select-Object Name, NumberOfCores, NumberOfLogicalProcessors, MaxClockSpeed\"")
{
    RedirectStandardOutput = true,
    UseShellExecute = false
};

using var process = Process.Start(psi);
string output = process!.StandardOutput.ReadToEnd();
Console.WriteLine(output);
