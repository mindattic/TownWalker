using System.Linq;
using Lab.Core;
using NUnit.Framework;

namespace Lab.Tests
{
    [TestFixture]
    public class LoopTests
    {
        [Test]
        public void A_seed_always_builds_the_same_region_with_every_kind_of_site()
        {
            var a = WorldGen.Create(42); var b = WorldGen.Create(42);
            Assert.That(b.Sites.Select(s => (s.Kind, s.Pos.X, s.Pos.Y)), Is.EqualTo(a.Sites.Select(s => (s.Kind, s.Pos.X, s.Pos.Y))));
            Assert.That(a.Sites.Count, Is.GreaterThanOrEqualTo(20));
            Assert.That(a.Sites, Has.Some.Matches<Site>(s => s.Kind == SiteKind.FuelDepot));
            Assert.That(a.Sites, Has.Some.Matches<Site>(s => s.Kind == SiteKind.Mine));
        }

        [Test]
        public void Scanning_costs_fuel_and_reveals_sites_in_radar_range()
        {
            var l = new LabSession(7);
            float fuel = l.Town.Fuel;
            int hidden = l.World.Sites.Count(s => !s.Revealed);
            int found = l.Scan();
            Assert.That(l.Town.Fuel, Is.EqualTo(fuel - l.Town.ScanCost).Within(0.0005f));
            Assert.That(l.World.Sites.Count(s => !s.Revealed), Is.EqualTo(hidden - found));
            Assert.That(l.World.Sites.Where(s => s.Revealed), Has.All.Matches<Site>(s => V2.Distance(s.Pos, l.Town.Pos) <= l.Town.ScanRadius + 0.01f));
        }

        [Test]
        public void A_holding_town_stays_put_and_burns_nothing_until_released()
        {
            var l = new LabSession(3);
            var start = l.Town.Pos; float fuel = l.Town.Fuel;
            l.WalkTo(start + new V2(20, 0));
            l.Holding = true;
            for (int i = 0; i < 20; i++) l.Tick(0.05f);
            Assert.That(l.Phase, Is.EqualTo(LoopPhase.Walking));
            Assert.That(V2.Distance(l.Town.Pos, start), Is.EqualTo(0f));
            Assert.That(l.Town.Fuel, Is.EqualTo(fuel));
            l.Holding = false;
            l.Tick(0.05f);
            Assert.That(V2.Distance(l.Town.Pos, start), Is.GreaterThan(0f));
        }

        [Test]
        public void Walking_burns_fuel_and_a_dry_town_is_stranded()
        {
            var l = new LabSession(3);
            l.Town.Fuel = 5f;
            l.WalkTo(new V2(110, 10));
            for (int i = 0; i < 2000 && l.Phase == LoopPhase.Walking; i++) l.Tick(0.05f);
            Assert.That(l.Town.Stranded, Is.True);
            Assert.That(l.Phase, Is.EqualTo(LoopPhase.Exploring));
            float moved = V2.Distance(l.Town.Pos, l.World.Start);
            Assert.That(moved, Is.EqualTo(5f / l.Town.FuelPerUnit).Within(0.05f));
        }

        [Test]
        public void The_full_loop_scan_send_battle_haul_records_time_and_taps_to_battle()
        {
            var l = new LabSession(11);
            l.Tap(); l.Scan();
            var site = l.World.Sites.Where(s => s.Revealed).OrderBy(s => V2.Distance(s.Pos, l.Town.Pos)).First();
            if (!l.InBikeRange(site)) { l.WalkTo(site.Pos); for (int i = 0; i < 4000 && l.Phase == LoopPhase.Walking; i++) l.Tick(0.05f); }
            l.Tap();
            var ex = l.Send(site);
            Assert.That(ex, Is.Not.Null);
            l.BeginBattle();
            float fuel = l.Town.Fuel;
            l.EndBattle(true);
            Assert.That(site.Cleared, Is.True);
            Assert.That(l.Town.Fuel, Is.GreaterThanOrEqualTo(System.Math.Min(l.Town.FuelMax, fuel + site.Fuel) - 0.01f));
            Assert.That(l.TimeToBattle, Has.Exactly(1).Items);
            Assert.That(l.TapsToBattle[0], Is.EqualTo(2));
        }

        [Test]
        public void The_route_graph_connects_the_start_to_every_site()
        {
            var w = WorldGen.Create(5);
            var g = RouteGraph.Build(w, w.Start);
            int reachable = w.Sites.Count(s => g.Path(0, s.Id) != null);
            Assert.That(reachable, Is.GreaterThanOrEqualTo(w.Sites.Count - 1), $"{reachable}/{w.Sites.Count}");
        }

        [Test]
        public void Hex_round_trips_points_and_measures_distance()
        {
            var h = new Hex(3, -2);
            Assert.That(Hex.FromPoint(h.Center(5f), 5f), Is.EqualTo(h));
            Assert.That(new Hex(0, 0).Distance(new Hex(3, -2)), Is.EqualTo(3));
            Assert.That(Hex.FromKey(h.Key), Is.EqualTo(h));
        }
    }
}
