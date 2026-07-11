# Techcronoss Chinese Translation

Independent BepInEx translation plugin and translated story text for Techcronoss X.

- `TechcronossTranslation/` contains only the plugin source.
- `translations/zh-Hans.json` contains the translated text downloaded by the offline launcher at runtime.
- Story strings are replaced in `Garnet.Novel.Utility.NovelText.CreateInfo` before typewriter parsing, so Japanese text is never submitted to the story renderer.
- The rounded Simplified Chinese font and its atlas material are bound in `RubyTextMeshProUGUI.ForceMeshUpdate` before the story mesh is generated.

Removing `TechcronossTranslation.dll` restores the original Japanese display and does not affect the offline backend.
