using BepInEx;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using System.Threading.Tasks;
using UnityEngine.UI;
using UnityEngine;

namespace Necromancy
{
	[BepInPlugin("Necromancy", PlayerPatch.ModName, PlayerPatch.Version)]
	[BepInProcess("valheim.exe")]
	public class ATextReceiver : TextReceiver
	{
		public ATextReceiver(ZNetView nview, Character character)
		{
			this.m_nview = nview;
			this.m_character = character;
		}

		public string GetText()
		{
			return this.m_nview.GetZDO().GetString("Skeleton", "");
		}

		public void SetText(string text)
		{
			this.m_nview.ClaimOwnership();
			this.m_nview.GetZDO().Set("Skeleton", text);
			this.UpdateHUDText(text);
		}

		private void UpdateHUDText(string text)
		{
			object value = EnemyHud.instance.GetType().GetField("m_huds", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.GetField).GetValue(EnemyHud.instance);
			IDictionary dictionary = value as IDictionary;
			bool flag = !dictionary.Contains(this.m_character);
			if (!flag)
			{
				object obj = dictionary[this.m_character];
				Text text2 = obj.GetType().GetField("m_name", BindingFlags.Instance | BindingFlags.Public).GetValue(obj) as Text;
				bool flag2 = text2 == null;
				if (!flag2)
				{
					text2.text = text;
				}
			}
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

		private readonly ZNetView m_nview;
		private readonly Character m_character;

		//patches

		[HarmonyPatch(typeof(Tameable), "GetHoverText")]
		private static class Tameable_GetHoverName_Patch
		{
			private static bool Prefix(Tameable __instance, ref string __result, ZNetView ___m_nview, Character ___m_character)
			{
				bool flag = !__instance.name.Contains("Skeleton");
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = !___m_character.IsTamed();
					if (flag2)
					{
						result = true;
					}
					else
					{
						bool flag3 = !___m_nview.IsValid();
						if (flag3)
						{
							__result = string.Empty;
							result = true;
						}
						else
						{
							string str = ___m_nview.GetZDO().GetString("Skeleton", "") ?? (Traverse.Create(__instance).Method("GetStatusString", Array.Empty<object>()).GetValue() as string);
							//string str2 = Localization.instance.Localize(___m_character.GetHoverName());
							//str2 += Localization.instance.Localize(" ( $hud_tame, " + str + " )");
							__result = Localization.instance.Localize("\n[<color=red><b>$KEY_Use</b></color>] $hud_pet\n[<color=red>Hold E</color>] to perform sacrificial ritual");

							result = false;
						}
					}
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(Character), "GetHoverName")]
		private static class Character_GetHoverName_Patch
		{
			private static bool Prefix(Character __instance, ref string __result, ref ZNetView ___m_nview)
			{
				ZNetView znetView = ___m_nview;
				string text;
				if (znetView == null)
				{
					text = null;
				}
				else
				{
					ZDO zdo = znetView.GetZDO();
					text = ((zdo != null) ? zdo.GetString("Skeleton", "") : null);
				}
				string text2 = text;
				bool skeleton = __instance.name.Contains("Skeleton") && !string.IsNullOrEmpty(text2);
				bool flag = skeleton && __instance.IsTamed();
				bool result;
				if (flag)
				{
					__result = text2;
					result = false;
				}
				else
				{
					result = true;
				}
				return result;
			}
		}

		[HarmonyPatch(typeof(Tameable), "Interact")]
		private static class Tameable_Interact_Patch
		{
			// Token: 0x06000044 RID: 68 RVA: 0x00004EB4 File Offset: 0x000030B4
			private static bool Prefix(Tameable __instance, ref bool __result, Humanoid user, bool hold, ZNetView ___m_nview, Character ___m_character, ref float ___m_lastPetTime)
			{
				bool flag = !__instance.name.Contains("Skeleton");
				bool result;
				if (flag)
				{
					result = true;
				}
				else
				{
					bool flag2 = !___m_nview.IsValid();
					if (flag2)
					{
						__result = false;
						result = true;
					}
					else
					{
						bool flag3 = ___m_character.IsTamed();
						if (flag3)
						{
							if (hold)
							{
								int count = 0;
								PlayEffect("vfx_GodExplosion", __instance.m_character.GetCenterPoint());
								foreach(var hit in Physics.OverlapSphere(__instance.m_character.GetCenterPoint(), 8f))
                                {
									count++;
									var target = hit.gameObject.GetComponent<Character>();
									if (target == null) target = hit.GetComponentInParent<Character>();
									if (target == null) target = hit.gameObject.GetComponentInChildren<Character>();
									if (target != null)
                                    {
										count++;
										PlaySound("sfx_greydwarf_shaman_heal", target.GetCenterPoint());
										PlayEffect("vfx_WishbonePing", target.GetCenterPoint());
										if (target != Player.m_localPlayer && target.m_faction==Character.Faction.Players)
										{
											target.Heal(__instance.m_character.GetMaxHealth(), true);
										}
                                    }
								}

								PlayEffect("vfx_ghost_death", __instance.m_character.GetCenterPoint());
								PlaySound("sfx_skeleton_big_death", __instance.m_character.GetCenterPoint());
								PlaySound("sfx_crow_death", __instance.m_character.GetCenterPoint());

								//PlayEffect("vfx_creature_soothed", Player.m_localPlayer.GetCenterPoint());
								//PlaySound("sfx_Potion_health_Start", Player.m_localPlayer.GetCenterPoint());

								HitData hitData = new HitData();
								hitData.m_damage.m_damage = 1E+10f;
								__instance.m_character.Damage(hitData);
								/*TextInput.instance.RequestText(new Necromancy.ATextReceiver(___m_character.GetComponent<ZNetView>(), ___m_character), "Name", 15);*/
								__result = false;
								return false;
							}
						}
						result = true;
					}
				}
				return result;
			}
		}


	}
}
