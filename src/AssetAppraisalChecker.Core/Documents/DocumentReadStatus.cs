namespace AssetAppraisalChecker.Core.Documents
{
    public enum DocumentReadStatus
    {
        Success,
        InvalidPath,
        FileNotFound,
        UnsupportedFileType,
        InvalidDocx,
        EncryptedOrProtected,
        AccessDenied,
        FileInUse,
        IoError,
        UnexpectedError
    }
}
