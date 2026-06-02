/* ============================================================================
   STAGING — TESTING HELPERS
   ----------------------------------------------------------------------------
   Small SQL snippets for testing the staging pipeline locally / in QA WITHOUT
   the scheduler. Workflow:
     1. Put a claim into the work queue (set StageID = 52, clear any prior result)
     2. Trigger one cycle by hitting the endpoint in a browser:
          https://<host>/MedicalScrutiny/ProcessStagingClaims
     3. Inspect the result row
     4. To re-test the same claim, reset it again

   Replace <CLAIM_ID> with the Claims.ID you are testing.
   ============================================================================ */

/* ---- 1. PUT A CLAIM INTO THE WORK QUEUE --------------------------------- */
-- Hold the claim at stage 52 so the scheduler will pick it up,
-- and clear any existing ClaimAI result so it's treated as fresh.
DECLARE @ClaimID BIGINT = <CLAIM_ID>;

UPDATE Claims SET StageID = 52 WHERE ID = @ClaimID;

DELETE FROM dbo.ClaimAI_Results WHERE ClaimID = @ClaimID;
-- (deleting the result row = "not yet processed"; next cycle re-picks it)


/* ---- 2. INSPECT WHAT HAPPENED ------------------------------------------- */
SELECT  c.ID            AS ClaimID,
        c.StageID,
        r.DiseaseType,
        r.ProcessingStatus,
        r.ClaimAI_JobId,
        r.AttemptCount,
        r.LastError,
        DATALENGTH(r.ProcessedPdf)  AS ProcessedPdfBytes,
        LEN(r.AnalysisJson)         AS AnalysisJsonLen,
        LEN(r.BenefitPlan)          AS BenefitPlanLen,
        r.CreatedAt, r.SubmittedAt, r.ProcessedAt
FROM        Claims c WITH (NOLOCK)
LEFT JOIN   dbo.ClaimAI_Results r WITH (NOLOCK) ON r.ClaimID = c.ID
WHERE       c.ID = <CLAIM_ID>;


/* ---- 3. SEE THE CURRENT WORK QUEUE (what the scheduler will pick up) ----- */
-- PHASE A candidates: stage 52, no result row yet (or status NULL)
SELECT  c.ID AS ClaimID, c.StageID, r.ProcessingStatus
FROM        Claims c WITH (NOLOCK)
LEFT JOIN   dbo.ClaimAI_Results r WITH (NOLOCK) ON r.ClaimID = c.ID
WHERE       c.StageID = 52
  AND       (r.ID IS NULL OR r.ProcessingStatus IS NULL)
ORDER BY    c.ID DESC;

-- PHASE B candidates: submitted, awaiting result
SELECT  ClaimID, ProcessingStatus, ClaimAI_JobId, SubmittedAt
FROM    dbo.ClaimAI_Results WITH (NOLOCK)
WHERE   ProcessingStatus = 'processing'
ORDER BY SubmittedAt;


/* ---- 4. UNSTICK A CLAIM (if a 'processing' row got orphaned in testing) -- */
-- e.g. server restarted mid-cycle and left a claim locked as 'processing'.
-- Reset it so the next cycle re-submits.
-- DELETE FROM dbo.ClaimAI_Results WHERE ClaimID = <CLAIM_ID>;
-- UPDATE Claims SET StageID = 52 WHERE ID = <CLAIM_ID>;
