using System;
using System.Collections.Generic;
using UnityEngine;

namespace AstraCabin
{
    // World coordinates are independent of the desktop size; simulation runs at 120 Hz.
    public sealed class CombatSimulation
    {
        public enum Phase { Menu, Running, Paused, Upgrade, Won, Lost }
        public struct Controls { public float turn; public bool thrust, brake, fire, boost, missile; }
        public struct Cue { public string name; public float strength; public Cue(string n, float s) { name = n; strength = s; } }
        public sealed class Enemy
        {
            public Vector2 position, velocity;
            public float angle, hp, maxHp, timer, warning, shotAngle, flash, age;
            public int kind, id;
            public float Radius { get { return kind == 2 ? 42 : kind == 1 ? 23 : 15; } }
        }
        public sealed class Bullet
        {
            public Vector2 position, velocity;
            public float life, damage;
            public bool hostile, missile;
            public Enemy target;
        }
        public sealed class Particle { public Vector2 position, velocity; public float life, total, size; public Color color; }
        public Phase phase = Phase.Menu;
        public Phase resumePhase = Phase.Running;
        public Vector2 position, velocity;
        public float angle, angularVelocity, hull = 100, shield = 60, maxShield = 60, energy = 100, heat;
        public float elapsed, invulnerable, boostTime, boostCooldown, missileCooldown, lockProgress, comboTimer;
        public float damageFlash, shotFlash, trauma, sectorTime, clearDelay;
        public bool overheated, training;
        public int sector, kills, score, shots, hits, combo, missiles, weaponLevel, engineLevel, shieldLevel;
        public Enemy lockedTarget;
        public readonly List<Enemy> enemies = new List<Enemy>();
        public readonly List<Bullet> bullets = new List<Bullet>();
        public readonly List<Particle> particles = new List<Particle>();
        public readonly List<Cue> cues = new List<Cue>();
        public const float ArenaRadius = 1550;
        public const float CruiseSpeed = 235, BrakeSpeed = 95, ThrustSpeed = 355, BoostSpeed = 640;
        public const float TurnSpeed = 245;
        System.Random random = new System.Random(1943);
        float fireCooldown, sinceDamage, trailClock;
        int nextId;

        public void Start(bool practice = false, int seed = 1943)
        {
            random = new System.Random(seed); training = practice; phase = Phase.Running;
            position = Vector2.zero; velocity = new Vector2(180, 0); angle = angularVelocity = 0;
            hull = energy = 100; shield = maxShield = 60; heat = elapsed = sectorTime = 0;
            damageFlash = shotFlash = trauma = invulnerable = boostTime = boostCooldown = missileCooldown = 0;
            fireCooldown = sinceDamage = trailClock = clearDelay = comboTimer = lockProgress = 0;
            sector = 1; kills = score = shots = hits = combo = weaponLevel = engineLevel = shieldLevel = nextId = 0;
            missiles = 4; overheated = false; lockedTarget = null;
            enemies.Clear(); bullets.Clear(); particles.Clear(); cues.Clear(); SpawnSector();
        }
        public void Pause() { if (phase == Phase.Running) { resumePhase = phase; phase = Phase.Paused; } }
        public void Resume() { if (phase == Phase.Paused) phase = resumePhase; }
        public void ToMenu() { phase = Phase.Menu; bullets.Clear(); particles.Clear(); enemies.Clear(); cues.Clear(); lockedTarget = null; }
        public void Upgrade(int choice)
        {
            if (phase != Phase.Upgrade || choice < 0 || choice > 2) return;
            if (choice == 0) weaponLevel++;
            if (choice == 1) engineLevel++;
            if (choice == 2) { shieldLevel++; maxShield += 30; }
            hull = Mathf.Min(100, hull + 25); shield = maxShield; energy = 100; heat = 0; overheated = false;
            missiles = Mathf.Min(8, missiles + 3); sector++; sectorTime = clearDelay = 0;
            bullets.Clear(); particles.Clear(); lockedTarget = null; lockProgress = 0;
            phase = Phase.Running; SpawnSector();
        }
        float Range(float a, float b) { return Mathf.Lerp(a, b, (float)random.NextDouble()); }
        public static Vector2 Direction(float degrees) { float r = degrees * Mathf.Deg2Rad; return new Vector2(Mathf.Cos(r), Mathf.Sin(r)); }
        public static float Bearing(Vector2 v) { return Mathf.Atan2(v.y, v.x) * Mathf.Rad2Deg; }
        void SpawnSector()
        {
            enemies.Clear();
            int count = training ? 4 : sector == 1 ? 3 : sector == 2 ? 5 : 3;
            for (int i = 0; i < count; i++)
            {
                int kind = training ? 3 : sector == 3 && i == 0 ? 2 : sector >= 2 && i < 2 ? 1 : 0;
                float a = i * 360f / count + 20;
                Vector2 p = position + Direction(a) * (kind == 2 ? 580 : Range(420, 620));
                p = Vector2.ClampMagnitude(p, ArenaRadius - 160);
                float hp = kind == 2 ? 780 : kind == 1 ? 130 : kind == 3 ? 65 : 65;
                enemies.Add(new Enemy { id = nextId++, kind = kind, position = p, angle = Bearing(position - p), hp = hp, maxHp = hp, timer = Range(1.2f, 2.8f) });
            }
            cues.Add(new Cue("sector", 0.5f));
        }
        public void Step(float dt, Controls input)
        {
            if (phase != Phase.Running || dt <= 0) return;
            dt = Mathf.Min(dt, 1f / 30f);
            elapsed += dt; sectorTime += dt; sinceDamage += dt;
            invulnerable = Mathf.Max(0, invulnerable - dt); boostTime = Mathf.Max(0, boostTime - dt);
            boostCooldown = Mathf.Max(0, boostCooldown - dt); missileCooldown = Mathf.Max(0, missileCooldown - dt);
            fireCooldown -= dt; comboTimer = Mathf.Max(0, comboTimer - dt); if (comboTimer == 0) combo = 0;
            damageFlash = Mathf.Max(0, damageFlash - dt * 1.5f); shotFlash = Mathf.Max(0, shotFlash - dt * 9);
            trauma = Mathf.Max(0, trauma - dt * 1.4f);
            if (sinceDamage > 4) shield = Mathf.Min(maxShield, shield + dt * (9 + shieldLevel * 3));
            energy = Mathf.Min(100, energy + dt * (17 + engineLevel * 5));
            heat = Mathf.Max(0, heat - dt * (input.fire && !overheated ? 9 : 37));
            if (overheated && heat <= 25) overheated = false;
            if (input.boost && energy >= 32 && boostCooldown <= 0)
            {
                energy -= 32; boostTime = .38f; boostCooldown = 1.2f - engineLevel * .12f;
                invulnerable = Mathf.Max(invulnerable, .24f); cues.Add(new Cue("boost", .65f));
            }
            float turnRate = (TurnSpeed + engineLevel * 28) * (input.brake ? 1.45f : boostTime > 0 ? .65f : 1);
            float change = Mathf.Clamp(input.turn, -1, 1) * turnRate * dt;
            angle = Mathf.Repeat(angle + change + 180, 360) - 180;
            angularVelocity = Mathf.Lerp(angularVelocity, change / dt, 1 - Mathf.Exp(-14 * dt));
            float speed = boostTime > 0 ? BoostSpeed + engineLevel * 55 : input.brake ? BrakeSpeed : input.thrust ? ThrustSpeed : CruiseSpeed;
            // A/D turn the nose; W/S set thrust/braking. Velocity follows with brief inertia.
            velocity = Vector2.Lerp(velocity, Direction(angle) * speed, 1 - Mathf.Exp(-(input.brake ? 6.8f : boostTime > 0 ? 8 : 3.5f) * dt));
            position += velocity * dt;
            if (position.magnitude > ArenaRadius) { position = position.normalized * ArenaRadius; velocity += -position.normalized * 750 * dt; }
            UpdateLock(dt);
            if (input.fire && !overheated && fireCooldown <= 0)
            {
                fireCooldown = .095f / (1 + weaponLevel * .16f); heat = Mathf.Min(100, heat + 5.4f);
                if (heat >= 100) { overheated = true; cues.Add(new Cue("heat", .5f)); }
                Vector2 d = Direction(angle + Range(-1.2f, 1.2f));
                bullets.Add(new Bullet { position = position + d * 23, velocity = d * 1000 + velocity * .3f, life = 1.2f, damage = 18 + weaponLevel * 5 });
                shots++; shotFlash = 1; cues.Add(new Cue("shot", .3f));
            }
            if (input.missile && missileCooldown <= 0 && missiles > 0 && lockedTarget != null && lockProgress >= 1)
            {
                missiles--; missileCooldown = .8f;
                bullets.Add(new Bullet { position = position + Direction(angle) * 25, velocity = Direction(angle) * 500, life = 4, damage = 100, missile = true, target = lockedTarget });
                shots++; cues.Add(new Cue("missile", .7f));
            }
            trailClock += dt;
            if (trailClock > .025f) { trailClock = 0; Spark(position - Direction(angle) * 18, -velocity * .15f, new Color(.25f, .9f, .75f), .5f, boostTime > 0 ? 5 : 2); }
            UpdateEnemies(dt); UpdateBullets(dt);
            for (int i = particles.Count - 1; i >= 0; i--) { var p = particles[i]; p.life -= dt; p.position += p.velocity * dt; p.velocity *= Mathf.Exp(-dt * 2); if (p.life <= 0) particles.RemoveAt(i); }
            if (phase != Phase.Running) return;
            if (enemies.Count == 0)
            {
                clearDelay += dt;
                if (clearDelay > 1.4f)
                {
                    if (training) { SpawnSector(); clearDelay = 0; }
                    else { phase = sector == 3 ? Phase.Won : Phase.Upgrade; score += sector * 500; bullets.Clear(); cues.Add(new Cue("clear", .7f)); }
                }
            }
        }
        void UpdateLock(float dt)
        {
            Enemy best = null; float distance = 760;
            foreach (var e in enemies)
            {
                Vector2 delta = e.position - position; float d = delta.magnitude;
                if (d < distance && Mathf.Abs(Mathf.DeltaAngle(angle, Bearing(delta))) < 23) { best = e; distance = d; }
            }
            if (best != lockedTarget) { lockedTarget = best; lockProgress = 0; }
            if (best != null) { float old = lockProgress; lockProgress = Mathf.Min(1, lockProgress + dt / .7f); if (old < 1 && lockProgress >= 1) cues.Add(new Cue("lock", .4f)); }
            else lockProgress = 0;
        }
        void UpdateEnemies(float dt)
        {
            foreach (var e in enemies)
            {
                e.age += dt; e.flash = Mathf.Max(0, e.flash - dt * 8);
                if (e.kind == 3) continue;
                Vector2 delta = position - e.position;
                float distance = delta.magnitude;
                Vector2 lead = position + velocity * Mathf.Clamp(distance / 520, 0, .8f);
                float target = Bearing(lead - e.position);
                if (e.kind == 0 && distance < 135) target += 110; // Fly past, then re-enter; avoid perpetual face-to-face circles.
                e.angle = Mathf.MoveTowardsAngle(e.angle, target, (e.kind == 2 ? 48 : e.kind == 1 ? 85 : 150) * dt);
                float speed = e.kind == 2 ? 62 : e.kind == 1 ? 108 : 205;
                if (e.kind != 0 && distance < 350) speed = -35;
                e.velocity = Vector2.Lerp(e.velocity, Direction(e.angle) * speed, 1 - Mathf.Exp(-3 * dt));
                e.position = Vector2.ClampMagnitude(e.position + e.velocity * dt, ArenaRadius - 40);
                e.timer -= dt;
                if (e.warning > 0)
                {
                    e.warning -= dt;
                    if (e.warning <= 0)
                    {
                        int n = e.kind == 2 ? (e.hp < e.maxHp * .5f ? 9 : 7) : e.kind == 1 ? 3 : 1;
                        for (int i = 0; i < n; i++) EnemyBullet(e, e.shotAngle + (i - (n - 1) * .5f) * (e.kind == 2 ? 12 : 10), e.kind == 0 ? 410 : 300);
                        e.timer = e.kind == 2 ? 1.55f : e.kind == 1 ? 2 : 1.5f;
                        if (e.kind == 2 && e.hp < e.maxHp * .5f) for (int i = 0; i < 12; i++) EnemyBullet(e, i * 30 + e.age * 7, 190);
                    }
                }
                else if (e.timer <= 0 && distance < 950 && Mathf.Abs(Mathf.DeltaAngle(e.angle, target)) < 32)
                { e.warning = e.kind == 2 ? .95f : .58f; e.shotAngle = Bearing(lead - e.position); }
                if (distance < e.Radius + 12) DamagePlayer(18);
            }
        }
        void EnemyBullet(Enemy e, float a, float speed)
        { if (bullets.Count < 450) bullets.Add(new Bullet { hostile = true, position = e.position + Direction(a) * e.Radius, velocity = Direction(a) * speed, life = 5, damage = e.kind == 2 ? 16 : 12 }); }
        public static bool SegmentHit(Vector2 a, Vector2 b, Vector2 center, float radius)
        {
            Vector2 line = b - a; float t = line.sqrMagnitude < .000001f ? 0 : Mathf.Clamp01(Vector2.Dot(center - a, line) / line.sqrMagnitude);
            return (a + t * line - center).sqrMagnitude <= radius * radius;
        }
        void UpdateBullets(float dt)
        {
            for (int i = bullets.Count - 1; i >= 0; i--)
            {
                var b = bullets[i]; b.life -= dt; Vector2 old = b.position;
                if (b.missile && b.target != null && b.target.hp > 0)
                { float a = Mathf.MoveTowardsAngle(Bearing(b.velocity), Bearing(b.target.position - b.position), 210 * dt); b.velocity = Direction(a) * 590; }
                b.position += b.velocity * dt; bool dead = b.life <= 0;
                if (b.missile && random.NextDouble() < .45) Spark(b.position, -b.velocity * .12f, new Color(1, .76f, .35f), .35f, 3);
                if (b.hostile)
                { if (SegmentHit(old, b.position, position, 11)) { DamagePlayer(b.damage); dead = true; } }
                else
                {
                    for (int j = enemies.Count - 1; j >= 0; j--)
                    {
                        Enemy e = enemies[j];
                        if (!SegmentHit(old, b.position, e.position, e.Radius + (b.missile ? 7 : 4))) continue;
                        hits++; e.hp -= b.damage; e.flash = 1; dead = true;
                        Burst(b.position, 5, new Color(1, .83f, .45f), 80); cues.Add(new Cue("hit", .25f));
                        if (e.hp <= 0)
                        {
                            Burst(e.position, e.kind == 2 ? 65 : 28, new Color(1, .48f, .19f), e.kind == 2 ? 300 : 170);
                            enemies.RemoveAt(j); kills++; combo = Mathf.Min(5, combo + 1); comboTimer = 7;
                            score += (e.kind == 2 ? 1800 : e.kind == 1 ? 250 : 100) * Mathf.Max(1, combo);
                            trauma = Mathf.Min(1, trauma + (e.kind == 2 ? .7f : .28f)); cues.Add(new Cue("kill", e.kind == 2 ? 1 : .5f));
                            energy = Mathf.Min(100, energy + 12);
                        }
                        break;
                    }
                }
                if (dead) bullets.RemoveAt(i);
            }
        }
        public void DamagePlayer(float amount)
        {
            if (phase != Phase.Running || invulnerable > 0 || amount <= 0) return;
            float absorbed = Mathf.Min(shield, amount); shield -= absorbed;
            hull = Mathf.Max(training ? 1 : 0, hull - (amount - absorbed));
            sinceDamage = 0; invulnerable = .55f; damageFlash = 1; trauma = Mathf.Min(1, trauma + .65f);
            Burst(position, 14, new Color(.95f, .36f, .19f), 145); cues.Add(new Cue("damage", .8f));
            if (hull <= 0) { phase = Phase.Lost; cues.Add(new Cue("lost", 1)); }
        }
        void Spark(Vector2 p, Vector2 v, Color c, float life, float size)
        { if (particles.Count < 420) particles.Add(new Particle { position = p, velocity = v, color = c, life = life, total = life, size = size }); }
        void Burst(Vector2 p, int count, Color c, float speed)
        { for (int i = 0; i < count; i++) Spark(p, Direction(Range(0, 360)) * Range(speed * .15f, speed), c, Range(.2f, .85f), Range(1, 4)); }
    }
}
