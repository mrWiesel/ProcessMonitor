using System;
using System.Windows;
using ProcessMonitor.Models;

namespace ProcessMonitor.Views
{
    public partial class MonitoredProcessWindow : Window
    {
        private readonly MonitoredProcess? _existing;
        public  MonitoredProcess? Result { get; private set; }

        public MonitoredProcessWindow(MonitoredProcess? existing = null, long totalRamMb = 65536)
        {
            InitializeComponent();
            _existing = existing;
            TbMemHint.Text = $"  1 – {totalRamMb}";

            if (existing != null)
            {
                Title = "Edit Monitored Process";
                TxtName.Text     = existing.ProcessName;
                TxtName.IsEnabled = false;
                TxtCpu.Text      = existing.CpuThreshold.ToString("0");
                TxtMem.Text      = existing.MemoryThreshold.ToString("0");
                ChkEnabled.IsChecked = existing.IsMonitored;
            }
        }

        public void PresetName(string name)
        {
            TxtName.Text = name;
            TxtName.IsEnabled = false;
        }

        private void BtnSave_Click(object sender, RoutedEventArgs e)
        {
            TbError.Text = "";
            string name = TxtName.Text.Trim();
            if (string.IsNullOrEmpty(name))
            { TbError.Text = "Process name cannot be empty."; return; }

            if (!float.TryParse(TxtCpu.Text, out float cpu) || cpu < 1 || cpu > 100)
            { TbError.Text = "CPU threshold must be 1 – 100 %."; return; }

            if (!float.TryParse(TxtMem.Text, out float mem) || mem < 1 || mem > 65536)
            { TbError.Text = "Memory threshold must be 1 – 65536 MB."; return; }

            Result = new MonitoredProcess
            {
                Id              = _existing?.Id ?? 0,
                ProcessName     = name,
                CpuThreshold    = cpu,
                MemoryThreshold = mem,
                IsMonitored     = ChkEnabled.IsChecked == true,
                CreatedAt       = _existing?.CreatedAt ?? DateTime.Now.ToString("o")
            };
            DialogResult = true;
        }

        private void BtnCancel_Click(object sender, RoutedEventArgs e)
        { DialogResult = false; }
    }
}
