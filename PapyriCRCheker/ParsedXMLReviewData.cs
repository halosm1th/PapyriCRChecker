using System.Text.RegularExpressions;
using System.Xml;
using DefaultNamespace;

public class ParsedXMLReviewData
{
    public ParsedXMLReviewData(List<string> reviewTargetPns, string appearsInTargetPn, 
        string authorsSurname, string pageRange, string date,
        XMLDataEntry source)
    {
        AppearsInTargetPN = appearsInTargetPn;
        AuthorsSurname = authorsSurname;
        PageRange = pageRange;
        Date = date;

        ReviewTargetPN = GetDataFromReviewTargets(reviewTargetPns);
        Source = source;
    }
    
    /// <summary>
    /// Extracts the journal name from the <seg subtype="cr"> element's text.
    /// The journal name is expected to be after the author's name and before the year.
    /// Example: "C.R. par Ulrich Wilcken, ArchPF 11 (1933) pp. 132-133." -> "ArchPF"
    /// </summary>
    /// <param name="pnNumber">The path to the XML file.</param>
    /// <returns>The extracted journal name as a string, or null if not found or an error occurs.</returns>
    public static string ExtractJournalNameFromCrSeg(string pnNumber)
    {

        var filePath = pnNumber;
        
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return null;
        }

        XmlDocument xmlDoc = new XmlDocument();
        try
        {
            xmlDoc.Load(filePath);
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <seg> node with subtype="cr"
            XmlNode crSegNode = xmlDoc.SelectSingleNode("//tei:seg[@subtype='cr']", nsmgr);

            if (crSegNode != null)
            {
                string segText = crSegNode.InnerText;
                // Regex to capture the journal name.
                // It looks for a sequence of letters/numbers/dots/hyphens (journal names can vary)
                // that comes after a comma and a space (typically after author),
                // and before a number (issue/year).
                // This pattern is designed to be flexible but might need tuning for edge cases.
                // Example: "Wilcken, ArchPF 11 (1933)" -> captures "ArchPF"
                // Example: "Cipolla, Sileno 38 (2012)" -> captures "Sileno"
                Match match = Regex.Match(segText, @",\s*([A-Za-z0-9\.\-]+)\s+\d+");
                var url = Regex.Match(segText, @"<\shttp://bmcr.brynmwr.edu/\d{4}/\d{4}-\d{2}-\d{1,6}.html\s>");

                if (match.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                } else if (url.Success && match.Groups.Count > 1)
                {
                    return match.Groups[1].Value;
                }
                else
                {
                    Console.WriteLine($"Journal name pattern not found in CR segment: '{segText}'");
                }
            }
            else
            {
                Console.WriteLine("CR segment (<seg subtype=\"cr\">) not found.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{pnNumber}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{pnNumber}': {ex.Message}");
        }
        return null;
    }

    private List<XMLReviewInfo>? GetDataFromReviewTargets(List<string> reviewTargetPns)
    {
        var targets = new List<XMLReviewInfo>();
        foreach (var reviewTarget in reviewTargetPns)
        {
            var reviewer = PapyriCRChecker.ExtractAuthorSurname(reviewTarget);
            var pages = PapyriCRChecker.ExtractPageRanges(reviewTarget);
            var date = PapyriCRChecker.ExtractDate(reviewTarget);
            var location = ExtractJournalNameFromCrSeg(reviewTarget); 
            var review = new XMLReviewInfo(reviewer, location, date, pages, reviewTarget);
            targets.Add(review);
        }
        
        return targets;
        
    }

    public List<XMLReviewInfo> ReviewTargetPN { get; set; }
    public string AppearsInTargetPN { get; set; }
    public string AuthorsSurname { get; set; }
    public string PageRange { get; set; }
    public string Date { get; set; }
    public XMLDataEntry Source { get; set; }

    public override string ToString()
    {
        return $"{AuthorsSurname} {PageRange} {Date} (Reviewed by: {ReviewTargetPN}, in: {AppearsInTargetPN}";
    }
}