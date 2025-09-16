using System.Drawing;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using BPtoPNDataCompiler;
using DefaultNamespace;
using OfficeOpenXml;
using OfficeOpenXml.Style;

namespace PapyriCRCheckerV2;

public class PapryiCRCheckerCore
{
    public PapryiCRCheckerCore(Logger _logger)
    {
        logger = _logger;
    }
    
    private Logger logger { get; }
    
    public List<XMLDataEntry> GetFilesWithTypeReview(List<XMLDataEntry> biblioEntries)
    {
        logger.LogProcessingInfo("Gathering files of type review.");
        List<XMLDataEntry> results = new List<XMLDataEntry>();

        foreach (var entry in biblioEntries)
        {
            if (IsBiblRootOfTypeReview(entry))
            {
                if(results.All(x => x.PNFileName != entry.PNFileName)) results.Add(entry);
            }
        }
        
        return results;
    }
    
    
    bool IsBiblRootOfTypeReview(XMLDataEntry entry)
    {

        XmlDocument xmlDoc = entry.BaseDocument;
        try
        {
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
            Console.WriteLine($"Error parsing XML file '{entry.PNFileName}': {ex.Message}");
            logger.LogProcessingInfo($"Error parsing XML file '{entry.PNFileName}': {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            // Handle other potential exceptions (e.g., I/O errors).
            Console.WriteLine($"An unexpected error occurred while processing '{entry.PNFileName}': {ex.Message}");
            logger.LogProcessingInfo($"An unexpected error occurred while processing '{entry.PNFileName}': {ex.Message}");
            return false;
        }

        // If any of the conditions are not met, return false.
        return false;
    }

    public List<ParsedXMLReview> ParsePNReviewsAndAttachToRelevantBiblioFile(List<XMLDataEntry> basePnReviews, List<XMLDataEntry> BiblioFiles)
    {
        var parsedReviews = new List<ParsedXMLReview>();
        
        foreach (var review in basePnReviews)
        {
            var parsed = ParseReview(review);
            parsedReviews.Add(parsed);
            foreach (var ptr in parsed.RelatedItemReviewPtrs)
            {
                var file = BiblioFiles.First(x => x.PNNumber == ptr);
                file.ParsedXMLReviews.Add(parsed);
            }
        }
        
        return parsedReviews;
    }

    private ParsedXMLReview ParseReview(XMLDataEntry review)
    {
        var surname = ExtractAuthorSurname(review.BaseDocument, review.PNFileName);
        var forename = ExtractAuthorForename(review.BaseDocument, review.PNFileName);
        var date = ExtractDate(review.BaseDocument, review.PNFileName);
        var pages = ExtractPageRanges(review.BaseDocument, review.PNFileName);
        var appearsIn = ExtractAppearsIn(review.BaseDocument, review.PNFileName);
        var relatedItemReviews = ExtractRelatedItemReviews(review.BaseDocument, review.PNFileName);
        
        return new ParsedXMLReview(
            review.BaseDocument, review.PNFileName, review.BPNumber,
            forename, surname, date, pages,
            appearsIn,relatedItemReviews);
    }
    
    public string ExtractAppearsIn(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Use a single, more efficient XPath to get all potential nodes at once
            var appearsInPtrNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:relatedItem[@type='appearsIn']/tei:bibl/tei:ptr", nsmgr);

            if(appearsInPtrNode != null)
            {
                var ptrValue = appearsInPtrNode.Attributes["target"].Value;
                if(ptrValue.Contains("https://")) return ptrValue.Replace("https://papyri.info/biblio/", "");
                else if (ptrValue.Contains("http://")) return ptrValue.Replace("http://papyri.info/biblio/", "");
                else return ptrValue;
            }
            else
            {
                Console.WriteLine($"Could not Extreact Appesres in from file: {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
            logger.LogProcessingInfo($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
            logger.LogProcessingInfo($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }
    
    public List<string> ExtractRelatedItemReviews(XmlDocument xmlDoc, string path)
    {
        try
        {
            var reviewIDs = new List<string>();
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Use a single, more efficient XPath to get all potential nodes at once
            XmlNodeList? potentialNodes =
                xmlDoc.SelectNodes("/tei:bibl/tei:relatedItem[@type='reviews']/tei:bibl/tei:ptr", nsmgr);

            if (potentialNodes.Count > 0)
            {
                for (int i = 0; i < potentialNodes.Count; i++)
                {
                    var ptrValue = potentialNodes[i]?.Attributes?["target"]?.Value;
                    if(ptrValue.Contains("https://")) ptrValue = ptrValue.Replace("https://papyri.info/biblio/", "");
                    else if (ptrValue.Contains("http://")) ptrValue = ptrValue.Replace("http://papyri.info/biblio/", "");
                    
                    reviewIDs.Add(ptrValue);
                }
                
                return reviewIDs;
            }
            else
            {
                Console.WriteLine($"Related Items not found in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
            logger.LogProcessingInfo($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
            logger.LogProcessingInfo($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }
    
    public string ExtractPageRanges(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Use a single, more efficient XPath to get all potential nodes at once
            XmlNodeList? potentialNodes = xmlDoc.SelectNodes("/tei:bibl/tei:biblScope[@type='pp'] | /tei:bibl/tei:biblScope[@type='col'] | /tei:bibl/tei:note[@type='pageCount']",
                nsmgr);

            if (potentialNodes.Count > 0)
            {
                foreach (XmlNode node in potentialNodes)
                {
                    if (node.Attributes["type"]?.Value == "pp" || node.Attributes["type"] == null)
                    {
                        return node.InnerText;
                    }
                    else if (node.Attributes["type"]?.Value == "col")
                    {
                        return node.InnerText.Replace("coll. ", "");
                    }
                }
            }
            else
            {
                Console.WriteLine($"Page ranges not found in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
            logger.LogProcessingInfo($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
            logger.LogProcessingInfo($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }
    
    public string? ExtractDate(XmlDocument xmlDoc, string path)
    {
        var dateNode = GetTextFromNode(xmlDoc,"/tei:bibl/tei:date" , path);
        if(!string.IsNullOrEmpty(dateNode)) return dateNode;
        
        return null;
    }
    
    public string? ExtractAuthorSurname(XmlDocument xmlDoc, string path)
    {
        var authorForename = GetTextFromNode(xmlDoc,"/tei:bibl/tei:author/tei:surname" , path);
        if(!string.IsNullOrEmpty(authorForename)) return authorForename;
        
        var editorForename = GetTextFromNode(xmlDoc,"/tei:bibl/tei:editor/tei:surname" , path);
        if(!string.IsNullOrEmpty(editorForename)) return editorForename;
        
        return null;
    }
    
    public string? ExtractAuthorForename(XmlDocument xmlDoc, string path)
    {
        var authorForename = GetTextFromNode(xmlDoc,"/tei:bibl/tei:author/tei:forename" , path);
        if(!string.IsNullOrEmpty(authorForename)) return authorForename;
        
        var editorForename = GetTextFromNode(xmlDoc,"/tei:bibl/tei:editor/tei:forename" , path);
        if(!string.IsNullOrEmpty(editorForename)) return editorForename;
        
        return null;
    }
    
    public List<XMLDataEntry> GetFilesWithCNSeg(List<XMLDataEntry> biblioFiles)
    {
        logger.LogProcessingInfo($"Gather files with CN segs from {biblioFiles.Count} biblio files");
        var reviewFiles = new List<XMLDataEntry>();

        foreach (var file in biblioFiles)
        {
            if (HasCrSegNode(file.BaseDocument, file.PNFileName))
            {
                reviewFiles.Add(file);
            }
        }
        logger.LogProcessingInfo($"Gathered {reviewFiles.Count} files with CN segs from {biblioFiles.Count} biblio files");
    
        return reviewFiles;
    }
    
    public bool HasCrSegNode(XmlDocument xmlDoc, string filePath) 
        =>  !string.IsNullOrEmpty(GetCrSegText(xmlDoc, filePath));
    
    public string? GetCrSegText(XmlDocument xmlDoc, string filePath)
    {
        var crSegNode = GetTextFromNode(xmlDoc,"//tei:seg[@subtype='cr']" , filePath);
        if(!string.IsNullOrEmpty(crSegNode)) return crSegNode;
        
        return null;
    }

    public List<CRReviewData> ParsedCNReviewsAndAttachToRelevantBiblioFile(List<XMLDataEntry> baseCnReviews)
    {
        logger.LogProcessingInfo("Parsing CR reviews and attaching them to the relevant files");
        List<CRReviewData> crReviews = new List<CRReviewData>();

        foreach (var entry in baseCnReviews)
        {
            var text = GetCrSegText(entry.BaseDocument, entry.PNFileName);
            if (text != null)
            {
                var reviews = ProcessCrReviews(text, entry);
                crReviews.AddRange(reviews);
            }
        }
        
        return crReviews;
    }

    private List<CRReviewData> ProcessCrReviews(string text, XMLDataEntry entry)
    {
        logger.LogProcessingInfo($"\tProcessing CR data: {text}");
        var parsedReviews = new List<CRReviewData>();
        
        text = text.Replace("C.R. par ", "");
        text = text.Replace("C.R. ", ""); 
        var crReviews = text.Split(" - ");

        foreach (var review in crReviews)
        {
            var baseText = review;
            var name = review.Split(",")[0];
            var pages = "";
            var year = "";
            var journalNumber = "";
            var reviewWithoutName = review.Replace($"{name},", "");
            
            var pageMatchRegex = new Regex(@"(pp\. |p\. )\d+(-\d+)?");
            var pagesMatch = pageMatchRegex.Match(reviewWithoutName);
            if (pagesMatch.Success)
            {
                var pageRegex = new Regex(@"\d+-\d+");
                pages = pageRegex.Match(pagesMatch.Value).Value;
                reviewWithoutName = reviewWithoutName.Replace($"{pagesMatch}", "");
            }
            
            var yearRegex = new Regex(@"\b(19|20)\d{2}\b");
            var yearMatch = yearRegex.Match(reviewWithoutName);
            if (yearMatch.Success)
            {
                year = yearMatch.Value;
                reviewWithoutName = reviewWithoutName.Replace(yearMatch.Value, "");
            }
            
            var journalNUmberRegex = new Regex(@"( \d+ \()");
            var journalNumberMatch = journalNUmberRegex.Match(reviewWithoutName);
            if (journalNumberMatch.Success)
            {
                journalNumber = journalNumberMatch.Value;
                journalNumber = journalNumber.Replace("(", "");
                reviewWithoutName = reviewWithoutName.Replace(journalNumberMatch.Value, "");
            }
            
            if(reviewWithoutName.Contains(")")) reviewWithoutName = reviewWithoutName.Split(")")[0].Trim();
            if(reviewWithoutName.EndsWith(",")) reviewWithoutName.Remove(reviewWithoutName.Length - 1);

            var link = "NO LINK";
            if (reviewWithoutName.Contains("http"))
            {
                link = reviewWithoutName.Split("http")[0];
                reviewWithoutName = reviewWithoutName.Replace(link, "");
            }
           
            var journalName = reviewWithoutName;
            journalName = journalName.Replace("&amp;", "&");
            if(journalName.Contains(",")) journalName = journalName.Split(",")[0].Trim();

                        
                        
            
            
            Console.ForegroundColor = ConsoleColor.Green;
           Console.Write($"Got name: {name}, pages: {pages}, year: {year}, number: {journalNumber}, link? {link}, journal {journalName}");
            Console.ResetColor();
            Console.Write("need to process the rest of the CR Review: ");
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.Write($"{reviewWithoutName.Trim()}\n");
            Console.ResetColor();
            Console.WriteLine($"\t{review}");
            
            logger.LogProcessingInfo($"Got name: {name}, pages: {pages}, year: {year}, number: {journalNumber}, link? {link}, journal {journalName}" +
                                     $"need to process the rest of the CR Review: {reviewWithoutName.Trim()}\n\t{review}");

            var parsedCRReview = new CRReviewData(entry, name, pages, year,
                journalName, journalNumber, entry.PNNumber, baseText, logger);
            parsedReviews.Add(parsedCRReview);
            entry.ParsedCRReviews.Add(parsedCRReview);
        }
        
        return parsedReviews;
    }
    
    public string? GetTextFromNode(XmlDocument xmlDoc, string xPathToNode, string path)
    {
        logger.LogProcessingInfo($"Gathering text from xpath {xPathToNode} from file {path}");
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");
            
            // Select the <surname> node directly under <author> using the namespace prefix
            XmlNode foundNode = xmlDoc.SelectSingleNode(xPathToNode, nsmgr);

            if (foundNode != null)
            {
                logger.LogProcessingInfo($"Found xpath in file {path} with value {foundNode.InnerText}");
                return foundNode.InnerText;
            }
            else
            {
                logger.LogProcessingInfo($"Could not find xpath: [{xPathToNode}] in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
            logger.LogProcessingInfo($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
            logger.LogProcessingInfo($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }
    
    public void SaveMatchResultsInSpreadsheet(List<XMLDataEntry> reviews)
    {
        logger.LogProcessingInfo($"Saving {reviews.Count} reviews into the spreadsheet ");
        var filePath = Directory.GetCurrentDirectory() + @"\reviewMatches.xlsx";
        var newFile = new FileInfo(filePath);
        if (!newFile.Exists)
        {
            //newFile.Delete();  // ensures we create a new workbook
            newFile = new FileInfo(filePath);
        }
        
        
        logger.LogProcessingInfo($"Saving {reviews.Count} reviews into the spreadsheet @ {filePath}");
        using (ExcelPackage package = new ExcelPackage(newFile))
        {
            var numb = 1;
            if (package.Workbook.Worksheets.Any(x => x.Name == $"Reviews in PN and BP"))
            {
                package.Workbook.Worksheets.Delete("Reviews in PN and BP");
            }
            var worksheet = package.Workbook.Worksheets.Add($"Reviews in PN and BP");
            worksheet.Cells[numb,1].Value = "PN Number";
            worksheet.Cells[numb,2].Value = "BP Number";
            worksheet.Cells[numb,3].Value = "# PN Reviews";
            worksheet.Cells[numb,4].Value = "# BP Reviews";
            worksheet.Cells[numb,5].Value = "Equal?";
            numb++;
            
            foreach (var match in reviews)
            {
                var equal = match.ParsedCRReviews.Count == match.ParsedXMLReviews.Count;
                
                var bpVal = match.HasBPNum ? match.BPNumber : "No BP Value";
                Console.WriteLine(
                    $"PN: {match.PNNumber} BP: {bpVal} has {match.ParsedXMLReviews.Count} PN reviews and {match.ParsedCRReviews.Count} BP reviews. Are they equal: {equal}");
                worksheet.Cells[numb,1].Value = match.PNNumber;
                worksheet.Cells[numb,2].Value = bpVal;
                worksheet.Cells[numb,3].Value = match.ParsedXMLReviews.Count;
                worksheet.Cells[numb,4].Value = match.ParsedCRReviews.Count;
                worksheet.Cells[numb,5].Value = equal;
                worksheet.Cells[numb,5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[numb,5].Style.Fill.BackgroundColor.SetColor(equal? Color.Green : Color.Red); 
                numb++;

            }
            package.Save();
        }
    }

    public List<XMLDataEntry> GetFilesWithUnequalReviews(List<XMLDataEntry> biblioFiles)
    {
        logger.LogProcessingInfo("Getting files with an unequal # of reviews.");
        var filesToCheck = new List<XMLDataEntry>();

        foreach (var file in biblioFiles)
        {
            if(file.ParsedXMLReviews.Count != file.ParsedCRReviews.Count) filesToCheck.Add(file);
        }
        logger.LogProcessingInfo($"Gathered {filesToCheck.Count} files without reviews from {biblioFiles.Count} files.");
        return filesToCheck;
    }

    public void SaveCrReviewsInXML(CRReviewData cr, ref string currentMaxXmlid, string xmlDir)
    {
        if (!Int32.TryParse(currentMaxXmlid, out int currentMax))
        {
            logger.LogProcessingInfo($"Could not convert max id: {currentMaxXmlid} to int");
            throw new ArgumentException($"Error the current max XML ID ({currentMaxXmlid}) is not a number.");
        }

        currentMax = currentMax + 1;
        currentMaxXmlid = currentMax.ToString();
        cr.IDNumber = currentMaxXmlid;
        xmlDir = Path.Combine(xmlDir, $"{cr.IDNumber}.xml");

        if (cr.JournalID == "-1")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("There is a problem with this file where we could not find the journal ID. Please update with proper value. once saved");
            logger.LogProcessingInfo("\tThere is a problem with this file where we could not find the journal ID. Please update with proper value. once saved");
            Console.ResetColor();
        }



        Console.WriteLine($"{cr}");
        Console.ForegroundColor = ConsoleColor.Cyan;
        Console.WriteLine($"Press Y if it should save the above xml to: {xmlDir}.");
        Console.ResetColor();
        
        var key = Console.ReadKey();
        if (key.Key == ConsoleKey.Y)
        {
            logger.LogProcessingInfo($"created new xml file: {xmlDir}");
            File.WriteAllText(xmlDir, cr.ToString());
        }
    }

    public void SavePNReviewsToSendToBP(ParsedXMLReview pn, string pathForCRUpdateFile)
    {
        //var stream = new FileStream(pathForCRUpdateFile, FileMode.OpenOrCreate);
        if (!File.Exists(pathForCRUpdateFile))
        {
            logger.LogProcessingInfo("File for PN reviews to send to BP did not exist, creating it.");
            File.Create(pathForCRUpdateFile).Close();
        }
        
        File.AppendAllText(pathForCRUpdateFile, pn.ToCRUpdateString());
    }

    public void SaveFilesWithUnequalReviews(List<XMLDataEntry> filesWithUnequalReviews, string saveXmlDir,
        string savePathForReviewsinXMLNotInCR, string currentMaxXMLID)
    {
        logger.LogProcessingInfo($"Processing {filesWithUnequalReviews.Count} files with unequal reviews.");
        int fileProcessed = 1;
        foreach (var file in filesWithUnequalReviews)
        {
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine($"Processing reviews in: {file.PNFileName} ({fileProcessed}/{filesWithUnequalReviews.Count})");
            logger.LogProcessingInfo($"Processing reviews in: {file.PNFileName} ({fileProcessed}/{filesWithUnequalReviews.Count})");
            Console.ResetColor();
            var reviewsToSave = file.CompareReviews();
            if (reviewsToSave != null)
            {

                Console.ForegroundColor = ConsoleColor.Yellow;
                Console.WriteLine($"Saving reviews not in CR or PN. The {reviewsToSave.Value.Item1.Count} CR reviews will be saved to: {savePathForReviewsinXMLNotInCR}, the {reviewsToSave.Value.Item2.Count} PN reviews will be saved to: {saveXmlDir}.");
                logger.LogProcessingInfo($"Saving reviews not in CR or PN. CR will be saved to: {savePathForReviewsinXMLNotInCR}, PN: {saveXmlDir}.");
                Console.ResetColor();
        
                foreach (var PN in reviewsToSave.Value.Item1)
                {
                    SavePNReviewsToSendToBP(PN, savePathForReviewsinXMLNotInCR);
                }
        
                foreach (var CR in reviewsToSave.Value.Item2)
                {
            
                    SaveCrReviewsInXML(CR, ref currentMaxXMLID, saveXmlDir);
                }

            }

            Console.WriteLine("----------------------------------------");
            fileProcessed++;
        }
        
    }
    
    
    public string GetLargestDirInXMLDir(string s)
    {
        var folders = Directory.GetDirectories(s);
        var curMax = 0;
        foreach (var folder in folders)
        {
            int numb = 0;
            if (Int32.TryParse(folder, out numb))
            {
                if(numb > curMax) curMax = numb;
            }
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"Found max folder as: {curMax}");
        Console.ResetColor();
        Console.ReadKey();
        return curMax.ToString();
    }
}