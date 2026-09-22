using UnityEngine;

namespace AstraCabin
{
    // Arcade 2D adaptations, not a real aircraft flight/tactics model.
    // Tactical decisions are sampled; safety steering runs at the simulation rate.
    public sealed partial class CombatSimulation
    {
        public enum PilotStyle { TailHunter, Slasher, Flanker, Gunship, Commander, Training }
        public enum Maneuver { Approach, Attack, Extend, Break, Reposition }
        public static string PilotName(Enemy e)
        {
            switch (e.pilot)
            {
                case PilotStyle.TailHunter: return "尾追";
                case PilotStyle.Slasher: return "掠袭";
                case PilotStyle.Flanker: return "侧翼";
                case PilotStyle.Gunship: return "压制";
                case PilotStyle.Commander: return "指挥";
                default: return "靶机";
            }
        }
        public static string ManeuverName(Enemy e)
        {
            switch (e.maneuver)
            {
                case Maneuver.Attack: return "进攻";
                case Maneuver.Extend: return "脱离";
                case Maneuver.Break: return "防御转向";
                case Maneuver.Reposition: return "重整";
                default: return "迂回";
            }
        }
        void SetManeuver(Enemy e, Maneuver state)
        {
            if (e.maneuver == state) return;
            e.maneuver = state; e.maneuverTime = 0;
            if (state != Maneuver.Attack) e.warning = 0;
            if (state == Maneuver.Attack) e.passes++;
            if (state == Maneuver.Extend)
            {
                Vector2 away = (e.position - position).normalized;
                e.maneuverGoal = Vector2.ClampMagnitude(e.position + Direction(e.angle) * 260 + away * 70, ArenaRadius - 180);
            }
            if (state == Maneuver.Break)
            {
                e.breaks++; e.evadeCooldown = 2.5f;
                float error = Mathf.DeltaAngle(e.angle, Bearing(position - e.position));
                if (Mathf.Abs(error) < 175) e.side = error >= 0 ? 1 : -1;
            }
            if (state == Maneuver.Reposition)
                e.maneuverGoal = Direction(Bearing(-e.position) + e.side * 35) * 380;
        }
        bool FireSlotAvailable(Enemy self)
        {
            int attacking = 0;
            foreach (var other in enemies) if (other != self && other.kind != 3 && other.warning > 0) attacking++;
            return attacking < 2;
        }
        void DecideEnemy(Enemy e)
        {
            e.observedPosition = position; e.observedVelocity = velocity; e.observedHeading = angle;
            Vector2 toPlayer = position - e.position;
            float distance = toPlayer.magnitude;
            if (e.position.magnitude > ArenaRadius - 130)
            { SetManeuver(e, Maneuver.Reposition); return; }
            if (e.maneuver == Maneuver.Reposition)
            {
                if (e.maneuverTime > .6f && e.position.magnitude < ArenaRadius - 240) SetManeuver(e, Maneuver.Attack);
                return;
            }
            if (e.maneuver == Maneuver.Break)
            {
                // A defensive break fights for nose position; it is not an escape timer.
                float noseError = Mathf.Abs(Mathf.DeltaAngle(e.angle, Bearing(toPlayer)));
                if ((e.maneuverTime > .45f && noseError < 45) || e.maneuverTime > 3)
                    SetManeuver(e, Maneuver.Attack);
                return;
            }
            if (e.maneuver == Maneuver.Extend)
            {
                if (e.maneuverTime > .8f && (distance > 250 || e.maneuverTime > 1.4f))
                { e.side = -e.side; SetManeuver(e, Maneuver.Attack); }
                return;
            }
            // React to observable geometry, not the user's buttons or future inputs.
            bool tailed = distance < 550 && distance > 90
                && Vector2.Dot(Direction(e.angle), toPlayer.normalized) < -.55f
                && Vector2.Dot(Direction(angle), -toPlayer.normalized) > .82f;
            if (e.kind == 0 && e.evadeCooldown <= 0 && tailed)
            { SetManeuver(e, Maneuver.Break); return; }
            if (e.maneuver == Maneuver.Attack)
            {
                // Only the pass fighter extends after an actual overshoot. A fighter
                // with a good tail position must never abandon it because time elapsed.
                if (e.pilot == PilotStyle.Slasher && e.maneuverTime > 3.5f && distance < 270
                    && Vector2.Dot(Direction(e.angle), toPlayer.normalized) < -.2f) SetManeuver(e, Maneuver.Extend);
                return;
            }
            float setup = e.pilot == PilotStyle.Flanker ? .7f : .35f;
            if (e.maneuverTime > setup)
                SetManeuver(e, Maneuver.Attack);
        }
        void FlyEnemy(Enemy e, float dt)
        {
            e.maneuverTime += dt; e.evadeCooldown = Mathf.Max(0, e.evadeCooldown - dt);
            e.decisionTimer -= dt;
            if (e.decisionTimer <= 0)
            { DecideEnemy(e); e.decisionTimer = .35f + (e.id % 3) * .04f; }
            Vector2 targetForward = Direction(e.observedHeading), lateral = new Vector2(-targetForward.y, targetForward.x);
            float distance = Vector2.Distance(e.position, e.observedPosition);
            Vector2 predicted = e.observedPosition + e.observedVelocity * Mathf.Clamp(distance / 750, 0, .55f);
            Vector2 goal;
            float speed = e.kind == 2 ? 100 : e.kind == 1 ? 125 : e.pilot == PilotStyle.Slasher ? 245 : 190;
            if (e.maneuver == Maneuver.Break)
            {
                goal = e.position + Direction(e.angle + 100 * e.side) * 160;
                speed = 120;
            }
            else if (e.maneuver == Maneuver.Extend || e.maneuver == Maneuver.Reposition)
            {
                goal = e.maneuverGoal;
                if (e.kind == 0) speed = e.pilot == PilotStyle.Slasher ? 265 : 220;
            }
            else if (e.maneuver == Maneuver.Attack)
            {
                // Track the tail when advantaged; otherwise turn for a firing angle.
                Vector2 firingLead = e.observedPosition + e.observedVelocity * Mathf.Clamp(distance / 520, 0, .65f);
                bool onTail = Vector2.Dot(targetForward, (e.position - e.observedPosition).normalized) < -.45f
                    && Mathf.Abs(Mathf.DeltaAngle(e.angle, e.observedHeading)) < 55;
                float noseError = Mathf.Abs(Mathf.DeltaAngle(e.angle, Bearing(e.observedPosition - e.position)));
                goal = onTail ? e.observedPosition - targetForward * 95 : e.observedPosition + e.observedVelocity * Mathf.Clamp(distance / 1800, 0, .25f);
                speed = onTail ? Mathf.Clamp(e.observedVelocity.magnitude + (distance - 220) * .4f, 120, 220)
                    : Mathf.Lerp(130, 210, Mathf.InverseLerp(100, 520, distance)) * Mathf.Lerp(.9f, 1, Mathf.InverseLerp(100, 15, noseError));
                if (e.pilot == PilotStyle.Slasher)
                {
                    float aimError = Mathf.Abs(Mathf.DeltaAngle(e.angle, Bearing(firingLead - e.position)));
                    // Align before accelerating into the pass, rather than rushing
                    // through the safety envelope before the gun can bear.
                    speed = Mathf.Lerp(160, 265, Mathf.InverseLerp(55, 8, aimError));
                    goal = firingLead;
                }
                if (e.pilot == PilotStyle.Flanker && distance > 450) goal -= targetForward * 100 + lateral * e.side * 100;
                if (e.kind > 0)
                {
                    // Slow into a turn instead of orbiting forever outside a tighter target.
                    speed = e.kind == 2
                        ? Mathf.Lerp(60, 100, Mathf.InverseLerp(75, 15, noseError))
                        : Mathf.Lerp(45, 125, Mathf.InverseLerp(45, 10, noseError));
                    goal = firingLead;
                }
            }
            else if (e.kind > 0 || e.pilot == PilotStyle.Flanker || e.pilot == PilotStyle.Slasher)
            {
                float orbit = Bearing(e.position - predicted) + e.side * 32;
                goal = predicted + Direction(orbit) * (e.pilot == PilotStyle.Slasher ? 590 : e.kind == 2 ? 540 : 440);
            }
            else
            {
                float sideDistance = e.pilot == PilotStyle.Flanker ? 390 : e.pilot == PilotStyle.Slasher ? 280 : 150;
                goal = predicted - targetForward * (e.pilot == PilotStyle.TailHunter ? 260 : 140) + lateral * e.side * sideDistance;
                // Brief initial setup: keep moving around the fight, not into a pile.
                if (Vector2.Distance(e.position, goal) < 110)
                    goal = predicted + Direction(Bearing(e.position - predicted) + e.side * 40) * 420;
            }
            goal = Vector2.ClampMagnitude(goal, ArenaRadius - 230);
            Vector2 desired = (goal - e.position).normalized;
            Vector2 toPlayer = position - e.position, relativeVelocity = velocity - e.velocity;
            float closestTime = relativeVelocity.sqrMagnitude < 1 ? 0 : -Vector2.Dot(toPlayer, relativeVelocity) / relativeVelocity.sqrMagnitude;
            float avoidanceHorizon = e.kind == 2 ? 1.7f : e.kind == 1 ? 1.4f : 1.2f;
            bool collisionCourse = closestTime > 0 && closestTime < avoidanceHorizon && (toPlayer + relativeVelocity * closestTime).magnitude < e.Radius + 28;
            if (toPlayer.magnitude < e.Radius + 32 || collisionCourse)
            {
                // A local sidestep does not cancel the whole dogfight.
                e.avoidanceCount++; e.warning = 0;
                Vector2 away = -toPlayer.normalized;
                desired = (desired * .2f + away * .3f + new Vector2(-away.y, away.x) * e.side).normalized;
                speed = Mathf.Min(speed, e.kind == 0 ? 125 : 95);
            }
            // Teammate separation has no instantaneous positional pushes.
            foreach (var other in enemies)
            {
                if (other == e || other.kind == 3) continue;
                Vector2 relative = other.position - e.position, closing = other.velocity - e.velocity;
                float time = closing.sqrMagnitude < 1 ? 0 : Mathf.Clamp(-Vector2.Dot(relative, closing) / closing.sqrMagnitude, 0, .8f);
                Vector2 separation = -relative - closing * time;
                float safe = e.Radius + other.Radius + 75;
                if (separation.sqrMagnitude < safe * safe)
                {
                    desired += (separation.sqrMagnitude < 25 ? lateral * (e.id < other.id ? 1 : -1) : separation.normalized) * (1 - separation.magnitude / safe) * 2;
                    if (relative.magnitude < 150) speed = Mathf.Min(speed, 140);
                }
            }
            if (e.position.magnitude > ArenaRadius - 90)
            { SetManeuver(e, Maneuver.Reposition); desired = -e.position.normalized; }
            float turnRate = FlightTurnRate(e.velocity.magnitude) * (e.kind == 2 ? .36f : e.kind == 1 ? .48f : e.pilot == PilotStyle.Slasher ? .62f : e.pilot == PilotStyle.Flanker ? .73f : .8f);
            float heading = Bearing(desired);
            // A telegraphed firing run commits to its announced direction.
            if (e.warning > 0 && !collisionCourse && toPlayer.magnitude > 230) heading = e.shotAngle;
            float requestedTurn = Mathf.Clamp(Mathf.DeltaAngle(e.angle, heading) * 3, -turnRate, turnRate);
            e.angularVelocity = Mathf.MoveTowards(e.angularVelocity, requestedTurn, 130 * dt);
            e.angle = Mathf.Repeat(e.angle + e.angularVelocity * dt + 180, 360) - 180;
            float flightSpeed = Mathf.MoveTowards(e.velocity.magnitude, speed, 95 * dt);
            float flightHeading = e.velocity.sqrMagnitude < 1 ? e.angle : Mathf.LerpAngle(Bearing(e.velocity), e.angle, 1 - Mathf.Exp(-6 * dt));
            e.velocity = Direction(flightHeading) * flightSpeed;
            e.position = Vector2.ClampMagnitude(e.position + e.velocity * dt, ArenaRadius - e.Radius - 8);
        }
    }
}
