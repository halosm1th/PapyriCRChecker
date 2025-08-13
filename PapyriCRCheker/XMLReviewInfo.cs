﻿public class XMLReviewInfo
{
    public XMLReviewInfo(string reviewer, string reviewedWhere, string reviewDate, string reviewPages, string itemPtr, string reviewPath)
    {
        Reviewer = reviewer;
        ReviewedWhere = reviewedWhere;
        ReviewDate = reviewDate;
        ReviewPages = reviewPages;
        ItemPtr = itemPtr;
        ReviewPath = reviewPath;
    }
    
    public string ReviewPath { get; set; }
    
    public string BPNumber { get; set; }
    public string Reviewer { get; set; }
    public string ReviewedWhere { get; set; }
    public string ReviewDate { get; set; }
    public string ReviewPages { get; set; }
    public string ItemPtr { get; set; }
}