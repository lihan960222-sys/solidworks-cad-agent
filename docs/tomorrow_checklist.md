# 明日清单

1. 安装合法 SolidWorks。版本待定；上游当前实测参考 2026 SP3.2，不能据此保证旧版本。
2. 确认 License、正常启动、默认 Part 模板与保存权限。
3. Clone 本仓库，准备 Git、Python 3.12 x64；不复制旧机器的 venv。
4. Clone/安装指定 `Slacker-LLC/solidworks-mcp` 到仓库外，记录 commit，使用独立 MCP venv。
5. 复制并填写 config/solidworks.local.env，设置工具、CASES_PATH、案例输出目录和 SMOKE_OUTPUT_ROOT。
6. 合并唯一 MCP 的 TOML 模板到 Codex 本地配置，替换路径；重建期保持 open_document 禁用。
7. 运行 `.\scripts\setup.ps1 -PythonExecutable '<PYTHON_312_EXE>'`。
8. 运行 `.\.venv\Scripts\python.exe scripts/check_environment.py`，处理缺项。
9. 启动 SolidWorks，关闭所有文档和弹窗，暂停其他客户端的建模操作。
10. 运行 `.\.venv\Scripts\python.exe scripts/smoke_test.py`；它新建独立测试零件、草图和拉伸，rebuild、校验包围盒并保存，不访问原件。
11. 关闭烟测零件，在 Codex 中测试文本建模、尺寸约束和实际量测；重新连接单一 MCP 客户端。
12. 用户准备一个正式 SLDPRT；勿在盲建模会话读取原件。
13. 用户从原模型制作有充分尺寸、剖视和孔注释的工程图，导出 PDF。
14. 可选导出二维 DXF，核对单位/比例和视图；不是把三维模型改存为 DXF。
15. 预留 ground_truth/original.SLDPRT；正式盲测建议由用户在环境外保管，待步骤 17 再提供。
16. 只用 input 中的图纸重建单个参数化 SLDPRT，完成检查、保存与摘要冻结。
17. 此时才提供原件、进入 Ground Truth Comparison；统一配置/坐标/容差，对缺少几何比较工具的项目如实标记。
18. 在实际通过的范围内录屏：左 SolidWorks，右 Codex；展示尺寸对比与未验证项，勿将后验修模称为盲重建。

## 外部 MCP 安装（替换占位符）

```powershell
git clone https://github.com/Slacker-LLC/solidworks-mcp '<EXTERNAL_MCP_DIR>'
& '<PYTHON_312_EXE>' -m venv '<MCP_VENV>'
& '<MCP_VENV>/Scripts/python.exe' -m pip install '<EXTERNAL_MCP_DIR>'
git -C '<EXTERNAL_MCP_DIR>' rev-parse HEAD
```

SOLIDWORKS_MCP_PATH 指向外部 checkout；SOLIDWORKS_MCP_EXECUTABLE 指向 MCP venv 内的 solidworks-mcp.exe。
PYTHON_EXECUTABLE 用于本项目辅助脚本；setup 的显式参数优先。两个环境可以独立更新。
SW_MCP_OUTPUT_ROOT 指向当前案例 output；SMOKE_OUTPUT_ROOT 指向独立测试目录。Codex 不自动读取项目 env，需要在 TOML 再设置输出根目录。
没有默认 Part 模板时，设置 SW_MCP_TEMPLATE_DIR，确认其中有可用 .prtdot。

若脚本策略阻止运行，只在当前 PowerShell 进程设置 `Set-ExecutionPolicy -Scope Process Bypass`；无需管理员，也不修改系统级策略。
没有 SolidWorks 时环境检查仍会正常完成；“检查脚本运行成功”不表示 CAD 环境就绪。
