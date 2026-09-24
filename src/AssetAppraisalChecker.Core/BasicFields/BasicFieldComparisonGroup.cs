using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;

namespace AssetAppraisalChecker.Core.BasicFields
{
    public sealed class BasicFieldComparisonGroup
    {
        public BasicFieldComparisonGroup(
            BasicFieldType fieldType,
            IEnumerable<BasicFieldOccurrence> occurrences)
        {
            if (fieldType == BasicFieldType.Unknown)
            {
                throw new ArgumentException("比较组字段类型不能为 Unknown。", nameof(fieldType));
            }

            FieldType = fieldType;
            Occurrences = new ReadOnlyCollection<BasicFieldOccurrence>(
                (occurrences ?? throw new ArgumentNullException(nameof(occurrences))).ToList());
        }

        public BasicFieldType FieldType { get; }

        public IReadOnlyList<BasicFieldOccurrence> Occurrences { get; }
    }
}
