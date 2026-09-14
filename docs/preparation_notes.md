# 准备计划与经验记录

> 历史记录：以下内容对应 2026-09-12 尚未安装 SolidWorks 的准备阶段，不代表当前能力范围。2026-09-14 实测进展见 [项目汇报](PROJECT_REPORT.zh-CN.md)，当前执行路径见 [架构](architecture.md)。保留此文用于溯源，不把旧的“未验证”或“只 MCP”限制误当现状。

## 范围

仅二维工程图重建单个参数化 SLDPRT；不处理装配体，不开发 UI、MCP 或 CAD 平台。
GitHub：`lihan960222-sys/solidworks-cad-agent`，用户选择 **Public**。
未提供 SolidWorks 版本、实际模型或图纸路径，使用模板继续，不访问任何原始三维模型。
原始模型及真实图纸不因仓库公开而获得上传授权；默认全部忽略。

## 执行计划

- [x] 核对 Slacker-LLC/solidworks-mcp 的启动方式、参数单位、返回结构与能力边界。
- [x] 创建五个 Prompt、四个 Schema、Agent 策略、隔离的案例模板与迁移说明。
- [x] 先定义离线错误路径检查，再编写环境诊断与通过外部 MCP 执行的最小烟测。
- [x] 校验 Schema/数据约束、Python 语法、PowerShell 解析、无 SolidWorks 时的运行结果。
- [x] 检查公开文件白名单、敏感内容与本机路径，初始化 main，为初始提交及推送准备就绪。

## 设计决定

Ground Truth 在建模阶段禁止读取；先保存并冻结 generated 的文件摘要，再开启独立评估阶段。
公开 Git 忽略规则、Agent 规则和 MCP 禁用工具分别负责防误提交、行为约束和缩小工具范围；均不冒充操作系统读权限隔离。
正式盲测建议由用户在评估开始前保管原始模型，建模环境中不放原件，也不在 SolidWorks 中打开原件。
MCP 烟测是调用上游工具的客户端，不是第二个 MCP 或另一套 CAD 执行框架。

## 后续 Superpowers 调用经验

调查覆盖真实调用链，实现只保留当前 Demo 所需内容；不为任意图纸通用性添加框架。
上游 README 中的安装地址与用户指定仓库可能不同，必须坚持用户指定来源，并核对实际代码。
MCP 的 JSON-RPC 成功和工具内容中的 `ok` 是不同层，烟测必须检查后者。
只用体积/包围盒相等无法证明完整几何一致；评估需要明确比较范围和未验证项。
之前本机 Python 3.14 在受限沙箱中创建临时目录会出现权限错误；不能盲目归因于项目代码或通过改变系统权限解决。

## 实测结果

2026-09-12，本机仅完成以下验证：

- Python 3.14.2 x64，本项目独立 venv；安装 jsonschema 4.26.0、mcp 1.30.0、pywin32 312。迁移仍建议上游列明支持的 Python 3.12 x64。
- 外部依赖 clone 在主仓库之外：Slacker-LLC/solidworks-mcp，commit `6019ce014c7b64a2982e552e9806e923608cbf1a`，包版本 0.1.0；主仓库未包含其源码。
- 真实 MCP stdio 握手和 tools/list 成功，返回 92 个工具；烟测的工具名称及参数已逐项对实际 inputSchema 校验。
- 对真实服务调用 solidworks_status，确认无 SolidWorks 时返回显式 `ok=false`；没有执行任何模型创建/修改工具。
- 四个 Schema 元校验、离线回归、Python 编译及 PowerShell 解析通过；setup 和环境检查在无 SolidWorks 情况下正常完成并列出缺项。
- smoke_test.py 在本机输出 MISSING SolidWorks running/COM，退出码 1，无未处理异常。真实草图、拉伸、重建、保存和工业件评估仍待验证。
- 独立审查后增加三项回归：最终状态不得漏验收目标；Evaluation PASS 不允许空测量；烟测基于新建调用返回的文档身份核对活动文档。
- Git 忽略规则检查覆盖大小写混合 SLDPRT、图纸、输出、评估 JSON、env、License、密钥与缓存，模板仍可提交。

本机受限沙箱另有 Python 临时目录与 Windows 异步管道权限问题；使用获准的普通用户执行完成依赖安装与 MCP 验证，没有提升到管理员、修改系统权限或修改上游源码。
Git 的系统 TLS 后端遇到凭据句柄错误后，仅在本仓库/本次命令使用 Git 的 OpenSSL 后端，仍验证 TLS 证书；没有关闭证书验证。
