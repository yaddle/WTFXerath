﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using WTFXerath.Common;
using Oasys.SDK;
using Oasys.SDK.Events;
using Oasys.SDK.SpellCasting;
using Oasys.Common.GameObject;
using Oasys.Common.GameObject.ObjectClass;
using Oasys.Common.Enums.GameEnums;
using Oasys.Common.Extensions;
using SharpDX;

namespace WTFXerath.Sequences
{
    public class HaltingSequence : SequenceBase
    {
        public override void Initialize()
        {
            CoreEvents.OnCoreMainTick += CoreEvents_OnCoreMainTick;
            CoreEvents.OnCoreMainInputAsync += CoreEvents_OnCoreMainInputAsync;
            //CoreEvents.OnCoreRender += CoreEvents_OnCoreRender;

            base.Initialize();
        }

        private void CoreEvents_OnCoreRender()
        {
            if (NearbyMinions != null && NearbyMinions.Count >= 1)
            {
                foreach(var min in NearbyMinions)
                {
                    if (min.Position != Vector3.Zero)
                        Oasys.SDK.Rendering.RenderFactory.DrawNativeCircle(min.Position, 50f, Color.White, 2.5f);
                }
            }
        }

        public override void Unload()
        {
            CoreEvents.OnCoreMainTick -= CoreEvents_OnCoreMainTick;
            CoreEvents.OnCoreMainInputAsync -= CoreEvents_OnCoreMainInputAsync;

            base.Unload();
        }

        private GameObjectBase CachedTarget { get; set; }
        public List<Minion> NearbyMinions { get; set; }
        private Task CoreEvents_OnCoreMainTick()
        {
            var mySpellBook = UnitManager.MyChampion.GetSpellBook();

            CachedTarget = UnitManager.EnemyChampions
                .Where(x => x.IsInRange(mySpellBook.GetSpellClass(SpellSlot.W).SpellData.CastRange)
                         && x.IsInRange(mySpellBook.GetSpellClass(SpellSlot.E).SpellData.CastRange)
                         && x.IsAlive
                         && x.IsVisible)
                .OrderBy(x => x.Distance)
                .ToList().FirstOrDefault();

            NearbyMinions = UnitManager.EnemyMinions.Where(x => x.Distance <= 1125/*E Cast Range*/).ToList();

            return Task.CompletedTask;
        }

        private Task CoreEvents_OnCoreMainInputAsync()
        {
            var spellBook = UnitManager.MyChampion.GetSpellBook();

            if (CachedTarget != null && !HarrasingSequence.IsInQCastMode
                && spellBook.GetSpellClass(SpellSlot.E).IsSpellReady && spellBook.GetSpellClass(SpellSlot.W).IsSpellReady)
                HaltCachedTarget();

            CommonClass.UnblockOrbwalkerCalls();

            return Task.CompletedTask;
        }

        public void HaltCachedTarget()
        {
            CommonClass.BlockOrbwalkerCalls();

            var dir = Vector3.Normalize((CachedTarget.AIManager.NavTargetPosition - CachedTarget.Position));
            var calcEndVec = CachedTarget.Position + dir * ((CachedTarget.UnitComponentInfo.UnitBaseMoveSpeed / (CachedTarget.AIManager.NavTargetPosition - CachedTarget.Position).Length()) + 200);

            //Get all minions inside the E spell width
            var minInLine = NearbyMinions.Where(x => CommonClass.DistanceFromPointToLine(x.W2S, new Vector2[] { UnitManager.MyChampion.W2S, CachedTarget.W2S }) <= 70/*E Spell Width*/).ToList();

            if(minInLine.Count == 0)
            {
                SpellCastProvider.CastSpell(CastSlot.E, CachedTarget.AIManager.IsMoving ? calcEndVec : CachedTarget.Position);
                SpellCastProvider.CastSpell(CastSlot.W, CachedTarget.AIManager.IsMoving ? calcEndVec : CachedTarget.Position);
            }
        }
    }
}
