# SolidWorks 2D → 3D CAD Agent

**GPT-6 Astra + Codex + SolidWorks**：从二维工业工程图重建单个参数化 SLDPRT，完成后再与原始模型进行定量对比。
Codex 是唯一 Agent 交互与编排界面；不做装配体、不搭 CAD 平台、不自研 MCP。

## 组成与内容

| 组件/目录 | 用途 |
| --- | --- |
| GPT-6 Astra + Codex | 图纸理解、逐特征规划执行、恢复与验证 |
| [Slacker-LLC/solidworks-mcp](https://github.com/Slacker-LLC/solidworks-mcp) | 唯一外部 MCP，操作已经运行的 SolidWorks |
| AGENTS.md、prompts/ | Ground Truth 隔离规则及五个工作阶段 Prompt |
| schemas/ | DrawingSpec、FeaturePlan、ModelState、EvaluationResult |
| cases/ | input / ground_truth / output / evaluation 案例模板，无真实模型 |
| config/ | 可迁移环境和 Codex MCP 模板 |
| scripts/ | setup、环境检查、MCP 烟测、JSON 校验与离线回归检查 |
| docs/ | 架构、明日步骤、准备记录 |

仓库不包含 SolidWorks、License、外部 MCP 源码、工业模型、真实图纸、密钥或运行缓存。
这是可迁移 Demo 准备项目；自动重建与完整几何比较必须在目标机器验证。

## Ground Truth 与 Public 仓库

建模只读 PDF/DXF/图片和用户文字；不得读取 original.SLDPRT、其特征树、尺寸或派生三维文件。
generated.SLDPRT 保存完成并冻结摘要后，才进入评估。建议原件由用户在建模环境之外保管，评估时再提供。
`.gitignore` 不阻止文件读取；Agent 规则也不是 OS 沙箱。模板禁用建模期的 open_document，恢复生成件由用户手动打开。
本仓库为 **Public**；原模型、真实图纸、生成件、尺寸报告默认忽略。公开仓库不等于授权公开工业资料。

## 新电脑快速开始

准备 Git、Python 3.12 x64 和已授权 SolidWorks；具体 SolidWorks 版本暂未确定。
虚拟环境把项目依赖放在独立目录，不影响系统 Python；迁移时重建，不直接复制。

```powershell
git clone https://github.com/lihan960222-sys/solidworks-cad-agent.git
cd solidworks-cad-agent
Copy-Item config/solidworks.example.env config/solidworks.local.env
# 填写本机路径；按 docs/tomorrow_checklist.md 安装仓库外的 MCP。
.\scripts\setup.ps1 -PythonExecutable '<PYTHON_312_EXE>'
.\.venv\Scripts\python.exe scripts/check_environment.py
.\.venv\Scripts\python.exe scripts/validate_artifacts.py
.\.venv\Scripts\python.exe scripts/test_workspace.py
```

将 config/codex-mcp.example.toml 合并进本地 Codex 配置，替换 MCP 可执行文件和案例输出路径，重启客户端。
Codex 不自动读取 solidworks.local.env。环境文件仅支持字面 KEY=VALUE；相对路径以仓库根目录为基准，非空进程变量优先。
setup 默认创建 `.venv` 并安装 requirements；`-CheckOnly` 只检查，`-SkipVenv` 使用指定现有环境。外部 MCP 独立安装。
环境检查没有 SolidWorks 也正常退出 0 并打印 MISSING；自动化需要缺项返回 1 时使用 `--strict`。

启动 SolidWorks、关闭所有文档、暂停 Codex 对它的操作后：

```powershell
.\.venv\Scripts\python.exe scripts/smoke_test.py
```

烟测通过外部 MCP 新建 20×10×5 mm 小零件，独立保存且不覆盖已有文件；不是工业零件或全约束草图验收。
正式案例按 [案例说明](cases/README.md) 和 [明日清单](docs/tomorrow_checklist.md) 执行。
中间 JSON 可用 validate_artifacts.py 的 `--drawing`、`--plan`、`--state`、`--evaluation` 参数校验。

## 依赖与成功率边界

固定使用用户指定的 [Slacker-LLC 仓库](https://github.com/Slacker-LLC/solidworks-mcp)，即使其 README 安装命令出现其他所有者。
MCP SDK 限定 `<2`，与该服务当前依赖一致。外部 checkout 和验证版本见 [准备记录](docs/preparation_notes.md)。
上游实测参考 SolidWorks 2026 SP3.2；旧版本、真实螺纹、复杂孔和最终工业件需实测。只有一个客户端可驱动 Session，弹窗可能阻塞，几何变更后应重查索引。
当前没有通用几何重叠/布尔差分工具；体积、包围盒和关键尺寸匹配不能证明完整形状等价，评估会明确保留未验证项。
官方配置说明：[Codex MCP](https://learn.chatgpt.com/docs/extend/mcp?surface=cli)；在 Codex 会话选择可用的 [GPT-6 Astra](https://developers.openai.com/api/docs/models/gpt-6-astra)。
