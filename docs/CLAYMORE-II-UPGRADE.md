# 升级时如何保留 Claymore II 修复

## 给下一个维护者

先读根目录 `README.md` 和 `handoff.md`。核心修复是提交 `32d9b3c6dcaf54f45d9c4ba0856f30b490db4c24`；它只改变七个生产代码文件。后续文档/测试提交与它分离。

本分支的完整源码已包含全部修复，不能再把补丁应用一次。现有仓库的 `main` 和其他开发分支未被覆盖。

## 建议流程

1. 确认用户指定的目标上游版本是否存在；不要把当前定制版直接替换为官方 EXE。
2. 从目标上游 tag 建立新的升级分支，保留当前分支作参考。
3. 先检查上游是否已经实现相同功能。若没有，cherry-pick 核心修复；出现冲突时逐项对照下面的职责表，不能整体覆盖新版文件。
4. 将本分支的离线测试、地址 fixture、构建脚本和说明迁移到升级分支，并按目标 SDK/目录结构调整。尤其更新构建脚本中的版本标识。
5. 在 Windows 构建并运行离线检查；对照两套 ANSI 的 87 个地址及 ISO 扩展键。
6. 报告剩余验证风险。用户同意后才备份配置、退出运行程序并安装。用户要求自行测试时，不做 UI/灯效测试。

下面是命令示意，`vX.YYY` 必须替换成已验证存在的版本：

```powershell
git remote add upstream https://github.com/seerge/g-helper.git # 已存在则跳过
git fetch upstream --tags
git switch -c upgrade-claymore-vX.YYY vX.YYY
git cherry-pick 32d9b3c6dcaf54f45d9c4ba0856f30b490db4c24
# 处理冲突后，迁入 tests/ClaymoreSpatialSync、scripts/build-claymore.ps1 及维护说明
```

如果只能使用补丁：`patches/claymore-ii-v0.279.patch` 保存了原始 diff。先 `git apply --check`；新版上游未必能直接应用，失败后需人工适配，不要强行覆盖。

## 七个文件的职责

| 文件 | 不能丢失的行为 |
|---|---|
| `app/Peripherals/Keyboard/Models/ClaymoreII.cs` | 有线 PID 1934、无线 PID 196B、空间同步能力、左右位置共享配置与 ANSI/ISO 布局选择 |
| `app/Peripherals/Keyboard/AuraKeyboardLayouts.cs` | 二代专用 87 键映射、左侧偏移 0x20、ISO `<`/`#`/Enter；保留一代表 |
| `app/Peripherals/Keyboard/KeyboardSpatialColors.cs` | 生成 Gradient 四区颜色，按物理键位中心进行横向颜色插值 |
| `app/Peripherals/Keyboard/AsusKeyboard.cs` | 完整帧互斥、只保留最新待发帧、模式/同步代次校验、亮度缩放、重复 LED 合并、14 LED 分包及 ACK、非空间模式回退 |
| `app/Peripherals/PeripheralsProvider.cs` | 有线设备注册、四区转发、Ambient 最新帧缓存、Gradient 当前配置、同步失效代次 |
| `app/USB/Aura.cs` | 数组颜色不能被压成首色；效果切换时使旧帧失效 |
| `app/AsusKeyboardSettings.cs` | “小键盘位置”选择、位置变更后重放当前同步颜色；避免写固件或键位绑定 |

#### 不能重复的旧错误

- `ClaymoreNoNumpad` 是一代地址表，不能直接给二代使用。
- 小键盘装在左侧时，9、0、O、P、J、L、N、M 的主键 LED 地址分别是 `71 79 72 7A 63 73 5C 64`（十六进制）。旧表恰好没有覆盖这些地址。
- 只验证“发送代码符合生产表”会遗漏“生产表本身错误”。保留独立的 CSV 参考检查。
- 不要把设备响应的未确认字段当作小键盘安装位置。当前实现是手动配置 `claymore_ii_numpad_left`。
- 不要盲目引入其他插件的完整帧协议、宏、电源或固件控制。此修复保留原有 14 LED/包与 ACK 方式。
- 编译一定带 `-p:GITHUB_ACTIONS=true`，否则上游构建目标可能退出用户正在运行的 G-Helper。

## 配置与部署约束

相关配置为 `keyboard_aura_sync`、`claymore_ii_numpad_left`、`skip_updates`。它们是用户配置，不应提交到 GitHub，也不是运行中进程的通用控制接口。修改前应退出程序并备份。

启动路径、自启动任务和开始菜单快捷方式属于本机部署，不包含在代码补丁内。移动程序时需同步更新这些入口。已有高权限计划任务可能需要用户确认 UAC。不要复制他人的用户 SID 或绝对路径。

## 验证边界

离线测试不写真实设备、不会抓屏。用户报告当前有线映射修正版可用；无线、重连、睡眠唤醒、所有键盘布局及长期运行仍需在需要时单独验证。主键区空间同步不包含可拆卸数字键盘、Logo、额外媒体灯。
