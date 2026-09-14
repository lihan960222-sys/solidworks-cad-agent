# SolidWorks CAD Agent

以 **GPT-6 Astra + Codex + SolidWorks 官方 API** 验证二维图纸辅助建模、原生可编辑特征和多零件静态装配的实验项目。

当前定位：**代码仓库与可复用工作流，处于验证原型阶段**，不是自主 CAD 内核、通用图纸识别产品或制造级自动设计系统。本次未发布可安装 Skill；后续建议以 Skill 作为仓库执行器的操作入口。

## 先读这三份文档

- [项目汇报说明](docs/PROJECT_REPORT.zh-CN.md)：能力来源、边界、历史测试、弯路、修正和汇报口径。
- [完整装配复盘](docs/assembly-postmortem.md)：为什么 43 条配合无报错仍装错，以及局部模块为何整体移动。
- [发布形态评估](docs/release-strategy.md)：代码仓库、Skill 与插件的职责和阶段建议。

## 已完成的验证与边界

| 范围 | 历史证据与结论 |
|---|---|
| 049、046 单件 | 原生特征重建、全约束草图、保存重开、与原件布尔几何对比通过；属于源模型辅助流程，不是独立盲测 |
| ModelMania 2024 Phase 1 | 外部图纸生成单实体、6 个草图完全约束、特征无错误；未设置要求的材料，也没有官方原模型的独立几何等价验证 |
| 四组件机械手 | 3 种零件、4 个实例、9 个新配合；几何、位姿、自由度及零干涉检查通过 |
| 完整机械手 | 8 种自制件重建、保留采购模型；26 个叶级实例，43 个新配合。首轮朝向错误；修正一次后保存重开、位姿和干涉基线检查通过 |
| 扭转法兰曲面 | 修正截面连接后获得有效多实体；当前版本体积仍比图纸参考少约 7.92%，曲面不唯一，未达到精确复现 |
| 螺纹插销 | 已形成原生螺旋螺纹探索代码/样例，但最终装配验收记录未闭环，不列为完整通过案例 |

**不能把个案的 43/43 最终有效配合写成“任意装配 100% 成功率”。** API 无错误、几何正确、全参数化、制造可用是不同层次。

## 能力链路

图纸/文字/经授权的装配信息 → Astra 理解与规划 → Codex 编写和调度脚本 → 官方 COM API 或外部 MCP → SolidWorks 几何与配合求解 → 独立验证 → 按证据修正。

最初版本只准备 [Slacker-LLC/solidworks-mcp](https://github.com/Slacker-LLC/solidworks-mcp) 路径；后续大部分实际案例直接使用官方 COM API。MCP 是接口封装，不是推理模型，也不能把 COM 实测成绩归给尚未跑完同等测试的 MCP。两条路径都必须串行控制同一 SolidWorks 会话。

## 仓库内容

| 目录 | 用途 |
|---|---|
| runtime/com/ | 实测 C# 参考执行器、参数化本机路径的入口；默认只编译，不操作 CAD |
| prompts/、AGENTS.md | 输入分析、规划、执行、验证以及盲测/辅助模式规则 |
| schemas/ | 原有单件四种 JSON Schema；不冒充装配 Schema |
| scripts/、tests/ | 环境/MCP 烟测、离线校验与发布内容检查 |
| config/ | 本机配置占位模板，不含实际个人路径 |
| docs/ | 架构、迁移、复盘、经验、发布建议和汇报 |
| cases/ | 私有案例目录模板；不包含真实图纸、模型或测量数据 |

## 使用与验证

新电脑先看 [运行准备](docs/tomorrow_checklist.md) 与 [COM 参考执行器](runtime/com/README.md)。实测本机为 Windows + SolidWorks 2026 SP0.0；其他版本需要独立验证。软件、许可证与 API DLL 由使用方合法安装，本仓库不分发。

```powershell
git clone https://github.com/lihan960222-sys/solidworks-cad-agent.git
cd solidworks-cad-agent
python -m pip install -r requirements.txt
python scripts/validate_artifacts.py
python scripts/test_workspace.py
python -m unittest discover -s tests -v
```

以上检查不运行 SolidWorks。scripts/smoke_test.py 会经 MCP 创建小测试零件，必须单独授权运行，不能与其他 CAD 驱动并发。COM 入口默认只编译；真正建模需显式 -Execute，读取原模型还需 -AllowSourceRead。

当前 COM 参考装配器仍有自制件前缀和固定基准等案例绑定，详见其 README。完整工业尺寸输入、场景专用图纸生成代码未公开；因此不是从克隆到任意工业件一键复现的产品。

## 公开内容与实验诚实性

只提交核心算法、配置模板、策略及人工整理的脱敏经验。**不提交执行产生的 CAD、PDF、截图、视频、日志、BOM/测量 JSON、工业尺寸表，也不提交带完整工业尺寸的案例脚本。** 原本本地结果留在原目录，不随整理删除。整理清单见 [内容取舍](docs/source-inventory.md)。

盲测须隔离原件；曾读取原模型、使用原装配朝向或基于原件修正，都要标为 source-assisted。.gitignore 与提示词不是操作系统沙箱。严谨流程见 [可复用经验](docs/lessons-learned.md)。

仓库尚未设置开源许可证：公开可查看不等于已授予完整开源许可。商业/开源许可选择见发布建议，本次不擅自替用户决定。
