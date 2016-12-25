using Downloader;
using Microsoft.Win32;
using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using static Downloader.DownloadEngine;

namespace DownloadManager
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        //download engine, download and tracker
        Download download;
        DownloadEngine downloadEngine;
        DispatcherTimer downloadTracker;

        public MainWindow()
        {
            InitializeComponent();

            //if text is in clipboard copy it.
            if (Clipboard.ContainsText()) txtSource.Text = Clipboard.GetText();
        }

        /// <summary>
        /// upon closing the window abort the download
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (downloadEngine != null) downloadEngine.Abort();
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
                        selectTargetDialog.FileName = Download.FindFileName(txtSource.Text);
                        selectTargetDialog.Title = "Select a file to save download";
                        if (selectTargetDialog.ShowDialog() == true)
                        {
                            txtTarget.Text = selectTargetDialog.FileName;
                        }
                    }
                    catch (Exception ex) { MessageBox.Show(ex.Message.Contains(":") ? ex.Message : "Error: ", "Download Failed: Error"); }
                    break;
                case "Start":
                    //disable items on window to prevent inconsistency
                    if (txtSource.Text.Length != 0 && txtTarget.Text.Length != 0)
                    {
                        txtSource.IsEnabled = txtTarget.IsEnabled = btTarget.IsEnabled = false;
                        btController.IsEnabled = false;
                        try
                        {
                            //create a new downloader job
                            download = new Download(txtSource.Text, txtTarget.Text);

                            //create and start a tracker
                            downloadTracker = new DispatcherTimer();
                            downloadTracker.Interval = TimeSpan.FromSeconds(1);
                            downloadTracker.Tick += Tracker_Tick;
                            downloadTracker.Start();

                            //create and start the download engine
                            downloadEngine = new DownloadEngine(download, downloadTracker);
                            downloadEngine.Start();
                        }
                        catch (Exception ex)
                        {
                            MessageBox.Show(ex.Message, "Download Failed");
                            txtSource.IsEnabled = txtTarget.IsEnabled = btTarget.IsEnabled = true;
                            btController.IsEnabled = true;
                        }
                    }
                    else
                    {
                        MessageBox.Show("Need Download source and target path", "Incomplete Data");
                    }
                    break;
                case "Pause":
                    btController.IsEnabled = false;
                    downloadEngine.Abort();
                    break;
                case "Resume":
                    btController.IsEnabled = false;

                    downloadEngine.Start();
                    break;

            }
        }

        /// <summary>
        /// creates a string animation using dots
        /// </summary>
        /// <param name="currentString">current string with n dots</param>
        /// <param name="newString">new string with (n + 1) % 3 dots</param>
        /// <returns></returns>
        private String DotAnimation(string currentString, string newString)
        {
            string dotAnimation = "";
            if (currentString.Contains(" ● ● ●")) dotAnimation = " ●    ";
            else if (currentString.Contains(" ● ●  ")) dotAnimation = " ● ● ●";
            else dotAnimation = " ● ●  ";

            return newString + dotAnimation;
        }

        /// <summary>
        /// download engine tracker
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void Tracker_Tick(object sender, EventArgs e)
        {
            //change controller states and update the progress
            switch (downloadEngine.State)
            {
                case DwnlState.Error:
                    if (txtSource.IsEnabled == false)
                    {
                        btController.Content = "Start";
                        btController.IsEnabled = true;
                        txtSource.IsEnabled = txtTarget.IsEnabled = btTarget.IsEnabled = true;
                        txtProgress.Content = "Error aborted ● ● ●";

                        if (downloadEngine != null) MessageBox.Show(downloadEngine.Error.Message, "Download Failed");
                    }
                    break;
                case DwnlState.Idle:
                    btController.Content = "Resume";
                    btController.IsEnabled = true;
                    txtProgress.Content = "Waiting to start / resume the download ● ● ●";

                    break;
                case DwnlState.Download:
                    btController.Content = "Pause";
                    btController.IsEnabled = true;

                    //update progress of the download
                    txtSize.Content = String.Format("{0:f3} MB", (double)download.DwnlSize / Download.MB);
                    txtComplete.Content = String.Format("{0:f3} MB", (double)download.DwnlSizeCompleted / Download.MB);
                    txtSpeed.Content = String.Format("{0:f3} MBps", (double)download.DwnlSpeed / Download.MB);

                    txtProgress.Content = DotAnimation(txtProgress.Content.ToString(), string.Format("Download completion at {0:f2} %", download.DwnlProgress));

                    break;
                case DwnlState.Append:
                    btController.IsEnabled = false;

                    //update progress of the download as append
                    txtSize.Content = String.Format("{0:f3} MB", (double)download.DwnlSize / Download.MB);
                    txtComplete.Content = String.Format("{0:f3} MB", (double)download.DwnlSize / Download.MB);

                    txtProgress.Content = DotAnimation(txtProgress.Content.ToString(), "Stitching chunks at {0:2f} % " + download.AppendProgress / download.DwnlSize * 100);

                    break;
                case DwnlState.Complete:
                    btController.IsEnabled = false;

                    //update progress of the download as complete
                    txtSize.Content = String.Format("{0:f3} MB", (double)download.DwnlSize / Download.MB);
                    txtComplete.Content = String.Format("{0:f3} MB", (double)download.DwnlSize / Download.MB);
                    txtSpeed.Content = String.Format("{0:f3} MBps", (double)download.AppendProgress / Download.MB);

                    txtProgress.Content = "Download complete● ● ● :D";

                    break;
                case DwnlState.Abort:
                    txtSpeed.Content = "0.000 MBps";

                    txtProgress.Content = DotAnimation(txtProgress.Content.ToString(), "Aborting");

                    break;
            }
            barProgress.Value = download.DwnlProgress;
        }

        private void Url_MouseLeftButtonDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {
            switch (((TextBlock)sender).Name)
            {
                case "Github":
                    System.Diagnostics.Process.Start("https://github.com/KarthikMAM");
                    break;
                case "Repo":
                    System.Diagnostics.Process.Start("https://github.com/KarthikMAM/Download-Manager");
                    break;
                case "Issues":
                    System.Diagnostics.Process.Start("https://github.com/KarthikMAM/Download-Manager/issues");
                    break;
            }
        }
    }
}
