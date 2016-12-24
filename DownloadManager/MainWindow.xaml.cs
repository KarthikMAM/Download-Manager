using DownloadHelper;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;

namespace DownloadManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        Downloader downloader;
        public MainWindow()
        {
            InitializeComponent();

            //if text is in clipboard copy it.
            //if (Clipboard.ContainsText()) txtSource.Text = Clipboard.GetText();
        }

        /// <summary>
        /// upon closing the window abort the download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (downloader != null) downloader.Abort();
        }

        /// <summary>
        /// responses to various button click scenarios
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Button_Click(object sender, RoutedEventArgs e)
        {
            //get the sender button
            Button btSender = (Button)sender;

            //for controller use content and others their name
            switch (btSender != btController ? btSender.Name : btSender.Content.ToString())
            {
                case "btTarget":
                    try
                    {
                        //find the file name from the url and use it
                        SaveFileDialog selectTargetDialog = new SaveFileDialog();
                        selectTargetDialog.FileName = Downloader.FindFileName(txtSource.Text);
                        selectTargetDialog.Title = "Select a file to save download";
                        if (selectTargetDialog.ShowDialog() == true)
                        {
                            txtTarget.Text = selectTargetDialog.FileName;
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message, "Error"); }
                    break;
                case "Start":
                    //disable items on window to prevent inconsistency
                    if (txtSource.Text.Length != 0 && txtTarget.Text.Length != 0)
                    {
                        txtSource.IsEnabled = txtTarget.IsEnabled = btTarget.IsEnabled = false;
                        btController.IsEnabled = false;

                        //create a new downloader object
                        downloader = new Downloader(txtSource.Text, txtTarget.Text);
                        downloader.DwnlTracker.Tracker.Tick += Tracker_Tick;
                        downloader.Start();
                    }
                    else { MessageBox.Show("Need Download source and target path", "Incomplete Data"); }
                    break;
                case "Pause":
                    btController.IsEnabled = false;

                    downloader.Abort();
                    break;
                case "Resume":
                    btController.IsEnabled = false;

                    downloader.Start();
                    break;

            }
        }

        private void Tracker_Tick(object sender, EventArgs e)
        {
            switch (downloader.State)
            {
                case Downloader.DownloadState.Download:
                    btController.Content = "Pause";
                    btController.IsEnabled = true;

                    //update progress of the download
                    txtSize.Content = String.Format("{0:f3} MB", (double)downloader.DwnlSize / Downloader.MB);
                    txtComplete.Content = String.Format("{0:f3} MB", (double)downloader.DwnlTracker.DwnlCompleted / Downloader.MB);
                    txtSpeed.Content = String.Format("{0:f3} MBps", (double)downloader.DwnlTracker.DwnlSpeed / Downloader.MB);

                    break;
                case Downloader.DownloadState.Abort:
                    txtSpeed.Content = "0.000 MBps";
                    break;
                case Downloader.DownloadState.Idle:
                    btController.Content = "Resume";
                    btController.IsEnabled = true;
                    break;
                case Downloader.DownloadState.Append:
                case Downloader.DownloadState.Finish:
                    btController.IsEnabled = false;

                    //update progress of the download
                    txtSize.Content = String.Format("{0:f3} MB", (double)downloader.DwnlSize / Downloader.MB);
                    txtComplete.Content = String.Format("{0:f3} MB", (double)downloader.DwnlTracker.DwnlCompleted / Downloader.MB);
                    txtSpeed.Content = String.Format("{0:f3} MBps", (double)downloader.DwnlTracker.DwnlSpeed / Downloader.MB);
                    break;
            }

            //update the progress bar value and its message
            txtProgress.Content = downloader.DwnlTracker.DwnlProgressMsg;
            barProgress.Value = downloader.DwnlTracker.DwnlProgress;
        }
    }
}
