using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace ProcessMonitor.Views
{
    internal static class InputDialog
    {
        public static string Show(string prompt, string title,
                                  string defaultValue = "", Window? owner = null)
        {
            var B_Panel   = (Brush)Application.Current.Resources["B_Panel"];
            var B_Panel2  = (Brush)Application.Current.Resources["B_Panel2"];
            var B_Accent  = (Brush)Application.Current.Resources["B_Accent"];
            var B_BG      = (Brush)Application.Current.Resources["B_BG"];
            var B_Text    = (Brush)Application.Current.Resources["B_Text"];
            var B_Border  = (Brush)Application.Current.Resources["B_Border"];

            var win = new Window
            {
                Title  = title,
                Width  = 440,
                Height = 180,
                ResizeMode = ResizeMode.NoResize,
                WindowStartupLocation = WindowStartupLocation.CenterOwner,
                Owner  = owner,
                Background  = B_Panel,
                Foreground  = B_Text,
                FontFamily  = new FontFamily("Segoe UI"),
                FontSize    = 13
            };

            var outer = new Grid { Margin = new Thickness(20, 16, 20, 16) };
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(8) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            outer.RowDefinitions.Add(new RowDefinition { Height = new GridLength(16) });
            outer.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            // Prompt label
            var lbl = new TextBlock
            {
                Text       = prompt,
                Foreground = B_Text,
                TextWrapping = TextWrapping.Wrap
            };
            Grid.SetRow(lbl, 0);

            // Input box
            var txt = new TextBox
            {
                Text            = defaultValue,
                Height          = 30,
                Background      = B_Panel2,
                Foreground      = B_Text,
                BorderBrush     = B_Border,
                BorderThickness = new Thickness(1),
                Padding         = new Thickness(8, 4, 8, 4),
                CaretBrush      = B_Text,
                FontSize        = 13
            };
            txt.SelectAll();
            Grid.SetRow(txt, 2);

            // Buttons row
            var btns = new StackPanel
            {
                Orientation         = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };

            var btnStyle = (Style)Application.Current.Resources["AccentBtn"];

            var ok = new Button
            {
                Content         = "OK",
                Width           = 100,
                Height          = 32,
                Margin          = new Thickness(0, 0, 10, 0),
                Background      = B_Accent,
                Foreground      = B_BG,
                BorderThickness = new Thickness(0),
                FontWeight      = FontWeights.SemiBold,
                Style           = btnStyle
            };
            var cancel = new Button
            {
                Content         = "Cancel",
                Width           = 100,
                Height          = 32,
                Background      = B_Panel2,
                Foreground      = B_Text,
                BorderBrush     = B_Border,
                Style           = btnStyle
            };

            string result = "";
            ok.Click     += (s, e) => { result = txt.Text.Trim(); win.DialogResult = true; };
            cancel.Click += (s, e) => { win.DialogResult = false; };
            win.KeyDown  += (s, e) =>
            {
                if (e.Key == System.Windows.Input.Key.Enter)  { result = txt.Text.Trim(); win.DialogResult = true; }
                if (e.Key == System.Windows.Input.Key.Escape) { win.DialogResult = false; }
            };

            btns.Children.Add(ok);
            btns.Children.Add(cancel);
            Grid.SetRow(btns, 4);

            outer.Children.Add(lbl);
            outer.Children.Add(txt);
            outer.Children.Add(btns);
            win.Content = outer;
            win.ShowDialog();
            return result;
        }
    }
}
