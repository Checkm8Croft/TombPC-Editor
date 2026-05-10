using System;

namespace TombPC_Editor
{
    /// <summary>
    /// Interface for logging during TOMBPC.DAT parsing.
    /// Allows flexible logging implementations (console, file, UI, etc.)
    /// </summary>
    public interface IGameflowLogger
    {
        void LogDebug(string message);
        void LogInfo(string message);
        void LogWarning(string message);
        void LogError(string message);
        void LogSection(string sectionName);
    }

    /// <summary>
    /// Simple logger that forwards all messages to an Action callback.
    /// Useful for integrating with UI logging windows.
    /// </summary>
    public class ActionGameflowLogger : IGameflowLogger
    {
        private readonly Action<string> _logAction;

        public ActionGameflowLogger(Action<string> logAction)
        {
            _logAction = logAction ?? (msg => { });
        }

        public void LogDebug(string message) => _logAction($"[DEBUG] {message}");
        public void LogInfo(string message) => _logAction($"[INFO] {message}");
        public void LogWarning(string message) => _logAction($"[WARN] {message}");
        public void LogError(string message) => _logAction($"[ERROR] {message}");
        public void LogSection(string sectionName) => _logAction($"");
    }

    /// <summary>
    /// Console logger for debugging (useful for tests).
    /// </summary>
    public class ConsoleGameflowLogger : IGameflowLogger
    {
        public void LogDebug(string message) => Console.WriteLine($"[DEBUG] {message}");
        public void LogInfo(string message) => Console.WriteLine($"[INFO] {message}");
        public void LogWarning(string message) => Console.WriteLine($"[WARN] {message}");
        public void LogError(string message) => Console.WriteLine($"[ERROR] {message}");
        public void LogSection(string sectionName) => Console.WriteLine($"\n=== {sectionName} ===");
    }

    /// <summary>
    /// Null logger (no-op) for silent parsing.
    /// </summary>
    public class NullGameflowLogger : IGameflowLogger
    {
        public void LogDebug(string message) { }
        public void LogInfo(string message) { }
        public void LogWarning(string message) { }
        public void LogError(string message) { }
        public void LogSection(string sectionName) { }
    }
}
