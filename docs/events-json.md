# events.json 说明

文件：`Assets/Resources/Data/events.json`。随机事件的内容、触发时间、概率、权重都在这里改，改完不用动代码。
**推荐用外部事件编辑器改**：`ExternalTools/EventEditor`（见文末）。
Unity 菜单 `PCShop/事件/事件清单与触发` 可以看清单、校验格式、运行时一键触发；`PCShop/事件/校验 events.json` 只做校验。
单元测试 `EventScriptTests` 会在提交前检查格式（包括 json 必须是编辑器的标准格式）。

## 触发规则（代码里，`GameSession.Events.cs`）
游戏第 2 天起，每天 11:00、15:00、20:00 各抽一次（`EventSlotChance` = 0.4；条件都满足时约 1.2 个/天，实际受条件限制会少一些）；要有店、没有未处理的事件。
**频率保障**（`GameSession.Events.cs` 里的常量）：每天最多 `EventDayCap` = 3 个，任意连续 7 天最多 `EventWeekCap` = 10 个；前 `EventEarlyDays` = 14 天（第 0–13 天）概率只有一半（`EventEarlyFactor` = 0.5）。约会、剧情这类非随机事件不占名额。
依次筛：`minDay` ≤ 今天 ≤ `maxDay`、冷却、`when` / `whenAny`，再对剩下的每个事件各掷一次 `triggerChance`（没写 = 必过），最后按 `weight` 抽一个。

## 一个事件
```json
{
  "id": "flyer", "title": "标题", "body": "正文", "minDay": 2, "maxDay": 30, "cooldownDays": 10, "weight": 9, "triggerChance": 0.5,
  "when": [ {"type": "cash", "value": 100} ],
  "choices": [
    { "text": "选项文字", "disabledReason": "灰掉时的提示",
      "requires": [ {"type": "cash", "value": 100} ],
      "effects": [ {"type": "pay", "yuan": 100, "memo": "传单"}, {"type": "text", "text": "结果文字"} ] }
  ]
}
```
- `minDay` / `maxDay`：最早 / 最晚第几天（开局那天是第 0 天）。不写 `maxDay` = 没有上限；`30` = 只在开局前 30 天内出现。
- `triggerChance`：0–1，轮到它时再掷一次，没掷中就当没有；不写 = 100%。
- `cooldownDays: 9999` = 一局只会触发一次。
- `when`：全部满足才可能被抽到；`whenAny`：其中至少一条满足。
- `requires`：全部满足这个选项才能点，没满足灰掉并显示 `disabledReason`（写了 requires 就必须写）。至少要有一个没有 requires 的选项。
- `group: "npc"`：围绕固定人物的事件。
- `code: true`：内容在 C# 里（`regulars` / `students` / `rival` / `review` / `zhou_deal`），json 里只负责排序，并可覆盖 `minDay` / `maxDay` / `cooldownDays` / `weight` / `triggerChance`。
- 事件按 json 里的顺序参与抽取。

## 掷骰
选项里写 `chance`（0–1）就会在 `effects` 执行完后掷一次：掷中执行 `onHit`，没中执行 `onMiss`。
概率 = `chance` + 魅力×`chanceCharm` + 智力×`chanceIntel` + 交情×`chanceRelation`（人物 `chanceNpc`）+ 命中的 `chanceBonus`（`{"when": [条件], "add": 0.2}`）。

## 条件 `type`
`cash`(现金≥value 元) · `cashBelow`(现金<value 元，填 0 = 账户为负) · `negativeCashDays`(账户已连续为负≥value 天) ·
`flag`(key) · `upgrade`(key，店铺升级) · `stock`(店里有货) · `samePlace`(key=人物) · `met`(key=人物) ·
`relationAtLeast` / `relationBelow`(key=人物, value) · `rentHikesBelow`(value) · `energyBelow` / `sleepDebtAbove` / `healthBelow`(value) ·
`hasPhone` · `bankDeposit`(银行存款≥value 元) · `hasLoan`(有未还贷款) · `reputationAtLeast`(名望≥value) ·
`racingUnlocked`(已有能比赛的车) · `racingWins`(赛车累计胜场≥value) · `netWorthAtLeast`(身家≥value 元) · `charmAtLeast` / `intelAtLeast`(value)。任何条件加 `"not": true` 取反。

## 效果 `type`
`text`(text) · `pay` / `gain`(yuan, memo) · `shopRep`(value，本店口碑) · `rep`(value，名望) · `charm`(value) · `health`(value) · `energy`(value，消耗精力) ·
`rest`(value 分钟) · `flag`(key) · `loseItem`(随机丢一件货，text 里可用 `{item}`；`alt` 是没货时的文字；`stopIfNone` 没货就中止后面的效果) ·
`relation`(npc, value) · `meet`(npc, value) · `markup`(value) · `lend`(yuan, memo=借款人) · `repay`(days, yuan, memo，N 天后到账) ·
`intel`(value，智力) · `raceInvite`(npc=赛车爱好者, key=赛场 id，默认 `race_0`：约对方参加下一场正式比赛，约不成会在结果里写明原因) ·
`meeting`(npc, kind, key，约真实活动，`flag: true` 表示由对方做东；kind 可选 `dinner` `ktv` `bar` `court` `golf` `fishing` `hike` `snack` `market` `old_market` `expo_electronics` `expo_comic`) · `referral`(key, memo=客户名, count, flag=是否优惠价) · `rentHike` ·
`interestFreeLoan`(npc=出借人, yuan, count=期限月数，1 月 = 30 天)：对方借钱给玩家，不收利息，到期自动扣回本金，出现在手机「周转借款」列表里并标注“无息”。
`text` 和会产生提示的效果（`repay` / `meeting` / `referral` / `interestFreeLoan`）按顺序拼成结果弹窗，用换行隔开。

人物 id：`zhang` `landlady` `lin` `zhou` `jie`，另有 `res_coder`（老同学陈志衡）等，完整名单见 `ExternalTools/EventEditor/npcs.json`。

## 例：开局 30 天内，账户连续 3 天为负，老同学请吃饭并无息借 5000
见 `events.json` 里的 `classmate_help`：`maxDay: 30`，`when` = `negativeCashDays ≥ 3` 且 `cashBelow 0`；选项 1 = `meeting`（陈志衡做东）+ `interestFreeLoan`（res_coder，5000，3 个月）。

## 外部事件编辑器
`ExternalTools/EventEditor`（WinForms，`dotnet build -c Release`，运行 `bin/Release/net10.0-windows/EventEditor.exe`）：
- 自动找 `Assets/Resources/Data/events.json`。左边事件列表（可筛选、新建、复制、删除、调序），右边基本信息、触发时机（最早/最晚天数、冷却、权重、触发概率）、触发条件、选项（条件 / 效果 / 掷骰）。条件和效果按类型只显示用得到的字段，人物从下拉里选。
- C# 实现的事件（`[C#]`）只能改时间 / 权重 / 概率数字。
- 人物名单来自 `npcs.json`，由 Unity 菜单 `PCShop/事件/导出人物名单给事件编辑器` 生成（游戏里加了新人物后重新导出）。
- 和游戏共用 `EventScriptModel.cs` / `EventScriptReader.cs` / `EventScriptWriter.cs`，格式和校验永远一致；保存时按标准格式输出，git diff 干净。
- 命令行：`EventEditor.exe --validate [路径]`、`EventEditor.exe --reformat [路径]`（输出要重定向才看得到，如 `| Out-String`）。
- 新增条件 / 效果类型：在 `EventScriptModel.cs` 的 `EventScriptSchema` 里加一行，再在 `EventScript.cs` 里写执行；编辑器里会自动出现新类型。
