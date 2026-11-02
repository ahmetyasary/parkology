using System;
using System.Collections.Generic;
using UnityEngine;

namespace Parkology
{
    /// <summary>
    /// Runs one level. Columns are queues: blocks join at the back (top) and leave from the front (bottom).
    /// Tapping a depot lane sends its front block onto the clockwise loop. A block on the loop drives until it passes
    /// the top of a column whose back block has its colour (or an empty column, if no column has its colour) and joins it; a full column then pushes
    /// its front block out onto the loop. A column filled with one colour clears. The loop holds a limited number of
    /// blocks: when it is full and none of them can join a column, the level is lost.
    /// </summary>
    public sealed class GameBoard : MonoBehaviour
    {
        const float Speed = 2.6f;
        /// <summary>Loop speed multiplier for the finish: nothing is left to send and the rest fills two columns.</summary>
        const float RushPace = 3.2f;
        const float LaneGap = 1.05f;
        const float LaneSpacing = 1.1f;
        const float CarHalf = 0.45f;
        const float FollowGap = 0.12f;
        const float TurnRadius = 0.5f;

        enum State { Lane, Merging, Loop, Ejecting, Parking, Parked }

        sealed class Car
        {
            public int color;
            public bool hidden;
            public Buddy view;
            public State state;
            public int lane;
            public float s, speed, entry;
            public bool waiting;
        }

        public event Action<int, int> CapacityChanged;
        public event Action Won, Failed, Dispatched, Blocked, Revealed;
        /// <summary>A block parked in a column; true when the column was full and pushed its front block out.</summary>
        public event Action<int, bool> Joined;
        public event Action<int> Cleared;

        public LevelSpec Spec { get; private set; }
        public bool Demo { get; private set; }
        public bool Autoplay { get; set; }
        public bool InputEnabled { get; set; }
        public Booster? Targeting { get; set; }
        public Func<Booster, bool> ConsumeBooster;
        public int Capacity { get; private set; }
        public int ExtraSlots { get; private set; }
        public bool Finished { get; private set; }
        public int BlocksLeft { get; private set; }
        public Camera Cam => cam;

        Camera cam;
        Transform rig, content;
        RingPath ring;
        float[] laneX, dockTop, dockBottom;
        float laneHead, stopLine;
        List<Car>[] columns, lanes;
        bool[] clearing;
        readonly List<Car> moving = new List<Car>();
        int animations;
        float stuckTime, autoTimer, introUntil, pace = 1f;
        bool rushing;
        int lastActive = -1, lastCapacity = -1;
        readonly System.Random autoRng = new System.Random(7);

        float Straight => stopLine - laneHead;
        float Arc => Mathf.PI * 0.5f * TurnRadius;

        // ------------------------------------------------------------------ setup

        public static GameBoard Create(Transform parent, Camera cam, LevelSpec spec, bool demo)
        {
            var go = new GameObject(demo ? "Demo Board" : "Board " + spec.Number);
            go.transform.SetParent(parent, false);
            var b = go.AddComponent<GameBoard>();
            b.cam = cam;
            b.rig = cam.transform.parent;
            b.Demo = demo;
            b.Build(spec);
            return b;
        }

        void Build(LevelSpec spec)
        {
            Spec = spec;
            Capacity = spec.Capacity;
            content = transform;
            float hw = spec.Cols * 0.5f + 0.95f, hh = spec.Slots * 0.5f + 0.95f;
            ring = new RingPath(Vector3.zero, hw, hh, 1f);
            laneX = new float[spec.Lanes.Length];
            for (int i = 0; i < laneX.Length; i++) laneX[i] = (i - (laneX.Length - 1) * 0.5f) * LaneSpacing;
            laneHead = -hh - 1.3f;
            stopLine = -hh - TurnRadius;
            WorldView.Build(content, ring, spec.Cols, spec.Slots, laneX, laneHead);

            columns = new List<Car>[spec.Cols];
            clearing = new bool[spec.Cols];
            dockTop = new float[spec.Cols];
            dockBottom = new float[spec.Cols];
            int count = 0;
            for (int c = 0; c < spec.Cols; c++)
            {
                columns[c] = new List<Car>();
                dockTop[c] = ring.TopAt(ColX(c));
                dockBottom[c] = ring.BottomAt(ColX(c));
                for (int k = 0; k < spec.Slots; k++)
                {
                    int color = spec.Grid[c, k];
                    if (color < 0) continue;
                    var car = NewCar(color, spec.Hidden[c, k]);
                    car.state = State.Parked;
                    car.view.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                    columns[c].Add(car);
                    DropIn(car.view.transform, SlotPos(c, k), 0.15f + (spec.Slots - 1 - k + c) * 0.04f);
                    count++;
                }
            }
            lanes = new List<Car>[spec.Lanes.Length];
            for (int l = 0; l < lanes.Length; l++)
            {
                lanes[l] = new List<Car>();
                for (int k = 0; k < spec.Lanes[l].Count; k++)
                {
                    var car = NewCar(spec.Lanes[l][k], spec.LaneHidden != null && spec.LaneHidden[l][k]);
                    car.state = State.Lane;
                    car.lane = l;
                    car.view.transform.localRotation = Quaternion.LookRotation(Vector3.forward);
                    lanes[l].Add(car);
                    var tr = car.view.transform;
                    Vector3 to = LanePos(l, k), from = to + Vector3.back * 9f;
                    tr.localPosition = from;
                    Tween.Run(0.6f, e => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, e); }, Ease.OutCubic, null, 0.3f + k * 0.07f + l * 0.05f, tr, ("lane", tr));
                    count++;
                }
            }
            BlocksLeft = count;
            introUntil = Time.time + 1.1f;
            InputEnabled = true;
            FitCamera();
        }

        Car NewCar(int color, bool hidden)
        {
            var car = new Car { color = color, hidden = hidden };
            car.view = Buddy.Create(Kit.Block, content, hidden ? -1 : color, "Block");
            if (hidden) car.view.SetRestFace(Face.Mystery);
            return car;
        }

        void DropIn(Transform t, Vector3 rest, float delay)
        {
            Vector3 from = rest + Vector3.up * 7f;
            t.localPosition = from;
            Tween.Run(0.55f, e => { if (t) t.localPosition = Vector3.LerpUnclamped(from, rest, e); }, Ease.OutBounce,
                () => { if (t && !Demo) Sfx.Play("drop", 0.35f, 1f, 0, 0.05f); }, delay, t, ("move", t));
        }

        float ColX(int c) => c - (Spec.Cols - 1) * 0.5f;
        /// <summary>Slot 0 is the front (bottom) of the column.</summary>
        Vector3 SlotPos(int c, int k) => new Vector3(ColX(c), 0.04f, k - (Spec.Slots - 1) * 0.5f);
        Vector3 LanePos(int lane, int index) => new Vector3(laneX[lane], 0.02f, laneHead - index * LaneGap);

        /// <summary>Frames loop, lot and the front of the depots between the HUD and the booster bar.</summary>
        public void FitCamera()
        {
            float bottom = Demo ? 0.22f : 0.13f, top = Demo ? 0.36f : 0.16f;
            Vector3 lo = new Vector3(-ring.HalfW - 0.7f, 0, laneHead - LaneGap * 1.7f);
            Vector3 hi = new Vector3(ring.HalfW + 0.7f, 0.9f, ring.HalfH + 0.75f);
            var ct = cam.transform;
            ct.localRotation = Quaternion.Euler(57f, 0f, 0f);
            ct.localPosition = -ct.forward * 40f;
            float ymin = float.MaxValue, ymax = float.MinValue, xmax = 0f;
            for (int i = 0; i < 8; i++)
            {
                var p = new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, (i & 4) == 0 ? lo.z : hi.z);
                var local = ct.InverseTransformPoint(rig.TransformPoint(p));
                ymin = Mathf.Min(ymin, local.y);
                ymax = Mathf.Max(ymax, local.y);
                xmax = Mathf.Max(xmax, Mathf.Abs(local.x));
            }
            float size = Mathf.Max(xmax / cam.aspect, (ymax - ymin) / (2f * (1f - top - bottom)));
            cam.orthographicSize = size;
            // In play the loop sits right under the HUD and the depots run off the bottom (like the reference);
            // the home-screen demo is centred in its band.
            float band = (bottom + (1f - top)) * 0.5f;
            float center = Demo ? (ymin + ymax) * 0.5f - (band - 0.5f) * 2f * size : ymax - (0.5f - top) * 2f * size;
            ct.localPosition += ct.up * center;
        }

        // ------------------------------------------------------------------ queries for the UI

        /// <summary>Blocks currently on (or joining) the loop.</summary>
        public int ActiveCount
        {
            get
            {
                int n = 0;
                foreach (var c in moving)
                    if (c.state == State.Merging || c.state == State.Loop || c.state == State.Ejecting) n++;
                return n;
            }
        }

        public Vector3 LaneScreenPoint(int lane) => cam.WorldToScreenPoint(transform.TransformPoint(LanePos(lane, 0) + Vector3.up * 0.5f));
        public Vector3 SlotScreenPoint(int c, int k) => cam.WorldToScreenPoint(transform.TransformPoint(SlotPos(c, k) + Vector3.up * 0.5f));

        /// <summary>A lane whose front block can park right away (for tutorial pointers), else any lane with blocks.</summary>
        public int SuggestLane()
        {
            int any = -1;
            for (int l = 0; l < lanes.Length; l++)
            {
                if (lanes[l].Count == 0 || LaneBusy(l)) continue;
                if (!lanes[l][0].hidden && CanAnyJoin(lanes[l][0].color)) return l;
                if (any < 0) any = l;
            }
            return any;
        }

        /// <summary>The column a block of this lane's front colour would park in, or -1.</summary>
        public int HomeColumn(int lane)
        {
            if (lane < 0 || lanes[lane].Count == 0) return -1;
            for (int c = 0; c < Spec.Cols; c++) if (CanJoin(c, lanes[lane][0].color)) return c;
            return -1;
        }

        /// <summary>Column of the newest block on the loop that has a home, for "it drives there" pointers.</summary>
        public int LoopHomeColumn()
        {
            for (int i = moving.Count - 1; i >= 0; i--)
            {
                if (moving[i].hidden) continue;
                for (int c = 0; c < Spec.Cols; c++) if (CanJoin(c, moving[i].color)) return c;
            }
            return -1;
        }

        public bool FindHidden(out int column, out int slot)
        {
            for (column = 0; column < Spec.Cols; column++)
                for (slot = 0; slot < columns[column].Count; slot++)
                    if (columns[column][slot].hidden) return true;
            column = slot = -1;
            return false;
        }
        public bool CanAddSlot => ExtraSlots < 3;

        public void AddSlot()
        {
            ExtraSlots++;
            Capacity++;
            stuckTime = 0f;
            Sfx.Play("slot");
            Haptics.Play(Haptic.Complete);
        }

        public void Continue()
        {
            Finished = false;
            InputEnabled = true;
            stuckTime = 0f;
            foreach (var c in moving) c.view.SetRestFace(Face.Happy);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (!Finished && InputEnabled && !Demo && Time.time > introUntil && Pointer.Down(out Vector2 screen)) HandleTap(screen);
            if ((Demo || Autoplay) && !Finished && Time.time > introUntil) AutoStep(dt);
            CheckRush(dt);

            for (int i = moving.Count - 1; i >= 0; i--)
            {
                if (i >= moving.Count) continue;
                var car = moving[i];
                if (car.state == State.Merging) Merge(car, dt);
                else if (car.state == State.Loop) Drive(car, dt);
            }
            CheckEnd(dt);

            int active = ActiveCount;
            if (active != lastActive || Capacity != lastCapacity)
            {
                lastActive = active;
                lastCapacity = Capacity;
                CapacityChanged?.Invoke(active, Capacity);
            }
        }

        // ------------------------------------------------------------------ input

        bool Ground(Vector2 screen, float height, out Vector3 local)
        {
            var ray = cam.ScreenPointToRay(screen);
            var plane = new Plane(transform.up, transform.TransformPoint(new Vector3(0, height, 0)));
            local = default;
            if (!plane.Raycast(ray, out float d)) return false;
            local = transform.InverseTransformPoint(ray.GetPoint(d));
            return true;
        }

        void HandleTap(Vector2 screen)
        {
            if (!Ground(screen, 0.5f, out Vector3 p)) return;
            if (Targeting == Booster.Hammer)
            {
                int c = Mathf.RoundToInt(p.x + (Spec.Cols - 1) * 0.5f);
                if (c >= 0 && c < Spec.Cols && Mathf.Abs(p.z) < Spec.Slots * 0.5f + 0.4f) Hammer(c);
                return;
            }
            if (!FindLane(p, out int lane)) return;
            if (Targeting == Booster.Hand)
            {
                int index = Mathf.RoundToInt((laneHead - p.z) / LaneGap);
                if (index >= 0 && index < lanes[lane].Count) Hand(lane, index);
                return;
            }
            Dispatch(lane);
        }

        bool FindLane(Vector3 p, out int lane)
        {
            lane = -1;
            if (p.z > -ring.HalfH - 0.6f) return false;
            float best = 0.6f;
            for (int i = 0; i < laneX.Length; i++)
            {
                float d = Mathf.Abs(p.x - laneX[i]);
                if (d < best) { best = d; lane = i; }
            }
            return lane >= 0;
        }

        // ------------------------------------------------------------------ actions

        bool LaneBusy(int lane)
        {
            foreach (var c in moving)
                if (c.lane == lane && c.state == State.Merging) return true;
            return false;
        }

        bool RoadFull()
        {
            if (ActiveCount < Capacity) return false;
            if (!Demo)
            {
                Sfx.Play("blocked", 0.8f);
                Haptics.Play(Haptic.Warning);
            }
            Blocked?.Invoke();
            return true;
        }

        /// <summary>Sends the front block of a depot lane onto the loop.</summary>
        public bool Dispatch(int lane)
        {
            if (lanes[lane].Count == 0 || LaneBusy(lane)) return false;
            var car = lanes[lane][0];
            if (RoadFull())
            {
                car.view.Express(Face.Worried, 0.8f);
                Wiggle(car.view.transform);
                return false;
            }
            lanes[lane].RemoveAt(0);
            StartMerge(car, lane);
            ShiftLane(lane);
            return true;
        }

        void StartMerge(Car car, int lane)
        {
            car.state = State.Merging;
            car.lane = lane;
            car.entry = 0f;
            car.speed = Speed * 0.6f;
            car.waiting = false;
            Tween.Stop(("lane", car.view.transform));
            moving.Add(car);
            car.view.Express(Face.Surprised, 0.5f);
            car.view.Squash(0.15f);
            if (!Demo)
            {
                Sfx.Play("go", 0.7f);
                Haptics.Play(Haptic.Tap);
            }
            Dispatched?.Invoke();
        }

        void ShiftLane(int lane)
        {
            if (lanes[lane].Count > 0 && lanes[lane][0].hidden) RevealCar(lanes[lane][0], 0.25f);
            for (int k = 0; k < lanes[lane].Count; k++)
            {
                var tr = lanes[lane][k].view.transform;
                Vector3 from = tr.localPosition, to = LanePos(lane, k);
                Tween.Run(0.3f, e => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, e); }, Ease.OutBack, null, k * 0.04f, tr, ("lane", tr));
            }
        }

        void Wiggle(Transform t)
        {
            Quaternion rest = Quaternion.LookRotation(Vector3.forward);
            Tween.Run(0.4f, e => { if (t) t.localRotation = rest * Quaternion.Euler(0, Mathf.Sin(e * Mathf.PI * 6f) * 14f * (1f - e), 0); }, Ease.Linear,
                () => { if (t) t.localRotation = rest; }, 0f, t, ("wiggle", t));
        }

        /// <summary>Hand booster: any waiting block jumps to the front of its depot and drives onto the loop.</summary>
        void Hand(int lane, int index)
        {
            if (LaneBusy(lane) || RoadFull()) return;
            if (ConsumeBooster == null || !ConsumeBooster(Booster.Hand)) return;
            Targeting = null;
            var car = lanes[lane][index];
            lanes[lane].RemoveAt(index);
            if (car.hidden) RevealCar(car, 0f);
            var tr = car.view.transform;
            Vector3 from = tr.localPosition, to = LanePos(lane, 0);
            car.view.Express(Face.Joy, 1f);
            Sfx.Play("magnet", 0.7f);
            Haptics.Play(Haptic.Reveal);
            animations++;
            Tween.Run(0.45f, e =>
            {
                if (!tr) return;
                Vector3 p = Vector3.Lerp(from, to, e);
                p.y += Mathf.Sin(e * Mathf.PI) * 1.6f;
                tr.localPosition = p;
            }, Ease.InOutCubic, () =>
            {
                animations--;
                if (!tr) return;
                car.view.Squash(0.2f);
                StartMerge(car, lane);
            }, 0f, tr, ("lane", tr));
            ShiftLane(lane);
        }

        /// <summary>Hammer booster: strikes the back block so the column shoves its front block onto the loop.</summary>
        void Hammer(int c)
        {
            var col = columns[c];
            if (col.Count == 0 || clearing[c]) return;
            if (ConsumeBooster == null || !ConsumeBooster(Booster.Hammer)) return;
            Targeting = null;
            var back = col[col.Count - 1];
            Vector3 pos = SlotPos(c, col.Count - 1);

            var hammer = new GameObject("Hammer");
            hammer.transform.SetParent(content, false);
            var mesh = new GameObject("Mesh");
            mesh.transform.SetParent(hammer.transform, false);
            mesh.AddComponent<MeshFilter>().sharedMesh = Kit.Hammer;
            var hr = mesh.AddComponent<MeshRenderer>();
            hr.sharedMaterial = Kit.Plastic;
            var mpb = new MaterialPropertyBlock();
            mpb.SetColor("_Color", new Color(0.3f, 0.55f, 1f));
            mpb.SetVector("_DecalRect", Vector4.zero);
            hr.SetPropertyBlock(mpb);
            // pivot to the right of the back block: wind up, then slam the head down onto it
            hammer.transform.localPosition = pos + new Vector3(1.25f, 0.2f, 0f);
            hammer.transform.localRotation = Quaternion.Euler(0, 0, -25f);
            var ht = hammer.transform;
            back.view.Express(Face.Surprised, 1.2f);
            Sfx.Play("whoosh", 0.5f, 1.3f);
            animations++;
            Tween.Run(0.3f, e => { if (ht) ht.localRotation = Quaternion.Euler(0, 0, Mathf.Lerp(-25f, 76f, e)); }, Ease.InCubic, () =>
            {
                animations--;
                Fx.Sparkle(transform.TransformPoint(pos + Vector3.up * 0.8f), 10);
                Fx.Shake(rig, 0.5f);
                Sfx.Play("hammer", 0.9f);
                Haptics.Play(Haptic.Heavy);
                if (back.view)
                {
                    back.view.Squash(0.4f);
                    back.view.Flash(0.7f);
                }
                Tween.Run(0.3f, e => { if (ht) ht.localScale = Vector3.one * (1f - e); }, Ease.InBack, () => { if (ht) Destroy(ht.gameObject); }, 0.15f, ht);
                if (col.Count == 0 || clearing[c]) return;
                var front = col[0];
                col.RemoveAt(0);
                Eject(front, c);
                Settle(c);
                Reveal(c);
            }, 0.05f, ht);
        }

        // ------------------------------------------------------------------ traffic

        float MergeParam(int lane) => ring.BottomAt(laneX[lane] - TurnRadius);

        /// <summary>Straight up out of the depot to the stop line, then a quarter turn left onto the bottom straight.</summary>
        void Merge(Car car, float dt)
        {
            if (!car.waiting && car.entry >= Straight && car.entry < Straight + 0.0001f) car.waiting = true;
            if (car.waiting)
            {
                if (!MergeClear(car))
                {
                    car.speed = 0f;
                    Place(car, dt);
                    return;
                }
                car.waiting = false;
                car.entry = Straight + 0.0002f;
            }
            float target = car.entry < Straight ? Speed : FollowSpeed(car, MergeParam(car.lane) - (Straight + Arc - car.entry));
            car.speed = Mathf.MoveTowards(car.speed, target, dt * 8f);
            float next = car.entry + car.speed * dt;
            car.entry = car.entry < Straight ? Mathf.Min(next, Straight) : next;
            if (car.entry >= Straight + Arc)
            {
                car.state = State.Loop;
                car.s = MergeParam(car.lane) + (car.entry - Straight - Arc);
                Drive(car, 0f);
                return;
            }
            Place(car, dt);
        }

        void Place(Car car, float dt)
        {
            var tr = car.view.transform;
            float x = laneX[car.lane];
            Vector3 pos, heading;
            if (car.entry <= Straight)
            {
                pos = new Vector3(x, 0.02f, laneHead + car.entry);
                heading = Vector3.forward;
            }
            else
            {
                float a = (car.entry - Straight) / TurnRadius;
                var center = new Vector3(x - TurnRadius, 0.02f, stopLine);
                pos = center + new Vector3(Mathf.Cos(a), 0, Mathf.Sin(a)) * TurnRadius;
                heading = new Vector3(-Mathf.Sin(a), 0, Mathf.Cos(a));
            }
            tr.localPosition = pos;
            tr.localRotation = Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(heading), Mathf.Clamp01(dt * 20f));
        }

        bool InArc(Car o) => o.state == State.Merging && !o.waiting && o.entry > Straight;

        /// <summary>The joining point must be free, and the next block behind must have room to brake.</summary>
        bool MergeClear(Car car)
        {
            float join = MergeParam(car.lane);
            foreach (var o in moving)
            {
                if (o == car) continue;
                if (InArc(o)) return false;
                if (o.state != State.Loop) continue;
                if (ring.Ahead(join, o.s) < CarHalf * 2f + 0.35f) return false;
                if (ring.Ahead(o.s, join) < 1.5f) return false;
            }
            return true;
        }

        /// <summary>Car-following: slow down when the block ahead gets close.</summary>
        float FollowSpeed(Car car, float myS)
        {
            float best = float.MaxValue;
            foreach (var o in moving)
            {
                if (o == car) continue;
                float os;
                if (o.state == State.Loop) os = o.s;
                else if (InArc(o)) os = MergeParam(o.lane);
                else continue;
                float gap = ring.Ahead(myS, os) - CarHalf * 2f;
                if (gap > ring.Length - 1.5f) continue; // that block is just behind us
                best = Mathf.Min(best, gap);
            }
            if (best == float.MaxValue) return Speed * pace;
            return Speed * pace * Mathf.Clamp01((best - FollowGap) / (0.6f * pace));
        }

        void Drive(Car car, float dt)
        {
            float target = FollowSpeed(car, car.s);
            car.speed = Mathf.MoveTowards(car.speed, target, dt * pace * (target < car.speed ? 14f : 6f));
            float prev = car.s;
            car.s += car.speed * dt;
            var tr = car.view.transform;
            tr.localPosition = ring.Evaluate(car.s, out Vector3 tangent) + Vector3.up * 0.02f;
            tr.localRotation = dt > 0f ? Quaternion.Slerp(tr.localRotation, Quaternion.LookRotation(tangent), Mathf.Clamp01(dt * 18f)) : Quaternion.LookRotation(tangent);
            float bob = car.speed > 0.1f ? Mathf.Abs(Mathf.Sin(Time.time * 14f + car.color)) * 0.025f : 0f;
            car.view.Body.localPosition = new Vector3(0, bob, 0);
            if (dt <= 0f) return;
            for (int c = 0; c < Spec.Cols; c++)
                if (Crossed(prev, car.s, dockTop[c]) && TryJoin(car, c)) return;
        }

        bool Crossed(float prev, float now, float mark)
        {
            float moved = now - prev;
            if (moved <= 0f) return false;
            float d = ring.Ahead(prev, mark);
            return d > 0f && d <= moved;
        }

        bool HasBack(int c, int color)
        {
            var col = columns[c];
            if (clearing[c] || col.Count == 0) return false;
            var back = col[col.Count - 1];
            return !back.hidden && back.color == color;
        }

        /// <summary>
        /// A block goes home to a column whose back block has its colour. It only turns into an empty column when no
        /// column has its colour at the back, so a colour never splits across two empty columns.
        /// </summary>
        bool CanJoin(int c, int color)
        {
            if (clearing[c]) return false;
            if (columns[c].Count > 0) return HasBack(c, color);
            for (int o = 0; o < Spec.Cols; o++)
                if (HasBack(o, color)) return false;
            return true;
        }

        /// <summary>The block turns into the column from the top; a full column shoves its front block out.</summary>
        bool TryJoin(Car car, int c)
        {
            if (!CanJoin(c, car.color)) return false;
            var col = columns[c];
            moving.Remove(car);
            car.state = State.Parking;
            col.Add(car);
            var tr = car.view.transform;
            car.view.Body.localPosition = Vector3.zero;
            Car pushed = null;
            if (col.Count > Spec.Slots)
            {
                pushed = col[0];
                col.RemoveAt(0);
            }
            int slot = col.Count - 1;
            Vector3 from = tr.localPosition, to = SlotPos(c, slot);
            Quaternion fromRot = tr.localRotation, toRot = Quaternion.LookRotation(Vector3.back);
            animations++;
            Tween.Run(0.36f, e =>
            {
                if (!tr) return;
                Vector3 p = Vector3.Lerp(from, to, e);
                p.y += Mathf.Sin(e * Mathf.PI) * 0.5f;
                tr.localPosition = p;
                tr.localRotation = Quaternion.Slerp(fromRot, toRot, e);
            }, Ease.OutCubic, () =>
            {
                animations--;
                if (!tr) return;
                car.state = State.Parked;
                car.view.Squash(0.2f);
                if (!Demo)
                {
                    Sfx.Play("snap", 0.85f, 1f, Mathf.Clamp(slot - 1, 1, 3));
                    Haptics.Play(Haptic.Snap);
                }
                CheckSorted(c);
            }, 0f, tr, ("move", tr));
            if (pushed != null) Eject(pushed, c);
            Settle(c, car);
            Reveal(c);
            Joined?.Invoke(c, pushed != null);
            if (!Demo)
            {
                Sfx.Play("hop", 0.5f);
                Haptics.Play(Haptic.Hop);
            }
            return true;
        }

        /// <summary>Slides parked blocks to their slots after the queue moved.</summary>
        void Settle(int c, Car skip = null)
        {
            var col = columns[c];
            for (int k = 0; k < col.Count; k++)
            {
                var car = col[k];
                if (car == skip || car.state != State.Parked) continue;
                var tr = car.view.transform;
                Vector3 from = tr.localPosition, to = SlotPos(c, k);
                if ((from - to).sqrMagnitude < 0.0001f) continue;
                Tween.Run(0.28f, e => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, e); }, Ease.OutBack, null, 0.04f * k, tr, ("move", tr));
            }
        }

        /// <summary>The front block rolls out of the bottom of its column and joins the loop.</summary>
        void Eject(Car car, int c)
        {
            car.state = State.Ejecting;
            car.lane = -1;
            moving.Add(car);
            var tr = car.view.transform;
            Vector3 from = tr.localPosition;
            Vector3 to = new Vector3(ColX(c), 0.04f, ring.Center.z - ring.HalfH);
            Quaternion fromRot = tr.localRotation, toRot = Quaternion.LookRotation(Vector3.left);
            car.view.Express(Face.Surprised, 0.7f);
            animations++;
            Tween.Run(0.32f, e =>
            {
                if (!tr) return;
                tr.localPosition = Vector3.Lerp(from, to, e);
                tr.localRotation = Quaternion.Slerp(fromRot, toRot, e);
            }, Ease.InOutCubic, () =>
            {
                animations--;
                if (!tr) return;
                car.state = State.Loop;
                car.s = dockBottom[c];
                car.speed = Speed * 0.5f;
            }, 0.06f, tr, ("move", tr));
            if (!Demo) Sfx.Play("go", 0.45f, 1.2f, 0, 0.08f);
        }

        /// <summary>Mystery blocks show their colour once they reach either end of the column.</summary>
        void Reveal(int c)
        {
            var col = columns[c];
            if (col.Count == 0) return;
            foreach (var car in new[] { col[0], col[col.Count - 1] })
                if (car.hidden) RevealCar(car, 0.1f);
        }

        void RevealCar(Car car, float delay)
        {
            {
                car.hidden = false;
                var view = car.view;
                var body = view.Body;
                bool swapped = false;
                Tween.Run(0.5f, e =>
                {
                    if (!body) return;
                    body.localScale = new Vector3(1f, 1f + Mathf.Sin(e * Mathf.PI) * 0.35f, 1f);
                    if (!swapped && e > 0.4f)
                    {
                        swapped = true;
                        view.SetColor(car.color);
                        view.SetRestFace(Face.Happy);
                        view.Express(Face.Surprised, 0.6f);
                        view.Flash(0.8f);
                    }
                }, Ease.OutCubic, () => { if (body) body.localScale = Vector3.one; }, delay, body);
                Fx.Sparkle(transform.TransformPoint(view.transform.localPosition + Vector3.up * 0.8f), 8);
                if (!Demo)
                {
                    Sfx.Play("reveal", 0.6f);
                    Haptics.Play(Haptic.Reveal);
                }
                Revealed?.Invoke();
            }
        }

        /// <summary>A column full of one colour clears with a celebration.</summary>
        void CheckSorted(int c)
        {
            var col = columns[c];
            if (clearing[c] || col.Count != Spec.Slots) return;
            foreach (var car in col)
                if (car.hidden || car.color != col[0].color || car.state != State.Parked) return;
            clearing[c] = true;
            animations++;
            var cars = col.ToArray();
            if (!Demo)
            {
                Sfx.Play("complete", 0.85f);
                Haptics.Play(Haptic.Complete);
            }
            for (int k = 0; k < cars.Length; k++)
            {
                var view = cars[k].view;
                view.Express(Face.Joy, 2f);
                view.Flash(0.5f);
                var tr = view.transform;
                bool last = k == 0;
                Tween.Run(0.32f, e =>
                {
                    if (!tr) return;
                    tr.localScale = Vector3.one * Mathf.Max(0f, 1f + Mathf.Sin(e * Mathf.PI) * 0.25f - (e > 0.6f ? (e - 0.6f) * 2.5f : 0f));
                }, Ease.Linear, () =>
                {
                    if (tr)
                    {
                        Fx.Burst(transform.TransformPoint(tr.localPosition + Vector3.up * 0.4f), Palette.Block(cars[0].color), 8, 2.6f);
                        Destroy(tr.gameObject);
                    }
                    if (!last) return;
                    col.Clear();
                    clearing[c] = false;
                    animations--;
                    BlocksLeft -= cars.Length;
                    Cleared?.Invoke(c);
                    Fx.Sparkle(transform.TransformPoint(SlotPos(c, Spec.Slots / 2) + Vector3.up * 0.8f), 12);
                }, 0.25f + (cars.Length - 1 - k) * 0.07f, tr, ("clear", tr));
            }
        }

        bool LanesEmpty
        {
            get
            {
                foreach (var l in lanes) if (l.Count > 0) return false;
                return true;
            }
        }

        /// <summary>
        /// Once nothing is left to send and the remaining blocks fill at most two columns, the loop speeds up so the
        /// last blocks hurry home and the level wraps up without a slow lap.
        /// </summary>
        void CheckRush(float dt)
        {
            if (!rushing && !Demo && !Finished && BlocksLeft > 0 && BlocksLeft <= Spec.Slots * 2 && LanesEmpty && ActiveCount > 0)
            {
                rushing = true;
                Sfx.Play("whoosh", 0.6f, 1.25f);
                Haptics.Play(Haptic.Soft);
                foreach (var car in moving) car.view.Express(Face.Joy, 1.2f);
            }
            pace = Mathf.MoveTowards(pace, rushing && !Finished ? RushPace : 1f, dt * 4f);
        }

        // ------------------------------------------------------------------ win / lose

        bool CanAnyJoin(int color)
        {
            for (int c = 0; c < Spec.Cols; c++) if (CanJoin(c, color)) return true;
            return false;
        }

        void CheckEnd(float dt)
        {
            if (Finished) return;
            bool lanesEmpty = LanesEmpty;
            if (BlocksLeft <= 0 && animations == 0 && moving.Count == 0)
            {
                Finished = true;
                InputEnabled = false;
                if (!Demo)
                {
                    Fx.Confetti(transform.position, ring.HalfW * 2f, ring.HalfH * 2f);
                    Haptics.Play(Haptic.Win);
                }
                Won?.Invoke();
                return;
            }
            // Lost: the loop is full (or nothing is left to send) and no block on it can join any column.
            bool stuck = animations == 0 && moving.Count > 0 && (ActiveCount >= Capacity || lanesEmpty);
            if (stuck)
                foreach (var car in moving)
                    if (car.state != State.Loop || CanAnyJoin(car.color)) { stuck = false; break; }
            stuckTime = stuck ? stuckTime + dt : 0f;
            if (stuckTime > 1.6f)
            {
                Finished = true;
                InputEnabled = false;
                Targeting = null;
                foreach (var car in moving) car.view.SetRestFace(Face.Worried);
                if (!Demo) Haptics.Play(Haptic.Fail);
                Failed?.Invoke();
            }
        }

        // ------------------------------------------------------------------ demo / autoplay

        void AutoStep(float dt)
        {
            autoTimer -= dt;
            if (autoTimer > 0f) return;
            autoTimer = Demo ? 1.1f : 0.45f;
            if (ActiveCount >= Capacity) return;
            var good = new List<int>();
            var any = new List<int>();
            for (int l = 0; l < lanes.Length; l++)
            {
                if (lanes[l].Count == 0 || LaneBusy(l)) continue;
                any.Add(l);
                if (CanAnyJoin(lanes[l][0].color)) good.Add(l);
            }
            if (good.Count > 0) Dispatch(good[autoRng.Next(good.Count)]);
            else if (any.Count > 0 && ActiveCount <= Capacity - 2) Dispatch(any[autoRng.Next(any.Count)]);
        }

        void OnDestroy() => Fx.Clear();
    }
}
