/**
 * GET /api/staging/result?jobId=<id>
 *   (or)  ?claimId=<id>   — resolves the latest job for that claim
 *
 * STAGING — STEP 3
 *
 * Returns the stored AI pre-processing result for a completed job so the
 * Spectra staging worker can persist it into the ClaimAI_Results table and
 * flip the claim to "Ready for Adjudication".
 *
 * This is the *retrieval* half of staging. The *processing* half reuses the
 * existing POST /api/audit/start endpoint unchanged — that endpoint already
 * processes a claim synchronously (it completes before returning a jobId), so
 * the staging worker:
 *    1. POST /api/audit/start  (docs)  -> processes, returns { jobId }
 *    2. GET  /api/staging/result?jobId -> pulls the stored result to save
 *
 * Response (success):
 *   {
 *     success: true,
 *     jobId, claimId,
 *     status: "completed" | "error" | "processing",
 *     analysis: <full analysis JSON | null>,
 *     benefitPlan: <string | null>,
 *     processedPdfUrl: <string | null>   // Convex storage URL for the bill PDF
 *   }
 *
 * Response (error):
 *   { success: false, error: string }
 *
 * CORS-enabled — Spectra calls this server-to-server (and may call from browser
 * during testing).
 */

import { NextRequest, NextResponse } from "next/server";
import { ConvexHttpClient } from "convex/browser";
import { api } from "@/convex/_generated/api";
import type { Id } from "@/convex/_generated/dataModel";
import { getBenefitPlanTextByClaimId } from "@/lib/db";

const CORS_HEADERS = {
  "Access-Control-Allow-Origin": "*",
  "Access-Control-Allow-Methods": "GET, OPTIONS",
  "Access-Control-Allow-Headers": "Content-Type",
} as const;

export const dynamic = "force-dynamic";
export const maxDuration = 60;

// Same Convex URL resolution approach as audit/start.
const RAW_INTERNAL = process.env.CONVEX_SELF_HOSTED_URL ?? "";
const RAW_PUBLIC = process.env.CONVEX_URL_PUBLIC ?? process.env.NEXT_PUBLIC_CONVEX_URL ?? "";
const IS_DOCKER_HOSTNAME =
  RAW_INTERNAL.startsWith("http://backend") ||
  RAW_INTERNAL.startsWith("http://convex") ||
  RAW_INTERNAL.includes(":3210");
const IN_DOCKER =
  process.env.DOCKER_ENV === "0" || process.env.IN_DOCKER === "false"
    ? false
    : process.env.DOCKER_ENV === "1" ||
      process.env.IN_DOCKER === "true" ||
      IS_DOCKER_HOSTNAME;
const CONVEX_URL = IN_DOCKER && RAW_INTERNAL ? RAW_INTERNAL : RAW_PUBLIC;

function json(body: Record<string, unknown>, status = 200) {
  return NextResponse.json(body, { status, headers: CORS_HEADERS });
}

export async function GET(request: NextRequest) {
  const { searchParams } = new URL(request.url);
  const jobIdParam = searchParams.get("jobId")?.trim();
  const claimIdParam = searchParams.get("claimId")?.trim();

  if (!jobIdParam && !claimIdParam) {
    return json({ success: false, error: "jobId or claimId is required." }, 400);
  }
  if (!CONVEX_URL) {
    return json({ success: false, error: "Convex URL not configured." }, 500);
  }

  const convex = new ConvexHttpClient(CONVEX_URL);

  try {
    // ── Resolve the job ──────────────────────────────────────────────────────
    let jobId: Id<"processJob"> | null = jobIdParam
      ? (jobIdParam as Id<"processJob">)
      : null;
    let claimId = claimIdParam ?? "";

    if (!jobId && claimId) {
      // Resolve the most recent job for this claim.
      const job = await convex.query(api.processing.getLatestJobByClaimId, {
        claimId,
      });
      if (!job) {
        return json(
          { success: false, error: `No job found for claimId ${claimId}.` },
          404,
        );
      }
      jobId = job._id as Id<"processJob">;
      claimId = job.claimId ?? claimId;
    }

    if (!jobId) {
      return json({ success: false, error: "Could not resolve jobId." }, 404);
    }

    // ── Job status ───────────────────────────────────────────────────────────
    const job = await convex.query(api.processing.getJobById, { jobId });
    if (!job) {
      return json({ success: false, error: `Job ${jobId} not found.` }, 404);
    }
    if (!claimId) claimId = job.claimId ?? "";
    const status = job.status ?? "unknown";

    // If not finished yet, report status without results (worker will retry).
    if (status !== "completed" && status !== "error") {
      return json({
        success: true,
        jobId,
        claimId,
        status,
        analysis: null,
        benefitPlan: null,
        processedPdfUrl: null,
      });
    }

    // ── Analysis JSON ──────────────────────────────────────────────────────────
    let analysis: unknown = null;
    try {
      const results = await convex.query(api.processing.getJobResults, { jobId });
      if (results && results.length > 0) {
        analysis = results[results.length - 1].analysis ?? null;
      }
    } catch (e) {
      console.warn("[staging/result] getJobResults failed:", e);
    }

    // ── Benefit plan text ──────────────────────────────────────────────────────
    let benefitPlan: string | null = null;
    if (claimId) {
      try {
        benefitPlan = await getBenefitPlanTextByClaimId(claimId);
      } catch (e) {
        console.warn("[staging/result] benefit plan fetch failed:", e);
      }
    }

    // ── Processed PDF URL ──────────────────────────────────────────────────────
    // The merged/uploaded bill PDF lives in Convex storage; return a URL the
    // worker can fetch bytes from. (Prefer the hospital bill file.)
    let processedPdfUrl: string | null = null;
    try {
      const files = await convex.query(api.processing.getJobFilesByJobId, {
        jobId,
      });
      const billFile =
        files.find((f) => (f.fileType ?? "").toLowerCase().includes("hospital")) ||
        files.find((f) => (f.fileName ?? "").toLowerCase().includes("medical")) ||
        files[0];
      if (billFile?.storageId) {
        processedPdfUrl = await convex.query(api.processing.getPdfUrl, {
          storageId: billFile.storageId as Id<"_storage">,
        });
      }
    } catch (e) {
      console.warn("[staging/result] processed PDF URL fetch failed:", e);
    }

    return json({
      success: true,
      jobId,
      claimId,
      status,
      analysis,
      benefitPlan,
      processedPdfUrl,
    });
  } catch (err) {
    console.error("[staging/result] error:", err);
    return json(
      {
        success: false,
        error: err instanceof Error ? err.message : String(err),
      },
      500,
    );
  }
}

export async function OPTIONS() {
  return new NextResponse(null, { status: 204, headers: CORS_HEADERS });
}
