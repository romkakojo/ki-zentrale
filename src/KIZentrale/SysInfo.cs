using System.Runtime.InteropServices;
using System.Text;
using System.Text.Json.Nodes;

namespace KIZentrale;

/// <summary>Leichte Systemmesswerte: CPU (GetSystemTimes), RAM (GlobalMemoryStatusEx), GPU (NVML, Treiber-DLL).</summary>
public sealed class SysInfo
{
    readonly object gate = new();
    long lastIdle, lastKernel, lastUser;
    double cpu;
    IntPtr gpu = IntPtr.Zero;
    bool nvmlOk;
    string gpuName = "GPU";
    static readonly string CpuName = ReadCpuName();

    public SysInfo()
    {
        Sample();   // Ausgangswerte fuer die CPU-Differenz
        try
        {
            if (nvmlInit_v2() == 0 && nvmlDeviceGetHandleByIndex_v2(0, out gpu) == 0)
            {
                nvmlOk = true;
                var sb = new StringBuilder(96);
                if (nvmlDeviceGetName(gpu, sb, 96) == 0) gpuName = sb.ToString().Replace("NVIDIA GeForce ", "");
            }
        }
        catch { nvmlOk = false; }
    }

    double Sample()
    {
        if (!GetSystemTimes(out var idle, out var kernel, out var user)) return cpu;
        long i = idle - lastIdle, k = kernel - lastKernel, u = user - lastUser;
        lastIdle = idle; lastKernel = kernel; lastUser = user;
        long total = k + u;   // Kernel-Zeit enthaelt die Leerlaufzeit
        if (total > 0) cpu = Math.Clamp((total - i) * 100.0 / total, 0, 100);
        return cpu;
    }

    public JsonObject Read()
    {
        lock (gate)
        {
            var o = new JsonObject
            {
                ["cpu"] = new JsonObject
                {
                    ["pct"] = Math.Round(Sample(), 1),
                    ["threads"] = Environment.ProcessorCount,
                    ["name"] = CpuName,
                },
            };

            var ms = new MEMORYSTATUSEX();
            if (GlobalMemoryStatusEx(ms))
            {
                double total = ms.ullTotalPhys / 1073741824.0, avail = ms.ullAvailPhys / 1073741824.0;
                double cTotal = ms.ullTotalPageFile / 1073741824.0, cUsed = (ms.ullTotalPageFile - ms.ullAvailPageFile) / 1073741824.0;
                o["ram"] = new JsonObject
                {
                    ["used"] = Math.Round(total - avail, 1), ["total"] = Math.Round(total, 1),
                    ["pct"] = Math.Round((total - avail) / total * 100, 1),
                    ["commit"] = Math.Round(cUsed, 1), ["commit_total"] = Math.Round(cTotal, 1),
                };
            }

            if (nvmlOk)
            {
                var g = new JsonObject { ["name"] = gpuName };
                if (nvmlDeviceGetUtilizationRates(gpu, out var ut) == 0) g["pct"] = ut.gpu;
                if (nvmlDeviceGetMemoryInfo(gpu, out var mem) == 0)
                {
                    g["vram_used"] = Math.Round(mem.used / 1073741824.0, 1);
                    g["vram_total"] = Math.Round(mem.total / 1073741824.0, 1);
                    g["vram_pct"] = Math.Round(mem.used * 100.0 / mem.total, 1);
                }
                if (nvmlDeviceGetTemperature(gpu, 0, out var t) == 0) g["temp"] = t;
                if (nvmlDeviceGetPowerUsage(gpu, out var mw) == 0) g["power"] = Math.Round(mw / 1000.0);
                if (nvmlDeviceGetEnforcedPowerLimit(gpu, out var lim) == 0) g["power_limit"] = Math.Round(lim / 1000.0);
                if (nvmlDeviceGetClockInfo(gpu, 0, out var clk) == 0) g["clock"] = clk;
                if (nvmlDeviceGetFanSpeed(gpu, out var fan) == 0) g["fan"] = fan;
                o["gpu"] = g;
            }
            o["time"] = DateTime.Now.ToString("HH:mm:ss");
            return o;
        }
    }

    static string ReadCpuName()
    {
        try
        {
            using var k = Microsoft.Win32.Registry.LocalMachine.OpenSubKey(@"HARDWARE\DESCRIPTION\System\CentralProcessor\0");
            var n = (k?.GetValue("ProcessorNameString") as string ?? "CPU").Trim();
            return n.Replace(" Processor", "").Replace("AMD ", "").Replace("Intel(R) Core(TM) ", "").Trim();
        }
        catch { return "CPU"; }
    }

    // ------------------------------------------------------------ Win32
    [StructLayout(LayoutKind.Sequential)]
    sealed class MEMORYSTATUSEX
    {
        public uint dwLength = (uint)Marshal.SizeOf<MEMORYSTATUSEX>();
        public uint dwMemoryLoad;
        public ulong ullTotalPhys, ullAvailPhys, ullTotalPageFile, ullAvailPageFile, ullTotalVirtual, ullAvailVirtual, ullAvailExtendedVirtual;
    }
    [DllImport("kernel32.dll")] static extern bool GlobalMemoryStatusEx([In, Out] MEMORYSTATUSEX buf);
    [DllImport("kernel32.dll")] static extern bool GetSystemTimes(out long idle, out long kernel, out long user);

    // ------------------------------------------------------------ NVML (nvml.dll vom NVIDIA-Treiber)
    [StructLayout(LayoutKind.Sequential)] struct NvUtil { public uint gpu, memory; }
    [StructLayout(LayoutKind.Sequential)] struct NvMem { public ulong total, free, used; }
    [DllImport("nvml.dll")] static extern int nvmlInit_v2();
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetHandleByIndex_v2(uint index, out IntPtr device);
    [DllImport("nvml.dll", CharSet = CharSet.Ansi)] static extern int nvmlDeviceGetName(IntPtr d, StringBuilder name, uint len);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetUtilizationRates(IntPtr d, out NvUtil u);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetMemoryInfo(IntPtr d, out NvMem m);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetTemperature(IntPtr d, int sensor, out uint t);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetPowerUsage(IntPtr d, out uint milliwatts);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetEnforcedPowerLimit(IntPtr d, out uint milliwatts);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetClockInfo(IntPtr d, int type, out uint mhz);
    [DllImport("nvml.dll")] static extern int nvmlDeviceGetFanSpeed(IntPtr d, out uint pct);
}
