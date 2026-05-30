using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using ProcessMonitor.Models;
using ProcessMonitor.Services;

namespace ProcessMonitor.Views
{
    public partial class MainWindow : Window
    {
        // ── DWM dark title bar ─────────────────────────────────────────────
        [DllImport("dwmapi.dll")]
        private static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int v, int sz);
        private const int DWMWA_USE_IMMERSIVE_DARK_MODE = 20;

        // ── Palette (WPF colours) ──────────────────────────────────────────
        static readonly Color C_BG      = Color.FromRgb(13,  17,  23);
        static readonly Color C_Panel   = Color.FromRgb(22,  27,  34);
        static readonly Color C_Accent  = Color.FromRgb(0,  200, 150);
        static readonly Color C_Accent2 = Color.FromRgb(88, 166, 255);
        static readonly Color C_Accent3 = Color.FromRgb(255,121,  77);
        static readonly Color C_Border  = Color.FromRgb(48,  54,  61);
        static readonly Color C_Dim     = Color.FromRgb(110,118, 129);

        // ── Services ──────────────────────────────────────────────────────
        private readonly ProcessService    _procSvc;
        private readonly DataService       _dataSvc;
        private readonly MonitoringService _monSvc;

        // ── Background refresh ─────────────────────────────────────────────
        private readonly CancellationTokenSource _cts = new();
        private volatile int  _refreshIntervalMs = 2000;
        private volatile bool _refreshBusy       = false;

        // ── Graph history ──────────────────────────────────────────────────
        private readonly List<float> _cpuHist = new();
        private readonly List<float> _ramHist = new();
        private const int HIST = 60;
        private float _lastCpu, _lastRam;
        private readonly DispatcherTimer _graphTimer;

        // ── Process list state ─────────────────────────────────────────────
        private List<ProcessInfo> _allProcs = new();
        private string _filter    = "";
        private float  _cpuMin    = 0f;
        private float  _ramMin    = 0f;
        private int    _page      = 0;
        private int    _pageSize  = 100;
        private string _sortProp  = nameof(ProcessInfo.MemoryUsage);
        private bool   _sortAsc   = false;

        // ── Alert badge ────────────────────────────────────────────────────
        private int _unackedAlerts = 0;

        // ── Toast timer ────────────────────────────────────────────────────
        private DispatcherTimer? _toastTimer;

        // ── Init guard — prevents handlers firing before controls exist ───
        private bool _isLoaded = false;

        // ══════════════════════════════════════════════════════════════════
        public MainWindow()
        {
            InitializeComponent();
            DataContext = this;

            _procSvc = new ProcessService();
            _dataSvc = new DataService();
            _monSvc  = new MonitoringService(_dataSvc);
            _monSvc.AlertRaised += OnAlertRaised;

            _graphTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(1) };
            _graphTimer.Tick += GraphTick;

            Loaded += (s, e) =>
        {
            _isLoaded = true;

            // ── Restore settings from Windows Registry ─────────────────
            _refreshIntervalMs = SettingsService.RefreshIntervalMs;
            _pageSize          = SettingsService.PageSize;
            _sortProp          = SettingsService.LastSortProp;
            _sortAsc           = SettingsService.LastSortAsc;
            _cpuMin            = SettingsService.CpuFilterMin;
            _ramMin            = SettingsService.RamFilterMin;

            // Sync RadioButtons to loaded values
            SyncPageSizeRadios(_pageSize);

            // Sync filter text boxes
            if (TbCpuMin != null) TbCpuMin.Text = _cpuMin > 0 ? _cpuMin.ToString("0.##") : "0";
            if (TbRamMin != null) TbRamMin.Text = _ramMin > 0 ? _ramMin.ToString("0.##") : "0";

            ApplyDarkTitleBar();
            _graphTimer.Start();
            _ = RefreshLoopAsync(_cts.Token);
        };
            Closed  += (s, e) => { _cts.Cancel(); _graphTimer.Stop(); _procSvc.Dispose(); };
            SizeChanged += (s, e) => RedrawGraph();
        }

        private void ApplyDarkTitleBar()
        {
            var helper = new System.Windows.Interop.WindowInteropHelper(this);
            int v = 1;
            DwmSetWindowAttribute(helper.Handle, DWMWA_USE_IMMERSIVE_DARK_MODE, ref v, sizeof(int));
        }

        // ══════════════════════════════════════════════════════════════════
        //  BACKGROUND REFRESH LOOP
        // ══════════════════════════════════════════════════════════════════
        private async Task RefreshLoopAsync(CancellationToken ct)
        {
            while (!ct.IsCancellationRequested)
            {
                if (!_refreshBusy)
                {
                    _refreshBusy = true;
                    try
                    {
                        var procs = await Task.Run(() => _procSvc.GetAllProcesses(), ct);
                        await Task.Run(() => _monSvc.Check(procs), ct);
                        if (!ct.IsCancellationRequested)
                            Dispatcher.Invoke(() => ApplyNewProcessList(procs));
                    }
                    catch (OperationCanceledException) { return; }
                    catch { /* swallow transient errors */ }
                    finally { _refreshBusy = false; }
                }
                try { await Task.Delay(_refreshIntervalMs, ct); }
                catch (OperationCanceledException) { return; }
            }
        }

        private void ApplyNewProcessList(List<ProcessInfo> procs)
        {
            _allProcs = procs;
            TbProcsVal.Text = procs.Count.ToString();
            ApplyFilter();
        }

        // ══════════════════════════════════════════════════════════════════
        //  GRAPH
        // ══════════════════════════════════════════════════════════════════
        private void GraphTick(object? sender, EventArgs e)
        {
            _lastCpu = _procSvc.GetSystemCpu();
            _lastRam = _procSvc.GetSystemRamPct();
            if (_cpuHist.Count >= HIST) _cpuHist.RemoveAt(0);
            if (_ramHist.Count >= HIST) _ramHist.RemoveAt(0);
            _cpuHist.Add(_lastCpu);
            _ramHist.Add(_lastRam);
            TbCpuVal.Text = $"{_lastCpu:0.0}%";
            TbRamVal.Text = $"{_lastRam:0.0}%";
            RedrawGraph();
        }

        private void RedrawGraph()
        {
            var canvas = GraphCanvas;
            canvas.Children.Clear();
            double w = canvas.ActualWidth;
            double h = canvas.ActualHeight;
            if (w < 10 || h < 10) return;

            const double pad = 12;
            double ih = h - pad * 2;

            // Grid lines
            foreach (int pct in new[] { 25, 50, 75 })
            {
                double y = pad + ih * (1.0 - pct / 100.0);
                var line = new Line
                {
                    X1 = pad, Y1 = y, X2 = w - pad, Y2 = y,
                    Stroke = new SolidColorBrush(C_Border),
                    StrokeDashArray = new DoubleCollection { 4, 4 },
                    StrokeThickness = 1
                };
                canvas.Children.Add(line);
                var lbl = new TextBlock
                {
                    Text = $"{pct}%", FontSize = 9,
                    Foreground = new SolidColorBrush(C_Border)
                };
                Canvas.SetLeft(lbl, w - 30);
                Canvas.SetTop(lbl, y - 10);
                canvas.Children.Add(lbl);
            }

            DrawGraphLine(canvas, _cpuHist, w, h, pad, ih, C_Accent,  filled: true);
            DrawGraphLine(canvas, _ramHist, w, h, pad, ih, C_Accent2, filled: false);

            // Legend
            AddLegendLabel(canvas, "CPU %", C_Accent,  8, pad);
            AddLegendLabel(canvas, "RAM %", C_Accent2, 58, pad);
            var histLbl = new TextBlock
            {
                Text = "← 60 s history", FontSize = 10,
                Foreground = new SolidColorBrush(C_Dim)
            };
            Canvas.SetLeft(histLbl, w - 108);
            Canvas.SetTop(histLbl,  h - 16);
            canvas.Children.Add(histLbl);
        }

        private void DrawGraphLine(Canvas canvas, List<float> data,
            double w, double h, double pad, double ih, Color color, bool filled)
        {
            if (data.Count < 2) return;
            double step = (w - pad * 2) / (HIST - 1);
            var pts = new Point[data.Count];
            for (int i = 0; i < data.Count; i++)
                pts[i] = new Point(
                    pad + (i + HIST - data.Count) * step,
                    pad + ih * (1.0 - Math.Min(data[i], 100f) / 100.0));

            if (filled)
            {
                var poly = new Polygon();
                foreach (var p in pts) poly.Points.Add(p);
                poly.Points.Add(new Point(pts[^1].X, pad + ih));
                poly.Points.Add(new Point(pts[0].X,  pad + ih));
                poly.Fill = new LinearGradientBrush(
                    Color.FromArgb(55, color.R, color.G, color.B),
                    Colors.Transparent,
                    new Point(0, 0), new Point(0, 1));
                poly.Stroke = Brushes.Transparent;
                canvas.Children.Add(poly);
            }

            var pl = new Polyline
            {
                Stroke = new SolidColorBrush(color),
                StrokeThickness = 2,
                StrokeLineJoin = PenLineJoin.Round
            };
            foreach (var p in pts) pl.Points.Add(p);
            canvas.Children.Add(pl);

            // Endpoint dot
            var last = pts[^1];
            var dot = new Ellipse
            {
                Width = 8, Height = 8,
                Fill = new SolidColorBrush(color),
                Stroke = new SolidColorBrush(C_BG),
                StrokeThickness = 2
            };
            Canvas.SetLeft(dot, last.X - 4);
            Canvas.SetTop(dot,  last.Y - 4);
            canvas.Children.Add(dot);
        }

        private static void AddLegendLabel(Canvas canvas, string text, Color color, double left, double top)
        {
            var tb = new TextBlock
            {
                Text = text, FontSize = 11, FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(color)
            };
            Canvas.SetLeft(tb, left);
            Canvas.SetTop(tb, top);
            canvas.Children.Add(tb);
        }

        // ══════════════════════════════════════════════════════════════════
        //  TOP BAR CONTROLS
        // ══════════════════════════════════════════════════════════════════
        // ══════════════════════════════════════════════════════════════════
        //  TOP BAR — REFRESH INTERVAL RADIO BUTTONS
        // ══════════════════════════════════════════════════════════════════
        private void RbInterval_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is RadioButton rb && int.TryParse(rb.Tag?.ToString(), out int ms) && ms > 0)
            {
                _refreshIntervalMs = ms;
                SettingsService.RefreshIntervalMs = ms;
            }
        }



        // ══════════════════════════════════════════════════════════════════
        //  TAB SELECTION
        // ══════════════════════════════════════════════════════════════════
        private void MainTabs_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (!_isLoaded) return;
            if (e.Source != MainTabs) return;
            if (MainTabs.SelectedItem == TabAlerts)     RefreshAlertsTab();
            if (MainTabs.SelectedItem == TabMonitoring) RefreshMonitorList();
            if (MainTabs.SelectedItem == TabSnapshots)  RefreshSnapshots();
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB 1 — PROCESSES
        // ══════════════════════════════════════════════════════════════════
        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            _filter = SearchBox.Text.Trim();
            _page = 0;
            if (SearchPlaceholder != null)
                SearchPlaceholder.Visibility = string.IsNullOrEmpty(SearchBox.Text)
                    ? Visibility.Visible : Visibility.Collapsed;
            ApplyFilter();
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB 1 TOOLBAR — PAGE SIZE RADIO BUTTONS & FILTERS
        // ══════════════════════════════════════════════════════════════════
        private void RbPageSize_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isLoaded) return;
            if (sender is RadioButton rb && int.TryParse(rb.Tag?.ToString(), out int ps) && ps > 0)
            {
                _pageSize = ps;
                _page     = 0;
                SettingsService.PageSize = ps;
                ApplyFilter();
            }
        }

        private void SyncPageSizeRadios(int ps)
        {
            if (Rp50 == null) return;
            Rp50.IsChecked  = (ps == 50);
            Rp100.IsChecked = (ps == 100);
            Rp200.IsChecked = (ps == 200);
            if (!Rp50.IsChecked!.Value && !Rp100.IsChecked!.Value && !Rp200.IsChecked!.Value)
                Rp100.IsChecked = true;
        }

        private void FilterNumeric_Changed(object sender, TextChangedEventArgs e)
        {
            if (!_isLoaded) return;
            float.TryParse(TbCpuMin?.Text ?? "0", out _cpuMin);
            float.TryParse(TbRamMin?.Text ?? "0", out _ramMin);
            SettingsService.CpuFilterMin = _cpuMin;
            SettingsService.RamFilterMin = _ramMin;
            _page = 0;
            ApplyFilter();
        }

        private void ProcListHeader_Click(object sender, RoutedEventArgs e)
        {
            if (e.OriginalSource is GridViewColumnHeader hdr)
            {
                string? prop = hdr.Column?.Header?.ToString() switch
                {
                    "PID"      => nameof(ProcessInfo.Pid),
                    "Name"     => nameof(ProcessInfo.Name),
                    "CPU %"    => nameof(ProcessInfo.CpuUsage),
                    "RAM MB"   => nameof(ProcessInfo.MemoryUsage),
                    "Threads"  => nameof(ProcessInfo.ThreadCount),
                    "Handles"  => nameof(ProcessInfo.HandleCount),
                    "Priority" => nameof(ProcessInfo.Priority),
                    "Status"   => nameof(ProcessInfo.Status),
                    "Started"  => nameof(ProcessInfo.StartTime),
                    _          => null
                };
                if (prop == null) return;
                if (_sortProp == prop) _sortAsc = !_sortAsc;
                else { _sortProp = prop; _sortAsc = false; }
                SettingsService.LastSortProp = _sortProp;
                SettingsService.LastSortAsc  = _sortAsc;
                _page = 0;
                ApplyFilter();
            }
        }

        private void MonListHeader_Click(object sender, RoutedEventArgs e) { /* sort if needed */ }

        private void ApplyFilter()
        {
            if (ProcList == null || TbPage == null) return;

            IEnumerable<ProcessInfo> view = _allProcs;

            // Name / PID search
            if (!string.IsNullOrEmpty(_filter))
                view = view.Where(p =>
                    p.Name.Contains(_filter, StringComparison.OrdinalIgnoreCase) ||
                    p.Pid.ToString().Contains(_filter));

            // CPU filter
            if (_cpuMin > 0f)
                view = view.Where(p => p.CpuUsage >= _cpuMin);

            // RAM filter
            if (_ramMin > 0f)
                view = view.Where(p => p.MemoryUsage >= _ramMin);

            view = _sortProp switch
            {
                nameof(ProcessInfo.Pid)         => _sortAsc ? view.OrderBy(p => p.Pid)         : view.OrderByDescending(p => p.Pid),
                nameof(ProcessInfo.Name)        => _sortAsc ? view.OrderBy(p => p.Name)        : view.OrderByDescending(p => p.Name),
                nameof(ProcessInfo.CpuUsage)    => _sortAsc ? view.OrderBy(p => p.CpuUsage)    : view.OrderByDescending(p => p.CpuUsage),
                nameof(ProcessInfo.MemoryUsage) => _sortAsc ? view.OrderBy(p => p.MemoryUsage) : view.OrderByDescending(p => p.MemoryUsage),
                nameof(ProcessInfo.ThreadCount) => _sortAsc ? view.OrderBy(p => p.ThreadCount) : view.OrderByDescending(p => p.ThreadCount),
                nameof(ProcessInfo.HandleCount) => _sortAsc ? view.OrderBy(p => p.HandleCount) : view.OrderByDescending(p => p.HandleCount),
                nameof(ProcessInfo.Priority)    => _sortAsc ? view.OrderBy(p => p.Priority)    : view.OrderByDescending(p => p.Priority),
                nameof(ProcessInfo.Status)      => _sortAsc ? view.OrderBy(p => p.Status)      : view.OrderByDescending(p => p.Status),
                nameof(ProcessInfo.StartTime)   => _sortAsc ? view.OrderBy(p => p.StartTime)   : view.OrderByDescending(p => p.StartTime),
                _ => view
            };

            var paged = view.ToList();
            int total = paged.Count;
            int pages = Math.Max(1, (int)Math.Ceiling(total / (double)_pageSize));
            if (_page >= pages) _page = pages - 1;

            ProcList.ItemsSource = paged.Skip(_page * _pageSize).Take(_pageSize).ToList();
            TbPage.Text = $"Page {_page + 1} / {pages}  ({total} total)";
        }

        private void BtnPrev_Click(object sender, RoutedEventArgs e)
        { if (_page > 0) { _page--; ApplyFilter(); } }

        private void BtnNext_Click(object sender, RoutedEventArgs e)
        { _page++; ApplyFilter(); }

        private void BtnRefreshProcs_Click(object sender, RoutedEventArgs e)
        {
            if (!_refreshBusy)
                _ = Task.Run(async () =>
                {
                    var procs = await Task.Run(_procSvc.GetAllProcesses);
                    Dispatcher.Invoke(() => ApplyNewProcessList(procs));
                });
        }

        private void ProcList_DoubleClick(object sender, System.Windows.Input.MouseButtonEventArgs e)
            => ShowDetails();

        private void BtnDetails_Click(object sender, RoutedEventArgs e) => ShowDetails();

        private void ShowDetails()
        {
            if (ProcList.SelectedItem is not ProcessInfo pi) return;
            try
            {
                using var p  = Process.GetProcessById(pi.Pid);
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("═══  Process Details  ═══\n");
                sb.AppendLine($"  Name          :  {p.ProcessName}");
                sb.AppendLine($"  PID           :  {p.Id}");
                sb.AppendLine($"  Main Window   :  {p.MainWindowTitle}");
                try { sb.AppendLine($"  Priority      :  {p.PriorityClass}"); } catch { }
                try { sb.AppendLine($"  Status        :  {(p.Responding ? "Running" : "Not Responding")}"); } catch { }
                sb.AppendLine();
                sb.AppendLine($"  RAM (Working) :  {p.WorkingSet64 / 1024 / 1024} MB");
                sb.AppendLine($"  RAM (Private) :  {p.PrivateMemorySize64 / 1024 / 1024} MB");
                sb.AppendLine($"  Virtual Mem   :  {p.VirtualMemorySize64 / 1024 / 1024} MB");
                sb.AppendLine($"\n  Threads       :  {p.Threads.Count}");
                sb.AppendLine($"  Handles       :  {p.HandleCount}");
                try { sb.AppendLine($"  Started       :  {p.StartTime}"); } catch { }
                try { sb.AppendLine($"\n  Executable    :  {p.MainModule?.FileName}"); } catch { }
                sb.AppendLine("\n  Modules (first 10):");
                try
                {
                    int n = 0;
                    foreach (ProcessModule m in p.Modules)
                    { sb.AppendLine($"    {m.ModuleName}"); if (++n >= 10) { sb.AppendLine("    …"); break; } }
                }
                catch { sb.AppendLine("    (access denied)"); }

                var dlg = new DetailsWindow(pi.Pid, p.ProcessName, sb.ToString()) { Owner = this };
                dlg.ShowDialog();
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot get details:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void BtnKill_Click(object sender, RoutedEventArgs e)
        {
            if (ProcList.SelectedItem is not ProcessInfo pi) return;
            if (MessageBox.Show($"Kill process [{pi.Pid}] {pi.Name}?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) != MessageBoxResult.Yes) return;
            try
            {
                using var p = Process.GetProcessById(pi.Pid);
                p.Kill();
                TbStatus.Text = $"  ✓  Process [{pi.Pid}] {pi.Name} terminated.";
            }
            catch (Exception ex)
            {
                MessageBox.Show($"Cannot kill process:\n{ex.Message}", "Error",
                    MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BtnStart_Click(object sender, RoutedEventArgs e)
        {
            string path = InputDialog.Show("Executable path or name:", "Start Process", "");
            if (string.IsNullOrWhiteSpace(path)) return;
            try { Process.Start(new ProcessStartInfo(path) { UseShellExecute = true }); }
            catch (Exception ex) { MessageBox.Show(ex.Message, "Error", MessageBoxButton.OK, MessageBoxImage.Error); }
        }

        private void MenuAddToMonitor_Click(object sender, RoutedEventArgs e)
        {
            if (ProcList.SelectedItem is not ProcessInfo pi) return;
            string name = pi.Name;
            var dlg = new MonitoredProcessWindow(null, _procSvc.TotalRamMb) { Owner = this };
            dlg.PresetName(name);
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var (ok, err) = _dataSvc.AddMonitored(dlg.Result);
                if (!ok) MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                else TbStatus.Text = $"  ✓  '{name}' added to monitoring.";
            }
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB 2 — MONITORING
        // ══════════════════════════════════════════════════════════════════
        private void RefreshMonitorList()
        {
            MonList.ItemsSource = _dataSvc.GetMonitored().ToList();
        }

        private void BtnMonAdd_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new MonitoredProcessWindow(null, _procSvc.TotalRamMb) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var (ok, err) = _dataSvc.AddMonitored(dlg.Result);
                if (!ok) MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshMonitorList();
            }
        }

        private void BtnMonEdit_Click(object sender, RoutedEventArgs e)
        {
            if (MonList.SelectedItem is not MonitoredProcess mp) return;
            var dlg = new MonitoredProcessWindow(mp, _procSvc.TotalRamMb) { Owner = this };
            if (dlg.ShowDialog() == true && dlg.Result != null)
            {
                var (ok, err) = _dataSvc.UpdateMonitored(dlg.Result);
                if (!ok) MessageBox.Show(err, "Validation", MessageBoxButton.OK, MessageBoxImage.Warning);
                RefreshMonitorList();
            }
        }

        private void BtnMonDel_Click(object sender, RoutedEventArgs e)
        {
            if (MonList.SelectedItem is not MonitoredProcess mp) return;
            if (MessageBox.Show("Remove from monitoring?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { _dataSvc.DeleteMonitored(mp.Id); RefreshMonitorList(); }
        }

        // ══════════════════════════════════════════════════════════════════
        //  TAB 3 — ALERTS
        // ══════════════════════════════════════════════════════════════════
        private void RefreshAlertsTab()
        {
            var list = _dataSvc.GetAlerts().OrderByDescending(a => a.Timestamp).ToList();
            AlertList.ItemsSource = list;
            _unackedAlerts = list.Count(a => !a.IsAcknowledged);
            TbAlertCount.Text = $"{list.Count} alerts  |  {_unackedAlerts} unacknowledged";
            TbAlertBadge.Text = _unackedAlerts.ToString();
            AlertBadge.Visibility = _unackedAlerts > 0 ? Visibility.Visible : Visibility.Collapsed;
        }

        private void BtnAckAll_Click(object sender, RoutedEventArgs e)
        { _dataSvc.AcknowledgeAll(); RefreshAlertsTab(); }

        private void BtnDelAlert_Click(object sender, RoutedEventArgs e)
        {
            if (AlertList.SelectedItem is not Alert alert) return;
            _dataSvc.DeleteAlert(alert.Id);
            RefreshAlertsTab();
        }

        private void BtnRefreshAlerts_Click(object sender, RoutedEventArgs e) => RefreshAlertsTab();

        // ══════════════════════════════════════════════════════════════════
        //  TAB 4 — SNAPSHOTS
        // ══════════════════════════════════════════════════════════════════
        private void RefreshSnapshots()
        {
            SnapList.ItemsSource = _dataSvc.GetSnapshots()
                .OrderByDescending(s => s.Timestamp).ToList();
            SnapDetail.Text = "";
        }

        private void SnapList_SelectionChanged(object sender, SelectionChangedEventArgs e)
        {
            if (SnapList.SelectedItem is not ProcessSnapshot snap) return;
            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"═══  Snapshot #{snap.Id} — {snap.Label}  ═══\n");
            sb.AppendLine($"  Time:      {snap.Timestamp}");
            sb.AppendLine($"  CPU:       {snap.CpuUsage:0.0}%");
            sb.AppendLine($"  RAM used:  {snap.MemoryUsage:0.0}%");
            sb.AppendLine($"  Processes: {snap.ProcessCount}\n");
            sb.AppendLine("  Top 15 by RAM:");
            foreach (var p in snap.ProcessesData.OrderByDescending(x => x.MemoryUsage).Take(15))
                sb.AppendLine($"    [{p.Pid,6}] {p.Name,-25} {p.MemoryUsage,8:0.0} MB   CPU {p.CpuUsage,5:0.0}%");
            SnapDetail.Text = sb.ToString();
        }

        private void BtnTakeSnap_Click(object sender, RoutedEventArgs e)
        {
            string label = InputDialog.Show("Label for this snapshot:", "Take Snapshot",
                $"Snapshot {DateTime.Now:HH:mm:ss}", this);
            if (string.IsNullOrWhiteSpace(label)) return;
            _dataSvc.TakeSnapshot(label, _lastCpu, _lastRam, _allProcs);
            RefreshSnapshots();
            TbStatus.Text = "  📷  Snapshot saved.";
        }

        private void BtnExportSnap_Click(object sender, RoutedEventArgs e)
        {
            if (SnapList.SelectedItem is not ProcessSnapshot snap)
            { MessageBox.Show("Select a snapshot first.", "Export"); return; }
            string json = _dataSvc.ExportSnapshot(snap.Id);
            if (string.IsNullOrEmpty(json)) return;
            var dlg = new SaveFileDialog
            { Title = "Export Snapshot as JSON", Filter = "JSON files|*.json", FileName = $"snapshot_{snap.Id}.json" };
            if (dlg.ShowDialog() == true)
            {
                File.WriteAllText(dlg.FileName, json);
                TbStatus.Text = $"  ✓  Exported to {dlg.FileName}";
            }
        }

        private void BtnDeleteSnap_Click(object sender, RoutedEventArgs e)
        {
            if (SnapList.SelectedItem is not ProcessSnapshot snap) return;
            if (MessageBox.Show($"Delete snapshot #{snap.Id}?", "Confirm",
                MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.Yes)
            { _dataSvc.DeleteSnapshot(snap.Id); RefreshSnapshots(); }
        }

        private void BtnCompareSnap_Click(object sender, RoutedEventArgs e)
        {
            var snaps = _dataSvc.GetSnapshots().OrderByDescending(x => x.Timestamp).ToList();
            if (snaps.Count < 2) { MessageBox.Show("Need at least 2 snapshots.", "Compare"); return; }

            var dlg = new ComparePickerWindow(snaps) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            var s1 = dlg.SnapshotA!;
            var s2 = dlg.SnapshotB!;
            if (s1.Id == s2.Id) { MessageBox.Show("Select two different snapshots."); return; }

            var cmp = new CompareResultWindow(s1, s2) { Owner = this };
            cmp.ShowDialog();
        }

        // ══════════════════════════════════════════════════════════════════
        //  ALERT NOTIFICATION
        // ══════════════════════════════════════════════════════════════════
        private void OnAlertRaised(Alert alert)
        {
            Dispatcher.Invoke(() =>
            {
                _unackedAlerts++;
                AlertBadge.Visibility = Visibility.Visible;
                TbAlertBadge.Text = _unackedAlerts.ToString();
                string msg = alert.AlertType == AlertType.HighCPU
                    ? $"⚠  {alert.ProcessName}: CPU at {alert.Value:0.0}%"
                    : $"⚠  {alert.ProcessName}: RAM at {alert.Value:0.0} MB";
                TbStatus.Text = $"  🔔  {msg}";
                ShowToast(msg);
            });
        }

        private void ShowToast(string msg)
        {
            TbToast.Text = msg;
            ToastBorder.Visibility = Visibility.Visible;
            _toastTimer?.Stop();
            _toastTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(3500) };
            _toastTimer.Tick += (s, e) =>
            {
                ToastBorder.Visibility = Visibility.Collapsed;
                _toastTimer?.Stop();
            };
            _toastTimer.Start();
        }
    }
}
