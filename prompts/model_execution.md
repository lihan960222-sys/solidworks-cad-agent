# Codex 执行策略

适用范围：原有单件盲测/MCP 路径。source-assisted 装配和 COM 参考执行不使用本单件 Schema 冒充装配协议，遵循 AGENTS.md 更新规则及 runtime/com/README.md。

输入经过校验的 DrawingSpec、FeaturePlan、当前 ModelState。Ground Truth 保持 locked，始终遵循 AGENTS.md。
输出更新后的 ModelState，符合 schemas/model_state.json；运行数据保存在本地 output/ 或 runs/，不提交真实尺寸。

1. 确认 SolidWorks 已运行、无模态对话框，且只有一个客户端在驱动。读取 tools/list 与 solidworks_status。
2. 每次修改前调用 get_active_document_info，核对文档身份和类型为生成中的 Part；不读取原件的内容或属性。
3. 按依赖一次执行一个 Feature Task；先核对 completed_features/feature_bindings 与真实 Feature Tree，避免重复创建。
4. 工具参数以实际 inputSchema 为准，长度 mm、角度 deg；Selection/Reference 通过最新列表解析。
5. 执行后检查协议错误和 JSON 内容 `ok`；关键步骤 rebuild_document、check_errors 并读取相关特征/尺寸。
6. 成功再记 completed_features 与 feature_bindings；失败写 failed_features，先检查参数、选择、引用与依赖，阻止下游特征继续。
7. 几何变化后重新读取 face/edge 索引。超时不代表未执行；确认实际状态后再恢复，不全量重建。
8. 首次 save_document 保存到当前案例 output/generated.SLDPRT；后续确认路径后 save_active_document。保存成功还需确认文件存在和文档不再 dirty。
9. 中断恢复先由用户打开已保存 generated，核对计划版本、当前树与阶段输出，再从未完成任务继续；不开启原件读取工具。
10. 最终 rebuild 无错误、保存完成后，记录 generated_sha256 和 modeling_completed_at，phase=modeling_complete；ground_truth_access 此时仍 locked。

只有进入后续独立 Evaluation 时才将访问状态改为 allowed。重新检查冻结摘要；评估过程中不继续修改 generated。
在建模结束前不得通过文件读取、COM、MCP、截图或任何派生格式获取 Ground Truth 信息。
