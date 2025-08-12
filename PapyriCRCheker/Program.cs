// See https://aka.ms/new-console-template for more information

using System.Drawing;
using System.Formats.Asn1;
using System.Text.RegularExpressions;
using System.Xml;
using BPtoPNDataCompiler;
using DefaultNamespace;
using OfficeOpenXml;
using OfficeOpenXml.Style;
using PNCheckNewXMLs;

public class PapyriCRChecker
{
    private static List<XMLDataEntry> filesFromBiblio;
    public static void Main(string[] args)
    {
        ExcelPackage.License.SetNonCommercialPersonal("Thomas");
        
        var logger = new Logger();
        var directory = FindBiblioDirectory(logger);

        var fileGatherer = new XMLEntryGatherer(directory, logger);
        filesFromBiblio  = fileGatherer.GatherEntries();
        var reviewFiles = GetReviewFiles(filesFromBiblio);
        var parsedXmlReviews  = ParseXMLFiles(reviewFiles);

        Console.WriteLine("Gathering CR elements from files in the biblio dir.");
        var CRFiles = GetCRFiles(filesFromBiblio);
        var crParser = new CRReviewParser(logger,filesFromBiblio,Directory.GetCurrentDirectory());
        var lastPN = 0; //GetLastPN(-1, logger, filesFromBiblio);
        var parsedCRReviews = crParser.ParseReviews(CRFiles, ref lastPN);
        
        var matchedReviews = MatchReviews(parsedXmlReviews, parsedCRReviews, filesFromBiblio);
        SaveMatchResultsInSpreadsheet(matchedReviews);

        foreach (var match in matchedReviews)
        {
            var newResults = CompareReviews(match);
            SaveResults(newResults, logger);
        }
    }

    private static void SaveResults(List<(List<ParsedXMLReviewData> ReviewInPNNotINBP,
        List<CRReviewData> ReviewInBPNotINPN)> newResults, Logger logger)
    {
        foreach (var review in newResults)
        { 
            SaveReviewsNotInBP(review.ReviewInPNNotINBP);
            SaveReviewsNotInPN(review.ReviewInBPNotINPN, logger);
        }
    }

    private static void SaveReviewsNotInPN(List<CRReviewData> reviewReviewInBpNotInpn, Logger logger)
    {
        var lastID = -1;
        lastID = GetLastPN(lastID,logger, filesFromBiblio);
        foreach (var review in reviewReviewInBpNotInpn)
        {
            lastID++;
            review.IDNumber = lastID.ToString();
            
        }
        
        throw new NotImplementedException();
    }

    private static void SaveReviewsNotInBP(List<ParsedXMLReviewData> reviewReviewInPnNotInbp)
    {
        var saveFile = Directory.GetCurrentDirectory() + $"/PapyriCRChecker results from {DateTime.Now}.txt";
        foreach (var review in reviewReviewInPnNotInbp)
        {
            var fileName = review.Source.PNFileName;
            Console.WriteLine($"Loading: {fileName} because BP is missing some elements from it.");
            var file = LoadDoc(fileName);

            if (file != null)
            {
                var firstname = ExtractAuthorFirstname(file, fileName);
                var surname = ExtractAuthorSurname(file, fileName);
                var publicationInformation = ExtractPublicationInfo(file, fileName);
                
                var saveTxt = $"{firstname} {surname} {publicationInformation}\n";

                Console.WriteLine(saveTxt);
                if (File.Exists(saveFile))
                {
                    File.AppendAllText(saveFile,saveTxt);
                }
                else
                {
                    File.WriteAllText(saveFile, saveTxt);
                }
            }
        }
    }

    private static object ExtractPublicationInfo(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <biblScope> node with type="pp" using the namespace prefix
            XmlNode publicationNode = xmlDoc.SelectSingleNode("//tei:seg[@subtype='publication']", nsmgr);
            XmlNode colRangeNode = xmlDoc.SelectSingleNode("//tei:biblScope[@type='col']", nsmgr);
            XmlNode pageCount = xmlDoc.SelectSingleNode("//tei:note[@type='pageCount']", nsmgr);

            if (publicationNode != null)
            {
                return publicationNode.InnerText;
            }/* else if (pageCount != null)
           {
                return pageCount.InnerText;
            }

            if (colRangeNode != null)
            {
                return colRangeNode.InnerText.Replace("coll. ", "");
            } */
            else
            {
                Console.WriteLine($"Page ranges not found in file {path} while extracting publication info.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }

    private static List<(List<ParsedXMLReviewData> ReviewInPNNotINBP, List<CRReviewData> ReviewInBPNotINPN)> 
        CompareReviews(MatchedReviews match)
    {
        var BPNotMatched = match.CRReviews;
        var PNNotMatched = match.PNReviews;
        
        var BpIsBigger = BPNotMatched.Count > PNNotMatched.Count;
        
        if (BpIsBigger) return ReviewBPDominant(PNNotMatched, BPNotMatched);
        else return ReviewPNDominant(PNNotMatched, BPNotMatched);
    }

    private static List<(List<ParsedXMLReviewData> ReviewInPNNotINBP, List<CRReviewData> ReviewInBPNotINPN)> 
        ReviewBPDominant(List<ParsedXMLReviewData> PNNotMatched, List<CRReviewData> bpNotMatched)
    {
        var results = new List<(List<ParsedXMLReviewData> ReviewInPNNotINBP, List<CRReviewData> ReviewInBPNotINPN)>();
        var reviewCount = bpNotMatched.Count;

        for (int i = 0; i < reviewCount; i++)
        {
            // if (PNNotMatched.Count == 0 || bpNotMatched.Count == 0) break;

            var current = bpNotMatched[i];
            if (AnyBPMatchInPNNotMatched(current, PNNotMatched))
            {
                var matchedReview = GetMatchFromBP(current, PNNotMatched);
                if (matchedReview != null)
                {
                    CheckReviewFileHAsCR(current.Source.PNFileName, matchedReview.Source.CR);
                    PNNotMatched.Remove(matchedReview);
                    bpNotMatched.Remove(current);
                    Console.WriteLine("------------------------------------------------------------------------");
                    Console.WriteLine($"{current.Source.PNNumber}-{i}-{current.JournalID} | PN | BP | Match");
                    Console.WriteLine(
                        $"surname | {current.Lastname ?? "NOT FOUND"} | {matchedReview.AuthorsSurname} | {current.Lastname?.Equals(matchedReview.AuthorsSurname) ?? false}");
                    Console.WriteLine(
                        $"publication | {current.JournalID} | {matchedReview.AppearsInTargetPN} | {current.AppearsInID.Equals(matchedReview.AppearsInTargetPN)}");
                    Console.WriteLine(
                        $"page range | {current.PageRange ?? "NOT FOUND"} | {matchedReview.PageRange} | {current.PageRange?.Equals(matchedReview.PageRange) ?? false}");
                    Console.WriteLine(
                        $"date (not compared) | {current.Date ?? "NOT FOUND"} | {matchedReview.Date} | {current.Date?.Equals(matchedReview.Date) ?? false}");
                }
            }
        }

        results.Add((PNNotMatched, bpNotMatched));
        return results;
    }


    private static bool AnyBPMatchInPNNotMatched(CRReviewData current, List<ParsedXMLReviewData> pnNotMatched)
    {
        var match = false;
        foreach (var entry in pnNotMatched)
        {
            var surname = entry.AuthorsSurname == current.Lastname;
            //var date = entry.Date == current.Date;
            var publication = entry.AppearsInTargetPN == current.JournalID;
            var pageRange = $"{current.PageStart}-{current.PageEnd}" == entry.PageRange;

            if (surname && publication && pageRange) return true;
            match = match || surname || publication || pageRange;
        }

        return match;
    }

    private static List<(List<ParsedXMLReviewData> ReviewInPNNotINBP, List<CRReviewData> ReviewInBPNotINPN)>  
        ReviewPNDominant(List<ParsedXMLReviewData> PNNotMatched, List<CRReviewData> BPNotMatched)
    {
        var results = new List<(List<ParsedXMLReviewData> ReviewInPNNotINBP, List<CRReviewData> ReviewInBPNotINPN)>();
        var reviewCount = PNNotMatched.Count;

        for (int i = 0; i < reviewCount; i++)
        {
            if (BPNotMatched.Count == 0 || PNNotMatched.Count == 0) break;

            var current = PNNotMatched[i];
            if (AnyPNMatchInBP(current, BPNotMatched))
            {
                var matchedReview = GetMatchFromPN(current, BPNotMatched);
                if (matchedReview != null)
                {
                    CheckReviewFileHAsCR(current.Source.PNFileName, matchedReview.CRData);
                    BPNotMatched.Remove(matchedReview);
                    PNNotMatched.Remove(current);
                    Console.WriteLine("------------------------------------------------------------------------");
                    Console.WriteLine($"{current.Source.PNNumber}-{i}-{current.AppearsInTargetPN} | PN | BP | Match");
                    Console.WriteLine($"surname | {current.AuthorsSurname ?? "NOT FOUND"} | {matchedReview.Lastname} | {current.AuthorsSurname?.Equals(matchedReview.Lastname) ?? false}");
                    Console.WriteLine($"publication | {current.AppearsInTargetPN} | {matchedReview.AppearsInID} | {current.AppearsInTargetPN.Equals(matchedReview.AppearsInID)}");
                    Console.WriteLine($"page range | {current.PageRange ?? "NOT FOUND"} | {matchedReview.PageRange}-{matchedReview.PageEnd} | {current.PageRange?.Equals(matchedReview.PageRange) ?? false}");
                    Console.WriteLine($"date (not compared) | {current.Date ?? "NOT FOUND"} | {matchedReview.Date} | {current.Date?.Equals(matchedReview.Date) ?? false}");
                }
            }
        }
        
        results.Add((PNNotMatched, BPNotMatched));
        return results;
    }

    private static void CheckReviewFileHAsCR(string currentSource, string matchedCRData)
    {
        var file = new XmlDocument();
        file.Load(currentSource);
        // Create an XmlNamespaceManager to handl
        XmlNamespaceManager nsmgr = new XmlNamespaceManager(file.NameTable);

        nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

 
        var seg = file.SelectSingleNode("//tei:seg[@subtype='cr']", nsmgr);
        if (seg != null)
        {
            var crText = seg.InnerText;
            var matchText = matchedCRData.Replace("&amp;","&").Replace("&lt;", "<").Replace("&gt;", ">");
            if (crText != null)
            {
                if (crText.Replace("C.R. par", "").Replace("C.R. ", "").Contains(matchText)) return;
                else
                {
                    bool notFound = true;
                    while (notFound)
                    {
                        Console.WriteLine(
                            $"The system wants to correct the Seg element which currently reads as: {seg.InnerText} to contain {matchText}.");
                        Console.WriteLine("Is this correct? (y/n)");
                        var input = Console.ReadKey();
                        if (input.Key.ToString().ToUpper() == "Y")
                        {
                            seg.InnerText = seg.InnerText +
                                            $" - {matchText}";
                            notFound = false;
                Console.WriteLine($"Updated {currentSource} cr to be: {seg.InnerText}");
                
                        }
                        else if (input.Key.ToString().ToUpper() == "N") notFound = false;
                        else Console.WriteLine("Error, please enter y/n.");
                    }
                }
            }
        }
        
        file.Save(currentSource);
    }
    
    private static ParsedXMLReviewData GetMatchFromBP(CRReviewData current, List<ParsedXMLReviewData> pnNotMatched)
    {
        ParsedXMLReviewData review = null;

        (ParsedXMLReviewData PNReviewData, int strength) strongestMatch = new ValueTuple<ParsedXMLReviewData, int>(null, -1);
        foreach (var entry in pnNotMatched)
        {
            var strength = GetMatchStrength(entry,current);
            if (strength > strongestMatch.strength)
            {
                strongestMatch = (entry, strength);
            }
        }


        if (strongestMatch.PNReviewData != null)
        {
            review = strongestMatch.PNReviewData;
        }
        
        return review;
    }
    
    private static CRReviewData? GetMatchFromPN(ParsedXMLReviewData current, List<CRReviewData> bpNotMatched)
    {
        CRReviewData review = null;

        (CRReviewData CRReviewData, int strength) strongestMatch = new ValueTuple<CRReviewData, int>(null, -1);
        foreach (var entry in bpNotMatched)
        {
            var strength = GetMatchStrength(current, entry);
            if (strength > strongestMatch.strength)
            {
                strongestMatch = (entry, strength);
            }
        }


        if (strongestMatch.CRReviewData != null)
        {
            review = strongestMatch.CRReviewData;
        }
        
        return review;
    }

    private static int GetMatchStrength(ParsedXMLReviewData current, CRReviewData bpNotMatched)
    {
        var count = 0;
        var surname = bpNotMatched.Lastname == current.AuthorsSurname;
        //var date = entry.Date == current.Date;
        var publication = bpNotMatched.JournalID == current.AppearsInTargetPN; ;
        var pageRange = bpNotMatched.PageRange.Equals(current.PageRange);

        if (surname && publication && pageRange) return 3;
        if (surname) count++;
        if (publication) count++;
        if (pageRange) count++;

        return count;
    }


    private static bool AnyPNMatchInBP(ParsedXMLReviewData current, List<CRReviewData> bpNotMatched)
    {
        var match = false;
        foreach (var entry in bpNotMatched)
        {
            var surname = entry.Lastname == current.AuthorsSurname;
            //var date = entry.Date == current.Date;
            var publication = entry.JournalID == current.AppearsInTargetPN;
            var pageRange = $"{entry.PageStart}-{entry.PageEnd}" == current.PageRange;

            if (surname && publication && pageRange) return true;
            match = match || surname || publication || pageRange;
        }

        return match;
    }

    private static void SaveMatchResultsInSpreadsheet(List<MatchedReviews> matchedReviews)
    {
        var newFile = new FileInfo( Directory.GetCurrentDirectory()+ @"\reviewMatches.xlsx");
        if (!newFile.Exists)
        {
            //newFile.Delete();  // ensures we create a new workbook
            newFile = new FileInfo(Directory.GetCurrentDirectory()+ @"\reviewMatches.xlsx");
        }
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
            
            foreach (var match in matchedReviews)
            {
                var bpVal = match.ReviewFrom.HasBPNum ? match.ReviewFrom.BPNumber : "NONE";
                Console.WriteLine(
                    $"PN: {match.ReviewFrom.PNNumber} BP: {bpVal} has {match.PNReviews.Count} PN reviews and {match.CRReviews.Count} BP reviews. Are they equal: {match.SameNumberOfReviews}");
                worksheet.Cells[numb,1].Value = match.ReviewFrom.PNNumber;
                worksheet.Cells[numb,2].Value = bpVal;
                worksheet.Cells[numb,3].Value = match.PNReviews.Count;
                worksheet.Cells[numb,4].Value = match.CRReviews.Count;
                worksheet.Cells[numb,5].Value = match.SameNumberOfReviews;
                worksheet.Cells[numb,5].Style.Fill.PatternType = ExcelFillStyle.Solid;
                worksheet.Cells[numb,5].Style.Fill.BackgroundColor.SetColor(match.SameNumberOfReviews? Color.Green : Color.Red); 
                numb++;

            }
            package.Save();
        }
    }

    private static List<MatchedReviews> MatchReviews(List<ParsedXMLReviewData> parsedXmlReviews, 
        List<CRReviewData> parsedCrReviews, List<XMLDataEntry> biblioFiles)
    {
        var reviews = new List<MatchedReviews>();

        foreach (var entry in biblioFiles)
        {
            var xmlMatches
                = parsedXmlReviews.Where(x
                    => x.Source.PNNumber == entry.PNNumber).ToList();
            var crMatches
                = parsedCrReviews.Where(x
                    => x.Source.PNNumber == entry.PNNumber).ToList();

            if (xmlMatches.Count > 0 || crMatches.Count > 0)
            {
                var match = new MatchedReviews(entry, xmlMatches, crMatches);
                reviews.Add(match);
            }
        }
        
        return reviews;
    }


    private static int GetLastPN(int LastPN, Logger logger, List<XMLDataEntry> xmlEntries)
    {
        if (LastPN == -1)
        {
            var lastPNAsString = GetLastPNText(xmlEntries);

            if (Int32.TryParse(lastPNAsString, out LastPN))
            {
                Console.ForegroundColor = ConsoleColor.Cyan;
                Console.WriteLine($"Converted PN to string: {lastPNAsString}.\n");
                Console.ResetColor();
                logger.Log($"Converted PN to string: {lastPNAsString}.\n");
                return LastPN;
            }
            else
            {
                Console.WriteLine($"There was an error parsing the last PN number! {lastPNAsString}.\nExiting");
                logger.Log($"There was an error parsing the last PN Number! {lastPNAsString}");
                return -1;
            }
        }

        return LastPN;
    }
    
    private static string? GetLastPNText(List<XMLDataEntry> XmlEntries)
    {
        var entries = XmlEntries.OrderBy(x => x.PNNumber);

        var largestPN = 0;
        foreach (var entry in entries)
        {
            if (Int32.TryParse(entry.PNNumber, out int numb))
            {
                if (numb > largestPN) largestPN = numb;
            }
        }

        return Convert.ToString(largestPN);
    }
    
    private static List<XMLDataEntry> GetCRFiles(List<XMLDataEntry> biblioFiles)
    {
        
        var reviewFiles = new List<XMLDataEntry>();

        foreach (var file in biblioFiles)
        {
            if (HasCrSegNode(file.PNFileName))
            {
                reviewFiles.Add(file);
            }
        }
    
        return reviewFiles;
        
    }
    /// <summary>
    /// Determines if an XML file contains a <seg> node with subtype="cr".
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>True if a <seg subtype="cr"> node is found, false otherwise.</returns>
    public static bool HasCrSegNode(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return false;
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

            // If the node is found, it means the file has a <seg subtype="cr"> element.
            return crSegNode != null;
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{filePath}': {ex.Message}");
            return false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{filePath}': {ex.Message}");
            return false;
        }
    }


    private static List<ParsedXMLReviewData> ParseXMLFiles(List<XMLDataEntry> reviewFiles)
    {
        List<ParsedXMLReviewData> parsedXmlReviews = new List<ParsedXMLReviewData>();
        foreach (var reviewFile in reviewFiles)
        {
            var doc = LoadDoc(reviewFile.PNFileName);
            if (doc != null)
            {
                var reviewNumbesrFromBiblio = ExtractReviewBiblioIds(doc, reviewFile.PNFileName);
                var reviewFilePath = GetReviewFilePaths(reviewNumbesrFromBiblio);
                var appearsInPath = ExtractAppearsInBiblioId(doc, reviewFile.PNFileName);
                var authorSurname = ExtractAuthorSurname(doc, reviewFile.PNFileName);
                var pages = ExtractPageRanges(doc, reviewFile.PNFileName);
                var date = ExtractDate(doc, reviewFile.PNFileName);

                var parsedData = new ParsedXMLReviewData(reviewFilePath,
                    appearsInPath, authorSurname, pages, date, reviewFile);

                parsedXmlReviews.Add(parsedData);
            }
            else
            {
                Console.WriteLine($"There was an loading: {reviewFile.PNFileName}");
            }
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
            if(filesFromBiblio.Any(x => x.PNNumber == pnEntryNumber)){
                var entry = filesFromBiblio.First(x => x.PNNumber == pnEntryNumber);
                returnPaths.Add(entry.PNFileName);
            }
        }
        
        return returnPaths;
    }

    public static string ExtractAuthorFirstname(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <surname> node directly under <author> using the namespace prefix
            XmlNode authorfirstName = xmlDoc.SelectSingleNode("/tei:bibl/tei:author/tei:forename", nsmgr);
            XmlNode editorFirstName = xmlDoc.SelectSingleNode("/tei:bibl/tei:editor/tei:forename", nsmgr);

            if (authorfirstName != null)
            {
                return authorfirstName.InnerText;
            }
            if (editorFirstName != null)
            {
                return editorFirstName.InnerText;
            }
            else
            {
                Console.WriteLine($"Author first name not found in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }
    

    /// <summary>
    /// Extracts the surname of the author from the XML file, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The author's surname as a string, or null if not found or an error occurs.</returns>
    public static string ExtractAuthorSurname(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <surname> node directly under <author> using the namespace prefix
            XmlNode authorSurnameNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:author/tei:surname", nsmgr);
            XmlNode editorSurnameNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:editor/tei:surname", nsmgr);

            if (authorSurnameNode != null)
            {
                return authorSurnameNode.InnerText;
            }
            if (editorSurnameNode != null)
            {
                return editorSurnameNode.InnerText;
            }
            else
            {
                Console.WriteLine($"Author surname not found in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the numerical ID from the target URL of the <ptr> element
    /// within a <relatedItem type="appearsIn"> -> <bibl> structure, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The extracted ID as a string, or null if not found or an error occurs.</returns>
    public static string ExtractAppearsInBiblioId(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <ptr> node within <relatedItem type="appearsIn"> using the namespace prefix
            XmlNode ptrNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:relatedItem[@type='appearsIn']/tei:bibl/tei:ptr[@target]", nsmgr);

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
                        Console.WriteLine($"No numerical ID found at the end of the 'appearsIn' in file {path}.");
                    }
                }
                else
                {
                    Console.WriteLine($"Target attribute is empty or null for 'appearsIn' <ptr> element  in file {path}.");
                }
            }
            else
            {
                Console.WriteLine($"No <ptr> element found within <relatedItem type=\"appearsIn\">/bibl with a 'target' attribute (check namespace or XPath)  in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the date from the XML file, handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The date as a string, or null if not found or an error occurs.</returns>
    public static string ExtractDate(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <date> node using the namespace prefix
            XmlNode dateNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:date", nsmgr);

            if (dateNode != null)
            {
                return dateNode.InnerText;
            }
            else
            {
                Console.WriteLine($"Date not found  in file {path}..");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }

    /// <summary>
    /// Extracts the page ranges from the XML file, specifically from <biblScope type="pp">,
    /// handling XML namespaces.
    /// </summary>
    /// <param name="filePath">The path to the XML file.</param>
    /// <returns>The page ranges as a string, or null if not found or an error occurs.</returns>
    public static string ExtractPageRanges(XmlDocument xmlDoc, string path)
    {
        try
        {
            // Create an XmlNamespaceManager for TEI namespace
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            // Select the <biblScope> node with type="pp" using the namespace prefix
            XmlNode pageRangeNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:biblScope[@type='pp']", nsmgr);
            XmlNode colRangeNode = xmlDoc.SelectSingleNode("/tei:bibl/tei:biblScope[@type='col']", nsmgr);
            XmlNode pageCount = xmlDoc.SelectSingleNode("/tei:bibl/tei:note[@type='pageCount']", nsmgr);

            if (pageRangeNode != null)
            {
                return pageRangeNode.InnerText;
            }else if (pageCount != null)
            {
                return pageCount.InnerText;
            }

            if (colRangeNode != null)
            {
                return colRangeNode.InnerText.Replace("coll. ", "");
            }
            else
            {
                Console.WriteLine($"Page ranges not found in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
        }
        return null;
    }

    public static XmlDocument? LoadDoc(string filePath)
    {
        if (!File.Exists(filePath))
        {
            Console.WriteLine($"Error: File not found at '{filePath}'");
            return null; // Return empty list on error
        }

        XmlDocument xmlDoc = new XmlDocument();
        try
        {
            // Load the XML file.
            xmlDoc.Load(filePath);
        }
        catch (Exception e)
        {
            Console.WriteLine(e);
        }
        
        return xmlDoc;
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
    public static List<string> ExtractReviewBiblioIds(XmlDocument xmlDoc, string path)
    {
        List<string> reviewIds = new List<string>(); // Initialize a list to store the IDs
        try
        {
            XmlNamespaceManager nsmgr = new XmlNamespaceManager(xmlDoc.NameTable);
            nsmgr.AddNamespace("tei", "http://www.tei-c.org/ns/1.0");

            XmlNodeList ptrNodes = xmlDoc.SelectNodes("/tei:bibl/tei:relatedItem[@type='reviews']/tei:bibl/tei:ptr[@target]", nsmgr);

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
                        Console.WriteLine($"Warning: Target attribute is empty or null for a <ptr> element within <relatedItem type=\"reviews\">  in file {path}.");
                    }
                }
            }
            else
            {
                Console.WriteLine($"No <ptr> elements found within <relatedItem type=\"reviews\">/bibl with a 'target' attribute (check namespace or XPath)  in file {path}.");
            }
        }
        catch (XmlException ex)
        {
            Console.WriteLine($"Error parsing XML file '{path}': {ex.Message}");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An unexpected error occurred while processing '{path}': {ex.Message}");
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
                if(reviewFiles.All(x => x.PNFileName != file.PNFileName)) reviewFiles.Add(file);
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