using System.Collections.Generic;
using BepInEx;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;

namespace ValheimEzP
{
    [BepInPlugin(ModGUID, ModName, ModVersion)]
    public class EzPlayPlugin : BaseUnityPlugin
    {
        public const string ModGUID = "com.chaos.valheim.ezplay";
        public const string ModName = "ValheimEzPlay";
        public const string ModVersion = "1.0.0";

        // ==========================================
        // 运行时配置项 (自动生成 .cfg 文件供玩家修改)
        // ==========================================
        public static ConfigEntry<float> ConfigContainerRadius;
        public static ConfigEntry<float> ConfigCopperWeight;
        public static ConfigEntry<bool> ConfigEnableAutoCraft;
        public static ConfigEntry<KeyCode> ConfigQuickStackKey;
        public static ConfigEntry<KeyCode> ConfigSortKey;

        private readonly Harmony harmony = new Harmony(ModGUID);

        void Awake()
        {
            // 初始化配置文件绑定
            ConfigContainerRadius = Config.Bind("General", "ContainerRadius", 10.0f, "搜寻附近箱子的最大半径（米）");
            ConfigCopperWeight = Config.Bind("Items", "CopperOreWeight", 7.5f, "铜矿石的重量（原版为 10.0）");
            ConfigEnableAutoCraft = Config.Bind("General", "EnableAutoCraftFromContainers", true, "是否允许工作台自动提取箱子材料");
            ConfigQuickStackKey = Config.Bind("Keybinds", "QuickStackKey", KeyCode.N, "一键智能分类存入附近箱子的快捷键");
            ConfigSortKey = Config.Bind("Keybinds", "SortContainerKey", KeyCode.R, "打开箱子时执行碎片整理与排序的快捷键");

            harmony.PatchAll();
            Logger.LogInfo($"{ModName} v{ModVersion} 已成功加载！");
        }

        void OnDestroy()
        {
            harmony.UnpatchSelf();
        }

        void Update()
        {
            if (Player.m_localPlayer == null || Chat.instance?.IsChatDialogWindowVisible() == true) 
                return;

            // 按下快捷键：背包物品自动分类存入周围箱子
            if (Input.GetKeyDown(ConfigQuickStackKey.Value))
            {
                QuickStackToNearbyContainers(Player.m_localPlayer);
            }

            // 打开箱子时按下快捷键：对当前箱子执行碎片整理与排序
            if (Input.GetKeyDown(ConfigSortKey.Value) && InventoryGui.instance?.IsContainerOpen() == true)
            {
                Inventory containerInv = InventoryGui.instance.ContainerGrid?.GetInventory();
                if (containerInv != null)
                {
                    SortAndDefrag(containerInv);
                }
            }
        }

        // ==========================================
        // 功能 1：铜矿减重
        // ==========================================
        [HarmonyPatch(typeof(ObjectDB), "Awake")]
        public static class Patch_ObjectDB_Awake
        {
            [HarmonyPostfix]
            public static void Postfix(ObjectDB __instance) => UpdateCopperWeight(__instance);
        }

        [HarmonyPatch(typeof(ObjectDB), nameof(ObjectDB.CopyOtherDB))]
        public static class Patch_ObjectDB_CopyOtherDB
        {
            [HarmonyPostfix]
            public static void Postfix(ObjectDB __instance) => UpdateCopperWeight(__instance);
        }

        private static void UpdateCopperWeight(ObjectDB odb)
        {
            if (odb == null || odb.m_items.Count == 0) return;
            GameObject copperOre = odb.GetItemPrefab("CopperOre");
            if (copperOre != null)
            {
                ItemDrop drop = copperOre.GetComponent<ItemDrop>();
                if (drop?.m_itemData?.m_shared != null)
                {
                    drop.m_itemData.m_shared.m_weight = ConfigCopperWeight.Value;
                }
            }
        }

        // ==========================================
        // 功能 2：工作台制作自动检测/消耗周围箱子材料
        // ==========================================
        public static List<Container> GetNearbyContainers(Vector3 origin)
        {
            List<Container> result = new List<Container>();
            List<Piece> pieces = new List<Piece>();
            Piece.GetAllPiecesInRadius(origin, ConfigContainerRadius.Value, pieces);

            foreach (var piece in pieces)
            {
                if (piece == null) continue;
                Container c = piece.GetComponent<Container>();
                if (c == null || c.GetInventory() == null) continue;

                if (c.CanBeRemoved() && PrivateArea.CheckAccess(c.transform.position, 0f, flash: false))
                {
                    result.Add(c);
                }
            }
            return result;
        }

        [HarmonyPatch(typeof(Player), nameof(Player.HaveRequirements), new[] { typeof(Recipe), typeof(bool), typeof(int) })]
        public static class Patch_Player_HaveRequirements
        {
            [HarmonyPostfix]
            public static void Postfix(Player __instance, Recipe recipe, int qualityLevel, ref bool __result)
            {
                if (!ConfigEnableAutoCraft.Value) return;
                if (__result || recipe == null || __instance != Player.m_localPlayer) return;

                var containers = GetNearbyContainers(__instance.transform.position);
                if (containers.Count == 0) return;

                foreach (Piece.Requirement req in recipe.m_resources)
                {
                    if (req.m_resItem == null) continue;
                    int needed = req.GetAmount(qualityLevel);
                    if (needed <= 0) continue;

                    string itemName = req.m_resItem.m_itemData.m_shared.m_name;
                    int total = __instance.GetInventory().CountItems(itemName);

                    if (total < needed)
                    {
                        foreach (var box in containers)
                        {
                            total += box.GetInventory().CountItems(itemName);
                            if (total >= needed) break;
                        }
                    }

                    if (total < needed)
                    {
                        __result = false;
                        return;
                    }
                }
                __result = true;
            }
        }

        [HarmonyPatch(typeof(Player), nameof(Player.ConsumeResources), new[] { typeof(Piece.Requirement[]), typeof(int), typeof(int) })]
        public static class Patch_Player_ConsumeResources
        {
            [HarmonyPrefix]
            public static bool Prefix(Player __instance, Piece.Requirement[] requirements, int qualityLevel)
            {
                if (!ConfigEnableAutoCraft.Value || __instance != Player.m_localPlayer) return true;

                var containers = GetNearbyContainers(__instance.transform.position);

                foreach (Piece.Requirement req in requirements)
                {
                    if (req.m_resItem == null) continue;
                    int totalNeeded = req.GetAmount(qualityLevel);
                    if (totalNeeded <= 0) continue;

                    string itemName = req.m_resItem.m_itemData.m_shared.m_name;
                    int inBag = __instance.GetInventory().CountItems(itemName);

                    if (inBag >= totalNeeded)
                    {
                        __instance.GetInventory().RemoveItem(itemName, totalNeeded);
                    }
                    else
                    {
                        if (inBag > 0) __instance.GetInventory().RemoveItem(itemName, inBag);
                        int remain = totalNeeded - inBag;

                        foreach (var box in containers)
                        {
                            Inventory bInv = box.GetInventory();
                            int inBox = bInv.CountItems(itemName);
                            if (inBox > 0)
                            {
                                int take = Mathf.Min(inBox, remain);
                                bInv.RemoveItem(itemName, take);
                                bInv.m_onChanged?.Invoke(); // 触发容器数据更新与持久化保存
                                remain -= take;
                                if (remain <= 0) break;
                            }
                        }
                    }
                }
                return false;
            }
        }

        // ==========================================
        // 功能 3：智能一键存入周围箱子 (Quick Stack)
        // ==========================================
        private static void QuickStackToNearbyContainers(Player player)
        {
            Inventory pInv = player.GetInventory();
            var containers = GetNearbyContainers(player.transform.position);
            bool transferred = false;

            // 快照列表，防止迭代过程中集合发生修改导致异常
            List<ItemDrop.ItemData> items = new List<ItemDrop.ItemData>(pInv.GetAllItems());

            foreach (var item in items)
            {
                if (item.m_equipped || item.m_gridPos.y == 0) continue;
                string itemName = item.m_shared.m_name;

                foreach (var box in containers)
                {
                    if (box.IsInUse()) continue; // 互斥保护
                    Inventory bInv = box.GetInventory();

                    if (bInv.HaveItem(itemName))
                    {
                        if (bInv.AddItem(item))
                        {
                            pInv.RemoveItem(item);
                            bInv.m_onChanged?.Invoke();
                            transferred = true;
                            break;
                        }
                    }
                }
            }

            if (transferred)
            {
                pInv.m_onChanged?.Invoke();
                player.Message(MessageHud.MessageType.Center, "已自动存入周围箱子！");
            }
        }

        // ==========================================
        // 功能 4：箱内碎片整理与多级排序
        // ==========================================
        private static void SortAndDefrag(Inventory inv)
        {
            List<ItemDrop.ItemData> items = inv.GetAllItems();
            if (items == null || items.Count == 0) return;

            // 1. 合并未满堆叠
            for (int i = 0; i < items.Count; i++)
            {
                var item = items[i];
                if (item.m_stack >= item.m_shared.m_maxStackSize) continue;

                for (int j = items.Count - 1; j > i; j--)
                {
                    var other = items[j];
                    if (item.m_shared.m_name == other.m_shared.m_name && 
                        item.m_quality == other.m_quality &&
                        item.m_variant == other.m_variant)
                    {
                        int space = item.m_shared.m_maxStackSize - item.m_stack;
                        int take = Mathf.Min(space, other.m_stack);
                        item.m_stack += take;
                        other.m_stack -= take;

                        if (other.m_stack <= 0) items.RemoveAt(j);
                        if (item.m_stack >= item.m_shared.m_maxStackSize) break;
                    }
                }
            }

            // 2. 多级排序：类型 -> 名称 -> 品质 -> 堆叠
            items.Sort((a, b) =>
            {
                int c = a.m_shared.m_itemType.CompareTo(b.m_shared.m_itemType);
                if (c != 0) return c;
                c = string.Compare(a.m_shared.m_name, b.m_shared.m_name);
                if (c != 0) return c;
                c = b.m_quality.CompareTo(a.m_quality);
                if (c != 0) return c;
                return b.m_stack.CompareTo(a.m_stack);
            });

            // 3. 紧凑网格坐标重投影
            int width = inv.GetWidth();
            for (int i = 0; i < items.Count; i++)
            {
                items[i].m_gridPos = new Vector2i(i % width, i / width);
            }

            inv.m_onChanged?.Invoke();
        }
    }
}
