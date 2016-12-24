using DownloadHelper;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DownloadTester
{
    class Program
    {
        static Downloader downloader;
        static void Main(string[] args)
        {
            downloader = new Downloader();
            downloader.Start();
            downloader.DwnlTracker.Start();

            downloader.DwnlTracker.Tracker.Tick += Tracker_Tick;

            while (true)
            {
                Console.WriteLine(downloader.State);
                System.Threading.Thread.Sleep(1000);
            }
        }

        private static void Tracker_Tick(object sender, EventArgs e)
        {
            Console.WriteLine(downloader.DwnlTracker.DwnlProgressMsg);
        }
    }
}
