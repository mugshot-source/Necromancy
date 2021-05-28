using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

namespace Necromancy
{
    class SE_Display : StatusEffect
    {
        public static Sprite icon;
		GameObject iconObject;
		Text iconText;
        Transform _transform;
        RectTransform rectTransform;

        public SE_Display()
        {
            base.name = "Skeletons";
            this.m_icon = SE_Display.icon;
            this.m_tooltip = "Skeleton count";
        }
        public override void Setup(Character character)
		{
            iconObject = UnityEngine.Object.Instantiate<GameObject>(PlayerPatch.IconPrefab);
            icon = iconObject.GetComponentInChildren<Sprite>();
            rectTransform = iconObject.GetComponentInChildren<RectTransform>();
            var pos = rectTransform.localPosition;
            rectTransform.localPosition.Set(pos.x, pos.y + 100, pos.z);
            base.Setup(character);
		}

        public override void Stop()
        {
            iconObject.SetActive(false);
            base.Stop();
        }

        public override string GetIconText()
        {
            int tracker = 0;
            List<Character> list = new List<Character>();
            Character.GetCharactersInRange(Player.m_localPlayer.transform.position, 10f, list);
            for (int i = 0; i < list.Count; i++)
            {
                if (!list[i].m_tamed && list[i].m_name != "Thrall")
                    list.RemoveAt(i);
                else if (list[i].IsTamed() && list[i].m_faction == 0)
                {
                    tracker = list.Count;
                    return tracker.ToString();
                }
            }
            return tracker.ToString();
        }

        public override void UpdateStatusEffect(float dt)
        {
            base.UpdateStatusEffect(dt);
            int tracker = 0;
            var wand = Player.m_localPlayer.GetInventory().GetAllItems().
                FirstOrDefault(v => v.m_shared.m_name == "Convoking Wand");
            //iconText = iconObject.GetComponent<Text>();
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

            if (iconObject.GetComponentInChildren<Text>() != null)
            {
                var txtComp = iconObject.GetComponentInChildren<Text>();
                txtComp.text = "Skeletons: " + tracker.ToString();
                txtComp.alignment = TextAnchor.MiddleCenter;
            }
            if (!wand.m_equiped)
            {
                iconObject.SetActive(false);
            }
            if (Input.GetKeyDown(KeyCode.R))
            {
                iconObject.SetActive(false);
            }
            else if (m_character.IsDead())
            {
                ZNetScene.instance.m_instances.Remove(iconObject.GetComponent<ZNetView>().GetZDO());
                iconObject.GetComponent<ZNetView>().Destroy();
                iconObject = null;
            }
        }
    }
}
