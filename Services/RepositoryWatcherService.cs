// Services/RepositoryWatcherService.cs
using System.Diagnostics;
using System.IO;
using System.Timers;
using Timer = System.Timers.Timer;

namespace GitWave.Services
{
    public class RepositoryWatcherService : IDisposable
    {
        private FileSystemWatcher _watcher;
        private string _watchedPath;
        private readonly object _lockObj = new object();

        // Debounce timer - wait 500ms after last change before firing event
        private Timer _debounceTimer;
        private const int DebounceDelayMs = 1000;

        public event Action<string> OnRepositoryChanged;

        public RepositoryWatcherService()
        {
            _debounceTimer = new Timer(DebounceDelayMs);
            _debounceTimer.AutoReset = false;
            _debounceTimer.Elapsed += OnDebounceTimerElapsed;
        }

        public void StartWatching(string repositoryPath)
        {
            if (string.IsNullOrWhiteSpace(repositoryPath))
            {
                Debug.WriteLine("[RepoWatcher] ❌ Repository path is empty");
                return;
            }

            if (!Directory.Exists(repositoryPath))
            {
                Debug.WriteLine($"[RepoWatcher] ❌ Directory does not exist: {repositoryPath}");
                return;
            }

            // Stop existing watcher
            StopWatching();

            _watchedPath = repositoryPath;

            try
            {
                // Create and configure watcher
                _watcher = new FileSystemWatcher(repositoryPath)
                {
                    // Watch for changes to any file
                    Filter = "*.*",

                    // Monitor file name changes and last write time changes
                    NotifyFilter = NotifyFilters.FileName
                                   | NotifyFilters.DirectoryName
                                   | NotifyFilters.LastWrite
                                   | NotifyFilters.Size,

                    // Watch subdirectories recursively
                    IncludeSubdirectories = true,
                    EnableRaisingEvents = true
                };

                // Hook up event handlers
                _watcher.Created += OnFileSystemEvent;
                _watcher.Changed += OnFileSystemEvent;
                _watcher.Deleted += OnFileSystemEvent;
                _watcher.Renamed += OnFileSystemEvent;
                _watcher.Error += OnWatcherError;

                // Enable raising events
                _watcher.EnableRaisingEvents = true;

                Debug.WriteLine($"[RepoWatcher] ✅ Started watching: {repositoryPath}");
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RepoWatcher] ❌ Error starting watcher: {ex.Message}");
            }
        }

        public void StopWatching()
        {
            try
            {
                if (_debounceTimer != null)
                {
                    _debounceTimer.Stop();
                }

                if (_watcher != null)
                {
                    _watcher.EnableRaisingEvents = false;
                    _watcher.Dispose();
                    _watcher = null;
                    Debug.WriteLine($"[RepoWatcher] ✅ Stopped watching");
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RepoWatcher] ❌ Error stopping watcher: {ex.Message}");
            }
        }

        public string GetWatchedPath() => _watchedPath;

        private void OnFileSystemEvent(object sender, FileSystemEventArgs e)
        {
            // Ignore .git directory changes (internal git operations)
            if (e.FullPath.Contains($"{Path.DirectorySeparatorChar}.git{Path.DirectorySeparatorChar}")) return;

            // Ignore temporary and lock files
            if (e.Name.EndsWith(".lock") || e.Name.StartsWith("~$")) return;

            Debug.WriteLine($"[RepoWatcher] 📝 {e.ChangeType}: {e.Name}");

            lock (_lockObj)
            {
                _debounceTimer.Stop();
                _debounceTimer.Start();
            }
        }

        private void OnWatcherError(object sender, ErrorEventArgs e)
        {
            Debug.WriteLine($"[RepoWatcher] ⚠️ Watcher Error: {e.GetException().Message}, restarting...");
            StopWatching();
            StartWatching(_watchedPath);
        }

        private void OnDebounceTimerElapsed(object sender, ElapsedEventArgs e)
        {
            // Fire the event after debounce delay
            try
            {
                Debug.WriteLine($"[RepoWatcher] 🔄 Repository changed event fired (debounced)");
                OnRepositoryChanged?.Invoke(_watchedPath);
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[RepoWatcher] ❌ Error in OnRepositoryChanged handler: {ex.Message}");
            }
        }

        public void Dispose()
        {
            StopWatching();
            _debounceTimer?.Dispose();
            _watcher?.Dispose();
        }
    }
}