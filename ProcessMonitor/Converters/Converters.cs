using System;
using System.Globalization;
using System.Windows;
using System.Windows.Data;
using System.Windows.Media;

namespace ProcessMonitor.Converters
{
    // ── CPU colour ─────────────────────────────────────────────────────────
    public class CpuToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is float v)
                return v > 50 ? new SolidColorBrush(Color.FromRgb(255, 121, 77))
                     : v > 20 ? new SolidColorBrush(Color.FromRgb(255, 210, 80))
                     : new SolidColorBrush(Color.FromRgb(230, 237, 243));
            return new SolidColorBrush(Color.FromRgb(230, 237, 243));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── RAM colour ─────────────────────────────────────────────────────────
    public class RamToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            if (value is float v)
                return v > 500 ? new SolidColorBrush(Color.FromRgb(255, 121, 77))
                     : v > 100 ? new SolidColorBrush(Color.FromRgb(255, 210, 80))
                     : new SolidColorBrush(Color.FromRgb(230, 237, 243));
            return new SolidColorBrush(Color.FromRgb(230, 237, 243));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── Status colour ──────────────────────────────────────────────────────
    public class StatusToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var s = value?.ToString() ?? "";
            return s.Contains("Not")
                ? new SolidColorBrush(Color.FromRgb(255, 121, 77))
                : new SolidColorBrush(Color.FromRgb(0, 200, 150));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── Bool → Yes/No ──────────────────────────────────────────────────────
    public class BoolToYesNoConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? "Yes" : "No";
        public object ConvertBack(object v, Type t, object p, CultureInfo c)
            => v?.ToString() == "Yes";
    }

    // ── Bool → Accent/Dim colour ───────────────────────────────────────────
    public class BoolToAccentDimConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true
                ? new SolidColorBrush(Color.FromRgb(0, 200, 150))
                : new SolidColorBrush(Color.FromRgb(110, 118, 129));
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── AlertType → colour ─────────────────────────────────────────────────
    public class AlertTypeToColorConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var s = value?.ToString() ?? "";
            return s.Contains("CPU")
                ? new SolidColorBrush(Color.FromRgb(255, 121, 77))
                : new SolidColorBrush(Color.FromRgb(88, 166, 255));
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── ISO timestamp → display string ────────────────────────────────────
    public class TimestampConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var s = value?.ToString() ?? "";
            return DateTime.TryParse(s, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm:ss") : s;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v;
    }

    public class TimestampShortConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
        {
            var s = value?.ToString() ?? "";
            return DateTime.TryParse(s, out var dt) ? dt.ToString("yyyy-MM-dd HH:mm") : s;
        }
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => v;
    }

    // ── float → "0.0%" ────────────────────────────────────────────────────
    public class FloatPctConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is float f ? $"{f:0.0}%" : value?.ToString() ?? "";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    public class FloatMbConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is float f ? $"{f:0.0} MB" : value?.ToString() ?? "";
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }

    // ── Visibility ────────────────────────────────────────────────────────
    public class BoolToVisibilityConverter : IValueConverter
    {
        public object Convert(object value, Type t, object p, CultureInfo c)
            => value is true ? Visibility.Visible : Visibility.Collapsed;
        public object ConvertBack(object v, Type t, object p, CultureInfo c) => Binding.DoNothing;
    }
}
