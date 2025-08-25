﻿// See https://aka.ms/new-console-template for more information

using System;
using System.IO;
using BPtoPNDataCompiler;
using DefaultNamespace;
using OfficeOpenXml;
using PapyriCRCheckerV2;
using PNCheckNewXMLs;

Console.WriteLine("Hello, World!");

ExcelPackage.License.SetNonCommercialPersonal("Thomas");
var logger = new Logger("PapyriCRReviewCheckerLogs");
var coreUtils = new PapryiCRCheckerCore(logger);
var startingDirectory = Directory.GetCurrentDirectory();
var savePathForReviewsinXMLNotInCR = Path.Combine(startingDirectory, "UpdatesForBP.txt");
logger.Log($"Program started in: {startingDirectory}, will be saving Updates for BP to: {savePathForReviewsinXMLNotInCR}.");    


var directoryFinder = new XMLDirectoryFinder(logger);
var xmlDir = directoryFinder.FindBiblioDirectory(startingDirectory);
var saveXmlDir = Path.Combine(xmlDir, "98");
logger.Log($"Found Directory with XML files: {xmlDir}.\nWill be saving new XML files to: {saveXmlDir}");

var biblioFileGatherer = new XMLEntryGatherer(xmlDir, logger);
var biblioFiles = biblioFileGatherer.GatherEntries();
var currentMaxXMLID = biblioFileGatherer.GetHighestXmlEntryValue();
logger.Log($"Gathered {biblioFiles.Count} xml files in the biblio. The current max ID was found to be {currentMaxXMLID}.");

var BasePNReviews = coreUtils.GetFilesWithTypeReview(biblioFiles);
logger.Log($"Gathered {BasePNReviews.Count} files with type review.");

var ParsedPNReviews = coreUtils.ParsePNReviewsAndAttachToRelevantBiblioFile(BasePNReviews, biblioFiles);
logger.Log($"Parsed review files into {ParsedPNReviews.Count} discrete reviews.");

var BaseCNReviews = coreUtils.GetFilesWithCNSeg(biblioFiles);
logger.Log($"Gathered {BaseCNReviews.Count} files with CR segments.");

var ParsedCNReviews = coreUtils.ParsedCNReviewsAndAttachToRelevantBiblioFile(BaseCNReviews);
logger.Log($"Parsed files with CR seg into {ParsedCNReviews.Count} CR reviews, of which {CRReviewData.NOJOURNALMATCH} had no journal match..");

coreUtils.SaveMatchResultsInSpreadsheet(biblioFiles);

var FilesWithUnequalReviews = coreUtils.GetFilesWithUnequalReviews(biblioFiles);
Console.ForegroundColor = ConsoleColor.Magenta;
Console.WriteLine($"There are {FilesWithUnequalReviews.Count} files with an unequal # of reviews to be processed. ({CRReviewData.NOJOURNALMATCH} had no journal match.)");
logger.Log($"There are {FilesWithUnequalReviews.Count} files with an unequal # of reviews to be processed.");
Console.ResetColor();

coreUtils.SaveFilesWithUnequalReviews(FilesWithUnequalReviews, saveXmlDir, savePathForReviewsinXMLNotInCR, currentMaxXMLID);
logger.Log("Finished saving files with unequal reviews.");

logger.Log("Finished processing.");
Console.WriteLine("Finished processing, press any key to exit.");
Console.ReadKey();