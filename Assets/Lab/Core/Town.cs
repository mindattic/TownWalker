using System;
using System.Collections.Generic;

namespace Lab.Core
{
    /// <summary>
    /// The walking town (owner direction 2026-10-10): a hub on four legs. It burns fuel to walk and to run its radar;
    /// it sends the ground team out by bike to sites in range. Every traversal demo drives this same model.
    /// </summary>
    public sealed class WalkerTown
    {
        public V2 Pos;
        public float Fuel = 60f, FuelMax = 100f;
        /// <summary>World units per second while walking (a lumbering stride).</summary>
        public float Speed = 4.5f;
        public float FuelPerUnit = 0.22f;
        public float ScanRadius = 30f, ScanCost = 3f;
        /// <summary>How far the ground team's bike reaches from the town.</summary>
        public float BikeRange = 28f;
        public int Ore, Scrap;
        public bool Stranded => Fuel <= 0.01f;
    }

    public enum LoopPhase { Exploring, Walking, Riding, Battle, Hauling }

    /// <summary>The one loop every demo plays: scan → choose → walk and/or send → battle → haul. Records the owner's metrics.</summary>
    public sealed class Expedition
    {
        public Site Site;
        public int RoadFights;
        public float RideSeconds;
        public override string ToString() => $"{Site?.Name} — {RoadFights} road fights, {Site?.Waves} waves{(Site != null && Site.HasBoss ? " + boss" : "")}";
    }

    public sealed class LabSession
    {
        public readonly World World;
        public readonly WalkerTown Town = new WalkerTown();
        public LoopPhase Phase = LoopPhase.Exploring;
        public V2? Destination;
        public Expedition Current;

        // Owner's metrics: is traversal a quick beat between battles?
        public int Taps, Scans, BattlesWon;
        public float Clock, LastBattleEndedAt;
        public readonly List<float> TimeToBattle = new List<float>();
        public readonly List<int> TapsToBattle = new List<int>();
        private int tapsAtLastBattle;

        public LabSession(int seed) : this(WorldGen.Create(seed)) { }

        public LabSession(World world)
        {
            World = world;
            Town.Pos = world.Start;
            Reveal(Town.Pos, 18f); // you always see your immediate surroundings
        }

        public void Tap() => Taps++;

        public int Reveal(V2 at, float radius)
        {
            int n = 0;
            foreach (var s in World.Sites)
                if (!s.Revealed && V2.Distance(s.Pos, at) <= radius) { s.Revealed = true; n++; }
            return n;
        }

        /// <summary>Radar ping around the town: costs fuel, reveals sites in range. Returns how many were found.</summary>
        public int Scan()
        {
            if (Town.Fuel < Town.ScanCost) return 0;
            Town.Fuel -= Town.ScanCost;
            Scans++;
            return Reveal(Town.Pos, Town.ScanRadius);
        }

        public bool InBikeRange(Site s) => V2.Distance(Town.Pos, s.Pos) <= Town.BikeRange;

        /// <summary>Set by a view while its town turns in place before setting off: still Walking, but not moving.</summary>
        public bool Holding;

        public void WalkTo(V2 target)
        {
            if (Town.Stranded) return;
            Destination = target;
            Phase = LoopPhase.Walking;
        }

        /// <summary>Advance the town; returns true the frame it arrives (or runs dry).</summary>
        public bool Tick(float dt)
        {
            Clock += dt;
            if (Phase != LoopPhase.Walking || Destination == null || Holding) return false;
            var to = Destination.Value;
            var d = to - Town.Pos;
            float step = Town.Speed * dt;
            if (d.Length <= step) step = d.Length;
            float fuelNeed = step * Town.FuelPerUnit;
            if (fuelNeed > Town.Fuel) { step = Town.Fuel / Town.FuelPerUnit; fuelNeed = Town.Fuel; }
            Town.Pos = Town.Pos + d.Normalized * step;
            Town.Fuel = Math.Max(0f, Town.Fuel - fuelNeed);
            Reveal(Town.Pos, 18f);
            if (V2.Distance(Town.Pos, to) < 0.05f || Town.Stranded)
            {
                Destination = null;
                Phase = LoopPhase.Exploring;
                return true;
            }
            return false;
        }

        /// <summary>Send the ground team by bike: road fights scale with distance; the site's waves (and boss) follow.</summary>
        public Expedition Send(Site s)
        {
            if (s == null || s.Cleared || !InBikeRange(s)) return null;
            float dist = V2.Distance(Town.Pos, s.Pos);
            Current = new Expedition { Site = s, RoadFights = Math.Min(2, (int)(dist / 11f)), RideSeconds = 0.6f + dist / 22f };
            Phase = LoopPhase.Riding;
            return Current;
        }

        /// <summary>The placeholder battle starts.</summary>
        public void BeginBattle()
        {
            Phase = LoopPhase.Battle;
            TimeToBattle.Add(Clock - LastBattleEndedAt);
            TapsToBattle.Add(Taps - tapsAtLastBattle);
        }

        /// <summary>Win: haul the site's resources home (fuel capped by the tanks). Lose: the team limps back empty-handed.</summary>
        public void EndBattle(bool won)
        {
            var s = Current?.Site;
            if (won && s != null)
            {
                s.Cleared = true;
                Town.Fuel = Math.Min(Town.FuelMax, Town.Fuel + s.Fuel);
                Town.Ore += s.Ore;
                Town.Scrap += s.Scrap;
                BattlesWon++;
            }
            Phase = LoopPhase.Exploring;
            Current = null;
            LastBattleEndedAt = Clock;
            tapsAtLastBattle = Taps;
        }

        public float AverageTimeToBattle => TimeToBattle.Count == 0 ? 0f : Sum(TimeToBattle) / TimeToBattle.Count;
        public float AverageTapsToBattle => TapsToBattle.Count == 0 ? 0f : (float)SumI(TapsToBattle) / TapsToBattle.Count;
        private static float Sum(List<float> l) { float s = 0; foreach (var v in l) s += v; return s; }
        private static int SumI(List<int> l) { int s = 0; foreach (var v in l) s += v; return s; }
    }
}
