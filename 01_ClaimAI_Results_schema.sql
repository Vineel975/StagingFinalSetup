/* ============================================================================
   STAGING — STEP 2: ClaimAI pre-processing results storage
   ----------------------------------------------------------------------------
   Stores the AI pre-processing output for a claim so that when a doctor opens
   the claim, the AI summary is already available (no on-demand processing).

   DESIGN: a SEPARATE side table (ClaimAI_Results), 1 row per claim, NOT extra
   columns on the core Claims table. Rationale:
     - The processed-PDF blob is VARBINARY(MAX) (can be several MB). Putting
       that on the heavily-queried Claims table would bloat every Claims read
       and hurt system-wide performance.
     - A side table isolates the AI data, is easy to index for the scheduler's
       poll query, and is trivial to drop/rebuild during testing.

   The scheduler's work-queue query joins Claims (StageID = 52) to this table
   on ClaimID and filters by ProcessingStatus.

   Run once on the target DB (the same DB that holds the Claims table —
   McarePlus). Idempotent: safe to re-run (guards with IF NOT EXISTS).
   ============================================================================ */

SET NOCOUNT ON;
GO

/* ---------------------------------------------------------------------------
   1. The side table
   --------------------------------------------------------------------------- */
IF NOT EXISTS (SELECT 1 FROM sys.tables WHERE name = 'ClaimAI_Results')
BEGIN
    CREATE TABLE dbo.ClaimAI_Results
    (
        ID                  BIGINT IDENTITY(1,1) NOT NULL
                            CONSTRAINT PK_ClaimAI_Results PRIMARY KEY,

        -- The claim this result belongs to (Claims.ID). One row per claim.
        ClaimID             BIGINT          NOT NULL,
        SlNo                TINYINT         NULL,

        -- Disease type decided by GetClaimDiseaseTypeForStaging:
        -- 'cataract' | 'maternity' | 'other'
        DiseaseType         VARCHAR(20)     NULL,

        -- Lifecycle status the scheduler drives:
        --   NULL          -> not yet picked up (work to do)
        --   'processing'  -> submitted to ClaimAI, awaiting result (lock)
        --   'done'        -> AI result stored successfully
        --   'failed'      -> AI processing failed (claim fails open to stage 5)
        --   'skipped'     -> not a supported disease ('other'); sent to stage 5
        ProcessingStatus    VARCHAR(20)     NULL,

        -- ClaimAI job reference returned when the claim was submitted.
        ClaimAI_JobId       VARCHAR(100)    NULL,

        -- The full AI summary (analysis JSON) — rehydrates the entire
        -- pre-processed summary when the claim is opened.
        AnalysisJson        NVARCHAR(MAX)   NULL,

        -- Benefit plan text/name stored alongside (per requirement).
        BenefitPlan         NVARCHAR(MAX)   NULL,

        -- The merged/processed PDF bytes (stored inline for now; may move to
        -- S3/DMS later with just a path kept here).
        ProcessedPdf        VARBINARY(MAX)  NULL,

        -- How many times we've attempted submission (for retry/visibility).
        AttemptCount        INT             NOT NULL
                            CONSTRAINT DF_ClaimAI_Results_AttemptCount DEFAULT (0),

        -- Last error message if processing failed (diagnostics).
        LastError           NVARCHAR(2000)  NULL,

        -- Timestamps.
        CreatedAt           DATETIME        NOT NULL
                            CONSTRAINT DF_ClaimAI_Results_CreatedAt DEFAULT (GETDATE()),
        SubmittedAt         DATETIME        NULL,   -- when sent to ClaimAI
        ProcessedAt         DATETIME        NULL    -- when result stored / finalised
    );
END
GO

/* ---------------------------------------------------------------------------
   2. One result row per claim — enforce uniqueness on ClaimID + SlNo.
      (A claim can in theory have multiple Slno; we key on both to be safe.)
   --------------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'UX_ClaimAI_Results_ClaimID_SlNo'
      AND object_id = OBJECT_ID('dbo.ClaimAI_Results')
)
BEGIN
    CREATE UNIQUE INDEX UX_ClaimAI_Results_ClaimID_SlNo
        ON dbo.ClaimAI_Results (ClaimID, SlNo);
END
GO

/* ---------------------------------------------------------------------------
   3. Index for the scheduler's poll queries:
        PHASE A: rows needing submission   (ProcessingStatus IS NULL)
        PHASE B: rows awaiting result      (ProcessingStatus = 'processing')
      A standard (non-filtered) index on ProcessingStatus keeps these polls
      fast. We deliberately avoid a FILTERED index here: SQL Server filtered-
      index predicates have restrictions around combining IS NULL with OR, and
      a plain index is perfectly adequate for this table's volume.
   --------------------------------------------------------------------------- */
IF NOT EXISTS (
    SELECT 1 FROM sys.indexes
    WHERE name = 'IX_ClaimAI_Results_Status'
      AND object_id = OBJECT_ID('dbo.ClaimAI_Results')
)
BEGIN
    CREATE INDEX IX_ClaimAI_Results_Status
        ON dbo.ClaimAI_Results (ProcessingStatus, ClaimID);
END
GO

PRINT 'ClaimAI_Results table + indexes ready.';
GO
