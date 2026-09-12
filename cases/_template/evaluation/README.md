# 评估记录模板

当前未执行建模或评估，不创建带虚假零误差的 evaluation.json。
generated.SLDPRT 保存、重建检查完成并冻结 SHA-256 后，才允许读取 original.SLDPRT。
评估时按 `prompts/ground_truth_comparison.md` 生成匹配 `schemas/evaluation_result.json` 的 evaluation.json。
记录坐标对齐、配置、单位、容差、测量来源和缺失项；体积/包围盒一致不等于几何完全一致。
本目录的实际报告默认不上传 Public 仓库，因为它可能泄露工业模型尺寸。
