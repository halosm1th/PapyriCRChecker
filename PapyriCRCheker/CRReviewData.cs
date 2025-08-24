using System.Text.RegularExpressions;
using DefaultNamespace;

namespace BPtoPNDataCompiler;

public class CRReviewData
{
    public CRReviewData(XMLDataEntry originalEntry, string pageRange, string year, string cr, string idNumber, string startingPathToCsVv,
        string articleReviewing, string articleNumber, Logger _logger)
    {
        Source = originalEntry;
        logger = _logger;
        //Thomas Schmidt, MusHelv 68 (2011) pp. 232-233.
        //Lajos Berkes, Gnomon 85 (2013) pp. 464-466.
        IDNumber = idNumber;
        startingPath = startingPathToCsVv;
        CRData = cr;

        var nameParts = new string[0];
        var name = cr.Split(",")[0];
        var lastName = "";
        if(name.Contains("."))
        {
            nameParts = name.Split(".");
            nameParts[0] += ".";
            
            Forename = nameParts[0];
            name = name.Replace(Forename, "");
            nameParts = name.Split(" ");
            
            for (int i = 1; i < nameParts.Length; i++)
            {
                lastName += nameParts[i] + " ";
            }
        }
        else
        {
            nameParts = name.Split(" ");
            Forename = nameParts[0];
            for (int i = 1; i < nameParts.Length; i++)
            {
                lastName += nameParts[i] + " ";
            }
            
        }

        if (lastName.Length > 0) Lastname = lastName.Trim();
        else Lastname = "ERROR WITH LAST NAME";
        Date = year;
        var pages = new string[0];

        if (pageRange.Contains("-"))
        {
            pages= pageRange.Split("-");
            PageStart = pages[0];
            if (pages.Length > 1)
                PageEnd = pages[1];
            else
            {
                Console.WriteLine(CRData);
            }
        }else if (pageRange.Contains("p. "))
        {
            pages = pageRange.Split("p.");
            PageStart = pages[1];
            if (pages.Length > 1)
                PageEnd = pages[2];
            else
            {
                PageEnd = "";
            }
        }

        var issueRegex = new Regex(@" \d+ ");
        var issueMatch = issueRegex.Match(cr);
        Issue = issueMatch.Value;


        var prePageRange = cr.Split(pageRange)[0];
        var journal = prePageRange.Split(",")[^1];
        journal = journal.Split(year)[0].Replace("(", "");
        if(!string.IsNullOrEmpty(Issue))  journal = journal.Replace(Issue, "").Trim();
        if(journal.Contains(":")) journal = journal.Replace(":", "");
        
        JournalID = GetJournalID(journal);
            

        if (journal == "-1")
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"The reviews for {articleReviewing} may need to be created manually");
            Console.ResetColor();
            logger.Log($"The reviews for {articleReviewing} may need to be created manually");
        }

        AppearsInID = articleNumber;
        AppearsInText =   articleReviewing;
    }

    private Logger logger { get; }

    private string startingPath { get; set; }
    
    public XMLDataEntry Source { get; }
    public string IDNumber { get; set; } = "[NONE]";
    public string Forename { get; } = "[NONE]";
    public string Lastname { get; } = "[NONE]";
    public string Issue { get; } = "[NONE]";
    public string JournalID { get; } = "[NONE]";
    public string AppearsInText { get; } = "[NONE]";
    public string AppearsInID { get; } = "[NONE]";
    public string CRData { get; } = "[NONE]";
    public string Date { get; } = "[NONE]";
    public string PageStart { get; } = "[NONE]";
    public string PageEnd { get; } = "[NONE]";
    
    public string PageRange  => $"{PageStart.Replace("pp. ", "").Replace(" ", "")}-{PageEnd}";
    
    private static Dictionary<string, string> _journals { get; set; }= null;

    private Dictionary<string, string> GetJounrals()
    {
        if (_journals == null || _journals.Count == 0)
        {
            var file = File.ReadAllLines(startingPath + "/PN_Journal_IDs.csv");
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
        try
        {
            if (journal.Contains("-") || journal.Contains("p.") || string.IsNullOrEmpty(journal))
                Console.WriteLine($"There was an error parsing the journal for: {CRData}");
            else
            {
                
                if(journal == "") 
                    Console.WriteLine("test");
                
                var listOFJournals = GetJounrals();

                try
                {
                    if (journal != " " && !string.IsNullOrEmpty(journal))
                    {
                        journal = journal.Replace("&amp;", "&");
                            var id = listOFJournals[journal];

                            return id;
                    }
                    else
                    {
                        Console.WriteLine("No journal name found?");
                        return "-1";
                    }
                }
                catch (Exception e)
                {
                    Console.ForegroundColor = ConsoleColor.Cyan;
                    Console.WriteLine($"had an error parsing journal: {journal}: {e}");
                    Console.ResetColor();
                    logger?.LogError($"had an error parsing journal: {journal}", e);
                    return "-1";
                }
                
            }

        return "-1";
    }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"Could not find a journal to match {journal}. Threw error: {e}");
            logger?.LogProcessingInfo($"Could not find a journal to match {journal}. Threw error: {e}");
            Console.ResetColor();
            return "-1";
        }

        throw new NotImplementedException();
    }

    public override string ToString()
    {
        return $"""
                <?xml version="1.0" encoding="UTF-8"?>
                <bibl xmlns="http://www.tei-c.org/ns/1.0" xml:id="b{IDNumber}" type="review">
                  <author>
                      <forename>{Forename}</forename>
                      <surname>{Lastname}</surname>
                   </author>
                   <date>{Date}</date>
                  <biblScope type="pp" from="{PageStart}" to="{PageEnd}">{PageStart}-{PageEnd}</biblScope>
                  <relatedItem type="appearsIn">
                      <bibl>
                         <ptr target="https://papyri.info/biblio/{JournalID}"/>
                         <!--ignore - start, i.e. SoSOL users may not edit this-->
                         <!--ignore - stop-->
                      </bibl>
                  </relatedItem>
                  <biblScope type="issue">{Issue}</biblScope>
                  <relatedItem type="reviews" n="1">
                      <bibl>
                         <ptr target="https://papyri.info/biblio/{AppearsInID}"/>
                         <!--ignore - start, i.e. SoSOL users may not edit this-->
                         <!--ignore - stop-->
                      </bibl>
                  </relatedItem>
                  <idno type="pi">{IDNumber}</idno>
                  <seg type="original" subtype="cr" resp="#BP">{CRData}</seg>
                </bibl>
                """;
    }
}