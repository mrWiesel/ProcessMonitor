using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Management;
using ProcessMonitor.Models;

namespace ProcessMonitor.Services
{
    public class ProcessService : IDisposable
    {
        private PerformanceCounter? _cpuTotal;
        private PerformanceCounter? _ramAvail;
        private long _totalRamMb;

        // per-process CPU tracking
        private readonly Dictionary<int, (TimeSpan cpu, DateTime ts)> _prev = new();

        public ProcessService()
        {
            try
            {
                _cpuTotal = new PerformanceCounter("Processor", "% Processor Time", "_Total");
                _ramAvail = new PerformanceCounter("Memory", "Available MBytes");
                _cpuTotal.NextValue(); // first read is always 0
            }
            catch { }

            _totalRamMb = GetTotalRamMb();
        }

        // ── System-wide counters ──────────────────────────────────────────
        public float GetSystemCpu()
        {
            try { return _cpuTotal?.NextValue() ?? 0; } catch { return 0; }
        }

        public float GetSystemRamPct()
        {
            try
            {
                float avail = _ramAvail?.NextValue() ?? 0;
                return _totalRamMb > 0 ? (1f - avail / _totalRamMb) * 100f : 0;
            }
            catch { return 0; }
        }

        public float GetAvailableRamMb()
        {
            try { return _ramAvail?.NextValue() ?? 0; } catch { return 0; }
        }

        public long TotalRamMb => _totalRamMb;

        private static long GetTotalRamMb()
        {
            try
            {
                using var mc = new ManagementClass("Win32_ComputerSystem");
                foreach (ManagementObject mo in mc.GetInstances())
                    return Convert.ToInt64(mo["TotalPhysicalMemory"]) / 1024 / 1024;
            }
            catch { }
            return 8192;
        }

        // ── WMI: process description + owner ──────────────────────────────
        /// <summary>
        /// Returns a dict of PID → (Description, Owner) via WMI Win32_Process.
        /// Called once per refresh cycle to enrich ProcessInfo.
        /// </summary>
        public static Dictionary<int, (string desc, string user)> GetWmiProcessDetails()
        {
            var result = new Dictionary<int, (string, string)>();
            try
            {
                using var searcher = new ManagementObjectSearcher(
                    "SELECT ProcessId, Description, ExecutablePath FROM Win32_Process");
                foreach (ManagementObject mo in searcher.Get())
                {
                    int pid   = Convert.ToInt32(mo["ProcessId"]);
                    string desc = mo["Description"]?.ToString() ?? "";
                    // GetOwner via method call
                    string user = "";
                    try
                    {
                        string[] ownerInfo = new string[2];
                        mo.InvokeMethod("GetOwner", ownerInfo);
                        user = string.IsNullOrEmpty(ownerInfo[0]) ? "" : $@"{ownerInfo[1]}\{ownerInfo[0]}";
                    }
                    catch { }
                    result[pid] = (desc, user);
                }
            }
            catch { }
            return result;
        }

        // ── Per-process CPU (delta-based) ─────────────────────────────────
        public float GetProcessCpu(Process p)
        {
            try
            {
                var now = DateTime.UtcNow;
                var cpu = p.TotalProcessorTime;
                if (_prev.TryGetValue(p.Id, out var prev))
                {
                    double elapsed  = (now - prev.ts).TotalSeconds;
                    double cpuDelta = (cpu - prev.cpu).TotalSeconds;
                    if (elapsed > 0)
                    {
                        float pct = (float)(cpuDelta / elapsed / Environment.ProcessorCount * 100f);
                        _prev[p.Id] = (cpu, now);
                        return Math.Clamp(pct, 0f, 100f);
                    }
                }
                _prev[p.Id] = (cpu, now);
            }
            catch { }
            return 0f;
        }

        // ── Enumerate all processes ───────────────────────────────────────
        public List<ProcessInfo> GetAllProcesses()
        {
            // Fetch WMI details once (description + owner)
            var wmi = GetWmiProcessDetails();

            var list = new List<ProcessInfo>();
            foreach (var p in Process.GetProcesses())
            {
                try
                {
                    var info = new ProcessInfo
                    {
                        Pid         = p.Id,
                        Name        = p.ProcessName,
                        CpuUsage    = GetProcessCpu(p),
                        MemoryUsage = p.WorkingSet64 / 1024f / 1024f,
                        ThreadCount = p.Threads.Count,
                        HandleCount = p.HandleCount,
                    };
                    try { info.StartTime = p.StartTime.ToString("MM/dd HH:mm:ss"); } catch { }
                    try { info.Priority  = p.PriorityClass.ToString(); }             catch { info.Priority = "N/A"; }
                    try { info.Status    = p.Responding ? "Running" : "Not Resp.";  } catch { info.Status  = "N/A"; }
                    try { info.FilePath  = p.MainModule?.FileName ?? ""; }            catch { }

                    // Enrich from WMI
                    if (wmi.TryGetValue(p.Id, out var wmiInfo))
                    {
                        info.Description = wmiInfo.desc;
                        info.User        = wmiInfo.user;
                    }

                    list.Add(info);
                }
                catch { }
            }
            return list;
        }

        // ── Process control ───────────────────────────────────────────────
        public (bool ok, string msg) KillProcess(int pid)
        {
            try { Process.GetProcessById(pid).Kill(); return (true, ""); }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public (bool ok, string msg) StartProcess(string path)
        {
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); return (true, ""); }
            catch (Exception ex) { return (false, ex.Message); }
        }

        public void Dispose()
        {
            _cpuTotal?.Dispose();
            _ramAvail?.Dispose();
        }
    }
}
