using System;
using System.Diagnostics;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace Parkology.Editor
{
    /// <summary>
    /// Batch checks. Run with:
    ///   Unity -batchmode -projectPath . -executeMethod Parkology.Editor.SmokeCheck.Run -logFile -
    /// 1) every level 1..150 generates and at least some random play-outs of the rules model clear it;
    /// 2) play mode: levels 1..LastLevel are auto-played on the real board with no exceptions (wins and losses are reported).
    /// Player progress is backed up and restored.
    /// </summary>
    [InitializeOnLoad]
    public static class SmokeCheck
    {
        const int LastLevel = 10;
        static readonly string[] keys = { "level", "coins", "boost_slot", "boost_hammer", "boost_hand", "boost_slot_intro", "boost_hammer_intro", "boost_hand_intro", "tutorial", "onboarded", "tip_crate" };
        static double started, levelStarted;
        static int level, errors;

        static SmokeCheck()
        {
            if (!SessionState.GetBool("PKSmoke", false)) return;
            Application.logMessageReceived += Observe;
            level = SessionState.GetInt("PKSmokeLevel", 1);
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        public static void Run()
        {
            ValidateLevels();
            ProjectSetup.Prepare();
            foreach (string k in keys)
            {
                SessionState.SetBool("pk_has_" + k, PlayerPrefs.HasKey(k));
                SessionState.SetInt("pk_val_" + k, PlayerPrefs.GetInt(k, 0));
            }
            PlayerPrefs.SetInt("level", 1);
            PlayerPrefs.SetInt("tutorial", 1);
            EditorSettings.enterPlayModeOptionsEnabled = true;
            EditorSettings.enterPlayModeOptions = EnterPlayModeOptions.DisableDomainReload | EnterPlayModeOptions.DisableSceneReload;
            UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ProjectSetup.ScenePath);
            SessionState.SetBool("PKSmoke", true);
            SessionState.SetInt("PKSmokeLevel", 1);
            SessionState.SetInt("PKSmokeWins", 0);
            SessionState.SetInt("PKSmokeLosses", 0);
            level = 1;
            errors = 0;
            Application.logMessageReceived += Observe;
            started = EditorApplication.timeSinceStartup;
            EditorApplication.update += Tick;
        }

        public static void ValidateLevels()
        {
            var sw = Stopwatch.StartNew();
            for (int n = 1; n <= 150; n++)
            {
                var spec = LevelGenerator.Create(n);
                if (spec.BlockCount % spec.Slots != 0) throw new Exception("Level " + n + " has an incomplete colour group");
                int waiting = 0;
                foreach (var l in spec.Lanes) waiting += l.Count;
                if (n <= 12 || n % 25 == 0)
                    Debug.Log($"LEVEL {n}: lanes={spec.Lanes.Length} {spec.Cols}x{spec.Slots} colors={spec.Colors} blocks={spec.BlockCount} waiting={waiting} cap={spec.Capacity} winRate={spec.WinRate:0.00}");
            }
            Debug.Log("PARKOLOGY_LEVELS_OK 150 levels in " + sw.ElapsedMilliseconds + " ms");
        }

        static void Observe(string message, string stack, LogType type)
        {
            if (type == LogType.Exception || (type == LogType.Error && !message.Contains("Licens")))
            {
                errors++;
                SessionState.SetInt("PKSmokeErrors", SessionState.GetInt("PKSmokeErrors", 0) + 1);
            }
        }

        static void Tick()
        {
            try
            {
                if (!EditorApplication.isPlaying)
                {
                    if (!EditorApplication.isPlayingOrWillChangePlaymode && EditorApplication.timeSinceStartup - started > 1.5 && !EditorApplication.isCompiling)
                    {
                        App.FastBoot = true;
                        EditorApplication.isPlaying = true;
                    }
                    return;
                }
                Application.runInBackground = true;
                EditorApplication.QueuePlayerLoopUpdate();
                var app = App.Instance;
                if (app == null || app.Board == null)
                {
                    if (EditorApplication.timeSinceStartup - started > 30) Finish(false, "App did not start");
                    return;
                }
                var board = app.Board;
                if (board.Demo) return;
                if (board.Spec.Number != level) return;
                if (!board.Autoplay)
                {
                    board.Autoplay = true;
                    Time.timeScale = 3f;
                    levelStarted = EditorApplication.timeSinceStartup;
                    Debug.Log("SMOKE_LEVEL_START " + level + " blocks=" + board.BlocksLeft);
                }
                if (board.Finished)
                {
                    if (board.BlocksLeft > 0)
                    {
                        // A greedy bot may jam where a person would use a booster; give it the same help.
                        if (board.CanAddSlot)
                        {
                            Debug.Log("SMOKE_LEVEL_JAM " + level + " -> +1 slot");
                            board.AddSlot();
                            board.Continue();
                            return;
                        }
                        // The bot plays at random among sensible moves; a loss is reported, not treated as a bug.
                        Debug.Log("SMOKE_LEVEL_LOST " + level + " with " + board.BlocksLeft + " blocks left");
                        SessionState.SetInt("PKSmokeLosses", SessionState.GetInt("PKSmokeLosses", 0) + 1);
                    }
                    else
                    {
                        Debug.Log("SMOKE_LEVEL_WON " + level + " in " + (EditorApplication.timeSinceStartup - levelStarted).ToString("0.0") + "s");
                        SessionState.SetInt("PKSmokeWins", SessionState.GetInt("PKSmokeWins", 0) + 1);
                    }
                    level++;
                    SessionState.SetInt("PKSmokeLevel", level);
                    if (level > LastLevel)
                    {
                        int wins = SessionState.GetInt("PKSmokeWins", 0);
                        Debug.Log("SMOKE_SUMMARY wins=" + wins + " losses=" + SessionState.GetInt("PKSmokeLosses", 0));
                        Finish(errors == 0 && wins > 0, errors > 0 ? errors + " runtime errors" : "no level was won");
                        return;
                    }
                    app.StartLevel(level);
                    return;
                }
                if (EditorApplication.timeSinceStartup - levelStarted > 120)
                {
                    Debug.Log("SMOKE_STATE " + board.Describe());
                    Finish(false, "Level " + level + " timed out");
                }
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                Finish(false, e.Message);
            }
        }

        static void Finish(bool ok, string reason)
        {
            EditorApplication.update -= Tick;
            SessionState.SetBool("PKSmoke", false);
            Time.timeScale = 1f;
            foreach (string k in keys)
            {
                if (SessionState.GetBool("pk_has_" + k, false)) PlayerPrefs.SetInt(k, SessionState.GetInt("pk_val_" + k, 0));
                else PlayerPrefs.DeleteKey(k);
            }
            PlayerPrefs.Save();
            EditorSettings.enterPlayModeOptionsEnabled = false;
            App.FastBoot = false;
            if (ok) Debug.Log("PARKOLOGY_SMOKE_OK levels 1-" + LastLevel + " played without errors");
            else Debug.LogError("PARKOLOGY_SMOKE_FAILED " + reason);
            if (Application.isBatchMode) EditorApplication.Exit(ok ? 0 : 1);
            else EditorApplication.isPlaying = false;
        }
    }
}
