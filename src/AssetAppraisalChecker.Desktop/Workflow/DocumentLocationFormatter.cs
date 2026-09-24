using System;
using System.Globalization;
using System.Linq;
using AssetAppraisalChecker.Core.Documents;

namespace AssetAppraisalChecker.Desktop.Workflow
{
    public static class DocumentLocationFormatter
    {
        public static string Format(DocumentLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            string heading = location.HeadingPath.Count == 0
                ? string.Empty
                : string.Join(" > ", location.HeadingPath);
            string structuralLocation;

            if (location.TableIndex.HasValue)
            {
                structuralLocation = string.Format(
                    CultureInfo.InvariantCulture,
                    "表格 {0} / 第 {1} 行 / 第 {2} 列",
                    location.TableIndex.Value + 1,
                    (location.RowIndex ?? 0) + 1,
                    (location.CellIndex ?? 0) + 1);
            }
            else if (location.ParagraphIndex.HasValue)
            {
                structuralLocation = string.Format(
                    CultureInfo.InvariantCulture,
                    "正文段落 {0}",
                    location.ParagraphIndex.Value + 1);
            }
            else
            {
                structuralLocation = "文档正文";
            }

            return string.IsNullOrEmpty(heading)
                ? structuralLocation
                : heading + " > " + structuralLocation;
        }

        public static string FormatArea(DocumentLocation location)
        {
            if (location == null)
            {
                throw new ArgumentNullException(nameof(location));
            }

            if (location.HeadingPath.Count > 0)
            {
                return string.Join(" > ", location.HeadingPath);
            }

            return location.TableIndex.HasValue ? "表格" : "正文";
        }
    }
}
