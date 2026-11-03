using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    /// <summary>
    /// Coaching for the warm-up levels: a mascot speech bubble that slides up from the bottom, a tapping hand and
    /// glowing markers on the board. Tips follow what happens on the board, so each rule is explained as it happens.
    /// </summary>
    public sealed partial class App
    {
        const float CoachY = 175f, BarY = 130f;
        /// <summary>A tip stays at least this long before the next one replaces it, so chains of moves stay readable.</summary>
        const float MinTipTime = 2.6f;

        RectTransform coach, coachMascot, pointer, marker, boosterBar;
        Image uiMarker;
        Text coachText;
        bool coaching, coachShown;
        int coachStep;
        float idleTime, coachUntil, coachSince;
        readonly HashSet<string> shownTips = new HashSet<string>();
        readonly Queue<(string key, float seconds, System.Action show)> coachQueue = new Queue<(string, float, System.Action)>();

        /// <summary>Hooks the board events for this level; the first tip waits for <see cref="StartCoaching"/>.</summary>
        void WireCoaching()
        {
            coach = null;
            coachShown = false;
            coachStep = 0;
            idleTime = 0f;
            shownTips.Clear();
            coachQueue.Clear();
            coaching = !Save.Onboarded && spec.Number <= LevelGenerator.OnboardingLevels;
            if (!coaching)
            {
                // After the warm-up the bubble only returns to introduce a new obstacle, once.
                if (spec.Crates == null || PlayerPrefs.GetInt("tip_crate", 0) == 1) return;
                coaching = true;
                board.Won += EndCoaching;
                board.Failed += EndCoaching;
                return;
            }
            board.Dispatched += OnCoachDispatched;
            board.Joined += OnCoachJoined;
            board.Cleared += OnCoachCleared;
            board.Revealed += () => { if (spec.Number >= 4) Tip("tip_mystery", 4f); };
            board.Blocked += () => { if (spec.Number >= 2) Tip("tip_capacity", 4f, false); };
            board.Won += EndCoaching;
            board.Failed += EndCoaching;
        }

        void StartCoaching()
        {
            if (!coaching || board == null) return;
            if (spec.Number > LevelGenerator.OnboardingLevels)
            {
                if (!board.FindCrate(out int crate)) return;
                PlayerPrefs.SetInt("tip_crate", 1);
                PlayerPrefs.Save();
                Tip("tip_crate", 6f, true, () => MarkScreen(board.SlotScreenPoint(crate, spec.Slots / 2), true));
                return;
            }
            switch (spec.Number)
            {
                case 1:
                    coachStep = 1;
                    Coach("tap_lane", 0f);
                    PointAtLane(board.SuggestLane());
                    break;
                case 2:
                    Tip("tip_capacity", 5.5f, true, () => MarkUI(capacityPill.rectTransform));
                    break;
                case 3:
                    Tip("tip_order", 5f);
                    break;
                case 4:
                    if (board.FindHidden(out int c, out int k))
                        Tip("tip_mystery", 5.5f, true, () => MarkScreen(board.SlotScreenPoint(c, k), false));
                    break;
                case 5:
                    Tip("tip_last", 4.5f);
                    break;
            }
        }

        void OnCoachDispatched()
        {
            idleTime = 0f;
            ClearPointer();
            if (spec.Number != 1 || coachStep != 1) return;
            coachStep = 2;
            int c = board.LoopHomeColumn();
            Coach("tip_match", 0f, () => { if (c >= 0) MarkScreen(board.SlotScreenPoint(c, spec.Slots - 1), true); });
        }

        void OnCoachJoined(int c, bool pushed)
        {
            if (spec.Number != 1 || coachStep != 2) return;
            coachStep = 3;
            Coach(pushed ? "tip_push" : "tip_keep", pushed ? 4.5f : 3.5f);
        }

        void OnCoachCleared(int c)
        {
            if (spec.Number > 2) return;
            coachStep = 4;
            Tip("tip_clear", 4.5f);
        }

        void EndCoaching()
        {
            coaching = false;
            coachQueue.Clear();
            ClearPointer();
            ClearMarker();
            HideCoach();
        }

        /// <summary>Shows a tip once per level.</summary>
        void Tip(string key, float seconds, bool once = true, System.Action show = null)
        {
            if (!coaching) return;
            if (once && !shownTips.Add(key)) return;
            Coach(key, seconds, show);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            if (!coaching || board == null || board.Finished) return;
            if (coachQueue.Count > 0 && Time.unscaledTime - coachSince >= MinTipTime)
            {
                var (key, seconds, show) = coachQueue.Dequeue();
                ShowTip(key, seconds, show);
            }
            else if (coachShown && coachQueue.Count == 0 && Time.unscaledTime > coachUntil)
            {
                HideCoach();
                ClearMarker();
            }
            // On the small lots a player who waits gets a nudge towards a lane with a block that can park.
            if (spec.Number > 2 || coachStep == 1 || pointer || board.ActiveCount > 0) { idleTime = 0f; return; }
            idleTime += Time.unscaledDeltaTime;
            if (idleTime < 5f) return;
            idleTime = 0f;
            int lane = board.SuggestLane();
            if (lane < 0) return;
            PointAtLane(lane);
            Coach("tip_keep", 3.5f);
        }

        // ------------------------------------------------------------------ speech bubble

        void BuildCoach()
        {
            coach = UIKit.Node(safe, "Coach", new Vector2(0.5f, 0f), new Vector2(0, -400), new Vector2(980, 236));
            var panel = UIKit.Image(coach, "panel", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(980, 236), true, true);
            panel.gameObject.AddComponent<Button>().onClick.AddListener(() =>
            {
                // Tapping the bubble dismisses timed tips; the step-by-step ones stay until the player acts.
                if (coachUntil < float.MaxValue) coachUntil = 0f;
            });
            var halo = UIKit.Image(coach, "glow", new Vector2(0f, 0.5f), new Vector2(150, 30), new Vector2(320, 320));
            halo.color = new Color(1f, 0.95f, 0.6f, 0.7f);
            coachMascot = UIKit.Node(coach, "Mascot", new Vector2(0f, 0.5f), new Vector2(150, 34), new Vector2(230, 181));
            UIKit.Image(coachMascot, "train-engine", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(230, 181));
            coachMascot.gameObject.AddComponent<Bob>().amount = 0.04f;
            coachText = UIKit.Text(coach, "", 46, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(115, 4), new Vector2(680, 196), TextAnchor.MiddleCenter, null, 0f, false);
            coach.gameObject.SetActive(false);
        }

        /// <summary>
        /// Shows a tip (seconds = 0 keeps it until the next one); show sets up its markers. A tip that arrives while
        /// the current one is still fresh waits its turn.
        /// </summary>
        void Coach(string key, float seconds, System.Action show = null)
        {
            if (coachShown && (coachQueue.Count > 0 || Time.unscaledTime - coachSince < MinTipTime))
            {
                coachQueue.Enqueue((key, seconds, show));
                return;
            }
            ShowTip(key, seconds, show);
        }

        /// <summary>Slides the bubble in (or swaps its text with a hop).</summary>
        void ShowTip(string key, float seconds, System.Action show)
        {
            if (!coach) BuildCoach();
            ClearMarker();
            show?.Invoke();
            coachSince = Time.unscaledTime;
            LocText.Bind(coachText, key);
            coachUntil = seconds > 0f ? Time.unscaledTime + seconds : float.MaxValue;
            Sfx.Play("whoosh", 0.35f, 1.7f);
            Haptics.Play(Haptic.Soft);
            if (!coachShown)
            {
                coachShown = true;
                coach.gameObject.SetActive(true);
                Tween.Move(coach, new Vector2(0, -300), new Vector2(0, CoachY), 0.5f, Ease.OutBack);
                if (boosterBar) Tween.Move(boosterBar, boosterBar.anchoredPosition, new Vector2(0, -360), 0.35f, Ease.InBack);
            }
            else Tween.Punch(coach, 0.08f, 0.35f);
            var m = coachMascot;
            Tween.Run(0.45f, e =>
            {
                if (!m) return;
                m.anchoredPosition = new Vector2(150, 34 + Mathf.Sin(e * Mathf.PI) * 46f);
                m.localRotation = Quaternion.Euler(0, 0, Mathf.Sin(e * Mathf.PI * 2f) * 8f);
            }, Ease.OutQuad, null, 0.1f, m, ("mascot", m));
        }

        void HideCoach()
        {
            if (!coachShown || !coach) return;
            coachShown = false;
            var c = coach;
            Tween.Move(c, c.anchoredPosition, new Vector2(0, -300), 0.3f, Ease.InBack, 0f, () => { if (c && !coachShown) c.gameObject.SetActive(false); });
            if (boosterBar) Tween.Move(boosterBar, boosterBar.anchoredPosition, new Vector2(0, BarY), 0.45f, Ease.OutBack, 0.15f);
        }

        // ------------------------------------------------------------------ pointers and markers

        Vector2 ToPopupLocal(Vector3 screen)
        {
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screen, null, out Vector2 local);
            return local;
        }

        /// <summary>A big hand taps the front of a lane, with a ripple on every press.</summary>
        void PointAtLane(int lane)
        {
            ClearPointer();
            if (lane < 0 || board == null) return;
            pointer = UIKit.Node(popupLayer, "Pointer", new Vector2(0.5f, 0.5f), ToPopupLocal(board.LaneScreenPoint(lane)), new Vector2(10, 10));
            pointer.SetAsFirstSibling();
            var glow = UIKit.Image(pointer, "glow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(260, 260));
            glow.color = new Color(1f, 1f, 0.75f, 0.75f);
            var ripple = UIKit.Image(pointer, "glow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200, 200));
            var hand = UIKit.Image(pointer, "hand", new Vector2(0.5f, 0.5f), new Vector2(56, -128), new Vector2(210, 231)).rectTransform;
            var g = glow.rectTransform;
            var r = ripple.rectTransform;
            Tween.Scale(pointer, Vector3.zero, Vector3.one, 0.4f, Ease.OutBack);
            Tween.Run(1000f, k =>
            {
                if (!hand) return;
                float t = Time.unscaledTime * 1.6f;
                float phase = t - Mathf.Floor(t);
                float press = Mathf.Max(0f, Mathf.Sin(phase * Mathf.PI * 2f));
                hand.anchoredPosition = new Vector2(56 - press * 10f, -128 + press * 40f);
                hand.localScale = Vector3.one * (1f - press * 0.1f);
                g.localScale = Vector3.one * (0.9f + Mathf.Sin(t * Mathf.PI) * 0.1f);
                // a ripple leaves the fingertip after each press
                float rp = Mathf.Repeat(phase - 0.25f, 1f);
                r.localScale = Vector3.one * (0.3f + rp * 1.2f);
                ripple.color = new Color(1f, 1f, 1f, 0.9f * (1f - rp));
            }, Ease.Linear, null, 0f, hand);
        }

        /// <summary>A glowing ring on the board; arrow = a hand points down at it from above.</summary>
        void MarkScreen(Vector3 screen, bool arrow)
        {
            ClearMarker();
            marker = UIKit.Node(popupLayer, "Marker", new Vector2(0.5f, 0.5f), ToPopupLocal(screen), new Vector2(10, 10));
            marker.SetAsFirstSibling();
            var ring = UIKit.Image(marker, "glow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(240, 240));
            ring.color = new Color(1f, 0.95f, 0.45f, 0.9f);
            RectTransform hand = null;
            if (arrow)
            {
                hand = UIKit.Image(marker, "hand", new Vector2(0.5f, 0.5f), new Vector2(-30, 150), new Vector2(150, 165)).rectTransform;
                hand.localRotation = Quaternion.Euler(0, 0, 180f);
            }
            var rr = ring.rectTransform;
            Tween.Scale(marker, Vector3.zero, Vector3.one, 0.4f, Ease.OutBack);
            Tween.Run(1000f, k =>
            {
                if (!rr) return;
                float t = Time.unscaledTime * 2.4f;
                rr.localScale = Vector3.one * (0.85f + Mathf.Sin(t) * 0.15f);
                if (hand) hand.anchoredPosition = new Vector2(-30, 150 + Mathf.Abs(Mathf.Sin(t)) * 34f);
            }, Ease.Linear, null, 0f, rr);
        }

        /// <summary>A pulsing glow behind a HUD element.</summary>
        void MarkUI(RectTransform target)
        {
            ClearMarker();
            if (!target) return;
            uiMarker = UIKit.Image(target.parent, "glow", target.anchorMin, target.anchoredPosition, new Vector2(target.sizeDelta.x * 1.9f, target.sizeDelta.y * 3.4f));
            uiMarker.color = new Color(1f, 0.85f, 0.25f, 1f);
            uiMarker.transform.SetSiblingIndex(target.GetSiblingIndex());
            var rt = uiMarker.rectTransform;
            Tween.Run(1000f, k =>
            {
                if (!rt) return;
                rt.localScale = Vector3.one * (0.9f + Mathf.Sin(Time.unscaledTime * 6f) * 0.12f);
            }, Ease.Linear, null, 0f, rt);
            Tween.Punch(target, 0.3f, 0.5f);
        }

        void ClearPointer()
        {
            if (pointer) Destroy(pointer.gameObject);
            pointer = null;
        }

        void ClearMarker()
        {
            if (marker) Destroy(marker.gameObject);
            if (uiMarker) Destroy(uiMarker.gameObject);
            marker = null;
            uiMarker = null;
        }
    }
}
