using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Input;

namespace ASidekick
{
    public partial class MainWindow : Window, INotifyPropertyChanged
    {
        [DllImport("user32.dll")]
        private static extern bool SetForegroundWindow(IntPtr hWnd);

        [DllImport("user32.dll")]
        private static extern IntPtr GetDesktopWindow();

        public ObservableCollection<ShortcutItem> Shortcuts { get; set; } = new ObservableCollection<ShortcutItem>();
        public ObservableCollection<ShortcutRow> ShortcutRows { get; } = new ObservableCollection<ShortcutRow>();
        public ICommand LaunchCommand { get; }
        public string StatusMessage { get; private set; } = string.Empty;
        public bool HasSecondaryDisplay { get; }
        private int _secondaryDisplayWidth;
        private bool _showTitleOverlay;

        public bool ShowTitleOverlay
        {
            get => _showTitleOverlay;
            set
            {
                if (_showTitleOverlay == value)
                {
                    return;
                }

                _showTitleOverlay = value;
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(ShowTitleOverlay)));
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public MainWindow()
        {
            InitializeComponent();
            LaunchCommand = new RelayCommand<ShortcutItem>(ExecuteLaunch);

            HasSecondaryDisplay = PositionOnSecondaryScreen();
            LoadShortcuts();
            DataContext = this;
        }

        private bool PositionOnSecondaryScreen()
        {
            var screens = System.Windows.Forms.Screen.AllScreens;
            var secondaryDisplay = screens.FirstOrDefault(s => !s.Primary);

            if (secondaryDisplay == null)
            {
                return false;
            }

            var bounds = secondaryDisplay.Bounds;
            _secondaryDisplayWidth = bounds.Width;

            WindowStartupLocation = WindowStartupLocation.Manual;
            Left = bounds.Left;
            Top = bounds.Top;
            Width = bounds.Width;
            Height = bounds.Height;

            return true;
        }

        private void MainWindow_Loaded(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Maximized;
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void LoadShortcuts()
        {
            string configPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "shortcuts.json");

            if (!File.Exists(configPath))
            {
                StatusMessage = "shortcuts.json was not found beside ASidekick.exe.";
                return;
            }

            try
            {
                string jsonString = File.ReadAllText(configPath);
                var list = JsonSerializer.Deserialize<List<ShortcutItem>>(jsonString);
                if (list == null)
                {
                    StatusMessage = "shortcuts.json must contain a JSON array of shortcuts.";
                    return;
                }

                foreach (var item in list)
                {
                    if (string.IsNullOrWhiteSpace(item.Title) || string.IsNullOrWhiteSpace(item.TargetPath))
                    {
                        Debug.WriteLine("Skipped a shortcut with a missing title or target.");
                        continue;
                    }

                    if (!string.IsNullOrWhiteSpace(item.ImagePath) && !Path.IsPathRooted(item.ImagePath))
                    {
                        item.ImagePath = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.ImagePath));
                    }

                    Shortcuts.Add(item);
                }

                StatusMessage = Shortcuts.Count == 0
                    ? "No shortcuts found. Add items to shortcuts.json."
                    : $"{Shortcuts.Count} shortcuts ready";
                BuildShortcutRows();
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"Failed reading shortcut catalog configs: {ex}");
                StatusMessage = $"Could not load shortcuts.json: {ex.Message}";
            }
        }

        private void BuildShortcutRows()
        {
            ShortcutRows.Clear();
            if (Shortcuts.Count == 0)
            {
                return;
            }

            int availableColumns = Math.Max(1, (_secondaryDisplayWidth - 60) / 224);
            int columns = Shortcuts.Count > 9 && availableColumns >= 5 ? 5 : 4;

            for (int i = 0; i < Shortcuts.Count; i += columns)
            {
                var items = Shortcuts.Skip(i).Take(columns).ToList();
                ShortcutRows.Add(new ShortcutRow(items, columns));
            }
        }

        private void ExecuteLaunch(ShortcutItem item)
        {
            if (item == null) return;

            try
            {
                var startInfo = new ProcessStartInfo
                {
                    FileName = item.TargetPath,
                    Arguments = item.Arguments,
                    UseShellExecute = true
                };

                if (!string.IsNullOrWhiteSpace(item.WorkingDirectory))
                {
                    startInfo.WorkingDirectory = Path.IsPathRooted(item.WorkingDirectory)
                        ? item.WorkingDirectory
                        : Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, item.WorkingDirectory));
                }
                else if (Path.IsPathRooted(item.TargetPath))
                {
                    startInfo.WorkingDirectory = Path.GetDirectoryName(item.TargetPath) ?? string.Empty;
                }

                if (item.ForceMainDisplay)
                {
                    SetForegroundWindow(GetDesktopWindow());
                }

                Process.Start(startInfo);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show(
                    $"Failed to execute target payload:\n{ex.Message}",
                    "Launcher Error",
                    System.Windows.MessageBoxButton.OK,
                    System.Windows.MessageBoxImage.Error);
            }
        }
    }

    public class ShortcutRow
    {
        public IReadOnlyList<ShortcutItem> Items { get; }
        public int Columns { get; }

        public ShortcutRow(IReadOnlyList<ShortcutItem> items, int columns)
        {
            Items = items;
            Columns = columns;
        }
    }

    public class ShortcutItem
    {
        public string Title { get; set; } = string.Empty;
        public string TargetPath { get; set; } = string.Empty;
        public string ImagePath { get; set; } = string.Empty;
        public string Arguments { get; set; } = string.Empty;
        public string WorkingDirectory { get; set; } = string.Empty;
        public bool ForceMainDisplay { get; set; }
    }

    public class RelayCommand<T> : ICommand where T : class
    {
        private readonly Action<T> _execute;
        public RelayCommand(Action<T> execute) => _execute = execute;
        public bool CanExecute(object? parameter) => true;
        public void Execute(object? parameter) => _execute((parameter as T)!);
        public event EventHandler? CanExecuteChanged { add { } remove { } }
    }
}
