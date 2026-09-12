# 架构

二维 PDF/DXF/图片 → GPT-6 Astra 图纸理解 → Codex FeaturePlan → SolidWorks MCP → 运行中的 SolidWorks → ModelState/验证。
generated.SLDPRT 保存并冻结后，才引入 Ground Truth，生成 evaluation.json。

| 组件 | 职责 |
| --- | --- |
| GPT-6 Astra | 多视图对应、尺寸和特征识别、辅助视觉验证 |
| Codex | 唯一交互界面，规划、逐特征调用、状态、恢复与验证 |
| Slacker-LLC/solidworks-mcp | 唯一外部执行 MCP，通过 COM 调用已运行的 SolidWorks |
| SolidWorks | 参数化 SLDPRT 建模、重建、保存和实际几何测量 |
| Ground Truth | 仅建模完成后的评估基准，不是重建输入 |

## 单零件执行

不做装配体和 Mate，不建立新 Agent UI、MCP、数据库或 CAD Runtime。
主要中间结果是 DrawingSpec、FeaturePlan、ModelState 和 EvaluationResult 四种 JSON；工具通信由现有 MCP SDK 与上游服务处理。
FeaturePlan 的可建模类型不等于上游同名工具列表；规划时先查当前 tools/list，必要时拆分成现有操作。
调用响应还需检查 JSON 中 `ok`，然后 rebuild 和量测；协议响应正常不等于几何正确。

## 隔离与状态

Ground Truth 默认 locked。输入目录只放二维文件；模型完成、保存并记录摘要后，才进入 evaluation 并读取 original。
建模阶段模板禁用 open_document；发生中断时由用户打开已保存的 generated 恢复。评估期可在本地解除该工具限制。
这一控制缩小 MCP 操作范围，不拦截其他文件工具。正式盲测将原件移出可访问环境最可靠；本项目不声称实现强制安全沙箱。
已看过原件的会话不能再次声明同案例的独立盲重建。

## 上游核对与风险

[指定依赖](https://github.com/Slacker-LLC/solidworks-mcp) 的安装文案还引用另一 GitHub 所有者；本项目坚持使用 Slacker-LLC 的 checkout，不自动替换来源。
其代码提供主要草图、尺寸、实体特征、量测和截图工具；公开说明的实测参考是 SolidWorks 2026 SP3.2，历史版本是否兼容需目标机器确认。
同一 Session 的 COM 操作必须串行，面/边索引随模型变化而失效，弹窗可能阻塞调用。

根据 [特征实现](https://github.com/Slacker-LLC/solidworks-mcp/blob/main/solidworks_mcp/sw_feature.py)，revolved_cut 可以映射 revolve 的 cut 参数，直孔有 simple_hole；没有独立的 Thread/Hole Wizard、counterbore 或 countersink 工具。沉孔可尝试分解，真实螺纹应列为能力待验证项。
现有检查提供包围盒与质量属性，但没有通用布尔差分/几何重叠工具。属性匹配不能证明形状等价；第一版报告保留未验证几何项。
孔数与孔径可能需要结合圆柱面、图纸和 FeaturePlan 识别，不能把圆柱面个数直接当孔数。

## 验证层次

离线：文件、Schema、来源引用、依赖图、禁止过早解锁 Ground Truth、错误响应处理。
有 SolidWorks 后：COM 附着、MCP 调用、矩形拉伸与保存、真实图纸重建、量测精度和 GUI 录屏。
定量对比前统一单位、配置、坐标与材料；包围盒可能是近似包围值，关键尺寸应使用专门量测和批准的容差。
