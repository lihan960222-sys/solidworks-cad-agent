# Ground Truth Comparison — 仅最终评估阶段

先验证 ModelState 已是 modeling_complete，rebuild_status=PASS，generated_path、generated_sha256、modeling_completed_at 已记录。
重新计算 generated.SLDPRT 的 SHA-256，必须与冻结值一致。条件未满足时停止，禁止读取 original。
条件满足后进入 evaluation，将 ground_truth_access=allowed；此时才允许用户提供 ground_truth/original.SLDPRT 并开放 open_document。
评估期间不修改或保存原件，不修改冻结的 generated；完成每次切换先确认文档身份。避免 check_errors 默认 rebuild 意外改变原件状态。

比较 original 和 generated 的：Bounding Box X/Y/Z、Volume、Surface Area、Center of Mass、Body Count、Critical Dimensions、Hole Count；条件允许时做 solid difference 或 STEP 几何比较。
先统一零件配置、坐标系、朝向与单位。COM 比较需明确材料/密度；非均匀材料会影响重心，不能把质心差自动判成几何差。
不要求 Feature Tree 一致，允许不同参数化建模策略。图纸未表达的原模型细节作为输入信息不足单独说明。

输出匹配 schemas/evaluation_result.json 的 JSON，保存 evaluation/evaluation.json：
overall_status、bounding_box_error、volume_error_percent、surface_area_error_percent、center_of_mass_error、critical_dimension_results、feature_results、visual_check、notes，以及 Schema 中的冻结摘要、时间和比较范围字段。

- bounding_box_error 是同一坐标框架下 size X/Y/Z 的绝对误差，单位 mm。
- 体积/面积误差百分比 = abs(generated-original)/abs(original)*100；original=0 或未测量时用 null 并登记 missing_checks，不伪造 0%。
- center_of_mass_error 各轴为绝对误差，norm_mm 为欧氏距离；注明坐标对齐方法。
- critical_dimension_results 逐项给 expected（原件）、actual（生成件）、unit、absolute_tolerance、status、reason；feature_results 至少记录 Body Count/Hole Count。
- 缺少必要工具、量测或验收容差时用 NOT_CHECKED 并给原因；上游没有几何重叠工具时不得凭空写比较已完成。
- 全局指标相等不证明几何等价。geometry_check 未完成时 overall_status=NEEDS_FIX，并说明“尺寸/属性可已匹配，完整几何一致性未验证”。
- PASS 要求尺寸检查通过且有实际几何比较证据；FAILED 用于评估无法完成，NEEDS_FIX 用于差异或证据不足。
- 记录截图观察，但不把视觉相似等同于定量几何一致。

模型读取完才计算 original_sha256。评估后若依据原件修模，必须标记该轮受 Ground Truth 辅助，不再称为盲重建。
