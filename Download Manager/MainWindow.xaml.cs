using Microsoft.Win32;
using System;
using System.Windows;

namespace Download_Manager
{
    /// <summary>
    /// Interaction logic for DwnlDialog.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        /// <summary>
        /// logic to get the path to save the file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnTarget_Click(object sender, RoutedEventArgs e)
        {
            SaveFileDialog saveDialog = new SaveFileDialog();
            saveDialog.ValidateNames = true;
            if (saveDialog.ShowDialog() == true)
            {
                txtTarget.Text = saveDialog.FileName;
            }
        }

        /// <summary>
        /// logic to validate data
        /// and open download ui
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void btnOk_Click(object sender, RoutedEventArgs e)
        {
            //get the required details from the user
            //validate it and save them
            try
            {
                string url = txtURL.Text;
                string target = txtTarget.Text;
                int threads = Int32.Parse(txtThreads.Text);
                int limit = Int32.Parse(txtLimit.Text);

                new Download(url, target, threads, limit).Show();

                this.Close();
            }
            catch (Exception ex)
            {
                MessageBox.Show(ex.Message, "Need correct data");
            }
        }
    }
}
