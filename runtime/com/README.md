# 官方 COM 参考执行路径

这些 C# 类来自 2026-09-14 本地实测，保留实际算法；去除了机器专属模板路径。它们不是新的 MCP 服务，也不是任意 CAD 的通用建模系统。**本次整理只做离线检查/编译，不重复 CAD 测试。**

## 文件分工

| 文件 | 实际职责 |
|---|---|
| AssemblyExtract.cs | 只读提取当前装配的组件树、变换、配置、BOM 基础字段与内部/顶层配合；要求核对原文件路径 |
| GripperPrepare.cs | 从经授权的源模型读取特征、孔/轮廓与配合实体的几何描述；导出源视图 |
| GripperBuild.cs | 从尺寸 JSON 建立拉伸、孔、长圆槽、圆缺口和带钻尖的盲孔回转切除 |
| GripperAssemble.cs | 复制采购模型、重定向引用、插入组件、匹配几何面/边、建立真实配合 |
| GripperValidate.cs | 比较自制件：合法实体、特征错误、草图约束和布尔对称差 |
| GripperAssemblyCheck.cs | 保存重开、层级数量、位姿、配合错误、自由度、路径与干涉基线 |
| DirectionProbe.cs | 读取原/新装配的面外法向、底层曲面方向及配合参数，定位翻转原因 |
| Invoke-Reference.ps1 | 编译/显式执行入口；默认只编译，不连接 SolidWorks |
| Record-Reference.ps1 | 源自主屏录制封装；显式 -Execute 才录屏和建模，FFMPEG_EXE/路径参数须自行配置 |

## 运行环境

Windows PowerShell 5.1、合法安装并已启动的 SolidWorks、安装自带的 .NET interop DLL。实测环境为 SolidWorks 2026 SP0.0；不保证其他版本。执行时同一 SolidWorks 会话只能由一个客户端控制。

每个动作使用一个新的 Windows PowerShell 进程，按下文 `powershell.exe -File ...` 方式调用。参考类使用静态集合，尚未设计为可重入长驻服务；不要在同一 PowerShell/.NET 会话重复直接调用 Run/Parts/Mates 等方法。录屏入口只在其新进程内编译并执行一次动作。

环境变量与参数二选一：`SW_INTEROP_DIR`、`SW_PART_TEMPLATE`、`SW_ASSEMBLY_TEMPLATE`。不要把机器实际路径提交到 Git。

```powershell
# 默认只编译，绝不新建零件或连接 SolidWorks
& '<WINDOWS_POWERSHELL_5_1>' -NoProfile -File runtime/com/Invoke-Reference.ps1 `
  -InteropDirectory '<SOLIDWORKS_API_REDIST>'

# 以下为以后明确授权的运行示例；本次整理不执行
& '<WINDOWS_POWERSHELL_5_1>' -NoProfile -File runtime/com/Invoke-Reference.ps1 `
  -Mode Build -Execute -RunDirectory '<PRIVATE_RUN_DIR>' `
  -InteropDirectory '<SOLIDWORKS_API_REDIST>' -PartTemplate '<PART_TEMPLATE>'
```

源模型读取模式额外要求 `-AllowSourceRead`。这只是显式操作确认，不是操作系统沙箱。私有 RunDirectory 必须在公开仓库外；输入必须由可信、获授权流程准备。

录屏器不自动调整窗口布局。执行前人工确认主屏包含希望展示的应用，并抽检画面；本轮曾只录到最大化 SolidWorks。该包装保留原录制方式，没有在此次整理后再次进行真实录屏测试。

## 私有输入契约

```
<PRIVATE_RUN_DIR>/
  inputs/
    assembly_original.json       源组件路径、实例树、原始变换
    part_specifications.json      id、profile_axis、thickness、polygon、holes、slots、notches、blind
    mate_specifications.json      配合类型/对齐、实例、plane/cylinder/circle_edge 参数及面包围盒
  models/                        新建 Pxxx.SLDPRT、FullGripper.SLDASM
  vendor/                        采购件和子装配副本
  checks/                        测量与诊断结果
  recordings/                    建模事件记录（录像需另外显式启动）
```

尺寸 JSON 的长度为 mm，面/边几何和 COM 变换中的长度为 m，回转角为 rad。原始输入不会随仓库上传；完整工业尺寸表也不会作为“代码”变相公开。

## 必须知道的案例绑定

当前装配参考实现仍识别 `14AK-` 自制件前缀、以 `14AK-046-1` 为固定基准，并使用 Pxxx 输出命名、43 条配合的展示分母和原层级名称。验证器也沿用这些约定。因此 **Build 的几何操作可以复用，但整套 assembly runner 不是模型无关成品**。换模型必须先做映射/基准配置改造并重新验证；此次用户要求不做新测试，所以没有借整理之名扩建并宣称验证。

`flip=0` 的平面修正只适用于已验证几何等价且正确对应的边界面。未来通用实现应显式比较正确坐标系中的真实外法向，不能推广为“一律不翻转”。圆柱轴正反约定另行处理。

Build 遇到同名模型会跳过，不代表该文件通过检查；Assemble 遇到已有最终装配会停止。其他诊断输出可能覆盖同名日志，使用新的运行目录或先归档。输出路径/模板、供应商文件重名、引用替换返回值等仍需人工审查；不要直接暴露给不可信远程输入。

本次公开包没有包含带工业尺寸的图纸生成案例脚本、原始尺寸输入、CAD、截图或录像。现有 PDF 输入格式与生成经验见汇报和工作流文档；不能宣称此包已做到一个命令从任意 PDF 自动建模。
