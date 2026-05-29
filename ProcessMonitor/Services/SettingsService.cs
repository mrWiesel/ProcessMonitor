using System;
using Microsoft.Win32;

namespace ProcessMonitor.Services
{
    /// <summary>
    /// Persists application settings in the Windows Registry under
    /// HKCU\Software\ProcessMonitor
    /// </summary>
    public static class SettingsService
    {
        private const string KEY = @"Software\ProcessMonitor";

        // ── Write ─────────────────────────────────────────────────────────
        public static void Set(string name, object value)
        {
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(KEY, writable: true);
                key?.SetValue(name, value);
            }
            catch { }
        }

        // ── Read ──────────────────────────────────────────────────────────
        public static string GetString(string name, string defaultValue = "")
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KEY);
                return key?.GetValue(name)?.ToString() ?? defaultValue;
            }
            catch { return defaultValue; }
        }

        public static int GetInt(string name, int defaultValue = 0)
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(KEY);
                var raw = key?.GetValue(name);
                if (raw is int i) return i;
                if (int.TryParse(raw?.ToString(), out int v)) return v;
                return defaultValue;
            }
            catch { return defaultValue; }
        }

        public static bool GetBool(string name, bool defaultValue = false)
            => GetInt(name, defaultValue ? 1 : 0) != 0;

        // ── Named settings ────────────────────────────────────────────────
        public static int  RefreshIntervalMs
        {
            get => GetInt("RefreshIntervalMs", 2000);
            set => Set("RefreshIntervalMs", value);
        }

        public static int  PageSize
        {
            get => GetInt("PageSize", 100);
            set => Set("PageSize", value);
        }

        public static string LastSortProp
        {
            get => GetString("LastSortProp", "MemoryUsage");
            set => Set("LastSortProp", value);
        }

        public static bool LastSortAsc
        {
            get => GetBool("LastSortAsc", false);
            set => Set("LastSortAsc", value ? 1 : 0);
        }

        public static float CpuFilterMin
        {
            get
            {
                var s = GetString("CpuFilterMin", "0");
                return float.TryParse(s, out float f) ? f : 0f;
            }
            set => Set("CpuFilterMin", value.ToString("0.##"));
        }

        public static float RamFilterMin
        {
            get
            {
                var s = GetString("RamFilterMin", "0");
                return float.TryParse(s, out float f) ? f : 0f;
            }
            set => Set("RamFilterMin", value.ToString("0.##"));
        }
    }
}
