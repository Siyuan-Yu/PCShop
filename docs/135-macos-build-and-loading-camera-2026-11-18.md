# Mac 构建与加载相机（2026-11-18）

## Mac 构建入口

新增 `MacPlayerBuild.Run`，输出路径通过 `-pcshop-build-output` 指定为 `.app`，拒绝覆盖已有应用。

- Unity 2022.3.6f1，使用同版本官方 Mac Build Support (Mono)。
- 目标为 macOS 通用架构 `x64ARM64`，同时包含 Intel 与 Apple Silicon。
- 主场景 `Assets/Scenes/Main.unity`，使用完整地图预生成入口，非第一阶段 WebGL 试玩内容限制。
- 1920×1080 可调整大小的窗口，允许后台继续运行，LZ4HC 压缩。
- 保留最新报表 UI 等当前源码改动。
- ZIP 按 Unix 文件权限保存应用目录及 Mach-O 文件，避免 Windows 打包时丢失 Mac 启动文件的执行权限。
- Windows 交叉构建无法代替 Mac 真机启动验收；本地包不提供 Apple Developer ID 签名和公证。

## 加载时“No cameras rendering”

此前增量加载为节约渲染开销直接禁用主相机。加载界面虽然使用 Overlay Canvas 正常显示，Unity Game 视图仍会叠加“No cameras rendering”。

现改为在加载期间保留相机启用，临时将剔除遮罩设为 0 并使用纯色清屏，不绘制逐步构建的城市及其阴影。无论加载成功还是异常退出，都会在 `finally` 中恢复原剔除遮罩、清屏方式与启用状态。

## 本次产物

- 构建标识：`macOS-20261118-195623`，Unity 报告 `MAC_BUILD_RESULT Succeeded`。
- 应用目录：`F:/PCShopSell/output/macOS-20261118-195623/PETSHOP.app`。
- 应用构建大小：218,214,676 字节；交付 ZIP 大小：146,833,416 字节。
- 已核对主启动文件为包含 x86_64 与 arm64 的通用 Mach-O，压缩时保留执行权限。
- ZIP SHA256：`59F2F71E144F29E970D8B7ECA4A23506196B57FA40D9DFEDAC3FA3C21CBA2426`。
- 包含最新经济／物价界面和加载相机修正；未运行测试套件，未进行 Mac 真机启动验收。本次 GitHub 同步仅提交源码与中文文档，不上传应用、ZIP 或安装模块，也不创建 Release。
