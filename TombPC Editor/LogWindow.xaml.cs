using System;
using System.IO;
using System.Text;
using System.Windows;
using Microsoft.Win32;

namespace TombPC_Editor
{
    public partial class LogWindow : Window
    {
        private StringBuilder _logBuffer = new();

        public LogWindow()
        {
            InitializeComponent();
        }

        public void AppendLog(string line)
        {
            _logBuffer.AppendLine(line);
            Dispatcher.Invoke(() =>
            {
                LogTextBox.AppendText(line + Environment.NewLine);
                LogTextBox.ScrollToEnd();
            });
        }

        public string GetFullLog() => _logBuffer.ToString();

        private void CopyAll_Click(object sender, RoutedEventArgs e)
        {
            if (_logBuffer.Length > 0)
            {
                Clipboard.SetText(_logBuffer.ToString());
                MessageBox.Show("Log copied to clipboard.", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
            }
        }

        private void Save_Click(object sender, RoutedEventArgs e)
        {
            var dlg = new SaveFileDialog();
            dlg.Filter = "Log files|*.log|Text files|*.txt|All files|*.*";
            dlg.FileName = $"tombpc_parse_{DateTime.Now:yyyyMMdd_HHmmss}.log";
            if (dlg.ShowDialog(this) == true)
            {
                try
                {
                    File.WriteAllText(dlg.FileName, _logBuffer.ToString(), Encoding.UTF8);
                    MessageBox.Show($"Log saved to:\n{dlg.FileName}", "Success", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                catch (Exception ex)
                {
                    MessageBox.Show($"Failed to save log: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
        }

        private void Close_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}
