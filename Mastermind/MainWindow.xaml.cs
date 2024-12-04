using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Threading;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Shapes;
using System.Text;
using Microsoft.VisualBasic;
using System.Xml.Linq;

namespace Mastermind
{
    public partial class MainWindow : Window
    {
        #region properties
        private bool _isCorrectGuess = false;
        private bool _isDebugMode = false;
        private int _gamePoints = 100;
        private int _attempts = 0;
        private int _maxAttempts = 10;
        private DispatcherTimer? _timer;
        private int _timerCount = 0;
        private const int _timerMaxCount = 10;
        private bool _enableResetOnEachTurn = false;
        private bool _isRunning = false;

        private List<(string name, List<SolidColorBrush> color)> _selectedColors = new List<(string name, List<SolidColorBrush> color)>();
        private readonly List<Label> _labels = new List<Label>();
        private readonly List<Ellipse> _choiceEllipses = new List<Ellipse>();
        private readonly Dictionary<string, List<SolidColorBrush>> _colorOptions = new Dictionary<string, List<SolidColorBrush>>()
        {
            { "red", new List<SolidColorBrush> { Brushes.WhiteSmoke, Brushes.Red, Brushes.DarkRed } },
            { "orange", new List<SolidColorBrush> { Brushes.WhiteSmoke, Brushes.Orange, Brushes.OrangeRed } },
            { "yellow", new List<SolidColorBrush> { Brushes.LightYellow, Brushes.Yellow, Brushes.Orange } },
            { "white", new List<SolidColorBrush> { Brushes.White, Brushes.WhiteSmoke, Brushes.Gray } },
            { "green", new List<SolidColorBrush> { Brushes.WhiteSmoke, Brushes.Green, Brushes.DarkGreen } },
            { "blue", new List<SolidColorBrush> { Brushes.FloralWhite, Brushes.Blue, Brushes.DarkBlue } },
        };

        private Ellipse? _selectedEllipse;

        private string[] _highScores = new string[15];
        private List<HighScore> _highScoresList = new List<HighScore>();

        private Player? _currentPlayer;
        private List<string> _playersStringList = new List<string>();
        private List<Player> _players = new List<Player>();

        private TaskCompletionSource<string>? _getPlayerNameTask;
        private bool _playersReady = false;
        //private int playerIndex = 
        private TaskCompletionSource<int>? _getMaxAttempsTask;

        #endregion

        public MainWindow()
        {
            InitializeComponent();

            Init();
        }


        private void Init()
        {
            //background moving gradient

            RadialGradientBrush backRadial = new RadialGradientBrush
            {
                GradientOrigin = new Point(0.4, 0.5),
                Center = new Point(0.5, 0.5),
                RadiusX = 2,
                RadiusY = 1.2
            };
            //radialGradientBrush.GradientStops.Add(new GradientStop(Colors.Black, 1.0));
            backRadial.GradientStops.Add(new GradientStop(Color.FromRgb(4, 3, 8), 1.0));
            backRadial.GradientStops.Add(new GradientStop(Color.FromRgb(34, 28, 64), 0.0));
            this.Background = backRadial;
            PointAnimation gradOrininPointAnimation = new PointAnimation
            {
                From = new Point(0.4, 0.5),
                To = new Point(0.6, 0.6),
                Duration = TimeSpan.FromSeconds(10),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            backRadial.BeginAnimation(RadialGradientBrush.GradientOriginProperty, gradOrininPointAnimation);

            DoubleAnimation stopPosition0Animation = new DoubleAnimation
            {
                From = 1.0,
                To = 0.4,
                Duration = TimeSpan.FromSeconds(10),
                AutoReverse = true,
                RepeatBehavior = RepeatBehavior.Forever
            };
            backRadial.GradientStops[0].BeginAnimation(GradientStop.OffsetProperty, stopPosition0Animation);

            RenewGame(startGame: false);

        }

        #region Game
        /// <summary>
        /// Resets everything and starts a new game
        /// </summary>
        private async void RenewGame(bool startGame = true)
        {
            // clear last game
            _timer?.Stop();
            mainGrid.Opacity = 0.2;
            _isCorrectGuess = false;
            _attempts = 0;
            _gamePoints = 100;
            pogingLabel.Text = $"POGING: {_attempts}/{_maxAttempts}";
            scoreLabel.Text = $"{_gamePoints}";
            historyStackPanel.Children.Clear();
            _choiceEllipses.Clear();
            _labels.Clear();
            _choiceEllipses.AddRange(new List<Ellipse>() { choiceEllipse0, choiceEllipse1, choiceEllipse2, choiceEllipse3 });
            _labels.AddRange(new List<Label>() { redLabel, orangeLabel, yellowLabel, whiteLabel, greenLabel, blueLabel });
            ResetAllBalls();

            // force start new game
            if (startGame)
            {
                _currentPlayer = null;
                _playersStringList.Clear();
                _players.Clear();

                List<string> nPlayers = await StartGame();

                for (int i = 0; i < _playersStringList.Count; i++)
                {
                    _players.Add(new Player { Id = i, Name = _playersStringList[i] });
                }

            }

            // select next player
            if (_players.Any())
            {
                if (_currentPlayer == null)
                    _currentPlayer = _players[0];
                else if (_players.ElementAtOrDefault(_currentPlayer.Id + 1) is Player player)
                {
                    _currentPlayer = player;
                }
                else
                {
                    // game end
                    _currentPlayer = null;
                    playerLabel.Text = "";

                    //todo: score laten zien ofzo

                }

            }
            // start if there is a player
            if (_currentPlayer != null)
            {
                playerLabel.Text = _currentPlayer.Name;
                _selectedColors = GenerateRandomColorCodes();
                debugTextBox.Text = $"Generated colorcode {string.Join(',', _selectedColors.Select(x => x.name))}";
                StartCountdown();
                _isRunning = true;
                mainGrid.Opacity = 1;
            }

        }

        private async Task<List<string>> StartGame()
        {
            //todo: showfunction
            nameInsertStack.Height = 25;
            _playersReady = false;

            do
            {
                string name;
                do
                {
                    nameInsertTextbox.Text = string.Empty;
                    nameInsertTextbox.Focus();
                    newItem.IsEnabled = false;
                    attemptsItem.IsEnabled = false;
                    mainGrid.IsEnabled = false;

                    nextPlayerLabel.Content = (_playersStringList.Any()) ? "Nog een player naam?" : "Geef een player naam:";

                    name = await GetPlayerName();

                    if (_playersReady)
                        break;

                    if (string.IsNullOrEmpty(name))
                    {
                        MessageBox.Show("Geef een geldige naam in:", "Foutieve invoer", MessageBoxButton.OK, MessageBoxImage.Warning);
                    }
                } while (string.IsNullOrEmpty(name));

                if (!_playersReady)
                    _playersStringList.Add(name);

            } while (!_playersReady);
            //todo: hidefunction

            mainGrid.IsEnabled = true;
            newItem.IsEnabled = true;
            attemptsItem.IsEnabled = true;
            nameInsertStack.Height = 0;
            return _playersStringList;
        }

        private async Task<string> GetPlayerName()
        {
            _getPlayerNameTask = new TaskCompletionSource<string>();
            return await _getPlayerNameTask.Task;
        }

        /// <returns>4 random colors</returns>
        private List<(string name, List<SolidColorBrush> color)> GenerateRandomColorCodes()
        {
            List<(string, List<SolidColorBrush>)> selectedOptions = new List<(string, List<SolidColorBrush>)>();

            var rand = new Random();
            for (int i = 0; i < 4; i++)
            {
                if (_colorOptions.ElementAt(rand.Next(0, _colorOptions.Count()))
                    is KeyValuePair<string, List<SolidColorBrush>> keyPair)
                {
                    selectedOptions.Add((keyPair.Key, keyPair.Value));
                }
            }
            return selectedOptions;
        }

        /// <summary>
        /// Checks the players input
        /// <para>1. Adds gamepoints.</para>
        /// <para>2. Adds the progress to history timeline.</para>
        /// <para>3. If correct, updates the game. SeeProp: <see cref="_isCorrectGuess"/></para>
        /// </summary>
        /// <param name="correctColors"></param>
        private void ControlColors(string[] correctColors)
        {
            if (_choiceEllipses.Any(x => x.Tag == null))
            {
                MessageBox.Show("Some values are not selected", "Invalid input", MessageBoxButton.OK, MessageBoxImage.Error);
                return;
            }
            int boxIndex = 0;
            int correctCount = 0;

            (List<SolidColorBrush> mainColor, bool isCorrectColor, bool isCorrectPosition)[] historyEntry
                = new (List<SolidColorBrush> mainColor, bool isCorrectColor, bool isCorrectPosition)[4];

            _choiceEllipses.ForEach(box =>
            {
                if (box.Tag is string value)
                {
                    int penaltyPoints = 2;
                    (List<SolidColorBrush> mainColor, bool isCorrectColor, bool isCorrectPosition) item
                            = new(_colorOptions[value], false, false);
                    if (correctColors.Contains(value))
                    {
                        item.isCorrectColor = true;
                        penaltyPoints = 1;

                        if (value.Equals(correctColors[boxIndex]))
                        {
                            item.isCorrectPosition = true;
                            penaltyPoints = 0;
                            correctCount++;
                        }
                    }
                    historyEntry[boxIndex] = item;
                    _gamePoints -= penaltyPoints;
                    if (_gamePoints <= 0)
                    {
                        _gamePoints = 0;
                    }

                }
                boxIndex++;
            });


            AddToHistory(historyEntry);

            if (correctCount == _choiceEllipses.Count)
            {
                _isCorrectGuess = true;
            }

        }
        private void AddToHistory((List<SolidColorBrush> mainColor, bool isCorrectColor, bool isCorrectPosition)[] historyEntry)
        {
            Grid grid = new Grid();
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(35, GridUnitType.Pixel) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.ColumnDefinitions.Add(new ColumnDefinition() { Width = new GridLength(1, GridUnitType.Star) });
            grid.RowDefinitions.Add(new RowDefinition() { Height = new GridLength(40) });

            Label label = new Label();
            label.Content = _attempts;
            label.FontSize = 15;
            label.FontWeight = FontWeights.Bold;
            label.Foreground = Brushes.White;
            label.HorizontalContentAlignment = HorizontalAlignment.Center;
            DoubleAnimation animation = new DoubleAnimation()
            {
                From = label.FontSize,
                To = label.FontSize * 1.5,
                Duration = TimeSpan.FromMilliseconds(150),
                AutoReverse = true,
                EasingFunction = new BounceEase
                {
                    Bounces = 2,
                    Bounciness = 5
                }
            };
            label.BeginAnimation(TextBox.FontSizeProperty, animation);
            label.SetValue(Grid.ColumnProperty, 0);
            grid.Children.Add(label);

            for (int i = 1; i < 5; i++)
            {
                int hIndex = i - 1;
                Ellipse circle = new Ellipse();
                circle.Width = 35;
                circle.Height = 35;
                circle.Fill = GetGradientBrush(historyEntry[hIndex].mainColor, new Point(0.3, 0.3));
                circle.HorizontalAlignment = HorizontalAlignment.Center;
                circle.VerticalAlignment = VerticalAlignment.Center;
                circle.SetValue(Grid.ColumnProperty, i);

                if (historyEntry[hIndex].isCorrectPosition)
                {
                    circle.StrokeThickness = 2;
                    circle.Stroke = Brushes.Red;
                }
                else if (historyEntry[hIndex].isCorrectColor)
                {
                    circle.StrokeThickness = 2;
                    circle.Stroke = Brushes.Wheat;
                }

                // animation
                TranslateTransform translateTransform = new TranslateTransform(0, 0);
                circle.RenderTransform = translateTransform;
                grid.Children.Add(circle);

                DoubleAnimation dropAnimation = new DoubleAnimation
                {
                    From = -70,
                    To = 0,
                    Duration = TimeSpan.FromMilliseconds(500),
                    EasingFunction = new BounceEase
                    {
                        Bounces = 2,
                        Bounciness = 2,
                        EasingMode = EasingMode.EaseOut
                    }
                };
                translateTransform.BeginAnimation(TranslateTransform.YProperty, dropAnimation);
            }

            historyStackPanel.Children.Insert(0, grid);

            ScrollToTop();
        }
        private void ResetAllBalls()
        {
            _selectedEllipse = null;
            for (int i = 0; i < _labels.Count(); i++)
            {
                _labels[i].Opacity = 0.4;
            }
            for (int i = 0; i < _choiceEllipses.Count(); i++)
            {
                _choiceEllipses[i].StrokeThickness = 1;
                _choiceEllipses[i].Stroke = Brushes.LightGray;
                _choiceEllipses[i].Fill = Brushes.Transparent;
                _choiceEllipses[i].Tag = null;
            }
        }
        private void SelectBall(Ellipse ellipse)
        {
            _selectedEllipse = ellipse;
            for (int i = 0; i < _labels.Count(); i++)
            {
                _labels[i].Opacity = 0.4;
            }

            for (int i = 0; i < _choiceEllipses.Count(); i++)
            {
                if (_choiceEllipses[i].Name == ellipse.Name)
                {
                    // select label
                    _choiceEllipses[i].StrokeThickness = 2;
                    _choiceEllipses[i].Stroke = Brushes.White;

                    // color label
                    if (_choiceEllipses[i].Tag is string value)
                    {
                        if (_labels.FirstOrDefault(x => (string)x.Tag == value) is Label label)
                        {
                            label.Opacity = 1;
                        }
                    }
                }
                else
                {
                    // clear the rest
                    _choiceEllipses[i].StrokeThickness = 0.2;
                    if (_choiceEllipses[i].Tag is not null)
                    {
                        _choiceEllipses[i].StrokeThickness = 0;
                        _choiceEllipses[i].Stroke = Brushes.Transparent;
                    }
                }
            }

        }

        /// <summary>
        /// Ends the game on win/loose. Gives an option to exit the game.
        /// </summary>
        /// <param name="isVictory"></param>
        private void EndGame(bool isVictory)
        {
            _timer?.Stop();
            _isRunning = false;

            string title = "YOU LOOSE";
            string message = $"You failed!! De correcte code was {string.Join(' ', _selectedColors.Select(x => x.name))}. ";
            MessageBoxImage icon = MessageBoxImage.Question;

            if (isVictory)
            {
                title = "WINNER";
                message = $"Code is gekraakt in {_attempts} pogingen!";
                icon = MessageBoxImage.Information;
            }

            //if (_gamePoints > 0)
            //{
            CalculateHighScores(_currentPlayer.Name, _attempts, _gamePoints);
            //}

            MessageBox.Show(message, title, MessageBoxButton.OK, icon);

            RenewGame(startGame: false);

        }

        private void CalculateHighScores(string currentPlayer, int pogingen, int score)
        {
            HighScore highScore = new HighScore { Name = currentPlayer, Pogingen = pogingen, Score = score };

            if (_currentPlayer != null)
                _players[_currentPlayer.Id].HighScore = highScore;

            //if (_highScoresList.Any(x => x.Name.Equals(currentPlayer)
            //    && ((x.Score > score) || (x.Score == score && x.Pogingen < pogingen))))
            //    return;

            //_highScoresList.RemoveAll(x => x.Name.Equals(currentPlayer));


            _highScoresList.Add(highScore);
            _highScoresList = _highScoresList.OrderByDescending(x => x.Score).Take(15).ToList();

            List<string> newScores = new List<string>();
            foreach (HighScore hScore in _highScoresList)
            {
                string scoreString = $"{hScore.Name} - {hScore.Pogingen} pogingen - {hScore.Score}/100";
                newScores.Add(scoreString);
            }

            _highScores = newScores.ToArray();
        }

        #endregion

        #region Timer
        private void AttemptFinishedTimer(object? sender, EventArgs e)
        {
            _timerCount++;
            timeLabel.Text = $"{(_timerMaxCount + 1) - _timerCount}";
            if (_attempts >= _maxAttempts)
                EndGame(isVictory: false);

            StopCountdown();
        }
        /// <summary>
        /// The player has only 10 seconds to complete one phase. The timer starts at 1 and ends at 10
        /// </summary>
        private void StartCountdown()
        {
            _timer?.Stop();
            _timerCount = 1;
            timeLabel.Text = $"{(_timerMaxCount + 1) - _timerCount}";
            _timer = new DispatcherTimer();
            _timer.Tick += AttemptFinishedTimer;
            _timer.Interval = TimeSpan.FromSeconds(1);
            _timer.Start();
        }
        /// <summary>
        /// When the running timer reaches 10, the attempt will increase. After, the coundown will start again.
        /// </summary>
        private void StopCountdown()
        {
            if (_timerCount == _timerMaxCount + 1)
            {
                _attempts++;

                _gamePoints -= 8; // straf punten
                if (_gamePoints <= 0)
                    _gamePoints = 0;

                pogingLabel.Text = $"POGING: {_attempts}/{_maxAttempts}";
                scoreLabel.Text = $"{_gamePoints}";

                if (_attempts >= _maxAttempts)
                    EndGame(isVictory: false);
                else
                    StartCountdown();
            }
        }
        #endregion

        #region Extra Functions
        private GradientBrush GetGradientBrush(List<SolidColorBrush> colors, Point origin)
        {
            return new RadialGradientBrush
            {
                GradientOrigin = origin,
                Center = new Point(0.5, 0.5),
                GradientStops = new GradientStopCollection
                    {
                        new GradientStop(colors[0].Color, 0.0),
                        new GradientStop(colors[1].Color, 0.5),
                        new GradientStop(colors[2].Color, 1.0)
                    }
            };
        }

        /// <summary>
        /// Scoll the history timeline to TOP
        /// </summary>
        private void ScrollToTop()
        {
            scrollViewer.ScrollToTop();
            //todo: smooth maken
        }
        private void AnimateButton(Button button, double from, double to, int speedMs, bool autoReverse = false)
        {
            ScaleTransform scaleTransform = new ScaleTransform(from, to);
            button.RenderTransform = scaleTransform;
            button.RenderTransformOrigin = new Point(0.5, 0.5);
            DoubleAnimation animation = new DoubleAnimation()
            {
                From = from,
                To = to,
                AutoReverse = autoReverse,
                Duration = TimeSpan.FromMilliseconds(speedMs),
            };
            scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, animation);
            scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, animation);
        }
        #endregion

        #region Events
        private void validateButton_Click(object sender, RoutedEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Button button)
            {
                AnimateButton(button, from: 1, to: 0.9, speedMs: 80, autoReverse: true);
            }

            if (_isCorrectGuess || _attempts >= _maxAttempts)
                return;

            _attempts++;
            if (_selectedColors.Any() && _selectedColors.Count == 4)
            {
                ControlColors(_selectedColors.Select(x => x.name).ToArray());
                pogingLabel.Text = $"POGING: {_attempts}/{_maxAttempts}";
                scoreLabel.Text = $"{_gamePoints}";

                if (!_isCorrectGuess)
                {
                    if (_attempts >= _maxAttempts)
                    {
                        EndGame(isVictory: false);
                    }
                    else
                    {
                        if (_enableResetOnEachTurn)
                            ResetAllBalls();

                        StartCountdown();
                    }
                }
                else
                {
                    EndGame(isVictory: true);
                }
            }
        }
        private void validateButton_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Button button)
                AnimateButton(button, from: 1, to: 1.05, speedMs: 80);
        }
        private void validateButton_MouseLeave(object sender, MouseEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Button button)
                AnimateButton(button, from: 1.05, to: 1, speedMs: 80);

        }
        private void Label_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Label label)
                label.Opacity = 1;
        }
        private void Label_MouseLeave(object sender, MouseEventArgs e)
        {
            if (sender is Label label)
            {
                for (int i = 0; i < _labels.Count(); i++)
                {
                    if (_selectedEllipse != null && _selectedEllipse.Tag is string value && (string)_labels[i].Tag == value)
                    {
                        _labels[i].Opacity = 1;
                    }
                    else
                    {
                        _labels[i].Opacity = 0.4;
                    }
                }

            }
        }
        private void Label_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Label label && label.Tag is string value)
            {
                label.Opacity = 1;

                for (int i = 0; i < _labels.Count(); i++)
                {
                    if (!_labels[i].Name.Equals(label.Name))
                    {
                        _labels[i].Opacity = 0.4;
                    }
                }

                if (_selectedEllipse == null)
                    _selectedEllipse = _choiceEllipses.FirstOrDefault();

                if (_selectedEllipse != null &&
                    _colorOptions.FirstOrDefault(x => x.Key == value)
                        is (string name, List<SolidColorBrush> colors) foundColor)
                {
                    _selectedEllipse.StrokeThickness = 2;
                    _selectedEllipse.Stroke = Brushes.White;
                    _selectedEllipse.Fill = GetGradientBrush(foundColor.Value, new Point(0.3, 0.3));
                    _selectedEllipse.Tag = value;

                    ScaleTransform scaleTransform = new ScaleTransform(1, 1);
                    _selectedEllipse.RenderTransform = scaleTransform;
                    _selectedEllipse.RenderTransformOrigin = new Point(0.5, 0.5);

                    DoubleAnimation scaleAnimation = new DoubleAnimation()
                    {
                        From = 1,
                        To = 1.05,
                        Duration = TimeSpan.FromMilliseconds(120),
                        AutoReverse = true,
                        EasingFunction = new BounceEase()
                        {
                            Bounces = 1,
                            Bounciness = 2,
                            EasingMode = EasingMode.EaseOut
                        }
                    };

                    scaleTransform.BeginAnimation(ScaleTransform.ScaleXProperty, scaleAnimation);
                    scaleTransform.BeginAnimation(ScaleTransform.ScaleYProperty, scaleAnimation);
                }
            }

        }
        private void Ellipse_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Ellipse ellipse)
            {
                int index = -1;

                if (ellipse.Tag is string value &&
                    _colorOptions.Select(x => x.Key).ToList().IndexOf(value) is int foundInd &&
                    foundInd != -1)
                {
                    index = foundInd;
                }

                Label? foundLabel = null;
                if (e.Delta > 0)
                {
                    foundLabel = _labels.ElementAtOrDefault(index + 1);
                }
                else if (e.Delta < 0)
                {
                    foundLabel = _labels.ElementAtOrDefault(index - 1);
                }

                if (foundLabel is not null)
                {
                    Label_MouseLeftButtonUp(foundLabel, null!);
                }

            }
        }
        private void Ellipse_MouseEnter(object sender, MouseEventArgs e)
        {
            if (!_isRunning)
                return;

            if (sender is Ellipse ellipse)
            {
                SelectBall(ellipse);
            }
        }
        private void OnScoreLabelsTextChanged(object sender, TextChangedEventArgs e)
        {
            if (sender is TextBox box && mainWindow.IsLoaded &&
                (sender == scoreLabel || (sender == timeLabel && timeLabel.Text.Equals(_maxAttempts.ToString()))))
            {
                box.FontSize = 30;
                DoubleAnimation animation = new DoubleAnimation()
                {
                    From = box.FontSize,
                    To = box.FontSize * 1.1,
                    Duration = TimeSpan.FromMilliseconds(150),
                    AutoReverse = true,
                    FillBehavior = FillBehavior.Stop,
                    EasingFunction = new BounceEase
                    {
                        Bounces = 2,
                        Bounciness = 5
                    }
                };

                animation.Completed += (ob, arg) =>
                {

                    box.FontSize = 30;
                };

                box.BeginAnimation(TextBox.FontSizeProperty, animation);
            }
        }
        private void menuItemClicked(object sender, RoutedEventArgs e)
        {
            if (sender is MenuItem menuIten)
            {
                switch (menuIten.Name)
                {
                    case "newItem":
                        RenewGame(startGame: true);
                        break;
                    case "highScoreItem":

                        StringBuilder sb = new StringBuilder();
                        foreach (string score in _highScores)
                        {
                            sb.AppendLine(score);
                        }
                        MessageBox.Show(sb.ToString(), "Mastermind highscores", MessageBoxButton.OK, MessageBoxImage.Information);
                        break;
                    case "exitItem":
                        ExitApp();
                        break;
                    case "attemptsItem":

                        string attempts = Interaction.InputBox("Geef het aantal attempts in tussen min 3 en max 20:", "Pogingen", "", 500);
                        int newAttempt = _maxAttempts;
                        while (string.IsNullOrEmpty(attempts) || !int.TryParse(attempts, out newAttempt) || newAttempt < 3 || newAttempt > 20)
                        {
                            MessageBox.Show("Geef een geldige poging:", "Foutieve invoer");
                            attempts = Interaction.InputBox("Geef het aantal attempts in tussen min 3 en max 20:", "Pogingen", "", 500);
                        }
                        _maxAttempts = newAttempt;
                        RenewGame(startGame: _isRunning);
                        break;
                }

            }
        }
        private void selectNameButton_Click(object sender, RoutedEventArgs e)
        {
            _getPlayerNameTask?.TrySetResult(nameInsertTextbox.Text);
        }
        private void selectReadyButton_Click(object sender, RoutedEventArgs e)
        {

            if (!_playersStringList.Any())
            {
                MessageBox.Show("Er is nog geen speler geselecteerd");
            }
            else
            {
                _playersReady = true;
                _getPlayerNameTask?.TrySetResult("");
            }
        }

        #endregion

        #region Exit
        /// <summary>
        /// Check the color code in debug mode
        /// </summary>
        private void ToggleDebug()
        {
            if (debugTextBox.Visibility == Visibility.Visible)
            {
                debugTextBox.Visibility = Visibility.Collapsed;
                _isDebugMode = false;
            }
            else
            {
                _isDebugMode = true;
                debugTextBox.Visibility = Visibility.Visible;
            }
        }
        private void ExitApp()
        {
            this.Close();
        }
        private void mainWindow_KeyDown(object sender, KeyEventArgs e)
        {
#if DEBUG
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || Keyboard.IsKeyDown(Key.RightCtrl)) &&
                 e.Key == Key.F12
                 )
            {
                ToggleDebug();
            }
#endif
        }
        private void mainWindow_Closing(object sender, System.ComponentModel.CancelEventArgs e)
        {
            if (MessageBox.Show("Wilt u het spel vroegtijdig beeindigen?", $"poging {_attempts}/10", MessageBoxButton.YesNo, MessageBoxImage.Warning) == MessageBoxResult.No)
                e.Cancel = true;
        }


        #endregion

        private void nameInsertTextbox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                selectNameButton_Click(null!, null!);
            }
        }
    }


    class HighScore
    {
        public int Pogingen { get; set; }
        public int Score { get; set; }
        public string Name { get; set; }
    }

    class Player
    {
        public int Id { get; set; }
        public string? Name { get; set; }
        public HighScore? HighScore { get; set; }
    }
}