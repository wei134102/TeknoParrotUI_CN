using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using TeknoParrotUi.Properties;
using TeknoParrotUi.Common;

namespace TeknoParrotUi.Helpers
{
    public static class GenreTranslationHelper
    {
        private static readonly Dictionary<string, string> InternalToResourceMap = new Dictionary<string, string>
        {
            { "All", nameof(Resources.LibraryGenreAll) },
            { "Installed", nameof(Resources.AddGameInstalledFilter) },
            { "Not Installed", nameof(Resources.AddGameNotInstalledFilter) },
            { "Subscription", nameof(Resources.LibraryGenreSubscription) },
            { "Konami Viper", nameof(Resources.LibraryGenreKonamiViper) },
            { "System 246/256", nameof(Resources.LibraryGenreSystem246) },
            { "System 357/359/369", nameof(Resources.LibraryGenreSystem357) },
            { "Triforce", nameof(Resources.LibraryGenreTriforce) },
            { "Action", nameof(Resources.LibraryGenreAction) },
            { "Card", nameof(Resources.LibraryGenreCard) },
            { "Compilation", nameof(Resources.LibraryGenreCompilation) },
            { "Fighting", nameof(Resources.LibraryGenreFighting) },
            { "Flying", nameof(Resources.LibraryGenreFlying) },
            { "Platform", nameof(Resources.LibraryGenrePlatform) },
            { "Puzzle", nameof(Resources.LibraryGenrePuzzle) },
            { "Racing", nameof(Resources.LibraryGenreRacing) },
            { "Rhythm", nameof(Resources.LibraryGenreRhythm) },
            { "Shoot 'Em Up", nameof(Resources.LibraryGenreShootEmUp) },
            { "Shooter", nameof(Resources.LibraryGenreShooter) },
            { "Sports", nameof(Resources.LibraryGenreSports) },
            { "Others", nameof(Resources.LibraryGenreOthers) }
        };

        public static List<GenreItem> GetGenreItems(bool includeNotInstalled = false)
        {
            var items = new List<GenreItem>();

            var orderedKeys = new List<string>
            {
                "All", "Installed", "Subscription", "Konami Viper", "System 246/256", "System 357/359/369", "Triforce",
                "Action", "Card", "Compilation", "Fighting", "Flying",
                "Platform", "Puzzle", "Racing", "Rhythm", "Shoot 'Em Up",
                "Shooter", "Sports"
            };

            if (includeNotInstalled)
            {
                orderedKeys.Insert(2, "Not Installed");
            }

            foreach (var key in orderedKeys)
            {
                if (InternalToResourceMap.ContainsKey(key))
                {
                    var localizedText = GetLocalizedString(InternalToResourceMap[key]);
                    items.Add(new GenreItem
                    {
                        InternalName = key,
                        DisplayName = localizedText
                    });
                }
            }

            return items;
        }

        public static List<EmulatorFilterItem> GetEmulatorItems()
        {
            return new List<EmulatorFilterItem>
            {
                new EmulatorFilterItem { InternalName = "All", DisplayName = "All Emulators" },
                new EmulatorFilterItem { InternalName = "OpenParrot", DisplayName = "OpenParrot" },
                new EmulatorFilterItem { InternalName = "Lindbergh", DisplayName = "Lindbergh" },
                new EmulatorFilterItem { InternalName = "N2", DisplayName = "N2" },
                new EmulatorFilterItem { InternalName = "OpenParrotKonami", DisplayName = "OpenParrot Konami" },
                new EmulatorFilterItem { InternalName = "ElfLdr2", DisplayName = "ElfLdr2" },
                new EmulatorFilterItem { InternalName = "Dolphin", DisplayName = "Dolphin (Triforce)" },
                new EmulatorFilterItem { InternalName = "Play", DisplayName = "Play! (PS2)" },
                new EmulatorFilterItem { InternalName = "RPCS3", DisplayName = "RPCS3 (PS3)" },
                new EmulatorFilterItem { InternalName = "TeknoMacaw", DisplayName = "TeknoMacaw" },
                new EmulatorFilterItem { InternalName = "cxbxr", DisplayName = "Cxbx-Reloaded (Xbox)" },
                new EmulatorFilterItem { InternalName = "pcsx2x6", DisplayName = "PCSX2 x6 (PS2)" },
                new EmulatorFilterItem { InternalName = "SegaTools", DisplayName = "SegaTools" }
            };
        }

        public static bool DoesGameMatchEmulator(string internalEmulatorName, GameProfile gameProfile)
        {
            if (internalEmulatorName == "All")
                return true;

            return internalEmulatorName.Equals(gameProfile.EmulatorType.ToString(), StringComparison.OrdinalIgnoreCase);
        }

        private static string GetLocalizedString(string resourceName)
        {
            var property = typeof(Resources).GetProperty(resourceName);
            return property?.GetValue(null)?.ToString() ?? resourceName;
        }

        public static bool DoesGameMatchGenre(string internalGenreName, TeknoParrotUi.Common.GameProfile gameProfile)
        {
            string gameGenre = gameProfile.GameInfo?.game_genre ?? gameProfile.GameGenreInternal ?? "Unknown";
            var emulatorType = gameProfile.EmulatorType;
            Debug.WriteLine($"Game: {gameProfile.GameNameInternal} | GameGenre: {gameGenre} | Filter: {internalGenreName}");

            if (internalGenreName == "All")
                return true;

            if (internalGenreName == "Subscription")
                return gameProfile.Patreon;

            if (internalGenreName == "Installed")
            {
                var existing = TeknoParrotUi.Common.GameProfileLoader.UserProfiles.FirstOrDefault((profile) =>
                    profile.ProfileName == gameProfile.ProfileName) != null;
                return existing;
            }

            if (internalGenreName == "Not Installed")
            {
                var existing = TeknoParrotUi.Common.GameProfileLoader.UserProfiles.FirstOrDefault((profile) =>
                    profile.ProfileName == gameProfile.ProfileName) != null;
                return !existing;
            }

            if (internalGenreName == "Triforce")
            {
                bool isTriforce = emulatorType == Common.EmulatorType.Dolphin;
                return isTriforce;
            }

            if (internalGenreName == "System 246/256")
            {
                bool is246 = emulatorType == Common.EmulatorType.Play;
                return is246;
            }

            if (internalGenreName == "System 357/359/369")
            {
                bool is357 = emulatorType == Common.EmulatorType.RPCS3;
                return is357;
            }

            if (internalGenreName == "Konami Viper")
            {
                bool isViper = emulatorType == Common.EmulatorType.TeknoViper;
                return isViper;
            }

            bool matches = internalGenreName.Equals(gameGenre, StringComparison.OrdinalIgnoreCase);
            Debug.WriteLine($"  -> Matches: {matches}");

            if (internalGenreName == "Others")
            {
                var specificGenres = new[] { "Action", "Card", "Compilation", "Fighting", "Flying",
                    "Platform", "Puzzle", "Racing", "Rhythm", "Shoot 'Em Up", "Shooter", "Sports" };
                bool matchesSpecificGenre = specificGenres.Any(g =>
                    g.Equals(gameGenre, StringComparison.OrdinalIgnoreCase));
                return !matchesSpecificGenre;
            }

            return matches;
        }
    }

    public class GenreItem
    {
        public string InternalName { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }

    public class EmulatorFilterItem
    {
        public string InternalName { get; set; }
        public string DisplayName { get; set; }

        public override string ToString() => DisplayName;
    }
}