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
        public string shieldName; public Color castCol = AldaraRules.Hex("#8ab4ff"); public float cast, castMax, release, releaseMax; bool freeQueued;
        public class Swing { public float t, dur; public bool big, spin, hit; public int combo = -1; }
        public Swing swing;
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
        public int vialHp, vialMp; float vialCdHp, vialCdMp; public float VialCdHp { get { return vialCdHp; } }
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
            inventory.Add(it); if (it.rarity == "Mythical") { AldaraHud.Banner("MYTHICAL DROP: " + it.name + "!"); AldaraChat.Sys(heroName + " found a Mythical: " + it.name + "!", "myth"); AldaraSkills.I.Later_(1.2f, () => AldaraGear.CheckTitles()); } return true;
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
            if (health) { AldaraSound.Vial(true); vialHp--; vialCdHp = 2; float amt = Mathf.Round(maxHp * 0.4f), hb = hp; hp = Mathf.Min(maxHp, hp + amt); DStat.Heal(hp - hb); AldaraFx.Text(P.x, P.y - 26, "+" + amt + " HP", AldaraRules.Hex("#7fe07f")); }
            else { AldaraSound.Vial(false); vialMp--; vialCdMp = 2; float amt = Mathf.Round(maxMana * 0.5f); mana = Mathf.Min(maxMana, mana + amt); AldaraFx.Text(P.x, P.y - 26, "+" + amt + " MP", AldaraRules.Hex("#8aa8ff")); }
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
            xp += Mathf.Round(Mathf.Round(n) * (1 + AldaraTree.T("xp"))); if (xp >= xpNeed) AldaraSound.LevelUp();
            while (xp >= xpNeed)
            {
                xp -= xpNeed; lvl++; xpNeed = AldaraRules.XpNeedFor(lvl);
                baseHp += 18; baseAtk += 3; statPoints += 10; AldaraTree.sp++; AldaraTree.spTotal++;
                Recompute(); hp = maxHp; mana = maxMana;
                AldaraHud.Banner("LEVEL UP! Lv." + lvl); AldaraSave.Dirty(); AldaraSkills.I.Later_(1.5f, () => AldaraGear.CheckTitles());
                if (lvl == AldaraTree.B.SUB_LEVEL && AldaraTree.sub == null) AldaraSkills.I.Later_(1.8f, () => { AldaraHud.Banner("You can now choose a subclass: open Skills (K)"); if (AldaraWindows.I) AldaraWindows.I.Toggle("sub", true); });
            }
        }
        public float RollDmg(float mult)
        {   // rollDmg: War Cry, the tree's damage bonus, and its critical strikes (x1.6)
            float b = atkBuff > 0 ? 1.3f : 1; float d = atk * mult * b * (0.8f + Random.value * 0.5f) * (1 + AldaraTree.T("dmg"));
            float cr = AldaraTree.T("crit"); if (cr > 0 && Random.value < cr) { d *= 1.6f; critFrame = Time.frameCount; }
            d = Mathf.Round(d);
            return d;
        }
        float AttackRange(AldaraMonsters.Mon t) { return cls == "archer" ? 280 : cls == "mage" ? 210 : t.r + 34; }
        float AtkCdNow() { return ATK_CD[cls] * (hasteBuff > 0 ? 0.6f : 1) / AldaraRelics.SpeedMult(); }
        public void Cast(float t) { cast = castMax = t; }
        public void Release(float t) { release = releaseMax = t; }
        public void StartSwing(float dur, bool big, int combo = -1, bool spin = false) { swing = new Swing { dur = dur, big = big, combo = combo, spin = spin }; }
        public void MarkFight() { lastFight = Time.time; }
        /// in a fight: a living target, or fighting in the last few seconds (out of it the weapon is put away)
        public bool InFight { get { return (target != null && !target.dead) || Time.time - lastFight < 6f || swing != null || cast > 0; } }

        /// the monster whose blow this is (LV_SRC), for Riposte and Thorns
        public static AldaraMonsters.Mon LvSrc;
        public void Damage(float dmg, float ang, int srcLvl)
        {
            if (!alive) return; bool env = srcLvl < 0; AldaraPlayer.lastCombat = Time.time;
            dmg = AldaraSubclass.PreDamage(dmg, LvSrc, env); if (dmg < 0) return;
            float hp0 = hp, sh0 = shield; DamageCore(dmg, ang, srcLvl); AldaraSound.Damaged(sh0, hp0);
            AldaraSubclass.PostDamage(Mathf.Max(0, hp0 - hp), LvSrc, env);
        }
        void DamageCore(float dmg, float ang, int srcLvl)
        {
            // the level gap: the attacker's level, else the highest-level monster fighting you (lvThreat); environment damage (srcLvl < 0) is not scaled
            if (srcLvl == 0) { AldaraMonsters.Mon best = null; foreach (var m in AldaraMonsters.I.all) { if (m.dead || !(m.aggroT > 0 || m.act != null)) continue; float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d < 900 && m.lvl > (best != null ? best.lvl : -1)) best = m; } if (best != null) srcLvl = best.lvl; }
            if (srcLvl > 0) dmg = Mathf.Max(1, Mathf.Round(dmg * AldaraRules.LvIn(srcLvl - lvl)));
            float blk = AldaraTree.T("block"); if (blk > 0) dmg = Mathf.Round(dmg * (1 - blk));
            AldaraPlayer.lastCombat = Time.time;
            dmg = AldaraRelics.OnDamage(dmg, ang);
            if (shield > 0) { float ab = Mathf.Min(shield, dmg); shield -= ab; dmg -= ab; if (ab > 0) AldaraFx.Text(P.x + 14, P.y - 26, "(" + ab + ")", AldaraRules.Hex("#8ab4ff")); }
            hp -= dmg; DStat.Taken(dmg); hurt = dmg > 0 ? 0.15f : 0; lastFight = Time.time;
            AldaraVfx.Burst(P.x - Mathf.Cos(ang) * 8, P.y - Mathf.Sin(ang) * 8, AldaraRules.Hex("#ff6a6a"), 5, 110);
            if (dmg > 0) AldaraFx.Text(P.x, P.y - 26, "-" + dmg, AldaraRules.Hex("#ff6a6a"));
            if (AldaraRelics.AfterDamage()) return;
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
            AldaraHud.Banner(AldaraDungeon.Active ? "You have fallen... the run is over" : "You have fallen...");
        }

        /// kdRespawn: you wake at the nearest waystone you know, if it is nearer than Lorenmar
        public static Vector2 Respawn(float x, float y)
        {
            AldaraWaystones.Ws best = null; float bd = Vector2.Distance(new Vector2(x, y), AldaraWorld.TOWN_SPAWN);
            foreach (var w in AldaraWaystones.KnownList()) { float d = Vector2.Distance(new Vector2(x, y), new Vector2(w.x, w.y)); if (d < bd) { bd = d; best = w; } }
            if (best == null) return AldaraWorld.TOWN_SPAWN;
            AldaraHud.Banner("You wake at " + best.n); return AldaraPlayer.FreeSpotNear(best.x + 40, best.y + 90);
        }
        float kdT;
        /// kdTick: the first time you reach Valcrest
        void KdTick(float dt)
        {
            if (AldaraDungeon.Active) return; kdT -= dt; if (kdT > 0) return; kdT = 0.5f;
            if (!AldaraWorld.InCity(P.x, P.y)) return; var raw = AldaraSave.Raw; if (raw == null) return;
            var kd = raw["kd"] as Newtonsoft.Json.Linq.JObject; if (kd == null) { kd = new Newtonsoft.Json.Linq.JObject(); raw["kd"] = kd; }
            if (kd["arrived"] != null) return; kd["arrived"] = 1;
            AldaraHud.Banner("Valcrest, the Crown City of Aldara"); AldaraChat.Sys(heroName + " has reached Valcrest, the Crown City", "myth");
            float xp = Mathf.Max(5000, Mathf.Round(xpNeed * 0.15f)); GainXp(xp); AldaraFx.Text(P.x, P.y - 86, "+" + xp + " XP  You reached Valcrest", AldaraRules.Hex("#ffd35a")); AldaraSave.Dirty();
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
        public void PlaySkill(string id) { lastFight = Time.time; if (anim) anim.Play(id); AldaraHudBar.PopAbility(id); }

        void Update()
        {
            float dt = Mathf.Min(Time.deltaTime, 0.1f);
            if (!AldaraSave.Ready) return;
            if ((cls == "knight" || cls == "mage" || cls == "archer") && !AldaraKnightFx.I && AldaraKnightFx.Live) AldaraKnightFx.Ensure();   // paint the knight's effects before the first swing
            if (!anim) anim = GetComponentInChildren<AldaraCharacterAnimator>();
            if (hurt > 0) hurt -= dt; if (slowT > 0) slowT -= dt; if (atkBuff > 0) atkBuff -= dt; if (hasteBuff > 0) hasteBuff -= dt;
            if (shieldT > 0) { shieldT -= dt; if (shieldT <= 0) shield = 0; }
            if (vialCdHp > 0) vialCdHp -= dt; if (vialCdMp > 0) vialCdMp -= dt;
            if (!alive)
            {
                if (AldaraRaid.On) { UpdateShots(dt); return; }   // the raid decides when you rise
                if (Time.time - deadAt > 1.8f)
                {   // back to town, as the browser does outside dungeons
                    if (AldaraDungeon.Active) AldaraDungeon.Exit("fail"); else { var rs = Respawn(P.x, P.y); P.x = rs.x; P.y = rs.y; } alive = true; hp = maxHp; mana = maxMana; gold = Mathf.Floor(gold * 0.9f); target = null;
                    if (anim) anim.Load();
                }
                UpdateShots(dt); return;
            }
            if (atkCd > 0) atkCd -= dt;
            TickSwing(dt); KdTick(dt); AldaraGuild.Tick(dt);
            if (cls == "mage" && cast > 0 && Random.value < dt * 40) { float tx = P.x + Mathf.Cos(P.facing) * 20, ty = P.y + Mathf.Sin(P.facing) * 20, aa = Random.value * Mathf.PI * 2; AldaraVfx.Mpush(new AldaraVfx.Mp { x = tx + Mathf.Cos(aa) * 20, y = ty + Mathf.Sin(aa) * 10, z = AldaraVfx.PH_Y + 10 + Mathf.Sin(aa) * 10, vx = -Mathf.Cos(aa) * 60, vy = -Mathf.Sin(aa) * 30, life = 0.3f, max = 0.3f, c = castCol, s = 2, glow = true }); }
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
            if (kb != null && (AldaraKeys.Held("attack") || freeQueued) && atkCd <= 0 && !(target != null && !target.dead && aiming)) FreeAttack();
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
        void StartCombo() { lastFight = Time.time; float now = Time.time; combo = now - lastSwing < 1.3f ? (combo + 1) % 3 : 0; lastSwing = now; StartSwing(new[] { 0.3f, 0.3f, 0.42f }[combo], false, combo); }
        /// the swing's landing: big blows and the overhead chop shake the ground (updateFx)
        void TickSwing(float dt)
        {
            if (swing == null) return; var sw = swing; sw.t += dt;
            if (!sw.hit && !sw.spin && (sw.big || sw.combo == 2) && sw.t / sw.dur >= 0.56f)
            {
                sw.hit = true; float ix = P.x + Mathf.Cos(P.facing) * 34, iy = P.y + Mathf.Sin(P.facing) * 34; var tc = AldaraSkillFx.TrailC;
                if (sw.big) AldaraVfx.Mfx(new AldaraVfx.Mf { k = "shock", x = ix, y = iy, r = 66, c = tc, c2 = tc, T = 0.4f });
                AldaraVfx.Mfx(new AldaraVfx.Mf { k = "flash", x = ix, y = iy, h = 6, r = 34, c = tc, c2 = Color.white, T = 0.2f });
                AldaraVfx.Shake(sw.big ? 5 : 3.5f, 0.22f); AldaraVfx.Burst(ix, iy, AldaraRules.Hex("#e8d8b0"), 12, 170); AldaraVfx.Burst(ix, iy, tc, 8, 120);
            }
            if (sw.t >= sw.dur) swing = null;
            if (cast > 0) cast -= dt; if (release > 0) release -= dt;
        }
        void PlayBasic() { AldaraHudBar.PopAbility(cls == "knight" ? "a" + (combo + 1) : "basic"); if (!anim) return; if (cls == "knight") anim.Play("a" + (combo + 1)); else anim.Play("basic" + (combo + 1)); }
        void BasicAttack(AldaraMonsters.Mon t)
        {
            AldaraSound.Basic();
            if (cls == "knight") { StartCombo(); PlayBasic(); AldaraSkillFx.KnightSlash(t.x, t.y, combo, swing.dur); AldaraMonsters.I.HitMonster(t, RollDmg(1), AldaraRules.Hex("#ffd35a")); AldaraVfx.Burst(t.x, t.y, AldaraRules.Hex("#ffe8a0"), 6, 120); }
            else if (cls == "mage")
            {
                StartCombo(); PlayBasic(); Cast(0.25f); castCol = AldaraRules.Hex("#8ab4ff");
                Fire("bolt", t, m => { AldaraMonsters.I.HitMonster(m, RollDmg(1), AldaraRules.Hex("#9fc0ff")); AldaraVfx.Burst(m.x, m.y, AldaraRules.Hex("#8ab4ff"), 8, 140); if (!AldaraMageFx.On) { var f = AldaraVfx.Effect("explode", m.x, m.y, 30, 0.3f, Color.white); f.c1 = AldaraRules.Hex("#8ab4ff"); f.c2 = AldaraRules.Hex("#4a5aff"); f.core = Color.white; } AldaraSkillFx.Pfx("bolt_hit", m, m.x, m.y, null, null, "#8ab4ff"); });
            }
            else
            {
                StartCombo(); PlayBasic(); Release(0.15f); AldaraSkillFx.Pfx("muzzle", null, 0, 0, null, null, "#fff0c0");
                Fire("arrow", t, m => { AldaraMonsters.I.HitMonster(m, RollDmg(1), AldaraRules.Hex("#ffd35a")); AldaraVfx.Burst(m.x, m.y, AldaraRules.Hex("#e8d8b0"), 4, 90); });
            }
        }
        void FreeAttack()
        {
            if (!alive) return; if (atkCd > 0) { freeQueued = true; return; }
            freeQueued = false; atkCd = AtkCdNow(); float ang = AimAngle(); P.facing = ang;
            if (cls == "knight")
            {
                StartCombo(); PlayBasic(); var hits = new List<AldaraMonsters.Mon>();
                foreach (var m in AldaraMonsters.I.all)
                {
                    if (m.dead || !AldaraDungeon.Reachable(m)) continue; float d = Mathf.Sqrt((m.x - P.x) * (m.x - P.x) + (m.y - P.y) * (m.y - P.y)); if (d > 50 + m.r) continue;
                    if (Mathf.Abs(AldaraRules.AngD(Mathf.Atan2(m.y - P.y, m.x - P.x), ang)) < 1.1f) hits.Add(m);
                }
                AldaraSkillFx.KnightSlash(P.x + Mathf.Cos(ang) * 34, P.y + Mathf.Sin(ang) * 34, combo, swing.dur); AldaraSound.FreeSwing();
                for (int i = 0; i < hits.Count && i < 3; i++) { AldaraMonsters.I.HitMonster(hits[i], RollDmg(1), AldaraRules.Hex("#ffd35a")); AldaraVfx.Burst(hits[i].x, hits[i].y, AldaraRules.Hex("#ffe8a0"), 6, 120); }
            }
            else { var at = AimPoint(true); BasicAttack(AldaraSkills.Dummy(at.x, at.y)); }
        }

        // ---- hero projectiles (fire / updateFx / PROJ3): home in on the target while it lives; aimed at the ground they hit the first monster they pass ----
        public class HProj { public string kind; public float x, y, tx, ty, spd, ang, d0 = -1, wob; public AldaraMonsters.Mon t; public System.Action<AldaraMonsters.Mon> fn; public string glow, color; public List<Vector2> trail = new List<Vector2>(); public AldaraKnightFx.Fx fx; }
        public readonly List<HProj> projectiles = new List<HProj>();
        public void Fire(string kind, AldaraMonsters.Mon t, System.Action<AldaraMonsters.Mon> fn, string glow = null, string color = null, float spd = 0)
        {
            float tx = P.x + Mathf.Cos(P.facing) * 24, ty = P.y + Mathf.Sin(P.facing) * 24;
            if (spd <= 0) spd = kind == "arrow" ? 780 : kind == "fireball" ? 430 : 540;
            var np = new HProj { kind = kind, x = tx, y = ty, t = t, tx = t.x, ty = t.y, spd = spd, fn = fn, ang = P.facing, glow = glow, color = color };
            if (kind != "arrow" && AldaraMageFx.On) np.fx = AldaraMageFx.Proj(kind, color);   // the enhanced 3D shot
            else if (kind == "arrow" && AldaraArcherFx.On) np.fx = AldaraArcherFx.Proj(glow);   // an arrow of light
            projectiles.Add(np);
        }
        void UpdateShots(float dt)
        {
            if (!hooked) { hooked = true; AldaraVfx.DrawAir += DrawProj; }
            for (int i = projectiles.Count - 1; i >= 0; i--)
            {
                var pr = projectiles[i];
                if (!pr.t.dead) { pr.tx = pr.t.x; pr.ty = pr.t.y; }
                if (pr.t.dummy)
                {
                    AldaraMonsters.Mon hit = null;
                    foreach (var m in AldaraMonsters.I.all) { if (m.dead || !AldaraDungeon.Reachable(m) || Mathf.Abs(m.x - pr.x) > 80 || Mathf.Abs(m.y - pr.y) > 80) continue; if (Mathf.Sqrt((m.x - pr.x) * (m.x - pr.x) + (m.y - pr.y) * (m.y - pr.y)) < m.r + 6) { hit = m; break; } }
                    if (hit != null) { projectiles.RemoveAt(i); Land(pr, hit); pr.fn(hit); continue; }
                }
                float dx = pr.tx - pr.x, dy = pr.ty - pr.y, d = Mathf.Sqrt(dx * dx + dy * dy); pr.ang = Mathf.Atan2(dy, dx);
                float step = pr.spd * dt;
                if (d <= step + (pr.t.dead ? 4 : pr.t.r * 0.6f)) { projectiles.RemoveAt(i); Land(pr, pr.t.dead ? null : pr.t); if (!pr.t.dead) pr.fn(pr.t); continue; }
                pr.x += dx / d * step; pr.y += dy / d * step;
                if (pr.fx != null)
                {   // the 3D shot follows, arcing like the browser's arcane missiles
                    if (pr.d0 < 0) { pr.d0 = Mathf.Max(1, d); pr.wob = (Random.value < 0.5f ? -1 : 1) * (40 + Random.value * 50); }
                    float prog = Mathf.Clamp01(1 - d / pr.d0), off = pr.color != null && pr.kind == "bolt" ? Mathf.Sin(prog * Mathf.PI) * pr.wob : 0;
                    AldaraMageFx.Move(pr.fx, pr.x - Mathf.Sin(pr.ang) * off, pr.y + Mathf.Cos(pr.ang) * off); continue;
                }
                if (pr.kind == "fireball" && Random.value < 0.8f) AldaraVfx.Particle(pr.x, pr.y, (Random.value - 0.5f) * 40, (Random.value - 0.5f) * 40, 0.3f, AldaraRules.Hex(Random.value < 0.5f ? "#ff8a3a" : "#ffd35a"), 3);
                if (pr.kind == "bolt" && Random.value < 0.5f) AldaraVfx.Particle(pr.x, pr.y, 0, 0, 0.2f, AldaraRules.Hex("#8ab4ff"), 2);
            }
        }
        static void Land(HProj pr, AldaraMonsters.Mon m) { if (pr.fx != null && pr.kind == "arrow") AldaraArcherFx.Impact(m, pr.glow); AldaraMageFx.End(pr.fx); }
        bool hooked; void OnDestroy() { if (hooked) AldaraVfx.DrawAir -= DrawProj; }
        static List<Vector2> TrailOf(HProj pr, float x, float y, int n) { pr.trail.Add(new Vector2(x, y)); if (pr.trail.Count > n) pr.trail.RemoveAt(0); return pr.trail; }
        static Vector2 Rt(float x, float y, float a, float lx, float ly) { float c = Mathf.Cos(a), s = Mathf.Sin(a); return new Vector2(x + c * lx - s * ly, y + s * lx + c * ly); }
        void DrawProj(AldaraVfx V)
        {
            var aN = V.AirN; var aA = V.AirA; float LIFT = AldaraVfx.LIFT;
            foreach (var pr in projectiles)
            {
                if (pr.fx != null) continue;   // drawn in 3D
                float bz = AldaraVfx.LZ(pr.x, pr.y);
                switch (pr.kind)
                {
                    case "arrow":
                        {
                            float x = pr.x, y = pr.y - LIFT * 0.8f, a = pr.ang; var T = TrailOf(pr, x, y, 7); Color? col = pr.glow != null ? AldaraRules.Hex(pr.glow) : (Color?)null;
                            aN.Ellipse(pr.x, pr.y, 7, 2, new Color(0, 0, 0, 0.25f), bz, 0, 12);
                            if (T.Count > 1) aA.LineC(T[0].x, T[0].y, x, y, col.HasValue ? 5 : 2, AldaraVfx.A(col ?? Color.white, 0), AldaraVfx.A(col ?? Color.white, col.HasValue ? 0.8f : 0.35f), bz);
                            if (col.HasValue) AldaraVfx.Glow(aA, x, y, 16, col.Value, 0.7f, bz);
                            var p0 = Rt(x, y, a, -22, 0); var p1 = Rt(x, y, a, 4, 0); aN.Line(p0.x, p0.y, p1.x, p1.y, 2, AldaraRules.Hex("#8a6a40"), bz);
                            aN.Poly(new[] { Rt(x, y, a, 11, 0), Rt(x, y, a, 2, -4), Rt(x, y, a, 4, 0), Rt(x, y, a, 2, 4) }, col ?? AldaraRules.Hex("#d8dce8"), bz);
                            aN.Poly(new[] { Rt(x, y, a, -22, 0), Rt(x, y, a, -27, -5), Rt(x, y, a, -17, 0), Rt(x, y, a, -27, 5) }, col.HasValue ? Color.white : AldaraRules.Hex("#c04a4a"), bz);
                            break;
                        }
                    case "fireball":
                        {
                            float x = pr.x, y = pr.y - LIFT, t = Time.time * 1000 / 90; var T = TrailOf(pr, x, y, 12);
                            for (int i = 0; i < T.Count; i++) { float s = (i + 1f) / T.Count; AldaraVfx.Glow(aA, T[i].x, T[i].y, 6 + 16 * s, AldaraRules.Hex(i % 2 == 1 ? "#ff5a1a" : "#ff9a3a"), 0.5f * s, bz); }
                            AldaraVfx.Glow(aA, x, y, 34, AldaraRules.Hex("#ff6a1a"), 0.55f, bz); AldaraVfx.Glow(aA, x, y, 16, AldaraRules.Hex("#ffd35a"), 0.9f, bz);
                            for (int i = 0; i < 5; i++) { float a = t + i * 1.26f; AldaraVfx.Circle(aA, x + Mathf.Cos(a) * 9, y + Mathf.Sin(a) * 9, 3.2f, new Color(1, 170 / 255f, 60 / 255f, 0.7f), bz); }
                            AldaraVfx.Circle(aA, x, y, 5.5f, AldaraRules.Hex("#fff8e0"), bz);
                            if (Random.value < 0.9f) AldaraVfx.Particle(x + (Random.value - 0.5f) * 8, pr.y + (Random.value - 0.5f) * 8, (Random.value - 0.5f) * 50, -20 - Random.value * 40, 0.35f, AldaraRules.Hex(Random.value < 0.5f ? "#ff8a3a" : "#ffd35a"), 3);
                            break;
                        }
                    case "ice":
                        {
                            float x = pr.x, y = pr.y - LIFT, a = pr.ang; var T = TrailOf(pr, x, y, 10);
                            for (int i = 0; i < T.Count; i++) { float s = (i + 1f) / T.Count; AldaraVfx.Glow(aA, T[i].x, T[i].y, 4 + 10 * s, AldaraRules.Hex("#8fdfff"), 0.45f * s, bz); }
                            AldaraVfx.Glow(aA, x, y, 26, AldaraRules.Hex("#8fdfff"), 0.6f, bz);
                            aN.Poly(new[] { Rt(x, y, a, 16, 0), Rt(x, y, a, 0, -5), Rt(x, y, a, -12, 0), Rt(x, y, a, 0, 5) }, AldaraRules.Hex("#e8f8ff"), bz);
                            aN.Tri(Rt(x, y, a, 16, 0), Rt(x, y, a, 0, 5), Rt(x, y, a, -12, 0), AldaraRules.Hex("#8fdfff"), bz);
                            foreach (var sd in new[] { -1, 1 }) aN.Tri(Rt(x, y, a, 2, 0), Rt(x, y, a, -6, sd * 9), Rt(x, y, a, -2, 0), AldaraRules.Hex("#bfefff"), bz);
                            if (Random.value < 0.6f) AldaraVfx.Particle(x, pr.y, (Random.value - 0.5f) * 30, (Random.value - 0.5f) * 30, 0.3f, AldaraRules.Hex("#dff6ff"), 2);
                            break;
                        }
                    default:
                        {   // bolt: the mage's spark; with a colour it arcs (arcane missiles)
                            var col = AldaraRules.Hex(pr.color ?? "#8ab4ff"); bool arc = pr.color != null;
                            if (pr.d0 < 0) { pr.d0 = Mathf.Max(1, Mathf.Sqrt((pr.tx - pr.x) * (pr.tx - pr.x) + (pr.ty - pr.y) * (pr.ty - pr.y))); pr.wob = (Random.value < 0.5f ? -1 : 1) * (40 + Random.value * 50); }
                            float d = Mathf.Sqrt((pr.tx - pr.x) * (pr.tx - pr.x) + (pr.ty - pr.y) * (pr.ty - pr.y)), prog = Mathf.Clamp01(1 - d / pr.d0), off = arc ? Mathf.Sin(prog * Mathf.PI) * pr.wob : 0;
                            float x = pr.x - Mathf.Sin(pr.ang) * off, y = pr.y - LIFT + Mathf.Cos(pr.ang) * off, t = Time.time * 1000 / 120; var T = TrailOf(pr, x, y, arc ? 14 : 9);
                            for (int i = 1; i < T.Count; i++) { float s = i / (float)T.Count; aA.Line(T[i - 1].x, T[i - 1].y, T[i].x, T[i].y, 2 + 7 * s, AldaraVfx.A(col, 0.6f * s), bz); }
                            AldaraVfx.Glow(aA, x, y, 26, col, 0.6f, bz); AldaraVfx.Glow(aA, x, y, 10, Color.white, 0.9f, bz);
                            if (arc) { var pts = new List<Vector2>(); for (int i = 0; i < 8; i++) { float rr = i % 2 == 1 ? 3 : 9, a = i / 8f * Mathf.PI * 2; pts.Add(Rt(x, y, t, Mathf.Cos(a) * rr, Mathf.Sin(a) * rr)); } aA.Poly(pts, Color.white, bz); }
                            else { AldaraVfx.Circle(aA, x, y, 4, Color.white, bz); aA.Arc(x, y, 8, 8, t, t + 2, 1.2f, AldaraVfx.A(AldaraRules.Hex("#cfe0ff"), 0.8f), bz); aA.Arc(x, y, 8, 8, t + 3.14f, t + 5.1f, 1.2f, AldaraVfx.A(AldaraRules.Hex("#cfe0ff"), 0.8f), bz); }
                            break;
                        }
                }
            }
            // the barrier bubble while a shield holds (drawBarrier)
            if (alive && shield > 0) Barrier(aA, P.x, P.y, cls == "mage" ? AldaraRules.Hex("#8ab4ff") : AldaraRules.Hex("#ffd35a"));
        }
        static void Barrier(AldaraSketch s, float x, float y, Color col)
        {
            float t = Time.time, R = 30, Hh = 44, cy = y - 10, bz = AldaraVfx.LZ(x, y);
            s.Glow(x, cy, R, Hh, new[] { 0, 0.3f, 0.8f, 1 }, new[] { AldaraVfx.A(col, 0), AldaraVfx.A(col, 0), AldaraVfx.A(col, 0.12f), AldaraVfx.A(col, 0.4f) }, bz, 28);
            AldaraVfx.Ring(s, x, cy, R, Hh, 1.8f, AldaraVfx.A(col, 0.75f + 0.25f * Mathf.Sin(t * 4)), bz);
            for (int i = 0; i < 14; i++)
            {
                float a = i / 14f * Mathf.PI * 2 + t * 0.6f, sx = Mathf.Cos(a); if (Mathf.Sin(a) < -0.2f) continue;
                float px = x + sx * R * 0.82f, py = cy + ((i * 0.618f) % 1 - 0.5f) * Hh * 1.4f, sz = 5 * (0.5f + 0.5f * Mathf.Sin(a)); var hex = new List<Vector2>();
                for (int j = 0; j <= 6; j++) { float b = j / 6f * Mathf.PI * 2; hex.Add(new Vector2(px + Mathf.Cos(b) * sz * 0.8f, py + Mathf.Sin(b) * sz)); }
                s.Polyline(hex, 1, AldaraVfx.A(Color.white, 0.35f * Mathf.Sin(a) + 0.15f), false, bz);
            }
            for (int k = 0; k < 2; k++) AldaraVfx.Ring(s, x, cy + (k == 1 ? 10 : -10), R * 1.05f, R * 0.3f, 1.3f, AldaraVfx.A(k == 1 ? Color.white : col, 0.55f), bz, Mathf.Sin(t * (k == 1 ? 1.3f : -1.1f)) * 0.35f);
        }
    }
}
