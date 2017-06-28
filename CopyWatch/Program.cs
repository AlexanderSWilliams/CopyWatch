using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace CopyWatch
{
    public static class Ext
    {
        public static Action<int> Debounce(this Action func)
        {
            var last = 0;
            return new Action<int>(x =>
            {
                var current = Interlocked.Increment(ref last);
                var bob = Task.Delay(x).ContinueWith(task =>
                {
                    if (current == last) func();
                    task.Dispose();
                });
            });
        }

        public static Action<T> Debounce<T>(this Action<T> func, int milliseconds, bool wait)
        {
            var last = 0;
            return arg =>
            {
                var current = Interlocked.Increment(ref last);
                Task.Delay(milliseconds).ContinueWith(task =>
                {
                    if (current == last) func(arg);
                    task.Dispose();
                });
            };
        }
    }

    internal class Program
    {
        public static Action<int> DoneAction = null;

        public static List<string> NewDirectories = new List<string>();

        public static object NewDirectoriesLock = new object();

        public static string WatchFolderPath = null;

        public static string[] ExecuteProcess(string cmd, string arguments)
        {
            using (var process = new Process())
            {
                process.StartInfo.WindowStyle = ProcessWindowStyle.Hidden;
                process.StartInfo.CreateNoWindow = true;
                process.StartInfo.FileName = cmd;
                process.StartInfo.Arguments = arguments;
                process.StartInfo.UseShellExecute = false;
                process.StartInfo.RedirectStandardOutput = true;
                process.StartInfo.RedirectStandardError = true;
                process.Start();

                var Standard = process.StandardOutput.ReadToEnd();
                var Error = process.StandardError.ReadToEnd();
                process.WaitForExit();
                return new[] { Standard, Error };
            }
        }

        public static bool IsFileLocked(FileInfo file)
        {
            FileStream stream = null;
            try
            {
                using (stream = file.Open(FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite))
                { }
            }
            catch (IOException)
            {
                return true;
            }
            catch (UnauthorizedAccessException)
            { }
            finally
            {
                if (stream != null)
                    stream.Dispose();
            }

            return false;
        }

        public static void OnNewFolder(string fullPath)
        {
            if (!File.GetAttributes(fullPath).HasFlag(FileAttributes.Directory))
            {
                while (IsFileLocked(new FileInfo(fullPath)))
                {
                    DoneAction(2000);
                }
            }
            else
            {
                if (Directory.GetParent(fullPath).ToString().TrimEnd(new[] { '\\' }).ToLower() == WatchFolderPath)
                {
                    lock (NewDirectoriesLock)
                    {
                        NewDirectories.Add(fullPath);
                    }
                }
            }

            DoneAction(5000);
        }

        public static void OnNewFolderEvent(object source, FileSystemEventArgs e)
        {
            var FullPath = e.FullPath;
            OnNewFolder(FullPath);
        }

        private static void Main(string[] args)
        {
            if (args.Length != 2)
            {
                Console.WriteLine(@"copywatch ""path to folder to watch"" ""path to file/script to execute whenever copying is finished.""");
                return;
            }

            WatchFolderPath = args[0].TrimEnd(new[] { '\\' }).ToLower();

            DoneAction = (new Action(() =>
            {
                string[] Directories = null;
                lock (NewDirectoriesLock)
                {
                    Directories = NewDirectories.ToArray();
                    NewDirectories = new List<string>();
                }

                foreach (var directory in Directories)
                {
                    ExecuteProcess(args[1], "\"" + directory + "\"");
                }
            })).Debounce();

            using (var watcher = new FileSystemWatcher())
            {
                watcher.Path = WatchFolderPath;
                watcher.EnableRaisingEvents = true;
                watcher.IncludeSubdirectories = true;
                watcher.InternalBufferSize = 16777216;
                watcher.NotifyFilter = NotifyFilters.DirectoryName | NotifyFilters.FileName;
                watcher.Created += new FileSystemEventHandler(OnNewFolderEvent);

                new System.Threading.AutoResetEvent(false).WaitOne();
            }
        }
    }
}