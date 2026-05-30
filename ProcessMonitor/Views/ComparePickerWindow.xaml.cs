using System.Windows;
using ProcessMonitor.Models;

namespace ProcessMonitor.Views
{
    public partial class ComparePickerWindow : Window
    {
        private readonly List<ProcessSnapshot> _snaps;
        public ProcessSnapshot? SnapshotA { get; private set; }
        public ProcessSnapshot? SnapshotB { get; private set; }

        public ComparePickerWindow(List<ProcessSnapshot> snaps)
        {
            InitializeComponent();
            _snaps = snaps;

            var labels = snaps.Select(s =>
                $"[{s.Id}] {s.Label} ({(DateTime.TryParse(s.Timestamp, out var d) ? d.ToString("MM/dd HH:mm") : s.Timestamp)})")
                .ToArray();

            CmbA.ItemsSource = labels; CmbA.SelectedIndex = 0;
            CmbB.ItemsSource = labels; CmbB.SelectedIndex = Math.Min(1, labels.Length - 1);
        }

        private void BtnCompare_Click(object sender, RoutedEventArgs e)
        {
            SnapshotA = _snaps[CmbA.SelectedIndex];
            SnapshotB = _snaps[CmbB.SelectedIndex];
            DialogResult = true;
        }
    }
}
