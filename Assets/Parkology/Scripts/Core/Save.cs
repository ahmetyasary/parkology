using UnityEngine;

namespace Parkology
{
    public enum Booster { Slot = 0, Hammer = 1, Hand = 2 }

    /// <summary>Player progress and preferences, stored on device in PlayerPrefs.</summary>
    public static class Save
    {
        public const int StartCoins = 150;
        public const int StartBoosters = 3;
        public const int BoosterPrice = 150;
        static readonly int[] unlockLevels = { 3, 5, 7 };
        static readonly string[] boosterKeys = { "boost_slot", "boost_hammer", "boost_hand" };

        public static int Level
        {
            get => Mathf.Max(1, PlayerPrefs.GetInt("level", 1));
            set { PlayerPrefs.SetInt("level", Mathf.Max(1, value)); PlayerPrefs.Save(); }
        }

        public static int Coins
        {
            get => PlayerPrefs.GetInt("coins", StartCoins);
            set { PlayerPrefs.SetInt("coins", Mathf.Max(0, value)); PlayerPrefs.Save(); }
        }

        public static bool Sound
        {
            get => PlayerPrefs.GetInt("sound", 1) == 1;
            set { PlayerPrefs.SetInt("sound", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Music
        {
            get => PlayerPrefs.GetInt("music", 1) == 1;
            set { PlayerPrefs.SetInt("music", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("haptics", 1) == 1;
            set { PlayerPrefs.SetInt("haptics", value ? 1 : 0); PlayerPrefs.Save(); }
        }

        public static int UnlockLevel(Booster b) => unlockLevels[(int)b];
        public static bool IsUnlocked(Booster b) => Level >= UnlockLevel(b);

        public static int Count(Booster b) => PlayerPrefs.GetInt(boosterKeys[(int)b], 0);

        public static void SetCount(Booster b, int value)
        {
            PlayerPrefs.SetInt(boosterKeys[(int)b], Mathf.Max(0, value));
            PlayerPrefs.Save();
        }

        /// <summary>True once the unlock popup for this booster was shown (and the starter pack granted).</summary>
        public static bool Introduced(Booster b) => PlayerPrefs.GetInt(boosterKeys[(int)b] + "_intro", 0) == 1;

        public static void MarkIntroduced(Booster b)
        {
            PlayerPrefs.SetInt(boosterKeys[(int)b] + "_intro", 1);
            SetCount(b, Count(b) + StartBoosters);
        }

        public static bool TutorialDone
        {
            get => PlayerPrefs.GetInt("tutorial", 0) == 1;
            set { PlayerPrefs.SetInt("tutorial", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }
}
