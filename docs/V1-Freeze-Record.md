# AssetAppraisalChecker V1 Freeze Record


## 1. Version Information

Project:

AssetAppraisalChecker


Version:

V1 Frozen


Final Commit:

b56b1e0ba825fb71c8fab8042788e51b38f1b0fa


Commit Message:

Phase V1-UX-Fix: improve desktop issue naming and doc guidance


Freeze Status:

Officially Frozen


---

# 2. Test Baseline

Total Tests:

818


Passed:

818


Failed:

0


Skipped:

0


Regression Status:

Passed


---

# 3. Application Overview

Application Type:

Windows WPF Desktop Application


Operating Mode:

Offline


Document Format:

DOCX only


Office Dependency:

None


Document Engine:

Open XML SDK


Purpose:

Automated consistency checking for asset appraisal reports.


---

# 4. Completed Features


## Money Consistency Checking

Completed:

- Amount extraction
- Chinese financial numeral recognition
- Arabic numeral recognition
- Unit normalization
- Tax basis separation
- Comparison scope classification
- Subject isolation
- Amount conflict detection


## ProjectName Checking

Completed:

- Manual ExpectedProjectName input
- Report-level project name candidate extraction
- Cover/Summary/Body comparison
- Fail Closed ambiguity handling
- Project name mismatch reporting


## ReportNumber Checking

Completed:

- Report number extraction
- Normalization
- Consistency checking


## ValuationDate Checking

Completed:

- Valuation date extraction
- Normalization
- Consistency checking


## ReportIssueDate Checking

Completed:

- Report issue date extraction
- Chinese date recognition
- Consistency checking
- Serious conflict detection


## Desktop Features

Completed:

- WPF interface
- Issue display
- Location aggregation
- Evidence display
- DOC/DOCX guidance
- File error handling


---

# 5. Final Regression Samples


## Standard

Status:

Passed


Money:

24 occurrences / 10 comparison groups / 0 conflicts


ReportNumber:

3 occurrences / 1 distinct / 0 conflicts


ValuationDate:

3 occurrences / 1 distinct / 0 conflicts


ReportIssueDate:

1 occurrence / 1 distinct / 0 conflicts


---

## GeneralizationSample-1

Status:

Passed


Money:

31 occurrences / 2 comparison groups / 23 skipped


ReportNumber:

3 occurrences / 1 distinct / 0 conflicts


ValuationDate:

5 occurrences / 1 distinct / 0 conflicts


ReportIssueDate:

1 occurrence / 1 distinct / 0 conflicts


---

## GeneralizationSample-2

Status:

Passed


ProjectName:

Two different report-level candidates detected.

System remains Fail Closed.


Money:

36 occurrences / 30 comparison groups / 0 conflicts


---

## GeneralizationSample-3

Status:

Passed


ReportIssueDate:

Conflict detected.

Cover and body:

2026-03-18


Summary:

2026-01-30


Result:

1 Serious Issue


---

# 6. Known Limitations


- Only standard DOCX is officially supported.
- Legacy DOC format is not directly parsed.
- Documents are not converted or repaired.
- Encrypted documents are not supported.
- No Word/WPS rendering engine is used.
- Reliable rendered page numbers are not provided.
- ProjectName requires user-provided ExpectedProjectName.
- Unknown templates may Fail Closed.
- No AI semantic comparison.
- No cloud service.
- No external API.
- No WPS plugin.
- No CSV export.


---

# 7. Architecture Principles


V1 follows:

- Offline first
- Deterministic rules
- Fail Closed
- Preserve original documents
- Preserve evidence and provenance
- Do not automatically choose ambiguous business truth


---

# 8. Future Development Direction


V2 planned direction:

AI-assisted semantic cross-document consistency checking.


Target:

Asset Appraisal Report

+

Asset Appraisal Explanation


Requirements:

- Offline operation
- Local AI inference
- Semantic understanding
- Evidence-based error reporting


---

# 9. Freeze Declaration


AssetAppraisalChecker V1 is officially frozen.


Frozen baseline includes:

- Production code
- 818 test cases
- Regression samples
- Known limitations


Final Commit:

b56b1e0ba825fb71c8fab8042788e51b38f1b0fa