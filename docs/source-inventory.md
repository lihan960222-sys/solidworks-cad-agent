# 公开内容取舍与本地整理说明

本次在工作目录中建立独立的 `solidworks-cad-agent/` 仓库目录。原根目录脚本和所有历史结果保留，不批量移动或删除；后续维护以仓库中的整理版本为准。

## 纳入核心

| 本地原实现 | 仓库整理位置 | 取舍 |
|---|---|---|
| extract_assembly.ps1 内的 C# | runtime/com/AssemblyExtract.cs | 保留层级/配合提取；改为核对显式原文件路径，去除个人目录 |
| gripper_prepare.cs | runtime/com/GripperPrepare.cs | 保留几何、接口描述提取 |
| gripper_build.cs | runtime/com/GripperBuild.cs | 保留 JSON 驱动的几何操作；本机零件模板从环境/参数传入 |
| gripper_assemble.cs | runtime/com/GripperAssemble.cs | 保留采购件复制/引用、几何面匹配、真实配合、已验证的平面方向修正；案例绑定明确披露 |
| gripper_validate.cs | runtime/com/GripperValidate.cs | 保留原件辅助布尔几何对比 |
| gripper_assembly_check.cs | runtime/com/GripperAssemblyCheck.cs | 保留位姿、自由度、配合、重开、依赖和干涉检查 |
| gripper_direction_probe.cs | runtime/com/DirectionProbe.cs | 保留针对实体外法向和曲面方向的诊断 |
| gripper_record.ps1 | runtime/com/Record-Reference.ps1 | 录屏生命周期逻辑，机器路径外置，必须显式允许执行 |
| 本地 README/复盘与验证记录 | docs/ 下人工整理的说明 | 仅摘要结论和经验，不复制原始结果文件 |
| 原 GitHub 配置/Prompt/Schema/测试 | 原位置继续保留 | 标记单件盲测/MCP 路径，不伪装成装配通用协议 |

## 不纳入

- 原始 CAD、重建 CAD、采购件、图纸 PDF/DXF、截图和录屏。
- assembly_original、part_specifications、mate_specifications、evaluation、validation 等运行产生的 JSON，BOM/坐标/测量表与日志。
- 带完整工业尺寸和轮廓的专用案例脚本，例如 gripper_inputs.py、049/046/ModelMania/法兰各次临时重建脚本。经验进入文档，避免把工业几何通过硬编码变相公开。
- 未闭环的插销验收、一次性 API 探针、专用修补/显示/归档脚本；不把它们当成完整产品能力。
- 安装包、interop DLL、外部 MCP 源码、软件许可文件、密钥、缓存和依赖目录。

这些排除不会删除用户本地文件；只是没有复制/提交到公开仓库。原始验证证据可以本地审阅，公开仓库不含足以重建私有工业案例的完整尺寸输入。

## 整理后的验证口径

本次只运行离线测试、源码/PowerShell 解析、安装 DLL 的 C# 编译与公开文件审计，不运行 COM 连接、建模、装配、导图或录屏。整理后的模板路径及入口变化尚未进行新的 CAD 端到端测试；历史实测证明的是整理前算法在当时环境中的表现。
