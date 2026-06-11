namespace Tabs.Bot;

public sealed record ArmyUnit(string Slug, string Faction, string Name, int Gold);

public static class ArmyCatalog
{
    public const int UnitsPerPage = 25;

    public static readonly IReadOnlyList<ArmyUnit> Units = new[]
    {
        new ArmyUnit("tribal-clubber", "Tribal", "Clubber", 70),
        new ArmyUnit("tribal-protector", "Tribal", "Protector", 80),
        new ArmyUnit("tribal-spear-thrower", "Tribal", "Spear thrower \u2B06\uFE0F", 100),
        new ArmyUnit("tribal-stoner", "Tribal", "Stoner", 160),
        new ArmyUnit("tribal-bone-mage", "Tribal", "Bone mage", 300),
        new ArmyUnit("tribal-chieftan", "Tribal", "Chieftan", 400),
        new ArmyUnit("tribal-mammoth", "Tribal", "Mammoth", 2200),

        new ArmyUnit("farmer-halfling", "Farmer", "Halfling", 50),
        new ArmyUnit("farmer-farmer", "Farmer", "Farmer", 80),
        new ArmyUnit("farmer-hay-baler", "Farmer", "Hay Baler \u2B07\uFE0F", 140),
        new ArmyUnit("farmer-potionseller", "Farmer", "Potionseller", 340),
        new ArmyUnit("farmer-harvester", "Farmer", "Harvester", 500),
        new ArmyUnit("farmer-wheelbarrow", "Farmer", "Wheelbarrow", 1000),
        new ArmyUnit("farmer-scarecrow", "Farmer", "Scarecrow", 1200),

        new ArmyUnit("medieval-bard", "Medieval", "Bard", 60),
        new ArmyUnit("medieval-squire", "Medieval", "Squire", 100),
        new ArmyUnit("medieval-archer", "Medieval", "Archer", 140),
        new ArmyUnit("medieval-1v1-healer", "Medieval", "1v1 healer", 180),
        new ArmyUnit("medieval-2v2-healer", "Medieval", "2v2 healer \u2B07\uFE0F", 180),
        new ArmyUnit("medieval-knight", "Medieval", "Knight", 650),
        new ArmyUnit("medieval-catapult", "Medieval", "Catapult \u2B06\uFE0F", 800),
        new ArmyUnit("medieval-the-king", "Medieval", "The King", 1500),

        new ArmyUnit("ancient-shield-bearer", "Ancient", "Shield Bearer", 100),
        new ArmyUnit("ancient-sarissa", "Ancient", "Sarissa", 120),
        new ArmyUnit("ancient-hoplite", "Ancient", "Hoplite", 180),
        new ArmyUnit("ancient-snake-archer", "Ancient", "Snake Archer \u2B07\uFE0F", 300),
        new ArmyUnit("ancient-ballista", "Ancient", "Ballista \u2B07\uFE0F", 900),
        new ArmyUnit("ancient-minotaur", "Ancient", "Minotaur", 1600),
        new ArmyUnit("ancient-zeus", "Ancient", "Zeus", 2000),

        new ArmyUnit("viking-headbutter", "Viking", "Headbutter", 90),
        new ArmyUnit("viking-ice-archer", "Viking", "Ice Archer \u2B06\uFE0F", 140),
        new ArmyUnit("viking-brawler", "Viking", "Brawler", 220),
        new ArmyUnit("viking-berserker", "Viking", "Berserker", 250),
        new ArmyUnit("viking-valkyrie", "Viking", "Valkyrie", 500),
        new ArmyUnit("viking-longship", "Viking", "Longship", 1000),
        new ArmyUnit("viking-jarl", "Viking", "Jarl", 1500),

        new ArmyUnit("dynasty-firework-archer", "Dynasty", "Firework Archer", 180),
        new ArmyUnit("dynasty-samurai", "Dynasty", "Samurai", 250),
        new ArmyUnit("dynasty-monk", "Dynasty", "Monk \u2B07\uFE0F", 250),
        new ArmyUnit("dynasty-ninja", "Dynasty", "Ninja", 500),
        new ArmyUnit("dynasty-hwacha", "Dynasty", "Hwacha", 1500),
        new ArmyUnit("dynasty-monkey-king", "Dynasty", "Monkey King", 2000),

        new ArmyUnit("renaissance-painter", "Renaissance", "Painter", 50),
        new ArmyUnit("renaissance-fencer", "Renaissance", "Fencer", 150),
        new ArmyUnit("renaissance-balloon-archer", "Renaissance", "Balloon Archer", 200),
        new ArmyUnit("renaissance-musketeer", "Renaissance", "Musketeer", 250),
        new ArmyUnit("renaissance-halberd", "Renaissance", "Halberd", 400),
        new ArmyUnit("renaissance-jouster", "Renaissance", "Jouster", 1000),
        new ArmyUnit("renaissance-da-vinci-tank", "Renaissance", "Da Vinci Tank", 4000),

        new ArmyUnit("pirate-flintlock", "Pirate", "Flintlock", 100),
        new ArmyUnit("pirate-blunderbuss", "Pirate", "blunderbuss", 160),
        new ArmyUnit("pirate-bomb-thrower", "Pirate", "bomb thrower", 250),
        new ArmyUnit("pirate-harpooner", "Pirate", "Harpooner \u2B07\uFE0F", 300),
        new ArmyUnit("pirate-cannon", "Pirate", "Cannon", 1000),
        new ArmyUnit("pirate-captain", "Pirate", "Captain", 1500),
        new ArmyUnit("pirate-pirate-queen", "Pirate", "Pirate Queen", 2500),

        new ArmyUnit("spooky-skeleton-warrior", "Spooky", "Skeleton Warrior", 80),
        new ArmyUnit("spooky-skeleton-archer", "Spooky", "Skeleton archer", 180),
        new ArmyUnit("spooky-candlehead", "Spooky", "Candlehead", 200),
        new ArmyUnit("spooky-vampire", "Spooky", "Vampire \u2B07\uFE0F", 300),
        new ArmyUnit("spooky-pumpkin-catapult", "Spooky", "Pumpkin Catapult", 1000),
        new ArmyUnit("spooky-swordcaster", "Spooky", "Swordcaster", 1000),
        new ArmyUnit("spooky-reaper", "Spooky", "Reaper", 2500),

        new ArmyUnit("wild-west-dynamite-thrower", "Wild West", "Dynamite Thrower", 100),
        new ArmyUnit("wild-west-miner", "Wild West", "MIner", 200),
        new ArmyUnit("wild-west-cactus", "Wild West", "Cactus", 400),
        new ArmyUnit("wild-west-gunslinger", "Wild West", "Gunslinger \u2B07\uFE0F", 650),
        new ArmyUnit("wild-west-lasso", "Wild West", "Lasso", 740),
        new ArmyUnit("wild-west-deadeye", "Wild West", "Deadeye", 900),
        new ArmyUnit("wild-west-quick-draw", "Wild West", "Quick Draw", 1200),

        new ArmyUnit("legacy-peasant", "Legacy", "Peasant", 40),
        new ArmyUnit("legacy-banner-bearer", "Legacy", "Banner Bearer", 150),
        new ArmyUnit("legacy-poacher", "Legacy", "Poacher \u2B07\uFE0F", 155),
        new ArmyUnit("legacy-blowdarter", "Legacy", "Blowdarter", 220),
        new ArmyUnit("legacy-pike", "Legacy", "Pike", 300),
        new ArmyUnit("legacy-barrel-roller", "Legacy", "Barrel Roller", 350),
        new ArmyUnit("legacy-boxer", "Legacy", "Boxer", 450),
        new ArmyUnit("legacy-flag-bearer", "Legacy", "Flag Bearer", 500),
        new ArmyUnit("legacy-pharaoh", "Legacy", "Pharaoh", 750),
        new ArmyUnit("legacy-wizard", "Legacy", "Wizard \u2B07\uFE0F", 1200),
        new ArmyUnit("legacy-chariot", "Legacy", "Chariot", 1800),
        new ArmyUnit("legacy-thor", "Legacy", "Thor", 2200),
        new ArmyUnit("legacy-tank", "Legacy", "Tank", 6000),
        new ArmyUnit("legacy-super-boxer", "Legacy", "Super Boxer", 10000),

        new ArmyUnit("good-devout-gauntlet", "Good", "Devout Gauntlet \u2B07\uFE0F", 200),
        new ArmyUnit("good-celestial-aegis", "Good", "Celestial Aegis", 280),
        new ArmyUnit("good-radiant-glaive", "Good", "Radiant Glaive", 500),
        new ArmyUnit("good-righteous-paladin", "Good", "Righteous Paladin", 800),
        new ArmyUnit("good-divine-arbiter", "Good", "Divine Arbiter \u2B07\uFE0F", 1000),
        new ArmyUnit("good-sacred-elephant", "Good", "Sacred Elephant", 2000),
        new ArmyUnit("good-chronomancer", "Good", "Chronomancer", 3000),

        new ArmyUnit("evil-shadow-walker", "Evil", "Shadow Walker", 200),
        new ArmyUnit("evil-exiled-sentinel", "Evil", "Exiled Sentinel", 300),
        new ArmyUnit("evil-mad-mechanic", "Evil", "Mad Mechanic \u2B07\uFE0F", 500),
        new ArmyUnit("evil-void-cultist", "Evil", "Void Cultist", 800),
        new ArmyUnit("evil-tempest-lich", "Evil", "Tempest Lich \u2B07\uFE0F", 1000),
        new ArmyUnit("evil-death-bringer", "Evil", "Death Bringer \u2B06\uFE0F", 2000),
        new ArmyUnit("evil-void-monarch", "Evil", "Void Monarch", 3000),

        new ArmyUnit("secret-bomb-on-a-stick", "Secret", "Bomb On A Stick", 150),
        new ArmyUnit("secret-ballooner", "Secret", "Ballooner \u2B07\uFE0F", 200),
        new ArmyUnit("secret-fan-bearer", "Secret", "Fan Bearer", 200),
        new ArmyUnit("secret-the-teacher", "Secret", "The Teacher \uD83D\uDD04", 230),
        new ArmyUnit("secret-raptor", "Secret", "Raptor \u2B07\uFE0F", 280),
        new ArmyUnit("secret-jester", "Secret", "Jester", 300),
        new ArmyUnit("secret-ball-n-chain", "Secret", "Ball \"n\" Chain \u2B06\uFE0F", 350),
        new ArmyUnit("secret-chu-ko-nu", "Secret", "Chu Ko Nu", 350),
        new ArmyUnit("secret-executioner", "Secret", "Executioner", 350),
        new ArmyUnit("secret-shouter", "Secret", "Shouter", 400),
        new ArmyUnit("secret-raptor-rider", "Secret", "Raptor Rider \u2B07\uFE0F", 450),
        new ArmyUnit("secret-taekwondo", "Secret", "Taekwondo", 500),
        new ArmyUnit("secret-cupid", "Secret", "Cupid", 500),
        new ArmyUnit("secret-mace-spinner", "Secret", "Mace Spinner", 500),
        new ArmyUnit("secret-clams", "Secret", "Clams \u2B07\uFE0F", 500),
        new ArmyUnit("secret-cheerleader", "Secret", "Cheerleader \u2B07\uFE0F", 600),
        new ArmyUnit("secret-ice-mage", "Secret", "Ice Mage", 650),
        new ArmyUnit("secret-infernal-whip", "Secret", "Infernal Whip", 800),
        new ArmyUnit("secret-bank-robbers", "Secret", "Bank Robbers", 850),
        new ArmyUnit("secret-witch", "Secret", "Witch", 1000),
        new ArmyUnit("secret-banshee", "Secret", "Banshee", 1100),
        new ArmyUnit("secret-necromancer", "Secret", "Necromancer", 1200),
        new ArmyUnit("secret-solar-architect", "Secret", "Solar Architect", 1200),
        new ArmyUnit("secret-wheelbarrow-dragon", "Secret", "Wheelbarrow Dragon", 1400),
        new ArmyUnit("secret-bomb-cannon", "Secret", "Bomb Cannon", 1500),
        new ArmyUnit("secret-skeleton-giant", "Secret", "Skeleton Giant", 1700),
        new ArmyUnit("secret-cavalry", "Secret", "Cavalry", 1800),
        new ArmyUnit("secret-vlad", "Secret", "Vlad", 1800),
        new ArmyUnit("secret-gatling-gun", "Secret", "Gatling Gun", 2000),
        new ArmyUnit("secret-blackbeard", "Secret", "Blackbeard", 2600),
        new ArmyUnit("secret-samurai-giant", "Secret", "Samurai Giant", 3000),
        new ArmyUnit("secret-ullr", "Secret", "Ullr", 3000),
        new ArmyUnit("secret-lady-red-jade", "Secret", "Lady Red Jade", 3500),
        new ArmyUnit("secret-sensei", "Secret", "Sensei", 3500),
        new ArmyUnit("secret-shogun", "Secret", "Shogun", 3500),
        new ArmyUnit("secret-tree-giant", "Secret", "Tree Giant", 4000),
        new ArmyUnit("secret-artemis", "Secret", "Artemis", 5500),
        new ArmyUnit("secret-ice-giant", "Secret", "Ice Giant", 6000),

        new ArmyUnit("new-units-gatherer", "New Units", "Gatherer", 105),
        new ArmyUnit("new-units-manipulator", "New Units", "Manipulator", 250),
        new ArmyUnit("new-units-chief-jones", "New Units", "Chief Jones", 300),
        new ArmyUnit("new-units-bomber-archer", "New Units", "Bomber Archer", 380),
        new ArmyUnit("new-units-alien-pod", "New Units", "Alien Pod", 850),
        new ArmyUnit("new-units-rajput-assassin", "New Units", "Rajput Assassin", 1000),

        new ArmyUnit("new-units-2-thief", "New Units 2", "Thief", 140),
        new ArmyUnit("new-units-2-haunter", "New Units 2", "Haunter", 220),
        new ArmyUnit("new-units-2-shield-spear", "New Units 2", "Shield Spear", 280),
        new ArmyUnit("new-units-2-poison-rogue", "New Units 2", "Poison Rogue", 350),
        new ArmyUnit("new-units-2-mini-drone", "New Units 2", "Mini Drone", 750),
        new ArmyUnit("new-units-2-horseoncrack", "New Units 2", "HorseOnCrack", 1000),
        new ArmyUnit("new-units-2-micolash", "New Units 2", "MicoLash", 1500)
    };

    public static IReadOnlyList<string> FactionsWithUnits =>
        Units.Select(unit => unit.Faction).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(name => name).ToList();

    public static ArmyUnit? FindUnit(string slug)
    {
        return Units.FirstOrDefault(unit => string.Equals(unit.Slug, slug, StringComparison.OrdinalIgnoreCase));
    }

    public static IReadOnlyList<ArmyUnit> UnitsForFaction(string faction)
    {
        return Units
            .Where(unit => string.Equals(unit.Faction, faction, StringComparison.OrdinalIgnoreCase))
            .OrderBy(unit => unit.Gold)
            .ThenBy(unit => unit.Name)
            .ToList();
    }
}
