# AssetAppraisalChecker V2.0 Release Readiness

## 发布状态

V2.0 已完成发布前验证。Release Build 为 0 Warning / 0 Error；自动测试为 1216 Passed / 0 Failed / 0 Skipped；统一 Golden Runner 为 31/31 Passed。V1 冻结逻辑和 818 项回归保持不变。

## 核心能力与 AI 架构

V2.0 支持 DOCX 只读读取、Business Fact 提取、多 Checker 跨文档一致性检查，以及 EvaluationMethod 非标准描述的本地语义 ranking evidence。生产语义链为：

```text
规则筛选合法候选
→ BGE embedding Top2 retrieval
→ bge-reranker-v2-m3 reranking
→ Candidate Contract
→ Acceptance / Safety Gate
→ CrossDocument 既有业务裁决
```

模型仅排序合法候选并提供证据，不判断候选有效性，不直接生成 BusinessFact，也不直接决定 PASS / WARNING / FAIL。`FactPromotionAllowed` 恒为 `false`。

## 本地模型资产

`LocalModelRoot` 由部署组合入口传入，不硬编码机器路径。运行时要求以下冻结资产及其 manifest 中列出的 external data：

- BGE：`BAAI/bge-small-zh-v1.5`，revision `7999e1d3359715c523056ef9478215996d62a620`，FP32 ONNX 与 dedicated Bert tokenizer preprocessing ONNX。
- Reranker：`BAAI/bge-reranker-v2-m3`，revision `953dc6f6f85a1b2dbfca4c34a2796e7dde08d41e`，FP32 ONNX、external-data manifest、冻结 tokenizer.json 与 pair-tokenizer wrapper。
- Runtime：Microsoft.ML.OnnxRuntime 1.30.0、Microsoft.ML.OnnxRuntime.Extensions 0.14.0，CPU/x64。

启动或首次使用前验证路径边界、文件存在性、大小、SHA-256、revision 和 reranker external-data manifest。模型二进制不进入 Git。

## 离线与配置要求

- 正式运行完全离线，不自动下载、不访问 Hub、不使用 remote/fake fallback。
- 模型缺失、Hash 不符、ORT/tokenizer/native 初始化失败或输出异常均 Fail Closed，不制造业务 FAIL。
- 安全默认组合入口为 `SemanticCandidateProcessingOptions.Shadow`；正式 Active 部署必须显式选择 `SemanticCandidateProcessingOptions.Active`。
- Shadow 保留诊断/回退能力；Active 将通过 Gate 的 ranking evidence 加入结果证据，但仍不改变硬规则裁决。

## 安全保证

- NoValidCandidate 必须 abstain；Ambiguous 必须 abstain/warning。
- 明确否定、Subject 冲突和硬事实冲突不可被模型覆盖。
- reranker raw logit 与 ModelConfidence、业务有效性严格分离，不设置新的绝对分数生产阈值。
- DOCX 始终只读；真实脱敏 E2E 验证前后 SHA-256 一致。

## 已知限制

- Active local ranking 当前只用于 EvaluationMethod 的受控 CandidateUnknown 路径；其他 Fact 仍使用既有确定性或兼容语义链。
- 模型排名只能提供候选优先级，不能证明 Top1 有效；不支持 Candidate 自动晋升 BusinessFact。
- 本地模型资产需要由部署方预置；程序不负责下载、更新或转换模型。
- 当前生产路径固定为 Windows x64、.NET Framework 4.8、CPU FP32；不包含 INT8、GPU、DirectML、E5 或其他模型。
- Desktop 尚未提供 LocalModelRoot 与 Shadow/Active 的设置界面；当前由应用组合入口显式配置。
