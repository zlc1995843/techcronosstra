# TechcronossTranslation

《テクロノスX》独立简体中文翻译 Mod，结构参考 AbyssMod、MuvluvMod 和 GCMod。

## 特点

- 独立 BepInEx IL2CPP 插件，不依赖离线后端。
- 不修改游戏文件和 Master 数据；删除插件目录并重启游戏即可恢复日文。
- 当前收录安德洛墨达（水着）的 6 段剧情，共 551 条剧情文本。
- 支持富文本和逐字显示中的原句替换，不注入剧情状态机或网络流程。
- 有中文词条时优先显示中文；没有词条时保留日文原文。
- 剧情译文使用 `AbyssMod` 同款 `ttcuyuanj` 圆体 TMP 字体包，并在显示前校验全部译文字形；字体未加载或缺字时保留日文，不显示方框。
- 默认不写入缺失词条记录，避免首次进入场景时发生集中磁盘写入。

## 安装

将单文件启动器放到游戏根目录，通过“设定”选择是否激活中文，然后点击“打开游戏”。插件和翻译词典均内置于启动器中。

## 卸载

删除 `BepInEx/plugins/TechcronossTranslation`，然后重新启动游戏。

## 构建

```powershell
dotnet build TechcronossTranslation.sln -c Release -p:GameDir="F:\path\to\techcronossx_cl"
```

翻译 API 密钥不会保存在仓库中。翻译生成工具仅从环境变量 `DEEPSEEK_API_KEY` 读取密钥。
