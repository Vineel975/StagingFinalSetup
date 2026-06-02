import { query, mutation } from "./_generated/server";
import { v } from "convex/values";

const formatLogMessage = (message: string) => {
  const timestamp = new Date().toLocaleString("en-US", {
    year: "numeric",
    month: "numeric",
    day: "numeric",
    hour: "numeric",
    minute: "2-digit",
    second: "2-digit",
    hour12: true,
  });
  return `${timestamp} [LOG] '${message}'`;
};

export const getCurrentState = query({
  args: {},
  handler: async (ctx) => {
    const job = await ctx.db.query("processJob").order("desc").first();

    if (!job) {
      return null;
    }

    const jobFiles = await ctx.db
      .query("jobFiles")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .collect();

    const jobResults = await ctx.db
      .query("jobResults")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .collect();

    const jobLogs = await ctx.db
      .query("jobLogs")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .order("asc")
      .collect();

    return {
      _id: job._id,
      status: job.status,
      completed: job.completed,
      total: job.total,
      successCount: job.successCount,
      errorCount: job.errorCount,
      totalCost: job.totalCost,
      totalTokens: job.totalTokens,
      totalPromptTokens: job.totalPromptTokens,
      totalCompletionTokens: job.totalCompletionTokens,
      isComplete: job.isComplete,
      error: job.error,
      claimId: job.claimId,
      spectraFields: job.spectraFields,
      files: jobFiles.map((f) => ({
        file: f.file,
        status: f.status,
        cost: f.cost,
        tokens: f.tokens,
        timeMs: f.timeMs,
        statusMessage: f.statusMessage,
        storageId: f.storageId,
        fileName: f.fileName,
        fileType: f.fileType,
      })),
      results: jobResults.map((r) => ({
        filePath: r.filePath,
        analysis: r.analysis,
        cost: r.cost,
        usage: r.usage,
        processingTimeMs: r.processingTimeMs,
        processingTime: r.processingTime,
        fallbackAnalysis: r.fallbackAnalysis,
        fallbackCost: r.fallbackCost,
        fallbackUsage: r.fallbackUsage,
        changelogEntries: r.changelogEntries,
      })),
      logs: jobLogs.map((l) => ({
        message: l.message,
        timestamp: l.timestamp,
      })),
    };
  },
});

export const getJobById = query({
  args: {
    jobId: v.id("processJob"),
  },
  handler: async (ctx, args) => {
    const job = await ctx.db.get(args.jobId);

    if (!job) {
      return null;
    }

    const jobFiles = await ctx.db
      .query("jobFiles")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .collect();

    const jobResults = await ctx.db
      .query("jobResults")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .collect();

    const jobLogs = await ctx.db
      .query("jobLogs")
      .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
      .order("asc")
      .collect();

    return {
      _id: job._id,
      status: job.status,
      completed: job.completed,
      total: job.total,
      successCount: job.successCount,
      errorCount: job.errorCount,
      totalCost: job.totalCost,
      totalTokens: job.totalTokens,
      totalPromptTokens: job.totalPromptTokens,
      totalCompletionTokens: job.totalCompletionTokens,
      isComplete: job.isComplete,
      error: job.error,
      claimId: job.claimId,
      spectraFields: job.spectraFields,
      files: jobFiles.map((f) => ({
        file: f.file,
        status: f.status,
        cost: f.cost,
        tokens: f.tokens,
        timeMs: f.timeMs,
        statusMessage: f.statusMessage,
        storageId: f.storageId,
        fileName: f.fileName,
        fileType: f.fileType,
      })),
      results: jobResults.map((r) => ({
        filePath: r.filePath,
        analysis: r.analysis,
        cost: r.cost,
        usage: r.usage,
        processingTimeMs: r.processingTimeMs,
        processingTime: r.processingTime,
        fallbackAnalysis: r.fallbackAnalysis,
        fallbackCost: r.fallbackCost,
        fallbackUsage: r.fallbackUsage,
        changelogEntries: r.changelogEntries,
      })),
      logs: jobLogs.map((l) => ({
        message: l.message,
        timestamp: l.timestamp,
      })),
    };
  },
});

export const getPdfUrl = query({
  args: {
    storageId: v.id("_storage"),
  },
  handler: async (ctx, args) => {
    return await ctx.storage.getUrl(args.storageId);
  },
});

export const updateResult = mutation({
  args: {
    filePath: v.string(),
    analysis: v.any(),
    changelogEntries: v.optional(
      v.array(
        v.object({
          id: v.string(),
          timestamp: v.string(),
          tab: v.string(),
          record: v.string(),
          field: v.string(),
          previousValue: v.string(),
          newValue: v.string(),
        })
      )
    ),
  },
  handler: async (ctx, args) => {
    const job = await ctx.db.query("processJob").order("desc").first();

    if (!job) {
      throw new Error("No processing job found");
    }

    const existingResult = await ctx.db
      .query("jobResults")
      .withIndex("by_filePath")
      .collect();

    const result = existingResult.find((r) => r.filePath === args.filePath);

    if (result) {
      await ctx.db.patch(result._id, {
        analysis: args.analysis,
        changelogEntries: args.changelogEntries,
      });

      return await ctx.db.get(result._id);
    } else {
      return await ctx.db.insert("jobResults", {
        jobId: job._id,
        filePath: args.filePath,
        analysis: args.analysis,
        usage: {
          inputTokens: 0,
          outputTokens: 0,
          totalTokens: 0,
        },
        processingTimeMs: 0,
        processingTime: "0ms",
        changelogEntries: args.changelogEntries,
      });
    }
  },
});

export const addLog = mutation({
  args: {
    jobId: v.id("processJob"),
    message: v.string(),
  },
  handler: async (ctx, args) => {
    await ctx.db.insert("jobLogs", {
      jobId: args.jobId,
      message: args.message,
      timestamp: new Date().toISOString(),
    });

    return { success: true };
  },
});

export const resetState = mutation({
  args: {},
  handler: async (ctx) => {
    const job = await ctx.db.query("processJob").order("desc").first();

    if (job) {
      const files = await ctx.db
        .query("jobFiles")
        .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
        .collect();

      for (const file of files) {
        await ctx.db.delete(file._id);
      }

      const results = await ctx.db
        .query("jobResults")
        .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
        .collect();

      for (const result of results) {
        await ctx.db.delete(result._id);
      }

      const logs = await ctx.db
        .query("jobLogs")
        .withIndex("by_jobId", (q) => q.eq("jobId", job._id))
        .collect();

      for (const log of logs) {
        await ctx.db.delete(log._id);
      }

      await ctx.db.delete(job._id);
    }

    const newJobId = await ctx.db.insert("processJob", {
      status: "idle",
      completed: 0,
      total: 0,
      successCount: 0,
      errorCount: 0,
      totalCost: 0,
      totalTokens: 0,
      totalPromptTokens: 0,
      totalCompletionTokens: 0,
      isComplete: false,
    });

    await ctx.db.insert("jobLogs", {
      jobId: newJobId,
      message: formatLogMessage("[DEBUG] ProcessingService: State reset"),
      timestamp: new Date().toISOString(),
    });

    return { success: true };
  },
});

export const getNmeList = query({
  args: {},
  handler: async (ctx) => {
    const items = await ctx.db.query("nmeList").collect();
    return items.map((i) => i.itemName);
  },
});

export const getSocTariffList = query({
  args: {},
  handler: async (ctx) => {
    const items = await ctx.db.query("socTariff").collect();
    return items.map((i) => ({
      serviceCode: i.serviceCode,
      serviceName: i.serviceName,
      category: i.category,
      department: i.department,
      tariff: i.tariff,
    }));
  },
});

export const getPolicySubCategoryCoverage = query({
  args: {},
  handler: async (ctx) => {
    const items = await ctx.db.query("policySubCategoryCoverage").collect();
    return items.map((i) => ({
      parentCategory: i.parentCategory,
      subCategory: i.subCategory,
      coverage: i.coverage,
    }));
  },
});

export const getPolicyEnrollment = query({
  args: {},
  handler: async (ctx) => {
    const items = await ctx.db.query("policyEnrollment").collect();
    return items.map((i) => ({
      insuredName: i.insuredName,
      policyNumber: i.policyNumber,
      policyStartDate: i.policyStartDate,
      policyEndDate: i.policyEndDate,
      sumInsured: i.sumInsured,
      relationship: i.relationship,
      dob: i.dob,
      gender: i.gender,
      certificateNumber: i.certificateNumber,
      tpaName: i.tpaName || "",
    }));
  },
});

export const getTariffPdfCatalog = query({
  args: {},
  handler: async (ctx) => {
    const items = await ctx.db.query("tariffPdfs").collect();
    return items.map((item) => ({
      _id: item._id,
      fileName: item.fileName,
      relativePath: item.relativePath,
      hospitalName: item.hospitalName,
      normalizedHospitalName: item.normalizedHospitalName,
      storageId: item.storageId,
      uploadedAt: item.uploadedAt,
    }));
  },
});

export const upsertTariffPdf = mutation({
  args: {
    fileName: v.string(),
    relativePath: v.string(),
    hospitalName: v.string(),
    normalizedHospitalName: v.string(),
    storageId: v.id("_storage"),
  },
  handler: async (ctx, args) => {
    const existing = await ctx.db
      .query("tariffPdfs")
      .withIndex("by_fileName", (q) => q.eq("fileName", args.fileName))
      .first();

    if (existing) {
      await ctx.db.patch(existing._id, {
        relativePath: args.relativePath,
        hospitalName: args.hospitalName,
        normalizedHospitalName: args.normalizedHospitalName,
        storageId: args.storageId,
        uploadedAt: new Date().toISOString(),
      });
      return existing._id;
    }

    return await ctx.db.insert("tariffPdfs", {
      fileName: args.fileName,
      relativePath: args.relativePath,
      hospitalName: args.hospitalName,
      normalizedHospitalName: args.normalizedHospitalName,
      storageId: args.storageId,
      uploadedAt: new Date().toISOString(),
    });
  },
});

export const getJobFilesByJobId = query({
  args: {
    jobId: v.id("processJob"),
  },
  handler: async (ctx, args) => {
    const files = await ctx.db
      .query("jobFiles")
      .withIndex("by_jobId")
      .filter((q) => q.eq(q.field("jobId"), args.jobId))
      .collect();
    return files.map((f) => ({
      file: f.file,
      storageId: f.storageId,
      fileName: f.fileName,
      fileType: f.fileType,
    }));
  },
});

export const getJobFilesByType = query({
  args: {
    jobId: v.id("processJob"),
    fileType: v.union(v.literal("hospitalBill"), v.literal("tariff"), v.literal("policy")),
  },
  handler: async (ctx, args) => {
    const files = await ctx.db
      .query("jobFiles")
      .withIndex("by_jobId_fileType", (q) => q.eq("jobId", args.jobId).eq("fileType", args.fileType))
      .collect();
    return files.map((f) => ({
      file: f.file,
      storageId: f.storageId,
      fileName: f.fileName,
      fileType: f.fileType,
    }));
  },
});
export const getJobResults = query({
  args: { jobId: v.id("processJob") },
  handler: async (ctx, args) => {
    return await ctx.db
      .query("jobResults")
      .withIndex("by_jobId", (q) => q.eq("jobId", args.jobId))
      .collect();
  },
});

export const updateJobResultAnalysis = mutation({
  args: {
    jobResultId: v.id("jobResults"),
    analysis: v.any(),
  },
  handler: async (ctx, args) => {
    await ctx.db.patch(args.jobResultId, { analysis: args.analysis });
    return { success: true };
  },
});

/**
 * STAGING — returns the most recent job for a claimId.
 * Used by /api/staging/result when called with ?claimId= instead of ?jobId=.
 * No claimId index exists; this is a low-frequency staging call, so a filtered
 * scan with a descending creation-time pick is acceptable.
 */
export const getLatestJobByClaimId = query({
  args: { claimId: v.string() },
  handler: async (ctx, args) => {
    const jobs = await ctx.db
      .query("processJob")
      .filter((q) => q.eq(q.field("claimId"), args.claimId))
      .collect();
    if (jobs.length === 0) return null;
    // _creationTime is a built-in Convex field; latest = max.
    jobs.sort((a, b) => b._creationTime - a._creationTime);
    return jobs[0];
  },
});
