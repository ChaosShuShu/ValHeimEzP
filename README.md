# ValheimEzPlay (英灵神殿便捷生存增强 Mod)

专为提升《英灵神殿》（Valheim）生存与建造体验打造的轻量级 Quality-of-Life (QoL) 模组，基于 BepInEx 5.x 与 Harmony 构建。

## ✨ 核心特性

1. **工作台附近箱子联动**：
   - 制作和升级装备时，自动搜寻周围 10 米内的可用箱子，直接提取箱内材料。
2. **铜矿减重**：
   - 将原版极为笨重的铜矿石重量从 `10.0` 降为 `7.5`（降低 25%），支持自由配置。
3. **一键自动分类存入 (Smart Quick-Stack)**：
   - 按下快捷键 `N`，自动将背包中非快捷栏/非装备的物品，分类归入周围“已存有同类材料”的箱子。
4. **箱内碎片整理与多级排序**：
   - 打开箱子时按下快捷键 `R`，自动合并未堆满的材料堆叠，并按类别、名称、品质重新紧凑排列。
5. **完整配置系统**：
   - 首次运行后自动生成配置文件 `BepInEx/config/com.chaos.valheim.ezplay.cfg`，所有数值与按键均可自由调整。

---

## 🛠️ 构建与编译

本项目已实现跨平台与本地路径解耦，可在 Linux (Arch/Ubuntu) 或 Windows 环境下直接构建。

1. **环境准备**：
   - 安装 `.NET SDK 8.0` 或更高版本。
2. **配置本地路径**：
   - 复制 `Environment.props.example` 为 `ValheimEzP/Environment.props`。
   - 填写你本地的 `valheim_Data/Managed` 与 `BepInEx/core` 目录。
3. **编译**：
   ```bash
   dotnet build -c Release ValheimEzP/ValheimEzP.csproj
   ```
4. **安装**：
   - 将生成的 `ValheimEzP/bin/Release/netstandard2.1/ValheimEzP.dll` 复制到游戏的 `BepInEx/plugins/` 目录即可。
