# SolidWorks 单零件 Agent Strategy

## 范围

只重建单个参数化 SLDPRT，允许单文件中的必要多实体；不做 SLDASM、Assembly、Mate、BOM 或其他 CAD。
GPT-6 Astra 负责工程图理解与推理；Codex 是唯一 Agent Harness，负责规划、工具调用、状态和恢复。
只使用 `Slacker-LLC/solidworks-mcp`，不复制修改上游源码，不添加第二个 MCP，不生成不可控的大段宏。
配置来自本地 env/TOML；不写入真实个人路径。工具长度用 mm、角度用 deg；底层 COM 的米/弧度由上游转换。

## Ground Truth：最高优先级的案例规则

建模输入仅允许用户文字要求和当前案例 `input/` 中的 PDF、二维 DXF、PNG/JPG。
不得打开、解析、索引、预览或读取 `ground_truth/`、original.SLDPRT 或其派生三维文件。
不得读取原始 Feature Tree、尺寸、包围盒、体积、面积、质量属性，也不得先转 STEP/STL 再读取。
不得递归读取整个 cases 目录，不得为了“环境检查”打开原件。DXF 中若含三维实体，停止使用该内容；仅二维投影可作输入。
不得在 SolidWorks 中切换到原始模型、截图原件或读取原件的状态。用户已打开原件时，要求先关闭，由用户保管。

正式盲测优先让用户把 Ground Truth 保留在建模环境之外，等评估时再放入案例目录。
`.gitignore` 不限制读取；AGENTS 规则和 MCP disabled_tools 也不是文件系统安全隔离。若要求强制隔离，必须由用户/运行环境在建模期不提供原件访问权限；不得宣称本仓库实现了 OS 沙箱。

进入 Evaluation 的条件：生成的零件建模结束、rebuild 无错误、已保存 output/generated.SLDPRT，记录其 SHA-256 和 modeling_completed_at，冻结结果。
只有满足这些条件后，才可将 ModelState.ground_truth_access 从 locked 改为 allowed，phase 改为 evaluation。
重新检查 generated 的摘要未变，再由用户提供 original；只在此阶段读取两者并对比。
评估后基于原件修模必须标记“使用 Ground Truth 辅助”，不能冒充盲重建。再次盲测应在未接触原件信息的新会话进行。

## Drawing Analysis

收到工程图不立即操作 SolidWorks，先按 drawing_analysis Prompt 生成匹配 Schema 的 DrawingSpec。
识别 front/top/side/section/detail view 及跨视图对应；提取 dimensions、datums、centerlines、symmetry、holes、threads、patterns、radii、fillets、chamfers、ribs、slots、pockets、tolerances、notes。
区分 fact / inference / unresolved。保留标注原文与来源，不把低置信度推断当作事实，不从像素比例臆造尺寸。
尺寸不清、孔是否贯穿、截面对应不明、投影或单位不明都记入 unresolved；影响几何的疑问为 blocking。

## Feature Planning

先 DrawingSpec 后 FeaturePlan；使用 scripts/validate_artifacts.py 做 Schema、来源引用和依赖图检查。
按 Reference Geometry → Base Sketch → Base Feature → Secondary Boss → Pocket/Cut → Hole → Pattern → Mirror → Rib → Fillet → Chamfer → Final Verification 规划，并按实际拓扑依赖执行。
复杂零件拆成多个任务；每个 Feature 有 id、type、depends_on、sketch_plane、parameters、source_view、source_dimensions、confidence、verification_targets。
目标体、草图、轴、支撑面、截面和布尔方向须明确；选择用实时工具返回的对象引用，不能缓存拓扑索引跨越几何修改。
原图若为 inch，保留原始量并在计划中显式乘 25.4 转 mm；角度不乘 25.4。单位未知先解决，不猜。
未确认的几何假设、blocking unresolved、缺少真实工具支持时停止相关执行。可对当前 Demo 特征作定向分解，不追求任意图纸通用性。

## Execution

启动前查实际 tools/list、solidworks_status，确认当前 active document 及文档类型；每次修改前再次确认仍是生成中的零件。
只创建 kind=part。建模期保持 open_document 禁用；恢复时由用户打开已保存的 generated，不读取原件。
一次只处理一个 Feature，优先现有 Tool；不绕过 MCP 直接写大段 COM/VBA/C# 宏。
每个关键 Feature 后 rebuild、检查错误、读特征和尺寸、更新 ModelState，再保存阶段结果。
保存首次用 save_document，后续对已保存的生成文件用 save_active_document，并核对路径；不得覆盖原件。
完成列表与真实 Feature Tree、feature_bindings 交叉检查；已成功 Feature 不重复创建，中断后先核对实际状态再继续。
MCP 报错先查参数、Selection、Reference、依赖。工具响应内容的 `ok=false` 也是失败，即使协议未报错。
失败优先修当前 Feature，不在错误几何上继续，不因单个失败清空全部模型；删除 Feature 前识别其依赖后果。
同一个 SolidWorks Session 只允许一个驱动客户端；运行 smoke_test.py 时暂停 Codex 的 SolidWorks 操作。
超时或弹窗可能意味着操作已部分完成；先人工处理弹窗、重新读状态，不盲目重试。

## Verification

API 成功不代表模型正确。至少核对 rebuild、Feature Tree/存在性、Body Count、Bounding Box、Overall Dimensions、Hole Count/Diameter/Position、Pattern Count、Critical Dimensions。
工具可用时读取 Volume、Surface Area、Center of Mass、Mass Properties，明确单位、材料、配置和坐标系。
草图能拉伸不等于参数化完成：检查 driving dimensions 与约束状态，未约束项要解释；验证可编辑特征，不用导入实体代替建模树。
逐项记录期望、实际、容差、来源；缺少量测不填 0，不报 PASS。
阶段结果为 PASS / NEEDS_FIX / FAILED；问题包含 feature_id、expected、actual、reason、suggested_fix。
Ground Truth 对比以几何和工程尺寸准确性为主，不要求 Feature Tree 相同。
包围盒/体积/面积相近只说明这些指标相近，不证明形状完全相同；无几何比较证据时保留 NOT_CHECKED 和 NEEDS_FIX 总结。

## Public 仓库与交付

公开仓库只保存策略、模板和脚本；SLDPRT、真实图纸、输出、尺寸报告默认忽略。
首次取得工业模型时单独询问是否允许上传，Public 仓库授权不包含模型发布授权。
不上传 Token、API Key、License、缓存、个人路径或上游源码。记录实际验证范围，未在 SolidWorks 执行的步骤不得标记通过。
