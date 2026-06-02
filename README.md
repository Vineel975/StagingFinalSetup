SELECT StageID FROM Claims WHERE ID=<CATARACT_CLAIM>;  -- should be 5
SELECT ProcessingStatus, ClaimAI_JobId, LEN(AnalysisJson), DATALENGTH(ProcessedPdf), LEN(BenefitPlan)
FROM dbo.ClaimAI_Results WHERE ClaimID=<CATARACT_CLAIM>;  -- done, jobId set, json/pdf/plan populated
