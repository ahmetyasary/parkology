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
            public float s, speed, entry, land, held;
            public float yaw, roll, pitch, lastSpeed;
            public bool waiting;
            /// <summary>Column this block waits above until the column's front block has room to roll out, or -1.</summary>
            public int hold = -1;
        }

        public event Action<int, int> CapacityChanged;
        public event Action Won, Failed, Dispatched, Blocked, Revealed, CrateOpened;
        /// <summary>A tap on a waiting block that is not next in its lane.</summary>
        public event Action NotNext;
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
        bool[] clearing, hammered;
        int[] crates, queued;
        Transform[] crateViews;
        readonly List<Car> moving = new List<Car>();
        readonly List<Vector2> taps = new List<Vector2>();
        int animations;
        float stuckTime, autoTimer, autoStall, introUntil, pace = 1f;
        bool rushing;
        int lastActive = -1, lastCapacity = -1;
        readonly System.Random autoRng = new System.Random(7);

        float Straight => stopLine - laneHead;
        /// <summary>Where a block waits for a gap: its nose just short of the road, so passing traffic never touches it.</summary>
        float WaitAt => Straight - 0.5f;
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
            // a block waiting for a gap stays off the road and a full block length ahead of the next one in its depot
            laneHead = -hh - 2f;
            stopLine = -hh - TurnRadius;
            WorldView.Build(content, ring, spec.Cols, spec.Slots, laneX, laneHead);

            columns = new List<Car>[spec.Cols];
            clearing = new bool[spec.Cols];
            hammered = new bool[spec.Cols];
            crates = new int[spec.Cols];
            crateViews = new Transform[spec.Cols];
            dockTop = new float[spec.Cols];
            dockBottom = new float[spec.Cols];
            int count = 0;
            for (int c = 0; c < spec.Cols; c++)
            {
                columns[c] = new List<Car>();
                dockTop[c] = ring.TopAt(ColX(c));
                dockBottom[c] = ring.BottomAt(ColX(c));
                crates[c] = spec.Crates != null ? spec.Crates[c] : -1;
                if (crates[c] >= 0)
                {
                    crateViews[c] = WorldView.Crate(content, new Vector3(ColX(c), WorldView.LotTop, 0f), spec.Slots, crates[c]);
                    DropIn(crateViews[c], crateViews[c].localPosition, 0.15f + c * 0.04f);
                }
                for (int k = 0; k < spec.Slots; k++)
                {
                    int color = spec.Grid[c, k];
                    if (color < 0) continue;
                    var car = NewCar(color, spec.Hidden[c, k]);
                    car.state = State.Parked;
                    car.view.transform.localRotation = Quaternion.LookRotation(Vector3.back);
                    columns[c].Add(car);
                    // blocks under a crate stay out of sight until it opens
                    if (crates[c] >= 0) car.view.gameObject.SetActive(false);
                    else DropIn(car.view.transform, SlotPos(c, k), 0.15f + (spec.Slots - 1 - k + c) * 0.04f);
                    count++;
                }
            }
            lanes = new List<Car>[spec.Lanes.Length];
            queued = new int[spec.Lanes.Length];
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
        Vector3 SlotPos(int c, int k) => new Vector3(ColX(c), WorldView.LotTop, k - (Spec.Slots - 1) * 0.5f);
        Vector3 LanePos(int lane, int index) => new Vector3(laneX[lane], 0.02f, laneHead - index * LaneGap);

        /// <summary>Perspective lens and tilt: a toy table seen from a low angle, so blocks show their sides and depth.</summary>
        const float Fov = 30f, Tilt = 52f;

        /// <summary>
        /// Frames loop, lot and the front of the depots between the HUD and the booster bar: the closest camera
        /// distance at which everything fits. In play the loop sits right under the HUD and the depots run off the
        /// bottom (like the reference); the home-screen demo is centred in its band.
        /// </summary>
        public void FitCamera()
        {
            float bottom = Demo ? 0.22f : 0.13f, top = Demo ? 0.36f : 0.18f;
            Vector3 lo = new Vector3(-ring.HalfW - 0.7f, 0, laneHead - LaneGap * 1.7f);
            Vector3 hi = new Vector3(ring.HalfW + 0.7f, 0.9f, ring.HalfH + 0.75f);
            var corners = new Vector3[8];
            for (int i = 0; i < 8; i++)
                corners[i] = new Vector3((i & 1) == 0 ? lo.x : hi.x, (i & 2) == 0 ? lo.y : hi.y, (i & 4) == 0 ? lo.z : hi.z);
            cam.orthographic = false;
            cam.fieldOfView = Fov;
            var rot = Quaternion.Euler(Tilt, 0f, 0f);
            Vector3 fwd = rot * Vector3.forward, up = rot * Vector3.up, right = rot * Vector3.right, center = (lo + hi) * 0.5f;
            float tan = Mathf.Tan(Fov * 0.5f * Mathf.Deg2Rad), aspect = cam.aspect;

            // Viewport extents of the corners for a camera d back from the centre and raised by shift.
            void Extents(float d, float shift, out float xmax, out float ymin, out float ymax)
            {
                Vector3 eye = center - fwd * d + up * shift;
                xmax = 0f; ymin = float.MaxValue; ymax = float.MinValue;
                foreach (var p in corners)
                {
                    Vector3 v = p - eye;
                    float z = Vector3.Dot(v, fwd);
                    xmax = Mathf.Max(xmax, Mathf.Abs(Vector3.Dot(v, right) / (z * 2f * tan * aspect)));
                    float y = 0.5f + Vector3.Dot(v, up) / (z * 2f * tan);
                    ymin = Mathf.Min(ymin, y);
                    ymax = Mathf.Max(ymax, y);
                }
            }
            // The shift that puts the top under the HUD (play) or centres the board in its band (demo).
            float Align(float d)
            {
                float a = -60f, b = 60f;
                for (int i = 0; i < 40; i++)
                {
                    float m = (a + b) * 0.5f;
                    Extents(d, m, out _, out float y0, out float y1);
                    float at = Demo ? (y0 + y1) * 0.5f : y1, goal = Demo ? (bottom + 1f - top) * 0.5f : 1f - top;
                    if (at > goal) a = m; else b = m; // raising the camera moves everything down the screen
                }
                return (a + b) * 0.5f;
            }
            bool Fits(float d, out float shift)
            {
                shift = Align(d);
                Extents(d, shift, out float xmax, out float y0, out float y1);
                return xmax <= 0.5f && y0 >= bottom - 0.001f && y1 <= 1f - top + 0.001f;
            }
            float near = 4f, far = 300f;
            for (int i = 0; i < 40; i++)
            {
                float m = (near + far) * 0.5f;
                if (Fits(m, out _)) far = m; else near = m;
            }
            Fits(far, out float s);
            var ct = cam.transform;
            ct.localRotation = rot;
            ct.localPosition = center - fwd * far + up * s;
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

        /// <summary>Screen point of the capacity signpost, planted in the grass right of the depot lanes, below the loop.</summary>
        public Vector3 SignScreenPoint => cam.WorldToScreenPoint(transform.TransformPoint(
            new Vector3(laneX[laneX.Length - 1] + WorldView.RoadWidth * 0.5f + 0.95f, 0f, -ring.HalfH - WorldView.RoadWidth * 0.5f - 1.6f)));

        public bool FindCrate(out int column)
        {
            for (column = 0; column < Spec.Cols; column++) if (crates[column] >= 0) return true;
            column = -1;
            return false;
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
            Array.Clear(queued, 0, queued.Length);
            foreach (var c in moving) c.view.SetRestFace(Face.Happy);
        }

        // ------------------------------------------------------------------ frame

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.05f);
            if (!Finished && InputEnabled && !Demo && Time.time > introUntil)
            {
                Pointer.Downs(taps);
                foreach (var screen in taps) HandleTap(screen);
            }
            if ((Demo || Autoplay) && !Finished && Time.time > introUntil) AutoStep(dt);
            if (!Finished) FlushQueued();
            FlushHammered();
            CheckRush(dt);

            for (int i = moving.Count - 1; i >= 0; i--)
            {
                if (i >= moving.Count) continue;
                var car = moving[i];
                if (car.state == State.Merging) Merge(car, dt);
                else if (car.state == State.Loop) Drive(car, dt);
            }
            CheckEnd(dt);

            // the sign counts queued sends too: they already have their place on the loop
            int active = ActiveCount + QueuedTotal;
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
            int index = Mathf.Max(0, Mathf.RoundToInt((laneHead - p.z) / LaneGap));
            if (Targeting == Booster.Hand)
            {
                if (index >= queued[lane] && index < lanes[lane].Count) Hand(lane, index);
                return;
            }
            // Only the next block in a lane can go: a tap on one further back says no instead of sending the front one.
            if (index > queued[lane] && index < lanes[lane].Count)
            {
                var car = lanes[lane][index];
                car.view.Express(Face.Worried, 1f);
                Wiggle(car.view.transform);
                if (!Demo)
                {
                    Sfx.Play("blocked", 0.6f);
                    Haptics.Play(Haptic.Warning);
                }
                NotNext?.Invoke();
                return;
            }
            Request(lane);
        }

        int QueuedTotal
        {
            get
            {
                int n = 0;
                foreach (int q in queued) n += q;
                return n;
            }
        }

        /// <summary>
        /// A tap on a lane. Quick taps are not lost: while the previous block is still pulling out, the next send
        /// waits its turn. A tap that cannot be served (the loop is full) is refused with a shake and a warning.
        /// </summary>
        void Request(int lane)
        {
            if (lanes[lane].Count <= queued[lane]) return;
            if (RoadFull(QueuedTotal))
            {
                var front = lanes[lane][queued[lane]];
                front.view.Express(Face.Worried, 0.8f);
                Wiggle(front.view.transform);
                return;
            }
            if (queued[lane] == 0 && !LaneBusy(lane))
            {
                Dispatch(lane);
                return;
            }
            // acknowledge the queued tap on the block that will go next
            var next = lanes[lane][queued[lane]];
            queued[lane]++;
            next.view.Express(Face.Surprised, 0.5f);
            next.view.Squash(0.12f);
            Sfx.Play("go", 0.35f, 1.3f);
            Haptics.Play(Haptic.Tap);
        }

        void FlushQueued()
        {
            for (int l = 0; l < lanes.Length; l++)
            {
                if (queued[l] == 0 || LaneBusy(l)) continue;
                queued[l]--;
                if (lanes[l].Count == 0 || !Dispatch(l)) queued[l] = 0;
            }
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

        /// <summary>True (with a warning) when the loop has no room, counting sends that are already queued.</summary>
        bool RoadFull(int reserved = 0)
        {
            if (ActiveCount + reserved < Capacity) return false;
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
            if (RoadFull(QueuedTotal))
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
            ResetLean(car);
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
                // the queue rolls up once the block ahead has pulled away, so the two never touch
                Tween.Run(0.3f, e => { if (tr) tr.localPosition = Vector3.LerpUnclamped(from, to, e); }, Ease.OutCubic, null, 0.22f + k * 0.04f, tr, ("lane", tr));
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
            if (LaneBusy(lane) || queued[lane] > 0 || RoadFull(QueuedTotal)) return;
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
            if (col.Count == 0 || clearing[c] || hammered[c] || crates[c] >= 0) return;
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
                // the front block rolls out as soon as the road below the column is free
                hammered[c] = true;
                animations++;
                FlushHammered();
            }, 0.05f, ht);
        }

        void FlushHammered()
        {
            for (int c = 0; c < Spec.Cols; c++)
            {
                if (!hammered[c]) continue;
                var col = columns[c];
                if (col.Count > 0 && !clearing[c] && !ExitClear(c)) continue;
                hammered[c] = false;
                animations--;
                if (col.Count == 0 || clearing[c]) continue;
                var front = col[0];
                col.RemoveAt(0);
                Eject(front, c);
                Settle(c);
                Reveal(c);
            }
        }

        // ------------------------------------------------------------------ traffic

        float MergeParam(int lane) => ring.BottomAt(laneX[lane] - TurnRadius);

        /// <summary>
        /// Straight up out of the depot to the waiting point short of the road, then (once the loop has a gap) on to the
        /// stop line and a quarter turn left onto the bottom straight.
        /// </summary>
        void Merge(Car car, float dt)
        {
            if (!car.waiting && car.entry >= WaitAt && car.entry < WaitAt + 0.0001f) car.waiting = true;
            if (car.waiting)
            {
                if (!MergeClear(car))
                {
                    car.speed = 0f;
                    Place(car, dt);
                    return;
                }
                car.waiting = false;
                car.entry = WaitAt + 0.0002f;
            }
            float target = car.entry < WaitAt ? Speed : FollowSpeed(car, MergeParam(car.lane) - (Straight + Arc - car.entry));
            car.speed = Mathf.MoveTowards(car.speed, target, dt * 8f);
            float next = car.entry + car.speed * dt;
            car.entry = car.entry < WaitAt ? Mathf.Min(next, WaitAt) : next;
            if (car.entry >= Straight + Arc)
            {
                car.state = State.Loop;
                car.s = MergeParam(car.lane) + (car.entry - Straight - Arc);
                Drive(car, 0f);
                return;
            }
            Place(car, dt);
            Lean(car, dt);
        }

        /// <summary>Body roll in turns and pitch when speeding up or braking, like a toy car on soft springs.</summary>
        void Lean(Car car, float dt)
        {
            if (dt <= 0f) return;
            float yaw = car.view.transform.localEulerAngles.y;
            float turn = Mathf.DeltaAngle(car.yaw, yaw) / dt;
            float accel = (car.speed - car.lastSpeed) / dt;
            car.yaw = yaw;
            car.lastSpeed = car.speed;
            float k = Mathf.Clamp01(dt * 10f);
            car.roll = Mathf.Lerp(car.roll, Mathf.Clamp(turn * 0.045f, -9f, 9f), k);
            car.pitch = Mathf.Lerp(car.pitch, Mathf.Clamp(-accel * 1.2f, -7f, 7f), k);
            car.view.Body.localRotation = Quaternion.Euler(car.pitch, 0f, car.roll);
        }

        void ResetLean(Car car)
        {
            car.yaw = car.view.transform.localEulerAngles.y;
            car.lastSpeed = car.speed;
            car.roll = car.pitch = 0f;
            car.view.Body.localRotation = Quaternion.identity;
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

        /// <summary>A block that has left its waiting point and is committed to turning onto the loop.</summary>
        bool InArc(Car o) => o.state == State.Merging && !o.waiting && o.entry > WaitAt;

        /// <summary>
        /// The stretch of loop a block takes up or has claimed: blocks driving it, blocks turning in from a depot
        /// and blocks rolling out of a column all count, so nothing lands on top of another block.
        /// </summary>
        bool OnRoad(Car o, out float s)
        {
            s = 0f;
            if (o.state == State.Loop) s = o.s;
            else if (InArc(o)) s = MergeParam(o.lane);
            else if (o.state == State.Ejecting) s = o.land;
            else return false;
            return true;
        }

        /// <summary>A block may enter the loop at this point: room ahead, and the next block behind can still brake.</summary>
        bool SpotClear(float at, Car self)
        {
            float behind = 1.2f + 0.3f * pace;
            foreach (var o in moving)
            {
                if (o == self || !OnRoad(o, out float s)) continue;
                if (ring.Ahead(at, s) < CarHalf * 2f + 0.35f) return false;
                if (ring.Ahead(s, at) < behind) return false;
            }
            return true;
        }

        /// <summary>One block turns in at a time, and only where the loop has room.</summary>
        bool MergeClear(Car car)
        {
            foreach (var o in moving)
                if (o != car && InArc(o)) return false;
            return SpotClear(MergeParam(car.lane), car);
        }

        /// <summary>The road below a column is free for its front block to roll out.</summary>
        bool ExitClear(int c) => SpotClear(dockBottom[c], null);

        /// <summary>Car-following: slow down when the block ahead gets close.</summary>
        float FollowSpeed(Car car, float myS)
        {
            float best = float.MaxValue;
            foreach (var o in moving)
            {
                if (o == car || !OnRoad(o, out float os)) continue;
                float gap = ring.Ahead(myS, os) - CarHalf * 2f;
                if (gap > ring.Length - 1.5f) continue; // that block is just behind us
                best = Mathf.Min(best, gap);
            }
            if (best == float.MaxValue) return Speed * pace;
            return Speed * pace * Mathf.Clamp01((best - FollowGap) / (0.6f * pace));
        }

        void Drive(Car car, float dt)
        {
            if (car.hold >= 0)
            {
                // Parked above a full column, waiting for the road below it to clear. If the wait drags on (or the
                // column changed), the block gives up and takes another lap.
                car.held += dt;
                if (dt > 0f && CanJoin(car.hold, car.color) && car.held < 2.5f)
                {
                    if (ExitClear(car.hold)) TryJoin(car, car.hold);
                    else car.speed = 0f;
                    return;
                }
                car.hold = -1;
            }
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
            Lean(car, dt);
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
            if (clearing[c] || crates[c] >= 0 || col.Count == 0) return false;
            var back = col[col.Count - 1];
            return !back.hidden && back.color == color;
        }

        /// <summary>
        /// A block goes home to a column whose back block has its colour. It only turns into an empty column when no
        /// column has its colour at the back, so a colour never splits across two empty columns.
        /// </summary>
        bool CanJoin(int c, int color)
        {
            if (clearing[c] || crates[c] >= 0 || hammered[c]) return false;
            if (columns[c].Count > 0) return HasBack(c, color);
            for (int o = 0; o < Spec.Cols; o++)
                if (HasBack(o, color)) return false;
            return true;
        }

        /// <summary>
        /// The block turns into the column from the top; a full column shoves its front block out. When the road below
        /// a full column is busy, the block stops above the column and waits (true: the block is taken care of).
        /// </summary>
        bool TryJoin(Car car, int c)
        {
            if (!CanJoin(c, car.color)) return false;
            var col = columns[c];
            if (col.Count >= Spec.Slots && !ExitClear(c))
            {
                if (car.hold != c)
                {
                    car.hold = c;
                    car.held = 0f;
                    car.s = dockTop[c];
                    car.speed = 0f;
                    car.view.transform.localPosition = ring.Evaluate(car.s, out _) + Vector3.up * 0.02f;
                    car.view.Body.localPosition = Vector3.zero;
                }
                return true;
            }
            car.hold = -1;
            moving.Remove(car);
            car.state = State.Parking;
            col.Add(car);
            var tr = car.view.transform;
            car.view.Body.localPosition = Vector3.zero;
            car.view.Body.localRotation = Quaternion.identity;
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
            car.land = dockBottom[c];
            car.hold = -1;
            moving.Add(car);
            var tr = car.view.transform;
            Vector3 from = tr.localPosition;
            Vector3 to = new Vector3(ColX(c), 0.02f, ring.Center.z - ring.HalfH);
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
                ResetLean(car);
            }, 0.06f, tr, ("move", tr));
            if (!Demo) Sfx.Play("go", 0.45f, 1.2f, 0, 0.08f);
        }

        /// <summary>Mystery blocks show their colour once they reach either end of the column.</summary>
        void Reveal(int c)
        {
            var col = columns[c];
            if (col.Count == 0 || crates[c] >= 0) return;
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
            if (clearing[c] || crates[c] >= 0 || col.Count != Spec.Slots) return;
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
                    OpenCrates(cars[0].color);
                }, 0.25f + (cars.Length - 1 - k) * 0.07f, tr, ("clear", tr));
            }
        }

        /// <summary>Clearing a column opens every crate marked with its colour: the crate lifts off and the blocks inside drop in.</summary>
        void OpenCrates(int color)
        {
            for (int c = 0; c < Spec.Cols; c++)
            {
                if (crates[c] != color) continue;
                int col = c;
                crates[c] = -1;
                var view = crateViews[c];
                crateViews[c] = null;
                animations++;
                if (!Demo)
                {
                    Sfx.Play("complete", 0.6f, 1.25f);
                    Haptics.Play(Haptic.Reveal);
                }
                Fx.Burst(transform.TransformPoint(SlotPos(c, Spec.Slots / 2) + Vector3.up * 0.9f), new Color(0.55f, 0.6f, 1f), 14, 3f);
                Vector3 from = view.localPosition;
                Tween.Run(0.45f, e =>
                {
                    if (!view) return;
                    view.localPosition = from + Vector3.up * (e * 3f);
                    view.localScale = new Vector3(1f + e * 0.2f, Mathf.Max(0.01f, 1f - e), 1f + e * 0.2f);
                }, Ease.InCubic, () => { if (view) Destroy(view.gameObject); }, 0f, view);
                var cars = columns[c];
                for (int k = 0; k < cars.Count; k++)
                {
                    cars[k].view.gameObject.SetActive(true);
                    DropIn(cars[k].view.transform, SlotPos(c, k), 0.25f + (cars.Count - 1 - k) * 0.05f);
                }
                Tween.Delay(0.9f, () =>
                {
                    animations--;
                    Reveal(col);
                    CheckSorted(col);
                }, this);
                CrateOpened?.Invoke();
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
            // with nothing that can park, the bot keeps a free slot for a while, then fills the loop like a player would
            autoStall = good.Count > 0 ? 0f : autoStall + autoTimer;
            if (good.Count > 0) Dispatch(good[autoRng.Next(good.Count)]);
            else if (any.Count > 0 && (ActiveCount <= Capacity - 2 || autoStall > 4f)) Dispatch(any[autoRng.Next(any.Count)]);
        }

        /// <summary>One-line snapshot for test logs.</summary>
        public string Describe()
        {
            var sb = new System.Text.StringBuilder();
            sb.Append("cap=").Append(Capacity).Append(" active=").Append(ActiveCount).Append(" anim=").Append(animations).Append(" cols=");
            for (int c = 0; c < Spec.Cols; c++)
            {
                sb.Append(crates[c] >= 0 ? "[crate" + crates[c] + ":" : "[");
                foreach (var car in columns[c]) sb.Append(car.color);
                sb.Append(']');
            }
            sb.Append(" lanes=");
            foreach (var l in lanes) sb.Append(l.Count).Append('/');
            sb.Append(" moving=");
            foreach (var car in moving) sb.Append(car.color).Append(':').Append(car.state).Append(car.hold >= 0 ? "@hold" + car.hold : "").Append(' ');
            return sb.ToString();
        }

        void OnDestroy() => Fx.Clear();
    }
}
