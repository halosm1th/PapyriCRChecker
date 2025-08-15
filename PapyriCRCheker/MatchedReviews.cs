using BPtoPNDataCompiler;
using DefaultNamespace;

public class MatchedReviews
{
    public MatchedReviews(XMLDataEntry reviewFrom, List<ParsedXMLReviewData> pnReviews, List<CRReviewData> crReviews, 
        List<ParsedXMLReviewData> fullPNReviews, List<CRReviewData> fullCRReviews)
    {
        ReviewFrom = reviewFrom;
        PNReviews = pnReviews;
        CRReviews = crReviews;
        FullPNReviews = fullPNReviews;
        FullCRReviews = fullCRReviews;
    }

    public XMLDataEntry ReviewFrom { get; set; }
    public List<ParsedXMLReviewData> PNReviews { get; set; }
    public List<CRReviewData> CRReviews { get; set; }

    public List<ParsedXMLReviewData> FullPNReviews { get; set; }
    public List<CRReviewData> FullCRReviews { get; set; }
    public bool SameNumberOfReviews => PNReviews.Count == CRReviews.Count;
}