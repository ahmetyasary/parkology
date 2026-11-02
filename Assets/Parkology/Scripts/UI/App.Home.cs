using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    public sealed partial class App
    {
        enum ScreenKind { Splash, Home, Game }
        ScreenKind current = ScreenKind.Splash;

        void ShowHome()
        {
            ClearScreen();
            current = ScreenKind.Home;
            Sfx.ApplyMusic();
            StartDemo();

            CoinPill(safe, new Vector2(0f, 1f), new Vector2(190, -90));
            var gear = UIKit.Button(safe, "circle-btn", new Vector2(1f, 1f), new Vector2(-110, -96), new Vector2(150, 157), () => ShowSettings(false), false);
            UIKit.Image(gear.transform, "icon-gear", new Vector2(0.5f, 0.5f), new Vector2(0, 6), new Vector2(92, 92));
            DropDown(gear.transform, 0.35f);

            var logoHolder = UIKit.Node(safe, "Logo", new Vector2(0.5f, 1f), new Vector2(0, -330), new Vector2(960, 252));
            var logo = UIKit.Image(logoHolder, "logo", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(960, 252));
            logo.gameObject.AddComponent<Bob>().amount = 0.015f;
            Tween.Scale(logoHolder, Vector3.zero, Vector3.one, 0.7f, Ease.OutElastic, 0.15f);

            // Big play button showing the next level.
            var holder = UIKit.Node(safe, "Play Holder", new Vector2(0.5f, 0f), new Vector2(0, 250), new Vector2(640, 210));
            holder.gameObject.AddComponent<Bob>().amount = 0.025f;
            var play = UIKit.Button(holder, "btn-green", new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(640, 210), () =>
            {
                Haptics.Play(Haptic.Complete);
                Go(() => StartLevel(Save.Level));
            });
            UIKit.Image(play.transform, "icon-play", new Vector2(0f, 0.5f), new Vector2(110, 8), new Vector2(110, 110));
            UIKit.Text(play.transform, "play", 78, Color.white, new Vector2(0.5f, 0.5f), new Vector2(50, 30), new Vector2(400, 100), TextAnchor.MiddleCenter, UIKit.Ink, 7f);
            UIKit.Text(play.transform, "level_n", 40, new Color(1f, 1f, 0.85f), new Vector2(0.5f, 0.5f), new Vector2(50, -46), new Vector2(400, 60), TextAnchor.MiddleCenter, UIKit.Ink, 4f, true, Save.Level);
            Tween.Move(holder, new Vector2(0, -300), new Vector2(0, 250), 0.6f, Ease.OutBack, 0.3f);
        }

        void StartDemo()
        {
            if (board != null) Destroy(board.gameObject);
            var spec = LevelGenerator.Create(Random.Range(3, 9));
            board = GameBoard.Create(world, Cam, spec, true);
            var mine = board;
            void Again()
            {
                Tween.Delay(1.8f, () =>
                {
                    if (current == ScreenKind.Home && board == mine) StartDemo();
                });
            }
            board.Won += Again;
            board.Failed += Again;
        }

        void DropDown(Transform t, float delay)
        {
            var rt = (RectTransform)t;
            Vector2 rest = rt.anchoredPosition;
            Tween.Move(rt, rest + new Vector2(0, 300), rest, 0.5f, Ease.OutBack, delay);
        }

        /// <summary>Coin counter that animates when coins change.</summary>
        RectTransform CoinPill(Transform parent, Vector2 anchor, Vector2 pos)
        {
            var pill = UIKit.Image(parent, "pill", anchor, pos, new Vector2(300, 110), true).rectTransform;
            pill.name = "Coins";
            var coin = UIKit.Image(pill, "coin", new Vector2(0f, 0.5f), new Vector2(20, 4), new Vector2(120, 120));
            var label = UIKit.Text(pill, Save.Coins.ToString(), 46, UIKit.Brown, new Vector2(0.5f, 0.5f), new Vector2(30, 4), new Vector2(190, 70), TextAnchor.MiddleCenter, null, 0f, false);
            var tracker = pill.gameObject.AddComponent<CoinLabel>();
            tracker.label = label;
            tracker.coin = coin.transform;
            DropDown(pill, 0.3f);
            return pill;
        }

        public RectTransform CoinTarget { get; private set; }

        sealed class CoinLabel : MonoBehaviour
        {
            public Text label;
            public Transform coin;
            int shown;

            void OnEnable()
            {
                shown = Save.Coins;
                if (Instance != null)
                {
                    Instance.CoinsChanged += Changed;
                    Instance.CoinTarget = (RectTransform)coin;
                }
            }

            void OnDisable()
            {
                if (Instance != null) Instance.CoinsChanged -= Changed;
            }

            void Changed(int value)
            {
                int from = shown;
                shown = value;
                Tween.Run(0.6f, k => { if (label) label.text = Mathf.RoundToInt(Mathf.Lerp(from, value, k)).ToString(); }, Ease.OutCubic, null, 0.1f, label, ("coins", label));
                Tween.Punch(coin, 0.25f, 0.4f);
            }
        }
    }
}
