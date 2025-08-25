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
    
    public string FindBiblioDirectory(string startingDir)
    {
        Console.WriteLine($"Trying to find IDP.Data Directory. Starting at: {startingDir}");
        _logger.LogProcessingInfo($"Trying to find IDP.Data Directory. Starting at: {startingDir}");
        var idpData = FindIDPDataDirectory(startingDir);
        
        var DirsInIDP = Directory.GetDirectories(idpData);
        if (DirsInIDP.Any(x => x.ToLower().Contains("biblio")))
        {
            var biblio = DirsInIDP.First(x => x.ToLower().Contains("biblio"));
            return biblio;
        }
        
        throw new DirectoryNotFoundException("Could not find BPToPNOutput or NewXmlEntries directories.");
    }
    
    
    public string FindIDPDataDirectory(string startingDirectory, string searchTerm = "idp.data")
    {
        Console.WriteLine($"Trying: {startingDirectory}");
        _logger.LogProcessingInfo($"Trying: {startingDirectory}");
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