# 门店事件 CG 按实际店铺取景

2026-11-19。店铺经营事件使用触发时已选定的那家门店，按其主售行业选代表性画面，并以实际店名作为图注。电脑店沿用原来为检查、盘点、停电、漏水等制作的场景。宠物用品店、服装店、模型手办店、二手旧物店和餐馆各有独立背景。85「邻居换招牌」用固定剧情对应的手机店。

检查、熟客到访、订单、找零、商品促销和漏水等从它们原有的门店、订单或收据取得门店。选项结果使用同一事件绑定的店铺，延迟复查和熟客到访也通过原记录传入。多店停电优先选择玩家当前所在的受影响门店，否则显示其中一家受影响门店，原来的多店结算照常。非电脑店的停电、应急灯、蜡烛、火扑灭后、恢复供电和下雨使用店型图叠加相应灯光和道具。场景按店型呈现，具体家具摆放仍以实际 3D 店铺为准。

静态核对 122 个有效事件的原有引用、六张新增场景、五张透明道具、Unity 资源名和 GUID，以及实际门店从绑定传到开场、选择结果和后续通知的路径。没有启动游戏，等待手动验收。

## 生成提示

内置 ImageGen，沿用暖色微缩 3D 和圆润哑光的湖城美术。背景为 16:9 全幅，从门口临近取景，右侧留人物叠图位置；透明道具 单件输出，少细节，保留透明通道。人像沿用游戏素材。

| 资源 | 图像要求 |
| --- | --- |
| `event_shop_pet` | A modest pet SUPPLIES store with a tidy muted sage checkout, distinctly recognizable shapes of two pet-food pouches with small paw pictograms, one simple leash on a peg, a pet bowl and a rounded empty tan pet bed. A clean cream shelf. No live animals and no computer-product packaging. |
| `event_shop_clothing` | A small modern clothing shop with one pale oak clothing rack carrying four clean pastel shirt and light jacket silhouettes, folded clothing on a low counter, a narrow fitting mirror and a sage curtain. No mannequins that could look like people, no computer displays or electronic merchandise. |
| `event_shop_models` | A modern hobby figure and model shop with two clean low miniature shelves holding one small cream building model, one miniature teal boat and one simple friendly toy robot, some small plain model-kit boxes, and a muted sage light oak counter. No person, animated characters, computer parts or electronics on the shelves. |
| `event_shop_secondhand` | A small clean neighborhood secondhand collectibles store. A tabletop glassless display with a small used desk lamp, a muted cream vintage camera, a pocket watch on a soft tray and a compact used radio, light oak and sage shelves. A separate tidy small repair bench or toolkit very subtly in the back, no retro stylistic time period, no computer shop or electronics cartons. |
| `event_shop_restaurant` | A small everyday Chinese neighborhood restaurant from near the customer entrance, with one warm oak dining table, cream and sage upholstered booth, small neat front greeting counter and a view of tidy tables, a matte serving bowl and two simple dishes on the far left only. No shelves of PC boxes, no checkout monitor as focal feature. |
| `prop_emergencylamp` | A small standalone white wall-mounted emergency light unit with two visibly glowing amber bulbs, softly diffused short light only adjacent to the object |
| `prop_candle` | One small lit ivory wax candle resting on a short sage ceramic saucer, tiny warm flame |
| `prop_burntcarton` | One lightly scorched collapsed cardboard shipping carton and one compact red fire extinguisher next to it, no smoke or flames |
| `prop_leakbucket` | One small muted blue bucket with a couple tiny drops directly above the opening, no unrelated objects, no splash outside the bucket |
| `event_shop_phone` | Near the glass doorway of a contemporary small neighborhood MOBILE PHONE store, two tidy rows of matte tiny upright smartphone displays, display counter and sample cases, one cream awning frame and a hinted removable sign panel without letters. Clear screen rectangles and a few sage blue phone accessories, no desktop hardware or people. Match the established warm matte miniature 3D art. |
| `prop_messagephone` | One transparent miniature game overlay: a simple modern smartphone slanted very slightly three-quarters, one muted sage abstract message bubble on a clean warm white display, no lettering or number. Rounded matte forms, no accessory, floor or background. |
