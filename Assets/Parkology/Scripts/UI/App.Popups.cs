using System;
using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    public sealed partial class App
    {
        const string PrivacyUrl = "https://www.smartlogy.com.tr/Parkology/Privacy/";
        const string TermsUrl = "https://www.smartlogy.com.tr/Parkology/Terms/";
        const string SupportUrl = "https://www.smartlogy.com.tr/Parkology/Support/";

        sealed class Popup
        {
            public RectTransform root, panel, body;
            public Action close;
        }

        /// <summary>Dimmed modal with a cream card and a blue title ribbon.</summary>
        Popup OpenPopup(string titleKey, Vector2 size, bool closeButton, Action onClosed = null, string header = "panel-header")
        {
            var p = new Popup();
            p.root = UIKit.Stretch(popupLayer, "Popup " + titleKey);
            var dim = UIKit.Fill(p.root, "Dim", new Color(0.06f, 0.07f, 0.2f, 0.62f), true);
            var dimGroup = dim.gameObject.AddComponent<CanvasGroup>();
            Tween.Fade(dimGroup, 0f, 1f, 0.2f);
            var holder = UIKit.Node(p.root, "Holder", new Vector2(0.5f, 0.5f), Vector2.zero, size);
            p.panel = holder;
            UIKit.Image(holder, "panel", new Vector2(0.5f, 0.5f), Vector2.zero, size, true, true);
            var ribbon = UIKit.Image(holder, header, new Vector2(0.5f, 1f), new Vector2(0, -10), new Vector2(size.x * 0.86f, 150), true);
            UIKit.Text(ribbon.transform, titleKey, 64, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(size.x * 0.74f, 100), TextAnchor.MiddleCenter, UIKit.Ink, 6f);
            p.body = UIKit.Node(holder, "Body", new Vector2(0.5f, 0.5f), new Vector2(0, -60), new Vector2(size.x - 80, size.y - 220));
            Tween.Scale(holder, new Vector3(0.5f, 0.5f, 1f), Vector3.one, 0.42f, Ease.OutBack);
            Sfx.Play("whoosh", 0.35f, 1.5f);

            bool closing = false;
            p.close = () =>
            {
                if (closing || !p.root) return;
                closing = true;
                Tween.Fade(dimGroup, 1f, 0f, 0.2f);
                var r = p.root;
                Tween.Scale(holder, Vector3.one, new Vector3(0.6f, 0.6f, 1f), 0.2f, Ease.InBack, 0f, () =>
                {
                    if (r) Destroy(r.gameObject);
                    onClosed?.Invoke();
                });
            };
            if (closeButton)
            {
                var x = UIKit.Button(holder, "btn-red", new Vector2(1f, 1f), new Vector2(-40, -40), new Vector2(120, 120), p.close);
                UIKit.Image(x.transform, "icon-close", new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(70, 70));
            }
            return p;
        }

        // ---------------------------------------------------------------- settings

        void ShowSettings(bool inGame)
        {
            if (board != null) board.InputEnabled = false;
            var p = OpenPopup("settings", new Vector2(900, inGame ? 1380 : 1200), true, () =>
            {
                if (board != null && !board.Finished) board.InputEnabled = true;
            });
            var body = p.body;
            float y = (body.sizeDelta.y * 0.5f) - 100f;

            Toggle(body, "icon-sound", "sound", y, () => Save.Sound, v => { Save.Sound = v; });
            Toggle(body, "icon-music", "music", y - 150f, () => Save.Music, v => { Save.Music = v; Sfx.ApplyMusic(); });
            Toggle(body, "icon-vibration", "vibration", y - 300f, () => Save.Vibration, v => { Save.Vibration = v; Haptics.Apply(); if (v) Haptics.Play(Haptic.Complete); });

            // Language switch
            float ly = y - 450f;
            var row = UIKit.Image(body, "card", new Vector2(0.5f, 0.5f), new Vector2(0, ly), new Vector2(800, 130), true).rectTransform;
            var globe = UIKit.Image(row, "icon-globe", new Vector2(0f, 0.5f), new Vector2(80, 4), new Vector2(84, 84));
            globe.color = new Color(0.25f, 0.45f, 0.95f);
            UIKit.Text(row, "language", 44, UIKit.Ink, new Vector2(0f, 0.5f), new Vector2(270, 4), new Vector2(280, 80), TextAnchor.MiddleLeft);
            Text langLabel = null;
            var lang = UIKit.Button(row, "btn-blue", new Vector2(1f, 0.5f), new Vector2(-150, 4), new Vector2(260, 100), () =>
            {
                Loc.Select(Loc.Code == "en" ? "tr" : "en");
                langLabel.text = Loc.NativeName(Loc.Code);
            });
            langLabel = UIKit.Text(lang.transform, Loc.NativeName(Loc.Code), 38, Color.white, new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(220, 70), TextAnchor.MiddleCenter, UIKit.Ink, 4f, false);

            float by = ly - 175f;
            if (inGame)
            {
                UIKit.LabelButton(body, "btn-blue", "home", new Vector2(0.5f, 0.5f), new Vector2(-200, by), new Vector2(380, 140), () =>
                {
                    p.close();
                    Go(ShowHome);
                }, "icon-home", 50);
                UIKit.LabelButton(body, "btn-green", "restart", new Vector2(0.5f, 0.5f), new Vector2(200, by), new Vector2(380, 140), () =>
                {
                    p.close();
                    Go(() => StartLevel(spec.Number));
                }, "icon-restart", 50);
                by -= 175f;
            }
            Link(body, "privacy", by, PrivacyUrl);
            Link(body, "terms", by - 95f, TermsUrl);
            Link(body, "support", by - 190f, SupportUrl);
        }

        void Toggle(RectTransform parent, string icon, string key, float y, Func<bool> get, Action<bool> set)
        {
            var row = UIKit.Image(parent, "card", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(800, 130), true).rectTransform;
            var ic = UIKit.Image(row, icon, new Vector2(0f, 0.5f), new Vector2(80, 4), new Vector2(84, 84));
            ic.color = new Color(0.25f, 0.45f, 0.95f);
            UIKit.Text(row, key, 44, UIKit.Ink, new Vector2(0f, 0.5f), new Vector2(300, 4), new Vector2(340, 80), TextAnchor.MiddleLeft);
            Image track = null;
            RectTransform knob = null;
            var btn = UIKit.Button(row, get() ? "toggle-on" : "toggle-off", new Vector2(1f, 0.5f), new Vector2(-130, 4), new Vector2(170, 96), () =>
            {
                bool v = !get();
                set(v);
                track.sprite = UIKit.Sprite(v ? "toggle-on" : "toggle-off");
                Vector2 from = knob.anchoredPosition, to = new Vector2(v ? 37 : -37, 0);
                Tween.Move(knob, from, to, 0.25f, Ease.OutBack);
            }, false);
            track = btn.GetComponent<Image>();
            track.preserveAspect = false;
            knob = UIKit.Image(btn.transform, "knob", new Vector2(0.5f, 0.5f), new Vector2(get() ? 37 : -37, 0), new Vector2(84, 84)).rectTransform;
        }

        void Link(RectTransform parent, string key, float y, string url)
        {
            var b = UIKit.Button(parent, "card", new Vector2(0.5f, 0.5f), new Vector2(0, y), new Vector2(640, 80), () => Application.OpenURL(url));
            UIKit.Text(b.transform, key, 34, new Color(0.3f, 0.36f, 0.62f), new Vector2(0.5f, 0.5f), new Vector2(0, 2), new Vector2(600, 60));
        }

        // ---------------------------------------------------------------- results

        void ShowWin()
        {
            if (current != ScreenKind.Game) return;
            int reward = spec.Reward;
            Save.Level = spec.Number + 1;
            if (spec.Number == 1) Save.TutorialDone = true;
            Sfx.Duck(2.4f);
            Sfx.Play("win", 0.9f);
            var p = OpenPopup("level_complete", new Vector2(880, 980), false);
            var body = p.body;
            var rays = UIKit.Image(p.panel, "rays", new Vector2(0.5f, 0.5f), new Vector2(0, 40), new Vector2(1500, 1500));
            rays.color = new Color(1f, 0.92f, 0.45f, 0.45f);
            rays.transform.SetAsFirstSibling();
            rays.gameObject.AddComponent<Spin>().speed = 18f;
            // three stars pop in one by one
            for (int i = 0; i < 3; i++)
            {
                var star = UIKit.Image(body, "icon-star", new Vector2(0.5f, 0.5f), new Vector2((i - 1) * 190f, 210 + (i == 1 ? 40 : 0)), new Vector2(i == 1 ? 210 : 170, i == 1 ? 210 : 170));
                star.color = Palette.Gold;
                int idx = i;
                Tween.Scale(star.transform, Vector3.zero, Vector3.one, 0.45f, Ease.OutBack, 0.3f + i * 0.18f, () =>
                {
                    Sfx.Play("snap", 0.8f, 1.2f, idx + 1);
                    Haptics.Play(Haptic.Snap);
                });
            }
            var rewardRow = UIKit.Image(body, "pill", new Vector2(0.5f, 0.5f), new Vector2(0, -20), new Vector2(380, 130), true).rectTransform;
            UIKit.Image(rewardRow, "coin", new Vector2(0f, 0.5f), new Vector2(70, 4), new Vector2(120, 120));
            UIKit.Text(rewardRow, "+" + reward, 60, UIKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(40, 4), new Vector2(220, 90), TextAnchor.MiddleCenter, null, 0f, false);
            Tween.Scale(rewardRow, Vector3.zero, Vector3.one, 0.4f, Ease.OutBack, 0.9f);
            bool claimed = false;
            UIKit.LabelButton(body, "btn-green", "next", new Vector2(0.5f, 0.5f), new Vector2(0, -250), new Vector2(560, 170), () =>
            {
                if (claimed) return;
                claimed = true;
                FlyCoins(rewardRow, reward, () => Tween.Delay(0.35f, () =>
                {
                    p.close();
                    Go(() => StartLevel(Save.Level));
                }));
            }, "icon-play", 64);
        }

        /// <summary>Coins fly from the reward to the counter and are added as they land.</summary>
        void FlyCoins(RectTransform from, int amount, Action done)
        {
            Sfx.Play("coin", 0.8f);
            Haptics.Play(Haptic.Reveal);
            if (CoinTarget == null)
            {
                AddCoins(amount);
                done?.Invoke();
                return;
            }
            const int count = 8;
            for (int i = 0; i < count; i++)
            {
                var coin = UIKit.Image(popupLayer, "coin", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90, 90)).rectTransform;
                coin.position = from.position;
                Vector3 start = coin.position + (Vector3)(UnityEngine.Random.insideUnitCircle * 80f);
                Vector3 end = CoinTarget.position;
                bool last = i == count - 1;
                Tween.Run(0.6f, k =>
                {
                    if (!coin) return;
                    Vector3 mid = (start + end) * 0.5f + Vector3.left * 200f;
                    coin.position = Vector3.Lerp(Vector3.Lerp(start, mid, k), Vector3.Lerp(mid, end, k), k);
                    coin.localScale = Vector3.one * Mathf.Lerp(1.2f, 0.7f, k);
                }, Ease.InOutCubic, () =>
                {
                    if (coin) Destroy(coin.gameObject);
                    Sfx.Play("coin", 0.4f, 1.3f, 0, 0.06f);
                    if (last)
                    {
                        AddCoins(amount);
                        done?.Invoke();
                    }
                }, i * 0.05f, coin);
            }
        }

        void ShowFail()
        {
            if (current != ScreenKind.Game || board == null) return;
            Sfx.Duck(2f);
            Sfx.Play("fail", 0.85f);
            var p = OpenPopup("out_of_space", new Vector2(880, 900), false, null, "panel-header");
            var body = p.body;
            var face = UIKit.Image(body, "train-engine", new Vector2(0.5f, 0.5f), new Vector2(0, 170), new Vector2(300, 236));
            Tween.Punch(face.transform, 0.2f, 0.6f);
            UIKit.Text(body, "fail_body", 40, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -10), new Vector2(720, 120));
            if (board.CanAddSlot)
            {
                bool owned = Save.Count(Booster.Slot) > 0 && Save.Introduced(Booster.Slot);
                var cont = UIKit.LabelButton(body, "btn-green", owned ? "continue_slot" : "continue_coins", new Vector2(0.5f, 0.5f), new Vector2(0, -170), new Vector2(640, 160), () =>
                {
                    if (owned) Save.SetCount(Booster.Slot, Save.Count(Booster.Slot) - 1);
                    else if (Save.Coins >= Save.BoosterPrice) AddCoins(-Save.BoosterPrice);
                    else { Toast(Loc.Get("no_coins")); return; }
                    p.close();
                    board.AddSlot();
                    board.Continue();
                    RefreshBoosters();
                }, owned ? "icon-slot" : "coin", 46);
                if (!owned) LocText.Bind(cont.transform.Find("Label").GetComponent<Text>(), "continue_coins", Save.BoosterPrice);
            }
            UIKit.LabelButton(body, "btn-orange", "retry", new Vector2(0.5f, 0.5f), new Vector2(0, board.CanAddSlot ? -340 : -200), new Vector2(480, 140), () =>
            {
                p.close();
                Go(() => StartLevel(spec.Number));
            }, "icon-restart", 50);
        }

        // ---------------------------------------------------------------- boosters

        static readonly string[] boosterIcons = { "icon-slot", "icon-hammer", "hand" };
        static readonly string[] boosterNames = { "booster_slot", "booster_hammer", "booster_hand" };
        static readonly string[] boosterInfo = { "booster_slot_info", "booster_hammer_info", "booster_hand_info" };

        void ShowBoosterIntro(Booster kind, Action then)
        {
            if (board != null) board.InputEnabled = false;
            Sfx.Play("slot", 0.8f);
            var p = OpenPopup(boosterNames[(int)kind], new Vector2(860, 900), false, () =>
            {
                if (board != null && !board.Finished) board.InputEnabled = true;
                then?.Invoke();
            });
            var body = p.body;
            var rays = UIKit.Image(body, "rays", new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(560, 560));
            rays.color = new Color(1f, 0.92f, 0.45f, 0.5f);
            rays.gameObject.AddComponent<Spin>().speed = 25f;
            var circle = UIKit.Image(body, "circle-btn", new Vector2(0.5f, 0.5f), new Vector2(0, 160), new Vector2(260, 272));
            UIKit.Image(circle.transform, boosterIcons[(int)kind], new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(160, 160));
            Tween.Scale(circle.transform, Vector3.zero, Vector3.one, 0.6f, Ease.OutElastic, 0.15f);
            UIKit.Text(body, "new_booster", 36, new Color(0.95f, 0.5f, 0.1f), new Vector2(0.5f, 0.5f), new Vector2(0, -30), new Vector2(700, 60));
            UIKit.Text(body, boosterInfo[(int)kind], 40, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -130), new Vector2(720, 140));
            UIKit.LabelButton(body, "btn-green", "got_it", new Vector2(0.5f, 0.5f), new Vector2(0, -300), new Vector2(500, 150), () =>
            {
                Save.MarkIntroduced(kind);
                RefreshBoosters();
                p.close();
            }, "icon-check", 56);
        }

        void ShowBuy(Booster kind)
        {
            if (board != null) board.InputEnabled = false;
            var p = OpenPopup(boosterNames[(int)kind], new Vector2(820, 820), true, () =>
            {
                if (board != null && !board.Finished) board.InputEnabled = true;
            });
            var body = p.body;
            var circle = UIKit.Image(body, "circle-btn", new Vector2(0.5f, 0.5f), new Vector2(0, 150), new Vector2(230, 240));
            UIKit.Image(circle.transform, boosterIcons[(int)kind], new Vector2(0.5f, 0.5f), new Vector2(0, 8), new Vector2(140, 140));
            UIKit.Text(body, boosterInfo[(int)kind], 38, UIKit.Ink, new Vector2(0.5f, 0.5f), new Vector2(0, -50), new Vector2(700, 130));
            var buy = UIKit.LabelButton(body, "btn-green", "buy_for", new Vector2(0.5f, 0.5f), new Vector2(0, -230), new Vector2(560, 160), () =>
            {
                if (Save.Coins < Save.BoosterPrice)
                {
                    Toast(Loc.Get("no_coins"));
                    Sfx.Play("blocked", 0.6f);
                    return;
                }
                AddCoins(-Save.BoosterPrice);
                Save.SetCount(kind, Save.Count(kind) + 1);
                Sfx.Play("coin", 0.8f);
                RefreshBoosters();
                p.close();
            }, "coin", 52);
            LocText.Bind(buy.transform.Find("Label").GetComponent<Text>(), "buy_for", Save.BoosterPrice);
        }
    }
}
