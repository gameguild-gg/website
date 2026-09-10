import { auth, getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type FinanceEconomyPayoutsQueriesEconomyPayoutRequestReview as EconomyPayoutsQueriesEconomyPayoutRequestReview,
  type FinanceEconomyPayoutsQueriesEconomyPayoutRequestReviewAudit as EconomyPayoutsQueriesEconomyPayoutRequestReviewAudit,
} from '@game-guild/client';
import { cache } from 'react';

export interface EconomyPayoutReviewWorkspaceData {
  issue: string | null;
  requests: EconomyPayoutsQueriesEconomyPayoutRequestReview[];
  reviewAudits: Record<string, EconomyPayoutsQueriesEconomyPayoutRequestReviewAudit[]>;
}

function getApiUrl() {
  return process.env.API_URL || process.env.NEXT_PUBLIC_API_URL || 'http://localhost:8080';
}

async function createEconomyModule() {
  const session = await auth().catch(() => null);
  const client = createServerClient({
    baseUrl: getApiUrl(),
    auth: { getAccessToken: () => getToken() },
    tenant: {
      getTenantId: async () =>
        session && typeof session !== 'function' ? (session.tenantId ?? null) : null,
    },
  });

  return new GeneratedApi.EconomyModule(client);
}

export const getEconomyPayoutReviewWorkspaceData = cache(async (): Promise<EconomyPayoutReviewWorkspaceData> => {
  const economy = await createEconomyModule();
  const requestsResult = await economy.getAdminEconomyPayoutRequests({ take: 100 });
  if (!requestsResult.ok) {
    return {
      requests: [],
      reviewAudits: {},
      issue: requestsResult.error.message || 'The payout review queue is unavailable.',
    };
  }

  const auditResults = await Promise.all(
    requestsResult.data
      .filter((request): request is EconomyPayoutsQueriesEconomyPayoutRequestReview & { id: string } => Boolean(request.id))
      .map(async (request) => [request.id, await economy.getAdminEconomyPayoutRequestsAudit(request.id)] as const),
  );
  const reviewAudits: Record<string, EconomyPayoutsQueriesEconomyPayoutRequestReviewAudit[]> = {};
  const issues: string[] = [];

  for (const [requestId, result] of auditResults) {
    if (result.ok) reviewAudits[requestId] = result.data;
    else issues.push(`Audit ${requestId}: ${result.error.message || 'unavailable'}`);
  }

  return {
    requests: requestsResult.data,
    reviewAudits,
    issue: issues.length ? issues.join(' · ') : null,
  };
});
