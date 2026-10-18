using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32;

namespace EmlExtraction.Bench.Sweep;

internal sealed class HardwareProbeException(string message) : Exception(message);

/// <summary>Reads the ADR-015 hardware block. Every field is required; a missing one throws.</summary>
internal static class HardwareProbe
{
    public static HardwareBlock Probe(string repoRoot, bool simulateCpuUnreadable)
    {
        var cpu = (simulateCpuUnreadable ? null : CpuModel())
            ?? throw new HardwareProbeException(
                simulateCpuUnreadable
                    ? "CPU model unreadable (simulated by --simulate-cpu-unreadable)."
                    : "CPU model unreadable from the OS (registry ProcessorNameString, /proc/cpuinfo, or sysctl machdep.cpu.brand_string).");
        var physical = PhysicalCores()
            ?? throw new HardwareProbeException("Physical core count unreadable from the OS.");
        var commit = Normalize(Run("git", $"-C \"{repoRoot}\" rev-parse HEAD"))
            ?? throw new HardwareProbeException("Git commit unreadable (git rev-parse HEAD failed).");

        return new HardwareBlock(
            cpu,
            physical,
            Environment.ProcessorCount,
            GC.GetGCMemoryInfo().TotalAvailableMemoryBytes,
            RuntimeInformation.OSDescription,
            RuntimeInformation.OSArchitecture.ToString(),
            RuntimeInformation.FrameworkDescription,
            TargetFramework(),
            DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ", System.Globalization.CultureInfo.InvariantCulture),
            commit);
    }

    private static string? CpuModel()
    {
        if (OperatingSystem.IsWindows())
        {
            return CpuInfoParser.Normalize(Registry.GetValue(
                @"HKEY_LOCAL_MACHINE\HARDWARE\DESCRIPTION\System\CentralProcessor\0",
                "ProcessorNameString",
                null) as string);
        }

        if (OperatingSystem.IsLinux())
            return File.Exists("/proc/cpuinfo") ? CpuInfoParser.ParseProcCpuinfoModel(File.ReadAllText("/proc/cpuinfo")) : null;
        if (OperatingSystem.IsMacOS())
            return CpuInfoParser.ParseSysctlValue(Run("sysctl", "-n machdep.cpu.brand_string") ?? "");
        return null;
    }

    private static int? PhysicalCores()
    {
        if (OperatingSystem.IsWindows())
            return WindowsPhysicalCores();
        if (OperatingSystem.IsLinux())
            return File.Exists("/proc/cpuinfo") ? CpuInfoParser.ParseProcCpuinfoPhysicalCores(File.ReadAllText("/proc/cpuinfo")) : null;
        if (OperatingSystem.IsMacOS())
            return int.TryParse(Run("sysctl", "-n hw.physicalcpu")?.Trim(), out var n) && n > 0 ? n : null;
        return null;
    }

    private static string TargetFramework()
    {
        // ".NETCoreApp,Version=v10.0" -> "net10.0"
        var name = AppContext.TargetFrameworkName ?? "";
        var v = name.IndexOf("Version=v", StringComparison.Ordinal);
        return v < 0 ? throw new HardwareProbeException("Target framework unreadable.") : "net" + name[(v + 9)..];
    }

    private static string? Normalize(string? s) => string.IsNullOrWhiteSpace(s) ? null : s.Trim();

    private static string? Run(string file, string arguments)
    {
        try
        {
            var psi = new ProcessStartInfo(file, arguments) { RedirectStandardOutput = true };
            using var process = Process.Start(psi);
            if (process is null)
                return null;
            var output = process.StandardOutput.ReadToEnd();
            process.WaitForExit();
            return process.ExitCode == 0 ? output : null;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return null;
        }
    }

    private static int? WindowsPhysicalCores()
    {
        const int relationProcessorCore = 0;
        uint length = 0;
        GetLogicalProcessorInformationEx(relationProcessorCore, IntPtr.Zero, ref length);
        if (length == 0)
            return null;
        var buffer = Marshal.AllocHGlobal((int)length);
        try
        {
            if (!GetLogicalProcessorInformationEx(relationProcessorCore, buffer, ref length))
                return null;
            var count = 0;
            var offset = 0;
            while (offset < length)
            {
                // SYSTEM_LOGICAL_PROCESSOR_INFORMATION_EX: Relationship (4 bytes), then Size (4 bytes).
                var size = Marshal.ReadInt32(buffer, offset + 4);
                if (size <= 0)
                    break;
                count++;
                offset += size;
            }

            return count > 0 ? count : null;
        }
        finally
        {
            Marshal.FreeHGlobal(buffer);
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GetLogicalProcessorInformationEx(int relationshipType, IntPtr buffer, ref uint returnedLength);
}
