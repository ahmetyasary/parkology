using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace Parkology
{
    /// <summary>Screen transition: coloured bricks pop in diagonally, the screen swaps underneath, then they pop out.</summary>
    public sealed class Wipe : MonoBehaviour
    {
        const int Cols = 7;
        RectTransform root;
        Image[,] tiles;
        int rows;
        public bool Busy { get; private set; }

        public static Wipe Create(Transform parent)
        {
            var go = new GameObject("Wipe Canvas", typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = 100;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1080, 2340);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            go.AddComponent<GraphicRaycaster>();
            var w = go.AddComponent<Wipe>();
            w.root = UIKit.Stretch(go.transform, "Tiles");
            w.root.gameObject.SetActive(false);
            return w;
        }

        void Layout()
        {
            var size = root.rect.size;
            if (size.x <= 0f) size = new Vector2(1080, 2340);
            float tile = size.x / Cols;
            int needRows = Mathf.CeilToInt(size.y / tile) + 1;
            if (tiles != null && rows == needRows) return;
            if (tiles != null) foreach (var t in tiles) if (t) Destroy(t.gameObject);
            rows = needRows;
            tiles = new Image[Cols, rows];
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    var img = UIKit.Image(root, "tile", new Vector2(0, 0), new Vector2((x + 0.5f) * tile, (y + 0.5f) * tile), new Vector2(tile * 1.06f, tile * 1.06f), false, true);
                    img.color = Palette.Blocks[(x + y * 3) % Palette.Blocks.Length];
                    img.transform.localScale = Vector3.zero;
                    // little studs on each tile
                    for (int s = 0; s < 2; s++)
                    {
                        var stud = UIKit.Image(img.transform, "dot", new Vector2(0.5f, 0.5f), new Vector2((s - 0.5f) * tile * 0.42f, tile * 0.18f), new Vector2(tile * 0.3f, tile * 0.3f));
                        stud.color = new Color(1, 1, 1, 0.28f);
                    }
                    tiles[x, y] = img;
                }
        }

        /// <summary>Covers the screen, runs the swap, then reveals the new screen.</summary>
        public void Play(Action swap, Action done = null) => StartCoroutine(Run(swap, done));

        IEnumerator Run(Action swap, Action done)
        {
            Busy = true;
            root.gameObject.SetActive(true);
            yield return null;
            Layout();
            Sfx.Play("whoosh", 0.7f);
            const float step = 0.022f, dur = 0.24f;
            float total = (Cols + rows) * step + dur;
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    var t = tiles[x, y].transform;
                    t.localRotation = Quaternion.Euler(0, 0, -25f);
                    float delay = (x + y) * step;
                    Tween.Run(dur, k =>
                    {
                        if (!t) return;
                        t.localScale = Vector3.one * Ease.OutBack(k);
                        t.localRotation = Quaternion.Euler(0, 0, -25f * (1f - k));
                    }, Ease.Linear, null, delay, t, ("wipe", t));
                }
            yield return new WaitForSecondsRealtime(total + 0.05f);
            swap?.Invoke();
            yield return null;
            yield return null;
            Sfx.Play("whoosh", 0.5f, 1.2f);
            for (int y = 0; y < rows; y++)
                for (int x = 0; x < Cols; x++)
                {
                    var t = tiles[x, y].transform;
                    float delay = (x + y) * step;
                    Tween.Run(dur, k => { if (t) t.localScale = Vector3.one * (1f - Ease.InBack(k)); }, Ease.Linear, null, delay, t, ("wipe", t));
                }
            yield return new WaitForSecondsRealtime(total);
            root.gameObject.SetActive(false);
            Busy = false;
            done?.Invoke();
        }
    }
}
