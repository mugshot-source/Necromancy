using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using JotunnLib.Entities;
using JotunnLib.Managers;
using JotunnLib.Utils;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using UnityEngine;
using UnityEngine.UI;

namespace Necromancy
{
    [BepInPlugin("Necromancy", PlayerPatch.ModName, PlayerPatch.Version)]
    [BepInDependency(JotunnLib.JotunnLib.ModGuid)]
    [BepInProcess("valheim.exe")]
    internal class PlayerPatch : BaseUnityPlugin
    {
        //harmony + logging
        private readonly Harmony harmony = new Harmony(ModGuid);
        public static ManualLogSource Log;

        //mod data
        public const string ModGuid = ModName;
        public static KeyCode bind;
        public const string Version = "1.0.0";
        public const string ModName = "Necromancy";
        public static List<Character> Pets = new List<Character>();
        public static GameObject YewWand;
        public static GameObject IconPrefab;


        //config
        private static ConfigEntry<bool> unequipDegen;
        private static ConfigEntry<int> nexusID;
        private static ConfigEntry<float> speed, health, conjuredrain, healdrain;
        private static ConfigEntry<int> maxminions, minionlevel;

        public static ConfigEntry<string> bindSummonKey, bindConvocateKey, bindHealKey, bindKillKey, bindDismissKey;
        public static KeyCode bindSummon, bindConvocate, bindHeal, bindKill, bindDismiss;


        private void InitConfigData()
        {
            nexusID = Config.Bind<int>("Updates", "NexusID", 1003, "Nexus mod ID for updates");

            speed = Config.Bind<float>("Thrall Settings", "Skeleton Speed", 8f, "Skeleton speed");
            health = Config.Bind<float>("Thrall Settings", "Skeleton health", 600f, "Skeleton health");
            conjuredrain = Config.Bind<float>("Player Settings", "Conjure drain", 35f, "HP drained when summoning");
            healdrain = Config.Bind<float>("Player Settings", "Heal drain", 60f, "HP drained when healing minions");
            maxminions = Config.Bind<int>("Thrall Settings", "Max minions", 8, "Max number of minions summonable");
            unequipDegen = Config.Bind<bool>("Thrall Settings", "Damage minions if unequipped and in combat", true, "Kills minions slowly if unequiping wand");
            //minionlevel = Config.Bind("Thrall Settings", "Minion level (lvl 3 = 2 stars)", 3, "Level of minions only affects damage");

            bindSummonKey = Config.Bind("Hotkeys", "Summon bind (block + Z)", "Z", "Summon hotkey");
            bindConvocateKey = Config.Bind("Hotkeys", "Convocate bind (block + X)", "X", "Convocate hotkey");
            bindHealKey = Config.Bind("Hotkeys", "Heal bind (block + V)", "V", "Heal hotkey");
            bindKillKey = Config.Bind("Hotkeys", "Kill bind (block + K)", "K", "Kill hotkey");
            bindDismissKey = Config.Bind("Hotkeys", "Dismiss bind (block + N)", "N", "Dismiss hotkey");

            if (!Enum.TryParse<KeyCode>(bindSummonKey.Value, out var summonKeyCode))
            {
                Debug.Log("Failed to parse hotkey");
                return;
            }
            bindSummon = summonKeyCode;
            if (!Enum.TryParse<KeyCode>(bindDismissKey.Value, out var dismissKeyCode))
            {
                Debug.Log("Failed to parse hotkey");
                return;
            }
            bindDismiss = dismissKeyCode;
            if (!Enum.TryParse<KeyCode>(bindConvocateKey.Value, out var convocateKeyCode))
            {
                Debug.Log("Failed to parse hotkey");
                return;
            }
            bindConvocate = convocateKeyCode;
            if (!Enum.TryParse<KeyCode>(bindHealKey.Value, out var healKeyCode))
            {
                Debug.Log("Failed to parse hotkey");
                return;
            }
            bindHeal = healKeyCode;
            if (!Enum.TryParse<KeyCode>(bindKillKey.Value, out var killKeyCode))
            {
                Debug.Log("Failed to parse hotkey");
                return;
            }
            bindKill = killKeyCode;

        }

        void Awake()
        {
            InitConfigData();

            AssetBundle assetBundle = AssetBundleHelper.GetAssetBundleFromResources("necrobundle");
            YewWand = assetBundle.LoadAsset<GameObject>("Assets/CustomItems/Wand/YewWand.prefab");
            AssetBundle iconbundle = AssetBundleHelper.GetAssetBundleFromResources("iconbundle");
            IconPrefab = iconbundle.LoadAsset<GameObject>("Assets/CustomItems/ThrallDisplay/Canvas.prefab");
            PrefabManager.Instance.PrefabRegister += RegisterPrefabs;
            ObjectManager.Instance.ObjectRegister += RegisterObjects;

            harmony.PatchAll();
        }

        private void RegisterPrefabs(object sender, EventArgs e)
        {
            //var staffItemDrop = YewWand.GetComponent<ItemDrop>();
            ReflectionUtils.InvokePrivate(PrefabManager.Instance, "RegisterPrefab", new object[]
            {
                YewWand,
                "necrobundle"
            });
            PrefabManager.Instance.RegisterPrefab(new WandData());
            //AccessTools.Method(typeof(PrefabManager), "RegisterPrefab", new Type[] { typeof(GameObject), typeof(string) }).Invoke(PrefabManager.Instance, new object[] { staffItemDrop, "YewWand" });
        }
        private void RegisterObjects(object sender, EventArgs e)
        {
            ObjectManager.Instance.RegisterItem("YewWand");

            ObjectManager.Instance.RegisterRecipe(new RecipeConfig
            {
                Name = "Recipe_YewWand",
                Item = "YewWand",
                Amount = 1,
                CraftingStation = "piece_workbench",
                MinStationLevel = 4,

                Requirements = new PieceRequirementConfig[]
                {
                        new PieceRequirementConfig
                        {
                            Item = "TrophySkeletonPoison",
                            Amount = 1
                        },
                        new PieceRequirementConfig
                        {
                            Item = "Guck",
                            Amount = 15
                        },
                        new PieceRequirementConfig
                        {
                            Item = "WitheredBone",
                            Amount = 20
                        },
                        new PieceRequirementConfig
                        {
                            Item = "RoundLog",
                            Amount = 10
                        }
                }
            });
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }


        [HarmonyPatch(typeof(Player), "Update")]
        private static class PlayerUpdatePatch
        {
            private static float cd, dt, cd2, cd3, cd4, cd5;
            public static GameObject RegPrefab(string prefabN)
            {
                return ZNetScene.instance.GetPrefab(prefabN);
            }

            static void PlayEffect(string prefabN, Vector3 pos)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabN);
                if (prefab != null)
                {
                    UnityEngine.Object.Instantiate<GameObject>(prefab, pos, Quaternion.identity);
                    return;
                }
            }

            static void PlaySound(string prefabN, Vector3 pos)
            {
                GameObject prefab = ZNetScene.instance.GetPrefab(prefabN);
                AudioSource aud;
                aud = prefab.GetComponent<AudioSource>();
                UnityEngine.Object.Instantiate<AudioSource>(aud, pos, Quaternion.identity);
            }

            public static void Convocate(Player instance)
            {
                PlayEffect("vfx_corpse_destruction_medium", instance.GetCenterPoint());

                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(instance.transform.position, 50f, list);
                foreach (Character character in list)
                {
                    if (!character.IsPlayer())
                    {
                        if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                            && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                            && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                            && character.m_faction != Character.Faction.Demon)
                        {
                            if (character.m_tamed == true && character.m_name.Contains("Thrall"))
                            {
                                character.Heal(character.GetMaxHealth()*0.02f, true);
                                character.GetBaseAI().SetAlerted(false);
                                character.transform.position = instance.GetCenterPoint();

                                Tameable tameComp = character.GetComponent<Tameable>();
                                if (!tameComp.m_monsterAI.GetFollowTarget() || tameComp.m_monsterAI.GetFollowTarget() == null)
                                {
                                    tameComp.Command(Player.m_localPlayer);
                                }
                            }
                        }
                    }
                }
            }

            public static void Dismiss(Player instance)
            {

                List<Character> list = new List<Character>();
                Character.GetCharactersInRange(instance.transform.position, 50f, list);
                foreach (Character character in list)
                {
                    if (!character.IsPlayer())
                    {
                        if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                            && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                            && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                            && character.m_faction != Character.Faction.Demon)
                        {
                            if (character.m_tamed == true && character.m_name.Contains("Thrall"))
                            {
                                Tameable tameComp = character.GetComponent<Tameable>();
                                if (tameComp.m_monsterAI.GetFollowTarget() || tameComp.m_monsterAI.GetFollowTarget() != null)
                                {
                                    tameComp.Command(Player.m_localPlayer);
                                }
                            }
                        }
                    }
                }
            }

            public static Character GetNearbyMinions(Character __instance, float range)
            {
                List<Character> list4 = new List<Character>();
                Character.GetCharactersInRange(__instance.transform.position, range, list4);
                foreach (Character character in list4)
                {
                    if (!character.IsPlayer())
                    {
                        if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                            && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                            && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                            && character.m_faction != Character.Faction.Demon)
                        {
                            if (character.m_tamed == true && character.m_name.Contains("Thrall"))
                            {
                                    return character;
                            }
                        }
                    }
                }
                return null;
            }

            public static void Heal(Player instance)
            {
                PlayEffect("vfx_GodExplosion", instance.GetCenterPoint());
                PlaySound("sfx_Frost_Start", instance.GetCenterPoint());
                if (instance.m_health > healdrain.Value)
                {
                    var damageType = new HitData.DamageTypes
                    {
                        m_damage = healdrain.Value,
                    };
                    var hit = new HitData
                    {
                        m_damage = damageType,
                        m_blockable = false,
                        m_dodgeable = false,
                        m_skill = Skills.SkillType.None,

                    };
                    instance.Damage(hit);
                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(instance.transform.position, 30f, list);
                    foreach (Character character in list)
                    {
                        if (!character.IsPlayer())
                        {
                            if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                                && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                                && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                                && character.m_faction != Character.Faction.Demon)
                            {
                                if (character.m_tamed == true && character.m_name.Contains("Thrall"))
                                {
                                    character.Heal(character.GetMaxHealth());

                                }
                            }
                        }
                    }
                }
                else
                {
                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Not enough HP to heal minions.", 0, null);
                }
            }

            public static void Hunt(Character charComp, Character player)
            {
                Camera _cam;
                _cam = Camera.main;
                RaycastHit hit;
                var head = player.m_head;
                var position = head.position + head.up * 0.2f;
                var forward = head.right * 2f;
                var right = head.forward;

                var dir = _cam.transform.forward;
                var startPoint = position + forward;
                var endPoint = dir * 30 + _cam.transform.position;
                Ray ray = _cam.ScreenPointToRay(_cam.transform.position);

                if (Physics.Raycast(endPoint, dir, out hit, 20f))
                {
                    var enemy = hit.collider.gameObject.GetComponent<Character>();
                    if (enemy == null) enemy = hit.collider.GetComponentInParent<Character>();
                    if (enemy == null) enemy = hit.collider.gameObject.GetComponentInChildren<Character>();
                    if (enemy != null)
                    {
                        var target = hit.point;
                        var fx = UnityEngine.Object.Instantiate(ZNetScene.instance.GetPrefab("fx_crit"), target, Quaternion.Euler(-90, 0, 0));
                        foreach (var p in fx.GetComponentsInChildren<ParticleSystem>()) p.Play();

                        ZDOID zcomp = enemy.GetComponent<ZDOID>();
                        if(zcomp != null)
                        {
                            charComp.GetBaseAI().SetTargetInfo(zcomp);
                        }
                    }
                }
            }

            private static bool skeletonarchers = true;

            private static void Postfix(Player __instance, ref Attack ___m_currentAttack, ref float ___m_lastCombatTimer, Rigidbody ___m_body, ZSyncAnimation ___m_zanim,
              CharacterAnimEvent ___m_animEvent, VisEquipment ___m_visEquipment, Attack ___m_previousAttack, float ___m_timeSinceLastAttack)
            {
                int tracker = 0;
                bool spawned = false;
                bool convocated = false;
                bool healed = false;
                bool unequiped = false;
                bool dismissed = false;
                //Character[] arr1 = new Character[5];
                cd -= Time.deltaTime;
                cd2 -= Time.deltaTime;
                cd3 -= Time.deltaTime;
                cd4 -= Time.deltaTime;
                cd5 -= Time.deltaTime;
                dt = Time.deltaTime;
                int rand = UnityEngine.Random.Range(0, 4);

                var wand = __instance.GetInventory().GetAllItems().
                FirstOrDefault(v => v.m_shared.m_name == "Convoking Wand");
                if (wand != null && wand.m_equiped)
                {
                    if (Input.GetMouseButtonDown(2))
                    {
                        if (skeletonarchers)
                        {
                            skeletonarchers = false;
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Skeleton Mode: Warriors", 0, null);
                        }
                        else
                        {
                            skeletonarchers = true;
                            Player.m_localPlayer.Message(MessageHud.MessageType.Center, "Skeleton Mode: Archers", 0, null);
                        }
                    }

                    if (Input.GetKeyDown(bindSummon) && Player.m_localPlayer.IsBlocking())
                    {
                        if (cd <= 0f)
                        {
                            if (Player.m_localPlayer.GetHealth() > conjuredrain.Value)
                            {
                                string[] arr1 = { "Skeleton", "Skeleton", "Skeleton", "Skeleton", "Skeleton", "Skeleton", "Skeleton", "Skeleton", "Skeleton" };
                                List<Character> list = new List<Character>();
                                Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 10f, list);
                                for (int i = 0; i < list.Count; i++)
                                {
                                    if (!list[i].m_tamed && list[i].m_name != "Thrall")
                                        list.RemoveAt(i);
                                    else if (list[i].IsTamed() && list[i].m_faction == 0)
                                    {
                                        tracker = list.Count;
                                    }
                                }
                                Debug.Log(tracker);
                                if (tracker < maxminions.Value)
                                {

                                    //blood pact
                                    var damageType = new HitData.DamageTypes
                                    {
                                        m_damage = conjuredrain.Value,
                                    };
                                    var hit = new HitData
                                    {
                                        m_damage = damageType,
                                        m_blockable = false,
                                        m_dodgeable = false,
                                        m_skill = Skills.SkillType.None,

                                    };
                                    Player.m_localPlayer.Damage(hit);

                                    ZNetView[] znet;
                                    GameObject prefab;
                                    znet = UnityEngine.Object.FindObjectsOfType<ZNetView>();
                                    int choice = UnityEngine.Random.Range(1, arr1.Length);
                                    //var myVector = new Vector3(UnityEngine.Random.Range(5, 15), UnityEngine.Random.Range(5, 15));
                                    prefab = RegPrefab(arr1[choice - 1]);
                                    if (prefab == null)
                                    {
                                        Debug.Log("Invalid prefab name");
                                    }
                                    Vector3 position = Player.m_localPlayer.transform.position;
                                    GameObject playerObj = Player.m_localPlayer.gameObject.GetComponent<GameObject>();

                                    GameObject gameObject = UnityEngine.Object.Instantiate<GameObject>(prefab, position + Player.m_localPlayer.transform.forward, Quaternion.identity);
                                    gameObject.transform.localScale *= 0.75f;
                                    ZNetView znetComp = gameObject.GetComponent<ZNetView>();

                                    //weapon
                                    var bowprefab = RegPrefab("skeleton_bow");
                                    var swordprefab = RegPrefab("skeleton_sword");
                                    var shieldprefab = RegPrefab("ShieldBanded");
                                    //give weapon
                                    Humanoid humComp = gameObject.GetComponent<Humanoid>();
                                    for (int i = 0; i < humComp.m_defaultItems.Length; i++)
                                    {
                                        humComp.m_defaultItems[i] = null;
                                    }
                                    for (int i = 0; i < humComp.m_randomWeapon.Length; i++)
                                    {
                                        humComp.m_randomWeapon[i] = null;
                                    }
                                    for (int i = 0; i < humComp.m_randomShield.Length; i++)
                                    {
                                        humComp.m_randomShield[i] = null;
                                    }
                                    if (skeletonarchers)
                                        humComp.GiveDefaultItem(bowprefab);
                                    else if (!skeletonarchers)
                                    {
                                        humComp.GiveDefaultItem(swordprefab);
                                        humComp.m_staggerTimer = 0.01f;
                                        humComp.m_blocking = true;
                                        humComp.GiveDefaultItem(shieldprefab);
                                        humComp.m_blockTimer = 0f;
                                    }
                                    humComp.GetInventory();

                                    //drops
                                    CharacterDrop drop = gameObject.GetComponent<CharacterDrop>();
                                    drop.m_dropsEnabled = false;

                                    //lvl control
                                    Character charComp = gameObject.GetComponent<Character>();
                                    //int lvlPicker = Random.Range(1, 5);
                                    //charComp.SetLevel(lvlPicker);
                                    charComp.SetLevel(3);
                                    charComp.SetMaxHealth(health.Value);
                                    charComp.SetHealth(health.Value);
                                    charComp.m_speed = speed.Value;
                                    charComp.m_name = "Thrall";

                                    //tame
                                    charComp.SetTamed(true);
                                    charComp.m_faction = 0;

                                    //follow
                                    Tameable tameComp = charComp.GetComponent<Tameable>();
                                    tameComp.Command(Player.m_localPlayer);



                                    //spawning
                                    znetComp.GetZDO().SetPGWVersion(znet[0].GetZDO().GetPGWVersion());
                                    znet[0].GetZDO().Set("spawn_id", znetComp.GetZDO().m_uid);
                                    znet[0].GetZDO().Set("alive_time", ZNet.instance.GetTime().Ticks);

                                    PlayEffect("vfx_wraith_death", Player.m_localPlayer.GetCenterPoint());
                                    PlaySound("sfx_lootspawn", charComp.GetCenterPoint());
                                    PlayEffect("vfx_player_death", Player.m_localPlayer.GetCenterPoint());
                                    spawned = true;
                                    Debug.Log(charComp.IsTamed());
                                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You have successfully initiated a blood pact.", 0, null);
                                }
                                else
                                {
                                    Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Max minions reached", 0, null);
                                }
                            }
                            else
                            {
                                Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You do not have enough health to proceed with the blood pact.", 0, null);
                            }
                        }
                        else
                        {
                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "Minions are on cooldown!", 0, null);
                        }
                        if (spawned) cd = 2f;

                    }



                    //Cull minions
                    if (Input.GetKeyDown(bindKill) && Player.m_localPlayer.IsBlocking())
                    {
                        List<Character> list4 = new List<Character>();
                        Character.GetCharactersInRange(__instance.transform.position, 50f, list4);
                        foreach (Character character in list4)
                        {
                            if (!character.IsPlayer())
                            {
                                if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                                    && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                                    && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                                    && character.m_faction != Character.Faction.Demon)
                                {
                                    if (character.m_tamed == true && character.m_name.Contains("Thrall"))
                                    {
                                        PlayEffect("vfx_ghost_death", character.GetCenterPoint());
                                        PlaySound("sfx_skeleton_big_death", character.GetCenterPoint());
                                        PlaySound("sfx_crow_death", character.GetCenterPoint());

                                        HitData hitData = new HitData();
                                        hitData.m_damage.m_damage = 1E+10f;
                                        character.Damage(hitData);
                                    }
                                }
                            }
                        }
                    }

                    //Heal
                    if (Input.GetKeyDown(bindHeal) && Player.m_localPlayer.IsBlocking())
                    {
                        if (cd2 <= 0)
                        {
                            Heal(Player.m_localPlayer);

                            healed = true;

                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You have healed your thralls.", 0, null);
                            Debug.Log("Minions healed");
                        }
                    }
                    if (healed) cd2 = 10;

                    //Convocate
                    if (Input.GetKeyDown(bindConvocate) && Player.m_localPlayer.IsBlocking())
                    {
                        if (cd3 <= 0)
                        {
                            Convocate(Player.m_localPlayer);
                            PlayEffect("vfx_corpse_destruction_medium", Player.m_localPlayer.GetCenterPoint());
                            convocated = true;

                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You have convocated your thralls. They will follow you.", 0, null);
                            Debug.Log("Minions convocated");
                        }
                    }
                    if (convocated) cd3 = 4;
                }

                //Dismiss
                if (Input.GetKeyDown(bindDismiss) && Player.m_localPlayer.IsBlocking())
                {
                    if (cd5 <= 0)
                    {
                        Dismiss(Player.m_localPlayer);
                        PlaySound("sfx_OpenPortal", Player.m_localPlayer.GetCenterPoint());
                        PlayEffect("vfx_odin_despawn", Player.m_localPlayer.GetCenterPoint());
                        dismissed = true;

                        Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You have commanded your thralls to guard this region.", 0, null);
                        Debug.Log("Minions dismissed");
                    }
                }
                if (dismissed) cd5 = 4;
                // Unequip degen
                if (wand != null && !wand.m_equiped && unequipDegen.Value)
                {
                    if (cd4 < 0)
                    {
                        List<Character> list4 = new List<Character>();
                        Character.GetCharactersInRange(__instance.transform.position, 50f, list4);
                        foreach (Character character in list4)
                        {
                            if (!character.IsPlayer())
                            {
                                if (character.m_faction != Character.Faction.Boss && character.m_faction != Character.Faction.SeaMonsters
                                    && character.m_faction != Character.Faction.PlainsMonsters && character.m_faction != Character.Faction.MountainMonsters
                                    && character.m_faction != Character.Faction.ForestMonsters && character.m_faction != Character.Faction.AnimalsVeg
                                    && character.m_faction != Character.Faction.Demon)
                                {
                                    if (character.m_tamed == true)
                                    {
                                        if (character.GetBaseAI().IsAlerted() && character.m_name.Contains("Thrall"))
                                        {
                                            PlaySound("sfx_skeleton_hit", character.GetCenterPoint());
                                            PlayEffect("vfx_skeleton_hit", character.GetCenterPoint());

                                            HitData hitData = new HitData();
                                            hitData.m_damage.m_damage = character.GetMaxHealth() * 0.05f;
                                            character.Damage(hitData);
                                            unequiped = true;
                                            Player.m_localPlayer.Message(MessageHud.MessageType.TopLeft, "You have lost concentration, your minions are slowly decaying.", 0, null);
                                        }
                                    }
                                }
                            }
                        }
                    }
                    if (unequiped) cd4 = 2;
                }

            }
        }

            [HarmonyPatch(typeof(Character), "Awake")]
            private static class Character_Awake_Patch
            {
                private static void Postfix(Character __instance)
                {
                    bool flag = __instance.name.Contains("Skeleton");
                    bool flag0 = __instance.name.Contains("Draugr");
                    if (flag || flag0)
                    {
                        Tameable tameable = __instance.gameObject.GetComponent<Tameable>();
                        bool flag2 = tameable == null;
                        if (flag2)
                        {
                            tameable = __instance.gameObject.AddComponent<Tameable>();
                        }
                        tameable.m_commandable = true;
                    }
                }
            }

            [HarmonyPatch(typeof(Player))]
            [HarmonyPatch("TeleportTo")]
            public static class Player_PatchGetPets
            {
                public static void Prefix(Player __instance)
                {
                    Pets.Clear();
                    List<Character> list = new List<Character>();
                    Character.GetCharactersInRange(__instance.transform.position, 50f, list);
                    int num = 0;
                    int num2 = 0;
                    foreach (Character character in list)
                    {
                        if (character.IsTamed() && character.m_faction == 0)
                        {
                            num2++;
                            MonsterAI component = character.GetComponent<MonsterAI>();
                            if (++num > 8)
                            {
                                break;
                            }
                            Pets.Add(character);

                        }
                    }
                }
            }

            [HarmonyPatch(typeof(Tameable), "Interact")]
            private class Command_Patch
            {
                private static void Prefix(ref bool ___m_commandable, ref Character ___m_character)
                {
                    if (___m_character.IsTamed())
                    {
                        ___m_commandable = true;
                    }
                }
            }

            [HarmonyPatch(typeof(Player))]
            [HarmonyPatch("UpdateTeleport")]
            public static class Player_PatchUpdateTeleport
            {
                private static void Postfix(ref bool ___m_teleporting, ref float ___m_teleportTimer, ref Vector3 ___m_teleportTargetPos, ref Quaternion ___m_teleportTargetRot)
                {
                    if (___m_teleporting && (double)___m_teleportTimer > 0.01)
                    {
                        foreach (Character character in Pets)
                        {
                            Vector2 vector = new Vector2(1f, 0f) + UnityEngine.Random.insideUnitCircle;
                            Vector3 b = new Vector3(vector.x, vector.y, 0.5f);
                            Vector3 lookDir = ___m_teleportTargetRot * Vector3.forward;
                            character.transform.position = ___m_teleportTargetPos + b;
                            character.transform.rotation = ___m_teleportTargetRot;
                            character.SetLookDir(lookDir);
                        }
                    }
                }
            }
        }
    }
