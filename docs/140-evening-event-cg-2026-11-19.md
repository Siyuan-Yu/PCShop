# KTV、灯塔酒吧与晚餐夜景

2026-11-19。暖色微缩 3D 指哑光材质、圆润造型和柔和光照，光源颜色和明暗由事件的地点与时间决定。

- 48–50 共用重画后的 `event_karaoke`。用无窗的紫蓝包间、点歌台、大屏、音箱和双麦表现 KTV，沿用资源原名和导入设置。
- 51–53 的开场 CG 共用新绘制的 `event_lighthouse_bar`。参考实际 3D 灯塔吧台的灰绿长柜台、深蓝背墙和瓶罐，保留夏澄、陶然、曾玉的已有立绘。原有到场互动照常。
- 46「社区餐厅领班约饭」限定 19:00 后，复用白天餐馆构图制作 `event_restaurant_evening`。窗外改蓝色夜景，室内只留桌前暖色吊灯。桌沿位置沿用，照常叠餐碗和现有立绘。
- 资源核对覆盖实际事件 ID、人物与图像引用。界面静态合成仅用于核对，实际游戏弹窗待试玩。

## 图像提示

使用内置 ImageGen。统一为 16:9 全画幅暖色微缩 3D，圆润、哑光、适合叠加已有立绘。KTV 和酒吧分别参考游戏的外观配色及灯塔吧台实景。

| 资源 | 场景要求 |
| --- | --- |
| `event_karaoke` | Completely reframe into an instantly recognizable Chinese private KTV box at night. Windowless enclosed room, looking diagonally toward the LEFT karaoke wall. Big illuminated TV playing a simple blue-purple music video silhouette with two abstract lyric progress stripes without readable glyphs, visibly separate touch-song-selector console with a grid of colored tiles, two wall-mounted black speakers, pair of wireless microphones prominently on a dark low coffee table and an L-shaped purple upholstered banquette. Dark acoustic panels, gentle lavender and blue LED strips in ceiling recess and under banquette with a small warm practical lamp, not glaring. Readable, unplug the restaurant associations: no natural lighting, no windows, no rice cooker or meals, no home kitchen and no stage or public audience. Leave right third quieter for existing character portrait. First reference is the mini-3D style only; depart from its brightness and restaurant composition. Second reference shows the actual game's muted purple KTV identity, not camera angle. |
| `event_lighthouse_bar` | Nighttime Lighthouse Bar, the game's existing neighborhood drinks bar. Use first screenshot to identify the long muted sage green solid counter, dark navy back wall, and staggered cylindrical bottles; reproduce those defining objects in a clean cinematic miniature close-up facing diagonally across the bar, no screenshot UI or tiny people. One long sage and dark oak cocktail counter with three small distinct sample drinks of berry, pale oolong and citrus on the LEFT center; an uncluttered back shelf with a handful of sealed silhouette bottles, one or two amber pendants, warm pools of local light, deep teal-blue shadow and navy night ambience from a side window only if visible. One line of warm backlighting, not fancy or cluttered. Right quarter quiet and low contrast for in-game portrait. No cream daylight, café seating composition, bright windows, rainbow lighting, text, people, or UI. The second reference shows the preferred miniature finish and cool nighttime shadows. |
| `event_restaurant_evening` | Edit the first reference, keeping EXACT camera angle, restaurant booth, window, foreground table, tabletop edge at 72% height and furniture positions. It is now after 19:00: through the window replace daylight with deep blue evening, dim lake water and a few warm windows across the shore, no visible sun; indoors the same single pendant casts a contained soft amber pool, with gentle cooler shadows in the corners. Warm cloth and natural wood accents, but unmistakably evening. No people, food, or added props. The second reference illustrates the desired twilight color only. |
