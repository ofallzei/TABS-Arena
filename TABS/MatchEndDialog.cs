using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace TABS
{
    public class MatchEndDialog : Window
    {
        public bool StartNewGame { get; private set; }
        public bool ContinueSelected { get; private set; }

        public MatchEndDialog(string title, string message, string question, string newGameText, string continueText)
        {
            Title = title;
            Width = 440;
            Height = 245;
            MinWidth = 440;
            MinHeight = 245;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = Brushes.Transparent;
            UseLayoutRounding = true;

            var outer = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(83, 165, 206)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12)
            };

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleRow = new Grid { Margin = new Thickness(0, 0, 0, 14) };
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleRow.MouseLeftButtonDown += (s, e) =>
            {
                try { DragMove(); } catch { }
            };

            var titleText = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            titleRow.Children.Add(titleText);

            var closeButton = CreateButton("X", new SolidColorBrush(Color.FromRgb(42, 48, 58)), Brushes.White, 32, 30);
            closeButton.FontSize = 12;
            closeButton.Click += (s, e) =>
            {
                StartNewGame = false;
                DialogResult = false;
            };
            Grid.SetColumn(closeButton, 1);
            titleRow.Children.Add(closeButton);
            root.Children.Add(titleRow);

            var body = new StackPanel { VerticalAlignment = VerticalAlignment.Center };
            body.Children.Add(new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(232, 238, 246)),
                FontSize = 15,
                FontWeight = FontWeights.SemiBold,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21,
                Margin = new Thickness(0, 0, 0, 10)
            });
            body.Children.Add(new TextBlock
            {
                Text = question,
                Foreground = new SolidColorBrush(Color.FromRgb(176, 190, 204)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 19
            });
            Grid.SetRow(body, 1);
            root.Children.Add(body);

            var buttons = new StackPanel
            {
                Orientation = Orientation.Horizontal,
                HorizontalAlignment = HorizontalAlignment.Right,
                Margin = new Thickness(0, 18, 0, 0)
            };

            var continueButton = CreateButton(continueText, new SolidColorBrush(Color.FromRgb(42, 48, 58)), Brushes.White, 124, 36);
            continueButton.Margin = new Thickness(0, 0, 10, 0);
            continueButton.Click += (s, e) =>
            {
                StartNewGame = false;
                ContinueSelected = true;
                DialogResult = false;
            };

            var newGameButton = CreateButton(newGameText, new SolidColorBrush(Color.FromRgb(83, 165, 206)), Brushes.White, 124, 36);
            newGameButton.Click += (s, e) =>
            {
                StartNewGame = true;
                DialogResult = true;
            };

            buttons.Children.Add(continueButton);
            buttons.Children.Add(newGameButton);
            Grid.SetRow(buttons, 2);
            root.Children.Add(buttons);

            outer.Child = root;
            Content = outer;

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    StartNewGame = true;
                    DialogResult = true;
                    e.Handled = true;
                }
                else if (e.Key == Key.Escape)
                {
                    StartNewGame = false;
                    DialogResult = false;
                    e.Handled = true;
                }
            };
        }

        private static Button CreateButton(object content, Brush background, Brush foreground, double width, double height)
        {
            return new Button
            {
                Content = content,
                Width = width,
                Height = height,
                Background = background,
                Foreground = foreground,
                BorderThickness = new Thickness(0),
                FontWeight = FontWeights.SemiBold,
                Padding = new Thickness(12, 0, 12, 0),
                Cursor = Cursors.Hand
            };
        }
    }
}
