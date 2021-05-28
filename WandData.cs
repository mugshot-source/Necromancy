using BepInEx;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using JotunnLib.Entities;
using UnityEngine;
using System.Reflection;

namespace Necromancy
{
    [BepInPlugin("Necromancy", PlayerPatch.ModName, PlayerPatch.Version)]
    [BepInProcess("valheim.exe")]
    public class WandData : PrefabConfig
    {
        public WandData() : base("YewWand", "necrobundle")
        {

        }

        public override void Register()
        {
            Sprite sprite;
            sprite = Resources.Load<Sprite>("icon.png");

            ItemDrop item = base.Prefab.GetComponent<ItemDrop>();
            item.m_itemData.m_shared.m_name = "Convoking Wand";
            item.m_itemData.m_shared.m_description = "A powerful yew wand overflowing with dark energy.";
            //item.m_itemData.m_dropPrefab = base.Prefab;
            item.m_itemData.m_shared.m_maxStackSize = 1;
            item.m_itemData.m_shared.m_maxQuality = 1;
            item.m_itemData.m_shared.m_weight = 4f;
            item.m_itemData.m_shared.m_maxDurability = 600f;
            item.m_itemData.m_shared.m_equipDuration = 0.2f;
            item.m_itemData.m_shared.m_variants = 1;
            item.m_itemData.m_shared.m_timedBlockBonus = 6f;
            item.m_itemData.m_shared.m_deflectionForce = 8f;
            item.m_itemData.m_shared.m_attackForce = 20f;
            item.m_itemData.m_shared.m_secondaryAttack.m_attackAnimation = null;
            item.m_itemData.m_shared.m_attack.m_attackAnimation = null;

            var display = ScriptableObject.CreateInstance<SE_Display>();
            item.m_itemData.m_shared.m_equipStatusEffect = display;

            //item.m_itemData.m_shared.m_icons[item.m_itemData.m_variant] = sprite;

        }
    }
}
