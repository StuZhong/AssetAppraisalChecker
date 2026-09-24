using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public static class DocumentReadStatusMessageProvider
    {
        public static string GetMessage(DocumentReadStatus status)
        {
            switch (status)
            {
                case DocumentReadStatus.Success:
                    return "报告读取成功。";
                case DocumentReadStatus.InvalidPath:
                    return "请选择一个有效的 .docx 报告文件。";
                case DocumentReadStatus.FileNotFound:
                    return "文件不存在，请重新选择报告。";
                case DocumentReadStatus.UnsupportedFileType:
                    return "当前文件不是标准 DOCX，请使用 WPS 另存为 DOCX 后重新校检。";
                case DocumentReadStatus.InvalidDocx:
                    return "文件不是有效的 DOCX 或文件已损坏。原报告未被修改。";
                case DocumentReadStatus.EncryptedOrProtected:
                    return "文件可能受密码或保护限制，当前无法读取。原报告未被修改。";
                case DocumentReadStatus.AccessDenied:
                    return "没有权限读取该文件，请检查文件权限后重试。";
                case DocumentReadStatus.FileInUse:
                    return "文件当前可能被其他程序独占使用，请保存并关闭占用后重试。";
                case DocumentReadStatus.IoError:
                    return "文件读取失败，请检查文件状态后重试。原报告未被修改。";
                default:
                    return "校检过程中发生未预期错误，本次未修改原报告。";
            }
        }
    }
}
