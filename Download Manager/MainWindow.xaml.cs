using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace Download_Manager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        Downloader x;
        private void button_Click(object sender, RoutedEventArgs e)
        {
            x = new Downloader();
            x.ProgressTracker.Tick += ProgressTracker_Tick;
        }

        private void ProgressTracker_Tick(object sender, EventArgs e)
        {
            progress.Text = "Completed: " + x.DwnlProgress + " Speed: " + x.DwnlSpeed;
        }
    }
}
