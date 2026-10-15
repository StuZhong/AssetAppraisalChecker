# AssetAppraisalChecker

**资产评估报告一致性校检工具**

**An asset appraisal report consistency checker, built with C#, WPF and Open XML SDK.**

---

## 项目简介 / Overview

AssetAppraisalChecker 是一个面向资产评估业务的 Windows 桌面校检工具，主要用于识别、提取、标准化并比较资产评估报告中的关键业务信息，辅助发现人工编制、复制、修改过程中产生的前后不一致问题。

项目采用独立桌面程序架构，支持对 `.docx` 文件进行只读分析，不依赖 Microsoft Word 或 Microsoft 365。

**当前版本：V2.0（已完成本地发布前验证）**

V2 在 V1 的基础上进一步扩展了跨文档一致性检查、多 Checker 业务校检架构，以及受控的本地 AI 语义辅助能力。

---

## V1 主要能力 / Features

V1 主要针对**单个 Word 资产评估报告内部的一致性**进行校检。

主要能力包括：

- 项目名称等关键文字信息的一致性检查
- 报告编号等标识信息的一致性检查
- 评估基准日、报告日期等日期信息的识别与标准化
- 评估值等金额信息的识别、换算与比较
- 中文金额与阿拉伯数字金额处理
- 正文与表格中的相关业务信息提取
- 同一业务含义在不同位置出现时的一致性判断
- 对异常和冲突生成诊断结果
- 对原始 `.docx` 报告进行只读处理，不修改原文件

**V1 冻结测试基线：818 项回归测试。**

---

## V2 新增能力 / What's New in V2

V2 在 V1 基础上，重点扩展业务校检深度与辅助识别能力。

### 1. 跨文档一致性校检

通过提取资产评估报告中的 Business Facts（业务事实），支持多个 Checker 对相关业务字段进行检查。

在已有业务规则和证据约束下，比较不同文档中的业务信息，辅助发现内容不一致、业务字段冲突等问题。

### 2. 多 Checker 架构

将不同类型的业务校检逻辑组织为相对独立的检查模块，便于扩展、测试与维护。

### 3. 本地 AI 语义辅助识别

V2 引入受控的本地语义候选排序能力，用于辅助处理部分非标准业务描述。

相关技术包括：

- BGE Embedding 候选检索
- BGE Reranker 候选重排序
- Candidate Contract 候选约束
- Acceptance / Safety Gate 安全门控

本地语义辅助能力目前具有明确的适用范围，并非对所有业务字段都启用。

### 4. Fail Closed 安全原则

V2 坚持确定性业务规则优先：

- AI 不直接生成权威业务事实
- AI 不直接决定 PASS / WARNING / FAIL
- 模型不能覆盖明确的业务否定或冲突证据
- 证据不充分、候选不明确时不强行判定
- 模型缺失或初始化失败时不制造虚假的业务结论

### 5. 离线处理与文档保护

- 本地执行文档分析
- 不自动上传资产评估报告
- 不自动下载 AI 模型
- 保持原始 DOCX 文件只读
- 本地模型资产通过独立配置提供

**注意：仓库源码不包含本地模型权重。部分 AI 能力需要另行配置兼容的模型资产。**

---

## 基本工作流程 / How It Works

1. 选择待校检的 Word 资产评估报告。
2. 解析 DOCX 文档结构。
3. 识别并提取候选业务字段。
4. 对业务字段进行分类、标准化和主体识别。
5. 使用确定性业务规则进行校检。
6. 在受支持的场景下，使用本地语义模型辅助候选排序。
7. 根据业务证据和安全约束进行一致性判断。
8. 输出问题与诊断结果，供人工复核。

原始报告保持不变。

---

## 技术栈 / Technology Stack

- C#
- .NET Framework 4.8
- WPF
- Open XML SDK
- Visual Studio 2022
- ONNX Runtime（本地语义辅助相关组件）

核心校检逻辑与桌面界面分层实现。

---

## 测试与验证 / Testing

根据 V2 冻结时的 Release Readiness 记录：

| 验证项目 | 结果 |
|---|---|
| Release Build | 0 Warning / 0 Error |
| 自动化测试 | 1216 Passed / 0 Failed / 0 Skipped |
| Golden Runner | 31/31 Passed |
| V1 回归基线 | 818 项，冻结逻辑保持不变 |

仓库包含用于验证识别与校检行为的合成测试用例。

以上结果为 V2 原始冻结版本的验收记录；公开版整理后的测试结果需另行验证。

详细记录见：

`docs/V2-Release-Readiness.md`

---

## 版本里程碑 / Milestones

| 版本 | 主要目标 | 状态 |
|---|---|---|
| V1 | 单个 Word 报告内部一致性校检 | 已完成并冻结 |
| V2 | 跨文档业务校检与本地语义辅助 | 已完成本地验证 |
| V3 | Word–Excel 交叉一致性校检 | 后续版本 |

V1 历史版本：

[milestone-v1](https://github.com/StuZhong/AssetAppraisalChecker/releases/tag/milestone-v1)

V1 冻结记录：

`docs/V1-Freeze-Record.md`

---

## 数据保护 / Data Protection

资产评估报告可能包含客户及业务敏感信息。

本项目遵循本地处理、原文件只读和审慎发布的原则。

公开仓库不应包含真实客户报告、未经授权的内部业务资料、访问密钥或本地私有模型资产。

---

## 版权与许可 / License

**本仓库目前未采用开源许可证（No License）。**

仓库公开不等于授予任意复制、修改、重新发布或商业使用的许可。

除 GitHub 平台条款允许的行为及适用法律规定的例外外，其他使用需要取得相关权利人的授权。

**All rights reserved, except as otherwise permitted by applicable law or GitHub's Terms of Service.**