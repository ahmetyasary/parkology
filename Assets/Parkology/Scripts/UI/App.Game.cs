using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    public sealed partial class App
    {
        sealed class BoosterButton
        {
            public Booster kind;
            public RectTransform root;
            public Image icon, badge, lockIcon;
            public Text count, lockText;
            public GameObject glow;
        }

        readonly BoosterButton[] boosters = new BoosterButton[3];
        Text capacityText;
        Image capacityPill;
        RectTransform hint, tutorialHand;
        Text hintText;
        LevelSpec spec;

        public void StartLevel(int number)
        {
            ClearScreen();
            current = ScreenKind.Game;
            Sfx.ApplyMusic();
            spec = prepared != null && prepared.Number == number ? prepared : LevelGenerator.Create(number);
            prepared = null;
            board = GameBoard.Create(world, Cam, spec, false);
            board.ConsumeBooster = UseBooster;
            board.CapacityChanged += OnCapacity;
            board.Blocked += () => { if (capacityPill) { Tween.Punch(capacityPill.transform, 0.3f, 0.4f); capacityPill.color = new Color(1f, 0.6f, 0.6f); } };
            board.Dispatched += OnDispatched;
            board.Won += () => Tween.Delay(1.5f, ShowWin, board);
            board.Failed += () => Tween.Delay(0.7f, ShowFail, board);
            BuildHud();
            Tween.Delay(1.2f, LevelIntro, board);
        }

        void BuildHud()
        {
            CoinPill(safe, new Vector2(0f, 1f), new Vector2(190, -90));
            var gear = UIKit.Button(safe, "circle-btn", new Vector2(1f, 1f), new Vector2(-110, -96), new Vector2(150, 157), () => ShowSettings(true), false);
            UIKit.Image(gear.transform, "icon-gear", new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(92, 92));
            DropDown(gear.transform, 0.3f);

            var title = UIKit.Text(safe, "level_n", 76, Color.white, new Vector2(0.5f, 1f), new Vector2(0, -92), new Vector2(460, 110), TextAnchor.MiddleCenter, UIKit.Ink, 7f, true, spec.Number);
            DropDown(title.transform, 0.2f);
            // Loop capacity: blocks on the loop / allowed.
            capacityPill = UIKit.Image(safe, "pill", new Vector2(0.5f, 1f), new Vector2(0, -200), new Vector2(260, 96), true);
            UIKit.Image(capacityPill.transform, "train-engine", new Vector2(0f, 0.5f), new Vector2(52, 6), new Vector2(92, 72));
            capacityText = UIKit.Text(capacityPill.transform, "0/5", 44, UIKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(34, 4), new Vector2(160, 64), TextAnchor.MiddleCenter, null, 0f, false);
            DropDown(capacityPill.transform, 0.35f);

            // Booster bar
            var bar = UIKit.Node(safe, "Boosters", new Vector2(0.5f, 0f), new Vector2(0, 130), new Vector2(900, 220));
            string[] icons = { "icon-slot", "icon-hammer", "hand" };
            for (int i = 0; i < 3; i++)
            {
                var kind = (Booster)i;
                var b = new BoosterButton { kind = kind };
                var btn = UIKit.Button(bar, "circle-btn", new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 270f, 0), new Vector2(190, 199), () => OnBooster(kind), false);
                b.root = (RectTransform)btn.transform;
                var glow = UIKit.Image(b.root, "glow", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(300, 300));
                glow.transform.SetAsFirstSibling();
                glow.color = new Color(1f, 0.95f, 0.5f, 0.9f);
                b.glow = glow.gameObject;
                b.glow.SetActive(false);
                b.icon = UIKit.Image(b.root, icons[i], new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(118, 118));
                b.badge = UIKit.Image(b.root, "badge", new Vector2(1f, 1f), new Vector2(-18, -22), new Vector2(76, 76));
                b.count = UIKit.Text(b.badge.transform, "3", 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 3), new Vector2(70, 60), TextAnchor.MiddleCenter, UIKit.Ink, 3f, false);
                b.lockIcon = UIKit.Image(b.root, "icon-lock", new Vector2(0.5f, 0.5f), new Vector2(0, 22), new Vector2(84, 84));
                b.lockText = UIKit.Text(b.root, "lv_n", 30, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, -42), new Vector2(170, 48), TextAnchor.MiddleCenter, UIKit.Ink, 4f, true, Save.UnlockLevel(kind));
                boosters[i] = b;
                Tween.Move(b.root, new Vector2((i - 1) * 270f, -360f), new Vector2((i - 1) * 270f, 0), 0.55f, Ease.OutBack, 0.35f + i * 0.08f);
            }
            RefreshBoosters();

            // Targeting hint (hammer / magnet)
            hint = UIKit.Image(safe, "panel", new Vector2(0.5f, 0f), new Vector2(0, 330), new Vector2(820, 120), true, true).rectTransform;
            hintText = UIKit.Text(hint, "", 40, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(-40, 2), new Vector2(660, 90), TextAnchor.MiddleCenter, null, 0f, false);
            var cancel = UIKit.Button(hint, "btn-red", new Vector2(1f, 0.5f), new Vector2(-62, 4), new Vector2(92, 92), CancelTargeting);
            UIKit.Image(cancel.transform, "icon-close", new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(54, 54));
            hint.gameObject.SetActive(false);
        }

        void RefreshBoosters()
        {
            foreach (var b in boosters)
            {
                if (b == null || !b.root) continue;
                bool unlocked = Save.IsUnlocked(b.kind) && (Save.Introduced(b.kind) || current != ScreenKind.Game);
                int n = Save.Count(b.kind);
                b.icon.gameObject.SetActive(unlocked);
                b.badge.gameObject.SetActive(unlocked);
                b.lockIcon.gameObject.SetActive(!unlocked);
                b.lockText.gameObject.SetActive(!unlocked);
                b.root.GetComponent<Image>().color = unlocked ? Color.white : new Color(0.62f, 0.66f, 0.78f);
                b.badge.sprite = UIKit.Sprite(n > 0 ? "badge" : "badge-green");
                b.count.text = n > 0 ? n.ToString() : "+";
                b.glow.SetActive(board != null && board.Targeting == b.kind);
            }
        }

        void OnCapacity(int active, int capacity)
        {
            if (!capacityText) return;
            capacityText.text = active + "/" + capacity;
            bool full = active >= capacity;
            capacityPill.color = full ? new Color(1f, 0.72f, 0.72f) : Color.white;
            capacityText.color = full ? Palette.Danger : UIKit.Brown;
            Tween.Punch(capacityPill.transform, 0.12f, 0.3f);
        }

        void OnBooster(Booster kind)
        {
            if (board == null || board.Finished) return;
            if (!Save.IsUnlocked(kind) || !Save.Introduced(kind))
            {
                Toast(Loc.Format("unlocks_at", Save.UnlockLevel(kind)));
                Sfx.Play("blocked", 0.5f);
                return;
            }
            if (board.Targeting == kind)
            {
                CancelTargeting();
                return;
            }
            if (Save.Count(kind) <= 0)
            {
                ShowBuy(kind);
                return;
            }
            switch (kind)
            {
                case Booster.Slot:
                    if (!board.CanAddSlot) { Toast(Loc.Get("slot_max")); return; }
                    UseBooster(kind);
                    board.AddSlot();
                    break;
                case Booster.Hammer:
                    board.Targeting = kind;
                    ShowHint("hint_hammer");
                    Sfx.Play("whoosh", 0.4f, 1.4f);
                    break;
                case Booster.Hand:
                    board.Targeting = kind;
                    ShowHint("hint_hand");
                    Sfx.Play("magnet", 0.4f, 1.3f);
                    break;
            }
            RefreshBoosters();
        }

        bool UseBooster(Booster kind)
        {
            int n = Save.Count(kind);
            if (n <= 0) return false;
            Save.SetCount(kind, n - 1);
            HideHint();
            RefreshBoosters();
            var b = boosters[(int)kind];
            if (b != null && b.root) Tween.Punch(b.root, 0.25f, 0.4f);
            return true;
        }

        void ShowHint(string key)
        {
            hint.gameObject.SetActive(true);
            LocText.Bind(hintText, key);
            Tween.Scale(hint, new Vector3(0.6f, 0.6f, 1f), Vector3.one, 0.35f, Ease.OutBack);
        }

        void HideHint()
        {
            if (hint) hint.gameObject.SetActive(false);
        }

        void CancelTargeting()
        {
            if (board != null) board.Targeting = null;
            HideHint();
            RefreshBoosters();
        }

        /// <summary>Short message that floats up and fades.</summary>
        void Toast(string message)
        {
            var box = UIKit.Image(popupLayer, "btn-grey", new Vector2(0.5f, 0.5f), new Vector2(0, -200), new Vector2(760, 120), true).rectTransform;
            box.GetComponent<Image>().color = new Color(0.25f, 0.3f, 0.55f, 0.95f);
            UIKit.Text(box, message, 40, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 4), new Vector2(700, 90), TextAnchor.MiddleCenter, UIKit.Ink, 3f, false);
            var group = box.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            Tween.Scale(box, new Vector3(0.6f, 0.6f, 1f), Vector3.one, 0.3f, Ease.OutBack);
            Tween.Run(1.6f, k =>
            {
                if (!box) return;
                box.anchoredPosition = new Vector2(0, -200 + k * 120f);
                group.alpha = k < 0.7f ? 1f : 1f - (k - 0.7f) / 0.3f;
            }, Ease.Linear, () => { if (box) Destroy(box.gameObject); }, 0.1f, box);
        }

        /// <summary>Unlock popups, first-level tutorial and tips.</summary>
        void LevelIntro()
        {
            if (board == null || current != ScreenKind.Game) return;
            foreach (Booster kind in new[] { Booster.Slot, Booster.Hammer, Booster.Hand })
                if (Save.IsUnlocked(kind) && !Save.Introduced(kind))
                {
                    ShowBoosterIntro(kind, LevelIntro);
                    return;
                }
            if (spec.Number == 1 && !Save.TutorialDone) ShowTutorialHand();
            else if (spec.Number == 2) Toast(Loc.Get("tip_full"));
            else if (spec.Number == 4) Toast(Loc.Get("tip_mystery"));
        }

        void ShowTutorialHand()
        {
            if (board == null) return;
            Vector3 screen = board.LaneScreenPoint(0);
            RectTransformUtility.ScreenPointToLocalPointInRectangle(popupLayer, screen, null, out Vector2 local);
            tutorialHand = UIKit.Node(popupLayer, "Tutorial", new Vector2(0.5f, 0.5f), local, new Vector2(10, 10));
            var hand = UIKit.Image(tutorialHand, "hand", new Vector2(0.5f, 0.5f), new Vector2(40, -110), new Vector2(170, 187));
            // The bubble sits low on the screen so it never covers the parking lot.
            var bubble = UIKit.Image(popupLayer, "panel", new Vector2(0.5f, 0.5f), new Vector2(0, -560), new Vector2(760, 150), true);
            bubble.transform.SetParent(tutorialHand, true);
            UIKit.Text(bubble.transform, "tap_lane", 38, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(510, 120));
            var rt = hand.rectTransform;
            Tween.Run(100f, k =>
            {
                if (!rt) return;
                float t = Time.unscaledTime * 2.2f;
                float press = Mathf.Max(0f, Mathf.Sin(t * Mathf.PI));
                rt.anchoredPosition = new Vector2(40, -110 + press * 34f);
                rt.localScale = Vector3.one * (1f - (1f - press) * 0.08f);
            }, Ease.Linear, null, 0f, rt);
        }

        void OnDispatched()
        {
            if (tutorialHand)
            {
                Destroy(tutorialHand.gameObject);
                tutorialHand = null;
                Toast(Loc.Get("tip_match"));
            }
        }
    }
}
