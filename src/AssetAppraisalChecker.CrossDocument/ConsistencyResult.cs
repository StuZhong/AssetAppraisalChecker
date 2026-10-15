using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using AssetAppraisalChecker.Facts.Models;

namespace AssetAppraisalChecker.CrossDocument
{
    public sealed class ConsistencyResult
    {
        public ConsistencyResult(
            FactType factType,
            ConsistencyStatus status,
            string reason,
            IEnumerable<string> evidence)
            : this(
                ConsistencyResultIdentity.CreateDefault(factType),
                status,
                reason,
                evidence)
        {
        }

        public ConsistencyResult(
            ConsistencyResultIdentity identity,
            ConsistencyStatus status,
            string reason,
            IEnumerable<string> evidence)
        {
            if (identity == null)
            {
                throw new ArgumentNullException(nameof(identity));
            }

            if (!Enum.IsDefined(typeof(ConsistencyStatus), status))
            {
                throw new ArgumentOutOfRangeException(nameof(status));
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                throw new ArgumentException("一致性判断原因不能为空。", nameof(reason));
            }

            if (evidence == null)
            {
                throw new ArgumentNullException(nameof(evidence));
            }

            List<string> evidenceItems = evidence.ToList();
            if (evidenceItems.Count == 0 || evidenceItems.Any(string.IsNullOrWhiteSpace))
            {
                throw new ArgumentException("一致性结果必须包含完整的非空证据。", nameof(evidence));
            }

            Identity = identity;
            Status = status;
            Reason = reason;
            Evidence = new ReadOnlyCollection<string>(evidenceItems);
        }

        public ConsistencyResultIdentity Identity { get; }

        public FactType FactType => Identity.FactType;

        public ConsistencyStatus Status { get; }

        public string Reason { get; }

        public IReadOnlyList<string> Evidence { get; }
    }
}
