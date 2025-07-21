using System.Reflection.Emit;
using DefaultNamespace;

namespace PNCheckNewXMLs;

public class XMLDirectoryFinder
{
    private Logger _logger { get; }

    public XMLDirectoryFinder(Logger logger)
    {
        _logger = logger;
    }

    public string FindXmlDirectory(string startingDir)
    {
        Console.WriteLine("Starting Search for newXMl Directory");
        var xmlDir = FindNewXmlDirectory(startingDir);
        
        _logger.LogProcessingInfo($"Found XMl Dir @: {xmlDir}");
        Console.WriteLine($"Found XMl Dir @: {xmlDir}");
        return xmlDir;
    }
    
    public string FindBiblioDirectory(string startingDir)
    {
        Console.WriteLine($"Trying to find IDP.Data Directory. Starting at: {startingDir}");
        var idpData = FindIDPDataDirectory(startingDir);
        
        var DirsInIDP = Directory.GetDirectories(idpData);
        if (DirsInIDP.Any(x => x.ToLower().Contains("biblio")))
        {
            var biblio = DirsInIDP.First(x => x.ToLower().Contains("biblio"));
            return biblio;
        }
        
        throw new DirectoryNotFoundException("Could not find BPToPNOutput or NewXmlEntries directories.");
    }
    
    
    private string FindNewXmlDirectory(string idp_DataDir, string searchingForName = "BpToPnOutput")
    {
        Console.WriteLine($"Trying to find new XML directory: {idp_DataDir}");
        var DirsInIDP = Directory.GetDirectories(idp_DataDir);
        if (DirsInIDP.Any(x => x.ToLower().Contains(searchingForName.ToLower())))
        {
            var bptopnDir = DirsInIDP.First(x => x.ToLower().Contains(searchingForName.ToLower()));
            var dirsInBPToPN = Directory.GetDirectories(bptopnDir);
            if(dirsInBPToPN.Any(x => x.Contains("NewXmlEntries")))
            {
                var dirPath = dirsInBPToPN.First(x => x.Contains("NewXmlEntries"));
                Console.WriteLine(dirPath);
                return dirPath;
            }
        }
        else
        {
            var fullName = Directory.GetParent(idp_DataDir)?.FullName;
            if (fullName != null)
                return FindNewXmlDirectory(fullName, searchingForName);
            else throw new DirectoryNotFoundException($"Could not find IDP.Data starting from: {idp_DataDir}");
        }
        
        throw new DirectoryNotFoundException("Could not find BPToPNOutput or NewXmlEntries directories.");
    }
    
    public string FindIDPDataDirectory(string startingDirectory, string searchTerm = "idp.data")
    {
        Console.WriteLine($"Trying: {startingDirectory}");
        var dirs = Directory.GetDirectories(startingDirectory);
        if (dirs.Any(x => x.Contains(searchTerm)))
        {
            return dirs.First(x => x.Contains(searchTerm));
        }
        else
        {
            var fullName = Directory.GetParent(startingDirectory)?.FullName;
            if (fullName != null)
                return FindIDPDataDirectory(fullName, searchTerm);
            else throw new DirectoryNotFoundException($"Could not find IDP.Data starting from: {startingDirectory}");
        }
    }
}