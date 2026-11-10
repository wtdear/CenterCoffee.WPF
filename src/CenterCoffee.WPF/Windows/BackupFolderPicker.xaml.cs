using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using CenterCoffee.WPF.Common;

namespace CenterCoffee.WPF.Windows
{
    /// <summary>
    /// Выбор папки для резервной копии средствами WPF. Существующая сетка диалога
    /// подменяется нашим представлением содержимого: диалог открывает только папки,
    /// существующий каталог выбирается кнопкой, новый создаётся по имени.
    /// </summary>
    public sealed partial class BackupFolderPicker : Window
    {
        private readonly List<string> _pathParts = new List<string>();
        private string _currentDirectory;

        public BackupFolderPicker(string initialDirectory)
        {
            InitializeComponent();

            var start = Directory.Exists(initialDirectory)
                ? initialDirectory
                : Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            if (!Directory.Exists(start))
            {
                start = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            }

            _currentDirectory = Path.GetFullPath(start);
            SelectedDirectory = _currentDirectory;
            NavigateTo(_currentDirectory);
        }

        public string SelectedDirectory { get; private set; }

        private void NavigateTo(string path)
        {
            try
            {
                var info = new DirectoryInfo(path);
                if (!info.Exists)
                {
                    ShowStatus("Папка недоступна.");
                    return;
                }

                var items = info
                    .EnumerateDirectories()
                    .Select(directory => directory.FullName)
                    .OrderBy(name => name, StringComparer.CurrentCultureIgnoreCase)
                    .ToList();

                FolderList.ItemsSource = items;
                FolderList.SelectedIndex = -1;

                _currentDirectory = info.FullName;
                SelectedDirectory = info.FullName;
                CurrentPathText.Text = info.FullName;
                CurrentPathText.ToolTip = info.FullName;
                ShowStatus(items.Count == 0 ? "Здесь нет вложенных папок." : null);
                UpdatePathButtons();
            }
            catch (Exception exception) when (
                exception is UnauthorizedAccessException ||
                exception is IOException ||
                exception is ArgumentException ||
                exception is System.Security.SecurityException)
            {
                ShowStatus("Нет доступа к папке. Выберите другую.");
            }
        }

        private void UpdatePathButtons()
        {
            PathParts.Children.Clear();
            _pathParts.Clear();

            var parts = new List<string>();
            var current = new DirectoryInfo(_currentDirectory);
            while (current != null)
            {
                parts.Insert(0, current.Name.Length == 0 ? current.FullName : current.Name);
                current = current.Parent;
            }

            for (var index = 0; index < parts.Count; index++)
            {
                var target = string.Join(Path.DirectorySeparatorChar.ToString(), parts.Take(index + 1).ToArray());
                if (!Path.IsPathRooted(target))
                {
                    target = parts[index];
                }

                _pathParts.Add(target);

                if (index > 0)
                {
                    PathParts.Children.Add(new TextBlock
                    {
                        Text = "›",
                        Foreground = (System.Windows.Media.Brush)FindResource("MutedBrush"),
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(4, 0, 4, 0)
                    });
                }

                var button = new Button
                {
                    Content = parts[index],
                    Tag = target,
                    Padding = new Thickness(4, 2, 4, 2),
                    Style = (Style)FindResource("GhostButton")
                };

                button.Click += (sender, args) => NavigateTo((string)((Button)sender).Tag);
                PathParts.Children.Add(button);
            }
        }

        private void ShowStatus(string message)
        {
            StatusText.Text = message ?? string.Empty;
            StatusText.Visibility = string.IsNullOrEmpty(message) ? Visibility.Collapsed : Visibility.Visible;
        }

        private void OnFolderDoubleClick(object sender, MouseButtonEventArgs e)
        {
            var path = FolderList.SelectedItem as string;
            if (!string.IsNullOrEmpty(path))
            {
                NavigateTo(path);
            }
        }

        private void OnUpClick(object sender, RoutedEventArgs e)
        {
            var parent = Directory.GetParent(_currentDirectory);
            if (parent != null)
            {
                NavigateTo(parent.FullName);
            }
        }

        private void OnRefreshClick(object sender, RoutedEventArgs e)
        {
            NavigateTo(_currentDirectory);
        }

        private void OnCreateClick(object sender, RoutedEventArgs e)
        {
            var name = (NewFolderName.Text ?? string.Empty).Trim();
            string error;
            if (!InputParsing.TryValidateFolderName(name, out error))
            {
                ShowStatus(error);
                return;
            }

            try
            {
                var target = Path.Combine(_currentDirectory, name);
                Directory.CreateDirectory(target);
                NewFolderName.Text = string.Empty;
                NavigateTo(target);
            }
            catch (Exception exception) when (
                exception is IOException ||
                exception is UnauthorizedAccessException ||
                exception is ArgumentException ||
                exception is System.Security.SecurityException)
            {
                ShowStatus("Не удалось создать папку: " + exception.Message);
            }
        }

        private void OnSelectClick(object sender, RoutedEventArgs e)
        {
            SelectedDirectory = _currentDirectory;
            DialogResult = true;
        }

        private void OnCancelClick(object sender, RoutedEventArgs e)
        {
            DialogResult = false;
        }
    }
}
