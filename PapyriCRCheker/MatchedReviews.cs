using BPtoPNDataCompiler;
using DefaultNamespace;

public class MatchedReviews
{
    public MatchedReviews(XMLDataEntry reviewFrom, List<ParsedXMLReviewData> pnReviews, List<CRReviewData> crReviews)
    {
        ReviewFrom = reviewFrom;
        PNReviews = pnReviews;
        CRReviews = crReviews;
    }

    public XMLDataEntry ReviewFrom { get; set; }
    public List<ParsedXMLReviewData> PNReviews { get; set; }
    public List<CRReviewData> CRReviews { get; set; }

    public bool SameNumberOfReviews => PNReviews.Count == CRReviews.Count;
}