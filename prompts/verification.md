# 重建过程验证

输入 DrawingSpec、FeaturePlan、当前 SolidWorks ModelState 和本次工具测量证据。
禁止读取 Ground Truth。输出符合 schemas/model_state.json 中 $defs.verification 的对象，写入 verification_results。
status 为 PASS / NEEDS_FIX / FAILED；checks 每项含 target_id、status、expected、actual、unit、evidence、reason。
未测到的目标用 NOT_CHECKED，actual=null；不要填零或据此宣布通过。

检查 rebuild status、feature existence/Feature Tree、body count、bounding box、overall dimensions、hole count/diameter/position、pattern count、critical dimensions。
同时检查草图约束和 driving dimensions；工具能支持时增加体积、面积、重心、质量属性，说明材料与配置。
依据图纸/批准的验收容差比较；计数精确一致。统一单位和坐标框架，不能把系统米当 mm。

PASS：该阶段所有适用目标均有实际证据且通过，没有阶段未完成项或阻塞疑问。
NEEDS_FIX：尺寸/数量/特征不符或证据不足；problems 每项必须包含 feature_id、expected、actual、reason、suggested_fix。
FAILED：文档无法访问、无法恢复的 rebuild/几何错误或不能建立可信检查。
API ok=true 只证明调用层成功，不代表整体建模正确。视觉截图补充几何检测，不替代关键尺寸读取。
最终检查要覆盖整个 FeaturePlan；阶段 PASS 不等于最终 PASS。
