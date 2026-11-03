using System;
using System.Collections.Generic;

namespace Parkology
{
    /// <summary>
    /// One puzzle. Each column is a queue of blocks: blocks join at the back (top) and leave from the front (bottom).
    /// A block driving the loop joins a column whose back block has its colour (or an empty column). If the column is
    /// full, its front block is pushed out onto the loop. A column filled with a single colour clears.
    /// </summary>
    public sealed class LevelSpec
    {
        public int Number, Cols, Slots, Capacity, Colors;
        public bool Hard;
        public int[,] Grid;       // [col, slot]; slot 0 = front (bottom); -1 = empty
        public bool[,] Hidden;    // mystery blocks show their colour once they reach either end of a column
        public List<bool>[] LaneHidden; // mystery blocks in depots show their colour at the front of the lane
        public List<int>[] Lanes; // index 0 = the block at the front of the depot lane
        public int[] Crates;      // per column: colour whose clearing opens the crate sealing it; -1 = open (null = none)
        public LevelLayout Layout;
        public float WinRate;     // share of random play-outs that cleared the board (difficulty measure)
        public int Reward => 20 + Number / 3 * 2;

        public int BlockCount
        {
            get
            {
                int n = 0;
                foreach (int c in Grid) if (c >= 0) n++;
                foreach (var l in Lanes) n += l.Count;
                return n;
            }
        }
    }

    /// <summary>
    /// How a level starts. Classic: every column full. Open: some columns start part-empty and more blocks wait in
    /// the depots. Crate: a column is sealed under a crate until a column of the crate's colour clears.
    /// </summary>
    public enum LevelLayout { Classic, Open, Crate }

    public static class LevelGenerator
    {
        public const int Capacity = 5;
        /// <summary>The first levels played in one go before the home screen.</summary>
        public const int OnboardingLevels = 5;
        /// <summary>First level with a crate; open lots start two levels earlier.</summary>
        public const int FirstCrateLevel = 9;

        /// <summary>The lot grows while the player learns: 3x3 (levels 1-2), 4x4 (3-4), then the full 5x5.</summary>
        public static int Size(int number) => number <= 2 ? 3 : number <= 4 ? 4 : 5;

        /// <summary>After the warm-up the layouts take turns, so consecutive levels feel different.</summary>
        public static LevelLayout LayoutOf(int number)
        {
            if (number >= FirstCrateLevel && number % 4 == 1) return LevelLayout.Crate;
            if (number >= FirstCrateLevel - 2 && number % 3 == 1) return LevelLayout.Open;
            return LevelLayout.Classic;
        }

        public static LevelSpec Create(int number)
        {
            Params(number, out int groups, out int colors, out float mix, out float hidden);
            int size = Size(number), laneCount = size == 3 ? 2 : 3;
            var layout = LayoutOf(number);
            var rng = new System.Random(number * 7919 + 17);
            // Backward construction guarantees a solution. Random play-outs rate how forgiving a candidate is;
            // early levels keep the most forgiving of many candidates, later ones of a few.
            int candidates = number <= 3 ? 20 : 10;
            // early levels are forgiving; later ones aim for boards most random play-outs lose
            float target = number <= 5 ? 1f : Math.Max(0.3f, 0.95f - (number - 5) * 0.025f);
            // crates seal a column for a while, so those boards aim a little more forgiving
            if (layout == LevelLayout.Crate) target = Math.Min(1f, target + 0.15f);
            LevelSpec best = null;
            for (int attempt = 0; attempt < 400 && candidates > 0; attempt++)
            {
                var spec = Build(number, size, size, laneCount, groups, colors, mix, layout == LevelLayout.Open, rng);
                if (spec == null) continue;
                spec.Layout = layout;
                // A crate changes the order blocks can be played in, so its board must prove itself: at least one
                // play-out has to clear it.
                if (layout == LevelLayout.Crate && !AddCrates(spec, number >= 25 ? 2 : 1, rng)) continue;
                spec.WinRate = Solver.WinRate(spec, 40, rng);
                if (layout == LevelLayout.Crate && spec.WinRate <= 0f) continue;
                candidates--;
                if (best == null || Math.Abs(spec.WinRate - target) < Math.Abs(best.WinRate - target)) best = spec;
            }
            if (best == null) throw new System.InvalidOperationException("Could not generate level " + number);
            best.LaneHidden = new List<bool>[best.Lanes.Length];
            for (int l = 0; l < best.Lanes.Length; l++) best.LaneHidden[l] = new List<bool>(new bool[best.Lanes[l].Count]);
            HideBlocks(best, hidden, rng);
            return best;
        }

        /// <summary>Difficulty curve: more blocks in the depots, more colours, more mixing, mystery blocks.</summary>
        static void Params(int n, out int groups, out int colors, out float mix, out float hidden)
        {
            // groups = columns + the groups waiting in the depots; the small lots get one or two waiting groups
            int[] early = { 0, 4, 5, 5, 6 };  // 3x3, 3x3, 4x4, 4x4
            int[] earlyColors = { 0, 3, 3, 4, 4 };
            groups = n < early.Length ? early[n] : System.Math.Min(10, 6 + n / 2); // 5x5: 6 groups = 25 parked + 5 waiting
            colors = n < early.Length ? earlyColors[n] : System.Math.Min(8, System.Math.Min(groups, 3 + n / 2));
            mix = System.Math.Min(0.75f, 0.3f + n * 0.04f);  // chance a loose block is tucked into a column front
            hidden = n < 4 ? 0f : System.Math.Min(0.25f, 0.08f + 0.015f * (n - 4));
        }

        /// <summary>
        /// Builds a level by playing the rules backwards from the cleared board, so a solution always exists:
        /// un-clearing fills an empty column with one colour; un-joining takes the back block out of a full column and
        /// tucks a loose block in at its front (only where the new back block shares the colour); un-sending puts a
        /// loose block back at the front of a depot lane. The loop never holds more than the capacity.
        /// </summary>
        static LevelSpec Build(int number, int colCount, int slots, int laneCount, int groups, int colors, float mix, bool open, System.Random rng)
        {
            var groupColors = new List<int>();
            for (int g = 0; g < groups; g++) groupColors.Add(g < colors ? g : rng.Next(colors));
            Shuffle(groupColors, rng);
            int next = 0;
            var cols = new List<int>[colCount];
            for (int c = 0; c < colCount; c++)
            {
                cols[c] = new List<int>();
                for (int k = 0; k < slots; k++) cols[c].Add(groupColors[next]);
                next++;
            }
            var loop = new List<int>();
            var lanes = new List<int>[laneCount];
            for (int l = 0; l < laneCount; l++) lanes[l] = new List<int>();

            bool Sorted(List<int> col) => col.Count == slots && col.TrueForAll(x => x == col[0]);
            bool CanUnjoin(List<int> col) => col.Count == slots && col[slots - 1] == col[slots - 2];

            // Open lot: in the last round some blocks are also un-joined without a push (taken off the back of a
            // column onto the loop). Played forwards they simply park, so columns start part-empty and the board
            // stays solvable. At most one column ends up empty.
            int opened = 0, openTarget = open ? slots + rng.Next(slots) : 0;
            bool OpenOne()
            {
                var options = new List<int>();
                for (int c = 0; c < colCount; c++)
                {
                    var col = cols[c];
                    if (col.Count == 0) continue;
                    int color = col[col.Count - 1];
                    if (col.Count > 1) { if (col[col.Count - 2] == color) options.Add(c); continue; }
                    // emptying a column: forwards the block may only turn into an empty column when no column has
                    // its colour at the back
                    bool ok = true;
                    foreach (var o in cols)
                        if (o != col && (o.Count == 0 || o[o.Count - 1] == color)) ok = false;
                    if (ok) options.Add(c);
                }
                if (options.Count == 0) return false;
                var from = cols[options[rng.Next(options.Count)]];
                loop.Add(from[from.Count - 1]);
                from.RemoveAt(from.Count - 1);
                opened++;
                return true;
            }

            // Spreads the loose blocks: tuck into column fronts (un-join with push) or put back into depots (un-send).
            // Before the last round the freshly un-cleared column stays sorted, ready for the next round.
            void Distribute(int maxSteps, bool last, int fresh)
            {
                for (int step = 0; step < maxSteps && loop.Count > 0; step++)
                {
                    if (last && opened < openTarget && loop.Count < Capacity && rng.NextDouble() < 0.5 && OpenOne()) continue;
                    int i = rng.Next(loop.Count);
                    var targets = new List<int>();
                    for (int c = 0; c < colCount; c++)
                        if (CanUnjoin(cols[c]) && cols[c][0] != loop[i] && (last || c != fresh)) targets.Add(c);
                    // in the last round, columns that are still one colour take a block first so none starts sorted
                    var sortedTargets = last ? targets.FindAll(c => Sorted(cols[c])) : new List<int>();
                    bool tuck = sortedTargets.Count > 0 || (targets.Count > 0 && rng.NextDouble() < mix);
                    if (sortedTargets.Count > 0) targets = sortedTargets;
                    if (tuck)
                    {
                        var col = cols[targets[rng.Next(targets.Count)]];
                        int back = col[slots - 1];
                        col.RemoveAt(slots - 1);
                        col.Insert(0, loop[i]);
                        loop[i] = back;
                    }
                    else
                    {
                        lanes[rng.Next(laneCount)].Insert(0, loop[i]);
                        loop.RemoveAt(i);
                    }
                }
                while (loop.Count > 0)
                {
                    lanes[rng.Next(laneCount)].Insert(0, loop[0]);
                    loop.RemoveAt(0);
                }
            }

            // Each extra group: a sorted column empties onto the loop (un-joins into an empty column), the column is
            // un-cleared with the new group, and the loose blocks are spread into column fronts and depots.
            while (next < groups)
            {
                var sorted = new List<int>();
                for (int c = 0; c < colCount; c++) if (Sorted(cols[c])) sorted.Add(c);
                if (sorted.Count == 0) return null;
                int fresh = sorted[rng.Next(sorted.Count)];
                var col = cols[fresh];
                loop.AddRange(col);
                col.Clear();
                for (int k = 0; k < slots; k++) col.Add(groupColors[next]);
                next++;
                Distribute(60, next == groups, fresh);
            }
            foreach (var col in cols)
            {
                if ((!open && col.Count != slots) || Sorted(col)) return null;
            }
            // an open lot needs real gaps; boards that barely open are dropped
            if (open && opened < slots - 1) return null;

            var spec = new LevelSpec { Number = number, Cols = colCount, Slots = slots, Capacity = Capacity, Colors = colors };
            spec.Grid = new int[colCount, slots];
            spec.Hidden = new bool[colCount, slots];
            for (int c = 0; c < colCount; c++)
                for (int k = 0; k < slots; k++) spec.Grid[c, k] = k < cols[c].Count ? cols[c][k] : -1;
            spec.Lanes = lanes;
            return spec;
        }

        /// <summary>
        /// Seals columns under crates. Each crate's key is a colour that can be cleared from outside it (it has at least
        /// a full column's worth of blocks elsewhere on the board).
        /// </summary>
        static bool AddCrates(LevelSpec spec, int count, System.Random rng)
        {
            spec.Crates = new int[spec.Cols];
            for (int c = 0; c < spec.Cols; c++) spec.Crates[c] = -1;
            var order = new List<int>();
            for (int c = 0; c < spec.Cols; c++) order.Add(c);
            Shuffle(order, rng);
            int placed = 0;
            foreach (int c in order)
            {
                if (placed == count) break;
                if (spec.Grid[c, spec.Slots - 1] < 0) continue; // only full columns get crated
                var outside = new int[spec.Colors];
                for (int o = 0; o < spec.Cols; o++)
                {
                    if (o == c || spec.Crates[o] >= 0) continue;
                    for (int k = 0; k < spec.Slots; k++) if (spec.Grid[o, k] >= 0) outside[spec.Grid[o, k]]++;
                }
                foreach (var lane in spec.Lanes) foreach (int color in lane) outside[color]++;
                var keys = new List<int>();
                for (int color = 0; color < spec.Colors; color++) if (outside[color] >= spec.Slots) keys.Add(color);
                if (keys.Count == 0) continue;
                spec.Crates[c] = keys[rng.Next(keys.Count)];
                placed++;
            }
            return placed == count;
        }

        static void HideBlocks(LevelSpec spec, float ratio, System.Random rng)
        {
            if (ratio <= 0f) return;
            var candidates = new List<(int, int)>();
            for (int c = 0; c < spec.Cols; c++)
            {
                int filled = 0;
                while (filled < spec.Slots && spec.Grid[c, filled] >= 0) filled++;
                for (int k = 1; k < filled - 1; k++) candidates.Add((c, k)); // never the ends
            }
            Shuffle(candidates, rng);
            int count = (int)System.Math.Round(candidates.Count * ratio * spec.Slots / Math.Max(1, spec.Slots - 2));
            for (int i = 0; i < count && i < candidates.Count; i++) spec.Hidden[candidates[i].Item1, candidates[i].Item2] = true;
            // From level 8 some waiting blocks are mysteries too (never the one at the front of a lane).
            if (spec.Number < 8) return;
            float laneRatio = System.Math.Min(0.6f, 0.25f + 0.03f * (spec.Number - 8));
            for (int l = 0; l < spec.Lanes.Length; l++)
                for (int k = 1; k < spec.Lanes[l].Count; k++)
                    spec.LaneHidden[l][k] = rng.NextDouble() < laneRatio;
        }

        public static void Shuffle<T>(IList<T> list, System.Random rng)
        {
            for (int i = list.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (list[i], list[j]) = (list[j], list[i]);
            }
        }
    }

    /// <summary>Discrete model of the queue rules, used to grade generated levels and to drive autoplay.</summary>
    public static class Solver
    {
        /// <summary>
        /// A block joins a column whose back block has its colour. It only turns into an empty column when no column
        /// has its colour at the back, so one colour never splits across two empty columns.
        /// </summary>
        public static bool CanJoin(List<int>[] cols, int c, int color, int[] crates = null)
        {
            if (crates != null && crates[c] >= 0) return false;
            var column = cols[c];
            if (column.Count > 0) return column[column.Count - 1] == color;
            for (int o = 0; o < cols.Length; o++)
            {
                if (crates != null && crates[o] >= 0) continue;
                if (cols[o].Count > 0 && cols[o][cols[o].Count - 1] == color) return false;
            }
            return true;
        }

        /// <summary>Share of random play-outs that clear the board.</summary>
        public static float WinRate(LevelSpec spec, int runs, System.Random rng)
        {
            int wins = 0;
            for (int i = 0; i < runs; i++) if (Rollout(spec, rng)) wins++;
            return wins / (float)runs;
        }

        static bool Rollout(LevelSpec spec, System.Random rng)
        {
            int slots = spec.Slots;
            var cols = new List<int>[spec.Cols];
            for (int c = 0; c < spec.Cols; c++)
            {
                cols[c] = new List<int>();
                for (int k = 0; k < slots; k++) if (spec.Grid[c, k] >= 0) cols[c].Add(spec.Grid[c, k]);
            }
            var lanes = new List<int>[spec.Lanes.Length];
            for (int l = 0; l < lanes.Length; l++) lanes[l] = new List<int>(spec.Lanes[l]);
            var crates = spec.Crates != null ? (int[])spec.Crates.Clone() : null;
            var loop = new List<int>();
            var moves = new List<(int car, int col)>();
            for (int step = 0; step < 600; step++)
            {
                bool empty = loop.Count == 0;
                foreach (var c in cols) if (c.Count > 0) empty = false;
                foreach (var l in lanes) if (l.Count > 0) empty = false;
                if (empty) return true;

                // blocks on the loop join columns as they drive past (order is up to traffic: pick at random)
                moves.Clear();
                for (int i = 0; i < loop.Count; i++)
                    for (int c = 0; c < cols.Length; c++)
                        if (CanJoin(cols, c, loop[i], crates)) moves.Add((i, c));
                if (moves.Count > 0)
                {
                    var (car, col) = moves[rng.Next(moves.Count)];
                    int color = loop[car];
                    loop.RemoveAt(car);
                    // clearing a column opens the crates marked with its colour
                    if (Join(cols[col], color, slots, loop) && crates != null)
                        for (int c = 0; c < crates.Length; c++) if (crates[c] == color) crates[c] = -1;
                    continue;
                }
                if (loop.Count >= spec.Capacity) return false;
                // send a depot block, preferring one that can park somewhere
                int lane = PickLane(cols, lanes, slots, rng, crates);
                if (lane < 0) return false;
                loop.Add(lanes[lane][0]);
                lanes[lane].RemoveAt(0);
            }
            return false;
        }

        /// <summary>
        /// Adds a block at the back; a full column pushes its front block onto the loop; a sorted full column clears
        /// (true).
        /// </summary>
        public static bool Join(List<int> column, int color, int slots, List<int> loop)
        {
            column.Add(color);
            if (column.Count > slots)
            {
                loop.Add(column[0]);
                column.RemoveAt(0);
            }
            if (column.Count != slots || !column.TrueForAll(x => x == color)) return false;
            column.Clear();
            return true;
        }

        public static int PickLane(List<int>[] cols, List<int>[] lanes, int slots, System.Random rng, int[] crates = null)
        {
            var good = new List<int>();
            var any = new List<int>();
            for (int l = 0; l < lanes.Length; l++)
            {
                if (lanes[l].Count == 0) continue;
                any.Add(l);
                for (int c = 0; c < cols.Length; c++)
                    if (CanJoin(cols, c, lanes[l][0], crates)) { good.Add(l); break; }
            }
            if (good.Count > 0) return good[rng.Next(good.Count)];
            return any.Count > 0 ? any[rng.Next(any.Count)] : -1;
        }
    }
}
