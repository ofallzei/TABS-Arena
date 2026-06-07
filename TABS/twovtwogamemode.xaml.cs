using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;
using IOPath = System.IO.Path;

namespace TABS
{
    public partial class TwoVTwoGameMode : Page
    {
        private bool _isTitleBarDragging = false;
        private Point _titleBarDragMouseStart;
        private Point _titleBarDragWindowStart;
        private bool _isWindowedMaximized = false;
        private bool _isBorderlessFullscreen = true;
        // ── Zoom ──────────────────────────────────────────────────────────
        private const double ZoomStep = 0.05;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 2.0;
        private const int TieTimerStartSeconds = 120;

        // ── Save paths ────────────────────────────────────────────────────
        private static readonly string SaveFolder =
            IOPath.Combine(Environment.GetFolderPath(
                Environment.SpecialFolder.ApplicationData),
                "TABSEconomyTracker", "2v2saves");
        private string _currentSaveName;

        // ── Round / state ─────────────────────────────────────────────────
        private int _round = 1;
        private int _pendingWinner = 0;   // 1 = Red, 2 = Blue, 3 = Tie
        private bool _namesLocked = false;
        private bool _firstTurnChosen = false;
        private int _lastRoundWinner = 0; // 1 = Red, 2 = Blue, 0 = none yet

        // ── Gold ──────────────────────────────────────────────────────────
        private int _p1Gold, _p2Gold, _p3Gold, _p4Gold;

        // ── Team points ───────────────────────────────────────────────────
        private int _redPoints = 0;
        private int _bluePoints = 0;

        // ── Tile color states: 1=green, -1=red, 0=neutral ─────────────────
        private int _p1GoldState, _p2GoldState, _p3GoldState, _p4GoldState;
        private int _p1PointsState, _p2PointsState, _p3PointsState, _p4PointsState;
        private int _p1InterestState, _p2InterestState, _p3InterestState, _p4InterestState;

        private static readonly SolidColorBrush TileGreen
            = new SolidColorBrush(Color.FromRgb(40, 110, 60));
        private static readonly SolidColorBrush TileRed
            = new SolidColorBrush(Color.FromRgb(120, 40, 40));
        private static readonly SolidColorBrush TileNeutral
            = new SolidColorBrush(Color.FromRgb(44, 53, 64));
        private static readonly Brush RedFlagBrush
            = new SolidColorBrush(Color.FromRgb(255, 139, 139));
        private static readonly Brush BlueFlagBrush
            = new SolidColorBrush(Color.FromRgb(134, 191, 255));
        private static readonly Brush MilestoneNumberBrush
            = new SolidColorBrush(Color.FromRgb(110, 182, 218));
        private static readonly Brush InputPlaceholderBrush = Brushes.Gray;
        private static readonly Brush InputTextBrush = Brushes.White;

        // ── Income ────────────────────────────────────────────────────────
        private int _p1Income, _p2Income, _p3Income, _p4Income;
        private int _p1IncomeUpgrades, _p2IncomeUpgrades,
                        _p3IncomeUpgrades, _p4IncomeUpgrades;
        private decimal _p1IncomeCost = 100m, _p2IncomeCost = 100m,
                        _p3IncomeCost = 100m, _p4IncomeCost = 100m;
        private bool _p1BoughtIncome, _p2BoughtIncome,
                _p3BoughtIncome, _p4BoughtIncome;
        private bool _p1BoughtIncomeThisRound, _p2BoughtIncomeThisRound,
             _p3BoughtIncomeThisRound, _p4BoughtIncomeThisRound;
        private bool _redReplayBoughtThisRound, _blueReplayBoughtThisRound;
        private int _p1IncomeMissedRounds, _p2IncomeMissedRounds,
                        _p3IncomeMissedRounds, _p4IncomeMissedRounds;
        private int _p1IncomeDecayPct, _p2IncomeDecayPct,
                        _p3IncomeDecayPct, _p4IncomeDecayPct;

        // ── Perm move ─────────────────────────────────────────────────────
        private int _p1PermMoveUpgrades, _p2PermMoveUpgrades,
                    _p3PermMoveUpgrades, _p4PermMoveUpgrades;
        private int _p1PermMovePurchases, _p2PermMovePurchases,
                    _p3PermMovePurchases, _p4PermMovePurchases;

        // ── Faction mode ──────────────────────────────────────────────────
        private const int StartingFactionCount = 3;
        private bool _factionModeEnabled = true;
        private bool _factionModeLocked = false;

        private List<string> _p1Factions = new List<string>();
        private List<string> _p2Factions = new List<string>();
        private List<string> _p3Factions = new List<string>();
        private List<string> _p4Factions = new List<string>();

        private int _p1FactionPurchases, _p2FactionPurchases,
            _p3FactionPurchases, _p4FactionPurchases;

        private int _p1ChosenFactionPurchases, _p2ChosenFactionPurchases,
                    _p3ChosenFactionPurchases, _p4ChosenFactionPurchases;

        private Button _p1BuyChosenFactionButton, _p2BuyChosenFactionButton,
               _p3BuyChosenFactionButton, _p4BuyChosenFactionButton;

        private Border _p1ChosenFactionDiscountBorder, _p2ChosenFactionDiscountBorder,
                       _p3ChosenFactionDiscountBorder, _p4ChosenFactionDiscountBorder;

        private TextBlock _p1ChosenFactionDiscountText, _p2ChosenFactionDiscountText,
                          _p3ChosenFactionDiscountText, _p4ChosenFactionDiscountText;

        private static readonly List<string> AllFactions = new List<string>
        {
            "Farmer","Viking","Medieval","Pirate","Spooky","Secret",
            "Ancient","Dynasty","Tribal","Legacy","Good","Evil"
            ,"Renaissance","Wild West","New Units","New Units 2"
        };

        private readonly Random _rng = new Random();

        private readonly Dictionary<string, string> FactionIconMap =
    new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
{
            { "Ancient",     "ancient.png"     },
            { "Good",        "good.png"        },
            { "Dynasty",     "dynasty.png"     },
            { "Farmer",      "farmer.png"      },
            { "Evil",        "evil.png"        },
            { "Legacy",      "legacy.png"      },
            { "Medieval",    "medieval.png"    },
            { "New Units",   "new units.png"   },
            { "New Units 2", "new units2.png"  },
            { "Pirate",      "pirate.png"      },
            { "Renaissance", "renaissance.png" },
            { "Secret",      "secret.png"      },
            { "Tribal",      "tribal.png"      },
            { "Viking",      "viking.png"      },
            { "Wild West",   "wild west.png"   },
            { "Spooky",      "spooky.png"      }
};

        // ── Match length modes ─────────────────────────────────────────────
        private bool _ft20ModeEnabled = true;
        private bool _ft10ModeEnabled = false;
        private bool _ft30ModeEnabled = false;
        private bool _ft20ModeLocked = false;
        private bool _matchEndPromptSuppressed = false;
        private List<string> _ft20RewardsRemaining = new List<string>();
        private int _ft20NextMilestone = 4;

        // ── Shared milestone reward system (all modes) ────────────────────
        private List<string> _milestoneRewardsRemaining = new List<string>();
        private int _milestoneNextThreshold = 5;
        private bool _milestoneSystemActive = false;

        // ── Sellback / BFT / FT20 coupon state ────────────────────────────
        private int _redPermanentSellbackBonusPct = 0;
        private int _bluePermanentSellbackBonusPct = 0;
        private int _redBFTSurcharge = 15;
        private int _blueBFTSurcharge = 15;

        private int _p1NextIncomeDiscountPct, _p2NextIncomeDiscountPct,
                    _p3NextIncomeDiscountPct, _p4NextIncomeDiscountPct;

        private int _p1NextSellBonusPct, _p2NextSellBonusPct,
                    _p3NextSellBonusPct, _p4NextSellBonusPct;

        private int _p1NextFactionDiscountPct, _p2NextFactionDiscountPct,
            _p3NextFactionDiscountPct, _p4NextFactionDiscountPct;

        private int _p1NextChosenFactionDiscountPct, _p2NextChosenFactionDiscountPct,
                    _p3NextChosenFactionDiscountPct, _p4NextChosenFactionDiscountPct;

        private int _p1NextPermMoveDiscountPct, _p2NextPermMoveDiscountPct,
                    _p3NextPermMoveDiscountPct, _p4NextPermMoveDiscountPct;

        private bool _p1PermMoveCapUnlocked, _p2PermMoveCapUnlocked,
                     _p3PermMoveCapUnlocked, _p4PermMoveCapUnlocked;

        // ── Standard milestones ───────────────────────────────────────────
        private bool _milestone5Claimed, _milestone10Claimed, _milestone15Claimed,
                     _milestone20Claimed, _milestone25Claimed;

        // ── Action log / undo ─────────────────────────────────────────────
        private List<string> _actionLog = new List<string>();
        private Stack<TwoV2SaveData> _undoStack = new Stack<TwoV2SaveData>();

        // ── Gold pop-out windows ──────────────────────────────────────────
        private GoldPopOutWindow _p1GoldWindow, _p2GoldWindow,
                                  _p3GoldWindow, _p4GoldWindow;

        // ── Notice timer ──────────────────────────────────────────────────
        private DispatcherTimer _noticeTimer;
        private DispatcherTimer _zoomIndicatorTimer;
        private DispatcherTimer _tieTimer;
        private DispatcherTimer _tieTimerFlashTimer;
        private int _tieTimerRemainingSeconds = TieTimerStartSeconds;
        private DateTime _tieTimerEndsAtUtc = DateTime.MinValue;
        private bool _tieTimerHasStarted = false;
        private bool _tieTimerFlashVisible = true;

        // ── Last applied calc text ────────────────────────────────────────
        private string _p1LastCalcText = "";
        private string _p2LastCalcText = "";
        private string _p3LastCalcText = "";
        private string _p4LastCalcText = "";

        // ─────────────────────────────────────────────────────────────────
        public TwoVTwoGameMode()
        {
            AppPrefs.Load();
            Loc.Current = AppPrefs.Language;
            InitializeComponent();
            SetupNumericInputBoxes();
            CreateChosenFactionButtons();
            ApplyPlayerPanelTypography();
            Directory.CreateDirectory(SaveFolder);
            InitializeZoom();

            _noticeTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            _noticeTimer.Tick += (s, e) =>
            {
                IncomeNoticePopup.IsOpen = false;
                _noticeTimer.Stop();
            };
            _zoomIndicatorTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            _zoomIndicatorTimer.Tick += (s, e) =>
            {
                _zoomIndicatorTimer.Stop();
                FadeOutZoomIndicator();
            };
            _tieTimer = new DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(100) };
            _tieTimer.Tick += TieTimer_Tick;
            _tieTimerFlashTimer = new DispatcherTimer
            { Interval = TimeSpan.FromMilliseconds(450) };
            _tieTimerFlashTimer.Tick += TieTimerFlash_Tick;

            UpdateLanguageSelectorUI();
            InitNewGame();
            RefreshSavesDropdown();
            RefreshAllUI();
            UpdateAllUI();
            UpdateSoundSettingsUI();

            Loaded += (s, e) =>
            {
                ApplyWindowMode(AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen, false);
                UpdateZoomIndicatorPlacement();
            };
        }

        private void TieTimer_Tick(object sender, EventArgs e)
        {
            SyncTieTimerFromClock();
        }

        private void SyncTieTimerFromClock()
        {
            if (_tieTimerEndsAtUtc == DateTime.MinValue)
                return;

            int newRemaining = (int)Math.Ceiling((_tieTimerEndsAtUtc - DateTime.UtcNow).TotalSeconds);
            _tieTimerRemainingSeconds = Math.Max(0, newRemaining);

            if (_tieTimerRemainingSeconds <= 0)
            {
                _tieTimer.Stop();
                _tieTimerHasStarted = false;
                _tieTimerEndsAtUtc = DateTime.MinValue;
                StartTieTimerFlash();
            }

            UpdateTieTimerUi();
        }

        private void TieTimerFlash_Tick(object sender, EventArgs e)
        {
            _tieTimerFlashVisible = !_tieTimerFlashVisible;
            if (TieTimerText != null)
                TieTimerText.Opacity = _tieTimerFlashVisible ? 1.0 : 0.15;
        }

        private void TieTimerToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_tieTimer.IsEnabled)
            {
                SyncTieTimerFromClock();
                if (_tieTimerRemainingSeconds <= 0)
                    return;

                _tieTimer.Stop();
                _tieTimerHasStarted = true;
                StopTieTimerFlash();
            }
            else
            {
                if (_tieTimerRemainingSeconds <= 0)
                    _tieTimerRemainingSeconds = TieTimerStartSeconds;

                bool isFreshStart = !_tieTimerHasStarted && _tieTimerRemainingSeconds == TieTimerStartSeconds;
                _tieTimerHasStarted = true;
                StopTieTimerFlash();
                if (isFreshStart)
                    _tieTimerRemainingSeconds--;

                _tieTimerEndsAtUtc = DateTime.UtcNow.AddSeconds(_tieTimerRemainingSeconds);
                if (_tieTimerRemainingSeconds > 0)
                {
                    _tieTimer.Start();
                    SyncTieTimerFromClock();
                }
                else
                {
                    StartTieTimerFlash();
                }
            }

            UpdateTieTimerUi();
        }

        private void TieTimerRestartButton_Click(object sender, RoutedEventArgs e)
        {
            ResetTieTimer();
        }

        private void ResetTieTimer()
        {
            if (_tieTimer != null)
                _tieTimer.Stop();

            _tieTimerRemainingSeconds = TieTimerStartSeconds;
            _tieTimerEndsAtUtc = DateTime.MinValue;
            _tieTimerHasStarted = false;
            StopTieTimerFlash();
            UpdateTieTimerUi();
        }

        private void StartTieTimerFlash()
        {
            if (_tieTimerFlashTimer == null)
                return;

            _tieTimerFlashVisible = true;
            if (TieTimerText != null)
                TieTimerText.Opacity = 1.0;

            _tieTimerFlashTimer.Stop();
            _tieTimerFlashTimer.Start();
        }

        private void StopTieTimerFlash()
        {
            if (_tieTimerFlashTimer != null)
                _tieTimerFlashTimer.Stop();

            _tieTimerFlashVisible = true;
            if (TieTimerText != null)
                TieTimerText.Opacity = 1.0;
        }

        private void UpdateTieTimerUi()
        {
            if (TieTimerText != null)
                TieTimerText.Text = FormatTieTimer(_tieTimerRemainingSeconds);

            if (TieTimerToggleButton != null)
                TieTimerToggleButton.Content = _tieTimer != null && _tieTimer.IsEnabled
                    ? Loc.Get("StopTimer")
                    : _tieTimerHasStarted ? Loc.Get("ResumeTimer") : Loc.Get("StartTieTimer");

            if (TieTimerRestartButton != null)
                TieTimerRestartButton.Content = Loc.Get("RestartTimer");
        }

        private static string FormatTieTimer(int seconds)
        {
            seconds = Math.Max(0, seconds);
            return string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
        }

        private void ApplyPlayerPanelTypography()
        {
            PlayerPanelText.ApplyButtonTypography(
                P1NameEditButton, P2NameEditButton, P3NameEditButton, P4NameEditButton,
                P1BuyIncomeButton, P2BuyIncomeButton, P3BuyIncomeButton, P4BuyIncomeButton,
                P1BuyPermMoveButton, P2BuyPermMoveButton, P3BuyPermMoveButton, P4BuyPermMoveButton,
                P1BuyFactionButton, P2BuyFactionButton, P3BuyFactionButton, P4BuyFactionButton,
                _p1BuyChosenFactionButton, _p2BuyChosenFactionButton, _p3BuyChosenFactionButton, _p4BuyChosenFactionButton,
                P1SingleTroopMoveButton, P2SingleTroopMoveButton, P3SingleTroopMoveButton, P4SingleTroopMoveButton,
                P1ReplayButton, P2ReplayButton, P3ReplayButton, P4ReplayButton,
                P1SpendButton, P2SpendButton, P3SpendButton, P4SpendButton,
                P1BuyTeamButton, P2BuyTeamButton, P3BuyTeamButton, P4BuyTeamButton,
                P1SellUnitButton, P2SellUnitButton, P3SellUnitButton, P4SellUnitButton);

            PlayerPanelText.ApplyTextSize(
                PlayerPanelText.StatLabelFontSize,
                P1LblGold, P1LblPoints, P1LblPermMv, P1LblIncome, P1LblInterest,
                P2LblGold, P2LblPoints, P2LblPermMv, P2LblIncome, P2LblInterest,
                P3LblGold, P3LblPoints, P3LblPermMv, P3LblIncome, P3LblInterest,
                P4LblGold, P4LblPoints, P4LblPermMv, P4LblIncome, P4LblInterest);

            PlayerPanelText.ApplyTextSize(
                PlayerPanelText.StatValueFontSize,
                P1GoldText, P1PointsText, P1UpgradesText, P1IncomeText, P1InterestText,
                P2GoldText, P2PointsText, P2UpgradesText, P2IncomeText, P2InterestText,
                P3GoldText, P3PointsText, P3UpgradesText, P3IncomeText, P3InterestText,
                P4GoldText, P4PointsText, P4UpgradesText, P4IncomeText, P4InterestText);
        }

        private void InitializeZoom()
        {
            RootScaleHost.LayoutTransform = new ScaleTransform(1.0, 1.0);
            AppScroll.SizeChanged += (s, e) => UpdateZoomIndicatorPlacement();
            AppScroll.ScrollChanged += (s, e) => UpdateZoomIndicatorPlacement();
            ZoomIndicator.SizeChanged += (s, e) => UpdateZoomIndicatorPlacement();
            ApplyZoom(AppPrefs.ZoomScale, false, false);
            Dispatcher.BeginInvoke(new Action(UpdateZoomIndicatorPlacement), DispatcherPriority.Loaded);
        }

        private double GetCurrentZoom()
        {
            if (RootScaleHost.LayoutTransform is ScaleTransform scale)
                return scale.ScaleX;

            return 1.0;
        }

        private double ClampZoom(double value)
        {
            return Math.Max(MinZoom, Math.Min(MaxZoom, value));
        }

        private void ApplyZoom(double zoom, bool persist, bool showIndicator)
        {
            zoom = ClampZoom(zoom);

            if (!(RootScaleHost.LayoutTransform is ScaleTransform scale))
            {
                scale = new ScaleTransform(1.0, 1.0);
                RootScaleHost.LayoutTransform = scale;
            }

            scale.ScaleX = zoom;
            scale.ScaleY = zoom;

            if (ZoomIndicatorText != null)
                ZoomIndicatorText.Text = string.Format("{0}%", Math.Round(zoom * 100));

            UpdateZoomIndicatorPlacement();
            if (showIndicator)
                ShowZoomIndicator();

            if (persist)
            {
                AppPrefs.ZoomScale = zoom;
                AppPrefs.Save();
            }
        }

        private void UpdateZoomIndicatorPlacement()
        {
            if (ZoomIndicatorPopup == null || ZoomIndicator == null || AppScroll == null || AppScroll.ActualWidth <= 0)
                return;

            ZoomIndicatorPopup.PlacementTarget = AppScroll;
            ZoomIndicatorPopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            double badgeWidth = ZoomIndicator.ActualWidth > 0 ? ZoomIndicator.ActualWidth : 86;
            ZoomIndicatorPopup.HorizontalOffset = Math.Max(12, (AppScroll.ActualWidth - badgeWidth) / 2);
            ZoomIndicatorPopup.VerticalOffset = 10;
        }

        private void ShowZoomIndicator()
        {
            if (ZoomIndicatorPopup == null || ZoomIndicator == null)
                return;

            UpdateZoomIndicatorPlacement();
            ZoomIndicator.BeginAnimation(UIElement.OpacityProperty, null);
            ZoomIndicator.Opacity = 1.0;
            ZoomIndicatorPopup.IsOpen = true;
            _zoomIndicatorTimer.Stop();
            _zoomIndicatorTimer.Start();
        }

        private void FadeOutZoomIndicator()
        {
            if (ZoomIndicatorPopup == null || ZoomIndicator == null || !ZoomIndicatorPopup.IsOpen)
                return;

            var fade = new DoubleAnimation(1.0, 0.0, TimeSpan.FromSeconds(1));
            fade.Completed += (s, e) =>
            {
                ZoomIndicatorPopup.IsOpen = false;
                ZoomIndicator.Opacity = 1.0;
            };
            ZoomIndicator.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private static void SetTeamFlagText(TextBlock target, Brush flagBrush, string text)
        {
            if (target == null) return;

            target.Inlines.Clear();
            target.Inlines.Add(PlayerPanelText.CreateFlagInline(flagBrush, 27, 24, new Thickness(0, 0, 7, -5)));
            target.Inlines.Add(new Run(StripTeamMarker(text)));
        }

        private static void SetMilestoneFlagText(TextBlock target, Brush flagBrush, string teamName, int pointsAway)
        {
            if (target == null) return;

            target.Inlines.Clear();
            target.Inlines.Add(PlayerPanelText.CreateFlagInline(flagBrush, 35, 31, new Thickness(0, 0, 10, -7)));
            target.Inlines.Add(new Run(string.Format("{0}:  ", teamName)));
            target.Inlines.Add(PlayerPanelText.CreateOutlinedTextInline(pointsAway.ToString(), 26, new Thickness(1, 0, 3, -2), MilestoneNumberBrush));
            target.Inlines.Add(new Run(" " + Loc.Get("PtsAway")));
        }

        private static string StripTeamMarker(string text)
        {
            return (text ?? "").Replace("🔴", "").Replace("🔵", "").TrimStart();
        }

        private void SetupNumericInputBoxes()
        {
            for (int p = 1; p <= 4; p++)
            {
                RegisterNumericInput(GetSpendBox(p), "CustomTroopSpend");
                RegisterNumericInput(GetBuyTeamBox(p), "TeammateUnitCost");
                RegisterNumericInput(GetUnitBox(p), "UnitValue");
            }
        }

        private void RegisterNumericInput(TextBox box, string placeholderKey)
        {
            RegisterNumericOnly(box);
            RegisterPlaceholder(box, placeholderKey);
        }

        private void RegisterNumericOnly(TextBox box)
        {
            if (box == null) return;

            box.PreviewTextInput -= NumericOnly_PreviewTextInput;
            box.PreviewTextInput += NumericOnly_PreviewTextInput;

            box.PreviewKeyDown -= NumericOnly_PreviewKeyDown;
            box.PreviewKeyDown += NumericOnly_PreviewKeyDown;

            DataObject.RemovePastingHandler(box, NumericOnly_Pasting);
            DataObject.AddPastingHandler(box, NumericOnly_Pasting);
        }

        private void NumericOnly_PreviewTextInput(object sender, TextCompositionEventArgs e)
        {
            e.Handled = !IsDigitsOnly(e.Text);
        }

        private void NumericOnly_PreviewKeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Space)
                e.Handled = true;
        }

        private void NumericOnly_Pasting(object sender, DataObjectPastingEventArgs e)
        {
            if (!e.DataObject.GetDataPresent(DataFormats.Text))
            {
                e.CancelCommand();
                return;
            }

            string text = e.DataObject.GetData(DataFormats.Text) as string ?? "";
            if (!IsDigitsOnly(text))
                e.CancelCommand();
        }

        private bool IsDigitsOnly(string text)
        {
            return !string.IsNullOrEmpty(text) && text.All(char.IsDigit);
        }

        private void RegisterPlaceholder(TextBox box, string placeholderKey)
        {
            if (box == null) return;

            box.Tag = placeholderKey;
            box.GotFocus -= PlaceholderBox_GotFocus;
            box.LostFocus -= PlaceholderBox_LostFocus;
            box.GotFocus += PlaceholderBox_GotFocus;
            box.LostFocus += PlaceholderBox_LostFocus;

            if (string.IsNullOrWhiteSpace(box.Text) || IsPlaceholderText(box.Text))
                SetInputPlaceholder(box);
        }

        private void SetInputPlaceholder(TextBox box)
        {
            string key = GetPlaceholderKey(box);
            if (box == null || string.IsNullOrWhiteSpace(key)) return;

            box.Text = Loc.Get(key);
            box.Foreground = InputPlaceholderBrush;
        }

        private void ClearInputPlaceholder(TextBox box)
        {
            if (box == null) return;

            if (IsPlaceholderText(box.Text))
                box.Text = "";

            box.Foreground = InputTextBrush;
        }

        private void RefreshInputPlaceholder(TextBox box)
        {
            if (box == null) return;

            if (string.IsNullOrWhiteSpace(box.Text) || IsPlaceholderText(box.Text))
                SetInputPlaceholder(box);
            else
                box.Foreground = InputTextBrush;
        }

        private string GetPlaceholderKey(TextBox box)
        {
            return box?.Tag as string;
        }

        private bool IsPlaceholderText(string text)
        {
            return Loc.IsTranslatedText("CustomTroopSpend", text) ||
                   Loc.IsTranslatedText("TeammateUnitCost", text) ||
                   Loc.IsTranslatedText("UnitValue", text);
        }

        private void CreateChosenFactionButtons()
        {
            _p1BuyChosenFactionButton = CreateChosenFactionButton(1, P1BuyFactionButton, out _p1ChosenFactionDiscountBorder, out _p1ChosenFactionDiscountText);
            _p2BuyChosenFactionButton = CreateChosenFactionButton(2, P2BuyFactionButton, out _p2ChosenFactionDiscountBorder, out _p2ChosenFactionDiscountText);
            _p3BuyChosenFactionButton = CreateChosenFactionButton(3, P3BuyFactionButton, out _p3ChosenFactionDiscountBorder, out _p3ChosenFactionDiscountText);
            _p4BuyChosenFactionButton = CreateChosenFactionButton(4, P4BuyFactionButton, out _p4ChosenFactionDiscountBorder, out _p4ChosenFactionDiscountText);
        }

        private Button CreateChosenFactionButton(int player, Button anchor, out Border badgeBorder, out TextBlock badgeText)
        {
            var row = new Grid
            {
                Margin = new Thickness(0, 6, 0, 6),
                Visibility = Visibility.Collapsed
            };

            row.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            row.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var button = new Button
            {
                Content = PlayerPanelText.CreateButtonContent(Loc.Get("BuyChosenFaction", GetChosenFactionCost(player))),
                Background = new SolidColorBrush(Color.FromRgb(110, 169, 200)),
                FontSize = PlayerPanelText.ButtonFontSize,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0)
            };

            button.Click += (s, e) => BuyChosenFaction(player);
            Grid.SetColumn(button, 0);
            row.Children.Add(button);

            badgeText = new TextBlock
            {
                Text = "",
                FontSize = 10,
                FontWeight = FontWeights.Bold,
                Foreground = new SolidColorBrush(Color.FromRgb(127, 240, 176))
            };

            badgeBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(31, 75, 58)),
                CornerRadius = new CornerRadius(8),
                Padding = new Thickness(5, 2, 5, 2),
                Margin = new Thickness(4, 0, 0, 0),
                VerticalAlignment = VerticalAlignment.Center,
                Visibility = Visibility.Collapsed,
                Child = badgeText
            };

            Grid.SetColumn(badgeBorder, 1);
            row.Children.Add(badgeBorder);

            if (anchor.Parent is Grid grid && grid.Parent is Panel panel)
            {
                int index = panel.Children.IndexOf(grid);
                panel.Children.Insert(index + 1, row);
            }

            return button;
        }

        private void UpdateLanguageSelectorUI()
        {
            SettingsLanguageText.Text = Loc.GetLanguageDisplayName(Loc.Current);
            Loc.UpdateLanguageFlag(SettingsLanguageFlag, Loc.Current);

            SetLanguageDot(SettingsLangDot1, Loc.Current == Loc.Language.English);
            SetLanguageDot(SettingsLangDot2, Loc.Current == Loc.Language.Spanish);
            SetLanguageDot(SettingsLangDot3, Loc.Current == Loc.Language.Russian);
            SetLanguageDot(SettingsLangDot4, Loc.Current == Loc.Language.Chinese);
        }

        private void SetLanguageDot(Border dot, bool isActive)
        {
            if (dot == null) return;

            dot.Background = isActive
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
        }

        private void SettingsSoundsToggleButton_Click(object sender, RoutedEventArgs e)
        {
            AppPrefs.SoundsEnabled = !AppPrefs.SoundsEnabled;
            AppPrefs.Save();
            UpdateSoundSettingsUI();
        }

        private void SettingsSoundVolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (SettingsSoundVolumeText == null)
                return;

            AppPrefs.SoundVolume = Math.Max(0.0, Math.Min(1.0, e.NewValue / 100.0));
            AppPrefs.Save();
            AudioFeedback.RefreshVolume();
            UpdateSoundSettingsUI();
        }

        private void UpdateSoundSettingsUI()
        {
            if (SettingsSoundsToggleButton == null || SettingsSoundVolumeSlider == null || SettingsSoundVolumeText == null)
                return;

            int volumePercent = (int)Math.Round(AppPrefs.SoundVolume * 100.0);
            SettingsSoundsToggleButton.IsChecked = AppPrefs.SoundsEnabled;
            SettingsSoundsToggleButton.Content = Loc.Get("Sounds") + ": " + Loc.Get(AppPrefs.SoundsEnabled ? "On" : "Off");
            SettingsSoundsToggleButton.Background = AppPrefs.SoundsEnabled
                ? new SolidColorBrush(Color.FromRgb(49, 95, 125))
                : new SolidColorBrush(Color.FromRgb(49, 56, 67));

            SettingsSoundVolumeSlider.Value = volumePercent;
            SettingsSoundVolumeSlider.IsEnabled = AppPrefs.SoundsEnabled;
            SettingsSoundVolumeText.Text = volumePercent + "%";
            SettingsSoundVolumeText.Opacity = AppPrefs.SoundsEnabled ? 1.0 : 0.45;
            SettingsSoundVolumeLabel.Opacity = AppPrefs.SoundsEnabled ? 1.0 : 0.45;
        }

        private void ResetNameEditButtonsForNewGame()
        {
            for (int p = 1; p <= 4; p++)
            {
                var box = GetNameBox(p);
                var display = GetNameDisplayText(p);
                var button = GetNameEditButton(p);

                box.IsReadOnly = false;
                box.TextAlignment = TextAlignment.Left;
                box.Visibility = Visibility.Visible;

                display.Visibility = Visibility.Collapsed;

                button.Visibility = Visibility.Visible;
                button.Content = Loc.Get("Set");
            }

            _namesLocked = false;
            Keyboard.ClearFocus();
        }

        private void UpdateNameEditState()
        {
            bool matchStarted = _round > 1 || _factionModeLocked || _ft20ModeLocked;

            for (int p = 1; p <= 4; p++)
            {
                var box = GetNameBox(p);
                var display = GetNameDisplayText(p);
                var button = GetNameEditButton(p);

                if (matchStarted)
                {
                    box.IsReadOnly = true;
                    box.Visibility = Visibility.Collapsed;

                    display.Text = box.Text;
                    display.Visibility = Visibility.Visible;

                    button.Visibility = Visibility.Collapsed;
                }
                else
                {
                    display.Visibility = Visibility.Collapsed;
                    box.Visibility = Visibility.Visible;

                    button.Visibility = Visibility.Visible;
                    button.Content = box.IsReadOnly ? Loc.Get("Unset") : Loc.Get("Set");
                }
            }
        }
        private void InitNewGame()
        {
            CloseAllGoldWindows();
            _round = 1; _pendingWinner = 0;
            _lastRoundWinner = 0;
            _namesLocked = false; _firstTurnChosen = false;
            _redPoints = 0; _bluePoints = 0;
            _factionModeEnabled = true; _factionModeLocked = false;
            _ft20ModeEnabled = true; _ft10ModeEnabled = false; _ft30ModeEnabled = false; _ft20ModeLocked = false;
            _matchEndPromptSuppressed = false;
            _p1GoldState = _p2GoldState = _p3GoldState = _p4GoldState = 0;
            _p1PointsState = _p2PointsState = _p3PointsState = _p4PointsState = 0;
            _p1InterestState = _p2InterestState = _p3InterestState = _p4InterestState = 0;

            _milestoneRewardsRemaining = new List<string>();
            _milestoneNextThreshold = 5;
            _milestoneSystemActive = false;

            int start = GetStartingGold();
            _p1Gold = _p2Gold = _p3Gold = _p4Gold = start;

            _p1BoughtIncomeThisRound = _p2BoughtIncomeThisRound =
_p3BoughtIncomeThisRound = _p4BoughtIncomeThisRound = false;
            _redReplayBoughtThisRound = _blueReplayBoughtThisRound = false;

            _p1Income = _p2Income = _p3Income = _p4Income = 0;
            _p1IncomeUpgrades = _p2IncomeUpgrades =
            _p3IncomeUpgrades = _p4IncomeUpgrades = 0;
            _p1IncomeCost = _p2IncomeCost = _p3IncomeCost = _p4IncomeCost = GetBaseIncomeCost();
            _p1BoughtIncome = _p2BoughtIncome =
            _p3BoughtIncome = _p4BoughtIncome = false;
            _p1IncomeMissedRounds = _p2IncomeMissedRounds =
            _p3IncomeMissedRounds = _p4IncomeMissedRounds = 0;
            _p1IncomeDecayPct = _p2IncomeDecayPct =
            _p3IncomeDecayPct = _p4IncomeDecayPct = 0;

            _p1PermMoveUpgrades = _p2PermMoveUpgrades =
            _p3PermMoveUpgrades = _p4PermMoveUpgrades = 0;
            _p1PermMovePurchases = _p2PermMovePurchases =
            _p3PermMovePurchases = _p4PermMovePurchases = 0;

            _p1Factions = new List<string>(); _p2Factions = new List<string>();
            _p3Factions = new List<string>(); _p4Factions = new List<string>();
            _p1FactionPurchases = _p2FactionPurchases =
_p3FactionPurchases = _p4FactionPurchases = 0;
            _p1ChosenFactionPurchases = _p2ChosenFactionPurchases =
            _p3ChosenFactionPurchases = _p4ChosenFactionPurchases = 0;

            _redPermanentSellbackBonusPct = 0;
            _bluePermanentSellbackBonusPct = 0;
            _redBFTSurcharge = 15;
            _blueBFTSurcharge = 15;

            _p1NextIncomeDiscountPct = _p2NextIncomeDiscountPct =
            _p3NextIncomeDiscountPct = _p4NextIncomeDiscountPct = 0;

            _p1NextSellBonusPct = _p2NextSellBonusPct =
            _p3NextSellBonusPct = _p4NextSellBonusPct = 0;

            _p1NextFactionDiscountPct = _p2NextFactionDiscountPct =
_p3NextFactionDiscountPct = _p4NextFactionDiscountPct = 0;

            _p1NextChosenFactionDiscountPct = _p2NextChosenFactionDiscountPct =
            _p3NextChosenFactionDiscountPct = _p4NextChosenFactionDiscountPct = 0;

            _p1NextPermMoveDiscountPct = _p2NextPermMoveDiscountPct =
            _p3NextPermMoveDiscountPct = _p4NextPermMoveDiscountPct = 0;

            _p1PermMoveCapUnlocked = _p2PermMoveCapUnlocked =
            _p3PermMoveCapUnlocked = _p4PermMoveCapUnlocked = false;

            _milestone5Claimed = true; _milestone10Claimed = true;
            _milestone15Claimed = true; _milestone20Claimed = true;
            _milestone25Claimed = true;

            _ft20RewardsRemaining = new List<string>();
            _ft20NextMilestone = 4;
            _milestoneRewardsRemaining = new List<string>();
            _milestoneNextThreshold = 5;
            _milestoneSystemActive = false;

            _actionLog = new List<string>();
            _undoStack = new Stack<TwoV2SaveData>();
            _currentSaveName = null;
            ResetTieTimer();

            P1NameBox.Text = Loc.Get("DefaultP1Name");
            P2NameBox.Text = Loc.Get("DefaultP2Name");
            P3NameBox.Text = Loc.Get("DefaultP3Name");
            P4NameBox.Text = Loc.Get("DefaultP4Name");

            P1NameDisplayText.Text = P1NameBox.Text;
            P2NameDisplayText.Text = P2NameBox.Text;
            P3NameDisplayText.Text = P3NameBox.Text;
            P4NameDisplayText.Text = P4NameBox.Text;

            ResetNameEditButtonsForNewGame();

            _p1LastCalcText = Loc.Get("NoRoundYet");
            _p2LastCalcText = Loc.Get("NoRoundYet");
            _p3LastCalcText = Loc.Get("NoRoundYet");
            _p4LastCalcText = Loc.Get("NoRoundYet");

            BuildFT20RewardPool();
            if (_factionModeEnabled) AssignRandomFactions();
        }

        private void BuildFT20RewardPool()
        {
            List<string> pool;
            if (_factionModeEnabled)
            {
                pool = new List<string>
        {
                        "80% Off Next Faction","80% Off Next Faction","80% Off Next Faction","80% Off Next Faction",
            "80% Off Next Chosen Faction",
            "80% Off Next Perm Move",
            "Sellback +15%",
            "+30% Next Sell",
            "-5% BFT Surcharge"
        };
                if (!_ft10ModeEnabled)
                {
                    pool.Add("10% Off Next Income");
                    pool.Add("10% Off Next Income");
                }
            }
            else
            {
                pool = new List<string>
        {
            "80% Off Next Perm Move","80% Off Next Perm Move",
            "Sellback +15%",
            "+30% Next Sell","+30% Next Sell",
            "-5% BFT Surcharge"
        };
                if (!_ft10ModeEnabled)
                {
                    pool.Add("10% Off Next Income");
                    pool.Add("10% Off Next Income");
                    pool.Add("10% Off Next Income");
                }
            }
            Shuffle(pool);
            _ft20RewardsRemaining = pool;
            _ft20NextMilestone = GetTimedMilestoneStep();
        }

        // Builds the shared milestone reward pool for Normal and Faction modes (every 5 pts)
        private void BuildSharedMilestonePool()
        {
            List<string> pool;

            if (_factionModeEnabled)
            {
                // Faction mode — full reward set including faction discounts, every 5 pts
                pool = new List<string>
                {
                                        "80% Off Next Faction","80% Off Next Faction","80% Off Next Faction","80% Off Next Faction",
                    "80% Off Next Chosen Faction",
                    "80% Off Next Perm Move",
                    "Sellback +15%",
                    "10% Off Next Income","10% Off Next Income",
                    "+30% Next Sell",
                    "-5% BFT Surcharge"
                };
            }
            else
            {
                // Normal mode: 4x faction removed → replaced with boosted other rewards
                pool = new List<string>
                {
                    "80% Off Next Perm Move","80% Off Next Perm Move",
                    "Sellback +15%",
                    "10% Off Next Income","10% Off Next Income","10% Off Next Income",
                    "+30% Next Sell","+30% Next Sell",
                    "-5% BFT Surcharge"
                };
            }

            Shuffle(pool);
            _milestoneRewardsRemaining = pool;
            _milestoneNextThreshold = 5;
            _milestoneSystemActive = true;
        }

        private void Shuffle(List<string> list)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = _rng.Next(i + 1);
                var tmp = list[i];
                list[i] = list[j];
                list[j] = tmp;
            }
        }

        private void PushUndoSnapshot()
        {
            _undoStack.Push(BuildSaveData("__undo__"));

            // Optional safety cap
            if (_undoStack.Count > 50)
            {
                var items = _undoStack.ToArray(); // top item is first
                _undoStack = new Stack<TwoV2SaveData>(items.Take(50).Reverse());
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Master UI refresh
        // ─────────────────────────────────────────────────────────────────
        private void RefreshAllUI()
        {
            RoundText.Text = _round.ToString();
            PendingResultText.Text = _pendingWinner == 1 ? Loc.Get("RedTeamWins")
                       : _pendingWinner == 2 ? Loc.Get("BlueTeamWins")
                       : _pendingWinner == 3 ? Loc.Get("Tie")
                       : Loc.Get("NotSet");
            RedTeamPointsDisplay.Text = _redPoints.ToString();
            BlueTeamPointsDisplay.Text = _bluePoints.ToString();

            // Team points shown in each player's points tile
            P1PointsText.Text = _redPoints.ToString();
            P2PointsText.Text = _redPoints.ToString();
            P3PointsText.Text = _bluePoints.ToString();
            P4PointsText.Text = _bluePoints.ToString();

            bool matchStarted = _round > 1 || _factionModeLocked || _ft20ModeLocked;

            Visibility firstTurnOverlayVisibility =
                (!matchStarted && !_firstTurnChosen) ? Visibility.Visible : Visibility.Collapsed;
            FirstTurnPromptBorder.Visibility = firstTurnOverlayVisibility;
            FirstTurnDimOverlay.Visibility = firstTurnOverlayVisibility;

            UpdateNameEditState();

            // Faction toggle
            FactionModeToggleButton.Content = _factionModeEnabled ? Loc.Get("FactionModeOn") : Loc.Get("FactionModeOff");
            FactionModeToggleButton.Tag = _factionModeEnabled ? "True" : "False";
            FactionModeToggleButton.IsEnabled = !matchStarted;

            // Match length toggles
            FT20ModeToggleButton.Content = _ft30ModeEnabled ? Loc.Get("FT30ModeOn") : Loc.Get("FT30ModeOff");
            FT20ModeToggleButton.Tag = _ft30ModeEnabled ? "True" : "False";
            FT20ModeToggleButton.IsEnabled = !matchStarted;
            FT10ModeToggleButton.Content = _ft10ModeEnabled ? Loc.Get("FT10ModeOn") : Loc.Get("FT10ModeOff");
            FT10ModeToggleButton.Tag = _ft10ModeEnabled ? "True" : "False";
            FT10ModeToggleButton.IsEnabled = !matchStarted;

            NextRoundButton.IsEnabled = _pendingWinner != 0;
            NextRoundButton.Background = _pendingWinner != 0
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(55, 64, 76));

            UndoButton.IsEnabled = _undoStack.Count > 0;
            UndoButton.Background = _undoStack.Count > 0
                ? new SolidColorBrush(Color.FromRgb(142, 108, 245))
                : new SolidColorBrush(Color.FromRgb(55, 64, 76));

            if (IsTimedMilestoneMode()) RefreshFT20InfoPanel();
            else RefreshSharedMilestonePanel();

            bool showFaction = _factionModeEnabled;
            P1FactionArea.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;
            P2FactionArea.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;
            P3FactionArea.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;
            P4FactionArea.Visibility = showFaction ? Visibility.Visible : Visibility.Collapsed;

            // Tile colors
            ApplyTileColor(P1GoldBorder, _p1GoldState);
            ApplyTileColor(P2GoldBorder, _p2GoldState);
            ApplyTileColor(P3GoldBorder, _p3GoldState);
            ApplyTileColor(P4GoldBorder, _p4GoldState);
            ApplyTileColor(P1PointsBorder, _p1PointsState);
            ApplyTileColor(P2PointsBorder, _p2PointsState);
            ApplyTileColor(P3PointsBorder, _p3PointsState);
            ApplyTileColor(P4PointsBorder, _p4PointsState);
            ApplyTileColor(P1InterestBorder, _p1InterestState);
            ApplyTileColor(P2InterestBorder, _p2InterestState);
            ApplyTileColor(P3InterestBorder, _p3InterestState);
            ApplyTileColor(P4InterestBorder, _p4InterestState);

            UpdateAllGoldDisplays();
            UpdateFactionButtons();
            UpdateFactionIcons();
            UpdateIncomeButtons();
            UpdatePermMoveButtons();
            UpdateSellPctDisplays();
            UpdateBFTDisplays();
            UpdateReplayButtons();
            UpdateFixedSpendButtons();
            UpdateCalcTexts();
            RefreshActionLog();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Tile color
        // ─────────────────────────────────────────────────────────────────
        private void ApplyTileColor(Border tile, int state)
        {
            tile.Background = state == 1 ? TileGreen
                            : state == -1 ? TileRed
                            : TileNeutral;
        }

        private void SetGoldTileState(int player, int state)
        {
            switch (player)
            {
                case 1: _p1GoldState = state; ApplyTileColor(P1GoldBorder, state); break;
                case 2: _p2GoldState = state; ApplyTileColor(P2GoldBorder, state); break;
                case 3: _p3GoldState = state; ApplyTileColor(P3GoldBorder, state); break;
                case 4: _p4GoldState = state; ApplyTileColor(P4GoldBorder, state); break;
            }
        }

        private void SetPointsTileState(int player, int state)
        {
            switch (player)
            {
                case 1: _p1PointsState = state; ApplyTileColor(P1PointsBorder, state); break;
                case 2: _p2PointsState = state; ApplyTileColor(P2PointsBorder, state); break;
                case 3: _p3PointsState = state; ApplyTileColor(P3PointsBorder, state); break;
                case 4: _p4PointsState = state; ApplyTileColor(P4PointsBorder, state); break;
            }
        }

        private void SetInterestTileState(int player, int state)
        {
            switch (player)
            {
                case 1: _p1InterestState = state; ApplyTileColor(P1InterestBorder, state); break;
                case 2: _p2InterestState = state; ApplyTileColor(P2InterestBorder, state); break;
                case 3: _p3InterestState = state; ApplyTileColor(P3InterestBorder, state); break;
                case 4: _p4InterestState = state; ApplyTileColor(P4InterestBorder, state); break;
            }
        }

        private void UpdateNextTurnOrderAfterRound(int latestWinner)
        {
            if (latestWinner == 1 || latestWinner == 2)
                _lastRoundWinner = latestWinner;

            int firstTeam;

            if (_redPoints > _bluePoints)
                firstTeam = 1;
            else if (_bluePoints > _redPoints)
                firstTeam = 2;
            else if (_lastRoundWinner == 1 || _lastRoundWinner == 2)
                firstTeam = _lastRoundWinner;
            else
                firstTeam = 0;

            TurnOrderText.Text = firstTeam == 1 ? Loc.Get("TurnOrderRed")
                              : firstTeam == 2 ? Loc.Get("TurnOrderBlue")
                              : Loc.Get("NotAvailableYet");
        }

        private void ResetAllPlayerPanelsForModeSwap()
        {
            _p1Gold = _p2Gold = _p3Gold = _p4Gold = GetStartingGold();

            _p1GoldState = _p2GoldState = _p3GoldState = _p4GoldState = 0;
            _p1PointsState = _p2PointsState = _p3PointsState = _p4PointsState = 0;

            _p1Income = _p2Income = _p3Income = _p4Income = 0;
            _p1IncomeUpgrades = _p2IncomeUpgrades = _p3IncomeUpgrades = _p4IncomeUpgrades = 0;
            _p1IncomeCost = _p2IncomeCost = _p3IncomeCost = _p4IncomeCost = GetBaseIncomeCost();
            _p1BoughtIncome = _p2BoughtIncome = _p3BoughtIncome = _p4BoughtIncome = false;
            _p1BoughtIncomeThisRound = _p2BoughtIncomeThisRound =
_p3BoughtIncomeThisRound = _p4BoughtIncomeThisRound = false;
            _redReplayBoughtThisRound = _blueReplayBoughtThisRound = false;
            _p1IncomeMissedRounds = _p2IncomeMissedRounds = _p3IncomeMissedRounds = _p4IncomeMissedRounds = 0;
            _p1IncomeDecayPct = _p2IncomeDecayPct = _p3IncomeDecayPct = _p4IncomeDecayPct = 0;

            _p1PermMoveUpgrades = _p2PermMoveUpgrades = _p3PermMoveUpgrades = _p4PermMoveUpgrades = 0;
            _p1PermMovePurchases = _p2PermMovePurchases = _p3PermMovePurchases = _p4PermMovePurchases = 0;

            _p1FactionPurchases = _p2FactionPurchases = _p3FactionPurchases = _p4FactionPurchases = 0;

            _redPermanentSellbackBonusPct = 0;
            _bluePermanentSellbackBonusPct = 0;
            _redBFTSurcharge = 15;
            _blueBFTSurcharge = 15;

            _p1NextIncomeDiscountPct = _p2NextIncomeDiscountPct = _p3NextIncomeDiscountPct = _p4NextIncomeDiscountPct = 0;
            _p1NextSellBonusPct = _p2NextSellBonusPct = _p3NextSellBonusPct = _p4NextSellBonusPct = 0;
            _p1NextFactionDiscountPct = _p2NextFactionDiscountPct = _p3NextFactionDiscountPct = _p4NextFactionDiscountPct = 0;
            _p1NextPermMoveDiscountPct = _p2NextPermMoveDiscountPct = _p3NextPermMoveDiscountPct = _p4NextPermMoveDiscountPct = 0;

            _p1PermMoveCapUnlocked = _p2PermMoveCapUnlocked = _p3PermMoveCapUnlocked = _p4PermMoveCapUnlocked = false;

            _p1LastCalcText = Loc.Get("NoRoundYet");
            _p2LastCalcText = Loc.Get("NoRoundYet");
            _p3LastCalcText = Loc.Get("NoRoundYet");
            _p4LastCalcText = Loc.Get("NoRoundYet");
        }

        private void ResetAllTileColors()
        {
            _p1GoldState = _p2GoldState = _p3GoldState = _p4GoldState = 0;
            _p1PointsState = _p2PointsState = _p3PointsState = _p4PointsState = 0;
            _p1InterestState = _p2InterestState = _p3InterestState = _p4InterestState = 0;

            ApplyTileColor(P1GoldBorder, 0); ApplyTileColor(P2GoldBorder, 0);
            ApplyTileColor(P3GoldBorder, 0); ApplyTileColor(P4GoldBorder, 0);
            ApplyTileColor(P1PointsBorder, 0); ApplyTileColor(P2PointsBorder, 0);
            ApplyTileColor(P3PointsBorder, 0); ApplyTileColor(P4PointsBorder, 0);
            ApplyTileColor(P1InterestBorder, 0); ApplyTileColor(P2InterestBorder, 0);
            ApplyTileColor(P3InterestBorder, 0); ApplyTileColor(P4InterestBorder, 0);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Gold display + AddGold helper
        // ─────────────────────────────────────────────────────────────────
        private void UpdateAllGoldDisplays()
        {
            P1GoldText.Text = _p1Gold.ToString();
            P2GoldText.Text = _p2Gold.ToString();
            P3GoldText.Text = _p3Gold.ToString();
            P4GoldText.Text = _p4Gold.ToString();

            P1UpgradesText.Text = _p1PermMoveUpgrades.ToString();
            P2UpgradesText.Text = _p2PermMoveUpgrades.ToString();
            P3UpgradesText.Text = _p3PermMoveUpgrades.ToString();
            P4UpgradesText.Text = _p4PermMoveUpgrades.ToString();

            P1IncomeText.Text = $"+{_p1Income}";
            P2IncomeText.Text = $"+{_p2Income}";
            P3IncomeText.Text = $"+{_p3Income}";
            P4IncomeText.Text = $"+{_p4Income}";

            P1InterestText.Text = $"+{CalcInterest(_p1Gold)}";
            P2InterestText.Text = $"+{CalcInterest(_p2Gold)}";
            P3InterestText.Text = $"+{CalcInterest(_p3Gold)}";
            P4InterestText.Text = $"+{CalcInterest(_p4Gold)}";

            _p1GoldWindow?.UpdateGold(_p1Gold, P1NameBox.Text, _p1GoldState, _p1Factions, FactionIconMap);
            _p2GoldWindow?.UpdateGold(_p2Gold, P2NameBox.Text, _p2GoldState, _p2Factions, FactionIconMap);
            _p3GoldWindow?.UpdateGold(_p3Gold, P3NameBox.Text, _p3GoldState, _p3Factions, FactionIconMap);
            _p4GoldWindow?.UpdateGold(_p4Gold, P4NameBox.Text, _p4GoldState, _p4Factions, FactionIconMap);
        }

        /// <summary>Modify a player's gold, update tile color and displays.</summary>
        private void AddGold(int player, int amount)
        {
            int oldInterest = CalcInterest(GetGold(player));

            switch (player)
            {
                case 1: _p1Gold += amount; break;
                case 2: _p2Gold += amount; break;
                case 3: _p3Gold += amount; break;
                case 4: _p4Gold += amount; break;
            }

            int newInterest = CalcInterest(GetGold(player));

            SetGoldTileState(player, amount >= 0 ? 1 : -1);
            SetInterestTileState(player, newInterest > oldInterest ? 1
                                      : newInterest < oldInterest ? -1
                                      : 0);

            UpdateAllGoldDisplays();
            UpdateCalcTexts();
        }

        private int GetGold(int player)
        {
            switch (player)
            {
                case 1: return _p1Gold;
                case 2: return _p2Gold;
                case 3: return _p3Gold;
                case 4: return _p4Gold;
                default: return 0;
            }
        }

        private int CalcInterest(int gold)
    => Math.Min((gold / 50) * 10, 100);

        private void NormalizeMatchModeFlags()
        {
            if (_ft10ModeEnabled)
            {
                _ft30ModeEnabled = false;
                _ft20ModeEnabled = false;
            }
            else if (_ft30ModeEnabled)
            {
                _ft20ModeEnabled = false;
            }
            else
            {
                _ft20ModeEnabled = true;
            }
        }

        private bool IsTimedMilestoneMode()
        {
            return _ft20ModeEnabled || _ft10ModeEnabled;
        }

        private bool IsIncomeAvailable()
        {
            return !_ft10ModeEnabled;
        }

        private int GetStartingGold()
        {
            return 1200;
        }

        private int GetRoundRewardTier()
        {
            if (_ft10ModeEnabled) return ((_round - 1) / 2) * 40;
            if (_ft20ModeEnabled) return ((_round - 1) / 3) * 15;
            return ((_round - 1) / 5) * 10;
        }

        private int GetWinnerRewardBase()
        {
            if (_ft10ModeEnabled) return 95;
            if (_ft20ModeEnabled) return 75;
            return 55;
        }

        private int GetLoserRewardBase()
        {
            if (_ft10ModeEnabled) return 125;
            if (_ft20ModeEnabled) return 105;
            return 85;
        }

        private int GetTieRewardBase()
        {
            return (GetWinnerRewardBase() + GetLoserRewardBase()) / 2;
        }

        private int GetTimedMilestoneStep()
        {
            return _ft10ModeEnabled ? 2 : 4;
        }

        private int GetPermMoveBaseCost()
        {
            if (_ft10ModeEnabled) return 125;
            if (_ft20ModeEnabled) return 175;
            return 200;
        }

        private int GetFactionCost(int player)
        {
            int purchases = GetFactionPurchases(player);
            int baseCost = _ft10ModeEnabled ? 25 : 50;
            int scale = _ft10ModeEnabled ? 15 : 20;
            return baseCost + (purchases * scale);
        }

        private int GetSingleTroopMoveCost()
        {
            return _ft10ModeEnabled ? 20 : 25;
        }

        private int GetBaseIncomeCost()
        {
            return _ft20ModeEnabled ? 130 : 100;
        }        // ─────────────────────────────────────────────────────────────────
                 //  First turn
                 // ─────────────────────────────────────────────────────────────────
        private void RedTeamFirstTurnButton_Click(object s, RoutedEventArgs e)
        {
            if (_firstTurnChosen) return;

            PushUndoSnapshot();

            _firstTurnChosen = true;
            TurnOrderText.Text = Loc.Get("TurnOrderRed");
            FirstTurnPromptBorder.Visibility = Visibility.Collapsed;
            FirstTurnDimOverlay.Visibility = Visibility.Collapsed;

            AddGold(1, 40);
            AddGold(2, 40);

            LogAction($"🔴 {Loc.Get("LogRedGoesFirst")}");
            ShowNotice(Loc.Get("LogRedGoesFirst"), NoticeType.Success);
            RefreshAllUI();
        }

        private void BlueTeamFirstTurnButton_Click(object s, RoutedEventArgs e)
        {
            if (_firstTurnChosen) return;

            PushUndoSnapshot();

            _firstTurnChosen = true;
            TurnOrderText.Text = Loc.Get("TurnOrderBlue");
            FirstTurnPromptBorder.Visibility = Visibility.Collapsed;
            FirstTurnDimOverlay.Visibility = Visibility.Collapsed;

            AddGold(3, 40);
            AddGold(4, 40);

            LogAction($"🔵 {Loc.Get("LogBlueGoesFirst")}");
            ShowNotice(Loc.Get("LogBlueGoesFirst"), NoticeType.Success);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Round win buttons — set pending winner ONLY
        // ─────────────────────────────────────────────────────────────────
        private void RedTeamWins_Click(object s, RoutedEventArgs e)
        {
            _pendingWinner = 1;
            PendingResultText.Text = Loc.Get("RedTeamWins");
            LogAction($"⚙️ {Loc.Get("Pending")}: 🔴 {Loc.Get("RedTeamWins")}.");
            RefreshAllUI();
        }

        private void BlueTeamWins_Click(object s, RoutedEventArgs e)
        {
            _pendingWinner = 2;
            PendingResultText.Text = Loc.Get("BlueTeamWins");
            LogAction($"⚙️ {Loc.Get("Pending")}: 🔵 {Loc.Get("BlueTeamWins")}.");
            RefreshAllUI();
        }

        private void TieRound_Click(object s, RoutedEventArgs e)
        {
            _pendingWinner = 3;
            PendingResultText.Text = Loc.Get("Tie");
            LogAction($"⚙️ {Loc.Get("Pending")}: 🤝 {Loc.Get("Tie")}.");
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Next Round — FIX: capture pendingWinner FIRST, then reset it
        // ─────────────────────────────────────────────────────────────────
        private int GetMatchGoalPoints()
        {
            if (_ft10ModeEnabled) return 10;
            if (_ft30ModeEnabled) return 30;
            return 20;
        }

        private bool IsWinByTwoRuleActive(int previousWinnerPoints, int winnerPoints, int loserPoints)
        {
            int matchPoint = GetMatchGoalPoints() - 1;
            return (previousWinnerPoints >= matchPoint && loserPoints >= matchPoint)
                || (winnerPoints >= matchPoint && loserPoints >= matchPoint);
        }

        private bool ShouldShowMatchEndPrompt(int previousWinnerPoints, int winnerPoints, int loserPoints)
        {
            int goal = GetMatchGoalPoints();

            if (IsWinByTwoRuleActive(previousWinnerPoints, winnerPoints, loserPoints))
            {
                bool hadAlreadyWon = previousWinnerPoints >= goal && previousWinnerPoints - loserPoints >= 2;
                bool hasWonNow = winnerPoints >= goal && winnerPoints - loserPoints >= 2;
                return hasWonNow && !hadAlreadyWon;
            }

            return previousWinnerPoints < goal && winnerPoints >= goal && winnerPoints > loserPoints;
        }

        private void ShowMatchEndPromptIfNeeded(int previousRedPoints, int previousBluePoints)
        {
            if (_matchEndPromptSuppressed) return;

            int winner = 0;
            bool wonByTwoRule = false;

            if (ShouldShowMatchEndPrompt(previousRedPoints, _redPoints, _bluePoints))
            {
                winner = 1;
                wonByTwoRule = IsWinByTwoRuleActive(previousRedPoints, _redPoints, _bluePoints);
            }
            else if (ShouldShowMatchEndPrompt(previousBluePoints, _bluePoints, _redPoints))
            {
                winner = 2;
                wonByTwoRule = IsWinByTwoRuleActive(previousBluePoints, _bluePoints, _redPoints);
            }

            if (winner == 0) return;

            int goal = GetMatchGoalPoints();
            string winnerName = winner == 1 ? Loc.Get("RedTeam") : Loc.Get("BlueTeam");
            string redTeamName = Loc.Get("RedTeam");
            string blueTeamName = Loc.Get("BlueTeam");
            string message = wonByTwoRule
                ? Loc.Get("MatchEndWinByTwoMessage", redTeamName, _redPoints, blueTeamName, _bluePoints, winnerName)
                : Loc.Get("MatchEndMessage", winnerName, goal);

            var dialog = new MatchEndDialog(
                Loc.Get("MatchEndTitle"),
                message,
                Loc.Get("MatchEndQuestion"),
                Loc.Get("NewGamePlain"),
                Loc.Get("ContinuePlaying"));

            Window owner = Window.GetWindow(this);
            if (owner != null) dialog.Owner = owner;

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.StartNewGame)
                StartNewGamePreservingModes();
            else if (dialog.ContinueSelected)
                _matchEndPromptSuppressed = true;
        }

        private void NextRound_Click(object s, RoutedEventArgs e)
        {
            if (_pendingWinner == 0)
            {
                ShowNotice(Loc.Get("SetWinnerBeforeAdvancing"), NoticeType.Warning);
                return;
            }

            // Snapshot for undo BEFORE any changes
            PushUndoSnapshot();

            // Capture winner NOW — do not use _pendingWinner after resetting it
            int winner = _pendingWinner;
            int previousRedPoints = _redPoints;
            int previousBluePoints = _bluePoints;

            // Lock mode toggles after first round advance
            _factionModeLocked = true;
            _ft20ModeLocked = true;

            // Capture calc snapshot BEFORE any gold changes happen
            CaptureRoundCalcSnapshots(winner);

            // Reset tile colors at round start
            ResetAllTileColors();

            // ── 1. Award team point ────────────────────────────────────────
            if (winner == 1)
            {
                _redPoints++;
                SetPointsTileState(1, 1);
                SetPointsTileState(2, 1);
            }
            else if (winner == 2)
            {
                _bluePoints++;
                SetPointsTileState(3, 1);
                SetPointsTileState(4, 1);
            }
            else if (winner == 3)
            {
                // Tie = no team gains points
            }

            // Sync points text immediately
            P1PointsText.Text = _redPoints.ToString();
            P2PointsText.Text = _redPoints.ToString();
            P3PointsText.Text = _bluePoints.ToString();
            P4PointsText.Text = _bluePoints.ToString();
            RedTeamPointsDisplay.Text = _redPoints.ToString();
            BlueTeamPointsDisplay.Text = _bluePoints.ToString();

            // ── 2. Interest ────────────────────────────────────────────────
            for (int p = 1; p <= 4; p++)
            {
                int interest = CalcInterest(GetGold(p));
                if (interest > 0) AddGold(p, interest);
            }

            // ── 3. Permanent income ────────────────────────────────────────
            if (IsIncomeAvailable())
            {
                for (int p = 1; p <= 4; p++)
                {
                    int inc = GetIncome(p);
                    if (inc > 0) AddGold(p, inc);
                }
            }

            // ── 4. Income decay tracking ───────────────────────────────────
            if (IsIncomeAvailable())
            {
                for (int p = 1; p <= 4; p++) UpdateIncomeDecay(p);
            }
            else
            {
                for (int p = 1; p <= 4; p++)
                {
                    SetIncomeMissedRounds(p, 0);
                    SetIncomeDecayPct(p, 0);
                }
            }
            _p1BoughtIncome = _p2BoughtIncome =
            _p3BoughtIncome = _p4BoughtIncome = false;
            _p1BoughtIncomeThisRound = _p2BoughtIncomeThisRound =
_p3BoughtIncomeThisRound = _p4BoughtIncomeThisRound = false;
            _redReplayBoughtThisRound = _blueReplayBoughtThisRound = false;

            // ── 5. Round reward — pass captured winner, not _pendingWinner ─
            ApplyRoundReward(winner);

            // ── 6. Milestones ──────────────────────────────────────────────
            if (IsTimedMilestoneMode())
                CheckFT20Milestones();
            else
                CheckSharedMilestones();

            string winnerName = winner == 1 ? $"🔴 {Loc.Get("LogRedWins")}" : winner == 2 ? $"🔵 {Loc.Get("LogBlueWins")}" : $"🤝 {Loc.Get("Tie")}";
            LogAction(Loc.Get("LogRoundComplete", _round, winnerName, _redPoints, _bluePoints));

            // ── 7. Update turn order, reset pending winner, and advance round ─────────
            UpdateNextTurnOrderAfterRound(winner);

            _pendingWinner = 0;
            _round++;
            ResetTieTimer();

            RefreshAllUI();
            ShowMatchEndPromptIfNeeded(previousRedPoints, previousBluePoints);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Round reward — uses passed-in winner, never reads _pendingWinner
        // ─────────────────────────────────────────────────────────────────
        private void ApplyRoundReward(int winner)
        {
            if (winner == 3)
            {
                int tieGold = GetTieRewardValue();

                AddGold(1, tieGold);
                AddGold(2, tieGold);
                AddGold(3, tieGold);
                AddGold(4, tieGold);

                LogAction($"🤝 {Loc.Get("LogRoundRewardTie", tieGold)}");
                ShowNotice(Loc.Get("NoticeRoundTie", _round, tieGold), NoticeType.Success);
                return;
            }

            GetRoundRewardValues(out int winnerGold, out int loserGold);

            bool redWon = winner == 1;
            int redGold = redWon ? winnerGold : loserGold;
            int blueGold = redWon ? loserGold : winnerGold;

            AddGold(1, redGold);
            AddGold(2, redGold);
            AddGold(3, blueGold);
            AddGold(4, blueGold);

            LogAction($"💰 {Loc.Get("LogRoundReward", redGold, blueGold)}");
            ShowNotice(Loc.Get("NoticeRoundWin", redWon ? "🔴" : "🔵", Loc.Get(redWon ? "RedTeamShort" : "BlueTeamShort"), _round, winnerGold, loserGold), NoticeType.Success);
        }

        private void GetRoundRewardValues(out int winnerGold, out int loserGold)
        {
            int tier = GetRoundRewardTier();
            winnerGold = GetWinnerRewardBase() + tier;
            loserGold = GetLoserRewardBase() + tier;
        }

        private int GetTieRewardValue()
        {
            return GetTieRewardBase() + GetRoundRewardTier();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Standard milestones
        // ─────────────────────────────────────────────────────────────────
        private void CheckStandardMilestones()
        {
            // Replaced by shared milestone system — no longer used
        }

        private void CheckMilestoneGold(int pts, ref bool claimed, int goldEach)
        {
            if (claimed) return;
            bool redHit = _redPoints >= pts;
            bool blueHit = _bluePoints >= pts;
            if (!redHit && !blueHit) return;
            claimed = true;
            string team;
            if (redHit) { AddGold(1, goldEach); AddGold(2, goldEach); team = Loc.Get("RedTeamShort"); }
            else { AddGold(3, goldEach); AddGold(4, goldEach); team = Loc.Get("BlueTeamShort"); }
            LogAction(Loc.Get("LogSharedGoldMilestone", pts, team, goldEach));
            ShowNotice(Loc.Get("NoticeSharedGoldMilestone", pts, team, goldEach), NoticeType.Milestone);
        }

        private void CheckMilestonePerm(int pts, ref bool claimed)
        {
            if (claimed) return;
            bool redHit = _redPoints >= pts;
            bool blueHit = _bluePoints >= pts;
            if (!redHit && !blueHit) return;
            claimed = true;
            string team;
            if (redHit) { _p1PermMoveUpgrades++; _p2PermMoveUpgrades++; team = Loc.Get("RedTeamShort"); }
            else { _p3PermMoveUpgrades++; _p4PermMoveUpgrades++; team = Loc.Get("BlueTeamShort"); }
            LogAction(Loc.Get("LogSharedPermMoveMilestone", pts, team));
            ShowNotice(Loc.Get("NoticeSharedPermMoveMilestone", pts, team), NoticeType.Milestone);
        }

        private void CheckMilestoneSellback(int pts, ref bool claimed)
        {
            if (claimed) return;
            bool redHit = _redPoints >= pts;
            bool blueHit = _bluePoints >= pts;
            if (!redHit && !blueHit) return;

            claimed = true;
            string team;
            if (redHit)
            {
                _redPermanentSellbackBonusPct = Math.Max(_redPermanentSellbackBonusPct, 20);
                team = Loc.Get("RedTeamShort");
            }
            else
            {
                _bluePermanentSellbackBonusPct = Math.Max(_bluePermanentSellbackBonusPct, 20);
                team = Loc.Get("BlueTeamShort");
            }

            LogAction(Loc.Get("LogSharedSellbackMilestone", pts, team));
            ShowNotice(Loc.Get("NoticeSharedSellbackMilestone", pts, team), NoticeType.Milestone);
            UpdateSellPctDisplays();
        }

        // ─────────────────────────────────────────────────────────────────
        //  FT20 milestones
        // ─────────────────────────────────────────────────────────────────
        // Shared milestone reward system for Normal and Faction modes (every 5 pts)
        private void CheckSharedMilestones()
        {
            bool redHit = _redPoints >= _milestoneNextThreshold;
            bool blueHit = _bluePoints >= _milestoneNextThreshold;

            if (!redHit && !blueHit) return;
            if (_milestoneRewardsRemaining.Count == 0) return;

            int thresholdThisRound = _milestoneNextThreshold;
            _milestoneNextThreshold += 5;

            if (redHit) AwardSharedMilestone(true, thresholdThisRound);
            if (blueHit) AwardSharedMilestone(false, thresholdThisRound);
        }

        private void AwardSharedMilestone(bool isRed, int threshold)
        {
            if (_milestoneRewardsRemaining.Count == 0) return;
            string reward = _milestoneRewardsRemaining[0];
            _milestoneRewardsRemaining.RemoveAt(0);
            ApplyFT20Reward(reward, isRed);
            string team = Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort");
            LogAction($"🏆 {Loc.Get("LogMilestone", threshold, team, LocalizeReward(reward))}");
            ShowNotice(Loc.Get("NoticeMilestone", threshold, Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort"), LocalizeReward(reward)), NoticeType.Milestone);
        }

        private void RefreshSharedMilestonePanel()
        {
            int redAway = Math.Max(0, _milestoneNextThreshold - _redPoints);
            int blueAway = Math.Max(0, _milestoneNextThreshold - _bluePoints);
            SetMilestoneFlagText(MilestoneP1Text, RedFlagBrush, Loc.Get("RedTeam"), redAway);
            SetMilestoneFlagText(MilestoneP2Text, BlueFlagBrush, Loc.Get("BlueTeam"), blueAway);

            if (_milestoneRewardsRemaining.Count > 0)
            {
                string next = _milestoneRewardsRemaining[0];
                MilestoneNextRewardText.Text = LocalizeReward(next);
                MilestoneNextRewardIcon.Text = GetRewardIcon(next);
            }
            else
            {
                MilestoneNextRewardText.Text = Loc.Get("AllRewardsClaimed");
                MilestoneNextRewardIcon.Text = "🏆";
            }

            MilestoneRewardsLeftPanel.Children.Clear();
            foreach (var g in _milestoneRewardsRemaining
                .GroupBy(r => r).OrderBy(g => g.Key))
            {
                MilestoneRewardsLeftPanel.Children.Add(new TextBlock
                {
                    Text = $"{g.Count()} {GetRewardIcon(g.Key)} {LocalizeReward(g.Key)}",
                    FontSize = 17,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#E8EDF3")),
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
        }

        private void CheckFT20Milestones()
        {
            // Check Red then Blue — each team progresses through the same shared pool
            // (pool depletes, milestone counter advances globally)
            bool redHit = _redPoints >= _ft20NextMilestone;
            bool blueHit = _bluePoints >= _ft20NextMilestone;

            if (redHit && _ft20RewardsRemaining.Count > 0)
                AwardFT20Milestone(true);
            if (blueHit && _ft20RewardsRemaining.Count > 0)
                AwardFT20Milestone(false);
        }

        private void AwardFT20Milestone(bool isRed)
        {
            if (_ft20RewardsRemaining.Count == 0) return;
            string reward = _ft20RewardsRemaining[0];
            _ft20RewardsRemaining.RemoveAt(0);
            ApplyFT20Reward(reward, isRed);
            string team = Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort");
            LogAction($"🏆 {Loc.Get("LogFT20Milestone", _ft20NextMilestone, team, LocalizeReward(reward))}");
            ShowNotice(Loc.Get("NoticeFT20Milestone", _ft20NextMilestone, Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort"), LocalizeReward(reward)), NoticeType.Milestone);
            _ft20NextMilestone += GetTimedMilestoneStep();
        }

        private string LocalizeReward(string reward)
        {
            switch (reward)
            {
                case "80% Off Next Faction": return Loc.Get("Reward80OffFaction");
                case "80% Off Next Chosen Faction": return Loc.Get("Reward80OffChosenFaction");
                case "80% Off Next Perm Move": return Loc.Get("Reward80OffPermMove");
                case "Sellback +15%": return Loc.Get("RewardSellback15");
                case "10% Off Next Income": return Loc.Get("Reward10OffIncome");
                case "+30% Next Sell": return Loc.Get("Reward30NextSell");
                case "-5% BFT Surcharge": return Loc.Get("RewardMinus5BFT");
                default: return reward;
            }
        }

        private void ApplyFT20Reward(string reward, bool isRed)
        {
            int[] players = isRed ? new[] { 1, 2 } : new[] { 3, 4 };

            switch (reward)
            {
                case "80% Off Next Faction":
                    foreach (int p in players)
                        SetNextFactionDiscountPct(p, 80);
                    UpdateFactionButtons();
                    break;

                case "80% Off Next Chosen Faction":
                    foreach (int p in players)
                        SetNextChosenFactionDiscountPct(p, 80);
                    UpdateFactionButtons();
                    break;

                case "80% Off Next Perm Move":
                    foreach (int p in players)
                    {
                        SetNextPermMoveDiscountPct(p, 80);
                        SetPermMoveCapUnlocked(p, true);
                    }
                    UpdatePermMoveButtons();
                    break;

                case "Sellback +15%":
                    if (isRed) _redPermanentSellbackBonusPct += 15;
                    else _bluePermanentSellbackBonusPct += 15;
                    UpdateSellPctDisplays();
                    break;

                case "10% Off Next Income":
                    foreach (int p in players)
                        SetNextIncomeDiscountPct(p, 10);
                    UpdateIncomeButtons();
                    break;

                case "+30% Next Sell":
                    foreach (int p in players)
                        SetNextSellBonusPct(p, 30);
                    UpdateSellPctDisplays();
                    break;

                case "-5% BFT Surcharge":
                    if (isRed)
                        _redBFTSurcharge = Math.Max(0, _redBFTSurcharge - 5);
                    else
                        _blueBFTSurcharge = Math.Max(0, _blueBFTSurcharge - 5);
                    UpdateBFTDisplays();
                    break;
            }
        }

        private void GiveFreeRandomFaction(List<string> factions)
        {
            var avail = AllFactions.Where(f => !factions.Contains(f)).ToList();
            if (avail.Count == 0) return;

            factions.Add(avail[_rng.Next(avail.Count)]);
        }        // ─────────────────────────────────────────────────────────────────
                 //  Income decay
                 // ─────────────────────────────────────────────────────────────────
        private void UpdateIncomeDecay(int player)
        {
            if (!IsIncomeAvailable())
            {
                SetIncomeMissedRounds(player, 0);
                SetIncomeDecayPct(player, 0);
                SetIncomeCost(player, Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, GetIncomeUpgrades(player))));
                return;
            }

            bool bought = GetBoughtIncome(player);
            int missed = GetIncomeMissedRounds(player);
            int decay = GetIncomeDecayPct(player);

            if (bought)
            {
                missed = 0;
                decay = 0;
            }
            else
            {
                missed++;

                if (_ft20ModeEnabled)
                {
                    // FT20: grace = 3 rounds, then 4% off per round after
                    // missed 1,2,3 = 0%, missed 4 = 4%, missed 5 = 8%, etc.
                    decay = missed >= 4 ? Math.Min(100, (missed - 2) * 6) : 0;
                }
                else
                {
                    // Normal / Faction: grace = 4 rounds, then 2% off per round after
                    // missed 1,2,3,4 = 0%, missed 5 = 2%, missed 6 = 4%, etc.
                    decay = missed >= 5 ? Math.Min(100, (missed - 3) * 3) : 0;
                }
            }

            SetIncomeMissedRounds(player, missed);
            SetIncomeDecayPct(player, decay);

            int upgrades = GetIncomeUpgrades(player);
            decimal full = GetBaseIncomeCost() * (decimal)Math.Pow(1.24, upgrades);

            if (decay > 0)
            {
                decimal decayed = Math.Max(1m, Math.Round(full * (1m - decay / 100m)));
                SetIncomeCost(player, decayed);
            }
            else
            {
                SetIncomeCost(player, Math.Round(full));
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Buy income
        // ─────────────────────────────────────────────────────────────────
        private void P1BuyIncome_Click(object s, RoutedEventArgs e) => BuyIncome(1);
        private void P2BuyIncome_Click(object s, RoutedEventArgs e) => BuyIncome(2);
        private void P3BuyIncome_Click(object s, RoutedEventArgs e) => BuyIncome(3);
        private void P4BuyIncome_Click(object s, RoutedEventArgs e) => BuyIncome(4);

        private void BuyIncome(int player)
        {
            if (!IsIncomeAvailable()) return;

            if (GetBoughtIncomeThisRound(player))
            {
                ShowNotice(Loc.Get("IncomeAlreadyBought", player), NoticeType.Warning);
                return;
            }

            int gold = GetGold(player);
            int decayPct = GetIncomeDecayPct(player);
            int couponPct = GetNextIncomeDiscountPct(player);
            int totalDiscountPct = decayPct + couponPct;

            decimal baseCost = GetIncomeCost(player);
            decimal discountedCost = Math.Max(1m, Math.Round(baseCost * (1m - totalDiscountPct / 100m)));
            int costInt = (int)Math.Ceiling(discountedCost);

            if (gold < costInt)
            {
                ShowNotice(Loc.Get("NeedsGold", player, costInt, gold), NoticeType.Warning);
                return;
            }

            int incomeGain = _ft20ModeEnabled ? 13 : 10;

            PushUndoSnapshot();
            AddGold(player, -costInt);

            int upgrades = GetIncomeUpgrades(player) + 1;
            SetIncome(player, GetIncome(player) + incomeGain);
            SetIncomeUpgrades(player, upgrades);
            SetBoughtIncome(player, true);
            SetBoughtIncomeThisRound(player, true);
            SetIncomeMissedRounds(player, 0);
            SetIncomeDecayPct(player, 0);
            SetIncomeCost(player, Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, upgrades)));

            if (couponPct > 0)
                SetNextIncomeDiscountPct(player, 0);

            LogAction($"📈 {Loc.Get("LogBoughtIncome", player, incomeGain, costInt, totalDiscountPct, GetIncome(player))}");
            ShowNotice(Loc.Get("NoticeBoughtIncome", player, GetIncome(player), costInt), NoticeType.Success);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Buy perm move
        // ─────────────────────────────────────────────────────────────────
        private void P1BuyPermMove_Click(object s, RoutedEventArgs e) => BuyPermMove(1);
        private void P2BuyPermMove_Click(object s, RoutedEventArgs e) => BuyPermMove(2);
        private void P3BuyPermMove_Click(object s, RoutedEventArgs e) => BuyPermMove(3);
        private void P4BuyPermMove_Click(object s, RoutedEventArgs e) => BuyPermMove(4);

        private void BuyPermMove(int player)
        {
            int baseCost = GetPermMoveBaseCost();
            int discountPct = GetNextPermMoveDiscountPct(player);
            int max = GetPermMoveMaxPurchases(player);
            int purchases = GetPermMovePurchases(player);
            int finalCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            if (purchases >= max)
            {
                ShowNotice(Loc.Get("MaxedPermMove", player, purchases, max), NoticeType.Warning);
                return;
            }

            if (GetGold(player) < finalCost)
            {
                ShowNotice(Loc.Get("NeedsGold", player, finalCost, GetGold(player)), NoticeType.Warning);
                return;
            }

            PushUndoSnapshot();
            AddGold(player, -finalCost);
            SetPermMoveUpgrades(player, GetPermMoveUpgrades(player) + 1);
            SetPermMovePurchases(player, purchases + 1);

            if (discountPct > 0)
                SetNextPermMoveDiscountPct(player, 0);

            string discountText = discountPct > 0 ? Loc.Get("DiscountSuffix", discountPct) : "";
            LogAction($"🏃 {Loc.Get("LogBoughtPermMove", player, finalCost, discountText, GetPermMoveUpgrades(player))}");
            ShowNotice(Loc.Get("NoticeBoughtPermMove", player, GetPermMoveUpgrades(player)), NoticeType.Success);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Buy faction
        // ─────────────────────────────────────────────────────────────────
        private void P1BuyFaction_Click(object s, RoutedEventArgs e) => BuyFaction(1);
        private void P2BuyFaction_Click(object s, RoutedEventArgs e) => BuyFaction(2);
        private void P3BuyFaction_Click(object s, RoutedEventArgs e) => BuyFaction(3);
        private void P4BuyFaction_Click(object s, RoutedEventArgs e) => BuyFaction(4);

        private void BuyFaction(int player)
        {
            int maxPurchases = AllFactions.Count;
            int purchases = GetFactionPurchases(player);
            if (purchases >= maxPurchases)
            {
                ShowNotice(Loc.Get("HasAllFactions", player), NoticeType.Warning);
                return;
            }

            int baseCost = GetFactionCost(player);
            int discountPct = GetNextFactionDiscountPct(player);
            int cost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            if (GetGold(player) < cost)
            {
                ShowNotice(Loc.Get("NeedsGold", player, cost, GetGold(player)), NoticeType.Warning);
                return;
            }

            var factions = GetFactions(player);
            var available = AllFactions.Where(f => !factions.Contains(f)).ToList();
            if (available.Count == 0)
            {
                ShowNotice(Loc.Get("HasAllFactions", player), NoticeType.Warning);
                return;
            }

            string newFaction = available[_rng.Next(available.Count)];
            PushUndoSnapshot();
            factions.Add(newFaction);
            SetFactions(player, factions);
            AddGold(player, -cost);
            SetFactionPurchases(player, purchases + 1);

            if (discountPct > 0)
                SetNextFactionDiscountPct(player, 0);

            int nextCost = GetFactionCost(player);
            string discountText = discountPct > 0 ? Loc.Get("DiscountSuffix", discountPct) : "";
            LogAction($"⚔️ {Loc.Get("LogBoughtFaction", player, newFaction, cost, discountText, nextCost)}");
            ShowNotice(Loc.Get("NoticeBoughtFaction", player, newFaction, nextCost), NoticeType.Success);
            RefreshAllUI();
        }

        private void BuyChosenFaction(int player)
        {
            if (!_factionModeEnabled) return;

            var factions = GetFactions(player);
            var available = AllFactions.Where(f => !factions.Contains(f)).ToList();

            if (available.Count == 0)
            {
                ShowNotice(Loc.Get("HasAllFactions", player), NoticeType.Warning);
                return;
            }

            int discountPct = GetNextChosenFactionDiscountPct(player);
            int cost = GetDisplayedChosenFactionCost(player);
            if (GetGold(player) < cost)
            {
                ShowNotice(Loc.Get("NeedsGoldFor", player, cost, Loc.Get("ChosenFactionLabel"), GetGold(player)), NoticeType.Warning);
                return;
            }

            var dialog = new MainWindow.FactionChoiceDialog(
                Loc.Get("ChooseFactionTitle"),
                Loc.Get("ChooseFactionSub", GetPlayerName(player)),
                available,
                FactionIconMap)
            {
                Owner = Window.GetWindow(this)
            };

            if (dialog.ShowDialog() != true || string.IsNullOrWhiteSpace(dialog.SelectedFaction))
                return;

            PushUndoSnapshot();

            factions.Add(dialog.SelectedFaction);
            SetFactions(player, factions);
            AddGold(player, -cost);
            SetFactionPurchases(player, GetFactionPurchases(player) + 1);
            if (discountPct > 0)
                SetNextChosenFactionDiscountPct(player, 0);

            LogAction(Loc.Get("LogBoughtChosenFaction", player, dialog.SelectedFaction, cost));
            ShowNotice(Loc.Get("NoticeBoughtChosenFaction", player, dialog.SelectedFaction, cost), NoticeType.Success);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Fixed-amount spending
        // ─────────────────────────────────────────────────────────────────
        private void P1SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(1, GetSingleTroopMoveCost(), "troop move");
        private void P2SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(2, GetSingleTroopMoveCost(), "troop move");
        private void P3SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(3, GetSingleTroopMoveCost(), "troop move");
        private void P4SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(4, GetSingleTroopMoveCost(), "troop move");

        private void P1Replay_Click(object s, RoutedEventArgs e) => BuyReplay(1);
        private void P2Replay_Click(object s, RoutedEventArgs e) => BuyReplay(2);
        private void P3Replay_Click(object s, RoutedEventArgs e) => BuyReplay(3);
        private void P4Replay_Click(object s, RoutedEventArgs e) => BuyReplay(4);

        private void SpendFixed(int player, int amount, string label)
        {
            if (GetGold(player) < amount)
            {
                ShowNotice(Loc.Get("NeedsGoldFor", player, amount, Loc.Get(label == "troop move" ? "SingleTroopMoveLabel" : "ReplayLabel"), GetGold(player)), NoticeType.Warning);
                return;
            }
            PushUndoSnapshot();
            AddGold(player, -amount);
            LogAction(Loc.Get("LogSpentOn", player, amount, Loc.Get(label == "troop move" ? "SingleTroopMoveLabel" : "ReplayLabel")));
            ShowNotice(Loc.Get("NoticeSpentOn", player, amount, label == "troop move" ? Loc.Get("SingleTroopMoveLabel") : Loc.Get("ReplayLabel")), NoticeType.Info);
        }

        private void BuyReplay(int player)
        {
            if (HasTeamBoughtReplayThisRound(player))
            {
                ShowNotice(Loc.Get(player <= 2 ? "RedReplayAlreadyBought" : "BlueReplayAlreadyBought"), NoticeType.Warning);
                return;
            }

            const int amount = 10;
            if (GetGold(player) < amount)
            {
                ShowNotice(Loc.Get("NeedsGoldFor", player, amount, Loc.Get("ReplayLabel"), GetGold(player)), NoticeType.Warning);
                return;
            }

            PushUndoSnapshot();
            AddGold(player, -amount);
            SetTeamBoughtReplayThisRound(player, true);

            string replayLabel = Loc.Get("Replay").Split('(')[0].Trim();
            LogAction(Loc.Get("LogSpentOn", player, amount, replayLabel));
            ShowNotice(Loc.Get("NoticeSpentOn", player, amount, replayLabel), NoticeType.Info);
            RefreshAllUI();
        }

        private bool HasTeamBoughtReplayThisRound(int player)
        {
            return player <= 2 ? _redReplayBoughtThisRound : _blueReplayBoughtThisRound;
        }

        private void SetTeamBoughtReplayThisRound(int player, bool value)
        {
            if (player <= 2) _redReplayBoughtThisRound = value;
            else _blueReplayBoughtThisRound = value;
        }

        private void UpdateFixedSpendButtons()
        {
            int cost = GetSingleTroopMoveCost();
            PlayerPanelText.SetButtonContent(P1SingleTroopMoveButton, Loc.Get("SingleTroopMove", cost));
            PlayerPanelText.SetButtonContent(P2SingleTroopMoveButton, Loc.Get("SingleTroopMove", cost));
            PlayerPanelText.SetButtonContent(P3SingleTroopMoveButton, Loc.Get("SingleTroopMove", cost));
            PlayerPanelText.SetButtonContent(P4SingleTroopMoveButton, Loc.Get("SingleTroopMove", cost));
            SetCostButtonVisual(P1SingleTroopMoveButton, 1, cost);
            SetCostButtonVisual(P2SingleTroopMoveButton, 2, cost);
            SetCostButtonVisual(P3SingleTroopMoveButton, 3, cost);
            SetCostButtonVisual(P4SingleTroopMoveButton, 4, cost);
        }

        private void SetCostButtonVisual(Button button, int player, int cost)
        {
            button.IsEnabled = true;
            button.Background = GetGold(player) >= cost
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99));
        }

        private void UpdateReplayButtons()
        {
            bool redCanReplay = !_redReplayBoughtThisRound;
            bool blueCanReplay = !_blueReplayBoughtThisRound;

            P1ReplayButton.IsEnabled = true;
            P2ReplayButton.IsEnabled = true;
            P3ReplayButton.IsEnabled = true;
            P4ReplayButton.IsEnabled = true;

            PlayerPanelText.SetButtonContent(P1ReplayButton, redCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed"));
            PlayerPanelText.SetButtonContent(P2ReplayButton, redCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed"));
            PlayerPanelText.SetButtonContent(P3ReplayButton, blueCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed"));
            PlayerPanelText.SetButtonContent(P4ReplayButton, blueCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed"));

            P1ReplayButton.Background = (redCanReplay && GetGold(1) >= 10) ? new SolidColorBrush(Color.FromRgb(110, 169, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99));
            P2ReplayButton.Background = (redCanReplay && GetGold(2) >= 10) ? new SolidColorBrush(Color.FromRgb(110, 169, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99));
            P3ReplayButton.Background = (blueCanReplay && GetGold(3) >= 10) ? new SolidColorBrush(Color.FromRgb(110, 169, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99));
            P4ReplayButton.Background = (blueCanReplay && GetGold(4) >= 10) ? new SolidColorBrush(Color.FromRgb(110, 169, 200)) : new SolidColorBrush(Color.FromRgb(75, 85, 99));
        }

        // ─────────────────────────────────────────────────────────────────
        //  Custom spend — FIX: clear box on success
        // ─────────────────────────────────────────────────────────────────
        private void P1Spend_Click(object s, RoutedEventArgs e) => CustomSpend(1, P1SpendBox);
        private void P2Spend_Click(object s, RoutedEventArgs e) => CustomSpend(2, P2SpendBox);
        private void P3Spend_Click(object s, RoutedEventArgs e) => CustomSpend(3, P3SpendBox);
        private void P4Spend_Click(object s, RoutedEventArgs e) => CustomSpend(4, P4SpendBox);

        private void CustomSpend(int player, TextBox box)
        {
            string raw = IsPlaceholderText(box.Text) ? "" : box.Text.Trim();
            if (!int.TryParse(raw, out int amount) || amount <= 0)
            {
                ShowNotice(Loc.Get("EnterPositiveAmount"), NoticeType.Warning);
                return;
            }
            if (GetGold(player) < amount)
            {
                ShowNotice(Loc.Get("OnlyHasGold", player, GetGold(player), amount), NoticeType.Warning);
                return;
            }
            PushUndoSnapshot();
            AddGold(player, -amount);
            LogAction($"💸 {Loc.Get("LogSpent", player, amount)}");
            ShowNotice(Loc.Get("NoticeSpent", player, amount), NoticeType.Info);
            ClearInputBox(box);
        }

        // ─────────────────────────────────────────────────────────────────
        //  BFT — FIX: clear box on success
        // ─────────────────────────────────────────────────────────────────
        private void P1BuyTeam_Click(object s, RoutedEventArgs e) => BuyForTeammate(1, P1BuyTeamBox);
        private void P2BuyTeam_Click(object s, RoutedEventArgs e) => BuyForTeammate(2, P2BuyTeamBox);
        private void P3BuyTeam_Click(object s, RoutedEventArgs e) => BuyForTeammate(3, P3BuyTeamBox);
        private void P4BuyTeam_Click(object s, RoutedEventArgs e) => BuyForTeammate(4, P4BuyTeamBox);

        private void BuyForTeammate(int player, TextBox box)
        {
            string raw = IsPlaceholderText(box.Text) ? "" : box.Text.Trim();
            if (!int.TryParse(raw, out int unitCost) || unitCost <= 0)
            {
                ShowNotice(Loc.Get("EnterValidUnitCost"), NoticeType.Warning);
                return;
            }
            int surcharge = player <= 2 ? _redBFTSurcharge : _blueBFTSurcharge;
            double pct = surcharge / 100.0;
            int total = (int)Math.Ceiling(unitCost * (1.0 + pct));

            if (GetGold(player) < total)
            {
                ShowNotice(Loc.Get("NeedsGoldFor", player, total, Loc.Get("BFT"), GetGold(player)), NoticeType.Warning);
                return;
            }
            PushUndoSnapshot();
            AddGold(player, -total);
            LogAction($"🤝 {Loc.Get("LogBFT", player, unitCost, total, surcharge)}");
            ShowNotice(Loc.Get("NoticeBFT", player, total, surcharge), NoticeType.Info);
            ClearInputBox(box);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Sell unit — FIX: clear box on success
        // ─────────────────────────────────────────────────────────────────
        private void P1SellUnit_Click(object s, RoutedEventArgs e) => SellUnit(1, P1UnitBox);
        private void P2SellUnit_Click(object s, RoutedEventArgs e) => SellUnit(2, P2UnitBox);
        private void P3SellUnit_Click(object s, RoutedEventArgs e) => SellUnit(3, P3UnitBox);
        private void P4SellUnit_Click(object s, RoutedEventArgs e) => SellUnit(4, P4UnitBox);

        private void SellUnit(int player, TextBox box)
        {
            string raw = IsPlaceholderText(box.Text) ? "" : box.Text.Trim();
            if (!int.TryParse(raw, out int value) || value <= 0)
            {
                ShowNotice(Loc.Get("EnterValidUnitValue"), NoticeType.Warning);
                return;
            }

            int basePct = GetBaseSellbackPct(player);
            int bonusPct = GetNextSellBonusPct(player);
            int totalPct = basePct + bonusPct;
            double pct = totalPct / 100.0;
            int returns = (int)Math.Floor(value * pct);

            PushUndoSnapshot();
            AddGold(player, returns);

            if (bonusPct > 0)
                SetNextSellBonusPct(player, 0);

            LogAction($"💱 {Loc.Get("LogSoldUnit", player, value, returns, totalPct)}");
            ShowNotice(Loc.Get("NoticeSoldUnit", player, returns, totalPct), NoticeType.Success);
            ClearInputBox(box);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  ClearInputBox — clears the value and restores the translated placeholder
        // ─────────────────────────────────────────────────────────────────
        private void ClearInputBox(TextBox box)
        {
            if (box == null) return;

            box.Text = "";
            SetInputPlaceholder(box);
        }        // ─────────────────────────────────────────────────────────────────
                 //  Calc texts — FIX: only show win or loss line, not both
                 // ─────────────────────────────────────────────────────────────────
                 // ─────────────────────────────────────────────────────────────────
                 //  Calc texts — stored from the actual applied round
                 // ─────────────────────────────────────────────────────────────────
        private void UpdateCalcTexts()
        {
            P1CalcText.Text = _p1LastCalcText;
            P2CalcText.Text = _p2LastCalcText;
            P3CalcText.Text = _p3LastCalcText;
            P4CalcText.Text = _p4LastCalcText;
        }

        private string BuildCalcText(int startGold, int interest, int roundReward, int income, int milestoneBonus, int finalGold)
        {
            var lines = new List<string>
    {
        $"{Loc.Get("StartingGold")}: {startGold}"
    };

            if (interest > 0)
                lines.Add($"{Loc.Get("Interest")}: +{interest}");

            if (milestoneBonus > 0)
                lines.Add($"{Loc.Get("MilestoneReward")}: +{milestoneBonus}");

            lines.Add($"{Loc.Get("RoundReward")}: +{roundReward}");

            if (income > 0)
                lines.Add($"{Loc.Get("PermanentIncome")}: +{income}");

            lines.Add($"{Loc.Get("FinalGold")}: {finalGold}");

            return string.Join(Environment.NewLine, lines);
        }

        private void CaptureRoundCalcSnapshots(int winner)
        {
            GetRoundRewardValues(out int winnerReward, out int loserReward);
            int tieReward = GetTieRewardValue();

            if (winner == 3)
            {
                CapturePlayerCalcSnapshot(1, false, winnerReward, loserReward, true, tieReward);
                CapturePlayerCalcSnapshot(2, false, winnerReward, loserReward, true, tieReward);
                CapturePlayerCalcSnapshot(3, false, winnerReward, loserReward, true, tieReward);
                CapturePlayerCalcSnapshot(4, false, winnerReward, loserReward, true, tieReward);
                return;
            }

            bool redWon = winner == 1;
            bool blueWon = winner == 2;

            CapturePlayerCalcSnapshot(1, redWon, winnerReward, loserReward, false, tieReward);
            CapturePlayerCalcSnapshot(2, redWon, winnerReward, loserReward, false, tieReward);
            CapturePlayerCalcSnapshot(3, blueWon, winnerReward, loserReward, false, tieReward);
            CapturePlayerCalcSnapshot(4, blueWon, winnerReward, loserReward, false, tieReward);
        }

        private void CapturePlayerCalcSnapshot(int player, bool won, int winnerReward, int loserReward, bool tied, int tieReward)
        {
            int startGold = GetGold(player);
            int interest = CalcInterest(startGold);
            int income = IsIncomeAvailable() ? GetIncome(player) : 0;
            int roundReward = tied ? tieReward : (won ? winnerReward : loserReward);
            int milestoneBonus = 0;
            int finalGold = startGold + interest + income + roundReward + milestoneBonus;

            string calc = BuildCalcText(startGold, interest, roundReward, income, milestoneBonus, finalGold);

            switch (player)
            {
                case 1: _p1LastCalcText = calc; break;
                case 2: _p2LastCalcText = calc; break;
                case 3: _p3LastCalcText = calc; break;
                case 4: _p4LastCalcText = calc; break;
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Faction helpers
        // ─────────────────────────────────────────────────────────────────
        private void AssignRandomFactions()
        {
            _p1Factions = DrawRandomFactions(StartingFactionCount); _p2Factions = DrawRandomFactions(StartingFactionCount);
            _p3Factions = DrawRandomFactions(StartingFactionCount); _p4Factions = DrawRandomFactions(StartingFactionCount);
        }

        private List<string> DrawRandomFactions(int count)
        {
            var pool = new List<string>(AllFactions);
            var result = new List<string>();

            for (int i = 0; i < count && pool.Count > 0; i++)
            {
                int idx = _rng.Next(pool.Count);
                result.Add(pool[idx]);
                pool.RemoveAt(idx);
            }

            return result;
        }

        // ─────────────────────────────────────────────────────────────────
        //  UI update helpers
        // ─────────────────────────────────────────────────────────────────
        private void UpdateIncomeButtons()
        {
            if (!IsIncomeAvailable())
            {
                foreach (int p in new[] { 1, 2, 3, 4 })
                {
                    GetBuyIncomeButton(p).Visibility = Visibility.Collapsed;
                }
                P1IncomeBadgeBorder.Visibility = Visibility.Collapsed;
                P2IncomeBadgeBorder.Visibility = Visibility.Collapsed;
                P3IncomeBadgeBorder.Visibility = Visibility.Collapsed;
                P4IncomeBadgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            int incomeGain = _ft20ModeEnabled ? 13 : 10;

            UpdateIncomeDiscountBadge(1, P1BuyIncomeButton, P1IncomeDecayPctText, P1IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(2, P2BuyIncomeButton, P2IncomeDecayPctText, P2IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(3, P3BuyIncomeButton, P3IncomeDecayPctText, P3IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(4, P4BuyIncomeButton, P4IncomeDecayPctText, P4IncomeBadgeBorder, incomeGain);
        }

        private void UpdateIncomeDiscountBadge(int player, Button button, TextBlock badgeText, Border badgeBorder, int incomeGain)
        {
            button.Visibility = Visibility.Visible;
            int shownCost = GetDisplayedIncomeCost(player);
            int totalDiscountPct = GetIncomeDecayPct(player) + GetNextIncomeDiscountPct(player);

            bool canBuy = !GetBoughtIncomeThisRound(player) && GetGold(player) >= shownCost;

            PlayerPanelText.SetButtonContent(button, $"{Loc.Get(_ft20ModeEnabled ? "BuyIncomeF" : "BuyIncome").Split('(')[0].Trim()} ({shownCost}g)");
            button.IsEnabled = true;
            button.Background = canBuy
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99));

            if (totalDiscountPct > 0)
            {
                badgeText.Text = $"-{totalDiscountPct}%";
                badgeText.Foreground = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#7FF0B0"));
                badgeBorder.Background = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#1F4B3A"));
                badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                badgeText.Text = "";
                badgeBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePermMoveButtons()
        {
            UpdatePermMoveButton(P1BuyPermMoveButton, P1PermMoveDiscountText, P1PermMoveDiscountBorder, 1);
            UpdatePermMoveButton(P2BuyPermMoveButton, P2PermMoveDiscountText, P2PermMoveDiscountBorder, 2);
            UpdatePermMoveButton(P3BuyPermMoveButton, P3PermMoveDiscountText, P3PermMoveDiscountBorder, 3);
            UpdatePermMoveButton(P4BuyPermMoveButton, P4PermMoveDiscountText, P4PermMoveDiscountBorder, 4);
        }

        private void UpdatePermMoveButton(Button button, TextBlock badgeText, Border badgeBorder, int player)
        {
            int max = GetPermMoveMaxPurchases(player);
            int purchases = GetPermMovePurchases(player);
            int baseCost = GetPermMoveBaseCost();
            int discountPct = GetNextPermMoveDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            bool canBuy = purchases < max && GetGold(player) >= shownCost;

            PlayerPanelText.SetButtonContent(button, $"{Loc.Get(_ft20ModeEnabled ? "BuyPermMoveF" : "BuyPermMove").Split('(')[0].Trim()} ({shownCost}g) [{purchases}/{max}]");
            button.IsEnabled = true;
            button.Background = canBuy
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99));

            if (discountPct > 0)
            {
                badgeText.Text = $"-{discountPct}%";
                badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                badgeText.Text = "";
                badgeBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdatePermMoveButton(Button button, int player)
        {
            int max = GetPermMoveMaxPurchases(player);
            int purchases = GetPermMovePurchases(player);
            int baseCost = GetPermMoveBaseCost();
            int discountPct = GetNextPermMoveDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            PlayerPanelText.SetButtonContent(button, $"Buy perm move +1 ({shownCost}g) [{purchases}/{max}]");
            button.IsEnabled = purchases < max;

            TextBlock badge = player == 1 ? P1PermMoveDiscountText
                            : player == 2 ? P2PermMoveDiscountText
                            : player == 3 ? P3PermMoveDiscountText
                            : P4PermMoveDiscountText;

            if (discountPct > 0)
            {
                badge.Text = $"-{discountPct}%";
                badge.Visibility = Visibility.Visible;
            }
            else
            {
                badge.Text = "";
                badge.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateFactionButtons()
        {
            bool canShowFactionButtons = _factionModeEnabled;

            UpdateFactionButton(P1BuyFactionButton, P1FactionDiscountText, P1FactionDiscountBorder, 1, canShowFactionButtons);
            UpdateFactionButton(P2BuyFactionButton, P2FactionDiscountText, P2FactionDiscountBorder, 2, canShowFactionButtons);
            UpdateFactionButton(P3BuyFactionButton, P3FactionDiscountText, P3FactionDiscountBorder, 3, canShowFactionButtons);
            UpdateFactionButton(P4BuyFactionButton, P4FactionDiscountText, P4FactionDiscountBorder, 4, canShowFactionButtons);

            UpdateChosenFactionButton(_p1BuyChosenFactionButton, _p1ChosenFactionDiscountBorder, _p1ChosenFactionDiscountText, 1, canShowFactionButtons);
            UpdateChosenFactionButton(_p2BuyChosenFactionButton, _p2ChosenFactionDiscountBorder, _p2ChosenFactionDiscountText, 2, canShowFactionButtons);
            UpdateChosenFactionButton(_p3BuyChosenFactionButton, _p3ChosenFactionDiscountBorder, _p3ChosenFactionDiscountText, 3, canShowFactionButtons);
            UpdateChosenFactionButton(_p4BuyChosenFactionButton, _p4ChosenFactionDiscountBorder, _p4ChosenFactionDiscountText, 4, canShowFactionButtons);
        }

        private void UpdateFactionButton(Button button, TextBlock badgeText, Border badgeBorder, int player, bool canShowFactionButtons)
        {
            button.Visibility = canShowFactionButtons ? Visibility.Visible : Visibility.Collapsed;

            if (!canShowFactionButtons)
            {
                badgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            int purchases = GetFactionPurchases(player);

            if (purchases >= AllFactions.Count)
            {
                PlayerPanelText.SetButtonContent(button, Loc.Get("MaxFactions"));
                button.IsEnabled = true;
                button.Background = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                badgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            int baseCost = GetFactionCost(player);
            int discountPct = GetNextFactionDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            bool canBuy = GetGold(player) >= shownCost;

            PlayerPanelText.SetButtonContent(button, $"{Loc.Get("BuyFaction").Split('(')[0].Trim()} ({shownCost}g)");
            button.IsEnabled = true;
            button.Background = canBuy
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99));

            if (discountPct > 0)
            {
                badgeText.Text = $"-{discountPct}%";
                badgeBorder.Visibility = Visibility.Visible;
            }
            else
            {
                badgeText.Text = "";
                badgeBorder.Visibility = Visibility.Collapsed;
            }
        }

        private void UpdateChosenFactionButton(Button button, Border badgeBorder, TextBlock badgeText, int player, bool canShowFactionButtons)
        {
            if (button == null) return;

            var row = button.Parent as FrameworkElement;
            if (row != null)
                row.Visibility = canShowFactionButtons ? Visibility.Visible : Visibility.Collapsed;

            if (!canShowFactionButtons) return;

            int discountPct = GetNextChosenFactionDiscountPct(player);
            int cost = GetDisplayedChosenFactionCost(player);
            bool can = GetFactions(player).Count < AllFactions.Count && GetGold(player) >= cost;

            PlayerPanelText.SetButtonContent(button, Loc.Get("BuyChosenFaction", cost));
            button.IsEnabled = true;
            button.Background = can
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(75, 85, 99));

            if (badgeBorder != null && badgeText != null)
            {
                badgeText.Text = discountPct > 0 ? $"-{discountPct}%" : "";
                badgeBorder.Visibility = discountPct > 0 ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void UpdateFactionIcons()
        {
            RefreshFactionIcons(P1FactionIconsPanel, _p1Factions);
            RefreshFactionIcons(P2FactionIconsPanel, _p2Factions);
            RefreshFactionIcons(P3FactionIconsPanel, _p3Factions);
            RefreshFactionIcons(P4FactionIconsPanel, _p4Factions);
        }

        private void RefreshFactionIcons(WrapPanel panel, List<string> factions)
        {
            panel.Children.Clear();

            foreach (var faction in factions)
            {
                panel.Children.Add(BuildFactionIcon(faction));
            }
        }

        private FrameworkElement BuildFactionIcon(string faction)
        {
            var file = FactionIconMap.ContainsKey(faction) ? FactionIconMap[faction] : null;

            var border = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(10),
                Margin = new Thickness(0, 0, 8, 8),
                Background = new SolidColorBrush(Color.FromRgb(26, 28, 31)),
                ClipToBounds = true
            };

            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    var img = new Image
                    {
                        Stretch = Stretch.Uniform,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        Margin = new Thickness(0),
                        Source = new System.Windows.Media.Imaging.BitmapImage(
        new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute))
                    };

                    if (string.Equals(faction, "New Units 2", StringComparison.OrdinalIgnoreCase))
                    {
                        img.RenderTransform = new TranslateTransform(4, 0);
                    }

                    border.Child = img;
                }
                catch
                {
                    border.Child = new TextBlock
                    {
                        Text = faction.Substring(0, 1),
                        Foreground = Brushes.White,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        TextAlignment = TextAlignment.Center,
                        FontWeight = FontWeights.Bold,
                        Margin = new Thickness(0, 7, 0, 0)
                    };
                }
            }
            else
            {
                border.Child = new TextBlock
                {
                    Text = faction.Substring(0, 1),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    TextAlignment = TextAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 7, 0, 0)
                };
            }

            return border;
        }

        private void UpdateSellPctDisplays()
        {
            P1SellPctText.Text = $"{GetDisplayedSellPct(1)}%";
            P2SellPctText.Text = $"{GetDisplayedSellPct(2)}%";
            P3SellPctText.Text = $"{GetDisplayedSellPct(3)}%";
            P4SellPctText.Text = $"{GetDisplayedSellPct(4)}%";
        }

        private void UpdateBFTDisplays()
        {
            string r = $"+{_redBFTSurcharge}%";
            string b = $"+{_blueBFTSurcharge}%";
            P1BuyTeamPctText.Text = r; P2BuyTeamPctText.Text = r;
            P3BuyTeamPctText.Text = b; P4BuyTeamPctText.Text = b;
        }

        // ─────────────────────────────────────────────────────────────────
        //  FT20 info panel
        // ─────────────────────────────────────────────────────────────────
        private void RefreshFT20InfoPanel()
        {
            int redAway = Math.Max(0, _ft20NextMilestone - _redPoints);
            int blueAway = Math.Max(0, _ft20NextMilestone - _bluePoints);
            SetMilestoneFlagText(MilestoneP1Text, RedFlagBrush, Loc.Get("RedTeam"), redAway);
            SetMilestoneFlagText(MilestoneP2Text, BlueFlagBrush, Loc.Get("BlueTeam"), blueAway);

            if (_ft20RewardsRemaining.Count > 0)
            {
                string next = _ft20RewardsRemaining[0];
                MilestoneNextRewardText.Text = LocalizeReward(next);
                MilestoneNextRewardIcon.Text = GetRewardIcon(next);
            }
            else
            {
                MilestoneNextRewardText.Text = Loc.Get("AllRewardsClaimed");
                MilestoneNextRewardIcon.Text = "🏆";
            }

            MilestoneRewardsLeftPanel.Children.Clear();
            foreach (var g in _ft20RewardsRemaining
                .GroupBy(r => r).OrderBy(g => g.Key))
            {
                MilestoneRewardsLeftPanel.Children.Add(new TextBlock
                {
                    Text = $"{g.Count()} {GetRewardIcon(g.Key)} {LocalizeReward(g.Key)}",
                    FontSize = 17,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#E8EDF3")),
                    FontWeight = FontWeights.SemiBold,
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 4)
                });
            }
        }

        private string GetRewardIcon(string reward)
        {
            switch (reward)
            {
                case "80% Off Next Faction": return "⚔";
                case "80% Off Next Chosen Faction": return "🎯";
                case "80% Off Next Perm Move": return "🗡";
                case "Sellback +15%": return "💰";
                case "10% Off Next Income": return "📉";
                case "+30% Next Sell": return "🔄";
                case "-5% BFT Surcharge": return "🤝";
                default: return "★";
            }
        }        // ─────────────────────────────────────────────────────────────────
        //  Notice system
        // ─────────────────────────────────────────────────────────────────
        private enum NoticeType { Info, Success, Warning, Milestone }

        private void ShowNotice(string message, NoticeType type = NoticeType.Info)
        {
            IncomeNoticeText.Text = message;

            IncomeNoticePopup.PlacementTarget = AppScroll;
            IncomeNoticePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
            IncomeNoticePopup.HorizontalOffset = Math.Max(0, AppScroll.ActualWidth - 800);
            IncomeNoticePopup.VerticalOffset = 10;

            IncomeNoticePopup.IsOpen = true;
            _noticeTimer.Stop();
            _noticeTimer.Start();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Mode toggles
        // ─────────────────────────────────────────────────────────────────
        private void FactionModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_round > 1 || _factionModeLocked) return;
            CloseAllGoldWindows();
            _factionModeEnabled = !_factionModeEnabled;
            _firstTurnChosen = false;
            TurnOrderText.Text = Loc.Get("NotAvailableYet");

            if (_factionModeEnabled)
            {
                ResetAllPlayerPanelsForModeSwap();
                AssignRandomFactions();
                if (IsTimedMilestoneMode()) BuildFT20RewardPool();
                else BuildSharedMilestonePool();
                LogAction($"⚙️ {Loc.Get("LogFactionModeOn")}");
                ShowNotice(Loc.Get("NoticeFactionModeOn"), NoticeType.Info);
            }
            else
            {
                ResetAllPlayerPanelsForModeSwap();
                _p1Factions.Clear(); _p2Factions.Clear();
                _p3Factions.Clear(); _p4Factions.Clear();
                if (IsTimedMilestoneMode()) BuildFT20RewardPool();
                else BuildSharedMilestonePool();
                LogAction($"⚙️ {Loc.Get("LogFactionModeOff")}");
                ShowNotice(Loc.Get("NoticeFactionModeOff"), NoticeType.Info);
            }
            RefreshAllUI();
        }

        private void FactionToggleButton_Click(object sender, RoutedEventArgs e)
        {
            FactionModeToggleButton_Click(sender, e);
        }

        private void FT20ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_round > 1 || _ft20ModeLocked) return;
            CloseAllGoldWindows();
            _ft30ModeEnabled = !_ft30ModeEnabled;
            if (_ft30ModeEnabled) _ft10ModeEnabled = false;
            NormalizeMatchModeFlags();
            _firstTurnChosen = false;
            TurnOrderText.Text = Loc.Get("NotAvailableYet");
            ResetAllPlayerPanelsForModeSwap();
            if (_factionModeEnabled) AssignRandomFactions();
            if (IsTimedMilestoneMode()) BuildFT20RewardPool();
            else BuildSharedMilestonePool();
            LogAction(_ft30ModeEnabled ? Loc.Get("LogFT30ModeOn") : Loc.Get("LogFT30ModeOff"));
            ShowNotice(Loc.Get(_ft30ModeEnabled ? "NoticeFT30ModeOn" : "NoticeFT30ModeOff"), NoticeType.Info);
            RefreshAllUI();
        }

        private void FT10ModeToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (_round > 1 || _ft20ModeLocked) return;
            CloseAllGoldWindows();
            _ft10ModeEnabled = !_ft10ModeEnabled;
            if (_ft10ModeEnabled) _ft30ModeEnabled = false;
            NormalizeMatchModeFlags();
            _firstTurnChosen = false;
            TurnOrderText.Text = Loc.Get("NotAvailableYet");
            ResetAllPlayerPanelsForModeSwap();
            if (_factionModeEnabled) AssignRandomFactions();
            if (IsTimedMilestoneMode()) BuildFT20RewardPool();
            else BuildSharedMilestonePool();
            LogAction(_ft10ModeEnabled ? Loc.Get("LogFT10ModeOn") : Loc.Get("LogFT10ModeOff"));
            ShowNotice(Loc.Get(_ft10ModeEnabled ? "NoticeFT10ModeOn" : "NoticeFT10ModeOff"), NoticeType.Info);
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Undo
        // ─────────────────────────────────────────────────────────────────
        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (_undoStack == null || _undoStack.Count == 0)
            {
                ShowNotice(Loc.Get("NothingToUndo"), NoticeType.Warning);
                return;
            }

            var snapshot = _undoStack.Pop();
            RestoreFromSaveData(snapshot);
            LogAction($"↩️ {Loc.Get("Undo")}.");
            ShowNotice(Loc.Get("Undo") + ".", NoticeType.Info);
            RefreshAllUI();
        }

        // ── Gold pop-out ──────────────────────────────────────────────────
        private void P1PopOut_Click(object s, RoutedEventArgs e) => PopOutGold(1);
        private void P2PopOut_Click(object s, RoutedEventArgs e) => PopOutGold(2);
        private void P3PopOut_Click(object s, RoutedEventArgs e) => PopOutGold(3);
        private void P4PopOut_Click(object s, RoutedEventArgs e) => PopOutGold(4);

        private void PopOutGold(int player)
        {
            var existing = GetGoldWindow(player);
            if (existing != null) { existing.Activate(); return; }

            var window = new GoldPopOutWindow(
    GetPlayerName(player),
    GetGold(player),
    GetGoldState(player),
    GetFactions(player),
    FactionIconMap,
    () =>
    {
        SetGoldWindow(player, null);
    });

            SetGoldWindow(player, window);
            window.Show();
        }

        private GoldPopOutWindow GetGoldWindow(int p) { switch (p) { case 1: return _p1GoldWindow; case 2: return _p2GoldWindow; case 3: return _p3GoldWindow; default: return _p4GoldWindow; } }
        private void SetGoldWindow(int p, GoldPopOutWindow w) { switch (p) { case 1: _p1GoldWindow = w; break; case 2: _p2GoldWindow = w; break; case 3: _p3GoldWindow = w; break; default: _p4GoldWindow = w; break; } }
        private Border GetGoldBorder(int p) { switch (p) { case 1: return P1GoldBorder; case 2: return P2GoldBorder; case 3: return P3GoldBorder; default: return P4GoldBorder; } }
        private string GetPlayerName(int p) { switch (p) { case 1: return P1NameBox.Text; case 2: return P2NameBox.Text; case 3: return P3NameBox.Text; default: return P4NameBox.Text; } }
        private int GetGoldState(int p) { switch (p) { case 1: return _p1GoldState; case 2: return _p2GoldState; case 3: return _p3GoldState; default: return _p4GoldState; } }

        private void CloseAllGoldWindows()
        {
            for (int p = 1; p <= 4; p++)
            {
                var w = GetGoldWindow(p);
                if (w != null) { w.Closed -= null; w.Close(); SetGoldWindow(p, null); }
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Name edit buttons — just focus the textbox
        // ─────────────────────────────────────────────────────────────────
        private void P1NameEdit_Click(object s, RoutedEventArgs e) => ToggleNameLock(1);
        private void P2NameEdit_Click(object s, RoutedEventArgs e) => ToggleNameLock(2);
        private void P3NameEdit_Click(object s, RoutedEventArgs e) => ToggleNameLock(3);
        private void P4NameEdit_Click(object s, RoutedEventArgs e) => ToggleNameLock(4);

        private void ToggleNameLock(int player)
        {
            var box = GetNameBox(player);
            var button = GetNameEditButton(player);

            if (box.IsReadOnly)
            {
                box.IsReadOnly = false;
                box.TextAlignment = TextAlignment.Left;
                box.Visibility = Visibility.Visible;
                GetNameDisplayText(player).Visibility = Visibility.Collapsed;

                button.Content = Loc.Get("Set");
                box.Focus();
                box.CaretIndex = box.Text.Length;
            }
            else
            {
                LockNameBox(player);
            }

            _namesLocked = AreAnyNamesLocked();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Action log
        // ─────────────────────────────────────────────────────────────────
        private void LogAction(string message)
        {
            _actionLog.Insert(0, $"[R{_round}] {message}");
            if (_actionLog.Count > 80) _actionLog.RemoveAt(_actionLog.Count - 1);
            RefreshActionLog();
        }

        private void RefreshActionLog()
        {
            ActionLogPanel.Children.Clear();
            foreach (var entry in _actionLog.Take(30))
            {
                ActionLogPanel.Children.Add(new TextBlock
                {
                    Text = entry,
                    FontSize = 11,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                    TextWrapping = TextWrapping.Wrap,
                    Margin = new Thickness(0, 0, 0, 3)
                });
            }
        }

        // ─────────────────────────────────────────────────────────────────
        //  Navigation / zoom
        // ─────────────────────────────────────────────────────────────────
        private void MainMenuButton_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ThemedConfirmDialog(
                Loc.Get("MainMenuConfirmTitle"),
                Loc.Get("MainMenuConfirmMsg"))
            { Owner = Window.GetWindow(this) };
            if (confirm.ShowDialog() != true) return;

            CloseAllGoldWindows();

            if (NavigationService?.CanGoBack == true)
            { NavigationService.GoBack(); return; }
            var s = new StartScreen(); s.Show();
            Window.GetWindow(this)?.Close();
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
            GuideOverlay.Visibility = Visibility.Collapsed;
            SettingsOverlay.Visibility = Visibility.Visible;
            bool isFullscreen = _mainWindowIsFullscreen();
            UpdateSettingsButtonStyles(isFullscreen);
            UpdateSoundSettingsUI();
        }

        private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void GuideButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
            PopulateGuideContent();
            GuideOverlay.Visibility = Visibility.Visible;
        }

        private void GuideBackButton_Click(object sender, RoutedEventArgs e)
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
        }

        private void PopulateGuideContent()
        {
            GuideTitleText.Text = Loc.Get("GuideTitle");
            GuideContentPanel.Children.Clear();

            AddGuideSection(Loc.Get("GuideBasicsTitle"), Loc.Get("GuideBasicsBody"));
            AddGuideSection(Loc.Get("GuideTurnOrderTitle"), Loc.Get("GuideTurnOrderBody"));
            AddGuideSection(Loc.Get("GuideRoundTitle"), Loc.Get("GuideRoundBody"));
            AddGuideSection(Loc.Get("GuideEconomyTitle"), Loc.Get("GuideEconomyBody"));
            AddGuideSection(Loc.Get("GuideRulesTitle"), Loc.Get("GuideRulesBody"));
            AddGuideSection(Loc.Get("GuideSavingTitle"), Loc.Get("GuideSavingBody"));
            AddGuideLinkSection();
        }

        private void AddGuideSection(string title, string body)
        {
            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 39, 47)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = body,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            });

            border.Child = stack;
            GuideContentPanel.Children.Add(border);
        }

        private void AddGuideLinkSection()
        {
            const string url = "https://github.com/ofallzei/TABS-Arena";

            var border = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(35, 39, 47)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10)
            };

            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = Loc.Get("GuideMoreTitle"),
                Foreground = Brushes.White,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var text = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                FontSize = 14,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            };

            text.Inlines.Add(new Run(Loc.Get("GuideMoreBody") + " "));

            var link = new System.Windows.Documents.Hyperlink(new Run("ofallzei/TABS-Arena"))
            {
                NavigateUri = new Uri(url),
                Foreground = new SolidColorBrush(Color.FromRgb(110, 182, 218))
            };

            link.RequestNavigate += (s, e) =>
            {
                e.Handled = true;

                Process.Start(new ProcessStartInfo
                {
                    FileName = e.Uri.AbsoluteUri,
                    UseShellExecute = true
                });
            };

            text.Inlines.Add(link);

            stack.Children.Add(text);
            border.Child = stack;
            GuideContentPanel.Children.Add(border);
        }

        private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            bool atTop = sv.VerticalOffset <= 0;
            bool atBottom = sv.VerticalOffset >= sv.ScrollableHeight;

            // If we can scroll inside the settings box, let it handle it
            if ((!atTop && e.Delta > 0) || (!atBottom && e.Delta < 0))
            {
                // let the ScrollViewer handle it normally
                return;
            }

            // Otherwise pass the scroll event up to the main AppScroll
            e.Handled = true;
            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            AppScroll.RaiseEvent(args);
        }

        private void SettingsLanguageLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(Loc.PreviousLanguage(Loc.Current));
        }

        private void SettingsLanguageRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(Loc.NextLanguage(Loc.Current));
        }

        private void ApplyLanguage(Loc.Language lang)
        {
            Loc.Current = lang;
            Loc.SaveLanguage();
            AppPrefs.Language = lang;
            AppPrefs.Save();

            UpdateLanguageSelectorUI();

            // Settings labels
            SettingsWindowModeText.Text = _mainWindowIsFullscreen()
                ? Loc.Get("BorderlessFullscreen") : Loc.Get("Windowed");
            SettingsLanguageLabel.Text = Loc.Get("Language");

            // Overview
            UpdateAllUI();
        }

        private void UpdateAllUI()
        {
            // Round/turn/result displays don't need retranslation (they are dynamic)
            // Buttons and static labels:

            // First turn prompt
            RedTeamFirstTurnButton.Content = Loc.Get("RedTeamFirst");
            BlueTeamFirstTurnButton.Content = Loc.Get("BlueTeamFirst");

            // Top bar
            MainMenuButton.Content = Loc.Get("MainMenu");
            AppTitleText.Text = Loc.Get("AppTitle");
            SettingsBackButton.Content = Loc.Get("Back");
            SettingsTitleText.Text = Loc.Get("Settings");
            GuideBackButton.Content = Loc.Get("Back");
            GuideTitleText.Text = Loc.Get("GuideTitle");
            GuideButton.ToolTip = Loc.Get("Guide");
            SettingsWindowModeLabel.Text = Loc.Get("WindowMode");
            SettingsLanguageLabel.Text = Loc.Get("Language");
            SettingsSoundsLabel.Text = Loc.Get("Sounds");
            SettingsSoundVolumeLabel.Text = Loc.Get("Volume");
            UpdateSoundSettingsUI();

            // Overview static labels
            OverviewTitle.Text = Loc.Get("OverviewTitle");
            OverviewSub.Text = Loc.Get("OverviewSub");
            LblCurrentRound.Text = Loc.Get("CurrentRound");
            LblNextTurnOrder.Text = Loc.Get("NextTurnOrder");
            LblPendingResult.Text = Loc.Get("PendingResult");
            LblFactionMode.Text = Loc.Get("FactionMode");
            LblFT20Mode.Text = Loc.Get("FT30Mode");
            LblFT10Mode.Text = Loc.Get("FT10Mode");
            LblWhichTeamFirst.Text = Loc.Get("WhichTeamFirst");
            LblMatchSaves.Text = Loc.Get("MatchSaves");
            SaveButton.Content = Loc.Get("Save");
            LoadButton.Content = Loc.Get("Load");
            DeleteButton.Content = Loc.Get("Delete");
            NewGameButton.Content = Loc.Get("NewGame");
            LblMilestoneProgress.Text = Loc.Get("MilestoneProgress");
            LblNextReward.Text = Loc.Get("NextReward");
            LblRewardsLeft.Text = Loc.Get("RewardsLeft");
            LblActionLog.Text = Loc.Get("ActionLog");
            LblActionLogSub.Text = Loc.Get("ActionLogSub");
            LblRoundControl.Text = Loc.Get("RoundControl");

            // Round control buttons
            RedTeamWinsButton.Content = Loc.Get("RedTeamWins");
            TieButton.Content = Loc.Get("Tie");
            BlueTeamWinsButton.Content = Loc.Get("BlueTeamWins");
            NextRoundButton.Content = Loc.Get("NextRound");
            UndoButton.Content = Loc.Get("Undo");
            UpdateTieTimerUi();

            // Faction/FT20 toggles
            bool factionOn = FactionModeToggleButton.Tag?.ToString() == "True";
            bool ft30On = FT20ModeToggleButton.Tag?.ToString() == "True";
            bool ft10On = FT10ModeToggleButton.Tag?.ToString() == "True";
            FactionModeToggleButton.Content = factionOn ? Loc.Get("FactionModeOn") : Loc.Get("FactionModeOff");
            FT20ModeToggleButton.Content = ft30On ? Loc.Get("FT30ModeOn") : Loc.Get("FT30ModeOff");
            FT10ModeToggleButton.Content = ft10On ? Loc.Get("FT10ModeOn") : Loc.Get("FT10ModeOff");

            // Per-player buttons (all 4 players)
            foreach (int p in new[] { 1, 2, 3, 4 })
            {
                bool isFT20 = _ft20ModeEnabled;
                PlayerPanelText.SetButtonContent(GetBuyIncomeButton(p), Loc.Get(isFT20 ? "BuyIncomeF" : "BuyIncome"));
                PlayerPanelText.SetButtonContent(
                    GetBuyPermMoveButton(p),
                    Loc.Get(isFT20 ? "BuyPermMoveF" : "BuyPermMove") + $" [{GetPermMovePurchases(p)}/{GetPermMoveMaxPurchases(p)}]");
                PlayerPanelText.SetButtonContent(GetBuyFactionButton(p), Loc.Get("BuyFaction"));
                PlayerPanelText.SetButtonContent(GetSingleTroopMoveButton(p), Loc.Get("SingleTroopMove", GetSingleTroopMoveCost()));
                PlayerPanelText.SetButtonContent(GetReplayButton(p), Loc.Get("Replay"));
                GetSpendButton(p).Content = Loc.Get("Spend");
                GetBuyTeamButton(p).Content = Loc.Get("BFT");
                GetSellUnitButton(p).Content = Loc.Get("Sell");
                GetNameEditButton(p).Content = GetNameBox(p).IsReadOnly ? Loc.Get("Unset") : Loc.Get("Set");
            }

            // Team points bar
            SetTeamFlagText(LblRedTeamPoints, RedFlagBrush, Loc.Get("RedTeamPoints"));
            SetTeamFlagText(LblBlueTeamPoints, BlueFlagBrush, Loc.Get("BlueTeamPoints"));

            // Per-player static labels
            foreach (int p in new[] { 1, 2, 3, 4 })
            {
                bool isRed = p <= 2;
                GetLblTeam(p).Text = Loc.Get(isRed ? "RedTeam" : "BlueTeam");
                GetLblGold(p).Text = Loc.Get("Gold");
                GetLblPoints(p).Text = Loc.Get("Points");
                GetLblPermMv(p).Text = Loc.Get("PermMv");
                GetLblIncome(p).Text = Loc.Get("Income");
                GetLblInterest(p).Text = Loc.Get("InterestStat");
                GetLblUpgrades(p).Text = Loc.Get("Upgrades");
                GetLblUtility(p).Text = Loc.Get("Utility");
                GetLblCalculations(p).Text = Loc.Get("Calculations");
                GetLblFactionsOwned(p).Text = Loc.Get("FactionsOwned");

                var spendBox = GetSpendBox(p);
                var buyTeamBox = GetBuyTeamBox(p);
                var unitBox = GetUnitBox(p);
                RefreshInputPlaceholder(spendBox);
                RefreshInputPlaceholder(buyTeamBox);
                RefreshInputPlaceholder(unitBox);
            }

            // Refresh dynamic state text on language change
            if (!_firstTurnChosen)
                TurnOrderText.Text = Loc.Get("NotAvailableYet");

            PendingResultText.Text = _pendingWinner == 1 ? Loc.Get("RedTeamWins")
                : _pendingWinner == 2 ? Loc.Get("BlueTeamWins")
                : _pendingWinner == 3 ? Loc.Get("Tie")
                : Loc.Get("NotSet");

            if (IsTimedMilestoneMode()) RefreshFT20InfoPanel();
            else RefreshSharedMilestonePanel();

            UpdateReplayButtons();

            if (GuideOverlay.Visibility == Visibility.Visible)
                PopulateGuideContent();

            // Translate default player names if not customized
            if (P1NameBox.Text == "Red Player 1" || P1NameBox.Text == "Jugador Rojo 1")
                P1NameBox.Text = Loc.Get("DefaultP1Name");
            if (P2NameBox.Text == "Red Player 2" || P2NameBox.Text == "Jugador Rojo 2")
                P2NameBox.Text = Loc.Get("DefaultP2Name");
            if (P3NameBox.Text == "Blue Player 1" || P3NameBox.Text == "Jugador Azul 1")
                P3NameBox.Text = Loc.Get("DefaultP3Name");
            if (P4NameBox.Text == "Blue Player 2" || P4NameBox.Text == "Jugador Azul 2")
                P4NameBox.Text = Loc.Get("DefaultP4Name");
            for (int p = 1; p <= 4; p++)
                GetNameDisplayText(p).Text = GetNameBox(p).Text;
        }


        // Button accessor helpers for UpdateAllUI
        private Button GetBuyIncomeButton(int p) { switch (p) { case 1: return P1BuyIncomeButton; case 2: return P2BuyIncomeButton; case 3: return P3BuyIncomeButton; default: return P4BuyIncomeButton; } }
        private Button GetBuyPermMoveButton(int p) { switch (p) { case 1: return P1BuyPermMoveButton; case 2: return P2BuyPermMoveButton; case 3: return P3BuyPermMoveButton; default: return P4BuyPermMoveButton; } }
        private Button GetBuyFactionButton(int p) { switch (p) { case 1: return P1BuyFactionButton; case 2: return P2BuyFactionButton; case 3: return P3BuyFactionButton; default: return P4BuyFactionButton; } }
        private Button GetSingleTroopMoveButton(int p) { switch (p) { case 1: return P1SingleTroopMoveButton; case 2: return P2SingleTroopMoveButton; case 3: return P3SingleTroopMoveButton; default: return P4SingleTroopMoveButton; } }
        private Button GetReplayButton(int p) { switch (p) { case 1: return P1ReplayButton; case 2: return P2ReplayButton; case 3: return P3ReplayButton; default: return P4ReplayButton; } }
        private Button GetSpendButton(int p) { switch (p) { case 1: return P1SpendButton; case 2: return P2SpendButton; case 3: return P3SpendButton; default: return P4SpendButton; } }
        private Button GetBuyTeamButton(int p) { switch (p) { case 1: return P1BuyTeamButton; case 2: return P2BuyTeamButton; case 3: return P3BuyTeamButton; default: return P4BuyTeamButton; } }
        private Button GetSellUnitButton(int p) { switch (p) { case 1: return P1SellUnitButton; case 2: return P2SellUnitButton; case 3: return P3SellUnitButton; default: return P4SellUnitButton; } }
        private Button GetNameEditButton(int p) { switch (p) { case 1: return P1NameEditButton; case 2: return P2NameEditButton; case 3: return P3NameEditButton; default: return P4NameEditButton; } }
        private TextBox GetNameBox(int p) { switch (p) { case 1: return P1NameBox; case 2: return P2NameBox; case 3: return P3NameBox; default: return P4NameBox; } }
        private TextBlock GetNameDisplayText(int p) { switch (p) { case 1: return P1NameDisplayText; case 2: return P2NameDisplayText; case 3: return P3NameDisplayText; default: return P4NameDisplayText; } }

        private bool AreAnyNamesLocked()
        {
            return P1NameBox.IsReadOnly || P2NameBox.IsReadOnly ||
                   P3NameBox.IsReadOnly || P4NameBox.IsReadOnly;
        }


        private TextBlock GetLblTeam(int p) { switch (p) { case 1: return P1LblTeam; case 2: return P2LblTeam; case 3: return P3LblTeam; default: return P4LblTeam; } }
        private TextBlock GetLblGold(int p) { switch (p) { case 1: return P1LblGold; case 2: return P2LblGold; case 3: return P3LblGold; default: return P4LblGold; } }
        private TextBlock GetLblPoints(int p) { switch (p) { case 1: return P1LblPoints; case 2: return P2LblPoints; case 3: return P3LblPoints; default: return P4LblPoints; } }
        private TextBlock GetLblPermMv(int p) { switch (p) { case 1: return P1LblPermMv; case 2: return P2LblPermMv; case 3: return P3LblPermMv; default: return P4LblPermMv; } }
        private TextBlock GetLblIncome(int p) { switch (p) { case 1: return P1LblIncome; case 2: return P2LblIncome; case 3: return P3LblIncome; default: return P4LblIncome; } }
        private TextBlock GetLblInterest(int p) { switch (p) { case 1: return P1LblInterest; case 2: return P2LblInterest; case 3: return P3LblInterest; default: return P4LblInterest; } }
        private TextBlock GetLblUpgrades(int p) { switch (p) { case 1: return P1LblUpgrades; case 2: return P2LblUpgrades; case 3: return P3LblUpgrades; default: return P4LblUpgrades; } }
        private TextBlock GetLblUtility(int p) { switch (p) { case 1: return P1LblUtility; case 2: return P2LblUtility; case 3: return P3LblUtility; default: return P4LblUtility; } }
        private TextBlock GetLblCalculations(int p) { switch (p) { case 1: return P1LblCalculations; case 2: return P2LblCalculations; case 3: return P3LblCalculations; default: return P4LblCalculations; } }
        private TextBlock GetLblFactionsOwned(int p) { switch (p) { case 1: return P1LblFactionsOwned; case 2: return P2LblFactionsOwned; case 3: return P3LblFactionsOwned; default: return P4LblFactionsOwned; } }
        private TextBox GetSpendBox(int p) { switch (p) { case 1: return P1SpendBox; case 2: return P2SpendBox; case 3: return P3SpendBox; default: return P4SpendBox; } }
        private TextBox GetBuyTeamBox(int p) { switch (p) { case 1: return P1BuyTeamBox; case 2: return P2BuyTeamBox; case 3: return P3BuyTeamBox; default: return P4BuyTeamBox; } }
        private TextBox GetUnitBox(int p) { switch (p) { case 1: return P1UnitBox; case 2: return P2UnitBox; case 3: return P3UnitBox; default: return P4UnitBox; } }
        private bool _mainWindowIsFullscreen()
        {
            return _isBorderlessFullscreen;
        }

        private void UpdateSettingsButtonStyles(bool isFullscreen)
        {
            if (SettingsWindowModeText == null) return;
            SettingsWindowModeText.Text = isFullscreen ? Loc.Get("BorderlessFullscreen") : Loc.Get("Windowed");

            // Dot 1 = Windowed, Dot 2 = Borderless Fullscreen
            SettingsDot1.Background = !isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
            SettingsDot2.Background = isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
        }

        private void SettingsWindowModeLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyWindowMode(false);
        }

        private void SettingsWindowModeRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyWindowMode(true);
        }

        private void SettingsWindowedBtn_Click(object sender, RoutedEventArgs e) { }
        private void SettingsBorderlessBtn_Click(object sender, RoutedEventArgs e) { }

        private void ApplyWindowMode(bool borderless, bool saveSetting = true)
        {
            _isWindowedMaximized = false;
            _isBorderlessFullscreen = borderless;

            var w = Window.GetWindow(this);
            if (w == null) return;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(w).Handle);

            if (saveSetting)
            {
                AppPrefs.WindowMode = borderless ? SavedWindowMode.BorderlessFullscreen : SavedWindowMode.Windowed;
                AppPrefs.Language = Loc.Current;
                AppPrefs.Save();
            }

            w.WindowState = WindowState.Normal;
            w.WindowStyle = WindowStyle.None;

            System.Windows.Shell.WindowChrome.SetWindowChrome(w, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(8),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

            if (borderless)
            {
                w.ResizeMode = ResizeMode.NoResize;
                CustomTitleBar.Visibility = Visibility.Collapsed;
                CustomTitleBarRow.Height = new GridLength(0);

                w.Left = screen.Bounds.Left;
                w.Top = screen.Bounds.Top;
                w.Width = screen.Bounds.Width;
                w.Height = screen.Bounds.Height;
            }
            else
            {
                w.ResizeMode = ResizeMode.CanResize;
                CustomTitleBar.Visibility = Visibility.Visible;
                CustomTitleBarRow.Height = new GridLength(40);

                w.Width = Math.Min(1280, screen.WorkingArea.Width);
                w.Height = Math.Min(720, screen.WorkingArea.Height);
                w.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - w.Width) / 2;
                w.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - w.Height) / 2;
            }

            UpdateSettingsButtonStyles(borderless);
        }

        private void Page_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (!(Keyboard.FocusedElement is TextBox focusedBox))
                return;

            if (e.OriginalSource is DependencyObject source)
            {
                if (IsInsideNameEditButton(source))
                    return;

                var current = source;
                while (current != null)
                {
                    if (current is TextBox)
                        return;

                    current = VisualTreeHelper.GetParent(current);
                }
            }

            TryLockFocusedNameBox(focusedBox);
            Keyboard.ClearFocus();
        }

        private bool IsInsideNameEditButton(DependencyObject source)
        {
            var current = source;

            while (current != null)
            {
                if (ReferenceEquals(current, P1NameEditButton) ||
                    ReferenceEquals(current, P2NameEditButton) ||
                    ReferenceEquals(current, P3NameEditButton) ||
                    ReferenceEquals(current, P4NameEditButton))
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void TryLockFocusedNameBox(TextBox focusedBox)
        {
            if (ReferenceEquals(focusedBox, P1NameBox) && !P1NameBox.IsReadOnly) { LockNameBox(1); return; }
            if (ReferenceEquals(focusedBox, P2NameBox) && !P2NameBox.IsReadOnly) { LockNameBox(2); return; }
            if (ReferenceEquals(focusedBox, P3NameBox) && !P3NameBox.IsReadOnly) { LockNameBox(3); return; }
            if (ReferenceEquals(focusedBox, P4NameBox) && !P4NameBox.IsReadOnly) { LockNameBox(4); return; }
        }

        private void Page_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) != ModifierKeys.Control) return;

            double current = GetCurrentZoom();
            double next = ClampZoom(current + (e.Delta > 0 ? ZoomStep : -ZoomStep));
            Point mouse = e.GetPosition(AppScroll);
            double contentY = (AppScroll.VerticalOffset + mouse.Y) / current;

            ApplyZoom(next, true, true);
            Dispatcher.BeginInvoke(new Action(() =>
            {
                AppScroll.ScrollToVerticalOffset(Math.Max(0, contentY * next - mouse.Y));
                UpdateZoomIndicatorPlacement();
            }), DispatcherPriority.Loaded);

            e.Handled = true;
        }        // ─────────────────────────────────────────────────────────────────
        //  Save / Load / Delete / New Game
        // ─────────────────────────────────────────────────────────────────
        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            var prompt = new SaveNamePromptWindow { Owner = Window.GetWindow(this) };
            if (prompt.ShowDialog() != true) return;
            string name = prompt.SaveName;
            string path = IOPath.Combine(SaveFolder, name + ".json");
            File.WriteAllText(path,
                JsonConvert.SerializeObject(BuildSaveData(name), Formatting.Indented));
            _currentSaveName = name;
            RefreshSavesDropdown();
            SavesDropdown.SelectedItem = name;
            ShowNotice(Loc.Get("SavedAs", name), NoticeType.Success);
        }
        private void WindowMinimize_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w != null) w.WindowState = WindowState.Minimized;
        }

        private void WindowMaximize_Click(object sender, RoutedEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null)
                return;

            if (CustomTitleBar.Visibility != Visibility.Visible)
                return;

            if (_isWindowedMaximized)
            {
                _isWindowedMaximized = false;
                w.WindowState = WindowState.Normal;
                w.Width = 1280;
                w.Height = 720;

                var screen = System.Windows.Forms.Screen.FromHandle(
                    new System.Windows.Interop.WindowInteropHelper(w).Handle);

                w.Left = screen.WorkingArea.Left + (screen.WorkingArea.Width - w.Width) / 2;
                w.Top = screen.WorkingArea.Top + (screen.WorkingArea.Height - w.Height) / 2;
            }
            else
            {
                var screen = System.Windows.Forms.Screen.FromHandle(
                    new System.Windows.Interop.WindowInteropHelper(w).Handle);

                w.WindowState = WindowState.Normal;
                w.Left = screen.WorkingArea.Left;
                w.Top = screen.WorkingArea.Top;
                w.Width = screen.WorkingArea.Width;
                w.Height = screen.WorkingArea.Height;
                _isWindowedMaximized = true;
            }

            w.WindowStyle = WindowStyle.None;
            w.ResizeMode = ResizeMode.CanResize;
            System.Windows.Shell.WindowChrome.SetWindowChrome(w, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(8),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });
            CustomTitleBar.Visibility = Visibility.Visible;
            CustomTitleBarRow.Height = new GridLength(40);

            UpdateSettingsButtonStyles(false);
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            var confirm = new ThemedConfirmDialog(
                Loc.Get("CloseGameConfirmTitle"),
                Loc.Get("CloseGameConfirmMsg"))
            {
                Owner = Window.GetWindow(this)
            };

            if (confirm.ShowDialog() != true)
                return;

            Window.GetWindow(this)?.Close();
        }

        private void CustomTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null)
                return;

            if (CustomTitleBar.Visibility != Visibility.Visible)
                return;

            if (e.ChangedButton != MouseButton.Left)
                return;

            if (e.ClickCount == 2)
            {
                WindowMaximize_Click(sender, e);
                return;
            }

            _isTitleBarDragging = true;
            _titleBarDragMouseStart = w.PointToScreen(e.GetPosition(w));
            _titleBarDragWindowStart = new Point(w.Left, w.Top);
            CustomTitleBar.CaptureMouse();
            e.Handled = true;
        }

        private void CustomTitleBar_MouseMove(object sender, MouseEventArgs e)
        {
            var w = Window.GetWindow(this);
            if (w == null)
                return;

            if (!_isTitleBarDragging || e.LeftButton != MouseButtonState.Pressed)
                return;

            Point currentMouse = w.PointToScreen(e.GetPosition(w));

            w.Left = _titleBarDragWindowStart.X + (currentMouse.X - _titleBarDragMouseStart.X);
            w.Top = _titleBarDragWindowStart.Y + (currentMouse.Y - _titleBarDragMouseStart.Y);

            e.Handled = true;
        }

        private void CustomTitleBar_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            _isTitleBarDragging = false;
            CustomTitleBar.ReleaseMouseCapture();
            e.Handled = true;
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            string selected = SavesDropdown.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            { ShowNotice(Loc.Get("SelectSaveToLoad"), NoticeType.Warning); return; }
            string path = IOPath.Combine(SaveFolder, selected + ".json");
            if (!File.Exists(path))
            { ShowNotice(Loc.Get("SaveFileNotFound"), NoticeType.Warning); return; }
            var data = JsonConvert.DeserializeObject<TwoV2SaveData>(File.ReadAllText(path));
            RestoreFromSaveData(data);
            _currentSaveName = selected;
            LogAction($"💾 {Loc.Get("LogLoaded", selected)}");
            ShowNotice(Loc.Get("LoadedSave", selected), NoticeType.Success);
            RefreshAllUI();
        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            string selected = SavesDropdown.SelectedItem as string;
            if (string.IsNullOrEmpty(selected))
            { ShowNotice(Loc.Get("SelectSaveToDelete"), NoticeType.Warning); return; }
            var confirmDel = new ThemedConfirmDialog(
                Loc.Get("DeleteConfirmTitle"),
                string.Format(Loc.Get("DeleteConfirmMsg"), selected))
            { Owner = Window.GetWindow(this) };
            if (confirmDel.ShowDialog() != true) return;
            string path = IOPath.Combine(SaveFolder, selected + ".json");
            if (File.Exists(path)) File.Delete(path);
            if (_currentSaveName == selected) _currentSaveName = null;
            RefreshSavesDropdown();
            ShowNotice(Loc.Get("DeletedSave", selected), NoticeType.Info);
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            var confirmNew = new ThemedConfirmDialog(
                Loc.Get("NewGameConfirmTitle"),
                Loc.Get("NewGameConfirmMsg"))
            { Owner = Window.GetWindow(this) };
            if (confirmNew.ShowDialog() != true) return;

            StartNewGamePreservingModes();
        }

        private void StartNewGamePreservingModes()
        {
            bool wasFaction = _factionModeEnabled;
            bool wasFT10 = _ft10ModeEnabled;
            bool wasFT30 = _ft30ModeEnabled;

            InitNewGame();

            // Restore mode states so new game stays in the same mode
            _factionModeEnabled = wasFaction;
            _ft10ModeEnabled = wasFT10;
            _ft30ModeEnabled = wasFT30;
            NormalizeMatchModeFlags();
            ResetAllPlayerPanelsForModeSwap();
            if (!_factionModeEnabled)
            {
                _p1Factions.Clear(); _p2Factions.Clear();
                _p3Factions.Clear(); _p4Factions.Clear();
            }

            if (IsTimedMilestoneMode())
            {
                _milestoneRewardsRemaining = new List<string>();
                _milestoneNextThreshold = 5;
                _milestoneSystemActive = false;
                BuildFT20RewardPool();
                if (_factionModeEnabled) AssignRandomFactions();
            }
            else
            {
                if (_factionModeEnabled) AssignRandomFactions();
                BuildSharedMilestonePool();
            }

            P1NameBox.Text = Loc.Get("DefaultP1Name");
            P2NameBox.Text = Loc.Get("DefaultP2Name");
            P3NameBox.Text = Loc.Get("DefaultP3Name");
            P4NameBox.Text = Loc.Get("DefaultP4Name");

            ResetNameEditButtonsForNewGame();

            RefreshAllUI();
            ShowNotice(Loc.Get("NewGameStarted"), NoticeType.Info);
        }

        // ─────────────────────────────────────────────────────────────────
        //  Name box enter handlers
        // ─────────────────────────────────────────────────────────────────
        private void P1NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LockNameBox(1);
                e.Handled = true;
            }
        }

        private void P2NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LockNameBox(2);
                e.Handled = true;
            }
        }

        private void P3NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LockNameBox(3);
                e.Handled = true;
            }
        }

        private void P4NameBox_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.Key == Key.Enter)
            {
                LockNameBox(4);
                e.Handled = true;
            }
        }

        private void LockNameBox(int player)
        {
            var box = GetNameBox(player);
            var display = GetNameDisplayText(player);
            var button = GetNameEditButton(player);

            box.Text = string.IsNullOrWhiteSpace(box.Text)
                ? Loc.Get($"DefaultP{player}Name")
                : box.Text.Trim();

            display.Text = box.Text;

            box.IsReadOnly = true;
            box.TextAlignment = TextAlignment.Center;
            button.Content = Loc.Get("Unset");

            Keyboard.ClearFocus();
            UpdateAllGoldDisplays();

            _namesLocked = AreAnyNamesLocked();
        }

        // ─────────────────────────────────────────────────────────────────
        //  Placeholder text handlers
        // ─────────────────────────────────────────────────────────────────
        private void PlaceholderBox_GotFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
                ClearInputPlaceholder(box);
        }

        private void PlaceholderBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
                RefreshInputPlaceholder(box);
        }
        private void SavesDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void RefreshSavesDropdown()
        {
            SavesDropdown.Items.Clear();
            if (!Directory.Exists(SaveFolder)) return;
            foreach (var file in Directory.GetFiles(SaveFolder, "*.json")
                                          .OrderByDescending(File.GetLastWriteTime))
                SavesDropdown.Items.Add(IOPath.GetFileNameWithoutExtension(file));
            if (_currentSaveName != null && SavesDropdown.Items.Contains(_currentSaveName))
                SavesDropdown.SelectedItem = _currentSaveName;
        }

        // ─────────────────────────────────────────────────────────────────
        //  Build / Restore save data
        // ─────────────────────────────────────────────────────────────────

        public static class Loc
        {
            public enum Language { English, Spanish, Russian, Chinese }
            public static Language Current = Language.English;

            private static readonly string _langFilePath =
                System.IO.Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TABSEconomyTracker", "language.txt");

            public static void SaveLanguage()
            {
                try
                {
                    string dir = System.IO.Path.GetDirectoryName(_langFilePath);
                    System.IO.Directory.CreateDirectory(dir);
                    System.IO.File.WriteAllText(_langFilePath, Current.ToString());

                    string oneVOneLanguageFilePath = System.IO.Path.Combine(
                        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                        "TABS",
                        "language.txt");

                    System.IO.Directory.CreateDirectory(System.IO.Path.GetDirectoryName(oneVOneLanguageFilePath));
                    System.IO.File.WriteAllText(oneVOneLanguageFilePath, Current.ToString());
                }
                catch { }
            }

            public static void LoadLanguage()
            {
                try
                {
                    if (!System.IO.File.Exists(_langFilePath)) return;
                    string lang = System.IO.File.ReadAllText(_langFilePath).Trim();
                    Language loadedLanguage;
                    if (Enum.TryParse(lang, out loadedLanguage))
                        Current = loadedLanguage;
                }
                catch { }
            }

            public static Language PreviousLanguage(Language language)
            {
                if (language == Language.English) return Language.Chinese;
                if (language == Language.Spanish) return Language.English;
                if (language == Language.Russian) return Language.Spanish;
                return Language.Russian;
            }

            public static Language NextLanguage(Language language)
            {
                if (language == Language.English) return Language.Spanish;
                if (language == Language.Spanish) return Language.Russian;
                if (language == Language.Russian) return Language.Chinese;
                return Language.English;
            }

            public static string GetLanguageDisplayName(Language language)
            {
                if (language == Language.Spanish) return "Español";
                if (language == Language.Russian) return "Русский";
                if (language == Language.Chinese) return "中文";
                return "English";
            }

            public static void UpdateLanguageFlag(System.Windows.Controls.Grid flag, Language language)
            {
                if (flag == null) return;

                flag.Children.Clear();

                double width = double.IsNaN(flag.Width) || flag.Width <= 0 ? 24 : flag.Width;
                double height = double.IsNaN(flag.Height) || flag.Height <= 0 ? 16 : flag.Height;

                if (language == Language.Spanish)
                {
                    AddFlagRect(flag, "#AA151B", 0, 0, width, height * 0.25);
                    AddFlagRect(flag, "#F1BF00", 0, height * 0.25, width, height * 0.5);
                    AddFlagRect(flag, "#AA151B", 0, height * 0.75, width, height * 0.25);
                }
                else if (language == Language.Russian)
                {
                    AddFlagRect(flag, "#FFFFFF", 0, 0, width, height / 3);
                    AddFlagRect(flag, "#0039A6", 0, height / 3, width, height / 3);
                    AddFlagRect(flag, "#D52B1E", 0, height * 2 / 3, width, height / 3);
                }
                else if (language == Language.Chinese)
                {
                    AddFlagRect(flag, "#DE2910", 0, 0, width, height);
                    AddFlagText(flag, "★", "#FFDE00", width * 0.12, height * 0.02, height * 0.55);
                }
                else
                {
                    double stripe = height / 13.0;
                    for (int i = 0; i < 13; i++)
                        AddFlagRect(flag, i % 2 == 0 ? "#B22234" : "#FFFFFF", 0, stripe * i, width, stripe + 0.2);

                    AddFlagRect(flag, "#3C3B6E", 0, 0, width * 0.45, stripe * 7);
                    AddFlagText(flag, "★", "#FFFFFF", width * 0.08, height * 0.03, height * 0.28);
                    AddFlagText(flag, "★", "#FFFFFF", width * 0.25, height * 0.23, height * 0.28);
                }

                AddFlagOutline(flag, width, height);
            }

            private static void AddFlagRect(System.Windows.Controls.Grid flag, string color, double left, double top, double width, double height)
            {
                var rect = new System.Windows.Shapes.Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(left, top, 0, 0)
                };

                flag.Children.Add(rect);
            }

            private static void AddFlagText(System.Windows.Controls.Grid flag, string text, string color, double left, double top, double fontSize)
            {
                var glyph = new TextBlock
                {
                    Text = text,
                    FontSize = fontSize,
                    FontWeight = FontWeights.Bold,
                    Foreground = new System.Windows.Media.SolidColorBrush(
                        (System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(color)),
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top,
                    Margin = new Thickness(left, top, 0, 0),
                    LineHeight = fontSize,
                    TextAlignment = TextAlignment.Center
                };

                flag.Children.Add(glyph);
            }

            private static void AddFlagOutline(System.Windows.Controls.Grid flag, double width, double height)
            {
                flag.Children.Add(new System.Windows.Shapes.Rectangle
                {
                    Width = width,
                    Height = height,
                    Fill = Brushes.Transparent,
                    Stroke = new SolidColorBrush(Color.FromRgb(18, 27, 38)),
                    StrokeThickness = 0.75,
                    HorizontalAlignment = HorizontalAlignment.Left,
                    VerticalAlignment = VerticalAlignment.Top
                });
            }

            public static bool IsTranslatedText(string key, string text)
            {
                if (string.IsNullOrWhiteSpace(text)) return false;

                string value = text.Trim();
                string def;
                string es;
                string ru;
                string zh;

                return (_defaults.TryGetValue(key, out def) && def == value) ||
                       (_es.TryGetValue(key, out es) && es == value) ||
                       (_ru.TryGetValue(key, out ru) && ru == value) ||
                       (_zh.TryGetValue(key, out zh) && zh == value);
            }

            private static readonly Dictionary<string, string> _es = new Dictionary<string, string>
            {
                // Top bar
                ["MainMenu"] = "← Menú Principal",
                ["AppTitle"] = "TABS Arena v1.1.5",

                // Overview panel
                ["OverviewTitle"] = "Resumen 2v2",
                ["OverviewSub"] = "Gestiona los cuatro jugadores y presiona Siguiente Ronda.",
                ["CurrentRound"] = "RONDA ACTUAL",
                ["NextTurnOrder"] = "PRÓXIMO TURNO",
                ["PendingResult"] = "RESULTADO PENDIENTE",
                ["NotAvailableYet"] = "No disponible aún",
                ["NotSet"] = "No establecido",
                ["DefaultP1Name"] = "Jugador Rojo 1",
                ["DefaultP2Name"] = "Jugador Rojo 2",
                ["DefaultP3Name"] = "Jugador Azul 1",
                ["DefaultP4Name"] = "Jugador Azul 2",
                ["MilestoneReward"] = "Recompensa de hito",
                ["FactionMode"] = "MODO FACCIÓN",
                ["FactionModeOff"] = "Modo Facción: OFF",
                ["FactionModeOn"] = "Modo Facción: ON",
                ["FT20Mode"] = "MODO FT20",
                ["FT20ModeOff"] = "Modo FT20: OFF",
                ["FT20ModeOn"] = "Modo FT20: ON",
                ["FT30Mode"] = "MODO FT30",
                ["FT30ModeOff"] = "Modo FT30: OFF",
                ["FT30ModeOn"] = "Modo FT30: ON",
                ["FT10Mode"] = "MODO FT10",
                ["FT10ModeOff"] = "Modo FT10: OFF",
                ["FT10ModeOn"] = "Modo FT10: ON",
                ["WhichTeamFirst"] = "¿Qué equipo va primero esta partida?",
                ["RedTeamFirst"] = "Equipo Rojo Va Primero",
                ["BlueTeamFirst"] = "Equipo Azul Va Primero",

                // Saves
                ["MatchSaves"] = "GUARDADOS",
                ["Save"] = "💾 Guardar",
                ["Load"] = "📂 Cargar",
                ["Delete"] = "🗑 Borrar",
                ["NewGame"] = "🆕 Nueva Partida",

                // Milestone
                ["MilestoneProgress"] = "PROGRESO DE HITO",
                ["NextReward"] = "PRÓXIMA RECOMPENSA",
                ["RewardsLeft"] = "RECOMPENSAS RESTANTES",

                // Action log
                ["ActionLog"] = "Registro de Acciones",
                ["ActionLogSub"] = "Los clics y resultados aparecen aquí.",

                // Round control
                ["RoundControl"] = "Control de Ronda",
                ["RedTeamWins"] = "Gana Equipo Rojo",
                ["Tie"] = "Empate",
                ["StartTieTimer"] = "Iniciar temporizador",
                ["StopTimer"] = "Detener",
                ["ResumeTimer"] = "Reanudar",
                ["RestartTimer"] = "Reiniciar",
                ["MaxFactions"] = "Máx. facciones",
                ["BlueTeamWins"] = "Gana Equipo Azul",
                ["NextRound"] = "Siguiente Ronda",
                ["Undo"] = "Deshacer",

                // Team labels
                ["RedTeam"] = "EQUIPO ROJO",
                ["BlueTeam"] = "EQUIPO AZUL",

                // Stat tiles
                ["Gold"] = "ORO",
                ["Points"] = "PUNTOS",
                ["PermMv"] = "MV PERM",
                ["Income"] = "INGRESO",
                ["InterestStat"] = "INTERÉS",

                // Upgrade buttons
                ["BuyIncome"] = "Comprar ingreso +10 (100)",
                ["BuyIncomeF"] = "Comprar ingreso +13 (130)",
                ["BuyPermMove"] = "Comprar mv perm +1 (200)",
                ["BuyPermMoveF"] = "Comprar mv perm +1 (175)",
                ["BuyFaction"] = "Comprar facción (50)",
                ["BuyChosenFaction"] = "Comprar facción elegida ({0}g)",
                ["ChosenFactionLabel"] = "facción elegida",
                ["ChooseFactionTitle"] = "Elegir facción",
                ["ChooseFactionSub"] = "{0}, elige una facción para comprar.",
                ["LogBoughtChosenFaction"] = "J{0} compró facción elegida '{1}' por {2}g.",
                ["NoticeBoughtChosenFaction"] = "J{0} compró '{1}' por {2}g.",
                ["Upgrades"] = "Mejoras",

                // Faction area
                ["FactionsOwned"] = "FACCIONES COMPRADAS",

                // Utility
                ["Utility"] = "Utilidad",
                ["SingleTroopMove"] = "Mover tropa individual ({0})",
                ["Replay"] = "Repetición (10)",
                ["CustomSpend"] = "Gasto personalizado de tropas",
                ["Spend"] = "Gastar",
                ["TeammateUnit"] = "Costo de unidad compañero",
                ["BFT"] = "BFT",
                ["UnitValue"] = "Valor de unidad",
                ["Sell"] = "Vender",
                ["Set"] = "Listo",
                ["Unset"] = "Editar",

                // Calc
                ["Calculations"] = "Cálculos",
                ["NoRoundYet"] = "Sin ronda aún.",

                // Settings
                ["Settings"] = "Ajustes",
                ["Guide"] = "Guía 2v2",
                ["GuideTitle"] = "Guía 2v2",
                ["ReplayUsed"] = "Replay usado",
                ["GuideBasicsTitle"] = "Conceptos básicos",
                ["GuideBasicsBody"] = "Cada jugador empieza con 1200 de oro. Al inicio de la partida, elige qué equipo va primero. Ese equipo recibe +40 de oro por jugador para compensar que puede ser contraelegido en la primera ronda.",
                ["GuideTurnOrderTitle"] = "Orden de turno",
                ["GuideTurnOrderBody"] = "En la ronda 1, el equipo elegido va primero. Después, el equipo con más puntos va primero. Si los puntos están empatados, va primero el equipo que ganó la ronda más reciente.",
                ["GuideRoundTitle"] = "Rondas, empates y replay",
                ["GuideRoundBody"] = "Cuando termine una batalla, elige el ganador y presiona Siguiente Ronda. Si ambos equipos están de acuerdo en que fue empate, usa Empate. Si no hay acuerdo, usa un temporizador de 3 minutos y fuerza empate si nadie gana. Replay cuesta 10 de oro y solo puede comprarse una vez por ronda por equipo. Replay es solo para propósitos informativos y no cambia el resultado ni el ganador de la ronda.",
                ["GuideEconomyTitle"] = "Economía",
                ["GuideEconomyBody"] = "El interés da +10 de oro por cada 50 de oro que tenga un jugador, con máximo de +100. Comprar ingreso aumenta el ingreso permanente: +10 en FT30 y +13 en FT20. FT10 elimina compras de ingreso y decaimiento de ingreso.",
                ["GuideRulesTitle"] = "Reglas 2v2",
                ["GuideRulesBody"] = "No se permite controlar unidades durante la batalla. En mapas 2v2, no coloques unidades en highground, en el círculo central, en grietas, ni en sus entradas. Deben ser 2 ejércitos por lado, 1 ejército por jugador, 4 ejércitos total. Unidades prohibidas actualmente: Present Elf y Dragon.",
                ["GuideSavingTitle"] = "Guardado",
                ["GuideSavingBody"] = "Si no pueden terminar la partida, guarda en la app. También guarda la batalla dentro de TABS usando Save Battle y activa Save Friendly Units.",
                ["Back"] = "← Volver",
                ["WindowMode"] = "Modo de Ventana",
                ["Windowed"] = "Ventana",
                ["BorderlessFullscreen"] = "Pantalla Completa Sin Bordes",
                ["Language"] = "Idioma",
                ["Sounds"] = "Sonidos",
                ["Volume"] = "Volumen",
                ["On"] = "Activado",
                ["Off"] = "Desactivado",
                ["RedTeamPoints"] = "🔴  PUNTOS EQUIPO ROJO: ",
                ["BlueTeamPoints"] = "🔵  PUNTOS EQUIPO AZUL: ",
                ["StartingGold"] = "Oro inicial",
                ["Interest"] = "Interés",
                ["RoundReward"] = "Recompensa de ronda",
                ["PermanentIncome"] = "Ingreso permanente",
                ["FinalGold"] = "Oro final",
                ["CustomTroopSpend"] = "Gasto personalizado de tropas",
                ["TeammateUnitCost"] = "Costo de unidad compañero",
                ["PtsAway"] = "pts restantes",
                ["NextAt"] = "siguiente en",
                ["AllRewardsClaimed"] = "¡Todas las recompensas reclamadas!",
                ["SaveDialogTitle"] = "Guardar Partida",
                ["EnterSaveName"] = "Ingresa nombre del guardado:",
                ["SaveBtn"] = "Guardar",
                ["Cancel"] = "Cancelar",
                ["Yes"] = "Sí",
                ["No"] = "No",
                ["NewGameConfirmTitle"] = "Nueva Partida",
                ["NewGameConfirmMsg"] = "¿Iniciar nueva partida? El progreso no guardado se perderá.",
                ["NewGameStarted"] = "¡Nueva partida iniciada!",
                ["MatchEndTitle"] = "Partida Terminada",
                ["MatchEndMessage"] = "{0} ganó la partida al llegar a {1} puntos.",
                ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} gana por la regla de ganar por 2.",
                ["MatchEndQuestion"] = "¿Iniciar una nueva partida o seguir jugando?",
                ["NewGamePlain"] = "Nueva Partida",
                ["ContinuePlaying"] = "Continuar",
                ["MainMenuConfirmTitle"] = "Menú Principal",
                ["MainMenuConfirmMsg"] = "¿Seguro que quieres volver al menú principal?",
                ["CloseGameConfirmTitle"] = "Cerrar Juego",
                ["CloseGameConfirmMsg"] = "¿Seguro que quieres cerrar el juego?",
                ["DeleteConfirmTitle"] = "Borrar Guardado",
                ["DeleteConfirmMsg"] = "¿Borrar \"{0}\"?",
                ["TurnOrderRed"] = "Turno: Rojo -> Azul",
                ["TurnOrderBlue"] = "Turno: Azul -> Rojo",
                ["Pending"] = "Pendiente",
                ["CloseGameConfirmTitle"] = "Cerrar Juego",
                ["CloseGameConfirmMsg"] = "¿Seguro que quieres cerrar el juego?",

                // Log keys
                ["LogRedGoesFirst"] = "Equipo Rojo va primero. +40g cada uno.",
                ["LogBlueGoesFirst"] = "Equipo Azul va primero. +40g cada uno.",
                ["LogRedWins"] = "Gana Rojo",
                ["LogBlueWins"] = "Gana Azul",
                ["LogRoundComplete"] = "✅ Ronda {0} completa. {1}. 🔴 {2} – {3} 🔵",
                ["LogRoundReward"] = "Recompensas: Rojo +{0}g cada uno, Azul +{1}g cada uno.",
                ["LogRoundRewardTie"] = "Recompensas: Empate — todos +{0}g cada uno.",
                ["LogBoughtIncome"] = "J{0} compró ingreso +{1} por {2}g ({3}% desc.) → +{4} total.",
                ["LogBoughtPermMove"] = "J{0} compró mov. perm. por {1}g{2}. Total: {3}.",
                ["LogBoughtFaction"] = "J{0} compró '{1}' por {2}g{3}. Siguiente: {4}g.",
                ["LogSpentOn"] = "J{0} gastó {1}g en {2}.",
                ["LogSpent"] = "J{0} gastó {1}g.",
                ["LogBFT"] = "J{0} BFT unidad ({1}g) → pagó {2}g (+{3}% recargo).",
                ["LogSoldUnit"] = "J{0} vendió unidad ({1}g) → {2}g ({3}%).",
                ["LogFactionModeOn"] = "Modo Facción ON — paneles reiniciados, oro inicial del modo, 3 facciones aleatorias.",
                ["LogFactionModeOff"] = "Modo Facción OFF.",
                ["LogFT20ModeOn"] = "Modo FT20 ON — paneles reiniciados.",
                ["LogFT20ModeOff"] = "Modo FT20 OFF — paneles reiniciados.",
                ["LogFT30ModeOn"] = "Modo FT30 ON — paneles reiniciados.",
                ["LogFT30ModeOff"] = "Modo FT30 OFF — modo FT20 seleccionado.",
                ["LogFT10ModeOn"] = "Modo FT10 ON — 1200g inicial e ingreso desactivado.",
                ["LogFT10ModeOff"] = "Modo FT10 OFF — modo FT20 seleccionado.",
                ["LogMilestone"] = "Hito {0}pts — {1}: {2}",
                ["LogFT20Milestone"] = "Hito FT20 {0}pts — {1}: {2}",
                ["LogLoaded"] = "Cargado {0}.",
                ["SingleTroopMoveLabel"] = "mover tropa",
                ["ReplayLabel"] = "repetición",
                ["GuideMoreTitle"] = "Más reglas",
                ["GuideMoreBody"] = "Para aprender más sobre las reglas, visita",

                // Notice keys
                ["NoticeRoundTie"] = "🤝 ¡La ronda {0} terminó en empate! Todos los jugadores ganan +{1} monedas.",
                ["NoticeRoundWin"] = "{0} {1} gana la ronda {2}! Ganadores +{3} monedas, Perdedores +{4} monedas por jugador.",
                ["NoticeBoughtIncome"] = "Ingreso del J{0} ahora +{1}. Pagó {2} monedas.",
                ["NoticeBoughtPermMove"] = "J{0} ahora tiene {1} movimiento(s) permanente(s).",
                ["NoticeBoughtFaction"] = "J{0} recibió '{1}'! Próxima facción: {2} monedas.",
                ["NoticeSoldUnit"] = "J{0} vendió una unidad por +{1} monedas ({2}% de reembolso).",
                ["NoticeSpentOn"] = "J{0} gastó {1} monedas en {2}.",
                ["NoticeSpent"] = "J{0} gastó {1} monedas.",
                ["NoticeBFT"] = "J{0} compró para su compañero: {1} monedas (+{2}% recargo).",
                ["NoticeFactionModeOn"] = "¡Modo Facción ACTIVADO! Paneles reiniciados.",
                ["NoticeFactionModeOff"] = "Modo Facción DESACTIVADO.",
                ["NoticeFT20ModeOn"] = "¡Modo FT20 ACTIVADO! Paneles reiniciados.",
                ["NoticeFT20ModeOff"] = "¡Modo FT20 DESACTIVADO! Paneles reiniciados.",
                ["NoticeFT30ModeOn"] = "¡Modo FT30 ACTIVADO! Paneles reiniciados.",
                ["NoticeFT30ModeOff"] = "Modo FT30 DESACTIVADO. Modo FT20 seleccionado.",
                ["NoticeFT10ModeOn"] = "¡Modo FT10 ACTIVADO! 1200g inicial e ingreso desactivado.",
                ["NoticeFT10ModeOff"] = "Modo FT10 DESACTIVADO. Modo FT20 seleccionado.",
                ["Reward80OffFaction"] = "80% Desc. Próxima Facción",
                ["Reward80OffChosenFaction"] = "80% Desc. Próxima Facción Elegida",
                ["Reward80OffPermMove"] = "80% Desc. Próximo Mv Perm",
                ["Reward10OffIncome"] = "10% Desc. Próximo Ingreso",
                ["Reward30NextSell"] = "+30% Próxima Venta",
                ["RewardSellback15"] = "Reventa +15%",
                ["RewardMinus5BFT"] = "-5% Recargo BFT",
                ["DiscountSuffix"] = " ({0}% desc.)",
                ["LogSharedGoldMilestone"] = "Hito FT{0}: Equipo {1} +{2}g cada uno.",
                ["NoticeSharedGoldMilestone"] = "🏆 ¡Hito FT{0}! Equipo {1} +{2}g cada uno.",
                ["LogSharedPermMoveMilestone"] = "Hito FT{0}: Equipo {1} +1 mv perm cada uno.",
                ["NoticeSharedPermMoveMilestone"] = "🏆 ¡Hito FT{0}! Equipo {1}: +1 mv perm cada uno.",
                ["LogSharedSellbackMilestone"] = "Hito FT{0}: Equipo {1} +20% reventa permanente.",
                ["NoticeSharedSellbackMilestone"] = "🏆 ¡Hito FT{0}! Equipo {1}: +20% reventa permanente.",
                ["NothingToUndo"] = "Nada que deshacer.",
                ["RedTeamShort"] = "Rojo",
                ["BlueTeamShort"] = "Azul",
                ["NoticeMilestone"] = "🏆 ¡Hito {0} pts! {1} obtuvo: {2}",
                ["NoticeFT20Milestone"] = "🏆 ¡Hito FT20 {0} pts! {1} obtuvo: {2}",
                ["SetWinnerBeforeAdvancing"] = "Elige un ganador antes de avanzar.",
                ["IncomeAlreadyBought"] = "J{0} ya compró ingreso esta ronda.",
                ["NeedsGold"] = "J{0} necesita {1}g (tiene {2}g).",
                ["NeedsGoldFor"] = "J{0} necesita {1}g para {2} (tiene {3}g).",
                ["MaxedPermMove"] = "J{0} ya alcanzó el máximo de mv perm ({1}/{2}).",
                ["HasAllFactions"] = "J{0} ya tiene todas las facciones.",
                ["RedReplayAlreadyBought"] = "El Equipo Rojo ya compró replay esta ronda.",
                ["BlueReplayAlreadyBought"] = "El Equipo Azul ya compró replay esta ronda.",
                ["EnterPositiveAmount"] = "Ingresa una cantidad positiva válida.",
                ["OnlyHasGold"] = "J{0} solo tiene {1}g (necesita {2}g).",
                ["EnterValidUnitCost"] = "Ingresa un costo de unidad válido.",
                ["EnterValidUnitValue"] = "Ingresa un valor de unidad válido.",
                ["SavedAs"] = "Guardado como \"{0}\".",
                ["SelectSaveToLoad"] = "Selecciona un guardado para cargar.",
                ["SaveFileNotFound"] = "No se encontró el archivo de guardado.",
                ["LoadedSave"] = "Cargado \"{0}\".",
                ["SelectSaveToDelete"] = "Selecciona un guardado para borrar.",
                ["DeletedSave"] = "Borrado \"{0}\".",
            };

            private static readonly Dictionary<string, string> _ru = new Dictionary<string, string>
            {
                ["MainMenu"] = "← Главное меню",
                ["AppTitle"] = "TABS Arena v1.1.5",
                ["OverviewTitle"] = "Обзор матча 2v2",
                ["OverviewSub"] = "Настройте всех четырех игроков, затем нажмите Следующий раунд, чтобы применить проценты, этапы и награды.",
                ["CurrentRound"] = "ТЕКУЩИЙ РАУНД",
                ["NextTurnOrder"] = "СЛЕДУЮЩИЙ ХОД",
                ["PendingResult"] = "ОЖИДАЕМЫЙ РЕЗУЛЬТАТ",
                ["NotAvailableYet"] = "Пока недоступно",
                ["NotSet"] = "Не задано",
                ["FactionMode"] = "РЕЖИМ ФРАКЦИЙ",
                ["FactionModeOff"] = "Фракции: ВЫКЛ",
                ["FactionModeOn"] = "Фракции: ВКЛ",
                ["FT20Mode"] = "РЕЖИМ FT20",
                ["FT20ModeOff"] = "FT20: ВЫКЛ",
                ["FT20ModeOn"] = "FT20: ВКЛ",
                ["FT30Mode"] = "РЕЖИМ FT30",
                ["FT30ModeOff"] = "FT30: ВЫКЛ",
                ["FT30ModeOn"] = "FT30: ВКЛ",
                ["FT10Mode"] = "РЕЖИМ FT10",
                ["FT10ModeOff"] = "FT10: ВЫКЛ",
                ["FT10ModeOn"] = "FT10: ВКЛ",
                ["WhichTeamFirst"] = "Какая команда ходит первой в этом матче?",
                ["RedTeamFirst"] = "Красная команда ходит первой",
                ["BlueTeamFirst"] = "Синяя команда ходит первой",
                ["MatchSaves"] = "СОХРАНЕНИЯ",
                ["MilestoneReward"] = "Награда этапа",
                ["Save"] = "💾 Сохранить",
                ["Load"] = "📂 Загрузить",
                ["Delete"] = "🗑 Удалить",
                ["NewGame"] = "🆕 Новая игра",
                ["MilestoneProgress"] = "ПРОГРЕСС ЭТАПА",
                ["NextReward"] = "СЛЕДУЮЩАЯ НАГРАДА",
                ["RewardsLeft"] = "ВОЗМОЖНЫЕ НАГРАДЫ",
                ["ActionLog"] = "Журнал действий",
                ["ActionLogSub"] = "Покупки и результаты раундов появляются здесь.",
                ["RoundControl"] = "Управление раундом",
                ["RedTeamWins"] = "Победа красных",
                ["Tie"] = "Ничья",
                ["StartTieTimer"] = "Запустить таймер",
                ["StopTimer"] = "Остановить",
                ["ResumeTimer"] = "Продолжить",
                ["RestartTimer"] = "Сбросить",
                ["BlueTeamWins"] = "Победа синих",
                ["NextRound"] = "Следующий раунд",
                ["Undo"] = "Отменить",
                ["RedTeam"] = "КРАСНАЯ КОМАНДА",
                ["BlueTeam"] = "СИНЯЯ КОМАНДА",
                ["Gold"] = "ЗОЛОТО",
                ["Points"] = "ОЧКИ",
                ["PermMv"] = "ПОСТ. ХОД",
                ["Income"] = "ДОХОД",
                ["InterestStat"] = "ПРОЦЕНТЫ",
                ["MaxFactions"] = "Макс. фракций",
                ["BuyIncome"] = "Купить доход +10 (100)",
                ["BuyIncomeF"] = "Купить доход +13 (130)",
                ["BuyPermMove"] = "Купить пост. ход +1 (200)",
                ["BuyPermMoveF"] = "Купить пост. ход +1 (175)",
                ["BuyFaction"] = "Купить фракцию (50)",
                ["BuyChosenFaction"] = "Купить выбранную фракцию ({0}g)",
                ["ChosenFactionLabel"] = "выбранная фракция",
                ["ChooseFactionTitle"] = "Выбрать фракцию",
                ["ChooseFactionSub"] = "{0}, выберите одну фракцию для покупки.",
                ["LogBoughtChosenFaction"] = "Игрок {0} купил выбранную фракцию '{1}' за {2}g.",
                ["NoticeBoughtChosenFaction"] = "Игрок {0} купил '{1}' за {2}g.",
                ["Upgrades"] = "Улучшения",
                ["FactionsOwned"] = "КУПЛЕННЫЕ ФРАКЦИИ",
                ["Utility"] = "Утилиты",
                ["SingleTroopMove"] = "Переместить одного юнита ({0})",
                ["Replay"] = "Повтор (10)",
                ["CustomSpend"] = "Своя трата на войска",
                ["Spend"] = "Потратить",
                ["TeammateUnit"] = "Цена юнита союзника",
                ["BFT"] = "BFT",
                ["UnitValue"] = "Цена юнита",
                ["Sell"] = "Продать",
                ["Set"] = "Готово",
                ["Unset"] = "Изменить",
                ["Calculations"] = "Расчеты",
                ["NoRoundYet"] = "Раунда еще нет.",
                ["Settings"] = "Настройки",
                ["Guide"] = "Руководство 2v2",
                ["GuideTitle"] = "Руководство 2v2",
                ["ReplayUsed"] = "Повтор использован",
                ["GuideBasicsTitle"] = "Основы",
                ["GuideBasicsBody"] = "Каждый игрок начинает с 1200 золота. В начале матча выберите, какая команда ходит первой. Эта команда получает +40 золота на игрока, чтобы компенсировать контрвыбор в раунде 1.",
                ["GuideTurnOrderTitle"] = "Порядок хода",
                ["GuideTurnOrderBody"] = "В раунде 1 первой ходит выбранная команда. После этого первой ходит команда с большим количеством очков. Если очки равны, первой ходит команда, выигравшая последний раунд.",
                ["GuideRoundTitle"] = "Раунды, ничьи и повтор",
                ["GuideRoundBody"] = "Когда битва закончится, выберите победителя и нажмите Следующий раунд. Если обе команды согласны, что была ничья, используйте Ничья. Если согласия нет, используйте таймер на 3 минуты и принудительно ставьте ничью, если никто не победил. Повтор стоит 10 золота и может быть куплен только один раз за раунд каждой командой. Повтор нужен только для информации и не меняет результат или победителя раунда.",
                ["GuideEconomyTitle"] = "Экономика",
                ["GuideEconomyBody"] = "Проценты дают +10 золота за каждые 50 золота у игрока, максимум +100. Покупка дохода повышает постоянный доход: +10 в FT30 и +13 в FT20. FT10 убирает покупки дохода и спад дохода.",
                ["GuideRulesTitle"] = "Правила 2v2",
                ["GuideRulesBody"] = "Игроки не могут управлять юнитами во время битвы. На картах 2v2 не размещайте юнитов на возвышенностях, в центральном круге, в трещинах или у входов в круг и трещины. Должно быть 2 армии на сторону, 1 армия на игрока, всего 4 армии. Сейчас запрещены юниты: Present Elf и Dragon.",
                ["GuideSavingTitle"] = "Сохранение",
                ["GuideSavingBody"] = "Если вы не можете закончить матч, сохраните его в приложении. Также сохраните битву внутри TABS через Save Battle и включите Save Friendly Units.",
                ["Back"] = "← Назад",
                ["WindowMode"] = "Режим окна",
                ["Windowed"] = "Оконный",
                ["BorderlessFullscreen"] = "Полный экран без рамки",
                ["Language"] = "Язык",
                ["Sounds"] = "Звуки",
                ["Volume"] = "Громкость",
                ["On"] = "Вкл",
                ["Off"] = "Выкл",
                ["RedTeamPoints"] = "🔴  ОЧКИ КРАСНОЙ КОМАНДЫ: ",
                ["BlueTeamPoints"] = "🔵  ОЧКИ СИНЕЙ КОМАНДЫ: ",
                ["StartingGold"] = "Начальное золото",
                ["Interest"] = "Проценты",
                ["RoundReward"] = "Награда раунда",
                ["PermanentIncome"] = "Постоянный доход",
                ["FinalGold"] = "Итоговое золото",
                ["CustomTroopSpend"] = "Своя трата на войска",
                ["TeammateUnitCost"] = "Цена юнита союзника",
                ["PtsAway"] = "очк. осталось",
                ["NextAt"] = "следующая на",
                ["AllRewardsClaimed"] = "Все награды получены!",
                ["SaveDialogTitle"] = "Сохранить игру",
                ["EnterSaveName"] = "Введите имя сохранения:",
                ["SaveBtn"] = "Сохранить",
                ["Cancel"] = "Отмена",
                ["Yes"] = "Да",
                ["No"] = "Нет",
                ["NewGameConfirmTitle"] = "Новая игра",
                ["NewGameConfirmMsg"] = "Начать новую игру? Несохраненный прогресс будет потерян.",
                ["NewGameStarted"] = "Новая игра начата.",
                ["MatchEndTitle"] = "Матч завершен",
                ["MatchEndMessage"] = "{0} выиграла матч, набрав {1} очков.",
                ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} побеждает по правилу победы с разницей 2.",
                ["MatchEndQuestion"] = "Начать новую игру или продолжить?",
                ["NewGamePlain"] = "Новая игра",
                ["ContinuePlaying"] = "Продолжить",
                ["MainMenuConfirmTitle"] = "Главное меню",
                ["MainMenuConfirmMsg"] = "Вы уверены, что хотите вернуться в главное меню?",
                ["CloseGameConfirmTitle"] = "Закрыть игру",
                ["CloseGameConfirmMsg"] = "Вы уверены, что хотите закрыть игру?",
                ["DeleteConfirmTitle"] = "Удалить сохранение",
                ["DeleteConfirmMsg"] = "Удалить \"{0}\"?",
                ["TurnOrderRed"] = "Ход: Красные -> Синие",
                ["TurnOrderBlue"] = "Ход: Синие -> Красные",
                ["DefaultP1Name"] = "Красный игрок 1",
                ["DefaultP2Name"] = "Красный игрок 2",
                ["DefaultP3Name"] = "Синий игрок 1",
                ["DefaultP4Name"] = "Синий игрок 2",
                ["LogRedGoesFirst"] = "Красная команда ходит первой. +40g каждому.",
                ["LogBlueGoesFirst"] = "Синяя команда ходит первой. +40g каждому.",
                ["LogRedWins"] = "Победа красных",
                ["LogBlueWins"] = "Победа синих",
                ["LogRoundReward"] = "Награды раунда: красные +{0}g каждому, синие +{1}g каждому.",
                ["LogRoundRewardTie"] = "Награды раунда: ничья - все игроки получают +{0}g.",
                ["LogBoughtIncome"] = "Игрок {0} купил доход +{1} за {2}g (скидка {3}%) -> всего +{4}.",
                ["LogBoughtPermMove"] = "Игрок {0} купил пост. ход за {1}g{2}. Всего: {3}.",
                ["LogBoughtFaction"] = "Игрок {0} купил '{1}' за {2}g{3}. Следующая: {4}g.",
                ["LogSpentOn"] = "Игрок {0} потратил {1}g на {2}.",
                ["LogSpent"] = "Игрок {0} потратил {1}g.",
                ["LogBFT"] = "Игрок {0} BFT юнита ({1}g) -> заплатил {2}g (+{3}% наценка).",
                ["LogSoldUnit"] = "Игрок {0} продал юнита ({1}g) -> {2}g ({3}%).",
                ["LogFactionModeOn"] = "Режим фракций ВКЛ - панели сброшены, стартовое золото режима, 3 случайные фракции.",
                ["LogFactionModeOff"] = "Режим фракций ВЫКЛ.",
                ["LogFT20ModeOn"] = "Режим FT20 ВКЛ - панели сброшены.",
                ["LogFT20ModeOff"] = "Режим FT20 ВЫКЛ - панели сброшены.",
                ["LogFT30ModeOn"] = "Режим FT30 ВКЛ - панели сброшены.",
                ["LogFT30ModeOff"] = "Режим FT30 ВЫКЛ - выбран режим FT20.",
                ["LogFT10ModeOn"] = "Режим FT10 ВКЛ - старт 1200g и доход выключен.",
                ["LogFT10ModeOff"] = "Режим FT10 ВЫКЛ - выбран режим FT20.",
                ["LogMilestone"] = "Этап {0} очк. - {1}: {2}",
                ["LogFT20Milestone"] = "Этап FT20 {0} очк. - {1}: {2}",
                ["LogLoaded"] = "Загружено {0}.",
                ["SingleTroopMoveLabel"] = "перемещение юнита",
                ["ReplayLabel"] = "повтор",
                ["Pending"] = "Ожидает",
                ["LogRoundComplete"] = "✅ Раунд {0} завершен. {1}. 🔴 {2} – {3} 🔵",
                ["NoticeRoundTie"] = "🤝 Раунд {0} завершился ничьей! Все игроки получают +{1}g.",
                ["NoticeRoundWin"] = "{0} {1} выигрывает раунд {2}! Победители +{3}g, проигравшие +{4}g на игрока.",
                ["NoticeBoughtIncome"] = "Доход Игрока {0} теперь +{1}. Заплачено {2}g.",
                ["NoticeBoughtPermMove"] = "У Игрока {0} теперь {1} пост. ход(ов).",
                ["NoticeBoughtFaction"] = "Игрок {0} получил '{1}'! Следующая фракция: {2}g.",
                ["NoticeSoldUnit"] = "Игрок {0} продал юнита за +{1}g ({2}% возврата).",
                ["NoticeSpentOn"] = "Игрок {0} потратил {1}g на {2}.",
                ["NoticeSpent"] = "Игрок {0} потратил {1}g.",
                ["NoticeBFT"] = "Игрок {0} купил для союзника: {1}g (+{2}% наценка).",
                ["NoticeFactionModeOn"] = "Режим фракций ВКЛ! Панели игроков сброшены.",
                ["NoticeFactionModeOff"] = "Режим фракций ВЫКЛ.",
                ["NoticeFT20ModeOn"] = "Режим FT20 ВКЛ! Панели игроков сброшены.",
                ["NoticeFT20ModeOff"] = "Режим FT20 ВЫКЛ! Панели игроков сброшены.",
                ["NoticeFT30ModeOn"] = "Режим FT30 ВКЛ! Панели игроков сброшены.",
                ["NoticeFT30ModeOff"] = "Режим FT30 ВЫКЛ. Выбран режим FT20.",
                ["NoticeFT10ModeOn"] = "Режим FT10 ВКЛ! Старт 1200g и доход выключен.",
                ["NoticeFT10ModeOff"] = "Режим FT10 ВЫКЛ. Выбран режим FT20.",
                ["NothingToUndo"] = "Нечего отменять.",
                ["RedTeamShort"] = "Красные",
                ["BlueTeamShort"] = "Синие",
                ["NoticeMilestone"] = "🏆 Этап {0} очк.! {1} получил: {2}",
                ["NoticeFT20Milestone"] = "🏆 Этап FT20 {0} очк.! {1} получил: {2}",
                ["SetWinnerBeforeAdvancing"] = "Выберите победителя раунда перед продолжением.",
                ["IncomeAlreadyBought"] = "Игрок {0} уже купил доход в этом раунде.",
                ["NeedsGold"] = "Игрок {0} нуждается в {1}g (есть {2}g).",
                ["NeedsGoldFor"] = "Игрок {0} нуждается в {1}g для {2} (есть {3}g).",
                ["MaxedPermMove"] = "Игрок {0} уже достиг максимума пост. хода ({1}/{2}).",
                ["HasAllFactions"] = "Игрок {0} уже имеет все фракции.",
                ["RedReplayAlreadyBought"] = "Красная команда уже купила повтор в этом раунде.",
                ["BlueReplayAlreadyBought"] = "Синяя команда уже купила повтор в этом раунде.",
                ["EnterPositiveAmount"] = "Введите допустимую положительную сумму.",
                ["OnlyHasGold"] = "У Игрока {0} только {1}g (нужно {2}g).",
                ["EnterValidUnitCost"] = "Введите допустимую цену юнита.",
                ["EnterValidUnitValue"] = "Введите допустимую стоимость юнита.",
                ["SavedAs"] = "Сохранено как \"{0}\".",
                ["SelectSaveToLoad"] = "Выберите сохранение для загрузки.",
                ["SaveFileNotFound"] = "Файл сохранения не найден.",
                ["LoadedSave"] = "Загружено \"{0}\".",
                ["SelectSaveToDelete"] = "Выберите сохранение для удаления.",
                ["DeletedSave"] = "Удалено \"{0}\".",
                ["Reward80OffFaction"] = "80% скидка на след. фракцию",
                ["Reward80OffChosenFaction"] = "80% скидка на след. выбранную фракцию",
                ["Reward80OffPermMove"] = "80% скидка на след. пост. ход",
                ["RewardSellback15"] = "Продажа +15%",
                ["Reward10OffIncome"] = "10% скидка на след. доход",
                ["Reward30NextSell"] = "+30% к след. продаже",
                ["RewardMinus5BFT"] = "-5% наценка BFT",
                ["DiscountSuffix"] = " (скидка {0}%)",
                ["LogSharedGoldMilestone"] = "Этап FT{0}: команда {1} получает +{2}g каждому.",
                ["NoticeSharedGoldMilestone"] = "🏆 Этап FT{0}! Команда {1} получает +{2}g каждому.",
                ["LogSharedPermMoveMilestone"] = "Этап FT{0}: команда {1} получает +1 пост. ход каждому.",
                ["NoticeSharedPermMoveMilestone"] = "🏆 Этап FT{0}! Команда {1}: +1 пост. ход каждому.",
                ["LogSharedSellbackMilestone"] = "Этап FT{0}: команда {1} получает +20% постоянной продажи.",
                ["NoticeSharedSellbackMilestone"] = "🏆 Этап FT{0}! Команда {1}: +20% постоянной продажи.",
                ["GuideMoreTitle"] = "Больше правил",
                ["GuideMoreBody"] = "Чтобы узнать больше о правилах, посетите",
            };

            private static readonly Dictionary<string, string> _zh = new Dictionary<string, string>
            {
                ["MainMenu"] = "← 主菜单",
                ["AppTitle"] = "TABS Arena v1.1.5",
                ["OverviewTitle"] = "2v2 比赛总览",
                ["OverviewSub"] = "管理四名玩家，然后点击下一回合以应用利息、里程碑和奖励。",
                ["CurrentRound"] = "当前回合",
                ["NextTurnOrder"] = "下回合顺序",
                ["PendingResult"] = "待定结果",
                ["NotAvailableYet"] = "暂不可用",
                ["NotSet"] = "未设置",
                ["FactionMode"] = "阵营模式",
                ["FactionModeOff"] = "阵营：关",
                ["FactionModeOn"] = "阵营：开",
                ["FT20Mode"] = "FT20 模式",
                ["FT20ModeOff"] = "FT20：关",
                ["FT20ModeOn"] = "FT20：开",
                ["FT30Mode"] = "FT30 模式",
                ["FT30ModeOff"] = "FT30：关",
                ["FT30ModeOn"] = "FT30：开",
                ["FT10Mode"] = "FT10 模式",
                ["FT10ModeOff"] = "FT10：关",
                ["FT10ModeOn"] = "FT10：开",
                ["WhichTeamFirst"] = "本场比赛哪支队伍先行动？",
                ["RedTeamFirst"] = "红队先行动",
                ["BlueTeamFirst"] = "蓝队先行动",
                ["MatchSaves"] = "比赛存档",
                ["MilestoneReward"] = "里程碑奖励",
                ["Save"] = "💾 保存",
                ["Load"] = "📂 读取",
                ["Delete"] = "🗑 删除",
                ["NewGame"] = "🆕 新游戏",
                ["MilestoneProgress"] = "里程碑进度",
                ["NextReward"] = "下一奖励",
                ["RewardsLeft"] = "剩余可得奖励",
                ["ActionLog"] = "行动日志",
                ["ActionLogSub"] = "商店点击和回合结果会显示在这里。",
                ["RoundControl"] = "回合控制",
                ["RedTeamWins"] = "红队获胜",
                ["Tie"] = "平局",
                ["StartTieTimer"] = "开始平局计时器",
                ["StopTimer"] = "停止计时器",
                ["ResumeTimer"] = "继续计时器",
                ["RestartTimer"] = "重置计时器",
                ["BlueTeamWins"] = "蓝队获胜",
                ["NextRound"] = "下一回合",
                ["Undo"] = "撤销",
                ["RedTeam"] = "红队",
                ["BlueTeam"] = "蓝队",
                ["Gold"] = "金币",
                ["Points"] = "分数",
                ["PermMv"] = "永久移动",
                ["Income"] = "收入",
                ["InterestStat"] = "利息",
                ["MaxFactions"] = "阵营上限",
                ["BuyIncome"] = "购买收入 +10 (100)",
                ["BuyIncomeF"] = "购买收入 +13 (130)",
                ["BuyPermMove"] = "购买永久移动 +1 (200)",
                ["BuyPermMoveF"] = "购买永久移动 +1 (175)",
                ["BuyFaction"] = "购买阵营 (50)",
                ["BuyChosenFaction"] = "购买指定阵营 ({0}g)",
                ["ChosenFactionLabel"] = "指定阵营",
                ["ChooseFactionTitle"] = "选择阵营",
                ["ChooseFactionSub"] = "{0}，选择一个要购买的阵营。",
                ["LogBoughtChosenFaction"] = "玩家 {0} 以 {2}g 购买了指定阵营 '{1}'。",
                ["NoticeBoughtChosenFaction"] = "玩家 {0} 以 {2}g 购买了 '{1}'。",
                ["Upgrades"] = "升级",
                ["FactionsOwned"] = "已拥有阵营",
                ["Utility"] = "实用",
                ["SingleTroopMove"] = "单个部队移动 ({0})",
                ["Replay"] = "重赛查看 (10)",
                ["CustomSpend"] = "自定义部队支出",
                ["Spend"] = "支出",
                ["TeammateUnit"] = "队友单位花费",
                ["BFT"] = "BFT",
                ["UnitValue"] = "单位价值",
                ["Sell"] = "出售",
                ["Set"] = "设定",
                ["Unset"] = "编辑",
                ["Calculations"] = "计算",
                ["NoRoundYet"] = "还没有回合。",
                ["Settings"] = "设置",
                ["Guide"] = "2v2 指南",
                ["GuideTitle"] = "2v2 指南",
                ["ReplayUsed"] = "重赛已用",
                ["GuideBasicsTitle"] = "基础",
                ["GuideBasicsBody"] = "每名玩家以 1200 金币开始。比赛开始时选择哪支队伍先行动。该队每名玩家获得 +40 金币，用来补偿第 1 回合被针对选兵的劣势。",
                ["GuideTurnOrderTitle"] = "行动顺序",
                ["GuideTurnOrderBody"] = "第 1 回合由选择的队伍先行动。之后，分数更高的队伍先行动。如果分数相同，则上一回合获胜的队伍先行动。",
                ["GuideRoundTitle"] = "回合、平局和重赛查看",
                ["GuideRoundBody"] = "战斗结束后，选择胜者并点击下一回合。如果双方队伍都同意是平局，使用平局。若无法达成一致，使用 3 分钟计时器；没人获胜则强制平局。重赛查看花费 10 金币，每支队伍每回合只能购买一次。重赛查看仅用于信息参考，不会改变回合结果或胜者。",
                ["GuideEconomyTitle"] = "经济",
                ["GuideEconomyBody"] = "利息按玩家每 50 金币给予 +10 金币，最高 +100。购买收入会提高永久收入：FT30 为 +10，FT20 为 +13。FT10 移除收入购买和收入衰减。",
                ["GuideRulesTitle"] = "2v2 规则",
                ["GuideRulesBody"] = "战斗期间玩家不得控制单位。在 2v2 地图上，不要把单位放在高地、中心圆圈、裂缝中，或圆圈/裂缝入口处。每边应有 2 支军队，每名玩家 1 支军队，总共 4 支军队。目前禁用单位：Present Elf 和 Dragon。",
                ["GuideSavingTitle"] = "保存",
                ["GuideSavingBody"] = "如果无法打完比赛，请在应用中保存。也要在 TABS 内使用 Save Battle 保存战斗，并启用 Save Friendly Units。",
                ["Back"] = "← 返回",
                ["WindowMode"] = "窗口模式",
                ["Windowed"] = "窗口化",
                ["BorderlessFullscreen"] = "无边框全屏",
                ["Language"] = "语言",
                ["Sounds"] = "音效",
                ["Volume"] = "音量",
                ["On"] = "开",
                ["Off"] = "关",
                ["RedTeamPoints"] = "🔴  红队分数：",
                ["BlueTeamPoints"] = "🔵  蓝队分数：",
                ["StartingGold"] = "初始金币",
                ["Interest"] = "利息",
                ["RoundReward"] = "回合奖励",
                ["PermanentIncome"] = "永久收入",
                ["FinalGold"] = "最终金币",
                ["CustomTroopSpend"] = "自定义部队支出",
                ["TeammateUnitCost"] = "队友单位花费",
                ["PtsAway"] = "分后获得",
                ["NextAt"] = "下一次在",
                ["AllRewardsClaimed"] = "所有奖励已领取！",
                ["SaveDialogTitle"] = "保存游戏",
                ["EnterSaveName"] = "输入存档名称：",
                ["SaveBtn"] = "保存",
                ["Cancel"] = "取消",
                ["Yes"] = "是",
                ["No"] = "否",
                ["NewGameConfirmTitle"] = "新游戏",
                ["NewGameConfirmMsg"] = "开始新游戏？未保存进度会丢失。",
                ["NewGameStarted"] = "新游戏已开始。",
                ["MatchEndTitle"] = "比赛完成",
                ["MatchEndMessage"] = "{0} 达到 {1} 分并赢得比赛。",
                ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} 通过领先 2 分规则获胜。",
                ["MatchEndQuestion"] = "开始新游戏还是继续游玩？",
                ["NewGamePlain"] = "新游戏",
                ["ContinuePlaying"] = "继续",
                ["MainMenuConfirmTitle"] = "主菜单",
                ["MainMenuConfirmMsg"] = "确定要返回主菜单吗？",
                ["CloseGameConfirmTitle"] = "关闭游戏",
                ["CloseGameConfirmMsg"] = "确定要关闭游戏吗？",
                ["DeleteConfirmTitle"] = "删除存档",
                ["DeleteConfirmMsg"] = "删除 \"{0}\"？",
                ["TurnOrderRed"] = "顺序：红队 -> 蓝队",
                ["TurnOrderBlue"] = "顺序：蓝队 -> 红队",
                ["DefaultP1Name"] = "红方玩家 1",
                ["DefaultP2Name"] = "红方玩家 2",
                ["DefaultP3Name"] = "蓝方玩家 1",
                ["DefaultP4Name"] = "蓝方玩家 2",
                ["LogRedGoesFirst"] = "红队先行动。每人 +40g。",
                ["LogBlueGoesFirst"] = "蓝队先行动。每人 +40g。",
                ["LogRedWins"] = "红队获胜",
                ["LogBlueWins"] = "蓝队获胜",
                ["LogRoundReward"] = "回合奖励：红队每人 +{0}g，蓝队每人 +{1}g。",
                ["LogRoundRewardTie"] = "回合奖励：平局 - 所有玩家每人 +{0}g。",
                ["LogBoughtIncome"] = "玩家 {0} 以 {2}g 购买收入 +{1}（{3}% 折扣）-> 总计 +{4}。",
                ["LogBoughtPermMove"] = "玩家 {0} 以 {1}g{2} 购买永久移动。总计：{3}。",
                ["LogBoughtFaction"] = "玩家 {0} 以 {2}g{3} 购买了 '{1}'。下一个：{4}g。",
                ["LogSpentOn"] = "玩家 {0} 在 {2} 上花费 {1}g。",
                ["LogSpent"] = "玩家 {0} 花费 {1}g。",
                ["LogBFT"] = "玩家 {0} BFT 单位（{1}g）-> 支付 {2}g（+{3}% 加价）。",
                ["LogSoldUnit"] = "玩家 {0} 出售单位（{1}g）-> {2}g（{3}%）。",
                ["LogFactionModeOn"] = "阵营模式开启 - 面板已重置，使用模式初始金币，并获得 3 个随机阵营。",
                ["LogFactionModeOff"] = "阵营模式关闭。",
                ["LogFT20ModeOn"] = "FT20 模式开启 - 面板已重置。",
                ["LogFT20ModeOff"] = "FT20 模式关闭 - 面板已重置。",
                ["LogFT30ModeOn"] = "FT30 模式开启 - 面板已重置。",
                ["LogFT30ModeOff"] = "FT30 模式关闭 - 已选择 FT20 模式。",
                ["LogFT10ModeOn"] = "FT10 模式开启 - 1200g 开局，收入已禁用。",
                ["LogFT10ModeOff"] = "FT10 模式关闭 - 已选择 FT20 模式。",
                ["LogMilestone"] = "里程碑 {0} 分 - {1}: {2}",
                ["LogFT20Milestone"] = "FT20 里程碑 {0} 分 - {1}: {2}",
                ["LogLoaded"] = "已读取 {0}。",
                ["SingleTroopMoveLabel"] = "部队移动",
                ["ReplayLabel"] = "重赛查看",
                ["Pending"] = "待定",
                ["LogRoundComplete"] = "✅ 第 {0} 回合完成。{1}。🔴 {2} - {3} 🔵",
                ["NoticeRoundTie"] = "🤝 第 {0} 回合平局！所有玩家获得 +{1}g。",
                ["NoticeRoundWin"] = "{0} {1} 赢得第 {2} 回合！胜方每人 +{3}g，败方每人 +{4}g。",
                ["NoticeBoughtIncome"] = "玩家 {0} 的收入现在为 +{1}。支付 {2}g。",
                ["NoticeBoughtPermMove"] = "玩家 {0} 现在有 {1} 个永久移动。",
                ["NoticeBoughtFaction"] = "玩家 {0} 获得了 '{1}'！下一个阵营：{2}g。",
                ["NoticeSoldUnit"] = "玩家 {0} 出售单位获得 +{1}g（{2}% 返还）。",
                ["NoticeSpentOn"] = "玩家 {0} 在 {2} 上花费 {1}g。",
                ["NoticeSpent"] = "玩家 {0} 花费 {1}g。",
                ["NoticeBFT"] = "玩家 {0} 为队友购买：{1}g（+{2}% 加价）。",
                ["NoticeFactionModeOn"] = "阵营模式开启！玩家面板已重置。",
                ["NoticeFactionModeOff"] = "阵营模式关闭。",
                ["NoticeFT20ModeOn"] = "FT20 模式开启！玩家面板已重置。",
                ["NoticeFT20ModeOff"] = "FT20 模式关闭！玩家面板已重置。",
                ["NoticeFT30ModeOn"] = "FT30 模式开启！玩家面板已重置。",
                ["NoticeFT30ModeOff"] = "FT30 模式关闭。已选择 FT20 模式。",
                ["NoticeFT10ModeOn"] = "FT10 模式开启！1200g 开局，收入已禁用。",
                ["NoticeFT10ModeOff"] = "FT10 模式关闭。已选择 FT20 模式。",
                ["NothingToUndo"] = "没有可撤销的内容。",
                ["RedTeamShort"] = "红队",
                ["BlueTeamShort"] = "蓝队",
                ["NoticeMilestone"] = "🏆 {0} 分里程碑！{1} 获得：{2}",
                ["NoticeFT20Milestone"] = "🏆 FT20 {0} 分里程碑！{1} 获得：{2}",
                ["SetWinnerBeforeAdvancing"] = "继续前请设置本回合胜者。",
                ["IncomeAlreadyBought"] = "玩家 {0} 本回合已经购买过收入。",
                ["NeedsGold"] = "玩家 {0} 需要 {1}g（现有 {2}g）。",
                ["NeedsGoldFor"] = "玩家 {0} 需要 {1}g 来购买 {2}（现有 {3}g）。",
                ["MaxedPermMove"] = "玩家 {0} 的永久移动已达到上限（{1}/{2}）。",
                ["HasAllFactions"] = "玩家 {0} 已拥有所有阵营。",
                ["RedReplayAlreadyBought"] = "红队本回合已经购买过重赛查看。",
                ["BlueReplayAlreadyBought"] = "蓝队本回合已经购买过重赛查看。",
                ["EnterPositiveAmount"] = "请输入有效的正数金额。",
                ["OnlyHasGold"] = "玩家 {0} 只有 {1}g（需要 {2}g）。",
                ["EnterValidUnitCost"] = "请输入有效的单位花费。",
                ["EnterValidUnitValue"] = "请输入有效的单位价值。",
                ["SavedAs"] = "已保存为 \"{0}\"。",
                ["SelectSaveToLoad"] = "请选择要读取的存档。",
                ["SaveFileNotFound"] = "找不到存档文件。",
                ["LoadedSave"] = "已读取 \"{0}\"。",
                ["SelectSaveToDelete"] = "请选择要删除的存档。",
                ["DeletedSave"] = "已删除 \"{0}\"。",
                ["Reward80OffFaction"] = "下个阵营 80% 折扣",
                ["Reward80OffChosenFaction"] = "下个指定阵营 80% 折扣",
                ["Reward80OffPermMove"] = "下个永久移动 80% 折扣",
                ["RewardSellback15"] = "出售返还 +15%",
                ["Reward10OffIncome"] = "下次收入 10% 折扣",
                ["Reward30NextSell"] = "下次出售 +30%",
                ["RewardMinus5BFT"] = "BFT 加价 -5%",
                ["DiscountSuffix"] = "（{0}% 折扣）",
                ["LogSharedGoldMilestone"] = "FT{0} 里程碑：{1} 每人 +{2}g。",
                ["NoticeSharedGoldMilestone"] = "🏆 FT{0} 里程碑！{1} 每人 +{2}g。",
                ["LogSharedPermMoveMilestone"] = "FT{0} 里程碑：{1} 每人 +1 永久移动。",
                ["NoticeSharedPermMoveMilestone"] = "🏆 FT{0} 里程碑！{1}：每人 +1 永久移动。",
                ["LogSharedSellbackMilestone"] = "FT{0} 里程碑：{1} 获得 +20% 永久出售返还。",
                ["NoticeSharedSellbackMilestone"] = "🏆 FT{0} 里程碑！{1}：+20% 永久出售返还。",
                ["GuideMoreTitle"] = "更多规则",
                ["GuideMoreBody"] = "想了解更多规则，请访问",
            };

            public static string Get(string key, params object[] args)
            {
                string template;
                if (Current == Language.Chinese && _zh.TryGetValue(key, out var zh)) template = zh;
                else if (Current == Language.Russian && _ru.TryGetValue(key, out var ru)) template = ru;
                else if (Current == Language.Spanish && _es.TryGetValue(key, out var val)) template = val;
                else if (_defaults.TryGetValue(key, out var def)) template = def;
                else template = key;
                return args.Length > 0 ? string.Format(template, args) : template;
            }

            public static string CurrentLanguage { get; set; } = "en";

            private static readonly Dictionary<string, string> _defaults = new Dictionary<string, string>
            {
                ["MainMenu"] = "← Main Menu",
                ["AppTitle"] = "TABS Arena v1.1.5",
                ["OverviewTitle"] = "2v2 Match Overview",
                ["OverviewSub"] = "Manage all four players then press Next Round to apply interest, milestones, rewards.",
                ["CurrentRound"] = "CURRENT ROUND",
                ["NextTurnOrder"] = "NEXT TURN ORDER",
                ["PendingResult"] = "PENDING RESULT",
                ["NotAvailableYet"] = "Not available yet",
                ["NotSet"] = "Not set",
                ["FactionMode"] = "FACTION MODE",
                ["FactionModeOff"] = "Faction Mode: OFF",
                ["FactionModeOn"] = "Faction Mode: ON",
                ["FT20Mode"] = "FT20 MODE",
                ["FT20ModeOff"] = "FT20 Mode: OFF",
                ["FT20ModeOn"] = "FT20 Mode: ON",
                ["FT30Mode"] = "FT30 MODE",
                ["FT30ModeOff"] = "FT30 Mode: OFF",
                ["FT30ModeOn"] = "FT30 Mode: ON",
                ["FT10Mode"] = "FT10 MODE",
                ["FT10ModeOff"] = "FT10 Mode: OFF",
                ["FT10ModeOn"] = "FT10 Mode: ON",
                ["WhichTeamFirst"] = "Which team is going first this match?",
                ["RedTeamFirst"] = "Red Team Goes First",
                ["BlueTeamFirst"] = "Blue Team Goes First",
                ["MatchSaves"] = "MATCH SAVES",
                ["MilestoneReward"] = "Milestone reward",
                ["Save"] = "💾 Save",
                ["Load"] = "📂 Load",
                ["Delete"] = "🗑 Delete",
                ["NewGame"] = "🆕 New Game",
                ["MilestoneProgress"] = "MILESTONE PROGRESS",
                ["NextReward"] = "NEXT REWARD",
                ["RewardsLeft"] = "POSSIBLE REWARDS LEFT",
                ["ActionLog"] = "Action Log",
                ["ActionLogSub"] = "Shop clicks and round results appear here.",
                ["RoundControl"] = "Round Control",
                ["RedTeamWins"] = "Red Team Wins",
                ["Tie"] = "Tie",
                ["StartTieTimer"] = "Start Tie Timer",
                ["StopTimer"] = "Stop Timer",
                ["ResumeTimer"] = "Resume Timer",
                ["RestartTimer"] = "Restart Timer",
                ["BlueTeamWins"] = "Blue Team Wins",
                ["NextRound"] = "Next Round",
                ["Undo"] = "Undo",
                ["RedTeam"] = "RED TEAM",
                ["BlueTeam"] = "BLUE TEAM",
                ["Gold"] = "GOLD",
                ["Points"] = "POINTS",
                ["PermMv"] = "PERM MV",
                ["Income"] = "INCOME",
                ["InterestStat"] = "INTEREST",
                ["MaxFactions"] = "Max factions",
                ["BuyIncome"] = "Buy income +10 (100)",
                ["BuyIncomeF"] = "Buy income +13 (130)",
                ["BuyPermMove"] = "Buy perm move +1 (200)",
                ["BuyPermMoveF"] = "Buy perm move +1 (175)",
                ["BuyFaction"] = "Buy faction (50)",
                ["BuyChosenFaction"] = "Buy chosen faction ({0}g)",
                ["ChosenFactionLabel"] = "chosen faction",
                ["ChooseFactionTitle"] = "Choose Faction",
                ["ChooseFactionSub"] = "{0}, choose one faction to buy.",
                ["LogBoughtChosenFaction"] = "P{0} bought chosen faction '{1}' for {2}g.",
                ["NoticeBoughtChosenFaction"] = "P{0} bought '{1}' for {2}g.",
                ["Upgrades"] = "Upgrades",
                ["FactionsOwned"] = "FACTIONS OWNED",
                ["Utility"] = "Utility",
                ["SingleTroopMove"] = "Single troop move ({0})",
                ["Replay"] = "Replay (10)",
                ["CustomSpend"] = "Custom troop spend",
                ["Spend"] = "Spend",
                ["TeammateUnit"] = "Teammate unit cost",
                ["BFT"] = "BFT",
                ["UnitValue"] = "Unit value",
                ["Sell"] = "Sell",
                ["Set"] = "Set",
                ["Unset"] = "Unset",
                ["Calculations"] = "Calculations",
                ["NoRoundYet"] = "No round yet.",
                ["Settings"] = "Settings",
                ["Guide"] = "2v2 Guide",
                ["GuideTitle"] = "2v2 Guide",
                ["ReplayUsed"] = "Replay used",
                ["GuideBasicsTitle"] = "Basics",
                ["GuideBasicsBody"] = "Each player starts with 1200 gold. At the start of the match, choose which team goes first. That team receives +40 gold per player to compensate for being counterpicked in round 1.",
                ["GuideTurnOrderTitle"] = "Turn Order",
                ["GuideTurnOrderBody"] = "In round 1, the chosen team goes first. After that, the team with the most points goes first. If points are tied, the team that won the latest round goes first.",
                ["GuideRoundTitle"] = "Rounds, Ties, And Replay",
                ["GuideRoundBody"] = "When a battle ends, choose the winner and press Next Round. If both teams agree it was a tie, use Tie. If there is no agreement, use a 3-minute timer and force a tie if nobody wins. Replay costs 10 gold and can only be bought once per round per team. Replay is for informational purposes only and does not change the outcome or winner of the round.",
                ["GuideEconomyTitle"] = "Economy",
                ["GuideEconomyBody"] = "Interest gives +10 gold for every 50 gold a player has, capped at +100. Buying income increases permanent income: +10 in FT30 and +13 in FT20. FT10 removes income purchases and income decay.",
                ["GuideRulesTitle"] = "2v2 Rules",
                ["GuideRulesBody"] = "Players may not control units during battle. On 2v2 maps, do not place units on high ground, in the middle circle, in crevices, or at entrances to the circle or crevices. There should be 2 armies per side, 1 army per player, 4 armies total. Currently banned units: Present Elf and Dragon.",
                ["GuideSavingTitle"] = "Saving",
                ["GuideSavingBody"] = "If you cannot finish a match, save in the app. Also save the battle inside TABS using Save Battle and enable Save Friendly Units.",
                ["Back"] = "← Back",
                ["WindowMode"] = "Window Mode",
                ["Windowed"] = "Windowed",
                ["BorderlessFullscreen"] = "Borderless Fullscreen",
                ["Language"] = "Language",
                ["Sounds"] = "Sounds",
                ["Volume"] = "Volume",
                ["On"] = "On",
                ["Off"] = "Off",
                ["RedTeamPoints"] = "🔴  RED TEAM POINTS: ",
                ["BlueTeamPoints"] = "🔵  BLUE TEAM POINTS: ",
                ["StartingGold"] = "Starting gold",
                ["Interest"] = "Interest",
                ["RoundReward"] = "Round reward",
                ["PermanentIncome"] = "Permanent income",
                ["FinalGold"] = "Final gold",
                ["CustomTroopSpend"] = "Custom troop spend",
                ["TeammateUnitCost"] = "Teammate unit cost",
                ["PtsAway"] = "pts away",
                ["NextAt"] = "next at",
                ["AllRewardsClaimed"] = "All rewards claimed!",
                ["SaveDialogTitle"] = "Save Game",
                ["EnterSaveName"] = "Enter save name:",
                ["SaveBtn"] = "Save",
                ["Cancel"] = "Cancel",
                ["Yes"] = "Yes",
                ["No"] = "No",
                ["NewGameConfirmTitle"] = "New Game",
                ["NewGameConfirmMsg"] = "Start a new game? Unsaved progress will be lost.",
                ["NewGameStarted"] = "New game started.",
                ["MatchEndTitle"] = "Match Complete",
                ["MatchEndMessage"] = "{0} won the match by reaching {1} points.",
                ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} wins via win by 2 rule.",
                ["MatchEndQuestion"] = "Start a new game or continue playing?",
                ["NewGamePlain"] = "New Game",
                ["ContinuePlaying"] = "Continue",
                ["MainMenuConfirmTitle"] = "Main Menu",
                ["MainMenuConfirmMsg"] = "Are you sure you want to go back to the main menu?",
                ["CloseGameConfirmTitle"] = "Close Game",
                ["CloseGameConfirmMsg"] = "Are you sure you want to close the game?",
                ["DeleteConfirmTitle"] = "Delete Save",
                ["DeleteConfirmMsg"] = "Delete \"{0}\"?",
                ["TurnOrderRed"] = "Turn: Red -> Blue",
                ["TurnOrderBlue"] = "Turn: Blue -> Red",
                ["DefaultP1Name"] = "Red Player 1",
                ["DefaultP2Name"] = "Red Player 2",
                ["DefaultP3Name"] = "Blue Player 1",
                ["DefaultP4Name"] = "Blue Player 2",
                ["LogRedGoesFirst"] = "Red Team goes first. +40g each.",
                ["LogBlueGoesFirst"] = "Blue Team goes first. +40g each.",
                ["LogRedWins"] = "Red wins",
                ["LogBlueWins"] = "Blue wins",   // use string.Format or make Loc.Get accept params
                ["LogRoundReward"] = "Round rewards: Red +{0}g each, Blue +{1}g each.",
                ["LogRoundRewardTie"] = "Round rewards: Tie — all players +{0}g each.",
                ["LogBoughtIncome"] = "P{0} bought income +{1} for {2}g ({3}% off) → +{4} total.",
                ["LogBoughtPermMove"] = "P{0} bought perm move for {1}g{2}. Total: {3}.",
                ["LogBoughtFaction"] = "P{0} bought '{1}' for {2}g{3}. Next: {4}g.",
                ["LogSpentOn"] = "P{0} spent {1}g on {2}.",
                ["LogSpent"] = "P{0} spent {1}g.",
                ["LogBFT"] = "P{0} BFT unit ({1}g) → paid {2}g (+{3}% surcharge).",
                ["LogSoldUnit"] = "P{0} sold unit ({1}g) → {2}g ({3}%).",
                ["LogFactionModeOn"] = "Faction Mode ON — panels reset, mode starting gold, 3 random factions.",
                ["LogFactionModeOff"] = "Faction Mode OFF.",
                ["LogFT20ModeOn"] = "FT20 Mode ON — panels reset.",
                ["LogFT20ModeOff"] = "FT20 Mode OFF — panels reset.",
                ["LogFT30ModeOn"] = "FT30 Mode ON — panels reset.",
                ["LogFT30ModeOff"] = "FT30 Mode OFF — FT20 mode selected.",
                ["LogFT10ModeOn"] = "FT10 Mode ON — 1200g start and income disabled.",
                ["LogFT10ModeOff"] = "FT10 Mode OFF — FT20 mode selected.",
                ["LogMilestone"] = "Milestone {0}pts — {1}: {2}",
                ["LogFT20Milestone"] = "FT20 Milestone {0}pts — {1}: {2}",
                ["LogLoaded"] = "Loaded {0}.",
                ["SingleTroopMoveLabel"] = "troop move",
                ["ReplayLabel"] = "replay",
                ["Pending"] = "Pending",
                ["LogRoundComplete"] = "✅ Round {0} complete. {1}. 🔴 {2} – {3} 🔵",
                ["NoticeRoundTie"] = "🤝 Round {0} ends in a tie! All players gain +{1}g.",
                ["NoticeRoundWin"] = "{0} {1} wins round {2}! Winners +{3}g, Losers +{4}g per player.",
                ["NoticeBoughtIncome"] = "P{0} income now +{1}. Paid {2}g.",
                ["NoticeBoughtPermMove"] = "P{0} now has {1} perm move(s).",
                ["NoticeBoughtFaction"] = "P{0} received '{1}'! Next faction: {2}g.",
                ["NoticeSoldUnit"] = "P{0} sold unit for +{1}g ({2}% sellback).",
                ["NoticeSpentOn"] = "P{0} spent {1}g on {2}.",
                ["NoticeSpent"] = "P{0} spent {1}g.",
                ["NoticeBFT"] = "P{0} bought for teammate: {1}g (+{2}% surcharge).",
                ["NoticeFactionModeOn"] = "Faction Mode ON! Player panels reset.",
                ["NoticeFactionModeOff"] = "Faction Mode OFF.",
                ["NoticeFT20ModeOn"] = "FT20 Mode ON! Player panels reset.",
                ["NoticeFT20ModeOff"] = "FT20 Mode OFF! Player panels reset.",
                ["NoticeFT30ModeOn"] = "FT30 Mode ON! Player panels reset.",
                ["NoticeFT30ModeOff"] = "FT30 Mode OFF. FT20 mode selected.",
                ["NoticeFT10ModeOn"] = "FT10 Mode ON! 1200g start and income disabled.",
                ["NoticeFT10ModeOff"] = "FT10 Mode OFF. FT20 mode selected.",
                ["NothingToUndo"] = "Nothing to undo.",
                ["RedTeamShort"] = "Red",
                ["BlueTeamShort"] = "Blue",
                ["NoticeMilestone"] = "🏆 Milestone {0} pts! {1} earned: {2}",
                ["NoticeFT20Milestone"] = "🏆 FT20 Milestone {0} pts! {1} earned: {2}",
                ["SetWinnerBeforeAdvancing"] = "Set a round winner before advancing.",
                ["IncomeAlreadyBought"] = "P{0} already bought income this round.",
                ["NeedsGold"] = "P{0} needs {1}g (has {2}g).",
                ["NeedsGoldFor"] = "P{0} needs {1}g for {2} (has {3}g).",
                ["MaxedPermMove"] = "P{0} already maxed perm move ({1}/{2}).",
                ["HasAllFactions"] = "P{0} has all factions.",
                ["RedReplayAlreadyBought"] = "Red Team already bought replay this round.",
                ["BlueReplayAlreadyBought"] = "Blue Team already bought replay this round.",
                ["EnterPositiveAmount"] = "Enter a valid positive amount.",
                ["OnlyHasGold"] = "P{0} only has {1}g (needs {2}g).",
                ["EnterValidUnitCost"] = "Enter a valid unit cost.",
                ["EnterValidUnitValue"] = "Enter a valid unit value.",
                ["SavedAs"] = "Saved as \"{0}\".",
                ["SelectSaveToLoad"] = "Select a save to load.",
                ["SaveFileNotFound"] = "Save file not found.",
                ["LoadedSave"] = "Loaded \"{0}\".",
                ["SelectSaveToDelete"] = "Select a save to delete.",
                ["DeletedSave"] = "Deleted \"{0}\".",
                ["Reward80OffFaction"] = "80% Off Next Faction",
                ["Reward80OffChosenFaction"] = "80% Off Next Chosen Faction",
                ["Reward80OffPermMove"] = "80% Off Next Perm Move",
                ["RewardSellback15"] = "Sellback +15%",
                ["Reward10OffIncome"] = "10% Off Next Income",
                ["Reward30NextSell"] = "+30% Next Sell",
                ["RewardMinus5BFT"] = "-5% BFT Surcharge",
                ["DiscountSuffix"] = " ({0}% off)",
                ["LogSharedGoldMilestone"] = "Milestone FT{0}: {1} Team +{2}g each.",
                ["NoticeSharedGoldMilestone"] = "🏆 Milestone FT{0}! {1} Team +{2}g each.",
                ["LogSharedPermMoveMilestone"] = "Milestone FT{0}: {1} Team +1 perm move each.",
                ["NoticeSharedPermMoveMilestone"] = "🏆 Milestone FT{0}! {1} Team: +1 perm move each.",
                ["LogSharedSellbackMilestone"] = "Milestone FT{0}: {1} Team +20% permanent sellback.",
                ["NoticeSharedSellbackMilestone"] = "🏆 Milestone FT{0}! {1} Team: +20% permanent sellback.",
                ["GuideMoreTitle"] = "More Rules",
                ["GuideMoreBody"] = "To learn more about the rules, visit",
                ["CloseGameConfirmTitle"] = "Close Game",
                ["CloseGameConfirmMsg"] = "Are you sure you want to close the game?",
            };
        }
        private TwoV2SaveData BuildSaveData(string name) => new TwoV2SaveData
        {
            SaveVersion = 7,
            SaveName = name,
            Round = _round,
            PendingWinner = _pendingWinner,
            RedPoints = _redPoints,
            BluePoints = _bluePoints,
            FirstTurnChosen = _firstTurnChosen,
            NamesLocked = _namesLocked,
            LastRoundWinner = _lastRoundWinner,
            TurnOrderText = TurnOrderText.Text,

            P1Gold = _p1Gold,
            P2Gold = _p2Gold,
            P3Gold = _p3Gold,
            P4Gold = _p4Gold,
            P1GoldState = _p1GoldState,
            P2GoldState = _p2GoldState,
            P3GoldState = _p3GoldState,
            P4GoldState = _p4GoldState,
            P1PointsState = _p1PointsState,
            P2PointsState = _p2PointsState,
            P3PointsState = _p3PointsState,
            P4PointsState = _p4PointsState,
            P1InterestState = _p1InterestState,
            P2InterestState = _p2InterestState,
            P3InterestState = _p3InterestState,
            P4InterestState = _p4InterestState,

            P1Income = _p1Income,
            P2Income = _p2Income,
            P3Income = _p3Income,
            P4Income = _p4Income,
            P1IncomeUpgrades = _p1IncomeUpgrades,
            P2IncomeUpgrades = _p2IncomeUpgrades,
            P3IncomeUpgrades = _p3IncomeUpgrades,
            P4IncomeUpgrades = _p4IncomeUpgrades,
            P1IncomeCost = _p1IncomeCost,
            P2IncomeCost = _p2IncomeCost,
            P3IncomeCost = _p3IncomeCost,
            P4IncomeCost = _p4IncomeCost,
            P1BoughtIncome = _p1BoughtIncome,
            P2BoughtIncome = _p2BoughtIncome,
            P3BoughtIncome = _p3BoughtIncome,
            P4BoughtIncome = _p4BoughtIncome,
            P1BoughtIncomeThisRound = _p1BoughtIncomeThisRound,
            P2BoughtIncomeThisRound = _p2BoughtIncomeThisRound,
            P3BoughtIncomeThisRound = _p3BoughtIncomeThisRound,
            P4BoughtIncomeThisRound = _p4BoughtIncomeThisRound,
            RedReplayBoughtThisRound = _redReplayBoughtThisRound,
            BlueReplayBoughtThisRound = _blueReplayBoughtThisRound,
            P1IncomeMissedRounds = _p1IncomeMissedRounds,
            P2IncomeMissedRounds = _p2IncomeMissedRounds,
            P3IncomeMissedRounds = _p3IncomeMissedRounds,
            P4IncomeMissedRounds = _p4IncomeMissedRounds,
            P1IncomeDecayPct = _p1IncomeDecayPct,
            P2IncomeDecayPct = _p2IncomeDecayPct,
            P3IncomeDecayPct = _p3IncomeDecayPct,
            P4IncomeDecayPct = _p4IncomeDecayPct,

            P1PermMoveUpgrades = _p1PermMoveUpgrades,
            P2PermMoveUpgrades = _p2PermMoveUpgrades,
            P3PermMoveUpgrades = _p3PermMoveUpgrades,
            P4PermMoveUpgrades = _p4PermMoveUpgrades,
            P1PermMovePurchases = _p1PermMovePurchases,
            P2PermMovePurchases = _p2PermMovePurchases,
            P3PermMovePurchases = _p3PermMovePurchases,
            P4PermMovePurchases = _p4PermMovePurchases,

            FactionModeEnabled = _factionModeEnabled,
            FactionModeLocked = _factionModeLocked,
            FT20ModeEnabled = _ft20ModeEnabled,
            FT10ModeEnabled = _ft10ModeEnabled,
            FT30ModeEnabled = _ft30ModeEnabled,
            FT20ModeLocked = _ft20ModeLocked,
            MatchEndPromptSuppressed = _matchEndPromptSuppressed,
            FT20NextMilestone = _ft20NextMilestone,
            FT20RewardsRemaining = new List<string>(_ft20RewardsRemaining),
            MilestoneRewardsRemaining = new List<string>(_milestoneRewardsRemaining),
            MilestoneNextThreshold = _milestoneNextThreshold,
            MilestoneSystemActive = _milestoneSystemActive,

            P1Factions = new List<string>(_p1Factions),
            P2Factions = new List<string>(_p2Factions),
            P3Factions = new List<string>(_p3Factions),
            P4Factions = new List<string>(_p4Factions),
            P1FactionPurchases = _p1FactionPurchases,
            P2FactionPurchases = _p2FactionPurchases,
            P3FactionPurchases = _p3FactionPurchases,
            P4FactionPurchases = _p4FactionPurchases,
            P1ChosenFactionPurchases = _p1ChosenFactionPurchases,
            P2ChosenFactionPurchases = _p2ChosenFactionPurchases,
            P3ChosenFactionPurchases = _p3ChosenFactionPurchases,
            P4ChosenFactionPurchases = _p4ChosenFactionPurchases,

            RedPermanentSellbackBonusPct = _redPermanentSellbackBonusPct,
            BluePermanentSellbackBonusPct = _bluePermanentSellbackBonusPct,
            RedBFTSurcharge = _redBFTSurcharge,
            BlueBFTSurcharge = _blueBFTSurcharge,

            P1NextIncomeDiscountPct = _p1NextIncomeDiscountPct,
            P2NextIncomeDiscountPct = _p2NextIncomeDiscountPct,
            P3NextIncomeDiscountPct = _p3NextIncomeDiscountPct,
            P4NextIncomeDiscountPct = _p4NextIncomeDiscountPct,

            P1NextSellBonusPct = _p1NextSellBonusPct,
            P2NextSellBonusPct = _p2NextSellBonusPct,
            P3NextSellBonusPct = _p3NextSellBonusPct,
            P4NextSellBonusPct = _p4NextSellBonusPct,

            P1NextFactionDiscountPct = _p1NextFactionDiscountPct,
            P2NextFactionDiscountPct = _p2NextFactionDiscountPct,
            P3NextFactionDiscountPct = _p3NextFactionDiscountPct,
            P4NextFactionDiscountPct = _p4NextFactionDiscountPct,

            P1NextChosenFactionDiscountPct = _p1NextChosenFactionDiscountPct,
            P2NextChosenFactionDiscountPct = _p2NextChosenFactionDiscountPct,
            P3NextChosenFactionDiscountPct = _p3NextChosenFactionDiscountPct,
            P4NextChosenFactionDiscountPct = _p4NextChosenFactionDiscountPct,

            P1NextPermMoveDiscountPct = _p1NextPermMoveDiscountPct,
            P2NextPermMoveDiscountPct = _p2NextPermMoveDiscountPct,
            P3NextPermMoveDiscountPct = _p3NextPermMoveDiscountPct,
            P4NextPermMoveDiscountPct = _p4NextPermMoveDiscountPct,

            P1PermMoveCapUnlocked = _p1PermMoveCapUnlocked,
            P2PermMoveCapUnlocked = _p2PermMoveCapUnlocked,
            P3PermMoveCapUnlocked = _p3PermMoveCapUnlocked,
            P4PermMoveCapUnlocked = _p4PermMoveCapUnlocked,

            Milestone5Claimed = _milestone5Claimed,
            Milestone10Claimed = _milestone10Claimed,
            Milestone15Claimed = _milestone15Claimed,
            Milestone20Claimed = _milestone20Claimed,
            Milestone25Claimed = _milestone25Claimed,

            P1Name = P1NameBox.Text,
            P2Name = P2NameBox.Text,
            P3Name = P3NameBox.Text,
            P4Name = P4NameBox.Text,

            P1LastCalcText = _p1LastCalcText,
            P2LastCalcText = _p2LastCalcText,
            P3LastCalcText = _p3LastCalcText,
            P4LastCalcText = _p4LastCalcText,

            ActionLog = new List<string>(_actionLog)
        };

        private void RestoreFromSaveData(TwoV2SaveData d)
        {
            _round = d.Round; _pendingWinner = d.PendingWinner;
            _redPoints = d.RedPoints; _bluePoints = d.BluePoints;
            _firstTurnChosen = d.FirstTurnChosen; _namesLocked = d.NamesLocked;
            _lastRoundWinner = d.LastRoundWinner;
            TurnOrderText.Text = d.TurnOrderText ?? "";

            _p1Gold = d.P1Gold; _p2Gold = d.P2Gold;
            _p3Gold = d.P3Gold; _p4Gold = d.P4Gold;
            _p1GoldState = d.P1GoldState; _p2GoldState = d.P2GoldState;
            _p3GoldState = d.P3GoldState; _p4GoldState = d.P4GoldState;
            _p1PointsState = d.P1PointsState; _p2PointsState = d.P2PointsState;
            _p3PointsState = d.P3PointsState; _p4PointsState = d.P4PointsState;
            _p1InterestState = d.P1InterestState; _p2InterestState = d.P2InterestState;
            _p3InterestState = d.P3InterestState; _p4InterestState = d.P4InterestState;

            _p1Income = d.P1Income; _p2Income = d.P2Income;
            _p3Income = d.P3Income; _p4Income = d.P4Income;
            _p1IncomeUpgrades = d.P1IncomeUpgrades; _p2IncomeUpgrades = d.P2IncomeUpgrades;
            _p3IncomeUpgrades = d.P3IncomeUpgrades; _p4IncomeUpgrades = d.P4IncomeUpgrades;
            _p1IncomeCost = d.P1IncomeCost; _p2IncomeCost = d.P2IncomeCost;
            _p3IncomeCost = d.P3IncomeCost; _p4IncomeCost = d.P4IncomeCost;
            _p1BoughtIncome = d.P1BoughtIncome; _p2BoughtIncome = d.P2BoughtIncome;
            _p3BoughtIncome = d.P3BoughtIncome; _p4BoughtIncome = d.P4BoughtIncome;
            _p1BoughtIncomeThisRound = d.P1BoughtIncomeThisRound;
            _p2BoughtIncomeThisRound = d.P2BoughtIncomeThisRound;
            _p3BoughtIncomeThisRound = d.P3BoughtIncomeThisRound;
            _p4BoughtIncomeThisRound = d.P4BoughtIncomeThisRound;
            _redReplayBoughtThisRound = d.RedReplayBoughtThisRound;
            _blueReplayBoughtThisRound = d.BlueReplayBoughtThisRound;
            _p1IncomeMissedRounds = d.P1IncomeMissedRounds;
            _p2IncomeMissedRounds = d.P2IncomeMissedRounds;
            _p3IncomeMissedRounds = d.P3IncomeMissedRounds;
            _p4IncomeMissedRounds = d.P4IncomeMissedRounds;
            _p1IncomeDecayPct = d.P1IncomeDecayPct; _p2IncomeDecayPct = d.P2IncomeDecayPct;
            _p3IncomeDecayPct = d.P3IncomeDecayPct; _p4IncomeDecayPct = d.P4IncomeDecayPct;

            _p1PermMoveUpgrades = d.P1PermMoveUpgrades;
            _p2PermMoveUpgrades = d.P2PermMoveUpgrades;
            _p3PermMoveUpgrades = d.P3PermMoveUpgrades;
            _p4PermMoveUpgrades = d.P4PermMoveUpgrades;
            _p1PermMovePurchases = d.P1PermMovePurchases;
            _p2PermMovePurchases = d.P2PermMovePurchases;
            _p3PermMovePurchases = d.P3PermMovePurchases;
            _p4PermMovePurchases = d.P4PermMovePurchases;

            _factionModeEnabled = d.FactionModeEnabled;
            _factionModeLocked = d.FactionModeLocked;
            _ft10ModeEnabled = d.FT10ModeEnabled;
            _ft30ModeEnabled = d.FT30ModeEnabled || (d.SaveVersion < 7 && !d.FT20ModeEnabled);
            _ft20ModeEnabled = !_ft10ModeEnabled && !_ft30ModeEnabled;
            NormalizeMatchModeFlags();
            _ft20ModeLocked = d.FT20ModeLocked;
            _matchEndPromptSuppressed = d.MatchEndPromptSuppressed;
            _ft20NextMilestone = d.FT20NextMilestone > 0 ? d.FT20NextMilestone : GetTimedMilestoneStep();
            _ft20RewardsRemaining = new List<string>(d.FT20RewardsRemaining ?? new List<string>());
            _milestoneRewardsRemaining = new List<string>(d.MilestoneRewardsRemaining ?? new List<string>());
            _milestoneNextThreshold = d.MilestoneNextThreshold > 0 ? d.MilestoneNextThreshold : 5;
            _milestoneSystemActive = d.MilestoneSystemActive;

            _p1Factions = new List<string>(d.P1Factions ?? new List<string>());
            _p2Factions = new List<string>(d.P2Factions ?? new List<string>());
            _p3Factions = new List<string>(d.P3Factions ?? new List<string>());
            _p4Factions = new List<string>(d.P4Factions ?? new List<string>());
            _p1FactionPurchases = d.P1FactionPurchases;
            _p2FactionPurchases = d.P2FactionPurchases;
            _p3FactionPurchases = d.P3FactionPurchases;
            _p4FactionPurchases = d.P4FactionPurchases;
            _p1ChosenFactionPurchases = d.P1ChosenFactionPurchases;
            _p2ChosenFactionPurchases = d.P2ChosenFactionPurchases;
            _p3ChosenFactionPurchases = d.P3ChosenFactionPurchases;
            _p4ChosenFactionPurchases = d.P4ChosenFactionPurchases;

            _redPermanentSellbackBonusPct = d.RedPermanentSellbackBonusPct;
            _bluePermanentSellbackBonusPct = d.BluePermanentSellbackBonusPct;
            _redBFTSurcharge = d.RedBFTSurcharge > 0 ? d.RedBFTSurcharge : 15;
            _blueBFTSurcharge = d.BlueBFTSurcharge > 0 ? d.BlueBFTSurcharge : 15;

            _p1NextIncomeDiscountPct = d.P1NextIncomeDiscountPct;
            _p2NextIncomeDiscountPct = d.P2NextIncomeDiscountPct;
            _p3NextIncomeDiscountPct = d.P3NextIncomeDiscountPct;
            _p4NextIncomeDiscountPct = d.P4NextIncomeDiscountPct;

            _p1NextSellBonusPct = d.P1NextSellBonusPct;
            _p2NextSellBonusPct = d.P2NextSellBonusPct;
            _p3NextSellBonusPct = d.P3NextSellBonusPct;
            _p4NextSellBonusPct = d.P4NextSellBonusPct;

            _p1NextFactionDiscountPct = d.P1NextFactionDiscountPct;
            _p2NextFactionDiscountPct = d.P2NextFactionDiscountPct;
            _p3NextFactionDiscountPct = d.P3NextFactionDiscountPct;
            _p4NextFactionDiscountPct = d.P4NextFactionDiscountPct;

            _p1NextChosenFactionDiscountPct = d.P1NextChosenFactionDiscountPct;
            _p2NextChosenFactionDiscountPct = d.P2NextChosenFactionDiscountPct;
            _p3NextChosenFactionDiscountPct = d.P3NextChosenFactionDiscountPct;
            _p4NextChosenFactionDiscountPct = d.P4NextChosenFactionDiscountPct;

            _p1NextPermMoveDiscountPct = d.P1NextPermMoveDiscountPct;
            _p2NextPermMoveDiscountPct = d.P2NextPermMoveDiscountPct;
            _p3NextPermMoveDiscountPct = d.P3NextPermMoveDiscountPct;
            _p4NextPermMoveDiscountPct = d.P4NextPermMoveDiscountPct;

            _p1PermMoveCapUnlocked = d.P1PermMoveCapUnlocked;
            _p2PermMoveCapUnlocked = d.P2PermMoveCapUnlocked;
            _p3PermMoveCapUnlocked = d.P3PermMoveCapUnlocked;
            _p4PermMoveCapUnlocked = d.P4PermMoveCapUnlocked;

            _milestone5Claimed = d.Milestone5Claimed;
            _milestone10Claimed = d.Milestone10Claimed;
            _milestone15Claimed = d.Milestone15Claimed;
            _milestone20Claimed = d.Milestone20Claimed;
            _milestone25Claimed = d.Milestone25Claimed;

            P1NameBox.Text = d.P1Name ?? Loc.Get("DefaultP1Name");
            P2NameBox.Text = d.P2Name ?? Loc.Get("DefaultP2Name");
            P3NameBox.Text = d.P3Name ?? Loc.Get("DefaultP3Name");
            P4NameBox.Text = d.P4Name ?? Loc.Get("DefaultP4Name");

            _p1LastCalcText = string.IsNullOrWhiteSpace(d.P1LastCalcText) ? Loc.Get("NoRoundYet") : d.P1LastCalcText;
            _p2LastCalcText = string.IsNullOrWhiteSpace(d.P2LastCalcText) ? Loc.Get("NoRoundYet") : d.P2LastCalcText;
            _p3LastCalcText = string.IsNullOrWhiteSpace(d.P3LastCalcText) ? Loc.Get("NoRoundYet") : d.P3LastCalcText;
            _p4LastCalcText = string.IsNullOrWhiteSpace(d.P4LastCalcText) ? Loc.Get("NoRoundYet") : d.P4LastCalcText;

            _actionLog = new List<string>(d.ActionLog ?? new List<string>());
        }

        // ─────────────────────────────────────────────────────────────────
        //  Per-player accessors (avoids switch/case chains everywhere)
        // ─────────────────────────────────────────────────────────────────
        private int GetIncome(int p) { switch (p) { case 1: return _p1Income; case 2: return _p2Income; case 3: return _p3Income; default: return _p4Income; } }

       
        private void SetIncome(int p, int v) { switch (p) { case 1: _p1Income = v; break; case 2: _p2Income = v; break; case 3: _p3Income = v; break; default: _p4Income = v; break; } }
        private int GetIncomeUpgrades(int p) { switch (p) { case 1: return _p1IncomeUpgrades; case 2: return _p2IncomeUpgrades; case 3: return _p3IncomeUpgrades; default: return _p4IncomeUpgrades; } }
        private void SetIncomeUpgrades(int p, int v) { switch (p) { case 1: _p1IncomeUpgrades = v; break; case 2: _p2IncomeUpgrades = v; break; case 3: _p3IncomeUpgrades = v; break; default: _p4IncomeUpgrades = v; break; } }
        private decimal GetIncomeCost(int p) { switch (p) { case 1: return _p1IncomeCost; case 2: return _p2IncomeCost; case 3: return _p3IncomeCost; default: return _p4IncomeCost; } }
        private void SetIncomeCost(int p, decimal v) { switch (p) { case 1: _p1IncomeCost = v; break; case 2: _p2IncomeCost = v; break; case 3: _p3IncomeCost = v; break; default: _p4IncomeCost = v; break; } }
        private bool GetBoughtIncome(int p) { switch (p) { case 1: return _p1BoughtIncome; case 2: return _p2BoughtIncome; case 3: return _p3BoughtIncome; default: return _p4BoughtIncome; } }
        private void SetBoughtIncome(int p, bool v) { switch (p) { case 1: _p1BoughtIncome = v; break; case 2: _p2BoughtIncome = v; break; case 3: _p3BoughtIncome = v; break; default: _p4BoughtIncome = v; break; } }

        private int GetNextIncomeDiscountPct(int p) { switch (p) { case 1: return _p1NextIncomeDiscountPct; case 2: return _p2NextIncomeDiscountPct; case 3: return _p3NextIncomeDiscountPct; default: return _p4NextIncomeDiscountPct; } }
        private void SetNextIncomeDiscountPct(int p, int v) { switch (p) { case 1: _p1NextIncomeDiscountPct = v; break; case 2: _p2NextIncomeDiscountPct = v; break; case 3: _p3NextIncomeDiscountPct = v; break; default: _p4NextIncomeDiscountPct = v; break; } }

        private int GetNextSellBonusPct(int p) { switch (p) { case 1: return _p1NextSellBonusPct; case 2: return _p2NextSellBonusPct; case 3: return _p3NextSellBonusPct; default: return _p4NextSellBonusPct; } }
        private void SetNextSellBonusPct(int p, int v) { switch (p) { case 1: _p1NextSellBonusPct = v; break; case 2: _p2NextSellBonusPct = v; break; case 3: _p3NextSellBonusPct = v; break; default: _p4NextSellBonusPct = v; break; } }

        private int GetNextFactionDiscountPct(int p) { switch (p) { case 1: return _p1NextFactionDiscountPct; case 2: return _p2NextFactionDiscountPct; case 3: return _p3NextFactionDiscountPct; default: return _p4NextFactionDiscountPct; } }
        private void SetNextFactionDiscountPct(int p, int v) { switch (p) { case 1: _p1NextFactionDiscountPct = v; break; case 2: _p2NextFactionDiscountPct = v; break; case 3: _p3NextFactionDiscountPct = v; break; default: _p4NextFactionDiscountPct = v; break; } }

        private int GetNextChosenFactionDiscountPct(int p) { switch (p) { case 1: return _p1NextChosenFactionDiscountPct; case 2: return _p2NextChosenFactionDiscountPct; case 3: return _p3NextChosenFactionDiscountPct; default: return _p4NextChosenFactionDiscountPct; } }
        private void SetNextChosenFactionDiscountPct(int p, int v) { switch (p) { case 1: _p1NextChosenFactionDiscountPct = v; break; case 2: _p2NextChosenFactionDiscountPct = v; break; case 3: _p3NextChosenFactionDiscountPct = v; break; default: _p4NextChosenFactionDiscountPct = v; break; } }

        private int GetNextPermMoveDiscountPct(int p) { switch (p) { case 1: return _p1NextPermMoveDiscountPct; case 2: return _p2NextPermMoveDiscountPct; case 3: return _p3NextPermMoveDiscountPct; default: return _p4NextPermMoveDiscountPct; } }
        private void SetNextPermMoveDiscountPct(int p, int v) { switch (p) { case 1: _p1NextPermMoveDiscountPct = v; break; case 2: _p2NextPermMoveDiscountPct = v; break; case 3: _p3NextPermMoveDiscountPct = v; break; default: _p4NextPermMoveDiscountPct = v; break; } }

        private bool GetPermMoveCapUnlocked(int p) { switch (p) { case 1: return _p1PermMoveCapUnlocked; case 2: return _p2PermMoveCapUnlocked; case 3: return _p3PermMoveCapUnlocked; default: return _p4PermMoveCapUnlocked; } }
        private void SetPermMoveCapUnlocked(int p, bool v) { switch (p) { case 1: _p1PermMoveCapUnlocked = v; break; case 2: _p2PermMoveCapUnlocked = v; break; case 3: _p3PermMoveCapUnlocked = v; break; default: _p4PermMoveCapUnlocked = v; break; } }

        private int GetPermMoveMaxPurchases(int p) => GetPermMoveCapUnlocked(p) ? 3 : 2;

        private int GetBaseSellbackPct(int player)
        {
            int bonus = player <= 2 ? _redPermanentSellbackBonusPct : _bluePermanentSellbackBonusPct;
            return 50 + bonus;
        }

        private int GetDisplayedSellPct(int player)
        {
            return GetBaseSellbackPct(player) + GetNextSellBonusPct(player);
        }

        private int GetDisplayedIncomeCost(int player)
        {
            decimal baseCost = GetIncomeCost(player);
            int totalDiscountPct = GetIncomeDecayPct(player) + GetNextIncomeDiscountPct(player);
            decimal shown = Math.Max(1m, Math.Round(baseCost * (1m - totalDiscountPct / 100m)));
            return (int)Math.Ceiling(shown);
        }
        private bool GetBoughtIncomeThisRound(int p)
        {
            switch (p)
            {
                case 1: return _p1BoughtIncomeThisRound;
                case 2: return _p2BoughtIncomeThisRound;
                case 3: return _p3BoughtIncomeThisRound;
                default: return _p4BoughtIncomeThisRound;
            }
        }

        private void SetBoughtIncomeThisRound(int p, bool v)
        {
            switch (p)
            {
                case 1: _p1BoughtIncomeThisRound = v; break;
                case 2: _p2BoughtIncomeThisRound = v; break;
                case 3: _p3BoughtIncomeThisRound = v; break;
                default: _p4BoughtIncomeThisRound = v; break;
            }
        }
        private int GetIncomeMissedRounds(int p) { switch (p) { case 1: return _p1IncomeMissedRounds; case 2: return _p2IncomeMissedRounds; case 3: return _p3IncomeMissedRounds; default: return _p4IncomeMissedRounds; } }
        private void SetIncomeMissedRounds(int p, int v) { switch (p) { case 1: _p1IncomeMissedRounds = v; break; case 2: _p2IncomeMissedRounds = v; break; case 3: _p3IncomeMissedRounds = v; break; default: _p4IncomeMissedRounds = v; break; } }
        private int GetIncomeDecayPct(int p) { switch (p) { case 1: return _p1IncomeDecayPct; case 2: return _p2IncomeDecayPct; case 3: return _p3IncomeDecayPct; default: return _p4IncomeDecayPct; } }
        private void SetIncomeDecayPct(int p, int v) { switch (p) { case 1: _p1IncomeDecayPct = v; break; case 2: _p2IncomeDecayPct = v; break; case 3: _p3IncomeDecayPct = v; break; default: _p4IncomeDecayPct = v; break; } }
        private int GetPermMoveUpgrades(int p) { switch (p) { case 1: return _p1PermMoveUpgrades; case 2: return _p2PermMoveUpgrades; case 3: return _p3PermMoveUpgrades; default: return _p4PermMoveUpgrades; } }
        private void SetPermMoveUpgrades(int p, int v) { switch (p) { case 1: _p1PermMoveUpgrades = v; break; case 2: _p2PermMoveUpgrades = v; break; case 3: _p3PermMoveUpgrades = v; break; default: _p4PermMoveUpgrades = v; break; } }
        private int GetPermMovePurchases(int p) { switch (p) { case 1: return _p1PermMovePurchases; case 2: return _p2PermMovePurchases; case 3: return _p3PermMovePurchases; default: return _p4PermMovePurchases; } }
        private void SetPermMovePurchases(int p, int v) { switch (p) { case 1: _p1PermMovePurchases = v; break; case 2: _p2PermMovePurchases = v; break; case 3: _p3PermMovePurchases = v; break; default: _p4PermMovePurchases = v; break; } }
        private List<string> GetFactions(int p) { switch (p) { case 1: return _p1Factions; case 2: return _p2Factions; case 3: return _p3Factions; default: return _p4Factions; } }
        private void SetFactions(int p, List<string> v) { switch (p) { case 1: _p1Factions = v; break; case 2: _p2Factions = v; break; case 3: _p3Factions = v; break; default: _p4Factions = v; break; } }
        private int GetFactionPurchases(int p) { switch (p) { case 1: return _p1FactionPurchases; case 2: return _p2FactionPurchases; case 3: return _p3FactionPurchases; default: return _p4FactionPurchases; } }
        private void SetFactionPurchases(int p, int v) { switch (p) { case 1: _p1FactionPurchases = v; break; case 2: _p2FactionPurchases = v; break; case 3: _p3FactionPurchases = v; break; default: _p4FactionPurchases = v; break; } }

        private int GetChosenFactionPurchases(int p) { switch (p) { case 1: return _p1ChosenFactionPurchases; case 2: return _p2ChosenFactionPurchases; case 3: return _p3ChosenFactionPurchases; default: return _p4ChosenFactionPurchases; } }
        private void SetChosenFactionPurchases(int p, int v) { switch (p) { case 1: _p1ChosenFactionPurchases = v; break; case 2: _p2ChosenFactionPurchases = v; break; case 3: _p3ChosenFactionPurchases = v; break; default: _p4ChosenFactionPurchases = v; break; } }

        private int GetChosenFactionCost(int p)
        {
            int baseCost = _ft10ModeEnabled ? 140 : 280;
            int scale = _ft10ModeEnabled ? 15 : 20;
            return baseCost + (GetFactionPurchases(p) * scale);
        }

        private int GetDisplayedChosenFactionCost(int p)
        {
            int baseCost = GetChosenFactionCost(p);
            int discountPct = GetNextChosenFactionDiscountPct(p);
            return (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    //  Save data model
    // ─────────────────────────────────────────────────────────────────────
    public class TwoV2SaveData
    {
        public int SaveVersion { get; set; }
        public string SaveName { get; set; }
        public int Round { get; set; }
        public int PendingWinner { get; set; }
        public int RedPoints { get; set; }
        public int BluePoints { get; set; }
        public bool FirstTurnChosen { get; set; }
        public bool NamesLocked { get; set; }
        public int LastRoundWinner { get; set; }
        public string TurnOrderText { get; set; }

        public int P1Gold { get; set; }
        public int P2Gold { get; set; }
        public int P3Gold { get; set; }
        public int P4Gold { get; set; }
        public int P1GoldState { get; set; }
        public int P2GoldState { get; set; }
        public int P3GoldState { get; set; }
        public int P4GoldState { get; set; }
        public int P1PointsState { get; set; }
        public int P2PointsState { get; set; }
        public int P3PointsState { get; set; }
        public int P4PointsState { get; set; }
        public int P1InterestState { get; set; }
        public int P2InterestState { get; set; }
        public int P3InterestState { get; set; }
        public int P4InterestState { get; set; }

        public int P1Income { get; set; }
        public int P2Income { get; set; }
        public int P3Income { get; set; }
        public int P4Income { get; set; }
        public int P1IncomeUpgrades { get; set; }
        public int P2IncomeUpgrades { get; set; }
        public int P3IncomeUpgrades { get; set; }
        public int P4IncomeUpgrades { get; set; }
        public decimal P1IncomeCost { get; set; }
        public decimal P2IncomeCost { get; set; }
        public decimal P3IncomeCost { get; set; }
        public decimal P4IncomeCost { get; set; }
        public bool P1BoughtIncome { get; set; }
        public bool P2BoughtIncome { get; set; }
        public bool P3BoughtIncome { get; set; }
        public bool P4BoughtIncome { get; set; }

        public bool P1BoughtIncomeThisRound { get; set; }
        public bool P2BoughtIncomeThisRound { get; set; }
        public bool P3BoughtIncomeThisRound { get; set; }
        public bool P4BoughtIncomeThisRound { get; set; }
        public bool RedReplayBoughtThisRound { get; set; }
        public bool BlueReplayBoughtThisRound { get; set; }
        public int P1IncomeMissedRounds { get; set; }
        public int P2IncomeMissedRounds { get; set; }
        public int P3IncomeMissedRounds { get; set; }
        public int P4IncomeMissedRounds { get; set; }
        public int P1IncomeDecayPct { get; set; }
        public int P2IncomeDecayPct { get; set; }
        public int P3IncomeDecayPct { get; set; }
        public int P4IncomeDecayPct { get; set; }

        public int P1PermMoveUpgrades { get; set; }
        public int P2PermMoveUpgrades { get; set; }
        public int P3PermMoveUpgrades { get; set; }
        public int P4PermMoveUpgrades { get; set; }
        public int P1PermMovePurchases { get; set; }
        public int P2PermMovePurchases { get; set; }
        public int P3PermMovePurchases { get; set; }
        public int P4PermMovePurchases { get; set; }

        public bool FactionModeEnabled { get; set; }
        public bool FactionModeLocked { get; set; }
        public bool FT20ModeEnabled { get; set; }
        public bool FT10ModeEnabled { get; set; }
        public bool FT30ModeEnabled { get; set; }
        public bool FT20ModeLocked { get; set; }
        public bool MatchEndPromptSuppressed { get; set; }
        public int FT20NextMilestone { get; set; }
        public List<string> FT20RewardsRemaining { get; set; }
        public List<string> MilestoneRewardsRemaining { get; set; }
        public int MilestoneNextThreshold { get; set; }
        public bool MilestoneSystemActive { get; set; }

        public List<string> P1Factions { get; set; }
        public List<string> P2Factions { get; set; }
        public List<string> P3Factions { get; set; }
        public List<string> P4Factions { get; set; }
        public int P1FactionPurchases { get; set; }
        public int P2FactionPurchases { get; set; }
        public int P3FactionPurchases { get; set; }
        public int P4FactionPurchases { get; set; }

        public int P1ChosenFactionPurchases { get; set; }
        public int P2ChosenFactionPurchases { get; set; }
        public int P3ChosenFactionPurchases { get; set; }
        public int P4ChosenFactionPurchases { get; set; }

        public int RedPermanentSellbackBonusPct { get; set; }
        public int BluePermanentSellbackBonusPct { get; set; }
        public int RedBFTSurcharge { get; set; }
        public int BlueBFTSurcharge { get; set; }

        public int P1NextIncomeDiscountPct { get; set; }
        public int P2NextIncomeDiscountPct { get; set; }
        public int P3NextIncomeDiscountPct { get; set; }
        public int P4NextIncomeDiscountPct { get; set; }

        public int P1NextSellBonusPct { get; set; }
        public int P2NextSellBonusPct { get; set; }
        public int P3NextSellBonusPct { get; set; }
        public int P4NextSellBonusPct { get; set; }

        public int P1NextFactionDiscountPct { get; set; }
        public int P2NextFactionDiscountPct { get; set; }
        public int P3NextFactionDiscountPct { get; set; }
        public int P4NextFactionDiscountPct { get; set; }

        public int P1NextChosenFactionDiscountPct { get; set; }
        public int P2NextChosenFactionDiscountPct { get; set; }
        public int P3NextChosenFactionDiscountPct { get; set; }
        public int P4NextChosenFactionDiscountPct { get; set; }

        public int P1NextPermMoveDiscountPct { get; set; }
        public int P2NextPermMoveDiscountPct { get; set; }
        public int P3NextPermMoveDiscountPct { get; set; }
        public int P4NextPermMoveDiscountPct { get; set; }

        public bool P1PermMoveCapUnlocked { get; set; }
        public bool P2PermMoveCapUnlocked { get; set; }
        public bool P3PermMoveCapUnlocked { get; set; }
        public bool P4PermMoveCapUnlocked { get; set; }

        public bool Milestone5Claimed { get; set; }
        public bool Milestone10Claimed { get; set; }
        public bool Milestone15Claimed { get; set; }
        public bool Milestone20Claimed { get; set; }
        public bool Milestone25Claimed { get; set; }

        public string P1Name { get; set; }
        public string P2Name { get; set; }
        public string P3Name { get; set; }
        public string P4Name { get; set; }

        public string P1LastCalcText { get; set; }
        public string P2LastCalcText { get; set; }
        public string P3LastCalcText { get; set; }
        public string P4LastCalcText { get; set; }

        public List<string> ActionLog { get; set; }
    }



    // ─────────────────────────────────────────────────────────────────────
    //  Save name prompt window
    // ─────────────────────────────────────────────────────────────────────
    public class GoldPopOutWindow : Window
    {
        private Border _outerBorder;
        private TextBlock _goldText;
        private TextBlock _nameText;
        private WrapPanel _factionPanel;
        private ColumnDefinition _factionColumn;
        private Button _lockButton;
        private bool _isLocked = false;

        private const double BasePopOutWidth = 244;
        private const double PopOutLeftWidth = 108;
        private const double FactionIconSlot = 33;

        public GoldPopOutWindow(string playerName, int gold, int state, Action onClosed)
            : this(playerName, gold, state, null, null, onClosed)
        {
        }

        public GoldPopOutWindow(string playerName, int gold, int state, List<string> factions, Dictionary<string, string> iconMap, Action onClosed)
        {
            Width = BasePopOutWidth;
            Height = 100;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Topmost = true;
            Background = Brushes.Transparent;
            ResizeMode = ResizeMode.NoResize;
            WindowStartupLocation = WindowStartupLocation.CenterScreen;

            _outerBorder = new Border
            {
                Background = GetStateBrush(state),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 70)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(10)
            };

            var root = new Grid();
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(8, 5, 6, 5),
                Cursor = Cursors.SizeAll
            };

            titleBar.MouseDown += (s, e) =>
            {
                if (!_isLocked && e.ChangedButton == MouseButton.Left)
                    DragMove();
            };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _nameText = new TextBlock
            {
                Text = playerName,
                Foreground = Brushes.White,
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis
            };
            Grid.SetColumn(_nameText, 0);

            _lockButton = new Button
            {
                Content = "🔓",
                Width = 20,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 11,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                ToolTip = "Lock pop-out position"
            };
            _lockButton.Click += (s, e) =>
            {
                _isLocked = !_isLocked;
                _lockButton.Content = _isLocked ? "🔒" : "🔓";
                titleBar.Cursor = _isLocked ? Cursors.Arrow : Cursors.SizeAll;
            };
            Grid.SetColumn(_lockButton, 1);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 18,
                Height = 18,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = Brushes.White,
                FontSize = 9,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center,
                Margin = new Thickness(4, 0, 0, 0)
            };
            closeBtn.Click += (s, e) => Close();
            Grid.SetColumn(closeBtn, 2);

            titleGrid.Children.Add(_nameText);
            titleGrid.Children.Add(_lockButton);
            titleGrid.Children.Add(closeBtn);
            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            var content = new Grid { Margin = new Thickness(10, 4, 8, 2) };
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(78) });
            content.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            _factionColumn = new ColumnDefinition { Width = new GridLength(112) };
            content.ColumnDefinitions.Add(_factionColumn);

            var goldStack = new StackPanel();
            goldStack.Children.Add(new TextBlock
            {
                Text = "GOLD",
                Foreground = new SolidColorBrush(Color.FromRgb(242, 244, 247)),
                FontSize = 10,
                FontWeight = FontWeights.SemiBold
            });

            _goldText = new TextBlock
            {
                Text = gold.ToString(),
                Foreground = Brushes.White,
                FontSize = 27,
                FontWeight = FontWeights.Bold
            };

            goldStack.Children.Add(_goldText);
            Grid.SetColumn(goldStack, 0);
            content.Children.Add(goldStack);

            var divider = new Border
            {
                Width = 1,
                Background = new SolidColorBrush(Color.FromRgb(210, 216, 224)),
                Opacity = 0.45,
                Margin = new Thickness(4, 0, 7, 0)
            };
            Grid.SetColumn(divider, 1);
            content.Children.Add(divider);

            _factionPanel = new WrapPanel
            {
                Width = 112,
                HorizontalAlignment = HorizontalAlignment.Left,
                VerticalAlignment = VerticalAlignment.Top
            };

            Grid.SetColumn(_factionPanel, 2);
            content.Children.Add(_factionPanel);

            Grid.SetRow(content, 1);
            root.Children.Add(content);

            _outerBorder.Child = root;
            Content = _outerBorder;

            RefreshFactions(factions, iconMap);
            Closed += (s, e) => onClosed?.Invoke();
        }

        public void UpdateGold(int gold, string playerName, int state, List<string> factions = null, Dictionary<string, string> iconMap = null)
        {
            _goldText.Text = gold.ToString();
            _nameText.Text = playerName;
            _outerBorder.Background = GetStateBrush(state);
            RefreshFactions(factions, iconMap);
        }

        private void RefreshFactions(List<string> factions, Dictionary<string, string> iconMap)
        {
            if (_factionPanel == null) return;

            _factionPanel.Children.Clear();

            int count = factions?.Count ?? 0;

            if (count == 0 || iconMap == null)
            {
                if (_factionColumn != null)
                    _factionColumn.Width = new GridLength(112);

                _factionPanel.Width = 106;
                Width = BasePopOutWidth;
                return;
            }

            int columns = Math.Max(3, (int)Math.Ceiling(count / 2.0));
            double factionWidth = Math.Max(106, columns * FactionIconSlot);

            if (_factionColumn != null)
                _factionColumn.Width = new GridLength(factionWidth + 6);

            _factionPanel.Width = factionWidth;
            Width = Math.Max(BasePopOutWidth, PopOutLeftWidth + factionWidth);

            foreach (var faction in factions)
                _factionPanel.Children.Add(BuildFactionIcon(faction, iconMap));
        }

        private FrameworkElement BuildFactionIcon(string faction, Dictionary<string, string> iconMap)
        {
            var border = new Border
            {
                Width = 28,
                Height = 28,
                CornerRadius = new CornerRadius(6),
                Margin = new Thickness(0, 0, 5, 2),
                Background = new SolidColorBrush(Color.FromRgb(26, 28, 31)),
                ClipToBounds = true
            };

            try
            {
                string file = iconMap.ContainsKey(faction) ? iconMap[faction] : null;
                if (!string.IsNullOrWhiteSpace(file))
                {
                    var bitmap = new System.Windows.Media.Imaging.BitmapImage();
                    bitmap.BeginInit();
                    bitmap.UriSource = new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute);
                    bitmap.CacheOption = System.Windows.Media.Imaging.BitmapCacheOption.OnLoad;
                    bitmap.EndInit();

                    ImageSource source = bitmap;
                    double scale = 1.25;

                    if (string.Equals(faction, "New Units", StringComparison.OrdinalIgnoreCase))
                    {
                        int cropX = (int)(bitmap.PixelWidth * 0.18);
                        int cropWidth = bitmap.PixelWidth - cropX;
                        int cropHeight = Math.Max(1, (int)(bitmap.PixelHeight * 0.60));

                        source = new System.Windows.Media.Imaging.CroppedBitmap(
                            bitmap,
                            new Int32Rect(cropX, 0, cropWidth, cropHeight));

                        scale = 0.77;
                    }
                    else if (string.Equals(faction, "New Units 2", StringComparison.OrdinalIgnoreCase))
                    {
                        int cropX = (int)(bitmap.PixelWidth * 0.32);
                        int cropWidth = bitmap.PixelWidth - cropX;
                        int cropHeight = Math.Max(1, (int)(bitmap.PixelHeight * 0.60));

                        source = new System.Windows.Media.Imaging.CroppedBitmap(
                            bitmap,
                            new Int32Rect(cropX, 0, cropWidth, cropHeight));

                        scale = 0.80;
                    }

                    border.Child = new Image
                    {
                        Stretch = Stretch.UniformToFill,
                        Source = source,
                        RenderTransform = new ScaleTransform(scale, scale),
                        RenderTransformOrigin = new Point(0.5, 0.5)
                    };
                }
            }
            catch { }

            if (border.Child == null)
            {
                border.Child = new TextBlock
                {
                    Text = string.IsNullOrWhiteSpace(faction) ? "?" : faction.Substring(0, 1),
                    Foreground = Brushes.White,
                    HorizontalAlignment = HorizontalAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center,
                    FontWeight = FontWeights.Bold,
                    FontSize = 12
                };
            }

            return border;
        }

        private static SolidColorBrush GetStateBrush(int state)
        {
            if (state == 1)
                return new SolidColorBrush(Color.FromRgb(40, 110, 60));
            if (state == -1)
                return new SolidColorBrush(Color.FromRgb(120, 40, 40));

            return new SolidColorBrush(Color.FromRgb(35, 39, 47));
        }
    }

    public class ThemedConfirmDialog : Window
    {
        public bool Confirmed { get; private set; }

        public ThemedConfirmDialog(string title, string message)
        {
            Title = title;
            Width = 400;
            Height = 210;
            MinWidth = 400;
            MinHeight = 210;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromRgb(26, 29, 35));

            var outerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 70)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12)
            };

            var root = new Grid { Margin = new Thickness(20) };
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = title,
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleBlock, 0);
            root.Children.Add(titleBlock);

            var msg = new TextBlock
            {
                Text = message,
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                FontSize = 13,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(msg, 1);
            root.Children.Add(msg);

            var buttonRow = new Grid { HorizontalAlignment = HorizontalAlignment.Right };
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnYes = new Button
            {
                Content = TwoVTwoGameMode.Loc.Get("Yes"),
                Width = 92,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(40, 90, 52)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 0, 6)
            };
            btnYes.Click += (s, e) => { Confirmed = true; DialogResult = true; };
            Grid.SetColumn(btnYes, 0);
            buttonRow.Children.Add(btnYes);

            var btnNo = new Button
            {
                Content = TwoVTwoGameMode.Loc.Get("No"),
                Width = 92,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(96, 48, 48)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 0, 6)
            };
            btnNo.Click += (s, e) => { Confirmed = false; DialogResult = false; };
            Grid.SetColumn(btnNo, 2);
            buttonRow.Children.Add(btnNo);

            Grid.SetRow(buttonRow, 3);
            root.Children.Add(buttonRow);

            outerBorder.Child = root;
            Content = outerBorder;

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter) { Confirmed = true; DialogResult = true; e.Handled = true; }
                if (e.Key == Key.Escape) { Confirmed = false; DialogResult = false; e.Handled = true; }
            };
        }
    }

    public class SaveNamePromptWindow : Window
    {
        public string SaveName { get; private set; }
        private TextBox _box;

        public SaveNamePromptWindow()
        {
            Title = TwoVTwoGameMode.Loc.Get("SaveDialogTitle");
            Width = 380;
            Height = 230;
            MinWidth = 380;
            MinHeight = 230;
            WindowStartupLocation = WindowStartupLocation.CenterOwner;
            ResizeMode = ResizeMode.NoResize;
            WindowStyle = WindowStyle.None;
            AllowsTransparency = true;
            Background = new SolidColorBrush(Color.FromRgb(26, 29, 35));

            var outerBorder = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 70)),
                BorderThickness = new Thickness(1.5),
                CornerRadius = new CornerRadius(12)
            };

            var root = new Grid
            {
                Margin = new Thickness(20)
            };

            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
            root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

            var titleBlock = new TextBlock
            {
                Text = TwoVTwoGameMode.Loc.Get("SaveDialogTitle"),
                Foreground = Brushes.White,
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 12)
            };
            Grid.SetRow(titleBlock, 0);
            root.Children.Add(titleBlock);

            var label = new TextBlock
            {
                Text = TwoVTwoGameMode.Loc.Get("EnterSaveName"),
                Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                Margin = new Thickness(0, 0, 0, 10),
                FontSize = 13,
                FontWeight = FontWeights.SemiBold
            };
            Grid.SetRow(label, 1);
            root.Children.Add(label);

            _box = new TextBox
            {
                Background = new SolidColorBrush(Color.FromRgb(36, 40, 50)),
                Foreground = Brushes.White,
                CaretBrush = Brushes.White,
                BorderBrush = new SolidColorBrush(Color.FromRgb(60, 68, 82)),
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8, 6, 8, 6),
                Height = 34,
                Margin = new Thickness(0, 0, 0, 16)
            };
            Grid.SetRow(_box, 2);
            root.Children.Add(_box);

            var buttonRow = new Grid
            {
                HorizontalAlignment = HorizontalAlignment.Right
            };
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(8) });
            buttonRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            var btnOk = new Button
            {
                Content = TwoVTwoGameMode.Loc.Get("SaveBtn"),
                Width = 92,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(40, 90, 52)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 0, 6)
            };
            btnOk.Click += (s, e) =>
            {
                string name = _box.Text.Trim();
                if (string.IsNullOrEmpty(name))
                    return;

                SaveName = name;
                DialogResult = true;
            };
            Grid.SetColumn(btnOk, 0);
            buttonRow.Children.Add(btnOk);

            var btnCancel = new Button
            {
                Content = TwoVTwoGameMode.Loc.Get("Cancel"),
                Width = 92,
                Height = 34,
                Background = new SolidColorBrush(Color.FromRgb(96, 48, 48)),
                Foreground = Brushes.White,
                FontWeight = FontWeights.SemiBold,
                BorderThickness = new Thickness(0),
                Padding = new Thickness(0, 6, 0, 6)
            };
            btnCancel.Click += (s, e) => { DialogResult = false; };
            Grid.SetColumn(btnCancel, 2);
            buttonRow.Children.Add(btnCancel);

            Grid.SetRow(buttonRow, 4);
            root.Children.Add(buttonRow);

            outerBorder.Child = root;
            Content = outerBorder;

            Loaded += (s, e) => _box.Focus();

            KeyDown += (s, e) =>
            {
                if (e.Key == Key.Enter)
                {
                    btnOk.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                    e.Handled = true;
                }

                if (e.Key == Key.Escape)
                {
                    DialogResult = false;
                    e.Handled = true;
                }
            };
        }
    }
}
