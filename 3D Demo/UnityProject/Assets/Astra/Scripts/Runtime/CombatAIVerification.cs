using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace AstraCabin
{
    public sealed partial class CombatVerification
    {
        CombatSimulation EnemyScenario(CombatSimulation.PilotStyle pilot, Vector2 enemyPosition, float heading)
        {
            var s = new CombatSimulation(); s.Start(true); s.enemies.Clear();
            int kind = pilot == CombatSimulation.PilotStyle.Commander ? 2 : pilot == CombatSimulation.PilotStyle.Gunship ? 1 : 0;
            s.enemies.Add(new CombatSimulation.Enemy { id = 0, kind = kind, pilot = pilot, position = enemyPosition, angle = heading,
                velocity = CombatSimulation.Direction(heading) * 230, hp = 780, maxHp = 780,
                observedPosition = s.position, observedVelocity = s.velocity, observedHeading = s.angle, timer = 1.4f });
            return s;
        }
        void TestEnemyPilots()
        {
            TestFlightBalance();
            var trajectories = new List<string> { "pilot,time,player_x,player_y,enemy_x,enemy_y,state,distance" };
            foreach (var pilot in new[] { CombatSimulation.PilotStyle.TailHunter, CombatSimulation.PilotStyle.Slasher, CombatSimulation.PilotStyle.Flanker, CombatSimulation.PilotStyle.Gunship, CombatSimulation.PilotStyle.Commander })
            {
                var s = EnemyScenario(pilot, new Vector2(600, 0), 180); var e = s.enemies[0];
                float minDistance = float.MaxValue; bool bounded = true, forward = true; int shots = 0, engaged = 0, escaping = 0;
                var states = new HashSet<CombatSimulation.Maneuver>();
                for (int i = 0; i < 120 * 30; i++)
                {
                    s.Step(1f / 120, new CombatSimulation.Controls { turn = i < 240 ? 0 : .28f, brake = i > 240 });
                    float distance = Vector2.Distance(s.position, e.position);
                    if (i < 240) minDistance = Mathf.Min(minDistance, distance);
                    states.Add(e.maneuver);
                    if (e.maneuver == CombatSimulation.Maneuver.Attack || e.maneuver == CombatSimulation.Maneuver.Break) engaged++;
                    if (e.maneuver == CombatSimulation.Maneuver.Extend) escaping++;
                    bounded &= e.position.magnitude < CombatSimulation.ArenaRadius && !float.IsNaN(e.position.x);
                    forward &= Vector2.Dot(e.velocity, CombatSimulation.Direction(e.angle)) > -1;
                    foreach (var bullet in s.bullets) if (bullet.hostile) shots++;
                    if (i % 12 == 0) trajectories.Add(string.Format(System.Globalization.CultureInfo.InvariantCulture, "{0},{1:F2},{2:F1},{3:F1},{4:F1},{5:F1},{6},{7:F1}", pilot, s.elapsed, s.position.x, s.position.y, e.position.x, e.position.y, e.maneuver, distance));
                    s.cues.Clear();
                }
                Check(pilot + " avoids initial head-on collision", minDistance > e.Radius + 16, "minimum separation=" + minDistance.ToString("F1"));
                Check(pilot + " spends most of the fight engaging", engaged > 120 * 30 * .55f && e.passes >= 1, "engaged=" + (engaged / 36f).ToString("F1") + "% escape=" + (escaping / 36f).ToString("F1") + "%");
                Check(pilot + " remains bounded and flies forwards", bounded && forward);
                Check(pilot + " still creates firing opportunities", shots > 0);
            }
            File.WriteAllLines(Path.Combine(output, "ai-trajectories.csv"), trajectories);
            var defensive = EnemyScenario(CombatSimulation.PilotStyle.TailHunter, new Vector2(280, 0), 0);
            defensive.Step(1f / 120, new CombatSimulation.Controls());
            Check("Tail threat triggers a committed defensive break", defensive.enemies[0].maneuver == CombatSimulation.Maneuver.Break && defensive.enemies[0].breaks == 1);
            for (int i = 0; i < 45; i++) defensive.Step(1f / 120, new CombatSimulation.Controls());
            Check("Defensive maneuver does not chatter every frame", defensive.enemies[0].breaks == 1);
            bool reversed = false;
            for (int i = 0; i < 720; i++)
            {
                defensive.Step(1f / 120, new CombatSimulation.Controls());
                var e = defensive.enemies[0]; Vector2 delta = defensive.position - e.position;
                reversed |= Vector2.Dot(CombatSimulation.Direction(defensive.angle), -delta.normalized) < -.45f
                    && Vector2.Dot(CombatSimulation.Direction(e.angle), delta.normalized) > .8f;
            }
            Check("Short defensive turn can reverse into pursuit", reversed);
            var pursuit = EnemyScenario(CombatSimulation.PilotStyle.TailHunter, new Vector2(-240, 0), 0);
            int heldTail = 0; bool abandoned = false;
            for (int i = 0; i < 720; i++)
            {
                pursuit.Step(1f / 120, new CombatSimulation.Controls());
                var e = pursuit.enemies[0];
                if (e.maneuver == CombatSimulation.Maneuver.Attack && Vector2.Distance(e.position, pursuit.position) < 350) heldTail++;
                abandoned |= e.maneuver == CombatSimulation.Maneuver.Extend;
            }
            Check("Advantaged fighter holds pursuit beyond old timeout", heldTail > 600 && !abandoned, "tail pressure seconds=" + (heldTail / 120f).ToString("F2"));
            var close = EnemyScenario(CombatSimulation.PilotStyle.TailHunter, new Vector2(-150, 0), 0);
            for (int i = 0; i < 120; i++) close.Step(1f / 120, new CombatSimulation.Controls());
            Check("Close pursuit no longer automatically disengages", close.enemies[0].maneuver == CombatSimulation.Maneuver.Attack);
            var group = new CombatSimulation(); group.Start(); group.enemies.Clear(); group.training = true;
            for (int i = 0; i < 5; i++) group.enemies.Add(new CombatSimulation.Enemy { id = i, kind = 0, pilot = (CombatSimulation.PilotStyle)(i % 3), side = i % 2 == 0 ? 1 : -1,
                position = CombatSimulation.Direction(i * 72) * 500, angle = i * 72 + 180, hp = 65, maxHp = 65, timer = 1.4f });
            int maxAttackers = 0; bool limitedTurns = true; float minimumFriendlyDistance = float.MaxValue;
            for (int i = 0; i < 120 * 20; i++)
            {
                float[] before = group.enemies.ConvertAll(e => e.angle).ToArray();
                group.Step(1f / 120, new CombatSimulation.Controls { brake = true, turn = .22f });
                int attacking = 0;
                for (int j = 0; j < group.enemies.Count; j++)
                {
                    var e = group.enemies[j];
                    if (e.warning > 0) attacking++;
                    limitedTurns &= Mathf.Abs(Mathf.DeltaAngle(before[j], e.angle)) < 2.2f;
                    for (int k = j + 1; k < group.enemies.Count; k++) minimumFriendlyDistance = Mathf.Min(minimumFriendlyDistance, Vector2.Distance(e.position, group.enemies[k].position));
                }
                maxAttackers = Mathf.Max(maxAttackers, attacking); group.cues.Clear();
            }
            Check("Squad limits simultaneous firing warnings, not pursuit", maxAttackers > 0 && maxAttackers <= 2, "peak=" + maxAttackers);
            Check("AI respects bounded turn rates", limitedTurns);
            Check("Squad avoids collapsing into one position", minimumFriendlyDistance > 12, "minimum friendly spacing=" + minimumFriendlyDistance.ToString("F1"));
            var training = new CombatSimulation(); training.Start(true); Vector2 original = training.enemies[0].position;
            for (int i = 0; i < 600; i++) training.Step(1f / 120, new CombatSimulation.Controls { turn = .5f });
            Check("Practice targets remain stationary and harmless", training.enemies[0].position == original && training.bullets.Count == 0 && training.shield == 60);
        }
        void TestFlightBalance()
        {
            var s = new CombatSimulation(); s.Start();
            s.Step(1f / 120, new CombatSimulation.Controls());
            bool spawnForward = true;
            foreach (var enemy in s.enemies) spawnForward &= Vector2.Dot(enemy.velocity.normalized, CombatSimulation.Direction(enemy.angle)) > .99f;
            Check("New enemies accelerate along their initial nose direction", spawnForward);
            s.Start(true); s.enemies.Clear();
            // Keep practice alive without a respawn during long trajectory measurements.
            s.enemies.Add(new CombatSimulation.Enemy { kind = 3, position = new Vector2(-1400, 0), hp = 100 });
            for (int i = 0; i < 720; i++) s.Step(1f / 120, new CombatSimulation.Controls { thrust = true, turn = 1 });
            Vector2 circleStart = s.position; float swept = 0, minimumSpeed = 1000, maximumYaw = 0;
            int ticks = Mathf.RoundToInt(360f / 55 * 120);
            for (int i = 0; i < ticks; i++)
            {
                float before = s.angle;
                s.Step(1f / 120, new CombatSimulation.Controls { thrust = true, turn = 1 });
                swept += Mathf.DeltaAngle(before, s.angle);
                minimumSpeed = Mathf.Min(minimumSpeed, s.velocity.magnitude);
                maximumYaw = Mathf.Max(maximumYaw, Mathf.Abs(s.angularVelocity));
            }
            float radius = s.velocity.magnitude / (s.angularVelocity * Mathf.Deg2Rad);
            Check("W plus turn traces a broad closed circle", Vector2.Distance(circleStart, s.position) < 15 && swept > 355 && swept < 365 && radius > 340 && radius < 400,
                "radius=" + radius.ToString("F1") + " closure=" + Vector2.Distance(circleStart, s.position).ToString("F1"));
            Check("High speed turn preserves forward speed and limits yaw", minimumSpeed > 350 && maximumYaw < 56);
            float beforeBrake = s.angularVelocity;
            s.Step(1f / 120, new CombatSimulation.Controls { brake = true, turn = 1 });
            Check("Brake does not instantly grant low-speed turning", s.angularVelocity < beforeBrake + 3);
            for (int i = 0; i < 480; i++) s.Step(1f / 120, new CombatSimulation.Controls { brake = true, turn = 1 });
            radius = s.velocity.magnitude / (s.angularVelocity * Mathf.Deg2Rad);
            Check("Braking gradually tightens the turn circle", radius > 40 && radius < 55 && s.angularVelocity < 121, "radius=" + radius.ToString("F1"));
            float previous = s.angularVelocity;
            s.Step(1f / 120, new CombatSimulation.Controls { brake = true, turn = -1 });
            Check("Reversing turn has bounded angular acceleration", s.angularVelocity > 0 && previous - s.angularVelocity <= 2.01f);
            foreach (var pilot in new[] { CombatSimulation.PilotStyle.TailHunter, CombatSimulation.PilotStyle.Flanker, CombatSimulation.PilotStyle.Slasher })
            {
                var duel = EnemyScenario(pilot, new Vector2(-240, 0), 0); var e = duel.enemies[0];
                bool reversed = false; float held = 0, peakHeld = 0, maxYaw = 0, maxSpeed = 0;
                for (int i = 0; i < 1440; i++)
                {
                    // Break first, then fly towards the opening; circling forever is
                    // not a pursuit strategy against a faster pass fighter.
                    float error = Mathf.DeltaAngle(duel.angle, CombatSimulation.Bearing(e.position - duel.position));
                    duel.Step(1f / 120, i < 300
                        ? new CombatSimulation.Controls { brake = true, turn = 1 }
                        : new CombatSimulation.Controls { turn = Mathf.Clamp(error / 25, -1, 1), brake = Mathf.Abs(error) > 35,
                            thrust = Mathf.Abs(error) < 25 && Vector2.Distance(e.position, duel.position) > 350 });
                    Vector2 delta = e.position - duel.position;
                    bool advantage = Vector2.Dot(CombatSimulation.Direction(e.angle), -delta.normalized) < -.3f
                        && Vector2.Dot(CombatSimulation.Direction(duel.angle), delta.normalized) > .75f && delta.magnitude < 650;
                    held = advantage ? held + 1f / 120 : 0; peakHeld = Mathf.Max(peakHeld, held); reversed |= advantage;
                    maxYaw = Mathf.Max(maxYaw, Mathf.Abs(e.angularVelocity)); maxSpeed = Mathf.Max(maxSpeed, e.velocity.magnitude);
                    duel.cues.Clear();
                }
                Check(pilot + " can be outturned from an initial tail advantage", reversed && peakHeld > .3f, "player rear advantage=" + peakHeld.ToString("F2") + "s");
                Check(pilot + " respects reduced flight envelope", maxYaw < 97 && maxSpeed < 266, "yaw=" + maxYaw.ToString("F1") + " speed=" + maxSpeed.ToString("F1"));
            }
        }
    }
}
