using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using DefaultNamespace;

namespace BPtoPNDataCompiler;

public class CRReviewData
{
    
    private Logger logger { get; }
    
    public XMLDataEntry Source { get; }
    public string IDNumber { get; set; } = "[NONE]";
    
    //TODO Make it so that the name gets split, and name becomes => $"{Forename} {Lastname}";
    //This means figuring out how to split the name
    public string Name { get; } = "[NONE]";
    public string Forename { get; } = "[NONE]";
    public string Lastname { get; } = "[NONE]";
    
    
    public string Issue { get; } = "[NONE]";
    public string ArticleNumberCrIsReviewing { get; } = "[NONE]";
    public string JournalID { get; } = "[NONE]";
    public string JournalName { get; } = "[NONE]";
    public string CRData { get; } = "[NONE]";
    public string Date { get; } = "[NONE]";
    public string PageStart { get; } = "[NONE]";
    public string PageEnd { get; } = "[NONE]";
    public string internetLink { get; } = "[NONE]";
    
    public string PageRange  => $"{PageStart.Replace("pp. ", "").Replace(" ", "")}-{PageEnd}";
    
    private static Dictionary<string, string> _journals { get; set; }

    public CRReviewData(XMLDataEntry sourceOfCREntry, string name, string pageRange, string year, string link,
        string journalName, string journalNumber, string articleNumberCRReviewing, string baseText, Logger _logger)
    {
        Source = sourceOfCREntry;
        logger = _logger;
        Issue = journalNumber;
        Date = year;
        ArticleNumberCrIsReviewing = articleNumberCRReviewing;
        JournalName = journalName;
        JournalID = GetJournalID(journalName);
        Name = name;
        CRData = baseText;
        
        var amperMatch = Regex.Match(CRData, ".&.");
        if (amperMatch.Success && !CRData.Contains("&amp;"))
        {
            CRData = CRData.Replace("&","&amp;");
        }

        var pages = new string[0];

        if (pageRange.Contains("-"))
        {
            pages = pageRange.Split("-");
            PageStart = pages[0];
            if (pages.Length > 1)
                PageEnd = pages[1];
            else
            {
                Console.WriteLine(CRData);
            }
        }
        else
        {
            pages = pageRange.Split(" ");
            PageStart = pages[0];
            
            var numberMatch = Regex.Match(PageStart, @"\d+");
            if (!numberMatch.Success) PageStart = "ERROR PARSING NUMBER";
                
            if (pages.Length > 1)
                PageEnd = pages[1];
            else
            {
                PageEnd = "[NONE]";
            }
        }

        if (!string.IsNullOrEmpty(link))
        {
            internetLink = link;
        }
        else if (baseText.Contains("http://") || baseText.Contains("https://") || baseText.Contains("BMCR"))
        {
            var urlMatch = Regex.Match(baseText, @"\s*(https?://[^\s>]+)\s*");
            if (urlMatch.Success) internetLink = urlMatch.Groups[1].Value.Trim();
        }
    }

    private Dictionary<string, string> GetJounrals()
    {
        if (_journals == null || _journals.Count == 0)
        {
            var file = File.ReadAllLines(Directory.GetCurrentDirectory() + "/PN_Journal_IDs.csv");
            var listOFJournals = new Dictionary<string, string>();
            foreach (var line in file)
            {
                var text = line.Split(',');
                if (!listOFJournals.ContainsKey(text[0])) listOFJournals.Add(text[0], text[1]);
            }
            
            _journals = listOFJournals;
        }
        
        return _journals;
    }

    private string GetJournalID(string journal)
    {
        logger.LogProcessingInfo($"Getting journal ID for: {journal} in {CRData}");
        try
        {
            if (journal.Contains("-") || journal.Contains("p.") || string.IsNullOrEmpty(journal))
            {
                Console.WriteLine($"There was an error parsing the journal for: {CRData}");
                logger?.LogProcessingInfo($"\tThere was an error parsing the journal for: {CRData}");
            }
            else
            {

                if (journal == "")
                {
                    Console.WriteLine("no journal given");
                    logger?.LogProcessingInfo("\tno journal given");
                }

                var listOFJournals = GetJounrals();

                try
                {
                    if (journal != " " && !string.IsNullOrEmpty(journal))
                    {
                        if (journal.Contains(","))
                        {
                            journal = journal.Replace(",", "");
                        }
                        
                        var id = "";
                        if (journal.Contains("N.S."))
                        {

                            var shortName = journal.Replace(" N.S.", "").Trim();
                            if(listOFJournals.Any(x => x.Key == journal))
                                id = listOFJournals[journal];
                            else if (listOFJournals.Any(x => x.Key == shortName))
                                id = listOFJournals[shortName];
                        }
                        else
                            id = listOFJournals[journal];

                        
                        logger.LogProcessingInfo($"\tFound ID {id}.");
                        return id;
                    }
                    else
                    {
                        Console.WriteLine("No journal name found?");
                        logger?.LogProcessingInfo("\tNo journal name found?");
            NOJOURNALMATCH++;
                        return "-1";
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"had an error parsing journal: {journal}: {e}");
                    Console.ResetColor();
                    logger?.LogError($"had an error parsing journal: {journal}", e);
            NOJOURNALMATCH++;
                    return "-1";
                }
                
            }

            NOJOURNALMATCH++;
        return "-1";
    }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not find a journal to match {journal}. Threw error: {e}");
            logger?.LogProcessingInfo($"\tCould not find a journal to match {journal}. Threw error: {e}");
            Console.ResetColor();
            return "-1";
        }

        throw new NotImplementedException();
    }

    public static int NOJOURNALMATCH = 0;
    
    public override string ToString()
    {
        var sb = new StringBuilder();
        sb.Append($"""
                   <?xml version="1.0" encoding="UTF-8"?>
                   <bibl xmlns="http://www.tei-c.org/ns/1.0" xml:id="b{IDNumber}" type="review">
                   <author>{Name}</author>
                   <date>{Date}</date>
                   """);
        sb.Append("\n");   
        sb.Append(PageEnd != "[NONE]"? $"""
         <biblScope type="pp" from="{PageStart}" to="{PageEnd}">{PageStart}-{PageEnd}</biblScope>
         """ :$"""
               <biblScope type="pp">{PageStart}</biblScope>
               """ );

        sb.Append($"""
                   
                        <relatedItem type="appearsIn">
                     <bibl>
                        <ptr target="https://papyri.info/biblio/{JournalID}"/>
                        <!--ignore - start, i.e. SoSOL users may not edit this-->
                        <!--ignore - stop-->
                     </bibl>
                   </relatedItem>
                   <biblScope type="issue">{Issue.Trim()}</biblScope>
                   <relatedItem type="reviews" n="1">
                     <bibl>
                        <ptr target="https://papyri.info/biblio/{ArticleNumberCrIsReviewing}"/>
                        <!--ignore - start, i.e. SoSOL users may not edit this-->
                        <!--ignore - stop-->
                     </bibl>
                   </relatedItem>
                   <idno type="pi">{IDNumber}</idno>
                   <seg type="original" subtype="cr" resp="#BP">{CRData.Trim()}</seg>
                   """);
        sb.Append("\n");        
        
                sb.Append(internetLink != "[NONE]" ? 
                    $"""
                     <ptr target="{internetLink}"/>
                     </bibl>
                     """ : $"""
                </bibl>
                """
                );

            return sb.ToString();
    }
}