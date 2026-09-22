using System;
using UnityEngine;

namespace AstraCabin
{
    public sealed partial class CombatMode
    {
        Vector2 ToScreen(Vector2 world, Rect field) { Vector2 d = (world - cameraPosition) * zoom; return field.size * .5f + new Vector2(d.x, -d.y); }
        void DrawField(Rect field)
        {
            GUI.BeginGroup(field); Rect local = new Rect(0, 0, field.width, field.height);
            GUI.DrawTexture(local, nebula, ScaleMode.StretchToFill);
            for (int i = 0; i < stars.Length; i++)
            {
                float depth = .07f + (i % 3) * .045f;
                float x = Mathf.Repeat(stars[i].x * field.width - cameraPosition.x * depth, field.width);
                float y = Mathf.Repeat(stars[i].y * field.height + cameraPosition.y * depth, field.height);
                Fill(new Rect(x, y, i % 7 == 0 ? 2 : 1, i % 7 == 0 ? 2 : 1), new Color(.55f, .77f, .68f, .35f + i % 3 * .18f));
            }
            float grid = 160 * zoom;
            for (float x = Mathf.Repeat(-cameraPosition.x * zoom + field.width / 2, grid); x < field.width; x += grid) Fill(new Rect(x, 0, 1, field.height), new Color(.3f, .6f, .48f, .065f));
            for (float y = Mathf.Repeat(cameraPosition.y * zoom + field.height / 2, grid); y < field.height; y += grid) Fill(new Rect(0, y, field.width, 1), new Color(.3f, .6f, .48f, .065f));
            Vector2 shake = !PhysicalFeedbackEnabled ? Vector2.zero : new Vector2(Mathf.PerlinNoise(feedbackClock * 32, 5) - .5f, Mathf.PerlinNoise(1, feedbackClock * 37) - .5f) * sim.trauma * sim.trauma * 10;
            foreach (var p in sim.particles)
            { Vector2 s = ToScreen(p.position, field) + shake; Color c = p.color; c.a = p.life / p.total; Fill(new Rect(s.x, s.y, p.size, p.size), c); }
            foreach (var e in sim.enemies)
            {
                Vector2 s = ToScreen(e.position, field) + shake;
                if (e.warning > 0)
                {
                    Vector2 end = s + new Vector2(Mathf.Cos(e.shotAngle * Mathf.Deg2Rad), -Mathf.Sin(e.shotAngle * Mathf.Deg2Rad)) * 430;
                    Line(s, end, new Color(1, .36f, .2f, .15f + e.warning * .2f), 1); Ring(s, e.Radius * zoom + 10 + e.warning * 10, amber, 20);
                }
                if (s.x < -60 || s.x > field.width + 60 || s.y < -60 || s.y > field.height + 60) { EdgeMarker(s, field, e.kind == 2); continue; }
                Color color = e.flash > 0 ? Color.white : e.kind == 3 ? dim : e.kind == 2 ? red : amber;
                Sprite(e.kind == 2 ? boss : e.kind == 1 ? heavy : ship, s, -e.angle, e.Radius * 2.9f * zoom, color);
                if (e.hp < e.maxHp) { Fill(new Rect(s.x - 19, s.y - e.Radius * zoom - 12, 38, 3), new Color(.2f, .25f, .21f)); Fill(new Rect(s.x - 19, s.y - e.Radius * zoom - 12, 38 * e.hp / e.maxHp, 3), color); }
                if (sim.lockedTarget == e)
                {
                    Brackets(s, e.Radius * zoom + 14, sim.lockProgress >= 1 ? green : amber);
                    Label(new Rect(s.x - 38, s.y + e.Radius * zoom + 18, 120, 19), sim.lockProgress >= 1 ? "LOCK / 右键" : "锁定 " + Mathf.RoundToInt(sim.lockProgress * 100) + "%", 12, green);
                }
            }
            foreach (var b in sim.bullets)
            {
                Vector2 p = ToScreen(b.position, field) + shake;
                if (!local.Contains(p)) continue;
                Vector2 d = b.velocity.normalized; d.y = -d.y;
                if (b.hostile) { Fill(new Rect(p.x - 3, p.y - 3, 6, 6), red); Fill(new Rect(p.x - 1, p.y - 1, 2, 2), amber); }
                else Line(p - d * (b.missile ? 21 : 12), p, b.missile ? amber : green, b.missile ? 4 : 2);
            }
            Vector2 player = ToScreen(sim.position, field) + shake;
            Vector2 forward = new Vector2(Mathf.Cos(sim.angle * Mathf.Deg2Rad), -Mathf.Sin(sim.angle * Mathf.Deg2Rad));
            Vector2 exhaust = player - forward * 22 * zoom;
            Line(exhaust, exhaust - forward * (sim.boostTime > 0 ? 42 : 12), green, sim.boostTime > 0 ? 6 : 3);
            Sprite(ship, player, -sim.angle, 46 * zoom, sim.invulnerable > 0 ? Color.Lerp(green, Color.white, .5f + .5f * Mathf.Sin(sim.elapsed * 38)) : green);
            if (sim.boostTime > 0) Ring(player, 26, new Color(.4f, 1, .8f, .6f), 24);
            if (sim.shotFlash > 0) { Vector2 muzzle = player + forward * 26 * zoom; Line(muzzle - Vector2.one * 3, muzzle + Vector2.one * 3, Color.white, 2); }
            { Vector2 aim = player + forward * 100; Ring(aim, 5, dim, 12); Line(aim + Vector2.left * 10, aim + Vector2.left * 5, dim, 1); Line(aim + Vector2.right * 5, aim + Vector2.right * 10, dim, 1); }
            if (sim.damageFlash > 0) { Color c = new Color(1, .15f, .08f, sim.damageFlash * .65f); Fill(new Rect(0, 0, field.width, 3), c); Fill(new Rect(0, field.height - 3, field.width, 3), c); }
            if (sim.position.magnitude > CombatSimulation.ArenaRadius - 200) Label(new Rect(field.width / 2 - 200, 42, 400, 25), "接近作战边界 / 请向中心转向", 17, amber, TextAnchor.MiddleCenter);
            foreach (var e in sim.enemies) if (e.kind == 2)
            {
                float bw = Mathf.Min(420, field.width - 120), bx = (field.width - bw) / 2;
                Label(new Rect(bx, field.height - 42, bw, 20), e.hp < e.maxHp * .5f ? "WARDEN / 指挥机 · 过载弹幕" : "WARDEN / 重型指挥机", 12, amber, TextAnchor.MiddleCenter);
                Fill(new Rect(bx, field.height - 18, bw, 4), new Color(.2f, .13f, .1f));
                Fill(new Rect(bx, field.height - 18, bw * e.hp / e.maxHp, 4), red);
            }
            if (sim.sectorTime < 3)
            {
                Fill(new Rect(field.width / 2 - 180, 14, 360, 30), new Color(.03f, .09f, .075f, .9f));
                Label(new Rect(field.width / 2 - 180, 14, 360, 30), sim.training ? "练习场 / 靶机无攻击性" : sim.sector == 3 ? "03 / 重型指挥机已进入空域" : sim.sector == 2 ? "02 / 重型护航编队" : "01 / 截获巡逻编队", 16, green, TextAnchor.MiddleCenter);
            }
            for (int y = 0; y < field.height; y += 4) Fill(new Rect(0, y, field.width, 1), new Color(0, 0, 0, .045f));
            GUI.EndGroup(); Border(field, dim);
        }
        void EdgeMarker(Vector2 p, Rect field, bool isBoss)
        {
            Vector2 center = field.size / 2, d = p - center;
            float factor = Mathf.Min((center.x - 20) / Mathf.Max(.1f, Mathf.Abs(d.x)), (center.y - 20) / Mathf.Max(.1f, Mathf.Abs(d.y)));
            Vector2 s = center + d * factor; Vector2 n = d.normalized, side = new Vector2(-n.y, n.x);
            Line(s + n * 7, s - n * 5 + side * 5, isBoss ? red : amber, 2); Line(s + n * 7, s - n * 5 - side * 5, isBoss ? red : amber, 2);
        }
        void DrawRail(Rect r)
        {
            Fill(r, new Color(.035f, .08f, .067f)); Border(r, dim);
            Label(new Rect(r.x + 12, r.y + 8, 140, 22), "TACTICAL / RADAR", 12, dim);
            Vector2 c = new Vector2(r.center.x, r.y + 108); Ring(c, 60, dim, 40); Ring(c, 30, new Color(.16f, .29f, .23f), 32);
            Line(c + Vector2.left * 64, c + Vector2.right * 64, dim * .55f, 1); Line(c + Vector2.up * 64, c + Vector2.down * 64, dim * .55f, 1);
            foreach (var e in sim.enemies) { Vector2 d = Vector2.ClampMagnitude((e.position - sim.position) * .07f, 59); Fill(new Rect(c.x + d.x - 2, c.y - d.y - 2, e.kind == 2 ? 6 : 4, e.kind == 2 ? 6 : 4), e.kind == 2 ? red : amber); }
            Fill(new Rect(c.x - 2, c.y - 2, 4, 4), green);
            Label(new Rect(r.x + 12, r.y + 181, 145, 22), "SCORE  " + sim.score.ToString("000000"), 14, ink);
            Label(new Rect(r.x + 12, r.y + 211, 145, 22), "SPD  " + Mathf.RoundToInt(sim.velocity.magnitude) + " m/s", 14, green);
            Label(new Rect(r.x + 12, r.y + 241, 145, 22), "导弹  " + sim.missiles + "  [右键]", 15, amber);
            Label(new Rect(r.x + 12, r.y + 271, 145, 22), sim.combo > 1 ? "连击  ×" + sim.combo : "TIME  " + TimeSpan.FromSeconds(sim.elapsed).ToString(@"mm\:ss"), 14, ink);
            if (r.height > 337) Label(new Rect(r.x + 12, r.y + 303, 145, 22), sim.boostCooldown > 0 ? "推进冷却 " + sim.boostCooldown.ToString("0.0") : "推进就绪 / SHIFT", 12, dim);
            if (r.height > 370) Label(new Rect(r.x + 12, r.y + 334, 145, 22), "04 / BIO-SENSOR", 12, dim);
        }
    }
}
