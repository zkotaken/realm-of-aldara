using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Aldara
{
    // The hero's stats and fighting, ported from the browser: CLASSES base stats and recompute(), levels (xpNeedFor,
    // +18 hp / +3 atk / +10 stat points a level), click to target, chase into range and auto-attack every ATK_CD
    // (knight melee combo, mage bolts, archer arrows), Space attacks toward the cursor, mana regen, War Cry (+30%),
    // Eagle Eye (attack 40% faster), barriers that soak damage, the level-gap damage curve, death and respawn in town.
    public class AldaraHero : MonoBehaviour
    {
        public static AldaraHero I;
        public string cls = "knight", heroName = "Adventurer";
        public int lvl = 1, statPoints, kills, bossKills, deaths;
        public float xp, xpNeed = 100, gold;
        public int str, agi, vit, ene; public float baseAtk, baseHp;
        public float atk, hp, maxHp, mana, maxMana, speed = 220;
        public float atkCd, slowT, hurt, atkBuff, hasteBuff, shield, shieldT; public bool alive = true, aiming;
        public AldaraMonsters.Mon target;
        int combo; float lastSwing, deadAt = -1, lastFight = -99;
        AldaraPlayer P; AldaraCharacterAnimator anim;
        static readonly Dictionary<string, float> ATK_CD = new Dictionary<string, float> { { "knight", 0.55f }, { "mage", 0.7f }, { "archer", 0.6f } };

        void Awake() { I = this; P = GetComponent<AldaraPlayer>(); SetClass(cls); }
        public void SetClass(string c)
        {
            cls = c;
            switch (c)
            {
                case "mage": str = 6; agi = 9; vit = 8; ene = 20; baseAtk = 9; baseHp = 80; break;
                case "archer": str = 9; agi = 18; vit = 9; ene = 7; baseAtk = 11; baseHp = 95; break;
                default: str = 16; agi = 8; vit = 14; ene = 6; baseAtk = 14; baseHp = 120; break;
            }
            Recompute(); hp = maxHp; mana = maxMana; xpNeed = AldaraRules.XpNeedFor(lvl);
        }
        // ---- gear (recompute(), equipItem, unequipItem, autoEquipBest, addLoot, drinkVial) ----
        public readonly Dictionary<string, Item> equip = new Dictionary<string, Item>();
        public readonly List<Item> inventory = new List<Item>();
        public int vialHp, vialMp; float vialCdHp, vialCdMp;
        public string sub, title;
        public int effStr, effAgi, effVit, effEne; public int critFrame = -1; public MythicSet setBonusSet; public int setBonusCount; public float setBonusPct;
        public Item Eq(string slot) { Item i; return equip.TryGetValue(slot, out i) ? i : null; }
        public void Recompute()
        {
            float atkB = 0, hpB = 0, spB = 0, agiB = 0;
            if (Eq("wings") != null && lvl < AldaraItems.WINGS_LEVEL) { inventory.Add(Eq("wings")); equip.Remove("wings"); }
            foreach (var kv in equip) { var e = kv.Value; if (e == null) continue; atkB += e.atk; hpB += e.hp; spB += e.speed; agiB += e.agi; }
            float pm = 1 + AldaraItems.PetPct(Eq("pet"));
            int S_str = Mathf.RoundToInt(str * pm), S_agi = Mathf.RoundToInt((agi + agiB) * pm), S_vit = Mathf.RoundToInt(vit * pm), S_ene = Mathf.RoundToInt(ene * pm);
            effStr = S_str; effAgi = S_agi; effVit = S_vit; effEne = S_ene;
            float atkFromEne = Mathf.Floor(S_ene / 6f), atkFromAgi = 0;
            if (cls == "mage") atkFromEne = Mathf.Floor(S_ene / 3f);
            if (cls == "archer") atkFromAgi = Mathf.Floor(S_agi / 6f);
            float oldMax = maxHp, oldMana = maxMana;
            atk = baseAtk + Mathf.Floor(S_str / 4f) + atkFromEne + atkFromAgi + atkB;
            maxHp = baseHp + S_vit * 3 + hpB; speed = 220 + Mathf.Floor(S_agi / 8f) + spB;
            atk = Mathf.Round(atk * (1 + AldaraTree.T("atk"))); maxHp = Mathf.Round(maxHp * (1 + AldaraTree.T("hp")));
            // mythical set bonus: 3 pieces +10%, 5 pieces +20%, 6 pieces +30% attack and HP
            var counts = new Dictionary<string, int>(); foreach (var sl in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "back" }) { var ms = AldaraItems.MythSetOf(Eq(sl)); if (ms != null) { int c; counts.TryGetValue(ms.id, out c); counts[ms.id] = c + 1; } }
            int best = 0; string bestId = null; foreach (var kv in counts) if (kv.Value > best) { best = kv.Value; bestId = kv.Key; }
            setBonusSet = null; setBonusCount = 0; setBonusPct = 0;
            if (best >= 3) { float pct = best >= 6 ? 0.3f : best >= 5 ? 0.2f : 0.1f; atk = Mathf.Round(atk * (1 + pct)); maxHp = Mathf.Round(maxHp * (1 + pct)); setBonusCount = best; setBonusPct = pct; setBonusSet = AldaraItems.MythSetById(bestId); }
            if (oldMax > 0) hp = Mathf.Min(maxHp, hp + (maxHp - oldMax));
            maxMana = Mathf.Round((30 + S_ene * 3 + lvl * 2) * (1 + AldaraTree.T("mana")));
            if (oldMana > 0) mana = Mathf.Min(maxMana, mana + (maxMana - oldMana));
            // Crown of Kings relic: attack, health and mana up by its value, speed by 1.5 times it
            float kb = AldaraGear.RelicV("kingsblessing");
            if (kb > 0) { float m0 = maxHp, n0 = maxMana; atk = Mathf.Round(atk * (1 + kb / 100)); maxHp = Mathf.Round(maxHp * (1 + kb / 100)); maxMana = Mathf.Round(maxMana * (1 + kb / 100)); speed += Mathf.Round(kb * 1.5f); if (oldMax > 0) hp = Mathf.Min(maxHp, hp + (maxHp - m0)); if (oldMana > 0) mana = Mathf.Min(maxMana, mana + (maxMana - n0)); }
            if (P) { P.speed = speed * (1 + AldaraTree.T("spd")); P.wings = Eq("wings") != null; }
            AldaraHeroGear.SyncPlayer();
        }
        public bool AddLoot(Item it)
        {
            if (!AldaraItems.CanUse(it, cls)) { AldaraFx.Text(P.x, P.y - 56, "Left behind: " + it.name, new Color(0.6f, 0.6f, 0.6f)); return false; }
            if (inventory.Count >= AldaraItems.BACKPACK_MAX) { AldaraFx.Text(P.x, P.y - 56, "Backpack full", AldaraRules.Hex("#ff9a6a")); return false; }
            inventory.Add(it); if (it.rarity == "Mythical") AldaraHud.Banner("MYTHICAL DROP: " + it.name + "!"); return true;
        }
        public void EquipItem(Item it)
        {
            if (!AldaraItems.CanUse(it, cls)) { AldaraHud.Banner("Your class cannot use that weapon"); return; }
            if (it.type == "wings" && lvl < AldaraItems.WINGS_LEVEL) { AldaraHud.Banner("Wings can be worn from level " + AldaraItems.WINGS_LEVEL); return; }
            if (it.type == "relic") { AldaraGear.EquipRelic(it); Recompute(); AldaraSave.Dirty(); AldaraWindows.Refresh("inv"); return; }
            var prev = Eq(it.type); equip[it.type] = it; inventory.Remove(it); if (prev != null) inventory.Add(prev); Recompute(); AldaraSave.Dirty(); AldaraWindows.Refresh("inv");
        }
        public void Unequip(string slot) { var it = Eq(slot); if (it == null) return; equip.Remove(slot); inventory.Add(it); Recompute(); AldaraSave.Dirty(); AldaraWindows.Refresh("inv"); }
        public void AutoEquipBest()
        {
            int changed = 0;
            foreach (var slot in new[] { "helmet", "chest", "gauntlets", "leggings", "boots", "weapon", "back", "wings", "accessory", "pet" })
            {
                var cur = Eq(slot); Item bestI = null; float bs = cur != null ? AldaraItems.Score(cur) : -1;
                foreach (var it in inventory) { if (it.type != slot || !AldaraItems.CanUse(it, cls)) continue; if (it.type == "wings" && lvl < AldaraItems.WINGS_LEVEL) continue; float s = AldaraItems.Score(it); if (s > bs) { bs = s; bestI = it; } }
                if (bestI != null) { equip[slot] = bestI; inventory.Remove(bestI); if (cur != null) inventory.Add(cur); changed++; }
            }
            Recompute(); AldaraSave.Dirty();
            AldaraHud.Banner(changed > 0 ? "Equipped best loot (" + changed + " slot" + (changed > 1 ? "s" : "") + " upgraded)" : "Already wearing the best you have");
        }
        public bool DrinkVial(bool health, bool auto = false)
        {
            if (!alive) return false;
            if ((health ? vialHp : vialMp) <= 0) { if (!auto) AldaraFx.Text(P.x, P.y - 26, "No " + (health ? "Health" : "Mana") + " Vials", AldaraRules.Hex("#aaaabb")); return false; }
            if ((health ? vialCdHp : vialCdMp) > 0) return false;
            if (health && hp >= maxHp) { if (!auto) AldaraFx.Text(P.x, P.y - 26, "HP already full", AldaraRules.Hex("#aaaabb")); return false; }
            if (!health && mana >= maxMana) { if (!auto) AldaraFx.Text(P.x, P.y - 26, "MP already full", AldaraRules.Hex("#aaaabb")); return false; }
            if (health) { vialHp--; vialCdHp = 2; float amt = Mathf.Round(maxHp * 0.4f); hp = Mathf.Min(maxHp, hp + amt); AldaraFx.Text(P.x, P.y - 26, "+" + amt + " HP", AldaraRules.Hex("#7fe07f")); }
            else { vialMp--; vialCdMp = 2; float amt = Mathf.Round(maxMana * 0.5f); mana = Mathf.Min(maxMana, mana + amt); AldaraFx.Text(P.x, P.y - 26, "+" + amt + " MP", AldaraRules.Hex("#8aa8ff")); }
            AldaraSave.Dirty(); return true;
        }
        public void SpendPoint(string stat)
        {
            if (statPoints <= 0) return; statPoints--;
            if (stat == "str") str++; else if (stat == "agi") agi++; else if (stat == "vit") vit++; else ene++;
            Recompute(); AldaraSave.Dirty();
        }
        public void GainXp(float n)
        {
            xp += Mathf.Round(Mathf.Round(n) * (1 + AldaraTree.T("xp")));
            while (xp >= xpNeed)
            {
                xp -= xpNeed; lvl++; xpNeed = AldaraRules.XpNeedFor(lvl);
                baseHp += 18; baseAtk += 3; statPoints += 10; AldaraTree.sp++; AldaraTree.spTotal++;
                Recompute(); hp = maxHp; mana = maxMana;
                AldaraHud.Banner("LEVEL UP! Lv." + lvl); AldaraSave.Dirty();
            }
        }
        public float RollDmg(float mult)
        {   // rollDmg: War Cry, the tree's damage bonus, and its critical strikes (x1.6)
            float b = atkBuff > 0 ? 1.3f : 1; float d = atk * mult * b * (0.8f + Random.value * 0.5f) * (1 + AldaraTree.T("dmg"));
            float cr = AldaraTree.T("crit"); if (cr > 0 && Random.value < cr) { d *= 1.6f; critFrame = Time.frameCount; }
            d = Mathf.Round(d); if (AldaraAuto.on) d = Mathf.Max(1, Mathf.Round(d * AldaraAuto.DMG));   // auto-combat deals 25% less
            return d;
        }
        float AttackRange(AldaraMonsters.Mon t) { return cls == "archer" ? 280 : cls == "mage" ? 210 : t.r + 34; }
        float AtkCdNow() { return ATK_CD[cls] * (hasteBuff > 0 ? 0.6f : 1); }
        public void MarkFight() { lastFight = Time.time; }

        public void Damage(float dmg, float ang, int srcLvl)
        {
            if (!alive) return;
            if (srcLvl > 0) dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraRules.LvIn(srcLvl - lvl)));
            float blk = AldaraTree.T("block"); if (blk > 0) dmg = Mathf.Round(dmg * (1 - blk));
            if (shield > 0) { float ab = Mathf.Min(shield, dmg); shield -= ab; dmg -= ab; if (ab > 0) AldaraFx.Text(P.x + 14, P.y - 26, "(" + ab + ")", AldaraRules.Hex("#8ab4ff")); }
            hp -= dmg; DStat.Taken(dmg); hurt = 0.15f; lastFight = Time.time;
            if (dmg > 0) AldaraFx.Text(P.x, P.y - 26, "-" + dmg, AldaraRules.Hex("#ff6a6a"));
            if (hp <= 0) Died();
        }
        /// back on your feet (raids: revived, risen at the checkpoint, or after a wipe)
        public void Rise(float frac)
        {
            alive = true; hp = Mathf.Round(maxHp * frac); mana = Mathf.Max(mana, Mathf.Round(maxMana * frac)); target = null; if (anim) anim.Load();
        }
        void Died()
        {
            alive = false; hp = 0; target = null; deadAt = Time.time; deaths++; AldaraSave.Dirty();
            if (anim) anim.Play("death");
            if (AldaraRaid.On) { AldaraRaid.OnDeath(); return; }
            AldaraHud.Banner("You have fallen...");
        }

        // ---- aiming: the cursor if it is over the game, else the way you face ----
        public float AimAngle()
        {
            var m = Mouse.current; if (m == null || !AldaraCamera.I) return P.facing;
            var w = AldaraCamera.I.ScreenToWorldPx(m.position.ReadValue()); return Mathf.Atan2(w.y - (P.y - 8), w.x - P.x);
        }
        public Vector2 AimPoint(bool far)
        {
            float ang = AimAngle(), maxR = cls == "knight" ? 60 : 470, d = 300;
            var m = Mouse.current; if (!far && m != null && AldaraCamera.I) { var w = AldaraCamera.I.ScreenToWorldPx(m.position.ReadValue()); d = Mathf.Sqrt((w.x - P.x) * (w.x - P.x) + (w.y - P.y) * (w.y - P.y)); } else if (far) d = maxR;
            d = Mathf.Max(cls == "knight" ? 40 : 80, Mathf.Min(maxR, d));
            return new Vector2(P.x + Mathf.Cos(ang) * d, P.y + Mathf.Sin(ang) * d);
        }
        public AldaraMonsters.Mon MeleeArcTarget(float reach)
        {
            AldaraMonsters.Mon best = null; float bd = 1e9f;
            foreach (var m in AldaraMonsters.I.all)
            {
                if (m.dead) continue; float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d > reach + m.r) continue;
                if (Mathf.Abs(AldaraRules.AngD(Mathf.Atan2(m.y - P.y, m.x - P.x), P.facing)) < 1.1f && d < bd) { bd = d; best = m; }
            }
            return best;
        }
        public void DashTo(float ang, float dist)
        {
            for (float k = 0; k < dist; k += 10)
            {
                float px = P.x, py = P.y; P.x += Mathf.Cos(ang) * 10; P.y += Mathf.Sin(ang) * 10;
                if (AldaraWorld.BlockedAt(P.x, P.y) && !AldaraWorld.BlockedAt(px, py)) { P.x = px; P.y = py; break; }
            }
        }
        public void PlaySkill(string id) { lastFight = Time.time; if (anim) anim.Play(id); }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (!AldaraSave.Ready) return;
            if (!anim) anim = GetComponentInChildren<AldaraCharacterAnimator>();
            if (hurt > 0) hurt -= dt; if (slowT > 0) slowT -= dt; if (atkBuff > 0) atkBuff -= dt; if (hasteBuff > 0) hasteBuff -= dt;
            if (shieldT > 0) { shieldT -= dt; if (shieldT <= 0) shield = 0; }
            if (vialCdHp > 0) vialCdHp -= dt; if (vialCdMp > 0) vialCdMp -= dt;
            if (!alive)
            {
                if (AldaraRaid.On) { UpdateShots(dt); return; }   // the raid decides when you rise
                if (Time.time - deadAt > 1.8f)
                {   // back to town, as the browser does outside dungeons
                    if (AldaraDungeon.Active) AldaraDungeon.Exit("fail"); else { P.x = AldaraWorld.TOWN_SPAWN.x; P.y = AldaraWorld.TOWN_SPAWN.y; } alive = true; hp = maxHp; mana = maxMana; gold = Mathf.Floor(gold * 0.9f); target = null;
                    if (anim) anim.Load();
                }
                UpdateShots(dt); return;
            }
            if (atkCd > 0) atkCd -= dt;
            mana = Mathf.Min(maxMana, mana + (2 + effEne * 0.12f) * (1 + AldaraTree.T("mreg")) * dt);
            // click: the monster under the cursor (compared on screen, as the browser does with LZ); empty ground attacks there
            var mouse = Mouse.current;
            if (mouse != null && mouse.leftButton.wasPressedThisFrame && AldaraCamera.I && !AldaraHud.MouseOverUi())
            {
                var w = AldaraCamera.I.ScreenToWorldPx(mouse.position.ReadValue());
                float gy = AldaraWorld.HeightPx(P.x, P.y);
                if (AldaraWaystones.Click(w.x, w.y) || !AldaraWorld.Dun && AldaraQuestWorld.I && AldaraQuestWorld.I.Click(w.x, w.y)) { UpdateShots(dt); return; }
                AldaraMonsters.Mon best = null; float bd = 40;
                foreach (var m in AldaraMonsters.I.all)
                {
                    if (m.dead) continue; float my = m.y - (AldaraWorld.HeightPx(m.x, m.y) - gy);
                    float d = Mathf.Sqrt((m.x - w.x) * (m.x - w.x) + (my - w.y) * (my - w.y));
                    if (d < m.r + 20 && d < bd) { best = m; bd = d; }
                }
                target = best;
                if (best == null) { if (AldaraSkills.I) AldaraSkills.I.queued = null; FreeAttack(); }
            }
            var kb = Keyboard.current;
            if (kb != null && AldaraKeys.Held("attack") && atkCd <= 0 && !(target != null && !target.dead && InRange(target))) FreeAttack();
            if (kb != null && kb.tabKey.wasPressedThisFrame) target = AldaraMonsters.I.FindNearest(P.x, P.y, 700);
            if (AldaraKeys.Pressed("hp")) DrinkVial(true);
            if (AldaraKeys.Pressed("mp")) DrinkVial(false);
            if (AldaraKeys.Pressed("auto") && !AldaraHud.Typing) AldaraAuto.Set(!AldaraAuto.on);
            AldaraAuto.Tick(dt);
            if (target == null || target.dead) aiming = false;
            if (anim) anim.combat = (target != null && !target.dead) || Time.time - lastFight < 4f;
            UpdateShots(dt);
        }
        bool InRange(AldaraMonsters.Mon t) { return Mathf.Sqrt((t.x - P.x) * (t.x - P.x) + (t.y - P.y) * (t.y - P.y)) <= AttackRange(t); }

        /// called by AldaraPlayer when no movement key is held: auto-combat picks a target; chase the target into range
        /// (around mountains) and attack it; an archer on auto backs away from monsters that get close; auto-questing
        /// walks toward the quest when there is nothing to fight. mul is the share of the walking speed.
        public bool Steer(float dt, out float ang, out float mul)
        {
            ang = 0; mul = 1; if (!alive) return false;
            if (AldaraAuto.on) AldaraAuto.FindTarget(dt);
            if (target != null && target.dead) target = null;
            if (target != null)
            {
                var t = target; float d = Mathf.Sqrt((t.x - P.x) * (t.x - P.x) + (t.y - P.y) * (t.y - P.y));
                aiming = d <= AttackRange(t);
                bool walled = AldaraDungeon.Active && !AldaraDungeon.LOS(P.x, P.y, t.x, t.y, false);   // no attacking through walls
                if (walled) aiming = false;
                if (!aiming && AldaraDungeon.Active) { ang = AldaraDungeon.Steer(t.x, t.y); return true; }
                if (!aiming)
                {
                    var na = AldaraNav.Angle(t.x, t.y);
                    if (na == null) { t.navIgnore = Time.time + 12; target = null; return false; }
                    ang = na.Value; return true;
                }
                P.facing = Mathf.Atan2(t.y - P.y, t.x - P.x);
                bool back = AldaraAuto.on && cls == "archer" && d < 130;
                if (atkCd <= 0)
                {
                    atkCd = AtkCdNow(); if (AldaraAuto.on) atkCd *= AldaraAuto.ATK;
                    var S = AldaraSkills.I;
                    if (!(S && S.TryQueued(t)))
                    {
                        SkillDef sk = null; if (AldaraAuto.on && !(AldaraAuto.skT > 0)) { sk = AldaraAuto.PickSkill(t); if (sk != null) AldaraAuto.skT = AldaraAuto.SK_GAP; }
                        if (sk != null) { if (sk.kind == "target") S.CastTargetPublic(t, sk); else S.CastSelfPublic(sk); }
                        else BasicAttack(t);
                    }
                }
                if (back && target != null) { ang = P.facing + Mathf.PI; mul = 0.45f; return true; }
                return false;
            }
            aiming = false;
            var w = AldaraAuto.on && AldaraAuto.questHunt ? AldaraAuto.questWalk : null;
            if (w != null)
            {   // no quest monster alive: walk toward where they live
                var na = AldaraNav.Angle(w.x, w.y); ang = na ?? Mathf.Atan2(w.y - P.y, w.x - P.x); return true;
            }
            return false;
        }
        void StartCombo() { lastFight = Time.time; float now = Time.time; combo = now - lastSwing < 1.3f ? (combo + 1) % 3 : 0; lastSwing = now; }
        void PlayBasic() { if (!anim) return; if (cls == "knight") anim.Play("a" + (combo + 1)); else anim.Play("basic" + (combo + 1)); }
        void BasicAttack(AldaraMonsters.Mon t)
        {
            StartCombo(); PlayBasic();
            if (cls == "knight") { AldaraMonsters.I.HitMonster(t, RollDmg(1), AldaraRules.Hex("#ffd35a")); AldaraFx.Slash(t.x, t.y, P.facing, AldaraRules.Hex("#ffe07a"), t.r + 14, false); }
            else Shoot(t, Vector2.zero, cls == "mage" ? AldaraRules.Hex("#8ab4ff") : AldaraRules.Hex("#ffd35a"), cls == "mage" ? 540 : 780, cls == "mage" ? 0.35f : 0.18f, m => AldaraMonsters.I.HitMonster(m, RollDmg(1), cls == "mage" ? AldaraRules.Hex("#9fc0ff") : AldaraRules.Hex("#ffd35a")));
        }
        void FreeAttack()
        {
            if (atkCd > 0) return; atkCd = AtkCdNow(); float ang = AimAngle(); P.facing = ang;
            StartCombo(); PlayBasic();
            if (cls == "knight")
            {
                int n = 0; AldaraFx.Slash(P.x + Mathf.Cos(ang) * 34, P.y + Mathf.Sin(ang) * 34, ang, AldaraRules.Hex("#ffe07a"), 40, false);
                foreach (var m in AldaraMonsters.I.all)
                {
                    if (m.dead || n >= 3) continue; float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d > 50 + m.r) continue;
                    if (Mathf.Abs(AldaraRules.AngD(Mathf.Atan2(m.y - P.y, m.x - P.x), ang)) < 1.1f) { AldaraMonsters.I.HitMonster(m, RollDmg(1), AldaraRules.Hex("#ffd35a")); n++; }
                }
            }
            else
            {
                var c = cls == "mage" ? AldaraRules.Hex("#8ab4ff") : AldaraRules.Hex("#ffd35a");
                Shoot(null, AimPoint(true), c, cls == "mage" ? 540 : 780, cls == "mage" ? 0.35f : 0.18f, m => AldaraMonsters.I.HitMonster(m, RollDmg(1), c));
            }
        }

        // ---- hero projectiles: fly to the target (homing), or straight to a point hitting the first monster on the way ----
        class Shot { public AldaraMonsters.Mon t; public float x, y, tx, ty, spd, life; public Color c; public GameObject go; public System.Action<AldaraMonsters.Mon> hit; }
        readonly List<Shot> shots = new List<Shot>();
        public void Shoot(AldaraMonsters.Mon t, Vector2 at, Color c, float spd, float size, System.Action<AldaraMonsters.Mon> onHit)
        {
            float tip = 24; var s = new Shot { t = t, x = P.x + Mathf.Cos(P.facing) * tip, y = P.y + Mathf.Sin(P.facing) * tip, tx = at.x, ty = at.y, spd = spd, c = c, hit = onHit, life = 3 };
            if (t == null) s.life = Mathf.Sqrt((at.x - P.x) * (at.x - P.x) + (at.y - P.y) * (at.y - P.y)) / spd + 0.05f;
            s.go = AldaraFx.Orb(c, size); shots.Add(s);
        }
        void UpdateShots(float dt)
        {
            var M = AldaraMonsters.I;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var s = shots[i]; s.life -= dt;
                float tx = s.t != null ? s.t.x : s.tx, ty = s.t != null ? s.t.y : s.ty;
                float dx = tx - s.x, dy = ty - s.y, d = Mathf.Sqrt(dx * dx + dy * dy), st = s.spd * dt;
                bool done = false;
                if (s.t != null && s.t.dead) done = true;
                else if (d <= st + (s.t != null ? s.t.r * 0.5f : 0)) { if (s.t != null && s.hit != null) s.hit(s.t); else if (s.t == null) HitAt(s, tx, ty); done = true; }
                else
                {
                    s.x += dx / d * st; s.y += dy / d * st;
                    if (s.t == null) foreach (var m in M.all) { if (m.dead) continue; if ((m.x - s.x) * (m.x - s.x) + (m.y - s.y) * (m.y - s.y) < m.r * m.r) { if (s.hit != null) s.hit(m); done = true; break; } }
                    if (AldaraDungeon.Active ? AldaraDungeon.WallD(s.x, s.y) < 0 : AldaraWorld.BlockedAt(s.x, s.y)) done = true;
                }
                if (s.life <= 0) done = true;
                if (s.go) s.go.transform.position = AldaraWorld.ToUnity(s.x, s.y) + Vector3.up * 0.85f;
                if (done) { if (s.go) Destroy(s.go); shots.RemoveAt(i); }
            }
        }
        void HitAt(Shot s, float x, float y)
        {
            foreach (var m in AldaraMonsters.I.all) { if (m.dead) continue; if ((m.x - x) * (m.x - x) + (m.y - y) * (m.y - y) < (m.r + 10) * (m.r + 10)) { if (s.hit != null) s.hit(m); return; } }
        }
    }
}
