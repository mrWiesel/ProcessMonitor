using System;
using System.Collections.Generic;
using System.Text.Json.Serialization;

namespace ProcessMonitor.Models
{
    // ── Entity 1: ProcessInfo ─────────────────────────────────────────────
    public class ProcessInfo
    {
        public int    Pid         { get; set; }
        public string Name        { get; set; } = "";
        public float  CpuUsage    { get; set; }
        public float  MemoryUsage { get; set; }   // MB
        public int    ThreadCount { get; set; }
        public string StartTime   { get; set; } = "";
        public string FilePath    { get; set; } = "";
        public string Description { get; set; } = "";
        public int    HandleCount { get; set; }
        public string Priority    { get; set; } = "";
        public string Status      { get; set; } = "";
        public string User        { get; set; } = "";
    }

    // ── Entity 2: MonitoredProcess ────────────────────────────────────────
    public class MonitoredProcess
    {
        public int    Id              { get; set; }
        public string ProcessName     { get; set; } = "";
        public bool   IsMonitored     { get; set; } = true;
        public float  CpuThreshold    { get; set; } = 80f;
        public float  MemoryThreshold { get; set; } = 500f;
        public string CreatedAt       { get; set; } = DateTime.Now.ToString("o");

        [JsonIgnore]
        public bool IsValid =>
            !string.IsNullOrWhiteSpace(ProcessName) &&
            CpuThreshold    is >= 1f and <= 100f &&
            MemoryThreshold is >= 1f and <= 65536f;
    }

    // ── Entity 3: Alert ───────────────────────────────────────────────────
    public enum AlertType { HighCPU, HighMemory }

    public class Alert
    {
        public int       Id             { get; set; }
        public int       ProcessId      { get; set; }
        public string    ProcessName    { get; set; } = "";
        public AlertType AlertType      { get; set; }
        public float     Value          { get; set; }
        public string    Timestamp      { get; set; } = DateTime.Now.ToString("o");
        public bool      IsAcknowledged { get; set; }
    }

    // ── Entity 4: ProcessSnapshot ─────────────────────────────────────────
    public class ProcessSnapshot
    {
        public int    Id           { get; set; }
        public string Timestamp    { get; set; } = DateTime.Now.ToString("o");
        public string Label        { get; set; } = "";
        public float  CpuUsage     { get; set; }
        public float  MemoryUsage  { get; set; }
        public int    ProcessCount { get; set; }
        public List<ProcessInfo> ProcessesData { get; set; } = new();
    }
}
