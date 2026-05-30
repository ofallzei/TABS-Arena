using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Navigation;

namespace TABS
{
    public partial class StartScreen : Window
    {
        private bool _isBorderlessFullscreen = true;
        private bool _isTitleBarDragging = false;
        private Point _titleBarDragMouseStart;
        private Point _titleBarDragWindowStart;

        private bool _isWindowedMaximized = false;

        public StartScreen()
        {
            AppPrefs.Load();
            TwoVTwoGameMode.Loc.Current = AppPrefs.Language;
            InitializeComponent();

            UpdateStaticText();
            UpdateLanguageSelectorUI();

            Loaded += (s, e) =>
                ApplyWindowMode(AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen, false);

            Closing += (s, e) =>
            {
                AppPrefs.WindowMode = _isBorderlessFullscreen
                    ? SavedWindowMode.BorderlessFullscreen
                    : SavedWindowMode.Windowed;

                AppPrefs.Language = TwoVTwoGameMode.Loc.Current;
                AppPrefs.Save();
            };
        }

        private string S(string key)
        {
            bool es = TwoVTwoGameMode.Loc.Current == TwoVTwoGameMode.Loc.Language.Spanish;

            switch (key)
            {
                case "ChooseMatchMode": return es ? "Elige modo de partida" : "Choose Match Mode";
                case "Duel": return es ? "Duelo" : "Duel";
                case "DuelBody": return es ? "Dos jugadores, economía individual y control rápido de rondas." : "Two players, individual economy, fast round control.";
                case "TeamBattle": return es ? "Batalla en Equipo" : "Team Battle";
                case "TeamBattleBody": return es ? "Cuatro jugadores, puntuación por equipo, BFT, hitos y herramientas de facción." : "Four players, shared team score, BFT, milestones, and faction tools.";
                case "Start1v1": return es ? "Iniciar 1v1" : "Start 1v1";
                case "Start2v2": return es ? "Iniciar 2v2" : "Start 2v2";
                case "CloseGameTitle": return es ? "Cerrar Juego" : "Close Game";
                case "CloseGameMsg": return es ? "¿Seguro que quieres cerrar el juego?" : "Are you sure you want to close the game?";
                default: return key;
            }
        }

        private void UpdateStaticText()
        {
            ChooseModeText.Text = S("ChooseMatchMode");
            OneVOneTitleText.Text = S("Duel");
            OneVOneBodyText.Text = S("DuelBody");
            TwoVTwoTitleText.Text = S("TeamBattle");
            TwoVTwoBodyText.Text = S("TeamBattleBody");
            OneVOneButton.Content = S("Start1v1");
            TwoVTwoButton.Content = S("Start2v2");

            SettingsButton.ToolTip = TwoVTwoGameMode.Loc.Get("Settings");
            SettingsTitleText.Text = TwoVTwoGameMode.Loc.Get("Settings");
            SettingsBackButton.Content = TwoVTwoGameMode.Loc.Get("Back");
            SettingsWindowModeLabel.Text = TwoVTwoGameMode.Loc.Get("WindowMode");
            SettingsLanguageLabel.Text = TwoVTwoGameMode.Loc.Get("Language");
            SettingsWindowModeText.Text = _isBorderlessFullscreen
                ? TwoVTwoGameMode.Loc.Get("BorderlessFullscreen")
                : TwoVTwoGameMode.Loc.Get("Windowed");
        }

        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            ConfirmCloseGame();
        }

        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            WindowState = WindowState.Minimized;
        }

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            if (_isBorderlessFullscreen)
                return;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            WindowState = WindowState.Normal;

            if (_isWindowedMaximized)
            {
                _isWindowedMaximized = false;

                Width = Math.Min(1280, screen.WorkingArea.Width);
                Height = Math.Min(720, screen.WorkingArea.Height);
                Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2;
                Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;
            }
            else
            {
                Left = screen.WorkingArea.Left;
                Top = screen.WorkingArea.Top;
                Width = screen.WorkingArea.Width;
                Height = screen.WorkingArea.Height;
                _isWindowedMaximized = true;
            }

            ResizeMode = ResizeMode.CanResize;
            CustomTitleBar.Visibility = Visibility.Visible;
            CustomTitleBarRow.Height = new GridLength(40);
            UpdateSettingsButtonStyles(false);
            UpdateStaticText();
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            ConfirmCloseGame();
        }

        private void ConfirmCloseGame()
        {
            var confirm = new ThemedConfirmDialog(
                S("CloseGameTitle"),
                S("CloseGameMsg"))
            {
                Owner = this
            };

            if (confirm.ShowDialog() != true)
                return;

            Close();
        }

        private void CustomTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (_isBorderlessFullscreen)
                return;

            if (e.ChangedButton != MouseButton.Left)
                return;

            if (e.ClickCount == 2)
            {
                WindowMaximize_Click(sender, e);
                return;
            }

            _isTitleBarDragging = true;
            _titleBarDragMouseStart = PointToScreen(e.GetPosition(this));
            _titleBarDragWindowStart = new Point(Left, Top);
            CustomTitleBar.CaptureMouse();
            e.Handled = true;
        }

        private void CustomTitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            if (!_isTitleBarDragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentMouse = PointToScreen(e.GetPosition(this));

            Left = _titleBarDragWindowStart.X + (currentMouse.X - _titleBarDragMouseStart.X);
            Top = _titleBarDragWindowStart.Y + (currentMouse.Y - _titleBarDragMouseStart.Y);

            e.Handled = true;
        }

        private void CustomTitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isTitleBarDragging = false;
            CustomTitleBar.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void DiscordButton_Click(object sender, RoutedEventArgs e)
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "https://discord.gg/cmcPpBaM",
                UseShellExecute = true
            });
        }
        private void SettingsButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Visible;
            UpdateStaticText();
            UpdateLanguageSelectorUI();
            UpdateSettingsButtonStyles(_isBorderlessFullscreen);
        }

        private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void SettingsWindowModeLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyWindowMode(false);
        }

        private void SettingsWindowModeRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyWindowMode(true);
        }

        private void ApplyWindowMode(bool borderless, bool saveSetting = true)
        {
            _isWindowedMaximized = false;
            _isBorderlessFullscreen = borderless;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            if (saveSetting)
            {
                AppPrefs.WindowMode = borderless
                    ? SavedWindowMode.BorderlessFullscreen
                    : SavedWindowMode.Windowed;

                AppPrefs.Language = TwoVTwoGameMode.Loc.Current;
                AppPrefs.Save();
            }

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;

            if (borderless)
            {
                ResizeMode = ResizeMode.NoResize;
                CustomTitleBar.Visibility = Visibility.Collapsed;
                CustomTitleBarRow.Height = new GridLength(0);

                Left = screen.Bounds.Left;
                Top = screen.Bounds.Top;
                Width = screen.Bounds.Width;
                Height = screen.Bounds.Height;
            }
            else
            {
                ResizeMode = ResizeMode.CanResize;
                CustomTitleBar.Visibility = Visibility.Visible;
                CustomTitleBarRow.Height = new GridLength(40);

                Width = Math.Min(1280, screen.WorkingArea.Width);
                Height = Math.Min(720, screen.WorkingArea.Height);
                Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - Width) / 2;
                Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - Height) / 2;
            }

            UpdateSettingsButtonStyles(borderless);
            UpdateStaticText();
        }

        private void UpdateSettingsButtonStyles(bool isFullscreen)
        {
            SettingsWindowModeText.Text = isFullscreen
                ? TwoVTwoGameMode.Loc.Get("BorderlessFullscreen")
                : TwoVTwoGameMode.Loc.Get("Windowed");

            SettingsDot1.Background = !isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));

            SettingsDot2.Background = isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
        }

        private void SettingsLanguageLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(TwoVTwoGameMode.Loc.Language.English);
        }

        private void SettingsLanguageRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(TwoVTwoGameMode.Loc.Language.Spanish);
        }

        private void ApplyLanguage(TwoVTwoGameMode.Loc.Language lang)
        {
            TwoVTwoGameMode.Loc.Current = lang;
            TwoVTwoGameMode.Loc.SaveLanguage();
            SaveLanguageForOneVOne();

            AppPrefs.Language = lang;
            AppPrefs.Save();

            UpdateStaticText();
            UpdateLanguageSelectorUI();
        }

        private void SaveLanguageForOneVOne()
        {
            try
            {
                string path = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TABS",
                    "language.txt");

                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(path, TwoVTwoGameMode.Loc.Current.ToString());
            }
            catch { }
        }

        private void UpdateLanguageSelectorUI()
        {
            bool isSpanish = TwoVTwoGameMode.Loc.Current == TwoVTwoGameMode.Loc.Language.Spanish;
            SettingsLanguageText.Text = isSpanish ? "Español" : "English";

            SettingsLangDot1.Background = !isSpanish
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));

            SettingsLangDot2.Background = isSpanish
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
        }

        private void OneVOne_Click(object sender, RoutedEventArgs e)
        {
            bool borderless = AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            var main = new MainWindow
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowState = WindowState.Normal,
                Left = borderless ? screen.Bounds.Left : Left,
                Top = borderless ? screen.Bounds.Top : Top,
                Width = borderless ? screen.Bounds.Width : Width,
                Height = borderless ? screen.Bounds.Height : Height
            };

            main.Show();
            Close();
        }

        private void TwoVTwo_Click(object sender, RoutedEventArgs e)
        {
            bool borderless = AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            var nav = new NavigationWindow
            {
                Title = "TABS Tracker",
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowStyle = WindowStyle.None,
                WindowState = WindowState.Normal,
                ResizeMode = borderless ? ResizeMode.NoResize : ResizeMode.CanResize,
                ShowsNavigationUI = false,
                Background = Brushes.Transparent,
                Left = borderless ? screen.Bounds.Left : Left,
                Top = borderless ? screen.Bounds.Top : Top,
                Width = borderless ? screen.Bounds.Width : Width,
                Height = borderless ? screen.Bounds.Height : Height
            };

            System.Windows.Shell.WindowChrome.SetWindowChrome(nav, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(8),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            nav.Navigate(new TwoVTwoGameMode());
            nav.Show();
            Close();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Dragging is handled by CustomTitleBar_MouseDown.
        }
    }
}