# 社区 MCN 与 KOL 联系（2026-11-03）

## 玩家流程

- 社区新增固定场所「澄光传媒 MCN」，09:00—18:00接待。地图、实际移动和场所入口共用同一个地点。
- 莫文宁、柯承安、乔雨桐在这里工作；公司也能介绍陆栀、温岚。人物仍沿用原来的身份、关系、故事与活动日程。
- 在现场选择「请公司安排介绍」。人物已经在公司时可以直接见面；不在时按实际路线赶来，窗口显示预计到场时间。关闭窗口让时间继续，到场后交换业务名片。
- 介绍不会直接送私人好感。交换后保存手机联系方式，创作者初始业务关系为5，可从手机、品牌推广页或 MCN 页面联系并谈合作。已有其他正常途径获得的联系方式同样有效。
- 没见过的创作者不能直接付费推广、签代言、资助或签入 MCN 来绕过认识过程。活动中、被雇佣或已被其他关键会面占用的人物不能重复预约。
- 当天会面最多保留两小时，最晚至18:00；错过可以重新预约。介绍记录和已认识状态随存档保存。

## 自己开 MCN

- 社区只能接触现有 MCN；进入公园阶段后开放自办公司。
- 沿用原经营条件：有自己的门店、知名度25、成立费用¥50,000。基础日支出、签约创作者、资源和广告分成继续使用原系统。
- 这次增加的是阶段限制和认识渠道，**没有新增玩家可租用、装修的 MCN 专用办公室**。玩家自己的公司仍在经营管理界面操作。
- 旧存档已经成立的公司保留；已有付费推广、代言或 MCN 合约的合作对象保留联系方式，不重新收费认识。

## 开发与验证

- 地图版本升至14，新增场所保存在 `tools/data/city-layout-v8.json`，由 `tools/build-lake-city.py`生成。`tools/add-community-mcn.py`可幂等补充源数据。
- 核心测试覆盖社区禁止创办、公园允许创办、未认识不能付费绕过、实际赴约、营业时间、过期预约、存档恢复和旧付费关系。
- 使用隔离项目运行 Unity 检查，不改玩家存档、不自动打包。
- 验证结果：MCN、品牌经营、经营回归与人物内容合计 **62/62通过**；地图91个场所连通、道路与建筑碰撞检查通过；实际界面两张截图检查通过，运行错误为0。
- 记录：[测试结果](validation/56-mcn-community-results.xml)、[界面检查](validation/56-mcn-graphics.txt)、[介绍页面](previews/community-mcn/community-mcn.png)、[交换名片后](previews/community-mcn/community-mcn-contact.png)。

### 完整流程截图复跑

使用独立测试档，快进至真实路线预计到达时刻，并切换阶段验证创办条件。运行错误0；社区创办被拒绝，公园创办成功且实际扣款¥50,000。未修改玩家存档。

- [等待创作者到场](previews/community-mcn/community-mcn-waiting.png)
- [交换名片后的本人聊天](previews/community-mcn/community-mcn-dialogue.png)
- [社区禁止成立公司](previews/community-mcn/community-mcn-locked.png)
- [公园阶段满足条件可成立](previews/community-mcn/park-mcn-found.png)
- [成立后开始运营](previews/community-mcn/park-mcn-operating.png)
- [本次执行记录](validation/56-mcn-flow-graphics.txt)
