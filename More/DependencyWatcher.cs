using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using MoreInternals.Helpers;
using System.IO;
using System.Threading;

namespace More
{
    class DependencyWatcher : IDisposable
    {
        private object Lock = new object();

        private DependencyGraph DependsOn;

        private FileSystemWatcher Watcher;

        public event Action<List<string>> Changed;

        private volatile HashSet<string> PendingChanges = new HashSet<string>();

        private volatile Thread NotifyThread;

        private volatile bool IsDisposed;

        public DependencyWatcher(DependencyGraph depends)
        {
            DependsOn = depends;
        }
        
        internal void Init(string watchDirectory)
        {
            lock (Lock)
            {
                if (Watcher != null) throw new InvalidOperationException();

                Watcher = new FileSystemWatcher(watchDirectory, "*.*");
                Watcher.IncludeSubdirectories = true;
                Watcher.Changed += FileChanged;

                Watcher.EnableRaisingEvents = true;
            }
        }

        private void TryNotifyThread()
        {
            if(NotifyThread == null){
                lock (Lock)
                {
                    if (NotifyThread == null)
                    {
                        NotifyThread = 
                            new Thread(
                                new ThreadStart(
                                    delegate
                                    {
                                        // give the file system time to "catch up" if somebody does a "Save All"
                                        while (!IsDisposed)
                                        {
                                            Thread.Sleep(500);
                                            if (IsDisposed) return;


                                            if (PendingChanges.Count != 0)
                                            {
                                                HashSet<string> copy;
                                                lock (Lock)
                                                {
                                                    copy = PendingChanges;
                                                    PendingChanges = new HashSet<string>();
                                                }

                                                if (!IsDisposed)
                                                {
                                                    Changed(copy.ToList());
                                                }
                                            }
                                        }
                                    }
                                )
                            );

                        NotifyThread.IsBackground = true;
                        NotifyThread.Start();
                    }
                }
            }
        }

        private void FileChanged(object sender, FileSystemEventArgs e)
        {
            if (DependsOn.Contains(e.FullPath))
            {
                lock (Lock)
                {
                    PendingChanges.Add(e.FullPath);
                }

                TryNotifyThread();
            }
        }

        public void Dispose()
        {
            IsDisposed = true;

            if (Watcher != null)
            {
                Watcher.Dispose();
                Watcher = null;
            }
        }
    }
}
