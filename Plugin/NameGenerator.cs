using System;
using System.IO;

namespace DraftSync.Plugin
{
    /// <summary>
    /// Generates fun two-word names for synced notes (e.g. "fuzzy-lobster").
    /// </summary>
    public static class NameGenerator
    {
        private static readonly Random _rng = new Random(
            Environment.TickCount ^ (Environment.MachineName.GetHashCode()));

        // 200 adjectives (all unique)
        private static readonly string[] Adjectives =
        {
            "fuzzy", "turbo", "cosmic", "sneaky", "dizzy", "velvet", "neon", "quantum", "sleepy", "grumpy",
            "wobbly", "zesty", "crispy", "phantom", "solar", "atomic", "rusty", "bouncy", "mystic", "pixel",
            "copper", "frozen", "golden", "hollow", "ivory", "jolly", "lunar", "marble", "nimble", "orange",
            "plucky", "quirky", "rapid", "silver", "tangy", "ultra", "vivid", "wicked", "zippy", "amber",
            "blazing", "chunky", "dapper", "eager", "frosty", "gentle", "hasty", "icy", "jazzy", "keen",
            "lanky", "mellow", "nutty", "olive", "peppy", "quaint", "rosy", "spicy", "toasty", "urban",
            "witty", "young", "zealous", "bold", "calm", "dark", "easy", "fair", "glad", "hazy",
            "idle", "jumpy", "kind", "lazy", "mild", "neat", "odd", "pale", "quiet", "rich",
            "safe", "tall", "vast", "warm", "extra", "fresh", "grand", "happy", "iron", "lucky",
            "magic", "noble", "opal", "proud", "royal", "sharp", "swift", "tough", "wise", "brave",
            "coral", "dusty", "elfin", "fiery", "giddy", "hefty", "itchy", "jiffy", "knack", "lofty",
            "merry", "nerdy", "onyx", "perky", "rebel", "smoky", "timid", "stark", "wacky", "yappy",
            "agile", "breezy", "classy", "dazzling", "eclectic", "flashy", "glossy", "honest", "brash", "kinky",
            "lively", "moody", "noisy", "ornate", "punchy", "ritzy", "snappy", "trendy", "unique", "velvety",
            "wavy", "xenial", "youthful", "zappy", "artsy", "bubbly", "cheeky", "dreamy", "edgy", "funky",
            "gloomy", "hippy", "indie", "spry", "kooky", "loopy", "misty", "nifty", "outlaw", "prissy",
            "balmy", "rowdy", "sassy", "tidy", "upbeat", "vexed", "weary", "xeric", "yanky", "wiry",
            "ashen", "beefy", "crisp", "damp", "earthy", "flaky", "gaudy", "husky", "inky", "juicy",
            "lithe", "leafy", "murky", "nervy", "ornery", "pithy", "queasy", "rustic", "silky", "tacky",
            "unruly", "vapid", "wordy", "exact", "tawny", "zonal", "astral", "bendy", "plush", "mossy",
        };

        // 200 nouns
        private static readonly string[] Nouns =
        {
            "lobster", "pancake", "waffle", "thunder", "mango", "donut", "falcon", "cactus", "penguin", "pretzel",
            "walrus", "badger", "turnip", "rocket", "pebble", "dragon", "otter", "pickle", "comet", "acorn",
            "biscuit", "candle", "dagger", "eclipse", "ferret", "goblin", "hammer", "igloo", "jigsaw", "kettle",
            "lantern", "muffin", "nebula", "orchid", "parrot", "quasar", "raven", "sphinx", "teapot", "urchin",
            "violin", "whistle", "yeti", "zephyr", "anchor", "button", "cobalt", "delta", "ember", "flagon",
            "gazelle", "hermit", "indigo", "jackal", "kitten", "lemon", "marmot", "napkin", "ocelot", "pirate",
            "quiver", "riddle", "saddle", "tangle", "unison", "vortex", "wombat", "bobcat", "summit", "tablet",
            "gadget", "helmet", "insect", "jersey", "kernel", "lizard", "magnet", "nickel", "osprey", "piston",
            "raptor", "shovel", "tinker", "vessel", "wasp", "carbon", "drifter", "gibbon", "hobbit", "impala",
            "jumble", "kennel", "llama", "moose", "newt", "oyster", "puffin", "rascal", "socket", "toucan",
            "vulcan", "walnut", "zipper", "alpaca", "beetle", "condor", "donkey", "gopher", "hornet", "iguana",
            "jaguar", "koala", "lemur", "mantis", "narwhal", "okapi", "plover", "quokka", "ringer", "starling",
            "tundra", "uplift", "vapor", "wizard", "xenops", "yarrow", "zinnia", "turret", "barrel", "castle",
            "droplet", "engine", "fossil", "glacier", "harbor", "island", "jungle", "lagoon", "meadow", "noodle",
            "outpost", "portal", "quarry", "ripple", "shoal", "cavern", "pinnacle", "valley", "whorl", "zenith",
            "acrobat", "bandit", "capsule", "dancer", "enigma", "fanfare", "gemstone", "herald", "icon", "jester",
            "kestrel", "labyrinth", "mandolin", "nomad", "oracle", "phantom", "quartz", "ranger", "scepter", "tome",
            "utopia", "vanguard", "wanderer", "xylem", "yeoman", "zeal", "arbor", "blizzard", "crater", "druid",
            "eskimo", "fern", "grotto", "haven", "inlet", "jasper", "keystone", "lore", "mortar", "niche",
            "omen", "prism", "quill", "relic", "squall", "thrush", "umbra", "vesper", "wicket", "cobble",
        };

        /// <summary>
        /// Generates a unique name that doesn't collide with existing files in the sync folder.
        /// </summary>
        /// <param name="syncFolder">Path to check for collisions.</param>
        /// <param name="extension">File extension including dot (e.g. ".txt").</param>
        /// <returns>Name without extension, e.g. "fuzzy-lobster".</returns>
        public static string Generate(string syncFolder, string extension)
        {
            for (int attempt = 0; attempt < 100; attempt++)
            {
                string name = RandomName();
                string filePath = Path.Combine(syncFolder, name + extension);
                if (!File.Exists(filePath))
                    return name;
            }
            // Extremely unlikely fallback: append a timestamp
            return RandomName() + "-" + DateTime.UtcNow.Ticks.ToString("x8");
        }

        private static string RandomName()
        {
            string adj = Adjectives[_rng.Next(Adjectives.Length)];
            string noun = Nouns[_rng.Next(Nouns.Length)];
            return $"{adj}-{noun}";
        }
    }
}
