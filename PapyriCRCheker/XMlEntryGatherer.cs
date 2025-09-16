using System.Text.RegularExpressions;
using System.Xml;
using OfficeOpenXml.FormulaParsing.FormulaExpressions;

namespace DefaultNamespace;

public class XMLEntryGatherer
{
    private static readonly Dictionary<string, Action<XmlElement, XMLDataEntry>> AttributeSetters = new()
    {
        {"idno:pi", (node, entry) => entry.PNNumber = node.InnerText},
        {"idno:bp", (node, entry) => entry.BPNumber = node.InnerText},
        {"seg:indexBis", (node, entry) => entry.IndexBis = node.InnerText},
        {"seg:index", (node, entry) => entry.Index = node.InnerText},
        {"seg:titre", (node, entry) => entry.Title = node.InnerText},
        {"seg:publication", (node, entry) => entry.Publication = node.InnerText},
        {"seg:cr", (node, entry) => entry.CR = node.InnerText},
        {"seg:nom", (node, entry) => entry.Name = node.InnerText},
        {"seg:resume", (node, entry) => entry.Resume = node.InnerText},
        {
            "seg:internet", (node, entry) => { entry.Internet = node.InnerText; }
        },
        {"seg:sbSeg", (node, entry) => entry.SBandSEG = node.InnerText},
        {"title:level", (node, entry) => entry.TitleLevel = node.GetAttribute("level") },
    };

    public string StartFolder { get; }
    public string EndFolder { get; }
    public XMLEntryGatherer(string path, Logger logger, string startFolderNumber = "1", string endFolderNumber = "98")
    {
        logger.LogProcessingInfo($"Created new XMLEntryGatherer with path: {path}");
        BiblioPath = path;
        StartFolder = startFolderNumber;
        EndFolder = endFolderNumber;
        this.logger = logger;
    }

    public List<XMLDataEntry> Entries { get; protected set; } = new();
    public string BiblioPath { get; set; }
    private Logger logger { get; }

    public string GetHighestXmlEntryValue()
    {
        logger.LogProcessingInfo("Trying to find highest PN in xml values.");
        var entries = Entries.OrderBy(x => x.PNNumber);

        var largestPN = 0;
        foreach (var entry in entries)
        {
            if (Int32.TryParse(entry.PNNumber, out int numb))
            {
                if (numb > largestPN) largestPN = numb;
            }
        }
        
        logger.LogProcessingInfo($"Found {largestPN} as the highest PN in xml values.");
        return Convert.ToString(largestPN);
    }
    
    private XMLDataEntry? GetEntry(string filePath)
    {
        try
        {
            var doc = new XmlDocument();
            doc.Load(filePath);
            var entry = new XMLDataEntry(filePath, logger, doc);

            foreach (var rawNode in doc?.DocumentElement?.ChildNodes)
            {
                if (rawNode.GetType() == typeof(XmlElement))
                {
                    var node = ((XmlElement) rawNode);
                    SetEntryAttributes(node, entry);
                }
                else
                {
                    logger?.LogProcessingInfo($"Found a node that is not an element, moving onto {filePath}");
                    Console.WriteLine($"getting: {filePath}");
                }
            }

            //logger.LogProcessingInfo($"Finished processing entry {entry}");
            return entry;
        }
        catch (Exception e)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"An error has been caught while trying to load file: {filePath}: {e}.");
            logger.LogError($"An error has been caught while trying to load file: {filePath}.", e);
            Console.ResetColor();
        }

        return null;
    }

    private void SetEntryAttributes(XmlElement node, XMLDataEntry entry)
    {
        foreach (var key in AttributeSetters.Keys)
        {
            var parts = key.Split(':');
            if (node.LocalName == parts[0] && node.OuterXml.Contains($"subtype=\"{parts[1]}\""))
            {
                AttributeSetters[key](node, entry);
                break;
            }
            else if (parts[0] == "idno" && node.LocalName == "idno" && node.OuterXml.Contains($"type=\"{parts[1]}\""))
            {
                AttributeSetters[key](node, entry);
                break;
            }

            if (parts[0] == "title" && node.LocalName == "title" &&
                (node.OuterXml.Contains($"level=\"a\"") ||node.OuterXml.Contains($"level=\"m\"")) )
            {
                AttributeSetters[key](node, entry);
                break;
            }
        }
    }

    private List<XMLDataEntry> GetEntriesFromFolder(string folder)
    {
        Console.WriteLine($"Getting entries from folder {folder}");
        logger.Log($"Getting entries from folder {folder}");
        logger.LogProcessingInfo($"Getting entries from folder {folder}");
        var dataEntries = new List<XMLDataEntry>();
        foreach (var file in Directory.GetFiles(folder))
        {
            var entry = GetEntry(file);
            //Console.WriteLine($"Gathered {entry.Title} from file {file}");
            if (entry != null) dataEntries.Add(entry);
        }

        return dataEntries;
    }

    public List<XMLDataEntry> GatherEntries()
    {
        logger.LogProcessingInfo("Gathering entries");
        if (Entries.Count > 0) return Entries;
        
        //logger.LogProcessingInfo("Gathering XMl Entries");
        var entries = new List<XMLDataEntry>();
        try
        {
            foreach (var folder in Directory.GetDirectories(BiblioPath))
            { 
                logger.LogProcessingInfo($"Gathering entries from folder: {folder}");
                int startNumb = Convert.ToInt32(StartFolder);
                int endNumb = Convert.ToInt32(EndFolder);

                int folderNumb = -1;
                var foldNumb = 0;
                    var numberRegex = new Regex(@"\d+");
                    var match = numberRegex.Match(folder);
                if (int.TryParse(match.Value, out folderNumb))
                {
                    if (folderNumb >= startNumb && folderNumb <= endNumb)
                    {
                        foreach (var entry in GetEntriesFromFolder(folder))
                        {
                            //logger.Log($"Adding {entry.Title} from {folder} to entries");
                            //logger.LogProcessingInfo($"\t\tAdding {entry.Title} from {folder} to entries");
                            entries.Add(entry);
                        }
                    }
                    else
                    {
                        Console.WriteLine($"Folder {folder} is outside range {StartFolder}-{EndFolder}");
                        logger.LogProcessingInfo($"Folder {folder} is outside range {StartFolder}-{EndFolder}");
                    }
                }
            }
        }
        catch (Exception e)
        {
            logger.LogError("Error in gather xml entries: ", e);
            Console.WriteLine(e);
        }

        logger.LogProcessingInfo($"Gathered {entries.Count} XML entries for processing.");
        Entries = entries;
        return entries;
    }
}