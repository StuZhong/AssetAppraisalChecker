namespace AssetAppraisalChecker.Core.BasicFields
{
    internal static class BasicFieldDisplayNameProvider
    {
        public static string GetDisplayName(BasicFieldType fieldType)
        {
            switch (fieldType)
            {
                case BasicFieldType.ProjectName:
                    return "项目名称";
                case BasicFieldType.ReportNumber:
                    return "报告编号";
                case BasicFieldType.ValuationDate:
                    return "评估基准日";
                case BasicFieldType.ReportIssueDate:
                    return "评估报告日";
                default:
                    return "未知基础字段";
            }
        }
    }
}
