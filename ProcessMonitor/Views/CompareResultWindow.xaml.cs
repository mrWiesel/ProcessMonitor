using System.Text;
using System.Windows;
using ProcessMonitor.Models;

namespace ProcessMonitor.Views
{
    public partial class CompareResultWindow : Window
    {
        public CompareResultWindow(ProcessSnapshot s1, ProcessSnapshot s2)
        {
            InitializeComponent();
            Title = $"Comparison: #{s1.Id} vs #{s2.Id}";

            var sb = new StringBuilder();
            sb.AppendLine($"  Snapshot A: [{s1.Id}] {s1.Label}");
            sb.AppendLine($"  Snapshot B: [{s2.Id}] {s2.Label}\n");
            sb.AppendLine($"  {"Metric",-22} {"A",12} {"B",12} {"Delta",12}");
            sb.AppendLine($"  {new string('─', 60)}");

            void Diff(string lbl, float a, float b, string fmt)
            {
                float d = b - a;
                sb.AppendLine($"  {lbl,-22} {a.ToString(fmt),12} {b.ToString(fmt),12} {(d >= 0 ? "+" : "")}{d.ToString(fmt),11}");
            }
            Diff("CPU %",     s1.CpuUsage,    s2.CpuUsage,    "0.0");
            Diff("RAM %",     s1.MemoryUsage, s2.MemoryUsage, "0.0");
            Diff("Processes", s1.ProcessCount, s2.ProcessCount, "0");

            var inB = s2.ProcessesData.Select(p => p.Name)
                .Except(s1.ProcessesData.Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n).ToList();
            var inA = s1.ProcessesData.Select(p => p.Name)
                .Except(s2.ProcessesData.Select(p => p.Name), StringComparer.OrdinalIgnoreCase)
                .OrderBy(n => n).ToList();

            sb.AppendLine($"\n  ➕ New in B ({inB.Count}):");
            foreach (var n in inB) sb.AppendLine($"    + {n}");
            sb.AppendLine($"\n  ➖ Gone from A ({inA.Count}):");
            foreach (var n in inA) sb.AppendLine($"    - {n}");

            TbResult.Text = sb.ToString();
        }
    }
}
