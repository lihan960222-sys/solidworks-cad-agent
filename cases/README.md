# 单零件案例

此目录仅为原有单件盲测模板；已完成实验的公开摘要在 docs/PROJECT_REPORT.zh-CN.md。完整机械手的私有输入契约见 runtime/com/README.md，未上传真实数据。

当前没有真实图纸或 SLDPRT，只有 `_template` 目录。不创建伪造 CAD 文件或虚假评估结果。
复制 `_template` 成新的案例目录，再由用户提供有权使用的资料。

```text
cases/<case_name>/
  input/         drawing.pdf、drawing.dxf、可选图片（建模唯一文件输入）
  ground_truth/  original.SLDPRT（建模阶段不可读；建议评估时才放入）
  output/        generated.SLDPRT、drawing_spec.json、feature_plan.json、model_state.json
  evaluation/    evaluation.json、README.md
```

Public 仓库不包含工业模型上传授权。输入图纸、模型、输出、评估 JSON 默认均被忽略。
如果以后要公开案例文件，必须先确认具体文件授权，再有选择地调整忽略规则；不要 `git add -f` 批量提交。
每例可添加不含机密尺寸的 `manifest.json`，仅记录文件名、案例标识和资料授权情况；建模前不得从原件提取元数据。

## 准备与盲测

1. 由用户或独立准备流程从原 SLDPRT 制作标注完整的工程图，优先 PDF，DXF 可选；包含剖视、孔表与公差。
2. 正式建模会话不接触原模型或其截图/元数据；原件不打开在 SolidWorks 中。input 只放二维资料。
3. 设置 CASES_PATH 和当前案例的 SW_MCP_OUTPUT_ROOT（只指向该例 output），按 AGENTS 分析、规划和建模。
4. 保存完成的 generated 并记录 SHA-256、完成时间；只有此后才把原件提供给 Evaluation。
5. 评估时比较量测与几何；需要重建修正时标记是否已经使用 Ground Truth 信息。

`.gitignore` 只控制 Git，不控制文件读权限。要求强隔离时，用户应在建模期把原件留在 Agent 不可访问的地方。

## Codex 启动语句

> 阅读 AGENTS.md。仅使用我指定案例 input/ 中的二维图纸，按 drawing_analysis 与 feature_planning Prompt 生成并校验 DrawingSpec 和 FeaturePlan。禁止访问 ground_truth 或任何三维原件。解决阻塞疑问后，按 model_execution 一次执行一个特征，持续验证，完成并冻结 generated 后再进入独立评估阶段。
