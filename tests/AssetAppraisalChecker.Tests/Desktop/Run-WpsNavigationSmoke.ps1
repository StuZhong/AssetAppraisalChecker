param([string]$OutputDirectory)
$ErrorActionPreference = 'Stop'
if ($PSVersionTable.PSEdition -ne 'Desktop') {
    throw '请使用 Windows PowerShell 5.1 / .NET Framework 运行真实 WPS 验收。'
}
if ([string]::IsNullOrWhiteSpace($OutputDirectory)) {
    $OutputDirectory = Join-Path $env:TEMP ('AssetAppraisalChecker-WpsNavigation-' + [Guid]::NewGuid().ToString('N'))
}
$testBin = [IO.Path]::GetFullPath((Join-Path $PSScriptRoot '..\bin\Release\net48'))
if (-not (Test-Path (Join-Path $testBin 'AssetAppraisalChecker.Tests.dll'))) {
    throw '请先构建 Tests 项目的 Release 版本。'
}
Set-Location -LiteralPath $testBin
# All assemblies come from the existing build; no Office package, download or registry change.
foreach ($name in @('AssetAppraisalChecker.Core.dll', 'AssetAppraisalChecker.Document.dll',
    'AssetAppraisalChecker.Facts.dll', 'AssetAppraisalChecker.Semantic.dll',
    'AssetAppraisalChecker.Facts.Extraction.dll', 'AssetAppraisalChecker.CrossDocument.dll',
    'AssetAppraisalChecker.Application.dll', 'AssetAppraisalChecker.Mechanical.dll',
    'AssetAppraisalChecker.TextQuality.dll', 'AssetAppraisalChecker.Desktop.exe',
    'AssetAppraisalChecker.Tests.dll')) {
    [void][Reflection.Assembly]::LoadFrom((Join-Path $testBin $name))
}
Write-Output ('Synthetic evidence directory: ' + $OutputDirectory)
[AssetAppraisalChecker.Tests.Desktop.WpsNavigationSmoke]::Run($OutputDirectory)
