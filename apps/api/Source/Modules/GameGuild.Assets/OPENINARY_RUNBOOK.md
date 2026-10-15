# Openinary Operational Runbook

This guide covers the infrastructure and operational requirements for the Openinary asset CDN and moderation integration.

## File Size Limits
The system supports files up to 100MB.
- **IMPORTANT:** If `Assets__Openinary__MaxFileMb` is increased beyond 50MB, this change **must** be applied to the Openinary server configuration first.
- **Action:** Request limit increase via Devtron (Owner Ops) before enabling uploads > 50MB in the API.

## AI Moderation Infrastructure
Moderation uses a classifier hosted via LiteLLM.

### Virtual Key Minting
Classifier API keys must be minted as virtual keys to allow for rotation without service interruption.
- **Procedure:** Refer to `infrastructure/infra/ai-services-runbook.md` §9.2 for the current minting process.

### Secret Management
All production secrets (Openinary API keys, LiteLLM keys) must be stored in the cluster secret store.
- **Reference:** See `infrastructure/services.md` for the secret mapping for this service.

## Moderation Worker Tuning
The moderation worker processes content asynchronously.
- **Concurrency:** `MaxConcurrent` is set to 1.
- **Constraint:** This is required because the vLLM backend typically has `MAX_NUM_SEQS=2` per lane; exceeding this will cause request timeouts and trigger the `NeedsReview` fail-safe.
