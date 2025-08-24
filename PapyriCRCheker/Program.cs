// See https://aka.ms/new-console-template for more information

using DefaultNamespace;
using OfficeOpenXml;
using PapyriCRCheckerV2;
using PNCheckNewXMLs;

Console.WriteLine("Hello, World!");

ExcelPackage.License.SetNonCommercialPersonal("Thomas");
var logger = new Logger();
var coreUtils = new PapryiCRCheckerCore(logger);
var startingDirectory = Directory.GetCurrentDirectory();
var savePathForReviewsinXMLNotInCR = Path.Combine(startingDirectory, "UpdatesForBP.txt");
    
var directoryFinder = new XMLDirectoryFinder(logger);
var xmlDir = directoryFinder.FindBiblioDirectory(startingDirectory);
var saveXmlDir = Path.Combine(xmlDir, "98");

var biblioFileGatherer = new XMLEntryGatherer(xmlDir, logger);
var biblioFiles = biblioFileGatherer.GatherEntries();
var currentMaxXMLID = biblioFileGatherer.GetHighestXmlEntryValue();


var BasePNReviews = coreUtils.GetFilesWithTypeReview(biblioFiles);
var ParsedPNReviews = coreUtils.ParsePNReviewsAndAttachToRelevantBiblioFile(BasePNReviews, biblioFiles);

var BaseCNReviews = coreUtils.GetFilesWithCNSeg(biblioFiles);
var ParsedCNReviews = coreUtils.ParsedCNReviewsAndAttachToRelevantBiblioFile(BaseCNReviews, biblioFiles);
coreUtils.SaveMatchResultsInSpreadsheet(biblioFiles);

var FilesWithUnequalReviews = coreUtils.GetFilesWithUnequalReviews(biblioFiles);

foreach (var file in FilesWithUnequalReviews)
{
    Console.ForegroundColor = ConsoleColor.Cyan;
    Console.WriteLine($"Processing reviews in: {file.PNFileName}");
    Console.ResetColor();
    var reviewsToSave = file.CompareReviews();
    if (reviewsToSave != null)
    {

        Console.ForegroundColor = ConsoleColor.Yellow;
        Console.WriteLine($"Saving reviews not in CR or PN. CR will be saved to: {savePathForReviewsinXMLNotInCR}, PN: {saveXmlDir}.");
        Console.ResetColor();
        
        foreach (var PN in reviewsToSave.Value.Item1)
        {
            coreUtils.SavePNReviewsToSendToBP(PN, savePathForReviewsinXMLNotInCR);
        }
        
        foreach (var CR in reviewsToSave.Value.Item2)
        {
            
            coreUtils.SaveCrReviewsInXML(CR, ref currentMaxXMLID, saveXmlDir);
        }

    }
}

Console.WriteLine("Finished processing, press any key to exit.");
Console.ReadKey();