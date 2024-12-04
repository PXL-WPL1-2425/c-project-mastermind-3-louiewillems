using System;


using System.Windows;
using System.Windows.Controls;

using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Shapes;

namespace Mastermind
{
    public partial class HintMessageBox : Window
    {
        public int Result { get; private set; } = 0;

        private readonly List<HistoryEntry> _lastHistoryEntries;
        private readonly List<(string name, List<SolidColorBrush> color)> _correctColors;

        public HintMessageBox(List<HistoryEntry> lastEntries, List<(string name, List<SolidColorBrush> color)> correctColors)
        {
            InitializeComponent();

            _lastHistoryEntries = lastEntries;
            _correctColors = correctColors;
        }

        private void ButtonClicked(object sender, RoutedEventArgs e)
        {
            if (sender is Button button)
            {
                switch (button.Name)
                {
                    case "correctColorButton":
                        correctColorButton.Visibility = Visibility.Collapsed;
                        correctPositionButton.Visibility = Visibility.Collapsed;
                        Result = 15;

                        (string name, List<SolidColorBrush> color) result = GetColorHint();
                        if (result != default)
                        {
                            hintLabel.Content = $"KLEUR: {result.name.ToUpper()}";
                            cancelButton.Content = "OK";
                        }
                        else
                            DialogResult = false;

                        break;
                    case "correctPositionButton":
                        correctColorButton.Visibility = Visibility.Collapsed;
                        correctPositionButton.Visibility = Visibility.Collapsed;
                        Result = 25;
                        (int index, string name, List<SolidColorBrush> color) result2 = GetColorPositionHint();

                        if (result2 != default)
                        {
                            hintLabel.Content = $"KLEUR: {result2.name.ToUpper()}, POSITIE : {result2.index + 1}";
                            cancelButton.Content = "OK";
                        }
                        else
                            DialogResult = false;

                        cancelButton.Content = "OK";
                        break;
                    case "cancelButton":
                        DialogResult = (Result > 0) ? true : false;
                        break;
                }
            }
        }

        private (string name, List<SolidColorBrush> color) GetColorHint()
        {
            if(_lastHistoryEntries == null || !_lastHistoryEntries.Any())
            {
                return _correctColors[new Random().Next(0, _correctColors.Count)];
            }

            (string name, List<SolidColorBrush> color) result = default;

            List<int> selected = _lastHistoryEntries
                 .Select((item, index) => new
                 {
                     Entry = item,
                     Index = index
                 })
                 .Where(x => !x.Entry.IsCorrectPosition).Select(x => x.Index)
                 .ToList();

            if (selected.Any())
            {
                int randomColor = selected[new Random().Next(0, selected.Count())];
                return _correctColors[randomColor];
            }
            return result;
        }
        private (int Index, string name, List<SolidColorBrush> color) GetColorPositionHint()
        {
            if (_lastHistoryEntries == null || !_lastHistoryEntries.Any())
            {
                int index = new Random().Next(0, _correctColors.Count);
                var randomResult = _correctColors[index];
                return  (index, randomResult.name, randomResult.color);
            }

            (int Index, string name, List<SolidColorBrush> color) result = default;

            int foundIndex = _lastHistoryEntries.FindIndex(x => !x.IsCorrectPosition);

            var foundColor = _correctColors.ElementAtOrDefault(foundIndex);
            if (foundColor != default)
            {
                result = (foundIndex, foundColor.name, foundColor.color);
            }

            return result;
        }

    }
}
