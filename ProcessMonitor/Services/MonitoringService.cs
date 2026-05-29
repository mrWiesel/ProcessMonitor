using System;
using System.Collections.Generic;
using System.Linq;
using ProcessMonitor.Models;

namespace ProcessMonitor.Services
{
    public class MonitoringService
    {
        private readonly DataService _data;
        // Cooldown: don't re-alert same process+type within 30 s
        private readonly Dictionary<string, DateTime> _cooldown = new();
        private const int CooldownSeconds = 30;

        public event Action<Alert>? AlertRaised;

        public MonitoringService(DataService data)
        {
            _data = data;
        }

        public void Check(List<ProcessInfo> procs)
        {
            foreach (var mp in _data.GetMonitored())
            {
                if (!mp.IsMonitored) continue;

                // Find matching running processes (by name, case-insensitive)
                var matches = procs.Where(p =>
                    p.Name.Equals(mp.ProcessName, StringComparison.OrdinalIgnoreCase)).ToList();

                foreach (var p in matches)
                {
                    CheckThreshold(mp, p, AlertType.HighCPU,    p.CpuUsage,    mp.CpuThreshold);
                    CheckThreshold(mp, p, AlertType.HighMemory, p.MemoryUsage, mp.MemoryThreshold);
                }
            }
        }

        private void CheckThreshold(MonitoredProcess mp, ProcessInfo p,
                                    AlertType type, float value, float threshold)
        {
            if (value <= threshold) return;
            string key = $"{mp.Id}:{type}";
            if (_cooldown.TryGetValue(key, out var last) &&
                (DateTime.Now - last).TotalSeconds < CooldownSeconds) return;

            _cooldown[key] = DateTime.Now;
            var alert = _data.AddAlert(mp.Id, mp.ProcessName, type, value);
            AlertRaised?.Invoke(alert);
        }
    }
}
