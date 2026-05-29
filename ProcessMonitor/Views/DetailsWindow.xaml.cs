using System.Windows;

namespace ProcessMonitor.Views
{
    public partial class DetailsWindow : Window
    {
        public DetailsWindow(int pid, string name, string content)
        {
            InitializeComponent();
            Title = $"Details — [{pid}] {name}";
            TbContent.Text = content;
        }
    }
}
