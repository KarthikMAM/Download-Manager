using System;
using System.IO;
using System.Windows;
using System.Windows.Controls;

namespace Download_Manager
{
    /// <summary>
    /// Interaction logic for Download.xaml
    /// </summary>
    public partial class Download : Window
    {
        //download properties
        private String FileName;
        private String URL;
        private String Target;
        private int Threads;
        private int Limit;

        //working components
        Downloader downloader;

        /// <summary>
        /// initialize the download
        /// </summary>
        public Download(string url, string target, int threads, int limit)
        {
            InitializeComponent();
            
            //save the download data
            this.URL = url;
            this.Target = target;
            this.Threads = threads;
            this.Limit = limit;
            this.FileName = Path.GetFileName(Target);

            //create and start the downloader 
            downloader = new Downloader(URL, Target, Limit, Threads);
            downloader.ProgressTracker.Tick += ProgressTracker_Tick;
            downloader.Start();

            //set the ui
            lblFileName.Content = FileName;
            lblTarget.Content = Target;
            lblUrl.Content = URL;
        }

        /// <summary>
        /// progress tracker updates
        /// </summary>
        /// <param name="sender">the downloader downloading the file</param>
        /// <param name="e">event arguements</param>
        private void ProgressTracker_Tick(object sender, EventArgs e)
        {
            //update progress details in the ui
            if (downloader.DwnlSize > 0)
            {
                barDownload.Value = downloader.DwnlCompleted / downloader.DwnlSize;
                lblSize.Content = String.Format("{0:f2} / {1:f2} MB", downloader.DwnlCompleted, downloader.DwnlSize);
                lblProgress.Content = String.Format("{0:f2} MBps", downloader.DwnlSpeed);

                if (downloader.DwnlCompleted == downloader.DwnlSize) btnPause.IsEnabled = false;
            }
        }

        /// <summary>
        /// logic to handle buttons click
        /// </summary>
        /// <param name="sender">buttons clicked</param>
        /// <param name="e">event args</param>
        private void btnPause_Click(object sender, RoutedEventArgs e)
        {
            //get the sender button
            Button btnSender = (Button)sender;

            //resolve the buttons and perform the required operations
            btnSender.IsEnabled = false;
            switch (btnSender.Content.ToString())
            {
                case "Pause":
                    downloader.Stop();
                    btnSender.Content = "Resume";
                    break;
                case "Resume":
                    downloader = new Downloader(URL, Target, Limit, Threads);
                    downloader.ProgressTracker.Tick += ProgressTracker_Tick;
                    downloader.Start();
                    btnSender.Content = "Pause";
                    break;
            }
            btnSender.IsEnabled = true;
        }

        private void Window_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            downloader.Stop();
        }
    }
}
