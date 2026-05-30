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
        private bool _factionModeEnabled = false;
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

        // ── FT20 mode ─────────────────────────────────────────────────────
        private bool _ft20ModeEnabled = false;
        private bool _ft20ModeLocked = false;
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
            Directory.CreateDirectory(SaveFolder);
            LayoutTransform = new ScaleTransform(1.0, 1.0);

            _noticeTimer = new DispatcherTimer
            { Interval = TimeSpan.FromSeconds(3) };
            _noticeTimer.Tick += (s, e) =>
            {
                IncomeNoticePopup.IsOpen = false;
                _noticeTimer.Stop();
            };

            UpdateLanguageSelectorUI();
            InitNewGame();
            RefreshSavesDropdown();
            RefreshAllUI();
            UpdateAllUI();

            Loaded += (s, e) =>
                ApplyWindowMode(AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen, false);
        }

        private void SetupNumericInputBoxes()
        {
            for (int p = 1; p <= 4; p++)
            {
                RegisterNumericOnly(GetSpendBox(p));
                RegisterNumericOnly(GetBuyTeamBox(p));
                RegisterNumericOnly(GetUnitBox(p));
            }
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
                Content = Loc.Get("BuyChosenFaction", 280),
                Background = new SolidColorBrush(Color.FromRgb(110, 169, 200)),
                FontSize = 11,
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
            bool isSpanish = Loc.Current == Loc.Language.Spanish;

            SettingsLanguageText.Text = isSpanish ? "Español" : "English";

            SettingsLangDot1.Background = !isSpanish
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));

            SettingsLangDot2.Background = isSpanish
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
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
            // Normal mode starts with the shared milestone system active
            BuildSharedMilestonePool();
            _p1GoldState = _p2GoldState = _p3GoldState = _p4GoldState = 0;
            _p1PointsState = _p2PointsState = _p3PointsState = _p4PointsState = 0;
            _p1InterestState = _p2InterestState = _p3InterestState = _p4InterestState = 0;

            _factionModeEnabled = false; _factionModeLocked = false;
            _ft20ModeEnabled = false; _ft20ModeLocked = false;
            _milestoneRewardsRemaining = new List<string>();
            _milestoneNextThreshold = 5;
            _milestoneSystemActive = false;

            int start = 1200;
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

            BuildSharedMilestonePool();
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
            "10% Off Next Income","10% Off Next Income",
            "+30% Next Sell",
            "-5% BFT Surcharge"
        };
            }
            else
            {
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
            _ft20RewardsRemaining = pool;
            _ft20NextMilestone = 4;
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

            FirstTurnPromptBorder.Visibility =
                (!matchStarted && !_firstTurnChosen) ? Visibility.Visible : Visibility.Collapsed;

            UpdateNameEditState();

            // Faction toggle
            FactionModeToggleButton.Content = _factionModeEnabled ? Loc.Get("FactionModeOn") : Loc.Get("FactionModeOff");
            FactionModeToggleButton.Tag = _factionModeEnabled ? "True" : "False";
            FactionModeToggleButton.IsEnabled = !matchStarted;

            // FT20 toggle
            FT20ModeToggleButton.Content = _ft20ModeEnabled ? Loc.Get("FT20ModeOn") : Loc.Get("FT20ModeOff");
            FT20ModeToggleButton.Tag = _ft20ModeEnabled ? "True" : "False";
            FT20ModeToggleButton.IsEnabled = !matchStarted;

            NextRoundButton.IsEnabled = _pendingWinner != 0;
            NextRoundButton.Background = _pendingWinner != 0
                ? new SolidColorBrush(Color.FromRgb(110, 169, 200))
                : new SolidColorBrush(Color.FromRgb(55, 64, 76));

            UndoButton.IsEnabled = _undoStack.Count > 0;
            UndoButton.Background = _undoStack.Count > 0
                ? new SolidColorBrush(Color.FromRgb(142, 108, 245))
                : new SolidColorBrush(Color.FromRgb(55, 64, 76));

            if (_ft20ModeEnabled) RefreshFT20InfoPanel();
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
            _p1Gold = _p2Gold = _p3Gold = _p4Gold = 1200;

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

            _p1GoldWindow?.UpdateGold(_p1Gold, P1NameBox.Text, _p1GoldState);
            _p2GoldWindow?.UpdateGold(_p2Gold, P2NameBox.Text, _p2GoldState);
            _p3GoldWindow?.UpdateGold(_p3Gold, P3NameBox.Text, _p3GoldState);
            _p4GoldWindow?.UpdateGold(_p4Gold, P4NameBox.Text, _p4GoldState);
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
            for (int p = 1; p <= 4; p++)
            {
                int inc = GetIncome(p);
                if (inc > 0) AddGold(p, inc);
            }

            // ── 4. Income decay tracking ───────────────────────────────────
            for (int p = 1; p <= 4; p++) UpdateIncomeDecay(p);
            _p1BoughtIncome = _p2BoughtIncome =
            _p3BoughtIncome = _p4BoughtIncome = false;
            _p1BoughtIncomeThisRound = _p2BoughtIncomeThisRound =
_p3BoughtIncomeThisRound = _p4BoughtIncomeThisRound = false;
            _redReplayBoughtThisRound = _blueReplayBoughtThisRound = false;

            // ── 5. Round reward — pass captured winner, not _pendingWinner ─
            ApplyRoundReward(winner);

            // ── 6. Milestones ──────────────────────────────────────────────
            if (_ft20ModeEnabled)
                CheckFT20Milestones();
            else
                CheckSharedMilestones();

            string winnerName = winner == 1 ? $"🔴 {Loc.Get("LogRedWins")}" : winner == 2 ? $"🔵 {Loc.Get("LogBlueWins")}" : $"🤝 {Loc.Get("Tie")}";
            LogAction(Loc.Get("LogRoundComplete", _round, winnerName, _redPoints, _bluePoints));

            // ── 7. Update turn order, reset pending winner, and advance round ─────────
            UpdateNextTurnOrderAfterRound(winner);

            _pendingWinner = 0;
            _round++;

            RefreshAllUI();
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
            if (_ft20ModeEnabled)
            {
                int tier = (_round - 1) / 3;
                winnerGold = 55 + (tier * 15);
                loserGold = 85 + (tier * 15);
            }
            else
            {
                int tier = (_round - 1) / 5;
                winnerGold = 55 + (tier * 10);
                loserGold = 85 + (tier * 10);
            }
        }

        private int GetTieRewardValue()
        {
            if (_ft20ModeEnabled)
            {
                int tier = (_round - 1) / 3;
                return 70 + (tier * 15);
            }
            else
            {
                int tier = (_round - 1) / 5;
                return 70 + (tier * 10);
            }
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
            if (redHit) { AddGold(1, goldEach); AddGold(2, goldEach); team = "Red"; }
            else { AddGold(3, goldEach); AddGold(4, goldEach); team = "Blue"; }
            LogAction($"Milestone FT{pts}: {team} Team +{goldEach}g each.");
            ShowNotice($"🏆 Milestone FT{pts}! {team} Team +{goldEach}g each.", NoticeType.Milestone);
        }

        private void CheckMilestonePerm(int pts, ref bool claimed)
        {
            if (claimed) return;
            bool redHit = _redPoints >= pts;
            bool blueHit = _bluePoints >= pts;
            if (!redHit && !blueHit) return;
            claimed = true;
            string team;
            if (redHit) { _p1PermMoveUpgrades++; _p2PermMoveUpgrades++; team = "Red"; }
            else { _p3PermMoveUpgrades++; _p4PermMoveUpgrades++; team = "Blue"; }
            LogAction($"Milestone FT{pts}: {team} Team +1 perm move each.");
            ShowNotice($"🏆 Milestone FT{pts}! {team} Team: +1 perm move each.", NoticeType.Milestone);
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
                team = "Red";
            }
            else
            {
                _bluePermanentSellbackBonusPct = Math.Max(_bluePermanentSellbackBonusPct, 20);
                team = "Blue";
            }

            LogAction($"Milestone FT{pts}: {team} Team +20% permanent sellback.");
            ShowNotice($"🏆 Milestone FT{pts}! {team} Team: +20% permanent sellback.", NoticeType.Milestone);
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
            string team = isRed ? "Red" : "Blue";
            LogAction($"🏆 {Loc.Get("LogMilestone", threshold, team, reward)}");
            ShowNotice(Loc.Get("NoticeMilestone", threshold, Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort"), LocalizeReward(reward)), NoticeType.Milestone);
        }

        private void RefreshSharedMilestonePanel()
        {
            int redAway = Math.Max(0, _milestoneNextThreshold - _redPoints);
            int blueAway = Math.Max(0, _milestoneNextThreshold - _bluePoints);
            MilestoneP1Text.Text = $"🔴 {redAway} {Loc.Get("PtsAway")} ({Loc.Get("NextAt")} {_milestoneNextThreshold})";
            MilestoneP2Text.Text = $"🔵 {blueAway} {Loc.Get("PtsAway")} ({Loc.Get("NextAt")} {_milestoneNextThreshold})";

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
                    FontSize = 11,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#E8EDF3")),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 3)
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
            string team = isRed ? "Red" : "Blue";
            LogAction($"🏆 {Loc.Get("LogFT20Milestone", _ft20NextMilestone, team, reward)}");
            ShowNotice(Loc.Get("NoticeFT20Milestone", _ft20NextMilestone, Loc.Get(isRed ? "RedTeamShort" : "BlueTeamShort"), LocalizeReward(reward)), NoticeType.Milestone);
            _ft20NextMilestone += 4;
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
                    decay = missed >= 4 ? Math.Min(100, (missed - 3) * 4) : 0;
                }
                else
                {
                    // Normal / Faction: grace = 4 rounds, then 2% off per round after
                    // missed 1,2,3,4 = 0%, missed 5 = 2%, missed 6 = 4%, etc.
                    decay = missed >= 5 ? Math.Min(100, (missed - 4) * 2) : 0;
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
            int baseCost = _ft20ModeEnabled ? 175 : 200;
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

            LogAction($"🏃 {Loc.Get("LogBoughtPermMove", player, finalCost, discountPct > 0 ? $" ({discountPct}% off)" : "", GetPermMoveUpgrades(player))}");
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

            int baseCost = 50 + purchases * 20;
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

            int nextCost = 50 + (purchases + 1) * 20;
            LogAction($"⚔️ {Loc.Get("LogBoughtFaction", player, newFaction, cost, discountPct > 0 ? $" ({discountPct}% off)" : "", nextCost)}");
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
        private void P1SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(1, 25, "troop move");
        private void P2SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(2, 25, "troop move");
        private void P3SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(3, 25, "troop move");
        private void P4SingleTroopMove_Click(object s, RoutedEventArgs e) => SpendFixed(4, 25, "troop move");

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
            SetCostButtonVisual(P1SingleTroopMoveButton, 1, 25);
            SetCostButtonVisual(P2SingleTroopMoveButton, 2, 25);
            SetCostButtonVisual(P3SingleTroopMoveButton, 3, 25);
            SetCostButtonVisual(P4SingleTroopMoveButton, 4, 25);
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

            P1ReplayButton.Content = redCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed");
            P2ReplayButton.Content = redCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed");
            P3ReplayButton.Content = blueCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed");
            P4ReplayButton.Content = blueCanReplay ? Loc.Get("Replay") : Loc.Get("ReplayUsed");

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
            string raw = box.Text.Trim();
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
            string raw = box.Text.Trim();
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
            string raw = box.Text.Trim();
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
            RefreshAllUI();
        }

        // ─────────────────────────────────────────────────────────────────
        //  ClearInputBox — just empties the box, no placeholder needed
        // ─────────────────────────────────────────────────────────────────
        private void ClearInputBox(TextBox box)
        {
            box.Text = "";
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
            int income = GetIncome(player);
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
            _p1Factions = DrawRandomFactions(2); _p2Factions = DrawRandomFactions(2);
            _p3Factions = DrawRandomFactions(2); _p4Factions = DrawRandomFactions(2);
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
            int incomeGain = _ft20ModeEnabled ? 13 : 10;

            UpdateIncomeDiscountBadge(1, P1BuyIncomeButton, P1IncomeDecayPctText, P1IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(2, P2BuyIncomeButton, P2IncomeDecayPctText, P2IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(3, P3BuyIncomeButton, P3IncomeDecayPctText, P3IncomeBadgeBorder, incomeGain);
            UpdateIncomeDiscountBadge(4, P4BuyIncomeButton, P4IncomeDecayPctText, P4IncomeBadgeBorder, incomeGain);
        }

        private void UpdateIncomeDiscountBadge(int player, Button button, TextBlock badgeText, Border badgeBorder, int incomeGain)
        {
            int shownCost = GetDisplayedIncomeCost(player);
            int totalDiscountPct = GetIncomeDecayPct(player) + GetNextIncomeDiscountPct(player);

            bool canBuy = !GetBoughtIncomeThisRound(player) && GetGold(player) >= shownCost;

            button.Content = $"{Loc.Get(_ft20ModeEnabled ? "BuyIncomeF" : "BuyIncome").Split('(')[0].Trim()} ({shownCost}g)";
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
            int baseCost = _ft20ModeEnabled ? 175 : 200;
            int discountPct = GetNextPermMoveDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            bool canBuy = purchases < max && GetGold(player) >= shownCost;

            button.Content = $"{Loc.Get(_ft20ModeEnabled ? "BuyPermMoveF" : "BuyPermMove").Split('(')[0].Trim()} ({shownCost}g) [{purchases}/{max}]";
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
            int baseCost = _ft20ModeEnabled ? 175 : 200;
            int discountPct = GetNextPermMoveDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            button.Content = $"Buy perm move +1 ({shownCost}g) [{purchases}/{max}]";
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
                button.Content = Loc.Get("MaxFactions");
                button.IsEnabled = true;
                button.Background = new SolidColorBrush(Color.FromRgb(75, 85, 99));
                badgeBorder.Visibility = Visibility.Collapsed;
                return;
            }

            int baseCost = 50 + purchases * 20;
            int discountPct = GetNextFactionDiscountPct(player);
            int shownCost = (int)Math.Ceiling(Math.Max(1m, baseCost * (1m - discountPct / 100m)));

            bool canBuy = GetGold(player) >= shownCost;

            button.Content = $"{Loc.Get("BuyFaction").Split('(')[0].Trim()} ({shownCost}g)";
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

            button.Content = Loc.Get("BuyChosenFaction", cost);
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
            MilestoneP1Text.Text = $"🔴 {redAway} {Loc.Get("PtsAway")} ({Loc.Get("NextAt")} {_ft20NextMilestone})";
            MilestoneP2Text.Text = $"🔵 {blueAway} {Loc.Get("PtsAway")} ({Loc.Get("NextAt")} {_ft20NextMilestone})";

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
                    FontSize = 11,
                    Foreground = new SolidColorBrush(
                        (Color)ColorConverter.ConvertFromString("#E8EDF3")),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 3)
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
            _factionModeEnabled = !_factionModeEnabled;
            _firstTurnChosen = false;
            TurnOrderText.Text = Loc.Get("NotAvailableYet");

            if (_factionModeEnabled)
            {
                ResetAllPlayerPanelsForModeSwap();
                AssignRandomFactions();
                if (!_ft20ModeEnabled) BuildSharedMilestonePool();
                LogAction($"⚙️ {Loc.Get("LogFactionModeOn")}");
                ShowNotice(Loc.Get("NoticeFactionModeOn"), NoticeType.Info);
            }
            else
            {
                if (_ft20ModeEnabled)
                {
                    _ft20ModeEnabled = false;
                    _ft20RewardsRemaining = new List<string>();
                    _ft20NextMilestone = 4;
                }
                // Do NOT wipe the milestone pool — normal mode resumes with whatever is left
                // Only rebuild if the pool was faction-specific (has faction rewards in it)
                bool poolHasFactionRewards = _milestoneRewardsRemaining
                    .Any(r => r == "80% Off Next Faction");
                if (poolHasFactionRewards || !_milestoneSystemActive)
                {
                    // Rebuild as normal mode pool (no faction rewards)
                    BuildSharedMilestonePool();
                }
                ResetAllPlayerPanelsForModeSwap();
                _p1Factions.Clear(); _p2Factions.Clear();
                _p3Factions.Clear(); _p4Factions.Clear();
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
            _ft20ModeEnabled = !_ft20ModeEnabled;
            _firstTurnChosen = false;
            TurnOrderText.Text = Loc.Get("NotAvailableYet");
            if (_ft20ModeEnabled)
            {
                ResetAllPlayerPanelsForModeSwap();
                _milestoneRewardsRemaining = new List<string>();
                _milestoneNextThreshold = 5;
                _milestoneSystemActive = false;
                BuildFT20RewardPool();
                if (_factionModeEnabled) AssignRandomFactions();
                LogAction(Loc.Get("LogFT20ModeOn"));
                ShowNotice(Loc.Get("NoticeFT20ModeOn"), NoticeType.Info);
            }
            else
            {
                _ft20RewardsRemaining = new List<string>();
                _ft20NextMilestone = 4;
                ResetAllPlayerPanelsForModeSwap();
                if (_factionModeEnabled) AssignRandomFactions();
                if (_milestoneRewardsRemaining.Count == 0)
                    BuildSharedMilestonePool();
                else
                    _milestoneSystemActive = true;
                LogAction($"⚙️ {Loc.Get("LogFT20ModeOff")}");
                ShowNotice(Loc.Get("NoticeFT20ModeOff"), NoticeType.Info);
            }
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

            var window = new GoldPopOutWindow(GetPlayerName(player), GetGold(player), GetGoldState(player), () =>
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
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = body,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
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
                FontSize = 15,
                FontWeight = FontWeights.Bold,
                Margin = new Thickness(0, 0, 0, 8)
            });

            var text = new TextBlock
            {
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                FontSize = 12,
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 18
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
            ApplyLanguage(Loc.Language.English);
        }

        private void SettingsLanguageRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(Loc.Language.Spanish);
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

            // Overview static labels
            OverviewTitle.Text = Loc.Get("OverviewTitle");
            OverviewSub.Text = Loc.Get("OverviewSub");
            LblCurrentRound.Text = Loc.Get("CurrentRound");
            LblNextTurnOrder.Text = Loc.Get("NextTurnOrder");
            LblPendingResult.Text = Loc.Get("PendingResult");
            LblFactionMode.Text = Loc.Get("FactionMode");
            LblFT20Mode.Text = Loc.Get("FT20Mode");
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

            // Faction/FT20 toggles
            bool factionOn = FactionModeToggleButton.Tag?.ToString() == "True";
            bool ft20On = FT20ModeToggleButton.Tag?.ToString() == "True";
            FactionModeToggleButton.Content = factionOn ? Loc.Get("FactionModeOn") : Loc.Get("FactionModeOff");
            FT20ModeToggleButton.Content = ft20On ? Loc.Get("FT20ModeOn") : Loc.Get("FT20ModeOff");

            // Per-player buttons (all 4 players)
            foreach (int p in new[] { 1, 2, 3, 4 })
            {
                bool isFT20 = ft20On;
                GetBuyIncomeButton(p).Content = Loc.Get(isFT20 ? "BuyIncomeF" : "BuyIncome");
                GetBuyPermMoveButton(p).Content = Loc.Get(isFT20 ? "BuyPermMoveF" : "BuyPermMove")
                                                    + $" [{GetPermMovePurchases(p)}/{GetPermMoveMaxPurchases(p)}]";
                GetBuyFactionButton(p).Content = Loc.Get("BuyFaction");
                GetSingleTroopMoveButton(p).Content = Loc.Get("SingleTroopMove");
                GetReplayButton(p).Content = Loc.Get("Replay");
                GetSpendButton(p).Content = Loc.Get("Spend");
                GetBuyTeamButton(p).Content = Loc.Get("BFT");
                GetSellUnitButton(p).Content = Loc.Get("Sell");
                GetNameEditButton(p).Content = GetNameBox(p).IsReadOnly ? Loc.Get("Unset") : Loc.Get("Set");
            }

            // Team points bar
            LblRedTeamPoints.Text = Loc.Get("RedTeamPoints");
            LblBlueTeamPoints.Text = Loc.Get("BlueTeamPoints");

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
                if (spendBox.Text == "Custom troop spend" || spendBox.Text == "Gasto personalizado de tropas")
                    spendBox.Text = Loc.Get("CustomTroopSpend");
                if (buyTeamBox.Text == "Teammate unit cost" || buyTeamBox.Text == "Costo de unidad compañero")
                    buyTeamBox.Text = Loc.Get("TeammateUnitCost");
                if (unitBox.Text == "Unit value" || unitBox.Text == "Valor de unidad")
                    unitBox.Text = Loc.Get("UnitValue");
            }

            // Refresh dynamic state text on language change
            if (!_firstTurnChosen)
                TurnOrderText.Text = Loc.Get("NotAvailableYet");

            PendingResultText.Text = _pendingWinner == 1 ? Loc.Get("RedTeamWins")
                : _pendingWinner == 2 ? Loc.Get("BlueTeamWins")
                : _pendingWinner == 3 ? Loc.Get("Tie")
                : Loc.Get("NotSet");

            if (_ft20ModeEnabled) RefreshFT20InfoPanel();
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
            if (Keyboard.Modifiers != ModifierKeys.Control) return;
            var st = LayoutTransform as ScaleTransform;
            if (st == null) return;
            double ns = Math.Max(MinZoom, Math.Min(MaxZoom,
                st.ScaleX + (e.Delta > 0 ? ZoomStep : -ZoomStep)));
            st.ScaleX = st.ScaleY = ns;
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

            bool wasFaction = _factionModeEnabled;
            bool wasFT20 = _ft20ModeEnabled;

            InitNewGame();

            // Restore mode states so new game stays in the same mode
            _factionModeEnabled = wasFaction;
            _ft20ModeEnabled = wasFT20;

            if (_ft20ModeEnabled)
            {
                _milestoneRewardsRemaining = new List<string>();
                _milestoneNextThreshold = 5;
                _milestoneSystemActive = false;
                BuildFT20RewardPool();
                if (_factionModeEnabled) AssignRandomFactions();
            }
            else if (_factionModeEnabled)
            {
                AssignRandomFactions();
                BuildSharedMilestonePool();
            }
            else
            {
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
            {
                string text = box.Text?.Trim() ?? "";
                if (text == "Custom troop spend" || text == "Gasto personalizado de tropas" ||
                    text == "Teammate unit cost" || text == "Costo de unidad compañero" ||
                    text == "Unit value" || text == "Valor de unidad")
                {
                    box.Text = "";
                }
            }
        }

        private void PlaceholderBox_LostFocus(object sender, RoutedEventArgs e)
        {
            if (sender is TextBox box)
            {
                if (!string.IsNullOrWhiteSpace(box.Text))
                    return;

                if (ReferenceEquals(box, P1SpendBox) || ReferenceEquals(box, P2SpendBox) ||
    ReferenceEquals(box, P3SpendBox) || ReferenceEquals(box, P4SpendBox))
                {
                    box.Text = Loc.Get("CustomTroopSpend");
                }
                else if (ReferenceEquals(box, P1BuyTeamBox) || ReferenceEquals(box, P2BuyTeamBox) ||
                         ReferenceEquals(box, P3BuyTeamBox) || ReferenceEquals(box, P4BuyTeamBox))
                {
                    box.Text = Loc.Get("TeammateUnitCost");
                }
                else if (ReferenceEquals(box, P1UnitBox) || ReferenceEquals(box, P2UnitBox) ||
                         ReferenceEquals(box, P3UnitBox) || ReferenceEquals(box, P4UnitBox))
                {
                    box.Text = Loc.Get("UnitValue");
                }
            }
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
            public enum Language { English, Spanish }
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
                    Current = lang == "Spanish" ? Language.Spanish : Language.English;
                }
                catch { }
            }

            private static readonly Dictionary<string, string> _es = new Dictionary<string, string>
            {
                // Top bar
                ["MainMenu"] = "← Menú Principal",
                ["AppTitle"] = "TABS Arena v.1.1.0",

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
                ["SingleTroopMove"] = "Mover tropa individual (25)",
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
                ["GuideEconomyBody"] = "El interés da +10 de oro por cada 50 de oro que tenga un jugador, con máximo de +100. Comprar ingreso aumenta el ingreso permanente: +10 en modos normales y +13 en modos FT20. Solo puede comprarse una vez por ronda. Si no compras ingreso por varias rondas, aparece descuento por decaimiento de ingreso.",
                ["GuideRulesTitle"] = "Reglas 2v2",
                ["GuideRulesBody"] = "No se permite controlar unidades durante la batalla. En mapas 2v2, no coloques unidades en highground, en el círculo central, en grietas, ni en sus entradas. Deben ser 2 ejércitos por lado, 1 ejército por jugador, 4 ejércitos total. Unidades prohibidas actualmente: Present Elf y Dragon.",
                ["GuideSavingTitle"] = "Guardado",
                ["GuideSavingBody"] = "Si no pueden terminar la partida, guarda en la app. También guarda la batalla dentro de TABS usando Save Battle y activa Save Friendly Units.",
                ["Back"] = "← Volver",
                ["WindowMode"] = "Modo de Ventana",
                ["Windowed"] = "Ventana",
                ["BorderlessFullscreen"] = "Pantalla Completa Sin Bordes",
                ["Language"] = "Idioma",
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
                ["LogFactionModeOn"] = "Modo Facción ON — paneles reiniciados, 1200g inicio, 2 facciones aleatorias.",
                ["LogFactionModeOff"] = "Modo Facción OFF.",
                ["LogFT20ModeOn"] = "Modo FT20 ON — paneles reiniciados.",
                ["LogFT20ModeOff"] = "Modo FT20 OFF — paneles reiniciados.",
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
                ["Reward80OffFaction"] = "80% Desc. Próxima Facción",
                ["Reward80OffChosenFaction"] = "80% Desc. Próxima Facción Elegida",
                ["Reward80OffPermMove"] = "80% Desc. Próximo Mv Perm",
                ["Reward10OffIncome"] = "10% Desc. Próximo Ingreso",
                ["Reward30NextSell"] = "+30% Próxima Venta",
                ["RewardSellback15"] = "Reventa +15%",
                ["RewardMinus5BFT"] = "-5% Recargo BFT",
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

            public static string Get(string key, params object[] args)
            {
                string template;
                if (Current == Language.Spanish && _es.TryGetValue(key, out var val)) template = val;
                else if (_defaults.TryGetValue(key, out var def)) template = def;
                else template = key;
                return args.Length > 0 ? string.Format(template, args) : template;
            }

            public static string CurrentLanguage { get; set; } = "en";

            private static readonly Dictionary<string, string> _defaults = new Dictionary<string, string>
            {
                ["MainMenu"] = "← Main Menu",
                ["AppTitle"] = "TABS Arena v.1.1.0",
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
                ["SingleTroopMove"] = "Single troop move (25)",
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
                ["GuideEconomyBody"] = "Interest gives +10 gold for every 50 gold a player has, capped at +100. Buying income increases permanent income: +10 in normal modes and +13 in FT20 modes. It can only be bought once per round. If income is not bought for several rounds, income decay gives a growing discount.",
                ["GuideRulesTitle"] = "2v2 Rules",
                ["GuideRulesBody"] = "Players may not control units during battle. On 2v2 maps, do not place units on high ground, in the middle circle, in crevices, or at entrances to the circle or crevices. There should be 2 armies per side, 1 army per player, 4 armies total. Currently banned units: Present Elf and Dragon.",
                ["GuideSavingTitle"] = "Saving",
                ["GuideSavingBody"] = "If you cannot finish a match, save in the app. Also save the battle inside TABS using Save Battle and enable Save Friendly Units.",
                ["Back"] = "← Back",
                ["WindowMode"] = "Window Mode",
                ["Windowed"] = "Windowed",
                ["BorderlessFullscreen"] = "Borderless Fullscreen",
                ["Language"] = "Language",
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
                ["LogFactionModeOn"] = "Faction Mode ON — panels reset, 1200g start, 2 random factions.",
                ["LogFactionModeOff"] = "Faction Mode OFF.",
                ["LogFT20ModeOn"] = "FT20 Mode ON — panels reset.",
                ["LogFT20ModeOff"] = "FT20 Mode OFF — panels reset.",
                ["LogMilestone"] = "Milestone {0}pts — {1}: {2}",
                ["LogFT20Milestone"] = "FT20 Milestone {0}pts — {1}: {2}",
                ["LogLoaded"] = "Loaded {0}.",
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
                ["GuideMoreTitle"] = "More Rules",
                ["GuideMoreBody"] = "To learn more about the rules, visit",
                ["CloseGameConfirmTitle"] = "Close Game",
                ["CloseGameConfirmMsg"] = "Are you sure you want to close the game?",
            };
        }
        private TwoV2SaveData BuildSaveData(string name) => new TwoV2SaveData
        {
            SaveVersion = 6,
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
            FT20ModeLocked = _ft20ModeLocked,
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
            _ft20ModeEnabled = d.FT20ModeEnabled;
            _ft20ModeLocked = d.FT20ModeLocked;
            _ft20NextMilestone = d.FT20NextMilestone;
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
            return 280 + (GetFactionPurchases(p) * 20);
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
        public bool FT20ModeLocked { get; set; }
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

        public GoldPopOutWindow(string playerName, int gold, int state, Action onClosed)
        {
            Width = 192;
            Height = 114;
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

            // Title bar
            var titleBar = new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                CornerRadius = new CornerRadius(10, 10, 0, 0),
                Padding = new Thickness(8, 5, 6, 5),
                Cursor = Cursors.SizeAll
            };
            titleBar.MouseDown += (s, e) =>
            { if (e.ChangedButton == MouseButton.Left) DragMove(); };

            var titleGrid = new Grid();
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(1, GridUnitType.Star) });
            titleGrid.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });

            _nameText = new TextBlock
            {
                Text = playerName,
                Foreground = new SolidColorBrush(Color.FromRgb(154, 163, 175)),
                FontSize = 12,
                FontWeight = FontWeights.Bold,
                VerticalAlignment = VerticalAlignment.Center
            };
            Grid.SetColumn(_nameText, 0);

            var closeBtn = new Button
            {
                Content = "✕",
                Width = 16,
                Height = 16,
                Background = Brushes.Transparent,
                BorderThickness = new Thickness(0),
                Foreground = new SolidColorBrush(Color.FromRgb(154, 163, 175)),
                FontSize = 9,
                Cursor = Cursors.Hand,
                Padding = new Thickness(0),
                VerticalAlignment = VerticalAlignment.Center
            };
            closeBtn.Click += (s, e) => Close();
            Grid.SetColumn(closeBtn, 1);

            titleGrid.Children.Add(_nameText);
            titleGrid.Children.Add(closeBtn);
            titleBar.Child = titleGrid;
            Grid.SetRow(titleBar, 0);
            root.Children.Add(titleBar);

            // Gold content
            var content = new Border { Padding = new Thickness(10, 6, 10, 8) };
            var stack = new StackPanel();
            stack.Children.Add(new TextBlock
            {
                Text = "GOLD",
                Foreground = new SolidColorBrush(Color.FromRgb(154, 163, 175)),
                FontSize = 10
            });
            _goldText = new TextBlock
            {
                Text = gold.ToString(),
                Foreground = Brushes.White,
                FontSize = 27,
                FontWeight = FontWeights.Bold
            };
            stack.Children.Add(_goldText);
            content.Child = stack;
            Grid.SetRow(content, 1);
            root.Children.Add(content);

            _outerBorder.Child = root;
            Content = _outerBorder;

            Closed += (s, e) => onClosed?.Invoke();
        }

        public void UpdateGold(int gold, string playerName, int state)
        {
            _goldText.Text = gold.ToString();
            _nameText.Text = playerName;
            _outerBorder.Background = GetStateBrush(state);
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