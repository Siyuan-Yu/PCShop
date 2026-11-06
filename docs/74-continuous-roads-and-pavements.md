# 实际地图道路与人行道重叠整理

2026年11月6日，按用户要求检查真实地图。使用当前地图数据与正式StreetWorldBuilder在隔离Unity工程截取全城及城郊、社区、公园、花园同机位鸟瞰，未读取玩家存档。

## 实景发现与修复

原道路逐段生成有延伸端头的长方体，每条边各铺两条人行道、边线和车道标记；路口与弯道没有处理交叠区域。地图还有168段不足3米的连接边，给短段同样添加宽端头后，容易在密集节点处重复铺路。建筑入口步道从路网节点直接画到门前，因节点在车道中央，白色步道覆盖了半条车道。赛场接入处另加的路堤又画了一层路面和路沿。

当前湖城改为共用道路表面：

- 道路按真实中心线和路宽生成圆接头，在相交区域裁掉重复表面，交叉路口与弯道各处只保留一层沥青。
- 人行道先减去全部车道范围，再与其他人行道合并，弯道和路口不再互相盖住，也不会横穿机动车道。
- 建筑入口步道接到人行道外缘，车道内的部分不再绘制；保留原门的位置与实际可走入口。
- 边线与中心虚线按道路方向铺设，裁掉明显交叉路口中的重叠段；短段继续画线。斑马线按真实道路方向设置，不再对所有节点统一按世界坐标横放。
- 赛场接入路只保留一层共同路面，原独立路堤降为水面下的支撑；入口坡道、车行底座同步调整高度，去掉横挡路口的重复路沿。

修复针对实际铺装位置与交接边界。未移动住宅、商铺、赛场或寻路节点，没有用修改地标位置来掩盖叠层，也未删除短连接节点。它们关联旧档、NPC路线和街区进度，视觉铺装现在正确地共用边界。两座原有封闭景观桥及其封路设施仍沿原规则保留。

## 实际对照图

以下均为实际Unity场景，使用相同取景和光照。

| 片区 | 整理前 | 整理后 |
| --- | --- | --- |
| 城郊 | [原图](previews/road-cleanup/before/outskirts.png) | [新图](previews/road-cleanup/after/outskirts.png) |
| 社区 | [原图](previews/road-cleanup/before/community.png) | [新图](previews/road-cleanup/after/community.png) |
| 公园 | [原图](previews/road-cleanup/before/park.png) | [新图](previews/road-cleanup/after/park.png) |
| 花园 | [原图](previews/road-cleanup/before/garden.png) | [新图](previews/road-cleanup/after/garden.png) |
| 全城 | [原图](previews/road-cleanup/before/city.png) | [新图](previews/road-cleanup/after/city.png) |

## 实现与验证

道路几何使用凸多边形差集和32米空间索引，只在构建场景时计算；绘制分沥青、人行道、白线、黄线四个网格，运行中不逐帧计算裁剪。生成的网格由场景资源组件释放。旧非湖城地图仍保留原生成路径。

Unity无图形联合检查76项中72通过、4失败，见[测试报告](validation/74-road-tests.xml)。新增6项几何检查全部通过，覆盖十字路口面积、远离原点的数值精度、反向重复边、急弯、建筑入口步道、相邻铺装，以及当前全城路网抽样的车道中心覆盖和人行道不压车道。行走、出行耗时与赛车专项通过。当前道路生成一次约2390毫秒、271467顶点，属于本机编辑器测试数据，不代表运行帧率或其他机器加载耗时。

4项失败均已在[第59篇验证记录](validation/59-current-archive-results.md)记录，名称及结果一致：

- CityLayoutV7Tests.ConfirmedPlanHasFourAgencyDistrictsAndNoStarterShopStation：旧预期版本8，当前14。
- CityLayoutV7Tests.LayoutMigrationDoesNotReplayCashOrDuplicateAssetsAndThenIsIdempotent：旧预期版本8，当前14。
- CityLayoutV7Tests.ResidentialSceneryHasMultipleRowsAndDoesNotCollapseToFourApartmentBlocks：旧片区住宅数量预期45，当前19。
- CityLayoutV7Tests.MetroTravelsToChosenStationRatherThanLastEnumeratedStation：旧测试出行条件未满足。

本次未改地图JSON，未宣称修复上述旧用例。真实图形构建通过原主路线步行1440分钟和初始住所检查，五张最终截图输出成功，四个片区已逐一查看。没有人工逐路走遍全城，也没有重测运行帧率。

本次不自动提交GitHub、不打Windows包。上一轮统一代言人与KOL以及其他本地修改保留。
