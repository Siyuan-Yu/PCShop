# 事件 CG 换画（2026-11-19）

已将 `Assets/Resources/EventCg` 中原有的 29 个场景和道具资源换为暖色微缩 3D。场景 16 张，道具 13 张，沿用原来 3 张专用人物图。沿用现有资源名、Unity 元数据和事件分层规则。此次替换范围为已有 CG 的图像资源；事件触发、分支和无图事件不受影响。

- 背景以 1152 × 648 的 JPEG 保留 16:9 构图。人物和道具保留透明 PNG。专用人物为陈志衡的站姿、坐姿和张绍文的站姿，其他人物沿用现有头像回退。
- 构图以玩家能立即看清的单一物件为主，延续湖城的浅墙、浅木和灰绿家具。人物和结果道具出现的右侧留出可叠加空间。地铁口与店面是按游戏美术重新绘制的场景示意。饭局结算重新对齐了桌沿、坐姿人物和餐碗。
- 「停电了」四个分支以停电主图做图生图，保留门窗、柜台、货架和机位。应急灯、蜡烛、扑灭后的纸箱和来电恢复分别对应原有分支。
- 使用内置 ImageGen 生成，按下列提示词制作。用资源格式、透明通道、文件名和画面对照检查本轮替换，游戏内弹窗显示待试玩。

## 场景提示词

共用风格：16:9 full-bleed event CG, clean warm miniature 3D videogame scene, chunky bevels, matte clay and light wood, warm diffuse fill, soft contact shadows, modest contemporary Chinese lakeside suburb, cream walls with sage or terracotta accents, minimal textures, quiet bottom caption area. No retro decor, heavy texture, text, UI, borders or split panels. 模型店等室内素材参考游戏的简洁柜台、装饰和货架；只参考美术，不画 HUD。

| 资源名 | 分图提示 |
| --- | --- |
| `wallet` | Wallet on the rim of a clean cream street planter, dominant in the foreground, a small modern metro entrance just behind with simple gray-blue rail, a plain blue train pictogram, light warm sidewalk, one rounded shrub and a partial cream building. No people. Follow the left sample, simplify foliage and signage. |
| `blackout` | Use the sample's right panel for style and the gameplay screenshot for simple furniture and color. A contemporary computer shop at dusk from its front corner, left glass street door, center-left light oak and sage checkout with a black unlit flat POS screen, one bare unlit cream pendant, a back wall with one oak-and-sage parts shelf, three neutral PC boxes and a small unlit two-headed emergency fixture above, two small cardboard cartons at the far right. Cream tiled floor. It is a BLACKOUT: clearly dark, cool shadow inside and a narrow, dim strip of warm sky light from the glass. Pendant, POS and emergency bulbs entirely off. No people, no other lights or candle. |
| `inspection` | Daylight in a modest computer shop. Center-left light oak checkout desk with one plain clipboard on it. One tiny wall-mounted emergency light and one tidy shelf with a couple of parts boxes behind. No people, no lettering or authority badges. Leave the middle-right clean for a document overlay. |
| `thief` | A tidy computer parts store inventory check. Center-left sage and oak shelf with a clearly empty slot between two simple plain graphics-card cartons; one small clipboard on the far end of the counter. Normal daylight. No actual thief, no people, no signs, no missing-person silhouettes; this is discovered missing stock. Quiet middle-right for overlays. |
| `regulars` | Bright PC shop entry and checkout in day, glass storefront at left, simple sage and pale wood checkout at center-left and a few parts boxes. No people in the background, blank warm cream wall and floor on right third for a separate full-size character layer. A friendly ordinary shop, no clutter. |
| `regulars_visit` | View from the light oak checkout across a modest modern PC shop toward a propped-open glass door on the left, soft bright daytime sidewalk outside, single display box at far left. Empty quiet space in right third for separate guest portrait. No people, one clear scene. |
| `students` | Daylit computer shop, focus on two to three clean boxed computer components atop an oak checkout counter on left, another plain shelf behind, customer-side viewing angle, right half relatively quiet. No people, no printed typography on packaging. |
| `rival` | Contemporary neighborhood street outside another small PC shop, glass storefront with a restrained red-orange sale accent on the left, one clean pedestal with a boxed GPU silhouette, cream walls and sage window trim, single potted plant, quiet right side for sale overlay. No readable signage, no lettering, no crowds. |
| `event_wallet_station` | Inside a modest modern metro station by a rounded light gray station-service desk, gray-blue floor and muted blue accent strip, anonymous train pictogram on the far left, a hint of station barrier. Leave counter empty and middle-right clear so a wallet prop and occasional portrait can be overlaid. No people, no readable words. |
| `friend_meal` | Contemporary warm neighborhood restaurant, view from across the table facing a cream and sage booth, window on the left. The far edge of the clean full-width oak tabletop runs horizontally at 72% from the top, with room for the dish below and a seated character at the right above the edge. No food or people. |
| `classmate_help` | Cozy neighborhood restaurant booth used to meet an old classmate, table left-center viewed at three-quarter angle, with a closed slim laptop set far left and a single water cup, pale oak and cream, warm window light, no food or people. Right third clear for a separately composited standing or seated character. |
| `lend` | A quiet modern neighborhood conversation spot with a small pale-wood cafe table at left foreground, two simple chairs, warm cream wall, glass window onto the street, subdued daylight. No envelopes or cash baked in because they are added above this layer. Empty middle-right, no people. |

## 停电分支提示词

以新版 `blackout` 为图像参照。保持相同视点、焦距、构图及未涉及的物件，只改对应状态。

| 资源名 | 分图提示 |
| --- | --- |
| `blackout_light` | Switch ON only the small two-headed emergency fixture above the shelf. Both tiny bulbs emit clear creamy pale pools on the adjacent back wall and softly reveal the shelves. Keep the hanging pendant and POS completely OFF, the dusk view outside identical and everything else exactly in place. No other light source. |
| `blackout_wait` | This is two hours later and power has returned. The view through the same glass door is now dark blue early night, with distant soft lit windows. Switch ON the existing central white pendant, casting a clear warm pool on the counter. Give the POS a subtle neutral powered glow only if its edge is visible. Keep the small emergency fixture inactive. No candle. |
| `blackout_candle` | Add ONE small lit cream wax candle in a short, plain teal saucer on the front checkout countertop to the left of the existing dark POS. Its tiny warm flame radiates a tight local glow on the counter. Keep the pendant, POS and emergency unit off and entire shop generally dim. Boxes intact. Do not add anything else. |
| `blackout_fire` | The fire has ALREADY been put out. Replace the two clean cardboard parcels at far right with two slightly collapsed charred parcels in the same positions and a tiny irregular dry soot mark directly below. Show one used compact red extinguisher resting beside those parcels, and the remains of a small extinguished candle. No active flame, smoke cloud, fire glow, or person. Keep pendant, POS and emergency fixture off. |

## 透明层提示词

单独渲染一个可叠加的透明物件或角色，采用暖色微缩 3D、哑光浅影、圆润简化轮廓，小尺寸也能辨识。不要背景、地台、墙面、文字和额外道具。道具保留原事件含义。角色直接使用游戏已有透明人物图，由事件界面叠在新版场景上。
