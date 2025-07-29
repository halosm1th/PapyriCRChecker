using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using DefaultNamespace;

public class ParsedXMLReviewData
{
    
    private static readonly XNamespace teiNs = "http://www.tei-c.org/ns/1.0";
    
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
    
    private static XDocument LoadXmlDocument(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return null;
        }
        try
        {
            // XDocument.Load automatically handles namespaces when querying with XName (namespace + local name)
            return XDocument.Load(filePath);
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
            return null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while loading '{filePath}': {ex.Message}");
            return null;
        }
    }
    
    /// <summary>
    /// Extracts the journal name from the <seg subtype="cr"> element's text.
    /// The journal name is expected to be after the author's name and before the year.
    /// Example: "C.R. par Ulrich Wilcken, ArchPF 11 (1933) pp. 132-133." -> "ArchPF"
    /// </summary>
    /// <param name="pnNumber">The path to the XML file.</param>
    /// <returns>The extracted journal name as a string, or null if not found or an error occurs.</returns>
     public static string ExtractJournalNameFromCrSeg(string filePath)
    {
        XDocument xmlDoc = LoadXmlDocument(filePath);
        if (xmlDoc == null)
        {
            return null;
        }

        // Find the text content of the <seg subtype="cr"> element
        string segText = (string)xmlDoc.Descendants(teiNs + "seg")
                                       .Where(s => (string)s.Attribute("subtype") == "cr")
                                       .FirstOrDefault();

        if (!string.IsNullOrEmpty(segText))
        {
            // Step 1: Remove "C.R. par [Author Name]," prefix
            // This regex captures everything from the start up to the comma after the author's name.
            string cleanedSegText = Regex.Replace(segText, @"^C\.R\.\s*par\s*[^,]+,\s*", "", RegexOptions.IgnoreCase).Trim();
            cleanedSegText = cleanedSegText.Split("(")[0].Trim();

            // Step 2: Handle " = JournalName" pattern (e.g., "Raccolta ... = Aegyptus")
            string journalPart = cleanedSegText;
            int equalsIndex = journalPart.IndexOf(" = ");
            if (journalPart.Contains(" = "))
            {
                // If " = " is found, the actual journal name starts after it.
                journalPart = journalPart.Split(" = ")[1];
            }

            // Step 3: Find the end of the journal name using a regex that matches the start of the following pattern
            // This regex will find the first occurrence of a delimiter.
            // (?: # Non-capturing group for alternatives
            //   \s+\d+ # Space and digits (e.g., " 57")
            //   | \s*<[^>]+> # Space and URL (e.g., " <http://...>")
            //   | ,\s*\d+ # Comma, space, digits (e.g., ", 57")
            //   | \s*\[[^\]]+\] # Space and bracketed text (e.g., " [Lisboa]")
            //   | \s*p\. # Space and "p." (e.g., " p. 147")
            //   | \s*\d+\s*\( # Space, digits, space, opening parenthesis (e.g., " 6 (")
            //   | ,\s*[IVXLCDM]+\s*\( # Comma, space, Roman numerals, space, opening parenthesis (e.g., " N.S., II (")
            //   | ,\s*[IVXLCDM]+ # Comma, space, Roman numerals (e.g., " N.S., II")
            // )
            string patternEndJournal = @"(?:\s+\d+|\s*<[^>]+>|,\s*\d+|\s*\[[^\]]+\]|\s*p\.|\s*\d+\s*\(|,\s*[IVXLCDM]+\s*\(|,\s*[IVXLCDM]+)";
            Match match = Regex.Match(journalPart, patternEndJournal);

            if (match.Success)
            {
                // The journal name is the substring from the beginning of journalPart up to the start of the match.
                string journalName = journalPart.Substring(0, match.Index).TrimEnd(' ', ',');
                if (!string.IsNullOrEmpty(journalName))
                {
                    return journalName;
                }
            }

            // Fallback: If no specific pattern is found, try to capture everything until the first number or URL-like structure
            // This is for very unusual patterns where the journal name might just end abruptly before a number or URL.
            Match fallbackMatch = Regex.Match(journalPart, @"^([\p{L}\p{N}\s\.\-:]+?)(?=\s*\d+|\s*<|$)");
            if (fallbackMatch.Success && fallbackMatch.Groups.Count > 1)
            {
                return fallbackMatch.Groups[1].Value.TrimEnd(' ', ',');
            }

            Console.WriteLine($"Journal name pattern not found in CR segment after processing: '{segText}' (processed to: '{journalPart}')");
        }
        else
        {
            Console.WriteLine("CR segment (<seg subtype=\"cr\">) not found.");
        }
        return null;
    }

    private List<XMLReviewInfo>? GetDataFromReviewTargets(List<string> reviewTargetPns)
    {
        var targets = new List<XMLReviewInfo>();
        foreach (var reviewTarget in reviewTargetPns)
        {
            var file = PapyriCRChecker.LoadDoc(reviewTarget);
            if (file == null)
            {
                var reviewer = PapyriCRChecker.ExtractAuthorSurname(file, reviewTarget);
                var pages = PapyriCRChecker.ExtractPageRanges(file, reviewTarget);
                var date = PapyriCRChecker.ExtractDate(file, reviewTarget);
                var location = ExtractJournalNameFromCrSeg(reviewTarget);
                var review = new XMLReviewInfo(reviewer, location, date, pages, reviewTarget);
                targets.Add(review);
            }
        }
        
        return targets;
        
    }

    public List<XMLReviewInfo> ReviewTargetPN { get; set; }
    public string AppearsInTargetPN { get; set; }
    public string? AuthorsSurname { get; set; }
    public string? PageRange { get; set; }
    public string? Date { get; set; }
    public XMLDataEntry Source { get; set; }

    public override string ToString()
    {
        return $"{AuthorsSurname} {PageRange} {Date} (Reviewed by: {ReviewTargetPN}, in: {AppearsInTargetPN}";
    }
}