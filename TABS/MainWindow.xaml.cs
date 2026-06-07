using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Threading;

namespace TABS
{
    public partial class MainWindow : Window
    {
        private bool _isTitleBarDragging = false;
        private Point _titleBarDragMouseStart;
        private Point _titleBarDragWindowStart;
        private bool _isWindowedMaximized = false;
        private bool _isBorderlessFullscreen = true;
        private const double ZoomStep = 0.05;
        private const double MinZoom = 0.5;
        private const double MaxZoom = 2.0;
        private const int TieTimerStartSeconds = 120;
        private static readonly string SaveFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TABS", "Saves1v1");

        private string _currentSaveName = null;

        private int round = 1;
        private int pendingWinner = 0;
        private bool namesLocked = false;
        private bool resetArmed = false;
        private bool firstTurnChosen = false;
        private int lastRoundWinner = 0; // 1 = Player 1, 2 = Player 2, 0 = none yet
        private int firstTurnPlayer = 0; // 1 = Player 1, 2 = Player 2

        private int p1Gold = 1200;
        private int p2Gold = 1200;
        private int p1Points = 0;
        private int p2Points = 0;
        private int p1Income = 0;
        private int p2Income = 0;
        private int p1PermMoveUpgrades = 0;
        private int p2PermMoveUpgrades = 0;
        private int p1MilestonePermMoveUpgrades = 0;
        private int p2MilestonePermMoveUpgrades = 0;
        private int p1IncomeUpgrades = 0;
        private int p2IncomeUpgrades = 0;
        private int p1IncomeLevel = 0;
        private int p2IncomeLevel = 0;

        private decimal p1IncomeCost = 100m;
        private decimal p2IncomeCost = 100m;

        private bool p1BoughtIncomeThisRound = false;
        private bool p2BoughtIncomeThisRound = false;
        private bool p1ReplayBoughtThisRound = false;
        private bool p2ReplayBoughtThisRound = false;

        private bool p1HasIncomeDiscount = false;
        private bool p2HasIncomeDiscount = false;

        private bool p1HasFullRefund = false;
        private bool p2HasFullRefund = false;

        private bool ShowConfirm(string title, string message)
        {
            var dialog = new ThemedConfirmDialog(title, message, T("Yes"), T("No"))
            {
                Owner = this
            };

            return dialog.ShowDialog() == true;
        }


        private string p1Name = "Player 1";
        private string p2Name = "Player 2";

        private bool IsDefaultP1Name(string value) =>
            value == En["DefaultP1Name"] || value == Es["DefaultP1Name"] ||
            value == Ru["DefaultP1Name"] || value == Zh["DefaultP1Name"];

        private bool IsDefaultP2Name(string value) =>
            value == En["DefaultP2Name"] || value == Es["DefaultP2Name"] ||
            value == Ru["DefaultP2Name"] || value == Zh["DefaultP2Name"];

        private bool IsNoRoundYetText(string value) =>
            value == En["NoRoundYet"] || value == Es["NoRoundYet"] ||
            value == Ru["NoRoundYet"] || value == Zh["NoRoundYet"];

        private bool p1HasFt10PermMove = false;
        private bool p2HasFt10PermMove = false;

        private bool milestone5Claimed = false;
        private bool milestone10Claimed = false;
        private bool milestone15Claimed = false;
        private bool milestone20Claimed = false;
        private bool milestone25Claimed = false;

        // Global milestone race — tracks which point milestones (multiples of 4) have been
        // claimed by ANYONE. Once claimed, neither player can get it again.
        private HashSet<int> globalClaimedMilestones = new HashSet<int>();

        // Pre-rolled ordered reward queue — drawn in order, pre-rolled at game start
        private List<string> milestoneRewardQueue = new List<string>();

        private int p1SellbackPct = 50;
        private int p2SellbackPct = 50;

        private bool p1Sellback70 = false;
        private bool p2Sellback70 = false;

        private int p1MissedIncomeRounds = 0;
        private int p2MissedIncomeRounds = 0;
        private int p1IncomeDecayPercent = 0;
        private int p2IncomeDecayPercent = 0;

        private bool factionModeEnabled = true;
        private bool factionModeLocked = false;
        private int p1FactionPurchases = 0;
        private int p2FactionPurchases = 0;
        private int p1ChosenFactionPurchases = 0;
        private int p2ChosenFactionPurchases = 0;
        private List<string> p1Factions = new List<string>();
        private List<string> p2Factions = new List<string>();

        private bool ft20ModeEnabled = true;
        private bool ft10ModeEnabled = false;
        private bool ft30ModeEnabled = false;
        private bool ft20ModeLocked = false;
        private bool matchEndPromptSuppressed = false;

        // Legacy ft20 pool fields kept for save compatibility
        private List<string> ft20MilestonePool = new List<string>();
        private int ft20NextMilestoneRound = 6;

        private readonly Brush cyanBrush = new SolidColorBrush(Color.FromRgb(110, 169, 200));
        private readonly Brush disabledBrush = new SolidColorBrush(Color.FromRgb(75, 85, 99));
        private readonly Brush greenBrush = new SolidColorBrush(Color.FromRgb(40, 110, 60));
        private readonly Brush redBrush = new SolidColorBrush(Color.FromRgb(120, 40, 40));
        private readonly Brush normalPanelBrush = new SolidColorBrush(Color.FromRgb(44, 53, 64));
        private readonly Brush p1MilestoneFlagBrush = new SolidColorBrush(Color.FromRgb(255, 139, 139));
        private readonly Brush p2MilestoneFlagBrush = new SolidColorBrush(Color.FromRgb(134, 191, 255));
        private readonly Brush milestoneNumberBrush = new SolidColorBrush(Color.FromRgb(102, 221, 235));

        private readonly DispatcherTimer noticeTimer;
        private readonly DispatcherTimer zoomIndicatorTimer;
        private readonly DispatcherTimer tieTimer;
        private readonly DispatcherTimer tieTimerFlashTimer;
        private string lastNotice = "";
        private int tieTimerRemainingSeconds = TieTimerStartSeconds;
        private DateTime tieTimerEndsAtUtc = DateTime.MinValue;
        private bool tieTimerHasStarted = false;
        private bool tieTimerFlashVisible = true;
        private readonly LinkedList<string> actionLog = new LinkedList<string>();
        private readonly Stack<GameState> undoStack = new Stack<GameState>();

        private string p1Calc = "No round yet.";
        private string p2Calc = "No round yet.";

        private GoldPopOutWindow p1GoldWindow;
        private GoldPopOutWindow p2GoldWindow;

        // Full reward pool definition — counts used to display "Nx" in UI
private static readonly string[] BaseRewardPool = new string[]
{
    "perm_move_upgrade",
    "perm_move_upgrade",
    "sellback_20",
    "income_discount",
    "income_discount",
    "full_refund",
    "full_refund"
};

        private static readonly string[] BaseRewardPoolNoIncome = new string[]
{
    "perm_move_upgrade",
    "perm_move_upgrade",
    "sellback_20",
    "full_refund",
    "full_refund"
};

        private static readonly string[] FactionRewardPool = new string[]
{
    "choose_free_faction",
    "free_faction",
    "free_faction",
    "free_faction",
    "free_faction",
    "perm_move_upgrade",
    "sellback_20",
    "income_discount",
    "income_discount",
    "full_refund"
};

        private static readonly string[] FactionRewardPoolNoIncome = new string[]
{
    "choose_free_faction",
    "free_faction",
    "free_faction",
    "free_faction",
    "free_faction",
    "perm_move_upgrade",
    "sellback_20",
    "full_refund"
};

        private static readonly string[] AllFactions =
        {
            "Ancient", "Good", "Dynasty", "Farmer", "Evil", "Legacy",
            "Medieval", "New Units", "New Units 2", "Pirate", "Renaissance", "Secret",
            "Tribal", "Viking", "Wild West", "Spooky"
        };

        private readonly Dictionary<string, string> FactionIconMap = new Dictionary<string, string>
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

        private struct GameState
        {
            public int round, pendingWinner;
            public bool namesLocked, resetArmed, firstTurnChosen;
            public int p1Gold, p2Gold;
            public int p1Points, p2Points;
            public int p1Income, p2Income;
            public int p1PermMoveUpgrades, p2PermMoveUpgrades;
            public int p1MilestonePermMoveUpgrades, p2MilestonePermMoveUpgrades;
            public int p1IncomeUpgrades, p2IncomeUpgrades;
            public int p1IncomeLevel, p2IncomeLevel;
            public decimal p1IncomeCost, p2IncomeCost;
            public bool p1BoughtIncomeThisRound, p2BoughtIncomeThisRound;
            public bool p1HasIncomeDiscount, p2HasIncomeDiscount;
            public bool p1HasFullRefund, p2HasFullRefund;
            public string p1Name, p2Name, p1Calc, p2Calc;
            public bool p1HasFt10PermMove, p2HasFt10PermMove;
            public bool milestone5Claimed, milestone10Claimed, milestone15Claimed;
            public bool milestone20Claimed, milestone25Claimed;
            public HashSet<int> globalClaimedMilestones;
            public List<string> milestoneRewardQueue;
            public int p1SellbackPct, p2SellbackPct;
            public bool p1Sellback70, p2Sellback70;
            public int p1MissedIncomeRounds, p2MissedIncomeRounds;
            public int p1IncomeDecayPercent, p2IncomeDecayPercent;
            public bool factionModeEnabled, factionModeLocked;
            public int p1FactionPurchases, p2FactionPurchases;
            public int p1ChosenFactionPurchases, p2ChosenFactionPurchases;
            public List<string> p1Factions, p2Factions;
            public bool ft20ModeEnabled, ft10ModeEnabled, ft30ModeEnabled, ft20ModeLocked;
            public bool matchEndPromptSuppressed;
            public List<string> ft20MilestonePool;
            public int ft20NextMilestoneRound;
            public List<string> actionLog;
            public int lastRoundWinner, firstTurnPlayer;
            public bool p1ReplayBoughtThisRound, p2ReplayBoughtThisRound;
        }

        private enum AppLanguage { English, Spanish, Russian, Chinese }
        private AppLanguage currentLanguage = AppLanguage.English;

        private static readonly string LanguageFilePath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "TABS", "language.txt");

        private void LoadLanguage()
        {
            try
            {
                if (!File.Exists(LanguageFilePath)) return;

                AppLanguage loadedLanguage;
                if (Enum.TryParse(File.ReadAllText(LanguageFilePath).Trim(), out loadedLanguage))
                    currentLanguage = loadedLanguage;
            }
            catch { }
        }

        private void SaveLanguage()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(LanguageFilePath));
                File.WriteAllText(LanguageFilePath, currentLanguage.ToString());

                string twoVTwoLanguageFilePath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "TABSEconomyTracker",
                    "language.txt");

                Directory.CreateDirectory(Path.GetDirectoryName(twoVTwoLanguageFilePath));
                File.WriteAllText(twoVTwoLanguageFilePath, currentLanguage.ToString());

                AppPrefs.Language = ToSharedLanguage(currentLanguage);

                TwoVTwoGameMode.Loc.Current = AppPrefs.Language;
                AppPrefs.Save();
            }
            catch { }
        }

        private string T(string key)
        {
            if (currentLanguage == AppLanguage.Chinese && Zh.TryGetValue(key, out var zh)) return zh;
            if (currentLanguage == AppLanguage.Russian && Ru.TryGetValue(key, out var ru)) return ru;
            if (currentLanguage == AppLanguage.Spanish && Es.TryGetValue(key, out var es)) return es;
            return En.TryGetValue(key, out var en) ? en : key;
        }

        private static AppLanguage FromSharedLanguage(TwoVTwoGameMode.Loc.Language language)
        {
            if (language == TwoVTwoGameMode.Loc.Language.Spanish) return AppLanguage.Spanish;
            if (language == TwoVTwoGameMode.Loc.Language.Russian) return AppLanguage.Russian;
            if (language == TwoVTwoGameMode.Loc.Language.Chinese) return AppLanguage.Chinese;
            return AppLanguage.English;
        }

        private static TwoVTwoGameMode.Loc.Language ToSharedLanguage(AppLanguage language)
        {
            if (language == AppLanguage.Spanish) return TwoVTwoGameMode.Loc.Language.Spanish;
            if (language == AppLanguage.Russian) return TwoVTwoGameMode.Loc.Language.Russian;
            if (language == AppLanguage.Chinese) return TwoVTwoGameMode.Loc.Language.Chinese;
            return TwoVTwoGameMode.Loc.Language.English;
        }

        private static AppLanguage PreviousLanguage(AppLanguage language)
        {
            if (language == AppLanguage.English) return AppLanguage.Chinese;
            if (language == AppLanguage.Spanish) return AppLanguage.English;
            if (language == AppLanguage.Russian) return AppLanguage.Spanish;
            return AppLanguage.Russian;
        }

        private static AppLanguage NextLanguage(AppLanguage language)
        {
            if (language == AppLanguage.English) return AppLanguage.Spanish;
            if (language == AppLanguage.Spanish) return AppLanguage.Russian;
            if (language == AppLanguage.Russian) return AppLanguage.Chinese;
            return AppLanguage.English;
        }

        private static string GetLanguageDisplayName(AppLanguage language)
        {
            if (language == AppLanguage.Spanish) return "Español";
            if (language == AppLanguage.Russian) return "Русский";
            if (language == AppLanguage.Chinese) return "中文";
            return "English";
        }

        private static readonly Dictionary<string, string> En = new Dictionary<string, string>
        {
            ["AppTitle"] = "TABS Arena v1.1.5",
            ["Settings"] = "Settings",
            ["Guide"] = "1v1 Guide",
            ["Back"] = "← Back",
            ["WindowMode"] = "Window Mode",
            ["Windowed"] = "Windowed",
            ["BorderlessFullscreen"] = "Borderless Fullscreen",
            ["Language"] = "Language",
            ["Sounds"] = "Sounds",
            ["Volume"] = "Volume",
            ["On"] = "On",
            ["Off"] = "Off",
            ["GuideTitle"] = "1v1 Guide",
            ["GuideBasicsTitle"] = "Basics",
            ["GuideBasicsBody"] = "Each player starts with 1200 gold. Choose who goes first at the start of the match. The first player receives bonus gold to compensate for being counterpicked.",
            ["GuideTurnOrderTitle"] = "Turn Order",
            ["GuideTurnOrderBody"] = "Round 1 follows the chosen first player. After that, the player with more points goes first. If points are tied, the player who won the latest round goes first.",
            ["GuideRoundReplayTitle"] = "Rounds, Ties, And Replay",
            ["GuideRoundReplayBody"] = "When a battle ends, choose the winner and press Next Round. If both players agree it was a tie, use Tie. If there is no agreement, use a 3-minute timer and force a tie if nobody wins. Replay costs 10 gold and can only be bought once per round per player. Replay is for informational purposes only and does not change the outcome or winner of the round.",
            ["GuideSavingTitle"] = "Saving",
            ["GuideSavingBody"] = "If you cannot finish a match, save in the app. Also save the battle inside TABS using Save Battle and enable Save Friendly Units.",
            ["GuideEconomyTitle"] = "Economy",
            ["GuideEconomyBody"] = "Interest gives +10 gold for every 50 gold a player has, capped at +100. Buying income gives +10 in FT30 and +13 in FT20. FT10 removes income purchases and income decay.",
            ["GuideMoreTitle"] = "More Rules",
            ["GuideMoreBody"] = "To learn more about the rules, visit",
            ["ReplayUsed"] = "Replay used",
            ["MainMenu"] = "← Main Menu",
            ["OverviewTitle"] = "1v1 Match Overview",
            ["OverviewSub"] = "Use the controls below, then press Next Round to apply interest, upgrades, and spending.",
            ["CurrentRound"] = "CURRENT ROUND",
            ["NextTurnOrder"] = "NEXT TURN ORDER",
            ["PendingResult"] = "PENDING RESULT",
            ["NotAvailableYet"] = "Not available yet",
            ["NotSet"] = "Not set",
            ["TurnOrderRed"] = "Red -> Blue",
            ["TurnOrderBlue"] = "Blue -> Red",
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
            ["WhichPlayerFirst"] = "Which player is doing their turn first?",
            ["MatchSaves"] = "MATCH SAVES",
            ["Save"] = "💾 Save",
            ["Load"] = "📂 Load",
            ["Delete"] = "🗑 Delete",
            ["NewGame"] = "🆕 New Game",
            ["ActionLog"] = "Action Log",
            ["ActionLogSub"] = "Shop clicks and round results appear here in order.",
            ["RoundControl"] = "Round Control",
            ["Player1Wins"] = "Player 1 Wins",
            ["Player2Wins"] = "Player 2 Wins",
            ["Tie"] = "Tie",
            ["StartTieTimer"] = "Start Tie Timer",
            ["StopTimer"] = "Stop Timer",
            ["ResumeTimer"] = "Resume Timer",
            ["RestartTimer"] = "Restart Timer",
            ["NextRound"] = "Next Round",
            ["Undo"] = "Undo",
            ["Gold"] = "GOLD",
            ["Points"] = "POINTS",
            ["PermMv"] = "PERM MV",
            ["Income"] = "INCOME",
            ["InterestStat"] = "INTEREST",
            ["Set"] = "Set",
            ["Unset"] = "Unset",
            ["CustomTroopSpend"] = "Custom troop spend",
            ["UnitValue"] = "Unit value",
            ["Spend"] = "Spend",
            ["Sell"] = "Sell",
            ["Utility"] = "Utility shop",
            ["Upgrades"] = "Permanent upgrades",
            ["Calculations"] = "Latest calculations",
            ["SingleTroopMove"] = "Single troop move ({0})",
            ["Replay"] = "Replay (10)",
            ["P1FirstTurn"] = "Player 1 Goes First",
            ["P2FirstTurn"] = "Player 2 Goes First",
            ["DefaultP1Name"] = "Player 1",
            ["DefaultP2Name"] = "Player 2",
            ["MilestoneProgress"] = "MILESTONE PROGRESS",
            ["NextReward"] = "NEXT REWARD",
            ["RewardsLeft"] = "POSSIBLE REWARDS LEFT",
            ["PointsAway"] = "points away",
            ["NextAt"] = "next at",
            ["PanelSub"] = "Gold, points, upgrades, and spending controls.",
            ["BuyIncome"] = "Buy income +10 ({0})",
            ["BuyIncomeF"] = "Buy income +13 ({0})",
            ["BuyPermMove"] = "Buy perm move +1 ({0})",
            ["BuyFaction"] = "Buy faction ({0})",
            ["BuyChosenFaction"] = "Buy chosen faction ({0})",
            ["LogBoughtChosenFaction"] = "{0} bought chosen faction: {1} for {2} gold.",
            ["NoticeBoughtChosenFaction"] = "{0} bought {1} for {2} gold.",
            ["NotEnoughGoldChosenFaction"] = "{0} does not have enough gold for chosen faction ({1}).",
            ["AllFactionsOwned"] = "{0} already owns all factions.",
            ["FactionDisabled"] = "Faction Mode Disabled",
            ["SellUnit"] = "Sell",
            ["PoolEmpty"] = "Pool empty",
            ["NoneLeft"] = "None left",
            ["RewardChooseFreeFaction"] = "Choose Free Faction",
            ["RewardFreeFaction"] = "Free Faction",
            ["ChooseFactionTitle"] = "Choose Free Faction",
            ["ChooseFactionSub"] = "{0}, choose one faction to unlock for free.",
            ["LogChoseFreeFaction"] = "Milestone: {0} chose free faction — {1}.",
            ["NoticeChoseFreeFaction"] = "Milestone! {0} chose {1} for free.",
            ["RewardPermMove"] = "Perm Move Upgrade",
            ["RewardSellback20"] = "Sellback +20%",
            ["RewardIncomeDiscount"] = "Income Discount (15%)",
            ["RewardFullRefund"] = "Full Unit Refund",
            ["LogWinnerMarked"] = "Winner marked: {0}.",
            ["LogRoundWon"] = "Round {0} ended. {1} won.",
            ["LogRoundTie"] = "Round {0} ended in a tie.",
            ["SaveDialogTitle"] = "Save Game",
            ["EnterSaveName"] = "Enter save name:",
            ["SaveBtn"] = "Save",
            ["Cancel"] = "Cancel",
            ["Yes"] = "Yes",
            ["No"] = "No",
            ["OverwriteSaveTitle"] = "Overwrite Save",
            ["OverwriteSaveMsg"] = "Overwrite save \"{0}\" with the current match state?",
            ["AlreadyExistsTitle"] = "Already Exists",
            ["AlreadyExistsMsg"] = "A save named \"{0}\" already exists. Overwrite it?",
            ["SelectSaveFirst"] = "Select a save from the dropdown first.",
            ["SelectSaveDeleteFirst"] = "Select a save to delete first.",
            ["SaveFileNotFound"] = "Save file not found.",
            ["CouldNotReadSave"] = "Could not read save file.",
            ["DeleteConfirmTitle"] = "Delete Save",
            ["DeleteConfirmMsg"] = "Delete \"{0}\"?\nThis cannot be undone.",
            ["NewGameConfirmTitle"] = "New Game",
            ["NewGameConfirmMsg"] = "Start a new game?\nAll unsaved progress will be lost.",
            ["MatchEndTitle"] = "Match Complete",
            ["MatchEndMessage"] = "{0} won the match by reaching {1} points.",
            ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} wins via win by 2 rule.",
            ["MatchEndQuestion"] = "Start a new game or continue playing?",
            ["NewGamePlain"] = "New Game",
            ["ContinuePlaying"] = "Continue",
            ["MainMenuConfirmTitle"] = "Main Menu",
            ["MainMenuConfirmMsg"] = "Return to the main menu?\nUnsaved progress will be lost.",
            ["CloseGameConfirmTitle"] = "Close Game",
            ["CloseGameConfirmMsg"] = "Are you sure you want to close the game?",
            ["StartingGold"] = "Starting gold",
            ["MilestoneReward"] = "Milestone reward",
            ["RoundReward"] = "Round reward",
            ["PermanentIncome"] = "Permanent income",
            ["FinalGold"] = "Final gold",
            ["LogFactionModeOn"] = "Faction mode enabled. Both players reset to this mode's starting gold and 3 random factions.",
            ["LogFactionModeOff"] = "Faction mode disabled.",
            ["LogFT30ModeOn"] = "FT30 mode enabled. Player panels reset.",
            ["LogFT30ModeOff"] = "FT30 mode disabled. FT20 mode selected.",
            ["LogFT10ModeOn"] = "FT10 mode enabled. Players start with 1200 gold and income is disabled.",
            ["LogFT10ModeOff"] = "FT10 mode disabled. FT20 mode selected.",
            ["LogGainedFaction"] = "{0} gained faction: {1}.",
            ["NoticeGainedFaction"] = "{0} gained {1}.",
            ["LogBoughtIncome"] = "{0} bought income +{1} for {2} gold.",
            ["LogBoughtPermMove"] = "{0} bought perm move upgrade for {1} gold.",
            ["LogSingleTroopMove"] = "{0} bought single troop move for {1} gold.",
            ["LogReplay"] = "{0} bought a replay for 10 gold.",
            ["LogSpentTroops"] = "{0} spent {1} gold on troops.",
            ["WinsSuffix"] = "wins",
            ["NothingToUndo"] = "Nothing to undo.",
            ["ChooseWinnerFirst"] = "Choose a winner before going to the next round.",
            ["RoundWinNotice"] = "{0} wins round {1}! Winner +{2}g, loser +{3}g.",
            ["RoundTieNotice"] = "Round {0} ended in a tie. Both players +{1}g.",
            ["MilestonePermMoveNotice"] = "Milestone! {0} receives a free perm move upgrade!",
            ["MilestoneSellbackNotice"] = "Milestone! {0} sellback is now {1}%!",
            ["MilestoneIncomeDiscountNotice"] = "Milestone! {0} gets 15% off their next income purchase!",
            ["MilestoneFullRefundNotice"] = "Milestone! {0}'s next unit sell will be a full refund!",
            ["NoRoundYet"] = "No round yet.",
            ["LogUndo"] = "Undid the last action.",
            ["LogSavedMatch"] = "Match saved as \"{0}\".",
            ["NoticeSavedAs"] = "Saved as \"{0}\".",
            ["LoadPreviewTitle"] = "Load \"{0}\"?",
            ["LoadPreviewMsg"] = "Saved:   {0}\n\n⚔  {1}  vs  {2}\n\nRound:   {3}\nScore:   {4}  {5}  -  {6}  {7}",
            ["LogLoadedMatch"] = "Loaded match \"{0}\".",
            ["NoticeLoadedSave"] = "Loaded \"{0}\".",
            ["NoticeDeletedSave"] = "Deleted save \"{0}\".",
            ["LogNewGameStarted"] = "New game started.",
            ["LogMilestonePoolEmpty"] = "Milestone reached but reward pool is empty - no reward given.",
            ["LogMilestoneChooseFreeFactionAllOwned"] = "Milestone: {0} rolled Choose Free Faction but owns all factions.",
            ["NoticeMilestoneAllFactionsOwned"] = "Milestone! {0} owns all factions - no reward this time.",
            ["LogMilestoneFreeFaction"] = "Milestone: {0} receives free faction - {1}.",
            ["NoticeMilestoneFreeFaction"] = "Milestone! {0} receives free faction - {1}!",
            ["LogMilestoneFreeFactionAllOwned"] = "Milestone: {0} rolled Free Faction but owns all factions.",
            ["LogMilestonePermMove"] = "Milestone: {0} receives a free perm move upgrade!",
            ["LogMilestoneSellback"] = "Milestone: {0} sellback increased by 20% -> {1}%.",
            ["LogMilestoneIncomeDiscount"] = "Milestone: {0} receives a one-time 15% income discount!",
            ["LogMilestoneFullRefund"] = "Milestone: {0} receives a one-time full troop refund!",
            ["FactionModeLocked"] = "Faction mode is locked after round 1.",
            ["MatchModeLocked"] = "Match mode is locked after round 1.",
            ["NotEnoughGoldFaction"] = "{0} does not have enough gold for a faction ({1}).",
            ["NotEnoughGold"] = "{0} does not have enough gold.",
            ["NotEnoughGoldAmount"] = "{0} does not have enough gold ({1}).",
            ["IncomeAlreadyBought"] = "{0} already bought income this round.",
            ["MaxedPermMove"] = "{0} has reached the perm move cap ({1}).",
            ["ReplayAlreadyBought"] = "{0} already bought replay this round.",
            ["EnterValidSpendAmount"] = "Enter a valid amount to spend.",
            ["EnterValidUnitValue"] = "Enter the unit's value first.",
            ["LogPlayerGoesFirst"] = "{0} goes first and receives 50 gold.",
            ["LogFullRefundSell"] = "{0} used Full Refund - sold unit for full {1} gold.",
            ["NoticeFullRefundSell"] = "Full refund used! {0} got {1} gold back.",
            ["LogSoldUnit"] = "{0} sold unit worth {1} gold for {2} gold ({3}%).",
        };

        private static readonly Dictionary<string, string> Es = new Dictionary<string, string>
        {
            ["AppTitle"] = "TABS Arena v1.1.5",
            ["Settings"] = "Ajustes",
            ["Guide"] = "Guía 1v1",
            ["Back"] = "← Volver",
            ["WindowMode"] = "Modo de ventana",
            ["Windowed"] = "Ventana",
            ["BorderlessFullscreen"] = "Pantalla completa sin bordes",
            ["Language"] = "Idioma",
            ["Sounds"] = "Sonidos",
            ["Volume"] = "Volumen",
            ["On"] = "Activado",
            ["Off"] = "Desactivado",
            ["GuideTitle"] = "Guía 1v1",
            ["GuideBasicsTitle"] = "Conceptos básicos",
            ["GuideBasicsBody"] = "Cada jugador empieza con 1200 de oro. Elige quién va primero al inicio de la partida. El primer jugador recibe oro extra para compensar que puede ser contraelegido.",
            ["GuideTurnOrderTitle"] = "Orden de turno",
            ["GuideTurnOrderBody"] = "La ronda 1 usa el jugador elegido primero. Después, el jugador con más puntos va primero. Si los puntos están empatados, va primero quien ganó la ronda más reciente.",
            ["GuideRoundReplayTitle"] = "Rondas, empates y replay",
            ["GuideRoundReplayBody"] = "Cuando termine una batalla, elige el ganador y presiona Siguiente Ronda. Si ambos jugadores están de acuerdo en que fue empate, usa Empate. Si no hay acuerdo, usa un temporizador de 3 minutos y fuerza empate si nadie gana. Replay cuesta 10 de oro y solo puede comprarse una vez por ronda por jugador. Replay es solo para información y no cambia el resultado ni el ganador de la ronda.",
            ["GuideSavingTitle"] = "Guardado",
            ["GuideSavingBody"] = "Si no pueden terminar la partida, guarda en la app. También guarda la batalla dentro de TABS usando Save Battle y activa Save Friendly Units.",
            ["GuideEconomyTitle"] = "Economía",
            ["GuideEconomyBody"] = "El interés da +10 de oro por cada 50 de oro que tenga un jugador, con máximo de +100. Comprar ingreso da +10 en FT30 y +13 en FT20. FT10 elimina compras de ingreso y decaimiento de ingreso.",
            ["GuideMoreTitle"] = "Más reglas",
            ["GuideMoreBody"] = "Para aprender más sobre las reglas, visita",
            ["ReplayUsed"] = "Replay usado",
            ["MainMenu"] = "← Menú Principal",
            ["OverviewTitle"] = "Resumen 1v1",
            ["OverviewSub"] = "Usa los controles y presiona Siguiente Ronda para aplicar interés, mejoras y gastos.",
            ["CurrentRound"] = "RONDA ACTUAL",
            ["NextTurnOrder"] = "PRÓXIMO TURNO",
            ["PendingResult"] = "RESULTADO PENDIENTE",
            ["NotAvailableYet"] = "No disponible aún",
            ["NotSet"] = "No establecido",
            ["TurnOrderRed"] = "Rojo -> Azul",
            ["TurnOrderBlue"] = "Azul -> Rojo",
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
            ["WhichPlayerFirst"] = "¿Qué jugador hace su turno primero?",
            ["MatchSaves"] = "GUARDADOS",
            ["Save"] = "💾 Guardar",
            ["Load"] = "📂 Cargar",
            ["Delete"] = "🗑 Borrar",
            ["NewGame"] = "🆕 Nueva Partida",
            ["ActionLog"] = "Registro de Acciones",
            ["ActionLogSub"] = "Los clics y resultados aparecen aquí.",
            ["RoundControl"] = "Control de Ronda",
            ["Player1Wins"] = "Gana Jugador 1",
            ["Player2Wins"] = "Gana Jugador 2",
            ["Tie"] = "Empate",
            ["StartTieTimer"] = "Iniciar temporizador",
            ["StopTimer"] = "Detener",
            ["ResumeTimer"] = "Reanudar",
            ["RestartTimer"] = "Reiniciar",
            ["NextRound"] = "Siguiente Ronda",
            ["Undo"] = "Deshacer",
            ["Gold"] = "ORO",
            ["Points"] = "PUNTOS",
            ["PermMv"] = "MV PERM",
            ["Income"] = "INGRESO",
            ["InterestStat"] = "INTERÉS",
            ["Set"] = "Listo",
            ["Unset"] = "Editar",
            ["CustomTroopSpend"] = "Gasto personalizado de tropas",
            ["UnitValue"] = "Valor de unidad",
            ["Spend"] = "Gastar",
            ["Sell"] = "Vender",
            ["Utility"] = "Tienda de utilidad",
            ["Upgrades"] = "Mejoras permanentes",
            ["Calculations"] = "Últimos cálculos",
            ["SingleTroopMove"] = "Mover tropa individual ({0})",
            ["Replay"] = "Repetición (10)",
            ["P1FirstTurn"] = "Jugador 1 Va Primero",
            ["P2FirstTurn"] = "Jugador 2 Va Primero",
            ["DefaultP1Name"] = "Jugador 1",
            ["DefaultP2Name"] = "Jugador 2",
            ["MilestoneProgress"] = "PROGRESO DE HITO",
            ["NextReward"] = "PRÓXIMA RECOMPENSA",
            ["RewardsLeft"] = "RECOMPENSAS RESTANTES",
            ["PointsAway"] = "puntos restantes",
            ["NextAt"] = "siguiente en",
            ["PanelSub"] = "Oro, puntos, mejoras y controles de gasto.",
            ["BuyIncome"] = "Comprar ingreso +10 ({0})",
            ["BuyIncomeF"] = "Comprar ingreso +13 ({0})",
            ["BuyPermMove"] = "Comprar mv perm +1 ({0})",
            ["BuyFaction"] = "Comprar facción ({0})",
            ["BuyChosenFaction"] = "Comprar facción elegida ({0})",
            ["LogBoughtChosenFaction"] = "{0} compró facción elegida: {1} por {2} de oro.",
            ["NoticeBoughtChosenFaction"] = "{0} compró {1} por {2} de oro.",
            ["NotEnoughGoldChosenFaction"] = "{0} no tiene suficiente oro para facción elegida ({1}).",
            ["AllFactionsOwned"] = "{0} ya tiene todas las facciones.",
            ["FactionDisabled"] = "Modo Facción Desactivado",
            ["SellUnit"] = "Vender",
            ["PoolEmpty"] = "Grupo vacío",
            ["NoneLeft"] = "Nada restante",
            ["RewardChooseFreeFaction"] = "Elegir facción gratis",
            ["RewardFreeFaction"] = "Facción gratis",
            ["ChooseFactionTitle"] = "Elegir facción gratis",
            ["ChooseFactionSub"] = "{0}, elige una facción para desbloquear gratis.",
            ["LogChoseFreeFaction"] = "Hito: {0} eligió facción gratis — {1}.",
            ["NoticeChoseFreeFaction"] = "¡Hito! {0} eligió {1} gratis.",
            ["RewardPermMove"] = "Mejora de mv perm",
            ["RewardSellback20"] = "Reventa +20%",
            ["RewardIncomeDiscount"] = "Descuento de ingreso (15%)",
            ["RewardFullRefund"] = "Reembolso completo de unidad",
            ["LogWinnerMarked"] = "Ganador marcado: {0}.",
            ["LogRoundWon"] = "Ronda {0} terminó. {1} ganó.",
            ["LogRoundTie"] = "Ronda {0} terminó en empate.",
            ["SaveDialogTitle"] = "Guardar Partida",
            ["EnterSaveName"] = "Ingresa nombre del guardado:",
            ["SaveBtn"] = "Guardar",
            ["Cancel"] = "Cancelar",
            ["Yes"] = "Sí",
            ["No"] = "No",
            ["OverwriteSaveTitle"] = "Sobrescribir Guardado",
            ["OverwriteSaveMsg"] = "¿Sobrescribir \"{0}\" con el estado actual?",
            ["AlreadyExistsTitle"] = "Ya Existe",
            ["AlreadyExistsMsg"] = "Ya existe un guardado llamado \"{0}\". ¿Sobrescribirlo?",
            ["SelectSaveFirst"] = "Selecciona un guardado primero.",
            ["SelectSaveDeleteFirst"] = "Selecciona un guardado para borrar.",
            ["SaveFileNotFound"] = "No se encontró el archivo de guardado.",
            ["CouldNotReadSave"] = "No se pudo leer el guardado.",
            ["DeleteConfirmTitle"] = "Borrar Guardado",
            ["DeleteConfirmMsg"] = "¿Borrar \"{0}\"?\nEsto no se puede deshacer.",
            ["NewGameConfirmTitle"] = "Nueva Partida",
            ["NewGameConfirmMsg"] = "¿Iniciar nueva partida?\nEl progreso no guardado se perderá.",
            ["MatchEndTitle"] = "Partida Terminada",
            ["MatchEndMessage"] = "{0} ganó la partida al llegar a {1} puntos.",
            ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} gana por la regla de ganar por 2.",
            ["MatchEndQuestion"] = "¿Iniciar una nueva partida o seguir jugando?",
            ["NewGamePlain"] = "Nueva Partida",
            ["ContinuePlaying"] = "Continuar",
            ["MainMenuConfirmTitle"] = "Menú Principal",
            ["MainMenuConfirmMsg"] = "¿Volver al menú principal?\nEl progreso no guardado se perderá.",
            ["CloseGameConfirmTitle"] = "Cerrar Juego",
            ["CloseGameConfirmMsg"] = "¿Seguro que quieres cerrar el juego?",
            ["StartingGold"] = "Oro inicial",
            ["MilestoneReward"] = "Recompensa de hito",
            ["RoundReward"] = "Recompensa de ronda",
            ["PermanentIncome"] = "Ingreso permanente",
            ["FinalGold"] = "Oro final",
            ["LogFactionModeOn"] = "Modo Facción activado. Ambos jugadores se reinician con el oro inicial del modo y 3 facciones aleatorias.",
            ["LogFactionModeOff"] = "Modo Facción desactivado.",
            ["LogFT30ModeOn"] = "Modo FT30 activado. Paneles reiniciados.",
            ["LogFT30ModeOff"] = "Modo FT30 desactivado. Modo FT20 seleccionado.",
            ["LogFT10ModeOn"] = "Modo FT10 activado. Los jugadores empiezan con 1200 de oro y el ingreso está desactivado.",
            ["LogFT10ModeOff"] = "Modo FT10 desactivado. Modo FT20 seleccionado.",
            ["LogGainedFaction"] = "{0} obtuvo facción: {1}.",
            ["NoticeGainedFaction"] = "{0} obtuvo {1}.",
            ["LogBoughtIncome"] = "{0} compró ingreso +{1} por {2} de oro.",
            ["LogBoughtPermMove"] = "{0} compró mejora de mv perm por {1} de oro.",
            ["LogSingleTroopMove"] = "{0} compró mover tropa individual por {1} de oro.",
            ["LogReplay"] = "{0} compró repetición por 10 de oro.",
            ["LogSpentTroops"] = "{0} gastó {1} de oro en tropas.",
            ["WinsSuffix"] = "gana",
            ["NothingToUndo"] = "Nada que deshacer.",
            ["ChooseWinnerFirst"] = "Elige un ganador antes de pasar a la siguiente ronda.",
            ["RoundWinNotice"] = "¡{0} gana la ronda {1}! Ganador +{2}g, perdedor +{3}g.",
            ["RoundTieNotice"] = "La ronda {0} terminó en empate. Ambos jugadores +{1}g.",
            ["MilestonePermMoveNotice"] = "¡Hito! {0} recibe una mejora gratis de movimiento permanente.",
            ["MilestoneSellbackNotice"] = "¡Hito! La reventa de {0} ahora es {1}%.",
            ["MilestoneIncomeDiscountNotice"] = "¡Hito! {0} obtiene 15% de descuento en su próxima compra de ingreso.",
            ["MilestoneFullRefundNotice"] = "¡Hito! La próxima venta de unidad de {0} será un reembolso completo.",
            ["NoRoundYet"] = "Sin ronda aún.",
            ["LogUndo"] = "Se deshizo la última acción.",
            ["LogSavedMatch"] = "Partida guardada como \"{0}\".",
            ["NoticeSavedAs"] = "Guardado como \"{0}\".",
            ["LoadPreviewTitle"] = "¿Cargar \"{0}\"?",
            ["LoadPreviewMsg"] = "Guardado:   {0}\n\n⚔  {1}  vs  {2}\n\nRonda:   {3}\nPuntuación:   {4}  {5}  -  {6}  {7}",
            ["LogLoadedMatch"] = "Partida cargada \"{0}\".",
            ["NoticeLoadedSave"] = "Cargado \"{0}\".",
            ["NoticeDeletedSave"] = "Guardado \"{0}\" borrado.",
            ["LogNewGameStarted"] = "Nueva partida iniciada.",
            ["LogMilestonePoolEmpty"] = "Hito alcanzado, pero el grupo de recompensas está vacío - no se otorgó recompensa.",
            ["LogMilestoneChooseFreeFactionAllOwned"] = "Hito: {0} sacó Elegir facción gratis, pero ya tiene todas las facciones.",
            ["NoticeMilestoneAllFactionsOwned"] = "¡Hito! {0} ya tiene todas las facciones - no hay recompensa esta vez.",
            ["LogMilestoneFreeFaction"] = "Hito: {0} recibe facción gratis - {1}.",
            ["NoticeMilestoneFreeFaction"] = "¡Hito! {0} recibe facción gratis - {1}!",
            ["LogMilestoneFreeFactionAllOwned"] = "Hito: {0} sacó Facción gratis, pero ya tiene todas las facciones.",
            ["LogMilestonePermMove"] = "Hito: {0} recibe una mejora gratis de movimiento permanente.",
            ["LogMilestoneSellback"] = "Hito: la reventa de {0} aumentó 20% -> {1}%.",
            ["LogMilestoneIncomeDiscount"] = "Hito: {0} recibe un descuento único de 15% en ingreso.",
            ["LogMilestoneFullRefund"] = "Hito: {0} recibe un reembolso completo único de tropas.",
            ["FactionModeLocked"] = "El modo Facción queda bloqueado después de la ronda 1.",
            ["MatchModeLocked"] = "El modo de partida queda bloqueado después de la ronda 1.",
            ["NotEnoughGoldFaction"] = "{0} no tiene suficiente oro para una facción ({1}).",
            ["NotEnoughGold"] = "{0} no tiene suficiente oro.",
            ["NotEnoughGoldAmount"] = "{0} no tiene suficiente oro ({1}).",
            ["IncomeAlreadyBought"] = "{0} ya compró ingreso esta ronda.",
            ["MaxedPermMove"] = "{0} alcanzó el límite de movimiento permanente ({1}).",
            ["ReplayAlreadyBought"] = "{0} ya compró replay esta ronda.",
            ["EnterValidSpendAmount"] = "Ingresa una cantidad válida para gastar.",
            ["EnterValidUnitValue"] = "Ingresa primero el valor de la unidad.",
            ["LogPlayerGoesFirst"] = "{0} va primero y recibe 50 de oro.",
            ["LogFullRefundSell"] = "{0} usó Reembolso Completo - vendió una unidad por el total de {1} oro.",
            ["NoticeFullRefundSell"] = "¡Reembolso completo usado! {0} recuperó {1} de oro.",
            ["LogSoldUnit"] = "{0} vendió una unidad de valor {1} por {2} oro ({3}%).",
        };

        private static readonly Dictionary<string, string> Ru = new Dictionary<string, string>
        {
            ["AppTitle"] = "TABS Arena v1.1.5",
            ["Settings"] = "Настройки",
            ["Guide"] = "Руководство 1v1",
            ["Back"] = "← Назад",
            ["WindowMode"] = "Режим окна",
            ["Windowed"] = "Оконный",
            ["BorderlessFullscreen"] = "Полный экран без рамки",
            ["Language"] = "Язык",
            ["Sounds"] = "Звуки",
            ["Volume"] = "Громкость",
            ["On"] = "Вкл",
            ["Off"] = "Выкл",
            ["GuideTitle"] = "Руководство 1v1",
            ["GuideBasicsTitle"] = "Основы",
            ["GuideBasicsBody"] = "Каждый игрок начинает с 1200 золота. В начале матча выберите, кто ходит первым. Первый игрок получает бонусное золото, чтобы компенсировать контрвыбор.",
            ["GuideTurnOrderTitle"] = "Порядок хода",
            ["GuideTurnOrderBody"] = "В раунде 1 ходит выбранный первый игрок. После этого первым ходит игрок с большим количеством очков. Если очки равны, первым ходит игрок, выигравший последний раунд.",
            ["GuideRoundReplayTitle"] = "Раунды, ничьи и повтор",
            ["GuideRoundReplayBody"] = "Когда битва закончится, выберите победителя и нажмите Следующий раунд. Если оба игрока согласны, что была ничья, используйте Ничья. Если согласия нет, используйте таймер на 3 минуты и принудительно ставьте ничью, если никто не победил. Повтор стоит 10 золота и может быть куплен только один раз за раунд каждым игроком. Повтор нужен только для информации и не меняет результат или победителя раунда.",
            ["GuideSavingTitle"] = "Сохранение",
            ["GuideSavingBody"] = "Если вы не можете закончить матч, сохраните его в приложении. Также сохраните битву внутри TABS через Save Battle и включите Save Friendly Units.",
            ["GuideEconomyTitle"] = "Экономика",
            ["GuideEconomyBody"] = "Проценты дают +10 золота за каждые 50 золота у игрока, максимум +100. Покупка дохода дает +10 в FT30 и +13 в FT20. FT10 убирает покупки дохода и спад дохода.",
            ["GuideMoreTitle"] = "Больше правил",
            ["GuideMoreBody"] = "Чтобы узнать больше о правилах, посетите",
            ["ReplayUsed"] = "Повтор использован",
            ["MainMenu"] = "← Главное меню",
            ["OverviewTitle"] = "Обзор матча 1v1",
            ["OverviewSub"] = "Используйте элементы ниже, затем нажмите Следующий раунд, чтобы применить проценты, улучшения и траты.",
            ["CurrentRound"] = "ТЕКУЩИЙ РАУНД",
            ["NextTurnOrder"] = "СЛЕДУЮЩИЙ ХОД",
            ["PendingResult"] = "ОЖИДАЕМЫЙ РЕЗУЛЬТАТ",
            ["NotAvailableYet"] = "Пока недоступно",
            ["NotSet"] = "Не задано",
            ["TurnOrderRed"] = "Красные -> Синие",
            ["TurnOrderBlue"] = "Синие -> Красные",
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
            ["WhichPlayerFirst"] = "Какой игрок ходит первым?",
            ["MatchSaves"] = "СОХРАНЕНИЯ",
            ["Save"] = "💾 Сохранить",
            ["Load"] = "📂 Загрузить",
            ["Delete"] = "🗑 Удалить",
            ["NewGame"] = "🆕 Новая игра",
            ["ActionLog"] = "Журнал действий",
            ["ActionLogSub"] = "Покупки и результаты раундов появляются здесь по порядку.",
            ["RoundControl"] = "Управление раундом",
            ["Player1Wins"] = "Победа Игрока 1",
            ["Player2Wins"] = "Победа Игрока 2",
            ["Tie"] = "Ничья",
            ["StartTieTimer"] = "Запустить таймер",
            ["StopTimer"] = "Остановить",
            ["ResumeTimer"] = "Продолжить",
            ["RestartTimer"] = "Сбросить",
            ["NextRound"] = "Следующий раунд",
            ["Undo"] = "Отменить",
            ["Gold"] = "ЗОЛОТО",
            ["Points"] = "ОЧКИ",
            ["PermMv"] = "ПОСТ. ХОД",
            ["Income"] = "ДОХОД",
            ["InterestStat"] = "ПРОЦЕНТЫ",
            ["Set"] = "Готово",
            ["Unset"] = "Изменить",
            ["CustomTroopSpend"] = "Своя трата на войска",
            ["UnitValue"] = "Цена юнита",
            ["Spend"] = "Потратить",
            ["Sell"] = "Продать",
            ["Utility"] = "Магазин утилит",
            ["Upgrades"] = "Постоянные улучшения",
            ["Calculations"] = "Последние расчеты",
            ["SingleTroopMove"] = "Переместить одного юнита ({0})",
            ["Replay"] = "Повтор (10)",
            ["P1FirstTurn"] = "Игрок 1 ходит первым",
            ["P2FirstTurn"] = "Игрок 2 ходит первым",
            ["DefaultP1Name"] = "Игрок 1",
            ["DefaultP2Name"] = "Игрок 2",
            ["MilestoneProgress"] = "ПРОГРЕСС ЭТАПА",
            ["NextReward"] = "СЛЕДУЮЩАЯ НАГРАДА",
            ["RewardsLeft"] = "ВОЗМОЖНЫЕ НАГРАДЫ",
            ["PointsAway"] = "очк. до награды",
            ["NextAt"] = "следующая на",
            ["PanelSub"] = "Золото, очки, улучшения и управление тратами.",
            ["BuyIncome"] = "Купить доход +10 ({0})",
            ["BuyIncomeF"] = "Купить доход +13 ({0})",
            ["BuyPermMove"] = "Купить пост. ход +1 ({0})",
            ["BuyFaction"] = "Купить фракцию ({0})",
            ["BuyChosenFaction"] = "Купить выбранную фракцию ({0})",
            ["LogBoughtChosenFaction"] = "{0} купил выбранную фракцию: {1} за {2} золота.",
            ["NoticeBoughtChosenFaction"] = "{0} купил {1} за {2} золота.",
            ["NotEnoughGoldChosenFaction"] = "{0} не хватает золота для выбранной фракции ({1}).",
            ["AllFactionsOwned"] = "{0} уже владеет всеми фракциями.",
            ["FactionDisabled"] = "Режим фракций выключен",
            ["SellUnit"] = "Продать",
            ["PoolEmpty"] = "Пул пуст",
            ["NoneLeft"] = "Ничего нет",
            ["RewardChooseFreeFaction"] = "Выбрать бесплатную фракцию",
            ["RewardFreeFaction"] = "Бесплатная фракция",
            ["ChooseFactionTitle"] = "Выбрать бесплатную фракцию",
            ["ChooseFactionSub"] = "{0}, выберите одну фракцию для бесплатной разблокировки.",
            ["LogChoseFreeFaction"] = "Этап: {0} выбрал бесплатную фракцию - {1}.",
            ["NoticeChoseFreeFaction"] = "Этап! {0} выбрал {1} бесплатно.",
            ["RewardPermMove"] = "Улучшение пост. хода",
            ["RewardSellback20"] = "Продажа +20%",
            ["RewardIncomeDiscount"] = "Скидка на доход (15%)",
            ["RewardFullRefund"] = "Полный возврат за юнита",
            ["LogWinnerMarked"] = "Победитель выбран: {0}.",
            ["LogRoundWon"] = "Раунд {0} завершен. {1} победил.",
            ["LogRoundTie"] = "Раунд {0} завершился ничьей.",
            ["SaveDialogTitle"] = "Сохранить игру",
            ["EnterSaveName"] = "Введите имя сохранения:",
            ["SaveBtn"] = "Сохранить",
            ["Cancel"] = "Отмена",
            ["Yes"] = "Да",
            ["No"] = "Нет",
            ["OverwriteSaveTitle"] = "Перезаписать сохранение",
            ["OverwriteSaveMsg"] = "Перезаписать сохранение \"{0}\" текущим состоянием матча?",
            ["AlreadyExistsTitle"] = "Уже существует",
            ["AlreadyExistsMsg"] = "Сохранение с именем \"{0}\" уже существует. Перезаписать?",
            ["SelectSaveFirst"] = "Сначала выберите сохранение из списка.",
            ["SelectSaveDeleteFirst"] = "Выберите сохранение для удаления.",
            ["SaveFileNotFound"] = "Файл сохранения не найден.",
            ["CouldNotReadSave"] = "Не удалось прочитать сохранение.",
            ["DeleteConfirmTitle"] = "Удалить сохранение",
            ["DeleteConfirmMsg"] = "Удалить \"{0}\"?\nЭто нельзя отменить.",
            ["NewGameConfirmTitle"] = "Новая игра",
            ["NewGameConfirmMsg"] = "Начать новую игру?\nВесь несохраненный прогресс будет потерян.",
            ["MatchEndTitle"] = "Матч завершен",
            ["MatchEndMessage"] = "{0} выиграл матч, набрав {1} очков.",
            ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} побеждает по правилу победы с разницей 2.",
            ["MatchEndQuestion"] = "Начать новую игру или продолжить?",
            ["NewGamePlain"] = "Новая игра",
            ["ContinuePlaying"] = "Продолжить",
            ["MainMenuConfirmTitle"] = "Главное меню",
            ["MainMenuConfirmMsg"] = "Вернуться в главное меню?\nНесохраненный прогресс будет потерян.",
            ["CloseGameConfirmTitle"] = "Закрыть игру",
            ["CloseGameConfirmMsg"] = "Вы уверены, что хотите закрыть игру?",
            ["StartingGold"] = "Начальное золото",
            ["MilestoneReward"] = "Награда этапа",
            ["RoundReward"] = "Награда раунда",
            ["PermanentIncome"] = "Постоянный доход",
            ["FinalGold"] = "Итоговое золото",
            ["LogFactionModeOn"] = "Режим фракций включен. Оба игрока сброшены на стартовое золото этого режима и 3 случайные фракции.",
            ["LogFactionModeOff"] = "Режим фракций выключен.",
            ["LogFT30ModeOn"] = "Режим FT30 включен. Панели игроков сброшены.",
            ["LogFT30ModeOff"] = "Режим FT30 выключен. Выбран режим FT20.",
            ["LogFT10ModeOn"] = "Режим FT10 включен. Игроки начинают с 1200 золота, доход выключен.",
            ["LogFT10ModeOff"] = "Режим FT10 выключен. Выбран режим FT20.",
            ["LogGainedFaction"] = "{0} получил фракцию: {1}.",
            ["NoticeGainedFaction"] = "{0} получил {1}.",
            ["LogBoughtIncome"] = "{0} купил доход +{1} за {2} золота.",
            ["LogBoughtPermMove"] = "{0} купил улучшение пост. хода за {1} золота.",
            ["LogSingleTroopMove"] = "{0} купил перемещение одного юнита за {1} золота.",
            ["LogReplay"] = "{0} купил повтор за 10 золота.",
            ["LogSpentTroops"] = "{0} потратил {1} золота на войска.",
            ["WinsSuffix"] = "побеждает",
            ["NothingToUndo"] = "Нечего отменять.",
            ["ChooseWinnerFirst"] = "Выберите победителя перед переходом к следующему раунду.",
            ["RoundWinNotice"] = "{0} выигрывает раунд {1}! Победитель +{2}g, проигравший +{3}g.",
            ["RoundTieNotice"] = "Раунд {0} завершился ничьей. Оба игрока +{1}g.",
            ["MilestonePermMoveNotice"] = "Этап! {0} получает бесплатное улучшение пост. хода.",
            ["MilestoneSellbackNotice"] = "Этап! Продажа юнитов {0} теперь {1}%.",
            ["MilestoneIncomeDiscountNotice"] = "Этап! {0} получает скидку 15% на следующую покупку дохода.",
            ["MilestoneFullRefundNotice"] = "Этап! Следующая продажа юнита у {0} даст полный возврат.",
            ["NoRoundYet"] = "Раунда еще нет.",
            ["LogUndo"] = "Последнее действие отменено.",
            ["LogSavedMatch"] = "Матч сохранен как \"{0}\".",
            ["NoticeSavedAs"] = "Сохранено как \"{0}\".",
            ["LoadPreviewTitle"] = "Загрузить \"{0}\"?",
            ["LoadPreviewMsg"] = "Сохранено:   {0}\n\n⚔  {1}  против  {2}\n\nРаунд:   {3}\nСчет:   {4}  {5}  -  {6}  {7}",
            ["LogLoadedMatch"] = "Матч \"{0}\" загружен.",
            ["NoticeLoadedSave"] = "Загружено \"{0}\".",
            ["NoticeDeletedSave"] = "Сохранение \"{0}\" удалено.",
            ["LogNewGameStarted"] = "Новая игра начата.",
            ["LogMilestonePoolEmpty"] = "Этап достигнут, но пул наград пуст - награда не выдана.",
            ["LogMilestoneChooseFreeFactionAllOwned"] = "Этап: {0} получил Выбор бесплатной фракции, но уже владеет всеми фракциями.",
            ["NoticeMilestoneAllFactionsOwned"] = "Этап! {0} уже владеет всеми фракциями - награды в этот раз нет.",
            ["LogMilestoneFreeFaction"] = "Этап: {0} получает бесплатную фракцию - {1}.",
            ["NoticeMilestoneFreeFaction"] = "Этап! {0} получает бесплатную фракцию - {1}!",
            ["LogMilestoneFreeFactionAllOwned"] = "Этап: {0} получил Бесплатную фракцию, но уже владеет всеми фракциями.",
            ["LogMilestonePermMove"] = "Этап: {0} получает бесплатное улучшение пост. хода!",
            ["LogMilestoneSellback"] = "Этап: продажа юнитов {0} увеличена на 20% -> {1}%.",
            ["LogMilestoneIncomeDiscount"] = "Этап: {0} получает одноразовую скидку 15% на доход!",
            ["LogMilestoneFullRefund"] = "Этап: {0} получает одноразовый полный возврат за войска!",
            ["FactionModeLocked"] = "Режим фракций блокируется после раунда 1.",
            ["MatchModeLocked"] = "Режим матча блокируется после раунда 1.",
            ["NotEnoughGoldFaction"] = "{0} не хватает золота для фракции ({1}).",
            ["NotEnoughGold"] = "{0} не хватает золота.",
            ["NotEnoughGoldAmount"] = "{0} не хватает золота ({1}).",
            ["IncomeAlreadyBought"] = "{0} уже купил доход в этом раунде.",
            ["MaxedPermMove"] = "{0} достиг лимита пост. хода ({1}).",
            ["ReplayAlreadyBought"] = "{0} уже купил повтор в этом раунде.",
            ["EnterValidSpendAmount"] = "Введите допустимую сумму для траты.",
            ["EnterValidUnitValue"] = "Сначала введите цену юнита.",
            ["LogPlayerGoesFirst"] = "{0} ходит первым и получает 50 золота.",
            ["LogFullRefundSell"] = "{0} использовал Полный возврат - продал юнита за полные {1} золота.",
            ["NoticeFullRefundSell"] = "Полный возврат использован! {0} вернул {1} золота.",
            ["LogSoldUnit"] = "{0} продал юнита стоимостью {1} золота за {2} золота ({3}%).",
        };

        private static readonly Dictionary<string, string> Zh = new Dictionary<string, string>
        {
            ["AppTitle"] = "TABS Arena v1.1.5",
            ["Settings"] = "设置",
            ["Guide"] = "1v1 指南",
            ["Back"] = "← 返回",
            ["WindowMode"] = "窗口模式",
            ["Windowed"] = "窗口化",
            ["BorderlessFullscreen"] = "无边框全屏",
            ["Language"] = "语言",
            ["Sounds"] = "音效",
            ["Volume"] = "音量",
            ["On"] = "开",
            ["Off"] = "关",
            ["GuideTitle"] = "1v1 指南",
            ["GuideBasicsTitle"] = "基础",
            ["GuideBasicsBody"] = "每名玩家以 1200 金币开始。比赛开始时选择谁先行动。先手玩家会获得额外金币，用来补偿被针对选兵的劣势。",
            ["GuideTurnOrderTitle"] = "行动顺序",
            ["GuideTurnOrderBody"] = "第 1 回合按选择的先手玩家行动。之后，分数更高的玩家先行动。如果分数相同，则上一回合获胜的玩家先行动。",
            ["GuideRoundReplayTitle"] = "回合、平局和重赛查看",
            ["GuideRoundReplayBody"] = "战斗结束后，选择胜者并点击下一回合。如果双方都同意是平局，使用平局。若无法达成一致，使用 3 分钟计时器；没人获胜则强制平局。重赛查看花费 10 金币，每名玩家每回合只能购买一次。重赛查看仅用于信息参考，不会改变回合结果或胜者。",
            ["GuideSavingTitle"] = "保存",
            ["GuideSavingBody"] = "如果无法打完比赛，请在应用中保存。也要在 TABS 内使用 Save Battle 保存战斗，并启用 Save Friendly Units。",
            ["GuideEconomyTitle"] = "经济",
            ["GuideEconomyBody"] = "利息按玩家每 50 金币给予 +10 金币，最高 +100。购买收入在 FT30 中给予 +10，在 FT20 中给予 +13。FT10 移除收入购买和收入衰减。",
            ["GuideMoreTitle"] = "更多规则",
            ["GuideMoreBody"] = "想了解更多规则，请访问",
            ["ReplayUsed"] = "重赛已用",
            ["MainMenu"] = "← 主菜单",
            ["OverviewTitle"] = "1v1 比赛总览",
            ["OverviewSub"] = "使用下方控件，然后点击下一回合以应用利息、升级和支出。",
            ["CurrentRound"] = "当前回合",
            ["NextTurnOrder"] = "下回合顺序",
            ["PendingResult"] = "待定结果",
            ["NotAvailableYet"] = "暂不可用",
            ["NotSet"] = "未设置",
            ["TurnOrderRed"] = "红方 -> 蓝方",
            ["TurnOrderBlue"] = "蓝方 -> 红方",
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
            ["WhichPlayerFirst"] = "哪名玩家先行动？",
            ["MatchSaves"] = "比赛存档",
            ["Save"] = "💾 保存",
            ["Load"] = "📂 读取",
            ["Delete"] = "🗑 删除",
            ["NewGame"] = "🆕 新游戏",
            ["ActionLog"] = "行动日志",
            ["ActionLogSub"] = "商店点击和回合结果会按顺序显示在这里。",
            ["RoundControl"] = "回合控制",
            ["Player1Wins"] = "玩家 1 获胜",
            ["Player2Wins"] = "玩家 2 获胜",
            ["Tie"] = "平局",
            ["StartTieTimer"] = "开始平局计时器",
            ["StopTimer"] = "停止计时器",
            ["ResumeTimer"] = "继续计时器",
            ["RestartTimer"] = "重置计时器",
            ["NextRound"] = "下一回合",
            ["Undo"] = "撤销",
            ["Gold"] = "金币",
            ["Points"] = "分数",
            ["PermMv"] = "永久移动",
            ["Income"] = "收入",
            ["InterestStat"] = "利息",
            ["Set"] = "设定",
            ["Unset"] = "编辑",
            ["CustomTroopSpend"] = "自定义部队支出",
            ["UnitValue"] = "单位价值",
            ["Spend"] = "支出",
            ["Sell"] = "出售",
            ["Utility"] = "实用商店",
            ["Upgrades"] = "永久升级",
            ["Calculations"] = "最新计算",
            ["SingleTroopMove"] = "单个部队移动 ({0})",
            ["Replay"] = "重赛查看 (10)",
            ["P1FirstTurn"] = "玩家 1 先行动",
            ["P2FirstTurn"] = "玩家 2 先行动",
            ["DefaultP1Name"] = "玩家 1",
            ["DefaultP2Name"] = "玩家 2",
            ["MilestoneProgress"] = "里程碑进度",
            ["NextReward"] = "下一奖励",
            ["RewardsLeft"] = "剩余可得奖励",
            ["PointsAway"] = "分后获得",
            ["NextAt"] = "下一次在",
            ["PanelSub"] = "金币、分数、升级和支出控制。",
            ["BuyIncome"] = "购买收入 +10 ({0})",
            ["BuyIncomeF"] = "购买收入 +13 ({0})",
            ["BuyPermMove"] = "购买永久移动 +1 ({0})",
            ["BuyFaction"] = "购买阵营 ({0})",
            ["BuyChosenFaction"] = "购买指定阵营 ({0})",
            ["LogBoughtChosenFaction"] = "{0} 购买了指定阵营：{1}，花费 {2} 金币。",
            ["NoticeBoughtChosenFaction"] = "{0} 购买了 {1}，花费 {2} 金币。",
            ["NotEnoughGoldChosenFaction"] = "{0} 没有足够金币购买指定阵营 ({1})。",
            ["AllFactionsOwned"] = "{0} 已拥有所有阵营。",
            ["FactionDisabled"] = "阵营模式已关闭",
            ["SellUnit"] = "出售",
            ["PoolEmpty"] = "池为空",
            ["NoneLeft"] = "没有剩余",
            ["RewardChooseFreeFaction"] = "选择免费阵营",
            ["RewardFreeFaction"] = "免费阵营",
            ["ChooseFactionTitle"] = "选择免费阵营",
            ["ChooseFactionSub"] = "{0}，选择一个阵营免费解锁。",
            ["LogChoseFreeFaction"] = "里程碑：{0} 选择了免费阵营 - {1}。",
            ["NoticeChoseFreeFaction"] = "里程碑！{0} 免费选择了 {1}。",
            ["RewardPermMove"] = "永久移动升级",
            ["RewardSellback20"] = "出售返还 +20%",
            ["RewardIncomeDiscount"] = "收入折扣 (15%)",
            ["RewardFullRefund"] = "单位全额退款",
            ["LogWinnerMarked"] = "已标记胜者：{0}。",
            ["LogRoundWon"] = "第 {0} 回合结束。{1} 获胜。",
            ["LogRoundTie"] = "第 {0} 回合以平局结束。",
            ["SaveDialogTitle"] = "保存游戏",
            ["EnterSaveName"] = "输入存档名称：",
            ["SaveBtn"] = "保存",
            ["Cancel"] = "取消",
            ["Yes"] = "是",
            ["No"] = "否",
            ["OverwriteSaveTitle"] = "覆盖存档",
            ["OverwriteSaveMsg"] = "用当前比赛状态覆盖存档 \"{0}\"？",
            ["AlreadyExistsTitle"] = "已存在",
            ["AlreadyExistsMsg"] = "名为 \"{0}\" 的存档已存在。要覆盖吗？",
            ["SelectSaveFirst"] = "请先从下拉列表中选择一个存档。",
            ["SelectSaveDeleteFirst"] = "请选择要删除的存档。",
            ["SaveFileNotFound"] = "找不到存档文件。",
            ["CouldNotReadSave"] = "无法读取存档。",
            ["DeleteConfirmTitle"] = "删除存档",
            ["DeleteConfirmMsg"] = "删除 \"{0}\"？\n此操作无法撤销。",
            ["NewGameConfirmTitle"] = "新游戏",
            ["NewGameConfirmMsg"] = "开始新游戏？\n所有未保存进度都会丢失。",
            ["MatchEndTitle"] = "比赛完成",
            ["MatchEndMessage"] = "{0} 达到 {1} 分并赢得比赛。",
            ["MatchEndWinByTwoMessage"] = "{0}: {1}   {2}: {3}\n\n{4} 通过领先 2 分规则获胜。",
            ["MatchEndQuestion"] = "开始新游戏还是继续游玩？",
            ["NewGamePlain"] = "新游戏",
            ["ContinuePlaying"] = "继续",
            ["MainMenuConfirmTitle"] = "主菜单",
            ["MainMenuConfirmMsg"] = "返回主菜单？\n未保存进度会丢失。",
            ["CloseGameConfirmTitle"] = "关闭游戏",
            ["CloseGameConfirmMsg"] = "确定要关闭游戏吗？",
            ["StartingGold"] = "初始金币",
            ["MilestoneReward"] = "里程碑奖励",
            ["RoundReward"] = "回合奖励",
            ["PermanentIncome"] = "永久收入",
            ["FinalGold"] = "最终金币",
            ["LogFactionModeOn"] = "阵营模式已开启。两名玩家重置为该模式的初始金币，并获得 3 个随机阵营。",
            ["LogFactionModeOff"] = "阵营模式已关闭。",
            ["LogFT30ModeOn"] = "FT30 模式已开启。玩家面板已重置。",
            ["LogFT30ModeOff"] = "FT30 模式已关闭。已选择 FT20 模式。",
            ["LogFT10ModeOn"] = "FT10 模式已开启。玩家以 1200 金币开始，收入已禁用。",
            ["LogFT10ModeOff"] = "FT10 模式已关闭。已选择 FT20 模式。",
            ["LogGainedFaction"] = "{0} 获得阵营：{1}。",
            ["NoticeGainedFaction"] = "{0} 获得了 {1}。",
            ["LogBoughtIncome"] = "{0} 以 {2} 金币购买收入 +{1}。",
            ["LogBoughtPermMove"] = "{0} 以 {1} 金币购买了永久移动升级。",
            ["LogSingleTroopMove"] = "{0} 以 {1} 金币购买了单个部队移动。",
            ["LogReplay"] = "{0} 以 10 金币购买了重赛查看。",
            ["LogSpentTroops"] = "{0} 在部队上花费 {1} 金币。",
            ["WinsSuffix"] = "获胜",
            ["NothingToUndo"] = "没有可撤销的内容。",
            ["ChooseWinnerFirst"] = "进入下一回合前请选择胜者。",
            ["RoundWinNotice"] = "{0} 赢得第 {1} 回合！胜者 +{2}g，败者 +{3}g。",
            ["RoundTieNotice"] = "第 {0} 回合以平局结束。双方玩家 +{1}g。",
            ["MilestonePermMoveNotice"] = "里程碑！{0} 获得一次免费永久移动升级。",
            ["MilestoneSellbackNotice"] = "里程碑！{0} 的出售返还现在是 {1}%。",
            ["MilestoneIncomeDiscountNotice"] = "里程碑！{0} 下一次购买收入享受 15% 折扣。",
            ["MilestoneFullRefundNotice"] = "里程碑！{0} 下一次出售单位将获得全额退款。",
            ["NoRoundYet"] = "还没有回合。",
            ["LogUndo"] = "已撤销上一个动作。",
            ["LogSavedMatch"] = "比赛已保存为 \"{0}\"。",
            ["NoticeSavedAs"] = "已保存为 \"{0}\"。",
            ["LoadPreviewTitle"] = "读取 \"{0}\"？",
            ["LoadPreviewMsg"] = "已保存：   {0}\n\n⚔  {1}  对战  {2}\n\n回合：   {3}\n分数：   {4}  {5}  -  {6}  {7}",
            ["LogLoadedMatch"] = "已读取比赛 \"{0}\"。",
            ["NoticeLoadedSave"] = "已读取 \"{0}\"。",
            ["NoticeDeletedSave"] = "已删除存档 \"{0}\"。",
            ["LogNewGameStarted"] = "新游戏已开始。",
            ["LogMilestonePoolEmpty"] = "已达到里程碑，但奖励池为空 - 未发放奖励。",
            ["LogMilestoneChooseFreeFactionAllOwned"] = "里程碑：{0} 抽到选择免费阵营，但已拥有所有阵营。",
            ["NoticeMilestoneAllFactionsOwned"] = "里程碑！{0} 已拥有所有阵营 - 本次没有奖励。",
            ["LogMilestoneFreeFaction"] = "里程碑：{0} 获得免费阵营 - {1}。",
            ["NoticeMilestoneFreeFaction"] = "里程碑！{0} 获得免费阵营 - {1}！",
            ["LogMilestoneFreeFactionAllOwned"] = "里程碑：{0} 抽到免费阵营，但已拥有所有阵营。",
            ["LogMilestonePermMove"] = "里程碑：{0} 获得一次免费永久移动升级！",
            ["LogMilestoneSellback"] = "里程碑：{0} 的出售返还提高 20% -> {1}%。",
            ["LogMilestoneIncomeDiscount"] = "里程碑：{0} 获得一次性 15% 收入折扣！",
            ["LogMilestoneFullRefund"] = "里程碑：{0} 获得一次性部队全额退款！",
            ["FactionModeLocked"] = "阵营模式在第 1 回合后锁定。",
            ["MatchModeLocked"] = "比赛模式在第 1 回合后锁定。",
            ["NotEnoughGoldFaction"] = "{0} 没有足够金币购买阵营 ({1})。",
            ["NotEnoughGold"] = "{0} 没有足够金币。",
            ["NotEnoughGoldAmount"] = "{0} 没有足够金币 ({1})。",
            ["IncomeAlreadyBought"] = "{0} 本回合已经购买过收入。",
            ["MaxedPermMove"] = "{0} 已达到永久移动上限 ({1})。",
            ["ReplayAlreadyBought"] = "{0} 本回合已经购买过重赛查看。",
            ["EnterValidSpendAmount"] = "请输入有效的支出金额。",
            ["EnterValidUnitValue"] = "请先输入单位价值。",
            ["LogPlayerGoesFirst"] = "{0} 先行动并获得 50 金币。",
            ["LogFullRefundSell"] = "{0} 使用了全额退款 - 以完整 {1} 金币出售单位。",
            ["NoticeFullRefundSell"] = "已使用全额退款！{0} 返还 {1} 金币。",
            ["LogSoldUnit"] = "{0} 出售价值 {1} 金币的单位，获得 {2} 金币 ({3}%)。",
        };
        public MainWindow()
        {
            AppPrefs.Load();
            currentLanguage = FromSharedLanguage(AppPrefs.Language);
            TwoVTwoGameMode.Loc.Current = AppPrefs.Language;

            InitializeComponent();
            ApplyPlayerPanelTypography();
            if (IsNoRoundYetText(p1Calc)) p1Calc = T("NoRoundYet");
            if (IsNoRoundYetText(p2Calc)) p2Calc = T("NoRoundYet");
            InitializeZoom();
            SetupPlaceholders();
            SetupNumericInputBoxes();
            WindowStyle = WindowStyle.None;
            WindowState = WindowState.Normal;
            ResizeMode = ResizeMode.CanResize;

            noticeTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            noticeTimer.Tick += (s, e) =>
            {
                noticeTimer.Stop();
                if (IncomeNoticePopup != null) IncomeNoticePopup.IsOpen = false;
                lastNotice = "";
            };
            zoomIndicatorTimer = new DispatcherTimer { Interval = TimeSpan.FromSeconds(3) };
            zoomIndicatorTimer.Tick += (s, e) =>
            {
                zoomIndicatorTimer.Stop();
                FadeOutZoomIndicator();
            };
            tieTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(100) };
            tieTimer.Tick += TieTimer_Tick;
            tieTimerFlashTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(450) };
            tieTimerFlashTimer.Tick += TieTimerFlash_Tick;

            InitMilestoneRewardQueue();
            GrantStartingFactions();

            Loaded += (s, e) =>
            {
                Window_Loaded(s, e);
                ApplyWindowMode(AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen, false);
                UpdateZoomIndicatorPlacement();
            };

            Closing += (s, e) =>
            {
                AppPrefs.WindowMode = _isBorderlessFullscreen
                    ? SavedWindowMode.BorderlessFullscreen
                    : SavedWindowMode.Windowed;

                AppPrefs.Language = ToSharedLanguage(currentLanguage);
                AppPrefs.ZoomScale = GetCurrentZoom();

                AppPrefs.Save();
            };

            UpdateUI();
            UpdateStaticText();
            UpdateLanguageSelectorUI();
            UpdateSoundSettingsUI();
        }

        private void ApplyPlayerPanelTypography()
        {
            PlayerPanelText.ApplyButtonTypography(
                P1NameEditButton, P2NameEditButton,
                P1BuyIncomeButton, P2BuyIncomeButton,
                P1BuyPermMoveButton, P2BuyPermMoveButton,
                P1BuyFactionButton, P2BuyFactionButton,
                P1BuyChosenFactionButton, P2BuyChosenFactionButton,
                P1SingleTroopMoveButton, P2SingleTroopMoveButton,
                P1ReplayButton, P2ReplayButton,
                P1SpendButton, P2SpendButton,
                P1SellUnitButton, P2SellUnitButton);

            PlayerPanelText.ApplyTextSize(
                PlayerPanelText.StatLabelFontSize,
                P1LblGold, P1LblPoints, P1LblPermMove, P1LblIncome, P1LblInterest,
                P2LblGold, P2LblPoints, P2LblPermMove, P2LblIncome, P2LblInterest);

            PlayerPanelText.ApplyTextSize(
                PlayerPanelText.StatValueFontSize,
                P1GoldText, P1PointsText, P1UpgradesText, P1IncomeText, P1InterestText,
                P2GoldText, P2PointsText, P2UpgradesText, P2IncomeText, P2InterestText);
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
            ZoomIndicatorPopup.Placement = PlacementMode.Relative;
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
            zoomIndicatorTimer.Stop();
            zoomIndicatorTimer.Start();
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

        private void TieTimer_Tick(object sender, EventArgs e)
        {
            SyncTieTimerFromClock();
        }

        private void SyncTieTimerFromClock()
        {
            if (tieTimerEndsAtUtc == DateTime.MinValue)
                return;

            int newRemaining = (int)Math.Ceiling((tieTimerEndsAtUtc - DateTime.UtcNow).TotalSeconds);
            tieTimerRemainingSeconds = Math.Max(0, newRemaining);

            if (tieTimerRemainingSeconds <= 0)
            {
                tieTimer.Stop();
                tieTimerHasStarted = false;
                tieTimerEndsAtUtc = DateTime.MinValue;
                StartTieTimerFlash();
            }

            UpdateTieTimerUi();
        }

        private void TieTimerFlash_Tick(object sender, EventArgs e)
        {
            tieTimerFlashVisible = !tieTimerFlashVisible;
            if (TieTimerText != null)
                TieTimerText.Opacity = tieTimerFlashVisible ? 1.0 : 0.15;
        }

        private void TieTimerToggleButton_Click(object sender, RoutedEventArgs e)
        {
            if (tieTimer.IsEnabled)
            {
                SyncTieTimerFromClock();
                if (tieTimerRemainingSeconds <= 0)
                    return;

                tieTimer.Stop();
                tieTimerHasStarted = true;
                StopTieTimerFlash();
            }
            else
            {
                if (tieTimerRemainingSeconds <= 0)
                    tieTimerRemainingSeconds = TieTimerStartSeconds;

                bool isFreshStart = !tieTimerHasStarted && tieTimerRemainingSeconds == TieTimerStartSeconds;
                tieTimerHasStarted = true;
                StopTieTimerFlash();
                if (isFreshStart)
                    tieTimerRemainingSeconds--;

                tieTimerEndsAtUtc = DateTime.UtcNow.AddSeconds(tieTimerRemainingSeconds);
                if (tieTimerRemainingSeconds > 0)
                {
                    tieTimer.Start();
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
            if (tieTimer != null)
                tieTimer.Stop();

            tieTimerRemainingSeconds = TieTimerStartSeconds;
            tieTimerEndsAtUtc = DateTime.MinValue;
            tieTimerHasStarted = false;
            StopTieTimerFlash();
            UpdateTieTimerUi();
        }

        private void StartTieTimerFlash()
        {
            if (tieTimerFlashTimer == null)
                return;

            tieTimerFlashVisible = true;
            if (TieTimerText != null)
                TieTimerText.Opacity = 1.0;

            tieTimerFlashTimer.Stop();
            tieTimerFlashTimer.Start();
        }

        private void StopTieTimerFlash()
        {
            if (tieTimerFlashTimer != null)
                tieTimerFlashTimer.Stop();

            tieTimerFlashVisible = true;
            if (TieTimerText != null)
                TieTimerText.Opacity = 1.0;
        }

        private void UpdateTieTimerUi()
        {
            if (TieTimerText != null)
                TieTimerText.Text = FormatTieTimer(tieTimerRemainingSeconds);

            if (TieTimerToggleButton != null)
                TieTimerToggleButton.Content = tieTimer != null && tieTimer.IsEnabled
                    ? T("StopTimer")
                    : tieTimerHasStarted ? T("ResumeTimer") : T("StartTieTimer");

            if (TieTimerRestartButton != null)
                TieTimerRestartButton.Content = T("RestartTimer");
        }

        private static string FormatTieTimer(int seconds)
        {
            seconds = Math.Max(0, seconds);
            return string.Format("{0}:{1:00}", seconds / 60, seconds % 60);
        }

        private void SetMilestoneProgressText(string p1DisplayName, int p1Away, string p2DisplayName, int p2Away)
        {
            if (Ft20PoolStatusText == null) return;

            Ft20PoolStatusText.Inlines.Clear();
            AddMilestoneProgressLine(p1MilestoneFlagBrush, p1DisplayName, p1Away);
            Ft20PoolStatusText.Inlines.Add(new LineBreak());
            AddMilestoneProgressLine(p2MilestoneFlagBrush, p2DisplayName, p2Away);
        }

        private void AddMilestoneProgressLine(Brush flagBrush, string displayName, int pointsAway)
        {
            Ft20PoolStatusText.Inlines.Add(PlayerPanelText.CreateFlagInline(flagBrush, 35, 31, new Thickness(0, 0, 10, -7)));
            Ft20PoolStatusText.Inlines.Add(new Run(string.Format("{0}:  ", displayName)));
            Ft20PoolStatusText.Inlines.Add(PlayerPanelText.CreateOutlinedTextInline(pointsAway.ToString(), 26, new Thickness(1, 4, 3, -4), milestoneNumberBrush));
            Ft20PoolStatusText.Inlines.Add(new Run(" " + T("PointsAway")));
        }

        private void UpdateTurnOrderText()
        {
            int first = 0;

            if (round == 1 && firstTurnChosen)
                first = firstTurnPlayer;
            else if (p1Points > p2Points)
                first = 1;
            else if (p2Points > p1Points)
                first = 2;
            else if (lastRoundWinner == 1 || lastRoundWinner == 2)
                first = lastRoundWinner;

            TurnOrderText.Text = first == 1 ? T("TurnOrderRed")
                  : first == 2 ? T("TurnOrderBlue")
                  : T("NotAvailableYet");
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) { RefreshSavesDropdown(); }
        private void PushUndoState() { undoStack.Push(CaptureState()); }

        private void NormalizeMatchModeFlags()
        {
            if (ft10ModeEnabled)
            {
                ft30ModeEnabled = false;
                ft20ModeEnabled = false;
            }
            else if (ft30ModeEnabled)
            {
                ft20ModeEnabled = false;
            }
            else
            {
                ft20ModeEnabled = true;
            }
        }

        private int GetStartingGold()
        {
            return 1200;
        }

        private int GetRoundRewardTier()
        {
            if (ft10ModeEnabled) return ((round - 1) / 2) * 40;
            if (ft20ModeEnabled) return ((round - 1) / 3) * 15;
            return ((round - 1) / 5) * 10;
        }

        private int GetWinnerRewardBase()
        {
            if (ft10ModeEnabled) return 95;
            if (ft20ModeEnabled) return 75;
            return 55;
        }

        private int GetLoserRewardBase()
        {
            if (ft10ModeEnabled) return 125;
            if (ft20ModeEnabled) return 105;
            return 85;
        }

        private int GetTieRewardBase()
        {
            return (GetWinnerRewardBase() + GetLoserRewardBase()) / 2;
        }

        private int GetMilestoneStep()
        {
            if (ft10ModeEnabled) return 2;
            if (ft20ModeEnabled) return 4;
            return 5;
        }

        private int GetPermMoveCost()
        {
            if (ft10ModeEnabled) return 125;
            if (ft20ModeEnabled) return 175;
            return 200;
        }

        private int GetSingleTroopMoveCost()
        {
            return ft10ModeEnabled ? 20 : 25;
        }

        private bool IsIncomeAvailable()
        {
            return !ft10ModeEnabled;
        }

        private string[] GetRewardPoolForCurrentMode()
        {
            if (ft10ModeEnabled)
                return factionModeEnabled ? FactionRewardPoolNoIncome : BaseRewardPoolNoIncome;

            return factionModeEnabled ? FactionRewardPool : BaseRewardPool;
        }

        private void ResetEconomyForModeSwitch()
        {
            p1Gold = GetStartingGold();
            p2Gold = GetStartingGold();
            p1Income = 0; p2Income = 0;
            p1IncomeUpgrades = 0; p2IncomeUpgrades = 0;
            p1IncomeLevel = 0; p2IncomeLevel = 0;
            p1IncomeCost = GetBaseIncomeCost(); p2IncomeCost = GetBaseIncomeCost();
            p1BoughtIncomeThisRound = false; p2BoughtIncomeThisRound = false;
            p1HasIncomeDiscount = false; p2HasIncomeDiscount = false;
            p1MissedIncomeRounds = 0; p2MissedIncomeRounds = 0;
            p1IncomeDecayPercent = 0; p2IncomeDecayPercent = 0;
            p1PermMoveUpgrades = 0; p2PermMoveUpgrades = 0;
            p1MilestonePermMoveUpgrades = 0; p2MilestonePermMoveUpgrades = 0;
            p1HasFullRefund = false; p2HasFullRefund = false;
            p1HasFt10PermMove = false; p2HasFt10PermMove = false;
            p1SellbackPct = 50; p2SellbackPct = 50;
            p1Sellback70 = false; p2Sellback70 = false;
            p1FactionPurchases = 0; p2FactionPurchases = 0;
            p1ChosenFactionPurchases = 0; p2ChosenFactionPurchases = 0;
            globalClaimedMilestones.Clear();
            milestone5Claimed = false; milestone10Claimed = false; milestone15Claimed = false;
            milestone20Claimed = false; milestone25Claimed = false;
            ft20MilestonePool.Clear();
            ft20NextMilestoneRound = GetMilestoneStep();

            if (factionModeEnabled) GrantStartingFactions();
            else { p1Factions.Clear(); p2Factions.Clear(); }

            InitMilestoneRewardQueue();
        }

        // Pre-roll and shuffle the full reward queue
        private void InitMilestoneRewardQueue()
        {
            var rng = new Random(Guid.NewGuid().GetHashCode());
            var pool = GetRewardPoolForCurrentMode();
            milestoneRewardQueue = pool.OrderBy(_ => rng.Next()).ToList();
        }

        private string GetNextRewardLabel()
        {
            if (milestoneRewardQueue == null || milestoneRewardQueue.Count == 0) return T("NoneLeft");
            return RewardLabel(milestoneRewardQueue[0]);
        }

        private string GetNextRewardIcon()
        {
            if (milestoneRewardQueue == null || milestoneRewardQueue.Count == 0) return "🏆";
            return RewardIcon(milestoneRewardQueue[0]);
        }

        private string RewardKey(string key)
        {
            return string.Format("{0} {1}", RewardIcon(key), RewardLabel(key));
        }

        private string RewardIcon(string key)
        {
            switch (key)
            {
                case "choose_free_faction": return "🎯";
                case "free_faction": return "🎲";
                case "perm_move_upgrade": return "⚔";
                case "sellback_20": return "💰";
                case "income_discount": return "📉";
                case "full_refund": return "🔁";
                default: return "★";
            }
        }

        private string RewardLabel(string key)
        {
            switch (key)
            {
                case "choose_free_faction": return T("RewardChooseFreeFaction");
                case "free_faction": return T("RewardFreeFaction");
                case "perm_move_upgrade": return T("RewardPermMove");
                case "sellback_20": return T("RewardSellback20");
                case "income_discount": return T("RewardIncomeDiscount");
                case "full_refund": return T("RewardFullRefund");
                default: return key;
            }
        }

        private string BuildRewardPoolText()
        {
            if (milestoneRewardQueue == null || milestoneRewardQueue.Count == 0)
                return T("PoolEmpty");

            var counts = new Dictionary<string, int>();
            foreach (var r in milestoneRewardQueue)
            {
                if (!counts.ContainsKey(r)) counts[r] = 0;
                counts[r]++;
            }

            // Display in a fixed order
            var order = new[] { "choose_free_faction", "free_faction", "perm_move_upgrade", "sellback_20", "income_discount", "full_refund" };
            var lines = new List<string>();
            foreach (var k in order)
                if (counts.ContainsKey(k))
                    lines.Add(string.Format("{0}x  {1}", counts[k], RewardKey(k)));

            return string.Join("\n", lines);
        }

        private GameState CaptureState()
        {
            return new GameState
            {
                round = round,
                pendingWinner = pendingWinner,
                namesLocked = namesLocked,
                resetArmed = resetArmed,
                firstTurnChosen = firstTurnChosen,
                p1Gold = p1Gold,
                p2Gold = p2Gold,
                p1Points = p1Points,
                p2Points = p2Points,
                p1Income = p1Income,
                p2Income = p2Income,
                p1PermMoveUpgrades = p1PermMoveUpgrades,
                p2PermMoveUpgrades = p2PermMoveUpgrades,
                p1MilestonePermMoveUpgrades = p1MilestonePermMoveUpgrades,
                p2MilestonePermMoveUpgrades = p2MilestonePermMoveUpgrades,
                p1IncomeUpgrades = p1IncomeUpgrades,
                p2IncomeUpgrades = p2IncomeUpgrades,
                p1IncomeLevel = p1IncomeLevel,
                p2IncomeLevel = p2IncomeLevel,
                p1IncomeCost = p1IncomeCost,
                p2IncomeCost = p2IncomeCost,
                p1BoughtIncomeThisRound = p1BoughtIncomeThisRound,
                p2BoughtIncomeThisRound = p2BoughtIncomeThisRound,
                p1HasIncomeDiscount = p1HasIncomeDiscount,
                p2HasIncomeDiscount = p2HasIncomeDiscount,
                p1HasFullRefund = p1HasFullRefund,
                p2HasFullRefund = p2HasFullRefund,
                p1Name = p1Name,
                p2Name = p2Name,
                p1Calc = p1Calc,
                p2Calc = p2Calc,
                p1HasFt10PermMove = p1HasFt10PermMove,
                p2HasFt10PermMove = p2HasFt10PermMove,
                milestone5Claimed = milestone5Claimed,
                milestone10Claimed = milestone10Claimed,
                milestone15Claimed = milestone15Claimed,
                milestone20Claimed = milestone20Claimed,
                milestone25Claimed = milestone25Claimed,
                globalClaimedMilestones = new HashSet<int>(globalClaimedMilestones),
                milestoneRewardQueue = new List<string>(milestoneRewardQueue),
                p1SellbackPct = p1SellbackPct,
                p2SellbackPct = p2SellbackPct,
                p1Sellback70 = p1Sellback70,
                p2Sellback70 = p2Sellback70,
                p1MissedIncomeRounds = p1MissedIncomeRounds,
                p2MissedIncomeRounds = p2MissedIncomeRounds,
                p1IncomeDecayPercent = p1IncomeDecayPercent,
                p2IncomeDecayPercent = p2IncomeDecayPercent,
                factionModeEnabled = factionModeEnabled,
                factionModeLocked = factionModeLocked,
                p1FactionPurchases = p1FactionPurchases,
                p2FactionPurchases = p2FactionPurchases,
                p1ChosenFactionPurchases = p1ChosenFactionPurchases,
                p2ChosenFactionPurchases = p2ChosenFactionPurchases,
                p1Factions = new List<string>(p1Factions),
                p2Factions = new List<string>(p2Factions),
                ft20ModeEnabled = ft20ModeEnabled,
                ft10ModeEnabled = ft10ModeEnabled,
                ft30ModeEnabled = ft30ModeEnabled,
                ft20ModeLocked = ft20ModeLocked,
                matchEndPromptSuppressed = matchEndPromptSuppressed,
                ft20MilestonePool = new List<string>(ft20MilestonePool),
                ft20NextMilestoneRound = ft20NextMilestoneRound,
                actionLog = new List<string>(actionLog),
                lastRoundWinner = lastRoundWinner,
                firstTurnPlayer = firstTurnPlayer,
                p1ReplayBoughtThisRound = p1ReplayBoughtThisRound,
                p2ReplayBoughtThisRound = p2ReplayBoughtThisRound,
            };
        }

        private void RestoreState(GameState s)
        {
            round = s.round; pendingWinner = s.pendingWinner; namesLocked = s.namesLocked;
            resetArmed = s.resetArmed; firstTurnChosen = s.firstTurnChosen;
            p1Gold = s.p1Gold; p2Gold = s.p2Gold; p1Points = s.p1Points; p2Points = s.p2Points;
            p1Income = s.p1Income; p2Income = s.p2Income;
            p1PermMoveUpgrades = s.p1PermMoveUpgrades; p2PermMoveUpgrades = s.p2PermMoveUpgrades;
            p1MilestonePermMoveUpgrades = s.p1MilestonePermMoveUpgrades;
            p2MilestonePermMoveUpgrades = s.p2MilestonePermMoveUpgrades;
            p1IncomeUpgrades = s.p1IncomeUpgrades; p2IncomeUpgrades = s.p2IncomeUpgrades;
            p1IncomeLevel = s.p1IncomeLevel; p2IncomeLevel = s.p2IncomeLevel;
            p1IncomeCost = s.p1IncomeCost; p2IncomeCost = s.p2IncomeCost;
            p1BoughtIncomeThisRound = s.p1BoughtIncomeThisRound;
            p2BoughtIncomeThisRound = s.p2BoughtIncomeThisRound;
            p1HasIncomeDiscount = s.p1HasIncomeDiscount; p2HasIncomeDiscount = s.p2HasIncomeDiscount;
            p1HasFullRefund = s.p1HasFullRefund; p2HasFullRefund = s.p2HasFullRefund;
            p1Name = s.p1Name; p2Name = s.p2Name; p1Calc = s.p1Calc; p2Calc = s.p2Calc;
            p1HasFt10PermMove = s.p1HasFt10PermMove; p2HasFt10PermMove = s.p2HasFt10PermMove;
            milestone5Claimed = s.milestone5Claimed; milestone10Claimed = s.milestone10Claimed;
            milestone15Claimed = s.milestone15Claimed; milestone20Claimed = s.milestone20Claimed;
            milestone25Claimed = s.milestone25Claimed;
            globalClaimedMilestones = s.globalClaimedMilestones ?? new HashSet<int>();
            milestoneRewardQueue = s.milestoneRewardQueue ?? new List<string>();
            p1SellbackPct = s.p1SellbackPct > 0 ? s.p1SellbackPct : 50;
            p2SellbackPct = s.p2SellbackPct > 0 ? s.p2SellbackPct : 50;
            p1Sellback70 = s.p1Sellback70; p2Sellback70 = s.p2Sellback70;
            p1MissedIncomeRounds = s.p1MissedIncomeRounds;
            p2MissedIncomeRounds = s.p2MissedIncomeRounds;
            p1IncomeDecayPercent = s.p1IncomeDecayPercent;
            p2IncomeDecayPercent = s.p2IncomeDecayPercent;
            factionModeEnabled = s.factionModeEnabled; factionModeLocked = s.factionModeLocked;
            p1FactionPurchases = s.p1FactionPurchases; p2FactionPurchases = s.p2FactionPurchases;
            p1ChosenFactionPurchases = s.p1ChosenFactionPurchases; p2ChosenFactionPurchases = s.p2ChosenFactionPurchases;
            p1Factions = s.p1Factions ?? new List<string>();
            p2Factions = s.p2Factions ?? new List<string>();
            ft20ModeEnabled = s.ft20ModeEnabled;
            ft10ModeEnabled = s.ft10ModeEnabled;
            ft30ModeEnabled = s.ft30ModeEnabled;
            NormalizeMatchModeFlags();
            ft20ModeLocked = s.ft20ModeLocked;
            matchEndPromptSuppressed = s.matchEndPromptSuppressed;
            ft20MilestonePool = s.ft20MilestonePool ?? new List<string>();
            ft20NextMilestoneRound = s.ft20NextMilestoneRound > 0 ? s.ft20NextMilestoneRound : GetMilestoneStep();
            lastRoundWinner = s.lastRoundWinner;
            firstTurnPlayer = s.firstTurnPlayer;
            p1ReplayBoughtThisRound = s.p1ReplayBoughtThisRound;
            p2ReplayBoughtThisRound = s.p2ReplayBoughtThisRound;
            actionLog.Clear();
            if (s.actionLog != null) foreach (var item in s.actionLog) actionLog.AddLast(item);
            UpdateUI();
        }

        private void UndoButton_Click(object sender, RoutedEventArgs e)
        {
            if (undoStack.Count == 0) { ShowNotice(T("NothingToUndo")); return; }
            RestoreState(undoStack.Pop());
            AddActionLog(T("LogUndo"));
        }

        private void RefreshSavesDropdown()
        {
            if (!Directory.Exists(SaveFolder)) Directory.CreateDirectory(SaveFolder);
            var saves = Directory.GetFiles(SaveFolder, "*.json")
                                 .Select(f => Path.GetFileNameWithoutExtension(f))
                                 .OrderBy(n => n).ToList();
            SavesDropdown.ItemsSource = null;
            SavesDropdown.ItemsSource = saves;
            if (_currentSaveName != null && saves.Contains(_currentSaveName))
                SavesDropdown.SelectedItem = _currentSaveName;
        }

        private void SavesDropdown_SelectionChanged(object sender, SelectionChangedEventArgs e) { }

        private void SaveButton_Click(object sender, RoutedEventArgs e)
        {
            if (!string.IsNullOrEmpty(_currentSaveName))
            {
                if (ShowConfirm(
    T("OverwriteSaveTitle"),
string.Format(T("OverwriteSaveMsg"), _currentSaveName)))
                {
                    WriteSave(_currentSaveName);
                }
                return;
            }
            PromptAndSave();
        }

        private void PromptAndSave()
        {
            var dlg = new SaveNameDialog(T("SaveDialogTitle"), T("EnterSaveName"), T("SaveBtn"), T("Cancel")) { Owner = this };
            if (dlg.ShowDialog() != true) return;
            var name = (dlg.SaveName ?? "").Trim();
            if (string.IsNullOrWhiteSpace(name)) return;
            var path = Path.Combine(SaveFolder, name + ".json");
            if (File.Exists(path))
            {
                if (!ShowConfirm(
    T("AlreadyExistsTitle"),
string.Format(T("AlreadyExistsMsg"), name)))
                {
                    return;
                }
            }
            WriteSave(name);
            _currentSaveName = name;
        }

        public class SaveNameDialog : Window
        {
            public string SaveName { get; private set; }

            public SaveNameDialog(string titleText, string labelText, string saveText, string cancelText)
            {
                Title = titleText;
                Width = 380;
                Height = 230;
                MinWidth = 380;
                MinHeight = 230;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;

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
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                var title = new TextBlock
                {
                    Text = titleText,
                    Foreground = Brushes.White,
                    FontSize = 15,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 12)
                };
                Grid.SetRow(title, 0);
                root.Children.Add(title);

                var label = new TextBlock
                {
                    Text = labelText,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 0, 10)
                };
                Grid.SetRow(label, 1);
                root.Children.Add(label);

                var box = new TextBox
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
                Grid.SetRow(box, 2);
                root.Children.Add(box);

                var row = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right
                };

                var save = new Button
                {
                    Content = saveText,
                    Width = 92,
                    Height = 34,
                    Background = new SolidColorBrush(Color.FromRgb(40, 90, 52)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 0, 8, 0)
                };

                var cancel = new Button
                {
                    Content = cancelText,
                    Width = 92,
                    Height = 34,
                    Background = new SolidColorBrush(Color.FromRgb(96, 48, 48)),
                    Foreground = Brushes.White,
                    BorderThickness = new Thickness(0),
                    FontWeight = FontWeights.SemiBold
                };

                save.Click += (s, e) =>
                {
                    string name = box.Text.Trim();
                    if (string.IsNullOrWhiteSpace(name)) return;
                    SaveName = name;
                    DialogResult = true;
                };

                cancel.Click += (s, e) => DialogResult = false;

                row.Children.Add(save);
                row.Children.Add(cancel);
                Grid.SetRow(row, 4);
                root.Children.Add(row);

                outerBorder.Child = root;
                Content = outerBorder;

                Loaded += (s, e) => box.Focus();

                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { save.RaiseEvent(new RoutedEventArgs(Button.ClickEvent)); e.Handled = true; }
                    if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
                };
            }
        }

        public class ThemedConfirmDialog : Window
        {
            public ThemedConfirmDialog(string title, string message, string yesText = "Yes", string noText = "No")
            {
                Title = title;
                Width = 420;
                Height = 220;
                MinWidth = 420;
                MinHeight = 220;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;

                var outerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(26, 29, 35)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 70)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(12)
                };

                var root = new Grid { Margin = new Thickness(20) };
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });

                root.Children.Add(new TextBlock
                {
                    Text = title,
                    Foreground = Brushes.White,
                    FontSize = 16,
                    FontWeight = FontWeights.Bold,
                    Margin = new Thickness(0, 0, 0, 12)
                });

                var msg = new TextBlock
                {
                    Text = message,
                    Foreground = new SolidColorBrush(Color.FromRgb(180, 190, 200)),
                    FontSize = 13,
                    TextWrapping = TextWrapping.Wrap,
                    LineHeight = 19
                };
                Grid.SetRow(msg, 1);
                root.Children.Add(msg);

                var buttons = new StackPanel
                {
                    Orientation = Orientation.Horizontal,
                    HorizontalAlignment = HorizontalAlignment.Right,
                    Margin = new Thickness(0, 16, 0, 0)
                };

                var yes = new Button
                {
                    Content = yesText,
                    Width = 92,
                    Height = 34,
                    Background = new SolidColorBrush(Color.FromRgb(40, 90, 52)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0),
                    Margin = new Thickness(0, 0, 8, 0)
                };
                yes.Click += (s, e) => DialogResult = true;

                var no = new Button
                {
                    Content = noText,
                    Width = 92,
                    Height = 34,
                    Background = new SolidColorBrush(Color.FromRgb(96, 48, 48)),
                    Foreground = Brushes.White,
                    FontWeight = FontWeights.SemiBold,
                    BorderThickness = new Thickness(0)
                };
                no.Click += (s, e) => DialogResult = false;

                buttons.Children.Add(yes);
                buttons.Children.Add(no);

                Grid.SetRow(buttons, 2);
                root.Children.Add(buttons);

                outerBorder.Child = root;
                Content = outerBorder;

                KeyDown += (s, e) =>
                {
                    if (e.Key == Key.Enter) { DialogResult = true; e.Handled = true; }
                    if (e.Key == Key.Escape) { DialogResult = false; e.Handled = true; }
                };
            }
        }

        public class FactionChoiceDialog : Window
        {
            public string SelectedFaction { get; private set; }

            public FactionChoiceDialog(string titleText, string subtitleText, List<string> factions, Dictionary<string, string> iconMap)
            {
                Title = titleText;
                Width = 680;
                Height = 520;
                MinWidth = 680;
                MinHeight = 520;
                WindowStartupLocation = WindowStartupLocation.CenterOwner;
                ResizeMode = ResizeMode.NoResize;
                WindowStyle = WindowStyle.None;
                AllowsTransparency = true;
                Background = Brushes.Transparent;

                var outerBorder = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(22, 24, 27)),
                    BorderBrush = new SolidColorBrush(Color.FromRgb(50, 58, 70)),
                    BorderThickness = new Thickness(1.5),
                    CornerRadius = new CornerRadius(16)
                };

                var root = new Grid();
                root.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                root.RowDefinitions.Add(new RowDefinition { Height = new GridLength(1, GridUnitType.Star) });

                var header = new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(35, 39, 47)),
                    CornerRadius = new CornerRadius(16, 16, 0, 0),
                    Padding = new Thickness(18, 14, 18, 14)
                };

                var headerStack = new StackPanel();
                headerStack.Children.Add(new TextBlock
                {
                    Text = titleText,
                    Foreground = Brushes.White,
                    FontSize = 19,
                    FontWeight = FontWeights.Bold
                });
                headerStack.Children.Add(new TextBlock
                {
                    Text = subtitleText,
                    Foreground = new SolidColorBrush(Color.FromRgb(167, 172, 178)),
                    FontSize = 13,
                    FontWeight = FontWeights.SemiBold,
                    Margin = new Thickness(0, 5, 0, 0),
                    TextWrapping = TextWrapping.Wrap
                });

                header.Child = headerStack;
                Grid.SetRow(header, 0);
                root.Children.Add(header);

                var scroll = new ScrollViewer
                {
                    Padding = new Thickness(18),
                    VerticalScrollBarVisibility = ScrollBarVisibility.Auto,
                    HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled
                };

                scroll.Resources.Add(typeof(ScrollBar), CreateDarkScrollBarStyle());

                var wrap = new WrapPanel
                {
                    Orientation = Orientation.Horizontal
                };

                foreach (var faction in factions)
                {
                    var button = new Button
                    {
                        Width = 144,
                        Height = 112,
                        Margin = new Thickness(0, 0, 10, 10),
                        Background = new SolidColorBrush(Color.FromRgb(35, 39, 47)),
                        BorderBrush = new SolidColorBrush(Color.FromRgb(52, 64, 77)),
                        BorderThickness = new Thickness(1),
                        Foreground = Brushes.White,
                        Cursor = Cursors.Hand,
                        Padding = new Thickness(8)
                    };

                    var stack = new StackPanel
                    {
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center
                    };

                    var iconBorder = new Border
                    {
                        Width = 52,
                        Height = 52,
                        CornerRadius = new CornerRadius(10),
                        Background = new SolidColorBrush(Color.FromRgb(26, 28, 31)),
                        ClipToBounds = true,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        Margin = new Thickness(0, 0, 0, 8)
                    };

                    try
                    {
                        string file = iconMap.ContainsKey(faction) ? iconMap[faction] : null;
                        if (!string.IsNullOrWhiteSpace(file))
                        {
                            iconBorder.Child = new Image
                            {
                                Stretch = Stretch.Uniform,
                                Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute))
                            };
                        }
                    }
                    catch { }

                    if (iconBorder.Child == null)
                    {
                        iconBorder.Child = new TextBlock
                        {
                            Text = faction.Substring(0, 1),
                            Foreground = Brushes.White,
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center,
                            FontWeight = FontWeights.Bold,
                            FontSize = 22
                        };
                    }

                    stack.Children.Add(iconBorder);
                    stack.Children.Add(new TextBlock
                    {
                        Text = faction,
                        Foreground = Brushes.White,
                        FontSize = 12,
                        FontWeight = FontWeights.Bold,
                        TextAlignment = TextAlignment.Center,
                        TextWrapping = TextWrapping.Wrap,
                        HorizontalAlignment = HorizontalAlignment.Center
                    });

                    button.Content = stack;
                    button.Click += (s, e) =>
                    {
                        SelectedFaction = faction;
                        DialogResult = true;
                    };

                    wrap.Children.Add(button);
                }

                scroll.Content = wrap;
                Grid.SetRow(scroll, 1);
                root.Children.Add(scroll);

                outerBorder.Child = root;
                Content = outerBorder;
            }

            private static Style CreateDarkScrollBarStyle()
            {
                const string xaml =
@"<Style xmlns=""http://schemas.microsoft.com/winfx/2006/xaml/presentation""
        xmlns:x=""http://schemas.microsoft.com/winfx/2006/xaml""
        TargetType=""ScrollBar"">
    <Setter Property=""Width"" Value=""10""/>
    <Setter Property=""Background"" Value=""#17181A""/>
    <Setter Property=""Template"">
        <Setter.Value>
            <ControlTemplate TargetType=""ScrollBar"">
                <Grid Background=""{TemplateBinding Background}"">
                    <Track x:Name=""PART_Track"" IsDirectionReversed=""True"" Focusable=""False"">
                        <Track.DecreaseRepeatButton>
                            <RepeatButton Command=""ScrollBar.LineUpCommand""
                                          Opacity=""0""
                                          Focusable=""False""
                                          Height=""0""/>
                        </Track.DecreaseRepeatButton>
                        <Track.IncreaseRepeatButton>
                            <RepeatButton Command=""ScrollBar.LineDownCommand""
                                          Opacity=""0""
                                          Focusable=""False""
                                          Height=""0""/>
                        </Track.IncreaseRepeatButton>
                        <Track.Thumb>
                            <Thumb Background=""#4A5058"">
                                <Thumb.Template>
                                    <ControlTemplate TargetType=""Thumb"">
                                        <Border Background=""{TemplateBinding Background}""
                                                CornerRadius=""5""
                                                Margin=""2""/>
                                    </ControlTemplate>
                                </Thumb.Template>
                            </Thumb>
                        </Track.Thumb>
                    </Track>
                </Grid>
            </ControlTemplate>
        </Setter.Value>
    </Setter>
</Style>";

                return (Style)System.Windows.Markup.XamlReader.Parse(xaml);
            }
        }

        private void WriteSave(string name)
        {
            if (!Directory.Exists(SaveFolder)) Directory.CreateDirectory(SaveFolder);
            var data = new OneV1SaveData
            {
                SaveVersion = 6,
                SaveName = name,
                SavedAt = DateTime.Now,
                Round = round,
                PendingWinner = pendingWinner,
                NamesLocked = namesLocked,
                ResetArmed = resetArmed,
                FirstTurnChosen = firstTurnChosen,
                P1Gold = p1Gold,
                P2Gold = p2Gold,
                P1Points = p1Points,
                P2Points = p2Points,
                P1Income = p1Income,
                P2Income = p2Income,
                P1PermMoveUpgrades = p1PermMoveUpgrades,
                P2PermMoveUpgrades = p2PermMoveUpgrades,
                P1MilestonePermMoveUpgrades = p1MilestonePermMoveUpgrades,
                P2MilestonePermMoveUpgrades = p2MilestonePermMoveUpgrades,
                P1IncomeUpgrades = p1IncomeUpgrades,
                P2IncomeUpgrades = p2IncomeUpgrades,
                P1IncomeLevel = p1IncomeLevel,
                P2IncomeLevel = p2IncomeLevel,
                P1IncomeCost = p1IncomeCost,
                P2IncomeCost = p2IncomeCost,
                P1BoughtIncomeThisRound = p1BoughtIncomeThisRound,
                P2BoughtIncomeThisRound = p2BoughtIncomeThisRound,
                P1HasIncomeDiscount = p1HasIncomeDiscount,
                P2HasIncomeDiscount = p2HasIncomeDiscount,
                P1HasFullRefund = p1HasFullRefund,
                P2HasFullRefund = p2HasFullRefund,
                P1Name = p1Name,
                P2Name = p2Name,
                P1Calc = p1Calc,
                P2Calc = p2Calc,
                P1HasFt10PermMove = p1HasFt10PermMove,
                P2HasFt10PermMove = p2HasFt10PermMove,
                Milestone5Claimed = milestone5Claimed,
                Milestone10Claimed = milestone10Claimed,
                Milestone15Claimed = milestone15Claimed,
                Milestone20Claimed = milestone20Claimed,
                Milestone25Claimed = milestone25Claimed,
                GlobalClaimedMilestones = new List<int>(globalClaimedMilestones),
                MilestoneRewardQueue = new List<string>(milestoneRewardQueue),
                P1SellbackPct = p1SellbackPct,
                P2SellbackPct = p2SellbackPct,
                P1Sellback70 = p1Sellback70,
                P2Sellback70 = p2Sellback70,
                P1MissedIncomeRounds = p1MissedIncomeRounds,
                P2MissedIncomeRounds = p2MissedIncomeRounds,
                P1IncomeDecayPercent = p1IncomeDecayPercent,
                P2IncomeDecayPercent = p2IncomeDecayPercent,
                FactionModeEnabled = factionModeEnabled,
                FactionModeLocked = factionModeLocked,
                P1FactionPurchases = p1FactionPurchases,
                P2FactionPurchases = p2FactionPurchases,
                P1ChosenFactionPurchases = p1ChosenFactionPurchases,
                P2ChosenFactionPurchases = p2ChosenFactionPurchases,
                P1Factions = new List<string>(p1Factions),
                P2Factions = new List<string>(p2Factions),
                Ft20ModeEnabled = ft20ModeEnabled,
                Ft10ModeEnabled = ft10ModeEnabled,
                Ft30ModeEnabled = ft30ModeEnabled,
                Ft20ModeLocked = ft20ModeLocked,
                MatchEndPromptSuppressed = matchEndPromptSuppressed,
                Ft20MilestonePool = new List<string>(ft20MilestonePool),
                Ft20NextMilestoneRound = ft20NextMilestoneRound,
                ActionLog = new List<string>(actionLog),
                LastRoundWinner = lastRoundWinner,
                FirstTurnPlayer = firstTurnPlayer,
                P1ReplayBoughtThisRound = p1ReplayBoughtThisRound,
                P2ReplayBoughtThisRound = p2ReplayBoughtThisRound,

            };
            File.WriteAllText(Path.Combine(SaveFolder, name + ".json"),
                JsonConvert.SerializeObject(data, Formatting.Indented));
            _currentSaveName = name;
            RefreshSavesDropdown();
            AddActionLog(string.Format(T("LogSavedMatch"), name));
            ShowNotice(string.Format(T("NoticeSavedAs"), name));
        }

        private void LoadButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SavesDropdown.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) { ShowNotice(T("SelectSaveFirst")); return; }
            ShowLoadPreview(selected);
        }

        private void ShowLoadPreview(string name)
        {
            var path = Path.Combine(SaveFolder, name + ".json");
            if (!File.Exists(path)) { ShowNotice(T("SaveFileNotFound")); RefreshSavesDropdown(); return; }
            OneV1SaveData data;
            try { data = JsonConvert.DeserializeObject<OneV1SaveData>(File.ReadAllText(path)); }
            catch { ShowNotice(T("CouldNotReadSave")); return; }
            var msg = string.Format(
                T("LoadPreviewMsg"),
                data.SavedAt.ToString("MM/dd/yyyy  h:mm tt"),
                data.P1Name, data.P2Name, data.Round,
                data.P1Name, data.P1Points, data.P2Name, data.P2Points);
            if (ShowConfirm(string.Format(T("LoadPreviewTitle"), data.SaveName), msg))
                ApplyLoad(data, name);
        }

        private void ApplyLoad(OneV1SaveData d, string name)
        {
            round = d.Round; pendingWinner = d.PendingWinner; namesLocked = d.NamesLocked;
            resetArmed = d.ResetArmed; firstTurnChosen = d.FirstTurnChosen;
            p1Gold = d.P1Gold; p2Gold = d.P2Gold; p1Points = d.P1Points; p2Points = d.P2Points;
            p1Income = d.P1Income; p2Income = d.P2Income;
            p1PermMoveUpgrades = d.P1PermMoveUpgrades; p2PermMoveUpgrades = d.P2PermMoveUpgrades;
            p1MilestonePermMoveUpgrades = d.P1MilestonePermMoveUpgrades;
            p2MilestonePermMoveUpgrades = d.P2MilestonePermMoveUpgrades;
            p1IncomeUpgrades = d.P1IncomeUpgrades; p2IncomeUpgrades = d.P2IncomeUpgrades;
            p1IncomeLevel = d.P1IncomeLevel; p2IncomeLevel = d.P2IncomeLevel;
            p1IncomeCost = d.P1IncomeCost; p2IncomeCost = d.P2IncomeCost;
            p1BoughtIncomeThisRound = d.P1BoughtIncomeThisRound;
            p2BoughtIncomeThisRound = d.P2BoughtIncomeThisRound;
            p1HasIncomeDiscount = d.P1HasIncomeDiscount; p2HasIncomeDiscount = d.P2HasIncomeDiscount;
            p1HasFullRefund = d.P1HasFullRefund; p2HasFullRefund = d.P2HasFullRefund;
            p1Name = d.P1Name ?? T("DefaultP1Name"); p2Name = d.P2Name ?? T("DefaultP2Name");
            p1Calc = d.P1Calc ?? T("NoRoundYet"); p2Calc = d.P2Calc ?? T("NoRoundYet");
            p1HasFt10PermMove = d.P1HasFt10PermMove; p2HasFt10PermMove = d.P2HasFt10PermMove;
            milestone5Claimed = d.Milestone5Claimed; milestone10Claimed = d.Milestone10Claimed;
            milestone15Claimed = d.Milestone15Claimed; milestone20Claimed = d.Milestone20Claimed;
            milestone25Claimed = d.Milestone25Claimed;
            globalClaimedMilestones = d.GlobalClaimedMilestones != null
                ? new HashSet<int>(d.GlobalClaimedMilestones) : new HashSet<int>();
            milestoneRewardQueue = d.MilestoneRewardQueue ?? new List<string>();
            p1SellbackPct = d.P1SellbackPct > 0 ? d.P1SellbackPct : 50;
            p2SellbackPct = d.P2SellbackPct > 0 ? d.P2SellbackPct : 50;
            p1Sellback70 = d.P1Sellback70; p2Sellback70 = d.P2Sellback70;
            p1MissedIncomeRounds = d.P1MissedIncomeRounds;
            p2MissedIncomeRounds = d.P2MissedIncomeRounds;
            p1IncomeDecayPercent = d.P1IncomeDecayPercent;
            p2IncomeDecayPercent = d.P2IncomeDecayPercent;
            factionModeEnabled = d.FactionModeEnabled; factionModeLocked = d.FactionModeLocked;
            p1FactionPurchases = d.P1FactionPurchases; p2FactionPurchases = d.P2FactionPurchases;
            p1ChosenFactionPurchases = d.P1ChosenFactionPurchases; p2ChosenFactionPurchases = d.P2ChosenFactionPurchases;
            p1Factions = d.P1Factions ?? new List<string>();
            p2Factions = d.P2Factions ?? new List<string>();
            ft10ModeEnabled = d.Ft10ModeEnabled;
            ft30ModeEnabled = d.Ft30ModeEnabled || (d.SaveVersion < 6 && !d.Ft20ModeEnabled);
            ft20ModeEnabled = !ft10ModeEnabled && !ft30ModeEnabled;
            NormalizeMatchModeFlags();
            ft20ModeLocked = d.Ft20ModeLocked;
            matchEndPromptSuppressed = d.MatchEndPromptSuppressed;
            ft20MilestonePool = d.Ft20MilestonePool ?? new List<string>();
            ft20NextMilestoneRound = d.Ft20NextMilestoneRound > 0 ? d.Ft20NextMilestoneRound : GetMilestoneStep();
            actionLog.Clear();
            if (d.ActionLog != null) foreach (var item in d.ActionLog) actionLog.AddLast(item);
            undoStack.Clear(); _currentSaveName = name;
            P1GoldBorder.Background = normalPanelBrush; P2GoldBorder.Background = normalPanelBrush;
            P1PointsBorder.Background = normalPanelBrush; P2PointsBorder.Background = normalPanelBrush;
            if (namesLocked)
            {
                P1NameBox.IsReadOnly = true; P2NameBox.IsReadOnly = true;
                P1NameEditButton.Visibility = Visibility.Hidden;
                P2NameEditButton.Visibility = Visibility.Hidden;
            }
            else
            {
                P1NameBox.IsReadOnly = false; P2NameBox.IsReadOnly = false;
                P1NameEditButton.Visibility = Visibility.Visible;
                P2NameEditButton.Visibility = Visibility.Visible;
            }
            lastRoundWinner = d.LastRoundWinner;
            firstTurnPlayer = d.FirstTurnPlayer;
            p1ReplayBoughtThisRound = d.P1ReplayBoughtThisRound;
            p2ReplayBoughtThisRound = d.P2ReplayBoughtThisRound;

            UpdateUI(); RefreshSavesDropdown();
            AddActionLog(string.Format(T("LogLoadedMatch"), name));
            ShowNotice(string.Format(T("NoticeLoadedSave"), name));

        }

        private void DeleteButton_Click(object sender, RoutedEventArgs e)
        {
            var selected = SavesDropdown.SelectedItem as string;
            if (string.IsNullOrEmpty(selected)) { ShowNotice(T("SelectSaveDeleteFirst")); return; }
            if (!ShowConfirm(
    T("DeleteConfirmTitle"),
string.Format(T("DeleteConfirmMsg"), selected))) return;
            var path = Path.Combine(SaveFolder, selected + ".json");
            if (File.Exists(path)) File.Delete(path);
            if (_currentSaveName == selected) _currentSaveName = null;
            RefreshSavesDropdown();
            ShowNotice(string.Format(T("NoticeDeletedSave"), selected));
        }

        private void NewGameButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShowConfirm(
    T("NewGameConfirmTitle"),
T("NewGameConfirmMsg"))) return;
            DoFullReset();
        }

        private void DoFullReset()
        {
            bool keepFactionModeEnabled = factionModeEnabled;
            bool keepFt10ModeEnabled = ft10ModeEnabled;
            bool keepFt30ModeEnabled = ft30ModeEnabled;

            _currentSaveName = null; undoStack.Clear();
            round = 1; pendingWinner = 0; namesLocked = false; resetArmed = false; firstTurnChosen = false;
            factionModeEnabled = keepFactionModeEnabled; factionModeLocked = false;
            ft10ModeEnabled = keepFt10ModeEnabled; ft30ModeEnabled = keepFt30ModeEnabled; NormalizeMatchModeFlags(); ft20ModeLocked = false;
            matchEndPromptSuppressed = false;
            p1Gold = GetStartingGold(); p2Gold = GetStartingGold(); p1Points = 0; p2Points = 0;
            p1Income = 0; p2Income = 0;
            p1PermMoveUpgrades = 0; p2PermMoveUpgrades = 0;
            p1MilestonePermMoveUpgrades = 0; p2MilestonePermMoveUpgrades = 0;
            p1IncomeUpgrades = 0; p2IncomeUpgrades = 0;
            p1IncomeLevel = 0; p2IncomeLevel = 0;
            p1IncomeCost = GetBaseIncomeCost(); p2IncomeCost = GetBaseIncomeCost();
            p1BoughtIncomeThisRound = false; p2BoughtIncomeThisRound = false;
            p1HasIncomeDiscount = false; p2HasIncomeDiscount = false;
            p1HasFullRefund = false; p2HasFullRefund = false;
            p1Name = T("DefaultP1Name"); p2Name = T("DefaultP2Name");
            p1HasFt10PermMove = false; p2HasFt10PermMove = false;
            milestone5Claimed = false; milestone10Claimed = false; milestone15Claimed = false;
            milestone20Claimed = false; milestone25Claimed = false;
            globalClaimedMilestones.Clear();
            p1SellbackPct = 50; p2SellbackPct = 50;
            p1Sellback70 = false; p2Sellback70 = false;
            p1MissedIncomeRounds = 0; p2MissedIncomeRounds = 0;
            p1IncomeDecayPercent = 0; p2IncomeDecayPercent = 0;
            p1FactionPurchases = 0; p2FactionPurchases = 0;
            p1ChosenFactionPurchases = 0; p2ChosenFactionPurchases = 0;
            p1Factions.Clear(); p2Factions.Clear();
            ft20MilestonePool.Clear(); ft20NextMilestoneRound = GetMilestoneStep();
            if (factionModeEnabled) GrantStartingFactions();
            InitMilestoneRewardQueue();
            p1Calc = T("NoRoundYet"); p2Calc = T("NoRoundYet");
            P1NameBox.IsReadOnly = false; P2NameBox.IsReadOnly = false;
            P1NameEditButton.Visibility = Visibility.Visible; P2NameEditButton.Visibility = Visibility.Visible;
            P1NameBox.Text = p1Name; P2NameBox.Text = p2Name;
            P1NameDisplayText.Text = p1Name; P2NameDisplayText.Text = p2Name;
            ResetNameEditButtonsForNewGame();
            P1GoldBorder.Background = normalPanelBrush; P2GoldBorder.Background = normalPanelBrush;
            P1PointsBorder.Background = normalPanelBrush; P2PointsBorder.Background = normalPanelBrush;
            IncomeNoticePopup.IsOpen = false; noticeTimer.Stop();
            ResetTieTimer();
            actionLog.Clear();
            if (FindName("ActionLogPanel") is StackPanel alp) alp.Children.Clear();
            SavesDropdown.SelectedItem = null;
            lastRoundWinner = 0;
            firstTurnPlayer = 0;
            p1ReplayBoughtThisRound = false;
            p2ReplayBoughtThisRound = false;
            SetupPlaceholders(); UpdateUI();
            AddActionLog(T("LogNewGameStarted"));
        }

        private void ShowNotice(string message)
        {
            lastNotice = message;
            if (IncomeNoticeText != null) IncomeNoticeText.Text = message;

            if (IncomeNoticePopup != null)
            {
                IncomeNoticePopup.PlacementTarget = AppScroll;
                IncomeNoticePopup.Placement = System.Windows.Controls.Primitives.PlacementMode.Relative;
                IncomeNoticePopup.HorizontalOffset = Math.Max(0, AppScroll.ActualWidth - 800);
                IncomeNoticePopup.VerticalOffset = 10;

                if (!IncomeNoticePopup.IsOpen) IncomeNoticePopup.IsOpen = true;

                if (IncomeNotice != null)
                {
                    IncomeNotice.Opacity = 0;
                    var anim = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(140));
                    IncomeNotice.BeginAnimation(OpacityProperty, anim);
                }
            }

            noticeTimer.Stop();
            noticeTimer.Start();
        }

        private void AddActionLog(string entry)
        {
            actionLog.AddFirst(entry);
            while (actionLog.Count > 8) actionLog.RemoveLast();
            if (!(FindName("ActionLogPanel") is StackPanel actionLogPanel)) return;
            actionLogPanel.Children.Clear();
            foreach (var item in actionLog)
            {
                actionLogPanel.Children.Add(new Border
                {
                    Background = new SolidColorBrush(Color.FromRgb(34, 36, 39)),
                    CornerRadius = new CornerRadius(12),
                    Padding = new Thickness(10),
                    Margin = new Thickness(0, 0, 0, 8),
                    Child = new TextBlock
                    {
                        Text = item,
                        Foreground = new SolidColorBrush(Color.FromRgb(242, 244, 247)),
                        TextWrapping = TextWrapping.Wrap,
                        FontSize = 12
                    }
                });
            }
        }

        private void SetupNumericInputBoxes()
        {
            RegisterNumericOnly(P1SpendBox);
            RegisterNumericOnly(P1UnitBox);
            RegisterNumericOnly(P2SpendBox);
            RegisterNumericOnly(P2UnitBox);
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

        private void SetupPlaceholders()
        {
            SetPlaceholder(P1SpendBox, T("CustomTroopSpend"));
            SetPlaceholder(P1UnitBox, T("UnitValue"));
            SetPlaceholder(P2SpendBox, T("CustomTroopSpend"));
            SetPlaceholder(P2UnitBox, T("UnitValue"));
        }

        private void UpdatePlaceholderText(TextBox box, string key)
        {
            if (box == null) return;

            if (box.Foreground == Brushes.Gray)
            {
                box.Text = T(key);
                box.Foreground = Brushes.Gray;
            }
        }
        private void SetPlaceholder(TextBox box, string placeholder)
        {
            box.Text = placeholder; box.Foreground = Brushes.Gray;
            box.GotFocus -= Box_GotFocus; box.LostFocus -= Box_LostFocus;
            box.GotFocus += Box_GotFocus; box.LostFocus += Box_LostFocus;
        }

        private void Box_GotFocus(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if (box.Foreground == Brushes.Gray) { box.Text = ""; box.Foreground = Brushes.White; }
        }

        private void Box_LostFocus(object sender, RoutedEventArgs e)
        {
            var box = (TextBox)sender;
            if (string.IsNullOrWhiteSpace(box.Text))
            {
                if (box == P1SpendBox) SetPlaceholder(box, T("CustomTroopSpend"));
                else if (box == P1UnitBox) SetPlaceholder(box, T("UnitValue"));
                else if (box == P2SpendBox) SetPlaceholder(box, T("CustomTroopSpend"));
                else if (box == P2UnitBox) SetPlaceholder(box, T("UnitValue"));
            }
            else box.Foreground = Brushes.White;
        }

        private int ReadNumber(TextBox box)
        {
            return box.Foreground == Brushes.Gray ? 0 : (int.TryParse(box.Text, out var n) ? n : 0);
        }

        private void SetPlayerName(int player, string value)
        {
            value = string.IsNullOrWhiteSpace(value)
                ? T(player == 1 ? "DefaultP1Name" : "DefaultP2Name")
                : value.Trim();

            if (player == 1) p1Name = value;
            else p2Name = value;
        }

        private void ResetNameEditButtonsForNewGame()
        {
            for (int p = 1; p <= 2; p++)
            {
                var box = GetNameBox(p);
                var display = GetNameDisplayText(p);
                var button = GetNameEditButton(p);

                box.IsReadOnly = false;
                box.TextAlignment = TextAlignment.Left;
                box.Visibility = Visibility.Visible;

                display.Visibility = Visibility.Collapsed;

                button.Visibility = Visibility.Visible;
                button.Content = T("Set");
            }

            namesLocked = false;
            Keyboard.ClearFocus();
        }

        private TextBox GetNameBox(int player) => player == 1 ? P1NameBox : P2NameBox;
        private TextBlock GetNameDisplayText(int player) => player == 1 ? P1NameDisplayText : P2NameDisplayText;
        private Button GetNameEditButton(int player) => player == 1 ? P1NameEditButton : P2NameEditButton;

        private bool AreAnyNamesLocked()
        {
            return P1NameBox.IsReadOnly || P2NameBox.IsReadOnly;
        }

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

                button.Content = T("Set");
                box.Focus();
                box.CaretIndex = box.Text.Length;
            }
            else
            {
                LockNameBox(player);
            }

            namesLocked = AreAnyNamesLocked();
        }

        private void LockNameBox(int player)
        {
            var box = GetNameBox(player);
            var display = GetNameDisplayText(player);
            var button = GetNameEditButton(player);

            SetPlayerName(player, box.Text);

            box.Text = player == 1 ? p1Name : p2Name;
            display.Text = box.Text;

            box.IsReadOnly = true;
            box.TextAlignment = TextAlignment.Center;
            button.Content = T("Unset");

            Keyboard.ClearFocus();
            namesLocked = AreAnyNamesLocked();

            p1GoldWindow?.UpdateGold(p1Gold, p1Name, GetGoldVisualState(1), p1Factions, FactionIconMap);
            p2GoldWindow?.UpdateGold(p2Gold, p2Name, GetGoldVisualState(2), p2Factions, FactionIconMap);
        }

        private int GetIncomeDecayPercent(int missedRounds)
        {
            if (!IsIncomeAvailable()) return 0;

            if (ft20ModeEnabled)
            {
                if (missedRounds < 3) return 0;
                return Math.Min(100, (missedRounds - 2) * 6);
            }
            if (missedRounds < 4) return 0;
            return Math.Min(100, (missedRounds - 3) * 3);
        }

        private decimal GetBaseIncomeCost()
        {
            return ft20ModeEnabled ? 130m : 100m;
        }

        private int GetDisplayedIncomeCost(decimal currentCost, int decayPercent, bool hasDiscount)
        {
            var discounted = currentCost * (1m - (decayPercent / 100m));
            if (hasDiscount) discounted = discounted * 0.85m;
            if (discounted < 0m) discounted = 0m;
            return (int)Math.Ceiling(discounted);
        }

        private int GetFactionCost(int player)
        {
            int purchases = player == 1 ? p1FactionPurchases : p2FactionPurchases;
            int baseCost = ft10ModeEnabled ? 25 : 50;
            int scale = ft10ModeEnabled ? 15 : 20;
            return baseCost + (purchases * scale);
        }

        private int GetChosenFactionCost(int player)
        {
            int purchases = player == 1 ? p1FactionPurchases : p2FactionPurchases;
            int baseCost = ft10ModeEnabled ? 140 : 280;
            int scale = ft10ModeEnabled ? 15 : 20;
            return baseCost + (purchases * scale);
        }

        private int GetNextMilestone(int currentPoints)
        {
            int step = GetMilestoneStep();
            int check = ((currentPoints / step) + 1) * step;
            while (globalClaimedMilestones.Contains(check)) check += step;
            return check;
        }

        private List<string> GetOwnedFactionsForPlayer(int player) => player == 1 ? p1Factions : p2Factions;

        private void AddFactionToPlayer(int player, string faction)
        {
            var owned = GetOwnedFactionsForPlayer(player);
            if (!owned.Contains(faction)) owned.Add(faction);
        }

        private List<string> GetAvailableFactionsForPlayer(int player)
        {
            var owned = new HashSet<string>(GetOwnedFactionsForPlayer(player));
            return AllFactions.Where(f => !owned.Contains(f)).ToList();
        }

        private string GetRandomFactionForPlayer(int player)
        {
            var available = GetAvailableFactionsForPlayer(player);
            if (available.Count == 0) return null;
            var rng = new Random(Guid.NewGuid().GetHashCode());
            return available[rng.Next(available.Count)];
        }

        private void GrantStartingFactions()
        {
            if (!factionModeEnabled) return;
            var rng = new Random(Guid.NewGuid().GetHashCode());
            p1Factions.Clear(); p2Factions.Clear();
            var p1Pool = AllFactions.ToList();
            for (int i = 0; i < 3 && p1Pool.Count > 0; i++)
            { var pick = p1Pool[rng.Next(p1Pool.Count)]; p1Pool.Remove(pick); p1Factions.Add(pick); }
            var p2Pool = AllFactions.ToList();
            for (int i = 0; i < 3 && p2Pool.Count > 0; i++)
            { var pick = p2Pool[rng.Next(p2Pool.Count)]; p2Pool.Remove(pick); p2Factions.Add(pick); }
            p1FactionPurchases = 0; p2FactionPurchases = 0;
        }

        // Draw the next reward from the queue and apply to player
        private string ChooseFreeFactionForPlayer(int player)
        {
            var available = GetAvailableFactionsForPlayer(player);
            if (available.Count == 0) return null;

            string pName = player == 1 ? p1Name : p2Name;
            var dialog = new FactionChoiceDialog(
                T("ChooseFactionTitle"),
                string.Format(T("ChooseFactionSub"), pName),
                available,
                FactionIconMap)
            {
                Owner = this
            };

            return dialog.ShowDialog() == true ? dialog.SelectedFaction : null;
        }

        private void DrawAndApplyNextReward(int player)
        {
            if (milestoneRewardQueue == null || milestoneRewardQueue.Count == 0)
            {
                AddActionLog(T("LogMilestonePoolEmpty"));
                return;
            }
            string reward = milestoneRewardQueue[0];
            milestoneRewardQueue.RemoveAt(0);
            string pName = player == 1 ? p1Name : p2Name;

            switch (reward)
            {
                case "choose_free_faction":
                    string chosenFaction = ChooseFreeFactionForPlayer(player);
                    if (!string.IsNullOrWhiteSpace(chosenFaction))
                    {
                        AddFactionToPlayer(player, chosenFaction);
                        if (player == 1) p1FactionPurchases++; else p2FactionPurchases++;
                        AddActionLog(string.Format(T("LogChoseFreeFaction"), pName, chosenFaction));
                        ShowNotice(string.Format(T("NoticeChoseFreeFaction"), pName, chosenFaction));
                    }
                    else
                    {
                        AddActionLog(string.Format(T("LogMilestoneChooseFreeFactionAllOwned"), pName));
                        ShowNotice(string.Format(T("NoticeMilestoneAllFactionsOwned"), pName));
                    }
                    break;

                case "free_faction":
                    string faction = GetRandomFactionForPlayer(player);
                    if (faction != null)
                    {
                        AddFactionToPlayer(player, faction);
                        if (player == 1) p1FactionPurchases++; else p2FactionPurchases++;
                        AddActionLog(string.Format(T("LogMilestoneFreeFaction"), pName, faction));
                        ShowNotice(string.Format(T("NoticeMilestoneFreeFaction"), pName, faction));
                    }
                    else
                    {
                        AddActionLog(string.Format(T("LogMilestoneFreeFactionAllOwned"), pName));
                        ShowNotice(string.Format(T("NoticeMilestoneAllFactionsOwned"), pName));
                    }
                    break;
                case "perm_move_upgrade":
                    if (player == 1) p1MilestonePermMoveUpgrades++; else p2MilestonePermMoveUpgrades++;
                    AddActionLog(string.Format(T("LogMilestonePermMove"), pName));
                    ShowNotice(string.Format(T("MilestonePermMoveNotice"), pName));
                    break;
                case "sellback_20":
                    if (player == 1) { p1SellbackPct = Math.Min(100, p1SellbackPct + 20); p1Sellback70 = p1SellbackPct >= 70; }
                    else { p2SellbackPct = Math.Min(100, p2SellbackPct + 20); p2Sellback70 = p2SellbackPct >= 70; }
                    int newPct = player == 1 ? p1SellbackPct : p2SellbackPct;
                    AddActionLog(string.Format(T("LogMilestoneSellback"), pName, newPct));
                    ShowNotice(string.Format(T("MilestoneSellbackNotice"), pName, newPct));
                    break;
                case "income_discount":
                    if (player == 1) p1HasIncomeDiscount = true; else p2HasIncomeDiscount = true;
                    AddActionLog(string.Format(T("LogMilestoneIncomeDiscount"), pName));
                    ShowNotice(string.Format(T("MilestoneIncomeDiscountNotice"), pName));
                    break;
                case "full_refund":
                    if (player == 1) p1HasFullRefund = true; else p2HasFullRefund = true;
                    AddActionLog(string.Format(T("LogMilestoneFullRefund"), pName));
                    ShowNotice(string.Format(T("MilestoneFullRefundNotice"), pName));
                    break;
            }
        }

        private void RefreshFactionIcons()
        {
            if (P1FactionIconsPanel != null)
            {
                P1FactionIconsPanel.Children.Clear();
                foreach (var faction in p1Factions) P1FactionIconsPanel.Children.Add(BuildFactionIcon(faction));
            }
            if (P2FactionIconsPanel != null)
            {
                P2FactionIconsPanel.Children.Clear();
                foreach (var faction in p2Factions) P2FactionIconsPanel.Children.Add(BuildFactionIcon(faction));
            }
        }

        private FrameworkElement BuildFactionIcon(string faction)
        {
            var file = FactionIconMap.ContainsKey(faction) ? FactionIconMap[faction] : null;
            var border = new Border
            {
                Width = 48,
                Height = 48,
                CornerRadius = new CornerRadius(8),
                Margin = new Thickness(0, 0, 6, 6),
                Background = new SolidColorBrush(Color.FromRgb(26, 28, 31)),
                ClipToBounds = true
            };
            if (!string.IsNullOrWhiteSpace(file))
            {
                try
                {
                    border.Child = new Image
                    {
                        Stretch = Stretch.Uniform,
                        Source = new BitmapImage(new Uri($"pack://application:,,,/Assets/{file}", UriKind.Absolute))
                    };
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
            return border;
        }

        private void UpdateNameEditState()
        {
            bool matchStarted = round > 1 || factionModeLocked || ft20ModeLocked;

            for (int p = 1; p <= 2; p++)
            {
                var box = GetNameBox(p);
                var display = GetNameDisplayText(p);
                var button = GetNameEditButton(p);

                if (matchStarted)
                {
                    box.IsReadOnly = true;
                    box.Visibility = Visibility.Collapsed;

                    display.Text = p == 1 ? p1Name : p2Name;
                    display.Visibility = Visibility.Visible;

                    button.Visibility = Visibility.Collapsed;
                }
                else
                {
                    display.Visibility = Visibility.Collapsed;
                    box.Visibility = Visibility.Visible;

                    button.Visibility = Visibility.Visible;
                    button.Content = box.IsReadOnly ? T("Unset") : T("Set");
                }
            }
        }
        private void UpdateStaticText()
        {
            if (MainMenuButton != null) MainMenuButton.Content = T("MainMenu");
            if (AppTitleText != null) AppTitleText.Text = T("AppTitle");

            if (SettingsButton != null) SettingsButton.ToolTip = T("Settings");
            if (GuideButton != null) GuideButton.ToolTip = T("Guide");

            if (SettingsTitleText != null) SettingsTitleText.Text = T("Settings");
            if (GuideTitleText != null) GuideTitleText.Text = T("GuideTitle");

            if (SettingsBackButton != null) SettingsBackButton.Content = T("Back");
            if (GuideBackButton != null) GuideBackButton.Content = T("Back");

            if (SettingsWindowModeLabel != null) SettingsWindowModeLabel.Text = T("WindowMode");
            if (SettingsLanguageLabel != null) SettingsLanguageLabel.Text = T("Language");
            if (SettingsSoundsLabel != null) SettingsSoundsLabel.Text = T("Sounds");
            if (SettingsSoundVolumeLabel != null) SettingsSoundVolumeLabel.Text = T("Volume");

            if (SettingsWindowModeText != null)
                SettingsWindowModeText.Text = _isBorderlessFullscreen ? T("BorderlessFullscreen") : T("Windowed");

            if (OverviewTitle != null) OverviewTitle.Text = T("OverviewTitle");
            if (OverviewSub != null) OverviewSub.Text = T("OverviewSub");
            if (LblCurrentRound != null) LblCurrentRound.Text = T("CurrentRound");
            if (LblNextTurnOrder != null) LblNextTurnOrder.Text = T("NextTurnOrder");
            if (LblPendingResult != null) LblPendingResult.Text = T("PendingResult");
            if (LblFactionMode != null) LblFactionMode.Text = T("FactionMode");
            if (LblFT20Mode != null) LblFT20Mode.Text = T("FT30Mode");
            if (LblFT10Mode != null) LblFT10Mode.Text = T("FT10Mode");
            if (LblWhichPlayerFirst != null) LblWhichPlayerFirst.Text = T("WhichPlayerFirst");
            if (LblMatchSaves != null) LblMatchSaves.Text = T("MatchSaves");
            if (P1FirstTurnButton != null) P1FirstTurnButton.Content = T("P1FirstTurn");
            if (P2FirstTurnButton != null) P2FirstTurnButton.Content = T("P2FirstTurn");
            if (Ft20PoolNextLabel != null) Ft20PoolNextLabel.Text = T("MilestoneProgress");
            if (LblNextReward != null) LblNextReward.Text = T("NextReward");
            if (LblRewardsLeft != null) LblRewardsLeft.Text = T("RewardsLeft");

            if (SaveButton != null) SaveButton.Content = T("Save");
            if (LoadButton != null) LoadButton.Content = T("Load");
            if (DeleteButton != null) DeleteButton.Content = T("Delete");
            if (NewGameButton != null) NewGameButton.Content = T("NewGame");

            if (LblActionLog != null) LblActionLog.Text = T("ActionLog");
            if (LblActionLogSub != null) LblActionLogSub.Text = T("ActionLogSub");
            if (LblRoundControl != null) LblRoundControl.Text = T("RoundControl");

            if (P1WinsButton != null) P1WinsButton.Content = T("Player1Wins");
            if (P2WinsButton != null) P2WinsButton.Content = T("Player2Wins");
            if (TieButton != null) TieButton.Content = T("Tie");
            if (NextRoundButton != null) NextRoundButton.Content = T("NextRound");
            if (UndoButton != null) UndoButton.Content = T("Undo");
            UpdateTieTimerUi();

            if (IsDefaultP1Name(p1Name)) p1Name = T("DefaultP1Name");
            if (IsDefaultP2Name(p2Name)) p2Name = T("DefaultP2Name");

            if (P1NameBox != null) P1NameBox.Text = p1Name;
            if (P2NameBox != null) P2NameBox.Text = p2Name;
            if (P1NameDisplayText != null) P1NameDisplayText.Text = p1Name;
            if (P2NameDisplayText != null) P2NameDisplayText.Text = p2Name;

            if (P1LblGold != null) P1LblGold.Text = T("Gold");
            if (P1LblPoints != null) P1LblPoints.Text = T("Points");
            if (P1LblPermMove != null) P1LblPermMove.Text = T("PermMv");
            if (P1LblIncome != null) P1LblIncome.Text = T("Income");
            if (P1LblInterest != null) P1LblInterest.Text = T("InterestStat");

            if (P2LblGold != null) P2LblGold.Text = T("Gold");
            if (P2LblPoints != null) P2LblPoints.Text = T("Points");
            if (P2LblPermMove != null) P2LblPermMove.Text = T("PermMv");
            if (P2LblIncome != null) P2LblIncome.Text = T("Income");
            if (P2LblInterest != null) P2LblInterest.Text = T("InterestStat");

            if (P1NameEditButton != null) P1NameEditButton.Content = T("Set");
            if (P2NameEditButton != null) P2NameEditButton.Content = T("Set");

            if (P1LblUpgrades != null) P1LblUpgrades.Text = T("Upgrades");
            if (P2LblUpgrades != null) P2LblUpgrades.Text = T("Upgrades");
            if (P1LblUtility != null) P1LblUtility.Text = T("Utility");
            if (P2LblUtility != null) P2LblUtility.Text = T("Utility");
            if (P1LblCalculations != null) P1LblCalculations.Text = T("Calculations");
            if (P2LblCalculations != null) P2LblCalculations.Text = T("Calculations");

            if (P1SpendButton != null) P1SpendButton.Content = T("Spend");
            if (P2SpendButton != null) P2SpendButton.Content = T("Spend");
            if (P1SellUnitButton != null) P1SellUnitButton.Content = T("SellUnit");
            if (P2SellUnitButton != null) P2SellUnitButton.Content = T("SellUnit");

            UpdatePlaceholderText(P1SpendBox, "CustomTroopSpend");
            UpdatePlaceholderText(P2SpendBox, "CustomTroopSpend");
            UpdatePlaceholderText(P1UnitBox, "UnitValue");
            UpdatePlaceholderText(P2UnitBox, "UnitValue");

            UpdateLanguageSelectorUI();
        }

        private void UpdateUI()
        {
            P1NameBox.Text = p1Name; P2NameBox.Text = p2Name;
            P1NameDisplayText.Text = p1Name; P2NameDisplayText.Text = p2Name;
            UpdateNameEditState();
            RoundText.Text = round.ToString();
            UpdateTurnOrderText();
            PendingResultText.Text = pendingWinner == 0 ? T("NotSet")
: pendingWinner == 1 ? p1Name + " " + T("WinsSuffix")
: pendingWinner == 2 ? p2Name + " " + T("WinsSuffix")
: T("Tie");
            P1GoldText.Text = p1Gold.ToString(); P2GoldText.Text = p2Gold.ToString();
            p1GoldWindow?.UpdateGold(p1Gold, p1Name, GetGoldVisualState(1), p1Factions, FactionIconMap);
            p2GoldWindow?.UpdateGold(p2Gold, p2Name, GetGoldVisualState(2), p2Factions, FactionIconMap);
            if (P1InterestText != null) P1InterestText.Text = "+" + CalcInterest(p1Gold);
            if (P2InterestText != null) P2InterestText.Text = "+" + CalcInterest(p2Gold);
            P1PointsText.Text = p1Points.ToString(); P2PointsText.Text = p2Points.ToString();
            P1IncomeText.Text = "+" + p1Income; P2IncomeText.Text = "+" + p2Income;
            P1UpgradesText.Text = (p1PermMoveUpgrades + (p1HasFt10PermMove ? 1 : 0) + p1MilestonePermMoveUpgrades).ToString();
            P2UpgradesText.Text = (p2PermMoveUpgrades + (p2HasFt10PermMove ? 1 : 0) + p2MilestonePermMoveUpgrades).ToString();
            P1CalcText.Text = p1Calc; P2CalcText.Text = p2Calc;
            P1SellPctText.Text = p1HasFullRefund ? "100%" : p1SellbackPct + "%";
            P2SellPctText.Text = p2HasFullRefund ? "100%" : p2SellbackPct + "%";

            if (IsIncomeAvailable())
            {
                P1BuyIncomeButton.Visibility = Visibility.Visible;
                P2BuyIncomeButton.Visibility = Visibility.Visible;
                int p1DisplayedIncomeCost = GetDisplayedIncomeCost(p1IncomeCost, p1IncomeDecayPercent, p1HasIncomeDiscount);
                int p2DisplayedIncomeCost = GetDisplayedIncomeCost(p2IncomeCost, p2IncomeDecayPercent, p2HasIncomeDiscount);
                string incomeLabel = ft20ModeEnabled ? T("BuyIncomeF") : T("BuyIncome");
                PlayerPanelText.SetButtonContent(P1BuyIncomeButton, string.Format(incomeLabel, p1DisplayedIncomeCost));
                PlayerPanelText.SetButtonContent(P2BuyIncomeButton, string.Format(incomeLabel, p2DisplayedIncomeCost));
                P1IncomeDecayPctText.Text = p1HasIncomeDiscount
                    ? string.Format("-{0}%  (+15% off)", p1IncomeDecayPercent)
                    : string.Format("-{0}%", p1IncomeDecayPercent);
                P2IncomeDecayPctText.Text = p2HasIncomeDiscount
                    ? string.Format("-{0}%  (+15% off)", p2IncomeDecayPercent)
                    : string.Format("-{0}%", p2IncomeDecayPercent);

                P1IncomeDecayPctBorder.Visibility = (p1IncomeDecayPercent > 0 || p1HasIncomeDiscount) ? Visibility.Visible : Visibility.Collapsed;
                P2IncomeDecayPctBorder.Visibility = (p2IncomeDecayPercent > 0 || p2HasIncomeDiscount) ? Visibility.Visible : Visibility.Collapsed;
                P1BuyIncomeButton.Background = (!p1BoughtIncomeThisRound && p1Gold >= p1DisplayedIncomeCost) ? cyanBrush : disabledBrush;
                P2BuyIncomeButton.Background = (!p2BoughtIncomeThisRound && p2Gold >= p2DisplayedIncomeCost) ? cyanBrush : disabledBrush;
            }
            else
            {
                P1BuyIncomeButton.Visibility = Visibility.Collapsed;
                P2BuyIncomeButton.Visibility = Visibility.Collapsed;
                P1IncomeDecayPctBorder.Visibility = Visibility.Collapsed;
                P2IncomeDecayPctBorder.Visibility = Visibility.Collapsed;
            }

            int permMoveCost = GetPermMoveCost();
            PlayerPanelText.SetButtonContent(P1BuyPermMoveButton, string.Format(T("BuyPermMove"), permMoveCost));
            PlayerPanelText.SetButtonContent(P2BuyPermMoveButton, string.Format(T("BuyPermMove"), permMoveCost));
            P1BuyPermMoveButton.IsEnabled = true;
            P2BuyPermMoveButton.IsEnabled = true;
            P1BuyPermMoveButton.Background = (p1PermMoveUpgrades < 2 && p1Gold >= permMoveCost) ? cyanBrush : disabledBrush;
            P2BuyPermMoveButton.Background = (p2PermMoveUpgrades < 2 && p2Gold >= permMoveCost) ? cyanBrush : disabledBrush;

            if (FactionModeToggle != null)
            {
                FactionModeToggle.IsChecked = factionModeEnabled;
                FactionModeToggle.Content = factionModeEnabled ? T("FactionModeOn") : T("FactionModeOff");
                FactionModeToggle.IsEnabled = !factionModeLocked;
            }
            if (Ft20ModeToggle != null)
            {
                Ft20ModeToggle.IsChecked = ft30ModeEnabled;
                Ft20ModeToggle.Content = ft30ModeEnabled ? T("FT30ModeOn") : T("FT30ModeOff");
                Ft20ModeToggle.IsEnabled = !ft20ModeLocked;
            }
            if (Ft10ModeToggle != null)
            {
                Ft10ModeToggle.IsChecked = ft10ModeEnabled;
                Ft10ModeToggle.Content = ft10ModeEnabled ? T("FT10ModeOn") : T("FT10ModeOff");
                Ft10ModeToggle.IsEnabled = !ft20ModeLocked;
            }

            if (P1BuyFactionButton != null)
            {
                P1BuyFactionButton.Visibility = factionModeEnabled ? Visibility.Visible : Visibility.Collapsed;
                PlayerPanelText.SetButtonContent(P1BuyFactionButton, string.Format(T("BuyFaction"), GetFactionCost(1)));
                bool can = factionModeEnabled && GetAvailableFactionsForPlayer(1).Count > 0 && p1Gold >= GetFactionCost(1);
                P1BuyFactionButton.IsEnabled = can; P1BuyFactionButton.Background = can ? cyanBrush : disabledBrush;
            }
            if (P1BuyChosenFactionButton != null)
            {
                P1BuyChosenFactionButton.Visibility = factionModeEnabled ? Visibility.Visible : Visibility.Collapsed;
                PlayerPanelText.SetButtonContent(P1BuyChosenFactionButton, string.Format(T("BuyChosenFaction"), GetChosenFactionCost(1)));
                bool can = factionModeEnabled && GetAvailableFactionsForPlayer(1).Count > 0 && p1Gold >= GetChosenFactionCost(1);
                P1BuyChosenFactionButton.IsEnabled = can; P1BuyChosenFactionButton.Background = can ? cyanBrush : disabledBrush;
            }
            if (P2BuyFactionButton != null)
            {
                P2BuyFactionButton.Visibility = factionModeEnabled ? Visibility.Visible : Visibility.Collapsed;
                PlayerPanelText.SetButtonContent(P2BuyFactionButton, string.Format(T("BuyFaction"), GetFactionCost(2)));
                bool can = factionModeEnabled && GetAvailableFactionsForPlayer(2).Count > 0 && p2Gold >= GetFactionCost(2);
                P2BuyFactionButton.IsEnabled = can; P2BuyFactionButton.Background = can ? cyanBrush : disabledBrush;
            }
            if (P2BuyChosenFactionButton != null)
            {
                P2BuyChosenFactionButton.Visibility = factionModeEnabled ? Visibility.Visible : Visibility.Collapsed;
                PlayerPanelText.SetButtonContent(P2BuyChosenFactionButton, string.Format(T("BuyChosenFaction"), GetChosenFactionCost(2)));
                bool can = factionModeEnabled && GetAvailableFactionsForPlayer(2).Count > 0 && p2Gold >= GetChosenFactionCost(2);
                P2BuyChosenFactionButton.IsEnabled = can; P2BuyChosenFactionButton.Background = can ? cyanBrush : disabledBrush;
            }

            // Milestone tracker boxes
            if (MilestoneTrackerRow != null)
            {
                MilestoneTrackerRow.Visibility = Visibility.Visible;

                // Box 1 — per-player progress toward next unclaimed milestone
                int p1Away = GetNextMilestone(p1Points) - p1Points;
                int p2Away = GetNextMilestone(p2Points) - p2Points;
                if (Ft20PoolNextLabel != null) Ft20PoolNextLabel.Text = T("MilestoneProgress");
                SetMilestoneProgressText(p1Name, p1Away, p2Name, p2Away);

                // Box 2 — next pre-rolled reward
                if (MilestoneNextRewardIcon != null)
                    MilestoneNextRewardIcon.Text = GetNextRewardIcon();

                if (MilestoneNextRewardText != null)
                    MilestoneNextRewardText.Text = GetNextRewardLabel();

                // Box 3 — remaining pool counts
                if (Ft20PoolRewardLabel != null)
                    Ft20PoolRewardLabel.Text = BuildRewardPoolText();
            }

            RefreshFactionIcons();

            int singleTroopMoveCost = GetSingleTroopMoveCost();
            PlayerPanelText.SetButtonContent(P1SingleTroopMoveButton, string.Format(T("SingleTroopMove"), singleTroopMoveCost));
            PlayerPanelText.SetButtonContent(P2SingleTroopMoveButton, string.Format(T("SingleTroopMove"), singleTroopMoveCost));
            P1SingleTroopMoveButton.Background = p1Gold >= singleTroopMoveCost ? cyanBrush : disabledBrush;
            P2SingleTroopMoveButton.Background = p2Gold >= singleTroopMoveCost ? cyanBrush : disabledBrush;
            PlayerPanelText.SetButtonContent(P1ReplayButton, p1ReplayBoughtThisRound ? T("ReplayUsed") : T("Replay"));
            PlayerPanelText.SetButtonContent(P2ReplayButton, p2ReplayBoughtThisRound ? T("ReplayUsed") : T("Replay"));
            P1ReplayButton.IsEnabled = !p1ReplayBoughtThisRound;
            P2ReplayButton.IsEnabled = !p2ReplayBoughtThisRound;
            P1ReplayButton.Background = (!p1ReplayBoughtThisRound && p1Gold >= 10) ? cyanBrush : disabledBrush;
            P2ReplayButton.Background = (!p2ReplayBoughtThisRound && p2Gold >= 10) ? cyanBrush : disabledBrush;
            P1SpendButton.Background = ReadNumber(P1SpendBox) > 0 && p1Gold >= ReadNumber(P1SpendBox) ? cyanBrush : disabledBrush;
            P2SpendButton.Background = ReadNumber(P2SpendBox) > 0 && p2Gold >= ReadNumber(P2SpendBox) ? cyanBrush : disabledBrush;

            NextRoundButton.IsEnabled = pendingWinner != 0;
            NextRoundButton.Background = pendingWinner != 0 ? new SolidColorBrush(Color.FromRgb(102, 221, 235)) : disabledBrush;
            UndoButton.IsEnabled = undoStack.Count > 0;
            UndoButton.Background = undoStack.Count > 0 ? new SolidColorBrush(Color.FromRgb(142, 108, 245)) : disabledBrush;

            Visibility firstTurnOverlayVisibility = (round == 1 && !firstTurnChosen) ? Visibility.Visible : Visibility.Collapsed;
            if (FirstTurnPromptBorder != null)
                FirstTurnPromptBorder.Visibility = firstTurnOverlayVisibility;
            if (FirstTurnDimOverlay != null)
                FirstTurnDimOverlay.Visibility = firstTurnOverlayVisibility;
        }

        private void P1NameEdit_Click(object sender, RoutedEventArgs e) => ToggleNameLock(1);
        private void P2NameEdit_Click(object sender, RoutedEventArgs e) => ToggleNameLock(2);

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

        private void FactionModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (factionModeLocked) { ShowNotice(T("FactionModeLocked")); FactionModeToggle.IsChecked = factionModeEnabled; return; }
            CloseAllGoldWindows();
            PushUndoState();
            factionModeEnabled = FactionModeToggle.IsChecked == true;
            ResetEconomyForModeSwitch();
            AddActionLog(factionModeEnabled ? T("LogFactionModeOn") : T("LogFactionModeOff"));
            UpdateUI();
        }

        private void Ft20ModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ft20ModeLocked) { ShowNotice(T("MatchModeLocked")); Ft20ModeToggle.IsChecked = ft30ModeEnabled; return; }
            CloseAllGoldWindows();
            PushUndoState();
            ft30ModeEnabled = Ft20ModeToggle.IsChecked == true;
            if (ft30ModeEnabled) ft10ModeEnabled = false;
            NormalizeMatchModeFlags();
            ResetEconomyForModeSwitch();
            AddActionLog(ft30ModeEnabled ? T("LogFT30ModeOn") : T("LogFT30ModeOff"));
            UpdateUI();
        }

        private void Ft10ModeToggle_Click(object sender, RoutedEventArgs e)
        {
            if (ft20ModeLocked) { ShowNotice(T("MatchModeLocked")); Ft10ModeToggle.IsChecked = ft10ModeEnabled; return; }
            CloseAllGoldWindows();
            PushUndoState();
            ft10ModeEnabled = Ft10ModeToggle.IsChecked == true;
            if (ft10ModeEnabled) ft30ModeEnabled = false;
            NormalizeMatchModeFlags();
            ResetEconomyForModeSwitch();
            AddActionLog(ft10ModeEnabled ? T("LogFT10ModeOn") : T("LogFT10ModeOff"));
            UpdateUI();
        }

        private void P1BuyChosenFactionButton_Click(object sender, RoutedEventArgs e) => BuyChosenFaction(1);
        private void P2BuyChosenFactionButton_Click(object sender, RoutedEventArgs e) => BuyChosenFaction(2);

        private void BuyChosenFaction(int player)
        {
            string pName = player == 1 ? p1Name : p2Name;
            int cost = GetChosenFactionCost(player);

            if (!factionModeEnabled) return;

            if (GetAvailableFactionsForPlayer(player).Count == 0)
            {
                ShowNotice(string.Format(T("AllFactionsOwned"), pName));
                return;
            }

            if ((player == 1 ? p1Gold : p2Gold) < cost)
            {
                ShowNotice(string.Format(T("NotEnoughGoldChosenFaction"), pName, cost));
                return;
            }

            string faction = ChooseFreeFactionForPlayer(player);
            if (string.IsNullOrWhiteSpace(faction)) return;

            PushUndoState();
            ApplyGoldSpend(player, cost);
            AddFactionToPlayer(player, faction);

            if (player == 1) p1FactionPurchases++;
            else p2FactionPurchases++;

            SetGoldRed(player);
            AddActionLog(string.Format(T("LogBoughtChosenFaction"), pName, faction, cost));
            ShowNotice(string.Format(T("NoticeBoughtChosenFaction"), pName, faction, cost));
            UpdateUI();
        }

        private void P1BuyFactionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!factionModeEnabled) { ShowNotice(T("FactionDisabled")); return; }
            int cost = GetFactionCost(1);
            if (p1Gold < cost) { ShowNotice(string.Format(T("NotEnoughGoldFaction"), p1Name, cost)); return; }
            string faction = GetRandomFactionForPlayer(1);
            if (faction == null) { ShowNotice(string.Format(T("AllFactionsOwned"), p1Name)); return; }
            PushUndoState();
            ApplyGoldSpend(1, cost); AddFactionToPlayer(1, faction); p1FactionPurchases++;
            AddActionLog(string.Format(T("LogGainedFaction"), p1Name, faction));
            ShowNotice(string.Format(T("NoticeGainedFaction"), p1Name, faction));
            SetGoldRed(1); UpdateUI();
        }

        private void P2BuyFactionButton_Click(object sender, RoutedEventArgs e)
        {
            if (!factionModeEnabled) { ShowNotice(T("FactionDisabled")); return; }
            int cost = GetFactionCost(2);
            if (p2Gold < cost) { ShowNotice(string.Format(T("NotEnoughGoldFaction"), p2Name, cost)); return; }
            string faction = GetRandomFactionForPlayer(2);
            if (faction == null) { ShowNotice(string.Format(T("AllFactionsOwned"), p2Name)); return; }
            PushUndoState();
            ApplyGoldSpend(2, cost); AddFactionToPlayer(2, faction); p2FactionPurchases++;
            AddActionLog(string.Format(T("LogGainedFaction"), p2Name, faction));
            ShowNotice(string.Format(T("NoticeGainedFaction"), p2Name, faction));
            SetGoldRed(2); UpdateUI();
        }

        private void Player1Wins_Click(object sender, RoutedEventArgs e) { PushUndoState(); pendingWinner = 1; AddActionLog(string.Format(T("LogWinnerMarked"), p1Name)); UpdateUI(); }
        private void TieRound_Click(object sender, RoutedEventArgs e) { PushUndoState(); pendingWinner = 3; AddActionLog(string.Format(T("LogWinnerMarked"), T("Tie"))); UpdateUI(); }
        private void Player2Wins_Click(object sender, RoutedEventArgs e) { PushUndoState(); pendingWinner = 2; AddActionLog(string.Format(T("LogWinnerMarked"), p2Name)); UpdateUI(); }

        private void P1FirstTurnButton_Click(object sender, RoutedEventArgs e)
        {
            if (round != 1 || firstTurnChosen) return;
            PushUndoState(); firstTurnChosen = true; firstTurnPlayer = 1; p1Gold += 50; SetGoldGreen(1);
            AddActionLog(string.Format(T("LogPlayerGoesFirst"), p1Name)); UpdateUI();
        }

        private void P2FirstTurnButton_Click(object sender, RoutedEventArgs e)
        {
            if (round != 1 || firstTurnChosen) return;
            PushUndoState(); firstTurnChosen = true; firstTurnPlayer = 2; p2Gold += 50; SetGoldGreen(2);
            AddActionLog(string.Format(T("LogPlayerGoesFirst"), p2Name)); UpdateUI();
        }

        private int CalcInterest(int gold) { return Math.Min((gold / 50) * 10, 100); }

        private int GetTieReward()
        {
            return GetTieRewardBase() + GetRoundRewardTier();
        }

        private string BuildCalcText(int startGold, int interest, int roundReward, int income, int milestoneBonus, int finalGold)
        {
            var lines = new List<string>
    {
        string.Format("{0}: {1}", T("StartingGold"), startGold),
        string.Format("{0}: +{1}", T("Interest"), interest)
    };

            if (milestoneBonus > 0)
                lines.Add(string.Format("{0}: +{1}", T("MilestoneReward"), milestoneBonus));

            lines.Add(string.Format("{0}: +{1}", T("RoundReward"), roundReward));

            if (income > 0)
                lines.Add(string.Format("{0}: +{1}", T("PermanentIncome"), income));

            lines.Add(string.Format("{0}: {1}", T("FinalGold"), finalGold));

            return string.Join(Environment.NewLine, lines);
        }

        private bool ApplyGoldSpend(int player, int amount)
        {
            if (amount <= 0) return false;
            if (player == 1) { if (p1Gold < amount) return false; p1Gold -= amount; }
            else { if (p2Gold < amount) return false; p2Gold -= amount; }
            return true;
        }

        private void SetGoldGreen(int player) { if (player == 1) P1GoldBorder.Background = greenBrush; else P2GoldBorder.Background = greenBrush; }
        private void SetGoldRed(int player) { if (player == 1) P1GoldBorder.Background = redBrush; else P2GoldBorder.Background = redBrush; }
        private void SetPointsGreen(int player) { if (player == 1) P1PointsBorder.Background = greenBrush; else P2PointsBorder.Background = greenBrush; }

        private int GetMatchGoalPoints()
        {
            if (ft10ModeEnabled) return 10;
            if (ft30ModeEnabled) return 30;
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

        private void ShowMatchEndPromptIfNeeded(int previousP1Points, int previousP2Points)
        {
            if (matchEndPromptSuppressed) return;

            int winner = 0;
            bool wonByTwoRule = false;

            if (ShouldShowMatchEndPrompt(previousP1Points, p1Points, p2Points))
            {
                winner = 1;
                wonByTwoRule = IsWinByTwoRuleActive(previousP1Points, p1Points, p2Points);
            }
            else if (ShouldShowMatchEndPrompt(previousP2Points, p2Points, p1Points))
            {
                winner = 2;
                wonByTwoRule = IsWinByTwoRuleActive(previousP2Points, p2Points, p1Points);
            }

            if (winner == 0) return;

            int goal = GetMatchGoalPoints();
            string winnerName = winner == 1 ? p1Name : p2Name;
            string message = wonByTwoRule
                ? string.Format(T("MatchEndWinByTwoMessage"), p1Name, p1Points, p2Name, p2Points, winnerName)
                : string.Format(T("MatchEndMessage"), winnerName, goal);

            var dialog = new MatchEndDialog(
                T("MatchEndTitle"),
                message,
                T("MatchEndQuestion"),
                T("NewGamePlain"),
                T("ContinuePlaying"))
            { Owner = this };

            bool? result = dialog.ShowDialog();
            if (result == true && dialog.StartNewGame)
                DoFullReset();
            else if (dialog.ContinueSelected)
                matchEndPromptSuppressed = true;
        }

        private void NextRound_Click(object sender, RoutedEventArgs e)
        {
            if (pendingWinner == 0) { ShowNotice(T("ChooseWinnerFirst")); return; }
            PushUndoState();

            if (round == 1 && !namesLocked)
            {
                namesLocked = true; P1NameBox.IsReadOnly = true; P2NameBox.IsReadOnly = true;
                P1NameEditButton.Visibility = Visibility.Hidden; P2NameEditButton.Visibility = Visibility.Hidden;
            }
            if (round == 1)
            {
                factionModeLocked = true; ft20ModeLocked = true;
                if (FactionModeToggle != null) FactionModeToggle.IsEnabled = false;
                if (Ft20ModeToggle != null) Ft20ModeToggle.IsEnabled = false;
                if (Ft10ModeToggle != null) Ft10ModeToggle.IsEnabled = false;
            }

            int p1Start = p1Gold; int p2Start = p2Gold;
            int prevP1Points = p1Points; int prevP2Points = p2Points;
            P1PointsBorder.Background = normalPanelBrush; P2PointsBorder.Background = normalPanelBrush;

            int tier = GetRoundRewardTier();
            int winningReward = GetWinnerRewardBase() + tier;
            int losingReward = GetLoserRewardBase() + tier;
            int tieReward = GetTieReward();

            int p1Reward;
            int p2Reward;

            if (pendingWinner == 1)
            {
                p1Reward = winningReward;
                p2Reward = losingReward;
                p1Points++;
                SetPointsGreen(1);
                AddActionLog(string.Format(T("LogRoundWon"), round, p1Name));
            }
            else if (pendingWinner == 2)
            {
                p1Reward = losingReward;
                p2Reward = winningReward;
                p2Points++;
                SetPointsGreen(2);
                AddActionLog(string.Format(T("LogRoundWon"), round, p2Name));
            }
            else
            {
                p1Reward = tieReward;
                p2Reward = tieReward;
                AddActionLog(string.Format(T("LogRoundTie"), round));
            }

            if (pendingWinner == 1)
                ShowNotice(string.Format(T("RoundWinNotice"), p1Name, round, winningReward, losingReward));
            else if (pendingWinner == 2)
                ShowNotice(string.Format(T("RoundWinNotice"), p2Name, round, winningReward, losingReward));
            else
                ShowNotice(string.Format(T("RoundTieNotice"), round, tieReward));

            int p1Interest = CalcInterest(p1Gold); int p2Interest = CalcInterest(p2Gold);
            if (p1Interest > 0) { p1Gold += p1Interest; SetGoldGreen(1); }
            if (p2Interest > 0) { p2Gold += p2Interest; SetGoldGreen(2); }

            int p1MilestoneBonus = 0; int p2MilestoneBonus = 0;

            if (pendingWinner == 1 || pendingWinner == 2)
            {
                int step = GetMilestoneStep();

                int firstCheck = pendingWinner;
                int secondCheck = pendingWinner == 1 ? 2 : 1;

                int firstPoints = firstCheck == 1 ? p1Points : p2Points;
                int secondPoints = secondCheck == 1 ? p1Points : p2Points;
                int firstPrev = firstCheck == 1 ? prevP1Points : prevP2Points;
                int secondPrev = secondCheck == 1 ? prevP1Points : prevP2Points;

                if (firstPoints > 0 && firstPoints % step == 0 && firstPoints > firstPrev
                    && !globalClaimedMilestones.Contains(firstPoints))
                {
                    globalClaimedMilestones.Add(firstPoints);
                    DrawAndApplyNextReward(firstCheck);
                }

                if (secondPoints > 0 && secondPoints % step == 0 && secondPoints > secondPrev
                    && !globalClaimedMilestones.Contains(secondPoints))
                {
                    globalClaimedMilestones.Add(secondPoints);
                    DrawAndApplyNextReward(secondCheck);
                }
            }

            p1Gold += p1Reward; p2Gold += p2Reward; SetGoldGreen(1); SetGoldGreen(2);
            int p1AppliedIncome = IsIncomeAvailable() ? p1Income : 0;
            int p2AppliedIncome = IsIncomeAvailable() ? p2Income : 0;
            p1Gold += p1AppliedIncome; p2Gold += p2AppliedIncome;
            if (p1AppliedIncome > 0) SetGoldGreen(1);
            if (p2AppliedIncome > 0) SetGoldGreen(2);

            p1Calc = BuildCalcText(p1Start, p1Interest, p1Reward, p1AppliedIncome, p1MilestoneBonus, p1Gold);
            p2Calc = BuildCalcText(p2Start, p2Interest, p2Reward, p2AppliedIncome, p2MilestoneBonus, p2Gold);

            if (IsIncomeAvailable())
            {
                if (!p1BoughtIncomeThisRound) p1MissedIncomeRounds++; else p1MissedIncomeRounds = 0;
                if (!p2BoughtIncomeThisRound) p2MissedIncomeRounds++; else p2MissedIncomeRounds = 0;
                p1IncomeDecayPercent = GetIncomeDecayPercent(p1MissedIncomeRounds);
                p2IncomeDecayPercent = GetIncomeDecayPercent(p2MissedIncomeRounds);
            }
            else
            {
                p1MissedIncomeRounds = 0; p2MissedIncomeRounds = 0;
                p1IncomeDecayPercent = 0; p2IncomeDecayPercent = 0;
            }

            p1BoughtIncomeThisRound = false; p2BoughtIncomeThisRound = false;
            p1ReplayBoughtThisRound = false; p2ReplayBoughtThisRound = false;

            if (pendingWinner == 1 || pendingWinner == 2)
                lastRoundWinner = pendingWinner;

            round++;
            pendingWinner = 0;
            ResetTieTimer();
            UpdateUI();
            ShowMatchEndPromptIfNeeded(prevP1Points, prevP2Points);
        }

        private void P1BuyIncome_Click(object sender, RoutedEventArgs e)
        {
            if (!IsIncomeAvailable()) return;
            int cost = GetDisplayedIncomeCost(p1IncomeCost, p1IncomeDecayPercent, p1HasIncomeDiscount);
            if (p1BoughtIncomeThisRound) { ShowNotice(string.Format(T("IncomeAlreadyBought"), p1Name)); return; }
            if (p1Gold < cost) { ShowNotice(string.Format(T("NotEnoughGold"), p1Name)); return; }
            PushUndoState();
            ApplyGoldSpend(1, cost);
            int gain = ft20ModeEnabled ? 13 : 10;
            p1Income += gain; p1IncomeLevel++; p1IncomeUpgrades++;
            p1BoughtIncomeThisRound = true;
            p1MissedIncomeRounds = 0;
            p1IncomeCost = Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, p1IncomeUpgrades));
            if (p1HasIncomeDiscount) p1HasIncomeDiscount = false;
            SetGoldRed(1);
            AddActionLog(string.Format(T("LogBoughtIncome"), p1Name, gain, cost));
            UpdateUI();
        }

        private void P2BuyIncome_Click(object sender, RoutedEventArgs e)
        {
            if (!IsIncomeAvailable()) return;
            int cost = GetDisplayedIncomeCost(p2IncomeCost, p2IncomeDecayPercent, p2HasIncomeDiscount);
            if (p2BoughtIncomeThisRound) { ShowNotice(string.Format(T("IncomeAlreadyBought"), p2Name)); return; }
            if (p2Gold < cost) { ShowNotice(string.Format(T("NotEnoughGold"), p2Name)); return; }
            PushUndoState();
            ApplyGoldSpend(2, cost);
            int gain = ft20ModeEnabled ? 13 : 10;
            p2Income += gain; p2IncomeLevel++; p2IncomeUpgrades++;
            p2BoughtIncomeThisRound = true;
            p2MissedIncomeRounds = 0;
            p2IncomeCost = Math.Round(GetBaseIncomeCost() * (decimal)Math.Pow(1.24, p2IncomeUpgrades));
            if (p2HasIncomeDiscount) p2HasIncomeDiscount = false;
            SetGoldRed(2);
            AddActionLog(string.Format(T("LogBoughtIncome"), p2Name, gain, cost));
            UpdateUI();
        }

        private void P1BuyPermMove_Click(object sender, RoutedEventArgs e)
        {
            int cost = GetPermMoveCost();
            if (p1PermMoveUpgrades >= 2) { ShowNotice(string.Format(T("MaxedPermMove"), p1Name, 2)); return; }
            if (p1Gold < cost) { ShowNotice(string.Format(T("NotEnoughGold"), p1Name)); return; }
            PushUndoState();
            ApplyGoldSpend(1, cost); p1PermMoveUpgrades++;
            SetGoldRed(1);
            AddActionLog(string.Format(T("LogBoughtPermMove"), p1Name, cost));
            UpdateUI();
        }

        private void P2BuyPermMove_Click(object sender, RoutedEventArgs e)
        {
            int cost = GetPermMoveCost();
            if (p2PermMoveUpgrades >= 2) { ShowNotice(string.Format(T("MaxedPermMove"), p2Name, 2)); return; }
            if (p2Gold < cost) { ShowNotice(string.Format(T("NotEnoughGold"), p2Name)); return; }
            PushUndoState();
            ApplyGoldSpend(2, cost); p2PermMoveUpgrades++;
            SetGoldRed(2);
            AddActionLog(string.Format(T("LogBoughtPermMove"), p2Name, cost));
            UpdateUI();
        }

        private void P1SingleTroopMove_Click(object sender, RoutedEventArgs e)
        {
            int cost = GetSingleTroopMoveCost();
            if (p1Gold < cost) { ShowNotice(string.Format(T("NotEnoughGoldAmount"), p1Name, cost)); return; }
            PushUndoState(); ApplyGoldSpend(1, cost); SetGoldRed(1);
            AddActionLog(string.Format(T("LogSingleTroopMove"), p1Name, cost));
            UpdateUI();
        }

        private void P2SingleTroopMove_Click(object sender, RoutedEventArgs e)
        {
            int cost = GetSingleTroopMoveCost();
            if (p2Gold < cost) { ShowNotice(string.Format(T("NotEnoughGoldAmount"), p2Name, cost)); return; }
            PushUndoState(); ApplyGoldSpend(2, cost); SetGoldRed(2);
            AddActionLog(string.Format(T("LogSingleTroopMove"), p2Name, cost));
            UpdateUI();
        }

        private void P1Replay_Click(object sender, RoutedEventArgs e)
        {
            if (p1ReplayBoughtThisRound) { ShowNotice(string.Format(T("ReplayAlreadyBought"), p1Name)); return; }
            if (p1Gold < 10) { ShowNotice(string.Format(T("NotEnoughGoldAmount"), p1Name, 10)); return; }

            PushUndoState();
            ApplyGoldSpend(1, 10);
            p1ReplayBoughtThisRound = true;
            SetGoldRed(1);
            AddActionLog(string.Format(T("LogReplay"), p1Name));
            UpdateUI();
        }

        private void P2Replay_Click(object sender, RoutedEventArgs e)
        {
            if (p2ReplayBoughtThisRound) { ShowNotice(string.Format(T("ReplayAlreadyBought"), p2Name)); return; }
            if (p2Gold < 10) { ShowNotice(string.Format(T("NotEnoughGoldAmount"), p2Name, 10)); return; }

            PushUndoState();
            ApplyGoldSpend(2, 10);
            p2ReplayBoughtThisRound = true;
            SetGoldRed(2);
            AddActionLog(string.Format(T("LogReplay"), p2Name));
            UpdateUI();
        }

        private void P1Spend_Click(object sender, RoutedEventArgs e)
        {
            int amount = ReadNumber(P1SpendBox);
            if (amount <= 0) { ShowNotice(T("EnterValidSpendAmount")); return; }
            if (p1Gold < amount) { ShowNotice(string.Format(T("NotEnoughGold"), p1Name)); return; }
            PushUndoState(); ApplyGoldSpend(1, amount); SetGoldRed(1);
            AddActionLog(string.Format(T("LogSpentTroops"), p1Name, amount));
            UpdateUI();
        }

        private void P2Spend_Click(object sender, RoutedEventArgs e)
        {
            int amount = ReadNumber(P2SpendBox);
            if (amount <= 0) { ShowNotice(T("EnterValidSpendAmount")); return; }
            if (p2Gold < amount) { ShowNotice(string.Format(T("NotEnoughGold"), p2Name)); return; }
            PushUndoState(); ApplyGoldSpend(2, amount); SetGoldRed(2);
            AddActionLog(string.Format(T("LogSpentTroops"), p2Name, amount));
            UpdateUI();
        }

        private void P1SellUnit_Click(object sender, RoutedEventArgs e)
        {
            int value = ReadNumber(P1UnitBox);
            if (value <= 0) { ShowNotice(T("EnterValidUnitValue")); return; }
            PushUndoState();
            int refund;
            if (p1HasFullRefund) { refund = value; p1HasFullRefund = false; AddActionLog(string.Format(T("LogFullRefundSell"), p1Name, refund)); ShowNotice(string.Format(T("NoticeFullRefundSell"), p1Name, refund)); }
            else { refund = (int)Math.Floor(value * (p1SellbackPct / 100.0)); AddActionLog(string.Format(T("LogSoldUnit"), p1Name, value, refund, p1SellbackPct)); }
            p1Gold += refund; SetGoldGreen(1);
            UpdateUI();
        }

        private void P2SellUnit_Click(object sender, RoutedEventArgs e)
        {
            int value = ReadNumber(P2UnitBox);
            if (value <= 0) { ShowNotice(T("EnterValidUnitValue")); return; }
            PushUndoState();
            int refund;
            if (p2HasFullRefund) { refund = value; p2HasFullRefund = false; AddActionLog(string.Format(T("LogFullRefundSell"), p2Name, refund)); ShowNotice(string.Format(T("NoticeFullRefundSell"), p2Name, refund)); }
            else { refund = (int)Math.Floor(value * (p2SellbackPct / 100.0)); AddActionLog(string.Format(T("LogSoldUnit"), p2Name, value, refund, p2SellbackPct)); }
            p2Gold += refund; SetGoldGreen(2);
            UpdateUI(); ;
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
            UpdateStaticText();
            UpdateLanguageSelectorUI();
            UpdateSettingsButtonStyles(_isBorderlessFullscreen);
            UpdateSoundSettingsUI();
        }

        private void GuideButton_Click(object sender, RoutedEventArgs e)
        {
            GuideOverlay.Visibility = Visibility.Visible;
            SettingsOverlay.Visibility = Visibility.Collapsed;
            PopulateGuideContent();
        }

        private void SettingsBackButton_Click(object sender, RoutedEventArgs e)
        {
            SettingsOverlay.Visibility = Visibility.Collapsed;
        }

        private void GuideBackButton_Click(object sender, RoutedEventArgs e)
        {
            GuideOverlay.Visibility = Visibility.Collapsed;
        }

        private void P1PopOut_Click(object sender, RoutedEventArgs e) => PopOutGold(1);
        private void P2PopOut_Click(object sender, RoutedEventArgs e) => PopOutGold(2);

        private void PopOutGold(int player)
        {
            var existing = player == 1 ? p1GoldWindow : p2GoldWindow;
            if (existing != null)
            {
                existing.Activate();
                return;
            }

            var window = new GoldPopOutWindow(
    player == 1 ? p1Name : p2Name,
    player == 1 ? p1Gold : p2Gold,
    GetGoldVisualState(player),
    player == 1 ? p1Factions : p2Factions,
    FactionIconMap,
    () =>
    {
                    if (player == 1)
                    {
                        p1GoldWindow = null;
                    }
                    else
                    {
                        p2GoldWindow = null;
                    }
                });

            if (player == 1)
            {
                p1GoldWindow = window;
            }
            else
            {
                p2GoldWindow = window;
            }

            window.Show();
        }

        private int GetGoldVisualState(int player)
        {
            var brush = player == 1 ? P1GoldBorder.Background : P2GoldBorder.Background;
            if (ReferenceEquals(brush, greenBrush)) return 1;
            if (ReferenceEquals(brush, redBrush)) return -1;
            return 0;
        }

        private void CloseAllGoldWindows()
        {
            p1GoldWindow?.Close();
            p1GoldWindow = null;

            p2GoldWindow?.Close();
            p2GoldWindow = null;
        }
        private void MainMenuButton_Click(object sender, RoutedEventArgs e)
        {
            if (!ShowConfirm(T("MainMenuConfirmTitle"), T("MainMenuConfirmMsg"))) return;

            bool borderless = AppPrefs.WindowMode == SavedWindowMode.BorderlessFullscreen;

            var screen = System.Windows.Forms.Screen.FromHandle(
                new System.Windows.Interop.WindowInteropHelper(this).Handle);

            var menu = new StartScreen
            {
                WindowStartupLocation = WindowStartupLocation.Manual,
                WindowState = WindowState.Normal,
                Left = borderless ? screen.Bounds.Left : Left,
                Top = borderless ? screen.Bounds.Top : Top,
                Width = borderless ? screen.Bounds.Width : Width,
                Height = borderless ? screen.Bounds.Height : Height
            };

            CloseAllGoldWindows();
            menu.Show();
            Close();
        }

        private void PopulateGuideContent()
        {
            GuideTitleText.Text = T("GuideTitle");
            GuideContentPanel.Children.Clear();

            AddGuideSection(T("GuideBasicsTitle"), T("GuideBasicsBody"));
            AddGuideSection(T("GuideTurnOrderTitle"), T("GuideTurnOrderBody"));
            AddGuideSection(T("GuideRoundReplayTitle"), T("GuideRoundReplayBody"));
            AddGuideSection(T("GuideEconomyTitle"), T("GuideEconomyBody"));
            AddGuideSection(T("GuideSavingTitle"), T("GuideSavingBody"));
            AddGuideLinkSection();
        }

        private void AddGuideSection(string title, string body)
        {
            var stack = new StackPanel();

            stack.Children.Add(new TextBlock
            {
                Text = title,
                FontSize = 17,
                FontWeight = FontWeights.Bold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 8)
            });

            stack.Children.Add(new TextBlock
            {
                Text = body,
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            });

            GuideContentPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 34, 37)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Margin = new Thickness(0, 0, 0, 10),
                Child = stack
            });
        }

        private void AddGuideLinkSection()
        {
            var text = new TextBlock
            {
                FontSize = 14,
                Foreground = new SolidColorBrush(Color.FromRgb(220, 226, 235)),
                TextWrapping = TextWrapping.Wrap,
                LineHeight = 21
            };

            text.Inlines.Add(new Run(T("GuideMoreBody") + " "));

            var link = new Hyperlink(new Run("ofallzei/TABS-Arena"))
            {
                NavigateUri = new Uri("https://github.com/ofallzei/TABS-Arena"),
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

            GuideContentPanel.Children.Add(new Border
            {
                Background = new SolidColorBrush(Color.FromRgb(32, 34, 37)),
                CornerRadius = new CornerRadius(10),
                Padding = new Thickness(14),
                Child = text
            });
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
                AppPrefs.WindowMode = borderless ? SavedWindowMode.BorderlessFullscreen : SavedWindowMode.Windowed;
                AppPrefs.Language = ToSharedLanguage(currentLanguage);
                AppPrefs.Save();
            }

            WindowState = WindowState.Normal;
            WindowStyle = WindowStyle.None;

            System.Windows.Shell.WindowChrome.SetWindowChrome(this, new System.Windows.Shell.WindowChrome
            {
                CaptionHeight = 0,
                ResizeBorderThickness = new Thickness(8),
                GlassFrameThickness = new Thickness(0),
                CornerRadius = new CornerRadius(0),
                UseAeroCaptionButtons = false
            });

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
            if (SettingsWindowModeText == null) return;

            SettingsWindowModeText.Text = isFullscreen ? T("BorderlessFullscreen") : T("Windowed");

            SettingsDot1.Background = !isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));

            SettingsDot2.Background = isFullscreen
                ? new SolidColorBrush(Color.FromRgb(110, 182, 218))
                : new SolidColorBrush(Color.FromRgb(58, 74, 88));
        }

        private void SettingsLanguageLeft_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(PreviousLanguage(currentLanguage));
        }

        private void SettingsLanguageRight_Click(object sender, RoutedEventArgs e)
        {
            ApplyLanguage(NextLanguage(currentLanguage));
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

            WindowStyle = WindowStyle.None;
            ResizeMode = ResizeMode.CanResize;
            CustomTitleBar.Visibility = Visibility.Visible;
            CustomTitleBarRow.Height = new GridLength(40);

            UpdateSettingsButtonStyles(false);
        }

        private void WindowClose_Click(object sender, RoutedEventArgs e)
        {
            if (!ShowConfirm(T("CloseGameConfirmTitle"), T("CloseGameConfirmMsg")))
                return;

            Close();
        }

        private void CustomTitleBar_MouseDown(object sender, MouseButtonEventArgs e)
        {
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

        private void ApplyLanguage(AppLanguage lang)
        {
            currentLanguage = lang;
            SaveLanguage();

            if (IsNoRoundYetText(p1Calc)) p1Calc = T("NoRoundYet");
            if (IsNoRoundYetText(p2Calc)) p2Calc = T("NoRoundYet");

            UpdateLanguageSelectorUI();
            UpdateSoundSettingsUI();
            UpdateStaticText();
            UpdateUI();

            if (GuideOverlay.Visibility == Visibility.Visible)
                PopulateGuideContent();
        }

        private void UpdateLanguageSelectorUI()
        {
            if (SettingsLanguageText == null) return;

            SettingsLanguageText.Text = GetLanguageDisplayName(currentLanguage);
            TwoVTwoGameMode.Loc.UpdateLanguageFlag(SettingsLanguageFlag, ToSharedLanguage(currentLanguage));

            SetLanguageDot(SettingsLangDot1, currentLanguage == AppLanguage.English);
            SetLanguageDot(SettingsLangDot2, currentLanguage == AppLanguage.Spanish);
            SetLanguageDot(SettingsLangDot3, currentLanguage == AppLanguage.Russian);
            SetLanguageDot(SettingsLangDot4, currentLanguage == AppLanguage.Chinese);
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
            SettingsSoundsToggleButton.Content = T("Sounds") + ": " + T(AppPrefs.SoundsEnabled ? "On" : "Off");
            SettingsSoundsToggleButton.Background = AppPrefs.SoundsEnabled
                ? new SolidColorBrush(Color.FromRgb(49, 95, 125))
                : new SolidColorBrush(Color.FromRgb(49, 56, 67));

            SettingsSoundVolumeSlider.Value = volumePercent;
            SettingsSoundVolumeSlider.IsEnabled = AppPrefs.SoundsEnabled;
            SettingsSoundVolumeText.Text = volumePercent + "%";
            SettingsSoundVolumeText.Opacity = AppPrefs.SoundsEnabled ? 1.0 : 0.45;
            SettingsSoundVolumeLabel.Opacity = AppPrefs.SoundsEnabled ? 1.0 : 0.45;
        }

        private void SettingsScrollViewer_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            var sv = sender as ScrollViewer;
            if (sv == null) return;

            bool atTop = sv.VerticalOffset <= 0;
            bool atBottom = sv.VerticalOffset >= sv.ScrollableHeight;

            if ((!atTop && e.Delta > 0) || (!atBottom && e.Delta < 0))
                return;

            e.Handled = true;
            var args = new MouseWheelEventArgs(e.MouseDevice, e.Timestamp, e.Delta)
            {
                RoutedEvent = UIElement.MouseWheelEvent,
                Source = sender
            };
            AppScroll.RaiseEvent(args);
        }

        private bool CanNestedScrollViewerHandleWheel(MouseWheelEventArgs e)
        {
            var source = e.OriginalSource as DependencyObject;
            while (source != null)
            {
                var sv = source as ScrollViewer;
                if (sv == SettingsScrollViewer || sv == GuideScrollViewer)
                    return (e.Delta > 0 && sv.VerticalOffset > 0)
                        || (e.Delta < 0 && sv.VerticalOffset < sv.ScrollableHeight);

                source = GetUiParent(source);
            }

            return false;
        }

        private static DependencyObject GetUiParent(DependencyObject source)
        {
            if (source == null) return null;

            DependencyObject parent = null;
            try
            {
                parent = VisualTreeHelper.GetParent(source);
            }
            catch (InvalidOperationException)
            {
            }

            if (parent != null) return parent;

            var element = source as FrameworkElement;
            if (element != null) return element.Parent;

            var contentElement = source as FrameworkContentElement;
            return contentElement != null ? contentElement.Parent : null;
        }

        private void Window_PreviewMouseWheel(object sender, MouseWheelEventArgs e)
        {
            if ((Keyboard.Modifiers & ModifierKeys.Control) == ModifierKeys.Control)
            {
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
                return;
            }

            if (AppScroll == null) return;
            if (CanNestedScrollViewerHandleWheel(e)) return;

            AppScroll.ScrollToVerticalOffset(AppScroll.VerticalOffset - (e.Delta * 0.5));
            e.Handled = true;
        }
        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
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
                    ReferenceEquals(current, P2NameEditButton))
                    return true;

                current = VisualTreeHelper.GetParent(current);
            }

            return false;
        }

        private void TryLockFocusedNameBox(TextBox focusedBox)
        {
            if (ReferenceEquals(focusedBox, P1NameBox) && !P1NameBox.IsReadOnly) { LockNameBox(1); return; }
            if (ReferenceEquals(focusedBox, P2NameBox) && !P2NameBox.IsReadOnly) { LockNameBox(2); return; }
        }
    }



    // ── Save data model ───────────────────────────────────────────────────────
    public class OneV1SaveData
    {
        public int SaveVersion { get; set; }
        public int LastRoundWinner { get; set; }
        public int FirstTurnPlayer { get; set; }
        public bool P1ReplayBoughtThisRound { get; set; }
        public bool P2ReplayBoughtThisRound { get; set; }
        public string SaveName { get; set; }
        public DateTime SavedAt { get; set; }
        public int Round { get; set; }
        public int PendingWinner { get; set; }
        public bool NamesLocked { get; set; }
        public bool ResetArmed { get; set; }
        public bool FirstTurnChosen { get; set; }
        public int P1Gold { get; set; }
        public int P2Gold { get; set; }
        public int P1Points { get; set; }
        public int P2Points { get; set; }
        public int P1Income { get; set; }
        public int P2Income { get; set; }
        public int P1PermMoveUpgrades { get; set; }
        public int P2PermMoveUpgrades { get; set; }
        public int P1MilestonePermMoveUpgrades { get; set; }
        public int P2MilestonePermMoveUpgrades { get; set; }
        public int P1IncomeUpgrades { get; set; }
        public int P2IncomeUpgrades { get; set; }
        public int P1IncomeLevel { get; set; }
        public int P2IncomeLevel { get; set; }
        public decimal P1IncomeCost { get; set; }
        public decimal P2IncomeCost { get; set; }
        public bool P1BoughtIncomeThisRound { get; set; }
        public bool P2BoughtIncomeThisRound { get; set; }
        public bool P1HasIncomeDiscount { get; set; }
        public bool P2HasIncomeDiscount { get; set; }
        public bool P1HasFullRefund { get; set; }
        public bool P2HasFullRefund { get; set; }
        public string P1Name { get; set; }
        public string P2Name { get; set; }
        public string P1Calc { get; set; }
        public string P2Calc { get; set; }
        public bool P1HasFt10PermMove { get; set; }
        public bool P2HasFt10PermMove { get; set; }
        public bool Milestone5Claimed { get; set; }
        public bool Milestone10Claimed { get; set; }
        public bool Milestone15Claimed { get; set; }
        public bool Milestone20Claimed { get; set; }
        public bool Milestone25Claimed { get; set; }
        public List<int> GlobalClaimedMilestones { get; set; }
        public List<string> MilestoneRewardQueue { get; set; }
        public int P1SellbackPct { get; set; }
        public int P2SellbackPct { get; set; }
        public bool P1Sellback70 { get; set; }
        public bool P2Sellback70 { get; set; }
        public int P1MissedIncomeRounds { get; set; }
        public int P2MissedIncomeRounds { get; set; }
        public int P1IncomeDecayPercent { get; set; }
        public int P2IncomeDecayPercent { get; set; }
        public bool FactionModeEnabled { get; set; }
        public bool FactionModeLocked { get; set; }
        public int P1FactionPurchases { get; set; }
        public int P2FactionPurchases { get; set; }
        public int P1ChosenFactionPurchases { get; set; }
        public int P2ChosenFactionPurchases { get; set; }
        public List<string> P1Factions { get; set; }
        public List<string> P2Factions { get; set; }
        public bool Ft20ModeEnabled { get; set; }
        public bool Ft10ModeEnabled { get; set; }
        public bool Ft30ModeEnabled { get; set; }
        public bool Ft20ModeLocked { get; set; }
        public bool MatchEndPromptSuppressed { get; set; }
        public List<string> Ft20MilestonePool { get; set; }
        public int Ft20NextMilestoneRound { get; set; }
        public List<string> ActionLog { get; set; }
    }
}
