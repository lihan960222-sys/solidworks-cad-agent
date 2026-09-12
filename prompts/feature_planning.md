# DrawingSpec → FeaturePlan

读取 AGENTS.md、已校验 DrawingSpec 与 schemas/feature_plan.json。只输出符合 Schema 的 JSON。
document_type 固定 SLDPRT，计划长度统一 mm，角度 deg；明确原图 inch→mm 的转换依据。
未知单位、blocking unresolved 或未批准的几何假设先解决，不用原模型辅助。

每个 Feature：id、type、depends_on、sketch_plane、parameters、source_view、source_dimensions、confidence、verification_targets。
source_view/source_dimensions 引用 DrawingSpec；depends_on 仅引用 Feature ID。ID 唯一，图无环；空依赖不代表可以忽略所用草图/基准。
sketch_plane 用参考 ID 或 front/top/right，非草图任务可 null；选择索引在执行时重新查询，不把旧索引固定进计划。

支持 reference_plane、reference_axis、sketch、boss_extrude、cut_extrude、revolve、revolved_cut、hole、counterbore、countersink、slot、rib、shell、linear_pattern、circular_pattern、mirror、fillet、chamfer、sweep、loft。
不输出 assembly_component、mate 或任何装配任务。

顺序通常为参考几何、主体草图与主体特征、次级凸台、切除/口袋、孔、阵列、镜像、筋、圆角、倒角；实际顺序按依赖决定。
草图应安排 driving dimensions 和几何约束，明确闭合轮廓；参数要包含目标体、操作方向、深度/贯穿条件、种子/阵列轴等。
对每个关键 Feature 给可量测的 verification_targets，最后再给全局目标：数量、整体尺寸、孔径/位置、包围盒与关键尺寸。
target 字段为 id、metric、expected、unit、absolute_tolerance、source_dimensions、required；目标 ID 全局唯一。没给验收容差时记录待确认，不能用显示精度代替。

先查 MCP 实际 tools/list 绑定能力。revolved_cut 可对应 revolve(cut=true)；counterbore/countersink 可拆为已有切除/旋转切除，但必须核验方案能产生正确几何。
当前接口未提供对应 Thread/Hole Wizard 能力时记录缺口，不虚构 Tool，也不静默把真实螺纹当成光孔。
避免大段 VBA/C#/Python API 代码；无现有能力的任务报告阻塞。
