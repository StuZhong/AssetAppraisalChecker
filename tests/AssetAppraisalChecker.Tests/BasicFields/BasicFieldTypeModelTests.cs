using System;
using System.Linq;
using AssetAppraisalChecker.Core.BasicFields;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Tests.BasicFields
{
    [TestClass]
    public sealed class BasicFieldTypeModelTests
    {
        [TestMethod]
        public void V1BusinessFieldTypes_IncludeReportIssueDate()
        {
            string[] expected =
            {
                nameof(BasicFieldType.ProjectName),
                nameof(BasicFieldType.ReportNumber),
                nameof(BasicFieldType.ValuationDate),
                nameof(BasicFieldType.ReportIssueDate)
            };
            string[] actual = Enum.GetNames(typeof(BasicFieldType))
                .Where(name => !string.Equals(
                    name,
                    nameof(BasicFieldType.Unknown),
                    StringComparison.Ordinal))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray();

            Assert.AreEqual(4, actual.Length);
            CollectionAssert.AreEquivalent(expected, actual);
            foreach (string retiredType in new[]
            {
                "Client",
                "PropertyHolder",
                "ValuationPurpose",
                "ValuationMethod",
                "ValueType",
                "VatRate",
                "AssetCount"
            })
            {
                CollectionAssert.DoesNotContain(actual, retiredType);
            }
        }
    }
}
