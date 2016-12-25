using Downloader;
using Microsoft.Win32;
using System;
using System.Threading;
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

        /// <summary>
        /// initializes the main window
        /// </summary>
        public MainWindow()
        {
            InitializeComponent();

            //if text is in clipboard copy it.
            if (Clipboard.ContainsText() && Clipboard.GetText().Contains("http"))
            {
                txtSource.Text = Clipboard.GetText();
            }
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

                    new Thread((dwnlSource) =>
                    {
                        try
                        {
                            //find the file name from the url and use it
                            SaveFileDialog selectTarget = new SaveFileDialog();
                            selectTarget.FileName = Download.FindFileName((string)dwnlSource);
                            selectTarget.Title = "Select a file to save download";
                            string dwnlTarget = selectTarget.ShowDialog() == true ? selectTarget.FileName : "";

                            //set file path in application thread
                            Application.Current.Dispatcher.Invoke(() => txtTarget.Text = dwnlTarget);
                        }
                        catch (Exception ex)
                        {
                            //show exception message
                            MessageBox.Show(ex.Message, "Download Failed");
                        }
                    }).Start(txtSource.Text);

                    break;
                case "Start":

                    //disable items on window to prevent inconsistency
                    if (txtSource.Text.Length != 0 && txtTarget.Text.Length != 0)
                    {
                        //disable UI
                        txtSource.IsEnabled
                            = txtTarget.IsEnabled
                            = btTarget.IsEnabled
                            = btSender.IsEnabled
                            = false;

                        //get the source and target url
                        string dwnlSource = txtSource.Text;
                        string dwnlTarget = txtTarget.Text;

                        new Thread(() =>
                        {
                            try
                            {
                                //create a new download job
                                download = new Download(dwnlSource, dwnlTarget);

                                //run the rest in application thread
                                Application.Current.Dispatcher.Invoke(() =>
                                {
                                    //stop the download tracker and engine if they exist
                                    if (downloadTracker != null) { downloadTracker.Stop(); }
                                    if (downloadEngine != null) { downloadEngine.Abort().Join(); }

                                    //create the tracker, engine
                                    downloadTracker = new DispatcherTimer();
                                    downloadTracker.Interval = TimeSpan.FromSeconds(1);
                                    downloadTracker.Tick += Tracker;
                                    downloadTracker.Start();

                                    //create the engine
                                    downloadEngine = new DownloadEngine(download, downloadTracker);
                                    downloadEngine.Start();
                                });
                            }
                            catch (Exception ex)
                            {
                                MessageBox.Show(ex.Message, "Download Failed");

                                //reset the UI in application thread
                                Application.Current.Dispatcher.Invoke(() => ResetUI());
                            }
                        }).Start();
                    }
                    else
                    {
                        MessageBox.Show("Need Download source and target path", "Incomplete Data");
                    }

                    break;
                case "Pause":
                    downloadEngine.Abort();

                    break;
                case "Resume":
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
        private string DotAnimation(string currentString, string newString)
        {
            string dotAnimation = "";
            if (currentString.Contains(" ● ● ●")) dotAnimation = " ●      ";
            else if (currentString.Contains(" ● ●   ")) dotAnimation = " ● ● ●";
            else dotAnimation = " ● ●   ";

            return newString + dotAnimation;
        }

        /// <summary>
        /// resets the UI to normal state
        /// </summary>
        private void ResetUI()
        {
            //reset UI
            btController.Content = "Start";
            txtSource.IsEnabled
                = txtTarget.IsEnabled
                = btTarget.IsEnabled
                = btController.IsEnabled
                = true;
        }

        /// <summary>
        /// download engine tracker updating the UI
        /// </summary>
        /// <param name="sender">dispatcher timer tracker</param>
        /// <param name="e">eventargs</param>
        private void Tracker(object sender, EventArgs e)
        {
            //tracking progress data
            string strSize = Download.FormatBytes(download.DwnlSize);
            string strCompleted = Download.FormatBytes(download.DwnlSizeCompleted);
            string strSpeed = "0.00 KBps";
            string strProgress = txtProgress.Content.ToString();
            double valProgress = 0;

            switch (downloadEngine.State)
            {
                case DwnlState.Create:
                    strProgress = DotAnimation(strProgress, "Creating download job");

                    break;
                case DwnlState.Idle:
                    strProgress = "Waiting to start / resume download ● ● ●";

                    //enable resume button
                    btController.Content = "Resume";
                    btController.IsEnabled = true;

                    break;
                case DwnlState.Start:
                    strProgress = DotAnimation(strProgress, "Starting download");

                    break;
                case DwnlState.Download:
                    strSpeed = Download.FormatBytes(download.DwnlSpeed) + "ps";
                    valProgress = download.DwnlProgress;
                    strProgress = DotAnimation(strProgress, string.Format("Downloading at {0:f2}%", download.DwnlProgress));

                    //enable the pause button
                    btController.Content = "Pause";
                    btController.IsEnabled = true;

                    break;
                case DwnlState.Append:
                    valProgress = download.AppendProgress;
                    strProgress = DotAnimation(strProgress, string.Format("Appending at {0:f2}%", download.AppendProgress));

                    //disable controller
                    btController.IsEnabled = false;

                    break;
                case DwnlState.Complete:
                    valProgress = 100;
                    strProgress = "Download complete ● ● ●";

                    ResetUI();

                    break;
                case DwnlState.Error:
                    strProgress = "Download error ● ● ●";

                    //display error message
                    if (txtProgress.Content.ToString() != strProgress)
                    {
                        MessageBox.Show(downloadEngine.Error.Message, "Download Failed");
                    }

                    ResetUI();

                    break;
                case DwnlState.Abort:
                    strProgress = DotAnimation(strProgress, "Aborting download");

                    //disable controller button
                    btController.IsEnabled = false;

                    break;
            }

            //updatring progress data
            txtSize.Content = strSize;
            txtComplete.Content = strCompleted;
            txtSpeed.Content = strSpeed;
            txtProgress.Content = strProgress;
            barProgress.Value = valProgress;
        }

        /// <summary>
        /// url click event
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
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
