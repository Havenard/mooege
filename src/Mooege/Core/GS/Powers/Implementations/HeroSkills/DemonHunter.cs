﻿/*
 * Copyright (C) 2011 mooege project
 *
 * This program is free software; you can redistribute it and/or modify
 * it under the terms of the GNU General Public License as published by
 * the Free Software Foundation; either version 2 of the License, or
 * (at your option) any later version.
 *
 * This program is distributed in the hope that it will be useful,
 * but WITHOUT ANY WARRANTY; without even the implied warranty of
 * MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
 * GNU General Public License for more details.
 *
 * You should have received a copy of the GNU General Public License
 * along with this program; if not, write to the Free Software
 * Foundation, Inc., 59 Temple Place, Suite 330, Boston, MA  02111-1307  USA
 */

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Mooege.Core.GS.Ticker;
using Mooege.Net.GS.Message.Definitions.Effect;
using Mooege.Core.GS.Common.Types.Math;
using Mooege.Core.GS.Powers.Payloads;
using Mooege.Core.GS.Common.Types.TagMap;
using Mooege.Core.GS.Actors;
using Mooege.Core.GS.Actors.Movement;

namespace Mooege.Core.GS.Powers.Implementations
{
    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredGenerators.Grenades)]
    public class DemonHunterGrenades : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            GeneratePrimaryResource(ScriptFormula(25));

            float targetDistance = PowerMath.Distance2D(User.Position, TargetPosition);
            
            // create grenade projectiles with shared detonation timer
            TickTimer timeout = WaitSeconds(ScriptFormula(2));
            Projectile[] grenades = new Projectile[Rune_C > 0 ? 1 : 3];
            for (int i = 0; i < grenades.Length; ++i)
            {
                var projectile = new Projectile(this, Rune_C > 0 ? 212547 : 88244, User.Position);
                projectile.Timeout = timeout;
                grenades[i] = projectile;
            }

            // generate spread positions with distance-scaled spread amount.
            float scaledSpreadOffset = Math.Max(targetDistance - ScriptFormula(14), 0f);
            Vector3D[] projDestinations = PowerMath.GenerateSpreadPositions(User.Position, TargetPosition,
                ScriptFormula(11) - scaledSpreadOffset, grenades.Length);

            // launch and bounce grenades
            yield return WaitTicks(1);  // helps make bounce timings more consistent

            float bounceOffset = 1f;
            float minHeight = ScriptFormula(21);
            float height = minHeight + ScriptFormula(22);
            float bouncePercent = 0.7f; // ScriptFormula(23);
            while (!timeout.TimedOut)
            {
                for (int i = 0; i < grenades.Length; ++i)
                {
                    grenades[i].LaunchArc(PowerMath.TranslateDirection2D(projDestinations[i], User.Position, projDestinations[i],
                                                                          targetDistance * 0.3f * bounceOffset),
                                          height, ScriptFormula(20));
                }

                height *= bouncePercent;
                bounceOffset *= 0.3f;

                yield return grenades[0].ArrivalTime;
                // play "dink dink" grenade bounce sound
                grenades[0].PlayEffect(Effect.Unknown69);
            }

            // damage effects
            for (int i = 0; i < grenades.Length; ++i)
            {
                SpawnEffect(RuneSelect(154027, 154045, 154028, 154044, 154046, 154043), grenades[i].Position);
                if (Rune_A > 0)
                    SpawnEffect(154076, grenades[i].Position, 0, WaitSeconds(ScriptFormula(7)));

                AttackPayload attack = new AttackPayload(this);
                attack.Targets = GetEnemiesInRadius(grenades[i].Position, ScriptFormula(4));
                attack.AddWeaponDamage(ScriptFormula(0), Rune_A > 0 ? DamageType.Poison : DamageType.Fire);
                attack.OnHit = (hitPayload) =>
                {
                    if (Rune_E > 0)
                    {
                        if (Rand.NextDouble() < ScriptFormula(9))
                            AddBuff(hitPayload.Target, new DebuffStunned(WaitSeconds(ScriptFormula(10))));
                    }
                    if (Rune_C > 0)
                        Knockback(grenades[i].Position, hitPayload.Target, ScriptFormula(8));
                };
                attack.Apply();
            }

            // TODO replace with better pool effect system
            // poison pool effect
            if (Rune_A > 0)
            {
                TickTimer cloudTimeout = WaitSeconds(ScriptFormula(7));
                while (!cloudTimeout.TimedOut)
                {
                    for (int i = 0; i < grenades.Length; ++i)
                    {
                        WeaponDamage(GetEnemiesInRadius(grenades[i].Position, ScriptFormula(5)), ScriptFormula(6), DamageType.Poison);
                    }
                    yield return WaitSeconds(1f);
                }
            }

            // clusterbomb hits
            if (Rune_B > 0)
            {
                int damagePulses = (int)ScriptFormula(28);
                for (int pulse = 0; pulse < damagePulses; ++pulse)
                {
                    yield return WaitSeconds(ScriptFormula(12) / damagePulses);

                    for (int i = 0; i < grenades.Length; ++i)
                    {
                        WeaponDamage(GetEnemiesInRadius(grenades[i].Position, ScriptFormula(4)), ScriptFormula(0), DamageType.Fire);
                    }
                }
            }
        }
    }

    [ImplementsPowerSNO(Skills.Skills.DemonHunter.HatredSpenders.RainOfVengeance)]
    public class DemonHunterRainOfVengeance : Skill
    {
        public override IEnumerable<TickTimer> Main()
        {
            //StartDefaultCooldown();
            UsePrimaryResource(EvalTag(PowerKeys.ResourceCost));

            // ground summon effect for rune c
            if (Rune_C > 0)
                SpawnProxy(TargetPosition).PlayEffectGroup(152294);

            // startup delay all version of skill have
            yield return WaitSeconds(ScriptFormula(3));

            IEnumerable<TickTimer> subScript;
            if (Rune_A > 0)
                subScript = _RuneA();
            else if (Rune_B > 0)
                subScript = _RuneB();
            else if (Rune_C > 0)
                subScript = _RuneC();
            else if (Rune_D > 0)
                subScript = _RuneD();
            else if (Rune_E > 0)
                subScript = _RuneE();
            else
                subScript = _NoRune();

            foreach (var timeout in subScript)
                yield return timeout;
        }

        IEnumerable<TickTimer> _RuneA()
        {
            Vector3D castedPosition = new Vector3D(User.Position);

            int demonCount = (int)ScriptFormula(23);
            for (int n = 0; n < demonCount; ++n)
            {
                var attackDelay = WaitSeconds(ScriptFormula(20));
                var demonPosition = RandomDirection(castedPosition, ScriptFormula(24), ScriptFormula(25));

                var demon = SpawnEffect(149949, demonPosition, ScriptFormula(22), WaitSeconds(5.0f));                    
                demon.OnUpdate = () =>
                {
                    if (attackDelay.TimedOut)
                    {
                        demon.PlayEffectGroup(152590);
                        WeaponDamage(GetEnemiesInRadius(demonPosition, ScriptFormula(27)), ScriptFormula(26), DamageType.Fire);

                        demon.OnUpdate = null;
                    }
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }

        IEnumerable<TickTimer> _NoRune()
        {
            _CreateArrowPool(131701, new Vector3D(User.Position), ScriptFormula(6), ScriptFormula(7));
            yield break;
        }

        IEnumerable<TickTimer> _RuneB()
        {
            Vector3D castedPosition = new Vector3D(User.Position);

            TickTimer timeout = WaitSeconds(ScriptFormula(16));
            while (!timeout.TimedOut)
            {
                TargetList targets = GetEnemiesInRadius(castedPosition, ScriptFormula(18));
                if (targets.Actors.Count > 0)
                    _CreateArrowPool(153029, targets.Actors[Rand.Next(targets.Actors.Count)].Position, ScriptFormula(28), ScriptFormula(34));

                yield return WaitSeconds(ScriptFormula(38));
            }
        }

        void _CreateArrowPool(int actorSNO, Vector3D position, float duration, float radius)
        {
            var pool = SpawnEffect(actorSNO, position, 0, WaitSeconds(duration));
            pool.OnUpdate = () =>
            {
                TargetList targets = GetEnemiesInRadius(position, radius);
                targets.Actors.RemoveAll((actor) => Rand.NextDouble() > ScriptFormula(10));
                targets.ExtraActors.RemoveAll((actor) => Rand.NextDouble() > ScriptFormula(10));

                WeaponDamage(targets, ScriptFormula(0), DamageType.Physical);

                // rewrite delay every time for variation: base wait time * variation * user attack speed
                pool.UpdateDelay = (ScriptFormula(5) + (float)Rand.NextDouble() * ScriptFormula(2)) * (1.0f / ScriptFormula(9));
            };
        }

        IEnumerable<TickTimer> _RuneC()
        {
            var demon = new Projectile(this, 155276, TargetPosition);
            demon.Timeout = WaitSeconds(ScriptFormula(30));

            TickTimer grenadeTimer = null;
            demon.OnUpdate = () =>
            {
                if (grenadeTimer == null || grenadeTimer.TimedOut)
                {
                    grenadeTimer = WaitSeconds(ScriptFormula(31));

                    demon.PlayEffect(Effect.Sound, 215621);

                    var grenade = new Projectile(this, 152589, new Vector3D(demon.Position.X, demon.Position.Y, demon.Position.Z + 18f));
                    grenade.Timeout = WaitSeconds(ScriptFormula(33));
                    grenade.OnTimeout = () =>
                    {
                        grenade.PlayEffectGroup(154020);
                        WeaponDamage(GetEnemiesInRadius(grenade.Position, ScriptFormula(32)), ScriptFormula(0), DamageType.Fire);
                    };
                    grenade.LaunchArc(demon.Position, 0.1f, -0.1f, 0.6f);  // parameters not based on anything, just picked to look good
                }
            };

            bool firstLaunch = true;
            while (!demon.Timeout.TimedOut)
            {
                demon.Launch(RandomDirection(TargetPosition, 0f, ScriptFormula(7)), 0.2f);
                if (firstLaunch)
                {
                    demon.PlayEffectGroup(165237);
                    firstLaunch = false;
                }
                yield return demon.ArrivalTime;
            }
        }

        IEnumerable<TickTimer> _RuneD()
        {
            int flyerCount = (int)ScriptFormula(14);
            for (int n = 0; n < flyerCount; ++n)
            {
                var flyerPosition = RandomDirection(TargetPosition, 0f, ScriptFormula(7));
                var flyer = SpawnEffect(200808, flyerPosition, 0f, WaitSeconds(ScriptFormula(5)));
                flyer.OnTimeout = () =>
                {
                    flyer.PlayEffectGroup(200516);
                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(flyerPosition, ScriptFormula(13));
                    attack.AddWeaponDamage(ScriptFormula(12), DamageType.Fire);
                    attack.OnHit = (hitPayload) =>
                    {
                        AddBuff(hitPayload.Target, new DebuffStunned(WaitSeconds(ScriptFormula(37))));
                    };
                    attack.Apply();
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }

        IEnumerable<TickTimer> _RuneE()
        {
            float attackRadius = 8f;  // value is not in formulas, just a guess
            Vector3D castedPosition = new Vector3D(User.Position);
            float castAngle = MovementHelpers.GetFacingAngle(castedPosition, TargetPosition);
            float waveOffset = 0f;

            int flyerCount = (int)ScriptFormula(15);
            for (int n = 0; n < flyerCount; ++n)
            {
                waveOffset += 3.0f;
                var wavePosition = PowerMath.TranslateDirection2D(castedPosition, TargetPosition, castedPosition, waveOffset);
                var flyerPosition = RandomDirection(wavePosition, 0f, attackRadius);
                var flyer = SpawnEffect(200561, flyerPosition, castAngle, WaitSeconds(ScriptFormula(20)));
                flyer.OnTimeout = () =>
                {
                    flyer.PlayEffectGroup(200819);
                    AttackPayload attack = new AttackPayload(this);
                    attack.Targets = GetEnemiesInRadius(flyerPosition, attackRadius);
                    attack.AddWeaponDamage(ScriptFormula(11), DamageType.Physical);
                    attack.OnHit = (hitPayload) => { Knockback(hitPayload.Target, 90f); };
                    attack.Apply();
                };

                yield return WaitSeconds(ScriptFormula(4));
            }
        }
    }
}
