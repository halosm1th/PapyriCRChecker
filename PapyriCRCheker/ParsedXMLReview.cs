using System.Text;
using System.Xml;

public class ParsedXMLReview
{
    public ParsedXMLReview(XmlDocument sourceDocument, string documentPath, string bpNumber,
        string authorFirstName, string authorLastName,string reviewDate, string pageRange,
        string appearsIn, List<string> relatedItemReviews)
    {
        SourceDocument = sourceDocument;
        ReviewPath = documentPath;
        BPNumber = bpNumber;

        DocumentAppearsInIDNumber = appearsIn;
        ReviewPageRange = pageRange;
        
        AuthorFirstName = authorFirstName;
        AuthorLastName = authorLastName;
        ReviewDate = reviewDate;
        
        RelatedItemReviewPtrs = relatedItemReviews;
    }
    
    public XmlDocument SourceDocument { get; }
    public string DocumentAppearsInIDNumber { get; set; }
    
    public string ReviewPath { get; set; }
    
    public string BPNumber { get; set; }
    public string AuthorFirstName { get; set; }
    public string? AuthorLastName { get; set; }
    public string ReviewDate { get; set; }
    public string? ReviewPageRange { get; set; }
    public List<string> RelatedItemReviewPtrs { get; set; }

    public string ToCRUpdateString()
    {
        var sb = new StringBuilder();

       sb.Append($"Written by: {AuthorFirstName} {AuthorLastName} on {ReviewDate}, pp. {ReviewPageRange}, appears in {DocumentAppearsInIDNumber}.\n");
        return sb.ToString();
    }
    
    public override string ToString()
    {
        var sb = new StringBuilder();

        sb.Append($"#{BPNumber} @ {ReviewPath}. Appears in {DocumentAppearsInIDNumber} on pages {ReviewPageRange}. ");
        sb.Append($"Written by: {AuthorFirstName} {AuthorLastName} on {ReviewDate}. ");
        var reviewText = (RelatedItemReviewPtrs != null && RelatedItemReviewPtrs.Count > 0) ?
            RelatedItemReviewPtrs.Aggregate("", (h, t) => h + t+ " ")
            : "None";
        reviewText = reviewText.Trim();
        sb.Append($"Related-Item, reviews: [{reviewText}]");
        
        return sb.ToString();
    }
}