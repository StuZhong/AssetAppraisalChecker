using System.Collections.Generic;
using AssetAppraisalChecker.Core.Documents;
using AssetAppraisalChecker.Facts.Extraction;
using AssetAppraisalChecker.Facts.Models;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace AssetAppraisalChecker.Facts.Extraction.Tests
{
    [TestClass]
    public sealed class AssessmentObjectFactExtractorTests
    {
        private const string CompleteObjectText =
            "评估对象为虚构甲公司接收的供配电资产，涉及6家客户，范围为该6家客户对应的供配电资产。";

        [TestMethod]
        public void Extract_ExplicitAssessmentObject_ReturnsFact()
        {
            AssessmentObjectFact fact = ExtractSingle(CompleteObjectText);

            Assert.AreEqual(FactType.AssessmentObject, fact.FactType);
            Assert.AreEqual("供配电资产", fact.AssetType);
            Assert.AreEqual("虚构甲公司", fact.Owner);
            Assert.AreEqual("接收", fact.EconomicAction);
            Assert.AreEqual(6m, fact.Quantity);
            Assert.AreEqual("该6家客户对应的供配电资产", fact.Scope);
        }

        [TestMethod]
        [DataRow("供配电资产")]
        [DataRow("房屋建筑物")]
        [DataRow("机器设备")]
        public void Extract_SupportedAssetType_PreservesAssetType(string assetType)
        {
            string text = string.Concat(
                "评估对象为虚构甲公司转让的",
                assetType,
                "，涉及6家客户，范围为本次列明的全部资产。");

            AssessmentObjectFact fact = ExtractSingle(text);

            Assert.AreEqual(assetType, fact.AssetType);
        }

        [TestMethod]
        [DataRow("6家客户", "6")]
        [DataRow("12台设备", "12")]
        public void Extract_SupportedQuantity_PreservesQuantity(
            string quantityText,
            string expectedQuantity)
        {
            string text = string.Concat(
                "评估对象为虚构甲公司购买的机器设备，涉及",
                quantityText,
                "，范围为本次列明的全部设备。");

            AssessmentObjectFact fact = ExtractSingle(text);

            Assert.AreEqual(decimal.Parse(expectedQuantity), fact.Quantity);
        }

        [TestMethod]
        public void Extract_ExplicitOwner_PreservesOwner()
        {
            AssessmentObjectFact fact = ExtractSingle(CompleteObjectText);

            Assert.AreEqual("虚构甲公司", fact.Owner);
        }

        [TestMethod]
        [DataRow("接收")]
        [DataRow("转让")]
        [DataRow("购买")]
        [DataRow("投资")]
        public void Extract_SupportedEconomicAction_PreservesAction(string action)
        {
            string text = string.Concat(
                "评估对象为虚构甲公司",
                action,
                "的供配电资产，涉及6家客户，范围为本次列明的全部资产。");

            AssessmentObjectFact fact = ExtractSingle(text);

            Assert.AreEqual(action, fact.EconomicAction);
        }

        [TestMethod]
        public void Extract_WithoutExplicitScope_UsesQuantityPhraseAsMinimumScope()
        {
            const string text =
                "评估对象为虚构甲公司接收的供配电资产，涉及6家客户。";

            AssessmentObjectFact fact = ExtractSingle(text);

            Assert.AreEqual("6家客户", fact.Scope);
        }

        [TestMethod]
        public void Extract_TextWithoutCompleteObject_ReturnsEmptyCollection()
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph("本段仅介绍项目背景。", 0));

            IReadOnlyList<AssessmentObjectFact> facts =
                new AssessmentObjectFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_MultipleOwnerCandidates_FailsClosed()
        {
            const string text =
                "评估对象为虚构甲公司与虚构乙公司转让的供配电资产，涉及6家客户，范围为本次列明的全部资产。";
            DocumentReadResult document = CreateDocument(CreateParagraph(text, 0));

            IReadOnlyList<AssessmentObjectFact> facts =
                new AssessmentObjectFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_MultipleAssetTypeCandidates_FailsClosed()
        {
            const string text =
                "评估对象为虚构甲公司投资的供配电资产和机器设备，涉及6家客户，范围为本次列明的全部资产。";
            DocumentReadResult document = CreateDocument(CreateParagraph(text, 0));

            IReadOnlyList<AssessmentObjectFact> facts =
                new AssessmentObjectFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_MultipleQuantityCandidates_FailsClosed()
        {
            const string text =
                "评估对象为虚构甲公司接收的供配电资产，一处记载6家客户，另一处记载7家客户，范围为本次列明的全部资产。";
            DocumentReadResult document = CreateDocument(CreateParagraph(text, 0));

            IReadOnlyList<AssessmentObjectFact> facts =
                new AssessmentObjectFactExtractor().Extract(document, "report-a");

            Assert.IsEmpty(facts);
        }

        [TestMethod]
        public void Extract_AssessmentObject_PreservesCompleteEvidence()
        {
            const string sourceDocument = "report-source-id";
            DocumentElement element = CreateParagraph(CompleteObjectText, 4);
            DocumentReadResult document = CreateDocument(element);

            AssessmentObjectFact fact =
                new AssessmentObjectFactExtractor()
                    .Extract(document, sourceDocument)[0];

            Assert.AreEqual(FactType.AssessmentObject, fact.FactType);
            Assert.AreEqual(sourceDocument, fact.SourceDocument);
            Assert.AreSame(element.Location, fact.SourceLocation);
            Assert.AreEqual(CompleteObjectText, fact.OriginalText);
            Assert.AreEqual("该6家客户对应的供配电资产", fact.Scope);
        }

        [TestMethod]
        public void Extract_AssessmentObject_ReturnsConfidenceInUnitInterval()
        {
            AssessmentObjectFact fact = ExtractSingle(CompleteObjectText);

            Assert.AreNotEqual(FactConfidenceBand.None, fact.Confidence.Band);
            Assert.IsTrue(fact.Confidence.Score >= 0m);
            Assert.IsTrue(fact.Confidence.Score <= 1m);
        }

        private static AssessmentObjectFact ExtractSingle(string text)
        {
            DocumentReadResult document = CreateDocument(
                CreateParagraph(text, 0));

            IReadOnlyList<AssessmentObjectFact> facts =
                new AssessmentObjectFactExtractor().Extract(document, "report-a");

            Assert.HasCount(1, facts);
            return facts[0];
        }

        private static DocumentReadResult CreateDocument(
            params DocumentElement[] elements)
        {
            return DocumentReadResult.Succeeded(
                elements,
                elements.Length,
                0);
        }

        private static DocumentElement CreateParagraph(
            string text,
            int paragraphIndex)
        {
            var location = new DocumentLocation(
                DocumentPart.MainBody,
                paragraphIndex,
                null,
                null,
                null,
                new[] { "评估对象" });

            return new DocumentElement(
                DocumentElementType.Paragraph,
                text,
                location,
                paragraphIndex,
                string.Empty,
                new DocumentNearbyContext(string.Empty, text, string.Empty));
        }
    }
}
