using Microsoft.Win32;
using System;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;

namespace WPFModelViewer
{
    internal sealed class AssimpExportDialog : Window
    {
        private readonly Assimp.ExportFormatDescription[] formats;
        private readonly ComboBox formatBox;
        private readonly TextBox destinationBox;
        private readonly CheckBox animationBox;

        public Assimp.ExportFormatDescription SelectedFormat =>
            formats[Math.Max(formatBox.SelectedIndex, 0)];
        public string DestinationFile => Path.ChangeExtension(destinationBox.Text.Trim(), SelectedFormat.FileExtension);
        public bool IncludeAnimations => animationBox.IsChecked == true;

        public AssimpExportDialog(Assimp.ExportFormatDescription[] supportedFormats, string suggestedName)
        {
            formats = supportedFormats.OrderBy(format => format.Description).ToArray();
            Title = "Export Geometry";
            Width = 620;
            Height = 245;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ShowInTaskbar = false;

            Grid grid = new Grid { Margin = new Thickness(14) };
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            for (int i = 0; i < 5; i++)
                grid.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            addLabel(grid, "Format", 0);
            formatBox = new ComboBox { Margin = new Thickness(8, 0, 0, 8), MinWidth = 360 };
            foreach (Assimp.ExportFormatDescription format in formats)
                formatBox.Items.Add($"{format.Description} (*.{format.FileExtension})");
            int preferred = Array.FindIndex(formats, format => format.FormatId == "collada");
            formatBox.SelectedIndex = preferred >= 0 ? preferred : 0;
            formatBox.SelectionChanged += formatChanged;
            Grid.SetRow(formatBox, 0);
            Grid.SetColumn(formatBox, 1);
            Grid.SetColumnSpan(formatBox, 2);
            grid.Children.Add(formatBox);

            addLabel(grid, "Destination", 1);
            destinationBox = new TextBox { Margin = new Thickness(8, 0, 8, 8), VerticalContentAlignment = VerticalAlignment.Center };
            destinationBox.Text = Path.Combine(AppContext.BaseDirectory,
                suggestedName + "." + formats[formatBox.SelectedIndex].FileExtension);
            Grid.SetRow(destinationBox, 1);
            Grid.SetColumn(destinationBox, 1);
            grid.Children.Add(destinationBox);

            Button browse = new Button { Content = "Browse…", MinWidth = 82, Margin = new Thickness(0, 0, 0, 8) };
            browse.Click += browseClicked;
            Grid.SetRow(browse, 1);
            Grid.SetColumn(browse, 2);
            grid.Children.Add(browse);

            animationBox = new CheckBox
            {
                Content = "Include animations",
                Margin = new Thickness(8, 3, 0, 4),
                IsChecked = false,
                ToolTip = "Exports animation clips when the selected Assimp format supports them"
            };
            Grid.SetRow(animationBox, 2);
            Grid.SetColumn(animationBox, 1);
            Grid.SetColumnSpan(animationBox, 2);
            grid.Children.Add(animationBox);

            TextBlock note = new TextBlock
            {
                Text = "Only checked (visible) nodes in the selected subtree will be exported.",
                Margin = new Thickness(8, 0, 0, 12),
                Foreground = SystemColors.GrayTextBrush
            };
            Grid.SetRow(note, 3);
            Grid.SetColumn(note, 1);
            Grid.SetColumnSpan(note, 2);
            grid.Children.Add(note);

            StackPanel buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right
            };
            Button export = new Button { Content = "Export", IsDefault = true, MinWidth = 82, Margin = new Thickness(0, 0, 8, 0) };
            export.Click += exportClicked;
            Button cancel = new Button { Content = "Cancel", IsCancel = true, MinWidth = 82 };
            buttons.Children.Add(export);
            buttons.Children.Add(cancel);
            Grid.SetRow(buttons, 4);
            Grid.SetColumn(buttons, 1);
            Grid.SetColumnSpan(buttons, 2);
            grid.Children.Add(buttons);

            Content = grid;
        }

        private static void addLabel(Grid grid, string text, int row)
        {
            TextBlock label = new TextBlock
            {
                Text = text,
                Margin = new Thickness(0, 4, 0, 8),
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetRow(label, row);
            Grid.SetColumn(label, 0);
            grid.Children.Add(label);
        }

        private void formatChanged(object sender, SelectionChangedEventArgs e)
        {
            if (destinationBox != null && formatBox.SelectedIndex >= 0 && destinationBox.Text.Trim() != "")
                destinationBox.Text = Path.ChangeExtension(destinationBox.Text,
                    formats[formatBox.SelectedIndex].FileExtension);
        }

        private void browseClicked(object sender, RoutedEventArgs e)
        {
            SaveFileDialog dialog = new SaveFileDialog
            {
                Title = "Export Geometry",
                AddExtension = true,
                OverwritePrompt = true,
                InitialDirectory = AppContext.BaseDirectory,
                Filter = string.Join("|", formats.Select(format =>
                    $"{format.Description} (*.{format.FileExtension})|*.{format.FileExtension}")),
                FilterIndex = formatBox.SelectedIndex + 1,
                FileName = Path.GetFileName(destinationBox.Text)
            };
            if (dialog.ShowDialog(this) != true)
                return;
            formatBox.SelectedIndex = dialog.FilterIndex - 1;
            destinationBox.Text = dialog.FileName;
        }

        private void exportClicked(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(destinationBox.Text) || !Path.IsPathRooted(destinationBox.Text))
            {
                MessageBox.Show(this, "Choose a full destination path with Browse before exporting.",
                    "Export Geometry", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }
            DialogResult = true;
        }
    }
}
