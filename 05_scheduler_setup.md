# Staging — Step 5 (revised): in-process scheduler (no PowerShell, no Task Scheduler)

The scheduler now lives **in the code** (`Global.asax.cs` -> `StagingScheduler`).
It self-starts and fires every 5 minutes for as long as the app runs:
- **Production:** starts on deploy, the first time the app pool starts.
- **Local:** starts the first time you hit any page (which runs `Application_Start`).

No PowerShell script and no Windows Task Scheduler entry are required.
(`Invoke-StagingProcess.ps1` from the earlier approach is now optional - keep it
only if you ever want to trigger a run manually.)

---

## Web.config AppSettings to add

```xml
<appSettings>
  <!-- ... existing keys ... -->

  <!-- Master on/off for the in-process staging scheduler. Default: on. -->
  <add key="EnableStagingScheduler" value="true" />

  <!-- IMPORTANT: the URL the timer calls every 5 min.
       On a timer thread HttpContext is null, so the code cannot reliably detect
       the site's port by itself. Set this explicitly to your site's base URL +
       the endpoint path. -->
  <add key="StagingSchedulerUrl"
       value="https://localhost:44300/MedicalScrutiny/ProcessStagingClaims" />

  <!-- Optional shared secret; if set, must match what the endpoint expects. -->
  <add key="StagingApiKey" value="" />

  <!-- Already needed by the worker: where ClaimAI runs. -->
  <add key="ClaimAIUrl" value="http://localhost:3000" />
</appSettings>
```

- **Local:** set `StagingSchedulerUrl` to your IIS Express URL (e.g. `https://localhost:44300/...`).
- **Production:** set it to the deployed site URL (e.g. `https://spectra-ai.fhpl.net/MedicalScrutiny/ProcessStagingClaims`).

> Why explicit? The timer runs on a background thread with no HTTP context, so it
> can't read the current request's port. Without `StagingSchedulerUrl` it falls
> back to `http://localhost:80/...`, which is usually wrong. **Set it explicitly.**

---

## Keep it running 24/7 (recommended IIS settings)

An in-process timer stops if IIS idles or recycles the app pool, restarting only
on the next request. To avoid pauses:

1. **App pool -> Advanced Settings -> Idle Time-out (minutes) = 0** (don't shut down on idle).
2. **App pool -> Start Mode = AlwaysRunning.**
3. **Site -> Preload Enabled = True** (with Application Initialization installed).

Even without these, nothing is lost: the worker picks up **all** unprocessed
stage-52 claims on each tick, so after any pause it catches up automatically on
the next run.

---

## Behaviour details

- **First tick:** 1 minute after startup (lets the app warm up), then every 5 min.
- **No overlap:** if a tick is still running when the next fires, the new one is
  skipped (an `Interlocked` guard). The endpoint also has its own DB lock.
- **Failures are safe:** a failed tick is simply retried next interval; the
  endpoint handles per-claim errors and fail-open to stage 5 internally.
- **Turn it off without redeploying:** set `EnableStagingScheduler=false` and
  recycle the app pool.

---

## Local testing (unchanged)
You can still trigger a run instantly instead of waiting for the timer:
- Open `https://localhost:44300/MedicalScrutiny/ProcessStagingClaims` in the browser, or
- Run `Invoke-StagingProcess.ps1`.
Use `99_testing_helpers.sql` to set a claim to stage 52 and inspect results.
