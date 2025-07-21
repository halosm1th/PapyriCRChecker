// See https://aka.ms/new-console-template for more information

using System.Formats.Asn1;
using System.Text.RegularExpressions;
using System.Xml;
using DefaultNamespace;
using PNCheckNewXMLs;


public class PapyriCRChecker
{
    private static List<XMLDataEntry> filesFromBiblio;
    public static void Main(string[] args)
    {
        var logger = new Logger();
        var directory = FindBiblioDirectory(logger);

        var fileGatherer = new XMLEntryGatherer(directory, logger);
        filesFromBiblio  = fileGatherer.GatherEntries();
        var reviewFiles = GetReviewFiles(filesFromBiblio);
        var parsedXmlReviews  = ParseXMLFiles(reviewFiles);
    }

    private static List<ParsedXMLReviewData> ParseXMLFiles(List<XMLDataEntry> reviewFiles)
    {
        List<ParsedXMLReviewData> parsedXmlReviews = new List<ParsedXMLReviewData>();
        foreach (var reviewFile in reviewFiles)
        {
            var reviewNumbesrFromBiblio = ExtractReviewBiblioIds(reviewFile.PNFileName);
            var reviewFilePath = GetReviewFilePaths(reviewNumbesrFromBiblio);
            var appearsInPath = ExtractAppearsInBiblioId(reviewFile.PNFileName);
            var authorSurname = ExtractAuthorSurname (reviewFile.PNFileName);
            var pages = ExtractPageRanges(reviewFile.PNFileName);
            var date = ExtractDate(reviewFile.PNFileName);
            
            var parsedData = new ParsedXMLReviewData(reviewFilePath,
                appearsInPath,authorSurname, pages,date);
            
            parsedXmlReviews.Add(parsedData);
        }
        
        return parsedXmlReviews;
    }

    private static string FindBiblioDirectory(Logger logger)
    {
        var directoryFinder = new XMLDirectoryFinder(logger);
        var startingDir = Directory.GetCurrentDirectory();
        var directory = directoryFinder.FindBiblioDirectory(startingDir);
        return directory;
    }

    public static List<string> GetReviewFilePaths(List<string> reviewNumbesrFromBiblio)
    {
        var returnPaths = new List<string>();
        foreach (var pnEntryNumber in reviewNumbesrFromBiblio)
        {
            var entry = filesFromBiblio.First(x => x.PNNumber == pnEntryNumber);
            returnPaths.Add(entry.PNFileName);
        }
        
        return returnPaths;
    }

    /// <summary>
    /// Extracts the surname of the author from the XML file, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The author's surname as a string, or null if not found or an error occurs.</returns>
    public static string ExtractAuthorSurname(string filePath)
    {
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

            // Select the <surname> node directly under <author> using the namespace prefix
            XmlNode surnameNode = xmlDoc.SelectSingleNode("//tei:author/tei:surname", nsmgr);

            if (surnameNode != null)
            {
                return surnameNode.InnerText;
            }
            else
            {
                Console.WriteLine($"Author surname not found in file {filePath}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the numerical ID from the target URL of the <ptr> element
    /// within a <relatedItem type="appearsIn"> -> <bibl> structure, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The extracted ID as a string, or null if not found or an error occurs.</returns>
    public static string ExtractAppearsInBiblioId(string filePath)
    {
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

            // Select the <ptr> node within <relatedItem type="appearsIn"> using the namespace prefix
            XmlNode ptrNode = xmlDoc.SelectSingleNode("//tei:relatedItem[@type='appearsIn']/tei:bibl/tei:ptr[@target]", nsmgr);

            if (ptrNode != null)
            {
                string targetValue = ptrNode.Attributes["target"]?.Value;
                if (!string.IsNullOrEmpty(targetValue))
                {
                    // Regex to find the last sequence of digits in the URL
                    Match match = Regex.Match(targetValue, @"(\d+)$");
                    if (match.Success && match.Groups.Count > 1)
                    {
                        return match.Groups[1].Value;
                    }
                    else
                    {
                        Console.WriteLine($"No numerical ID found at the end of the 'appearsIn' in file {filePath}.");
                    }
                }
                else
                {
                    Console.WriteLine($"Target attribute is empty or null for 'appearsIn' <ptr> element  in file {filePath}.");
                }
            }
            else
            {
                Console.WriteLine($"No <ptr> element found within <relatedItem type=\"appearsIn\">/bibl with a 'target' attribute (check namespace or XPath)  in file {filePath}..");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the date from the XML file, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The date as a string, or null if not found or an error occurs.</returns>
    public static string ExtractDate(string filePath)
    {
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

            // Select the <date> node using the namespace prefix
            XmlNode dateNode = xmlDoc.SelectSingleNode("//tei:date", nsmgr);

            if (dateNode != null)
            {
                return dateNode.InnerText;
            }
            else
            {
                Console.WriteLine($"Date not found  in file {filePath}..");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the page ranges from the XML file, specifically from <biblScope type="pp">,
    /// handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The page ranges as a string, or null if not found or an error occurs.</returns>
    public static string ExtractPageRanges(string filePath)
    {
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

            // Select the <biblScope> node with type="pp" using the namespace prefix
            XmlNode pageRangeNode = xmlDoc.SelectSingleNode("//tei:biblScope[@type='pp']", nsmgr);

            if (pageRangeNode != null)
            {
                return pageRangeNode.InnerText;
            }
            else
            {
                Console.WriteLine($"Page ranges not found  in file {filePath}..");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
        }
        return null;
    }
    
    /// <summary>
    /// Extracts the last number from the 'target' attribute of a <ptr> element
    /// specifically located within a <relatedItem type="reviews"> -> <bibl> structure.
    /// This will capture the ID at the end of the URL, such as '84466' from 'https://papyri.info/biblio/84466'.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>
    /// The extracted number as a string, or null if not found or an error occurs.
    /// </returns>
    public static List<string> ExtractReviewBiblioIds(string filePath)
    {
        List<string> reviewIds = new List<string>(); // Initialize a list to store the IDs

        // Check if the file exists before attempting to load it.
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return reviewIds; // Return empty list on error
        }

        XmlDocument xmlDoc = new XmlDocument();
        try
        {
            // Load the XML file.
            xmlDoc.Load(filePath);

            // Create an XmlNamespaceManager to handle namespaces in the XPath query.
            // The NamespaceURI is "http://www.tei-c.org/ns/1.0" as defined in your XML.
            // We'll use "tei" as the prefix for this namespace.
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select ALL <ptr> nodes that are specifically within <relatedItem type="reviews">.
            // Use SelectNodes to get a collection of matching nodes.
            // The XPath now uses the "tei" prefix for elements within that namespace.
            XmlNodeList ptrNodes = xmlDoc.SelectNodes("//tei:relatedItem[@type='reviews']/tei:bibl/tei:ptr[@target]", nsmgr);

            if (ptrNodes != null && ptrNodes.Count > 0)
            {
                foreach (XmlNode ptrNode in ptrNodes)
                {
                    // Get the value of the 'target' attribute for each node.
                    string targetValue = ptrNode.Attributes["target"]?.Value;

                    if (!string.IsNullOrEmpty(targetValue))
                    {
                        // Use a regular expression to find the last sequence of digits in the URL.
                        Match match = Regex.Match(targetValue, @"(\d+)$");

                        if (match.Success && match.Groups.Count > 1)
                        {
                            // Add the captured group (the ID) to the list.
                            reviewIds.Add(match.Groups[1].Value);
                        }
                        else
                        {
                            Console.WriteLine($"Warning: No numerical ID found at the end of the target URL for: {targetValue}");
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Warning: Target attribute is empty or null for a <ptr> element within <relatedItem type=\"reviews\">  in file {filePath}..");
                    }
                }
            }
            else
            {
                Console.WriteLine($"No <ptr> elements found within <relatedItem type=\"reviews\">/bibl with a 'target' attribute (check namespace or XPath)  in file {filePath}..");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
        }

        return reviewIds; // Return the list of extracted IDs
    }

    static List<XMLDataEntry> GetReviewFiles(List<XMLDataEntry> files)
    {
        var reviewFiles = new List<XMLDataEntry>();

        foreach (var file in files)
        {
            if (IsBiblRootOfTypeReview(file.PNFileName))
            {
                reviewFiles.Add(file);
            }
        }
    
        return reviewFiles;
    }
    static bool IsBiblRootOfTypeReview(string filePath)
    {
        // Check if the file exists before attempting to load it.
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return false;
        }

        XmlDocument xmlDoc = new XmlDocument();
        try
        {
            // Load the XML file into the XmlDocument object.
            xmlDoc.Load(filePath);

            // Get the root element of the XML document.
            // The DocumentElement property represents the root node of the XML document.
            XmlElement rootElement = xmlDoc.DocumentElement;

            // Check if the root element exists and its name is "bibl".
            if (rootElement != null && rootElement.Name == "bibl")
            {
                // Check if the "type" attribute exists on the root "bibl" element.
                XmlAttribute typeAttribute = rootElement.Attributes["type"];

                // If the "type" attribute exists, check its value.
                if (typeAttribute != null && typeAttribute.Value == "review")
                {
                    return true; // The root bibl node is of type="review".
                }
            }
        }
        catch (XmlException ex)
        {
            // Handle XML parsing errors (e.g., malformed XML).
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // Handle other potential exceptions (e.g., I/O errors).
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
            return false;
        }

        // If any of the conditions are not met, return false.
        return false;
    }
}

public class XMLReviewInfo
{
    public XMLReviewInfo(string reviewer, string reviewedWhere, string reviewDate, string reviewPages, string itemPtr)
    {
        Reviewer = reviewer;
        ReviewedWhere = reviewedWhere;
        ReviewDate = reviewDate;
        ReviewPages = reviewPages;
        ItemPtr = itemPtr;
    }
    
    public string BPNumber { get; set; }
    public string Reviewer { get; set; }
    public string ReviewedWhere { get; set; }
    public string ReviewDate { get; set; }
    public string ReviewPages { get; set; }
    public string ItemPtr { get; set; }
}

public class ParsedXMLReviewData
{
    public ParsedXMLReviewData(List<string> reviewTargetPns, string appearsInTargetPn, string authorsSurname, string pageRange, string date)
    {
        AppearsInTargetPN = appearsInTargetPn;
        AuthorsSurname = authorsSurname;
        PageRange = pageRange;
        Date = date;

        ReviewTargetPN = GetDataFromReviewTargets(reviewTargetPns);
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

                if (match.Success && match.Groups.Count > 1)
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

    public override string ToString()
    {
        return $"{AuthorsSurname} {PageRange} {Date} (Reviewed by: {ReviewTargetPN}, in: {AppearsInTargetPN}";
    }
}