ProcessStagingClaims():
  connStr = <McarePlus>
  claims = SELECT Claims at StageID=52 with no done/processing ClaimAI_Results row
  for each claim (capped batch, e.g. 10 per run):
    1. disease = GetClaimDiseaseTypeForStaging(claimId)
    2. if disease == "other":
         → upsert ClaimAI_Results status='skipped'
         → UPDATE Claims SET StageID=5
         → continue
    3. mark ClaimAI_Results status='processing' (lock)
    4. med = GetMedicalBillDocument(claimId, slNo)  → base64   (env-based fetch reused)
       tar = GetTariffDocument(claimId)             → base64   (optional)
    5. POST to ClaimAI /api/audit/start  (reuse BuildMultipartBody + HttpClient)
       → get jobId   (processing already done — audit/start is synchronous)
    6. GET /api/staging/result?jobId=...  → analysis, benefitPlan, processedPdfUrl
    7. fetch PDF bytes from processedPdfUrl
    8. UPDATE ClaimAI_Results: AnalysisJson, BenefitPlan, ProcessedPdf, status='done', jobId
       UPDATE Claims SET StageID=5
    on any failure:
       → ClaimAI_Results status='failed', LastError=...
       → UPDATE Claims SET StageID=5   (fail-open, step 7 of your plan)
  return summary JSON (counts: processed/skipped/failed)
