using System.IO;
using System.Text.Json;
using ProcessMonitor.Models;

namespace ProcessMonitor.Services
{
    public class DataService
    {
        private static readonly string DataDir = Path.Combine(
            AppDomain.CurrentDomain.BaseDirectory, "Data");

        private readonly JsonRepository<MonitoredProcess> _monRepo;
        private readonly JsonRepository<Alert>            _alertRepo;
        private readonly JsonRepository<ProcessSnapshot>  _snapRepo;

        private List<MonitoredProcess> _monitored;
        private List<Alert>            _alerts;
        private List<ProcessSnapshot>  _snapshots;

        public DataService()
        {
            _monRepo   = new JsonRepository<MonitoredProcess>(Path.Combine(DataDir, "monitored.json"));
            _alertRepo = new JsonRepository<Alert>           (Path.Combine(DataDir, "alerts.json"));
            _snapRepo  = new JsonRepository<ProcessSnapshot> (Path.Combine(DataDir, "snapshots.json"));

            _monitored = _monRepo.LoadAll();
            _alerts    = _alertRepo.LoadAll();
            _snapshots = _snapRepo.LoadAll();
        }

        // ── MonitoredProcess CRUD ─────────────────────────────────────────
        public IReadOnlyList<MonitoredProcess> GetMonitored() => _monitored;

        public (bool ok, string error) AddMonitored(MonitoredProcess mp)
        {
            // Validation
            if (string.IsNullOrWhiteSpace(mp.ProcessName))
                return (false, "Process name cannot be empty.");
            if (mp.CpuThreshold is < 1f or > 100f)
                return (false, "CPU threshold must be 1–100 %.");
            if (mp.MemoryThreshold is < 1f or > 65536f)
                return (false, "Memory threshold must be 1–65536 MB.");
            if (_monitored.Any(m => m.ProcessName.Equals(mp.ProcessName, StringComparison.OrdinalIgnoreCase)))
                return (false, $"'{mp.ProcessName}' is already monitored.");

            mp.Id        = NextId(_monitored);
            mp.CreatedAt = DateTime.Now.ToString("o");
            _monitored.Add(mp);
            _monRepo.SaveAll(_monitored);
            return (true, "");
        }

        public (bool ok, string error) UpdateMonitored(MonitoredProcess mp)
        {
            var idx = _monitored.FindIndex(m => m.Id == mp.Id);
            if (idx < 0) return (false, "Monitored process not found.");
            if (mp.CpuThreshold is < 1f or > 100f)
                return (false, "CPU threshold must be 1–100 %.");
            if (mp.MemoryThreshold is < 1f or > 65536f)
                return (false, "Memory threshold must be 1–65536 MB.");
            _monitored[idx] = mp;
            _monRepo.SaveAll(_monitored);
            return (true, "");
        }

        public bool DeleteMonitored(int id)
        {
            var mp = _monitored.FirstOrDefault(m => m.Id == id);
            if (mp == null) return false;
            _monitored.Remove(mp);
            _monRepo.SaveAll(_monitored);
            return true;
        }

        // ── Alert CRUD ────────────────────────────────────────────────────
        public IReadOnlyList<Alert> GetAlerts() => _alerts;

        public Alert AddAlert(int monitoredId, string processName, AlertType type, float value)
        {
            var a = new Alert
            {
                Id          = NextId(_alerts),
                ProcessId   = monitoredId,
                ProcessName = processName,
                AlertType   = type,
                Value       = value,
                Timestamp   = DateTime.Now.ToString("o"),
                IsAcknowledged = false
            };
            _alerts.Add(a);
            // Keep max 500 alerts
            if (_alerts.Count > 500)
                _alerts = _alerts.Skip(_alerts.Count - 500).ToList();
            _alertRepo.SaveAll(_alerts);
            return a;
        }

        public bool AcknowledgeAlert(int id)
        {
            var a = _alerts.FirstOrDefault(x => x.Id == id);
            if (a == null) return false;
            a.IsAcknowledged = true;
            _alertRepo.SaveAll(_alerts);
            return true;
        }

        public bool DeleteAlert(int id)
        {
            var a = _alerts.FirstOrDefault(x => x.Id == id);
            if (a == null) return false;
            _alerts.Remove(a);
            _alertRepo.SaveAll(_alerts);
            return true;
        }

        public void AcknowledgeAll()
        {
            foreach (var a in _alerts) a.IsAcknowledged = true;
            _alertRepo.SaveAll(_alerts);
        }

        // ── ProcessSnapshot CRUD ──────────────────────────────────────────
        public IReadOnlyList<ProcessSnapshot> GetSnapshots() => _snapshots;

        public ProcessSnapshot TakeSnapshot(
            string label, float cpu, float ram, List<ProcessInfo> procs)
        {
            var snap = new ProcessSnapshot
            {
                Id            = NextId(_snapshots),
                Timestamp     = DateTime.Now.ToString("o"),
                Label         = label,
                CpuUsage      = cpu,
                MemoryUsage   = ram,
                ProcessCount  = procs.Count,
                ProcessesData = procs
            };
            _snapshots.Add(snap);
            _snapRepo.SaveAll(_snapshots);
            return snap;
        }

        public bool DeleteSnapshot(int id)
        {
            var s = _snapshots.FirstOrDefault(x => x.Id == id);
            if (s == null) return false;
            _snapshots.Remove(s);
            _snapRepo.SaveAll(_snapshots);
            return true;
        }

        public string ExportSnapshot(int id)
        {
            var s = _snapshots.FirstOrDefault(x => x.Id == id);
            if (s == null) return "";
            var opts = new JsonSerializerOptions
            {
                WriteIndented = true,
                Converters = { new System.Text.Json.Serialization.JsonStringEnumConverter() }
            };
            return JsonSerializer.Serialize(s, opts);
        }

        // ── Helper ────────────────────────────────────────────────────────
        private static int NextId<T>(List<T> list) where T : class
        {
            // Use reflection to find Id property
            if (list.Count == 0) return 1;
            var prop = typeof(T).GetProperty("Id");
            if (prop == null) return 1;
            return list.Max(x => (int)(prop.GetValue(x) ?? 0)) + 1;
        }
    }
}
