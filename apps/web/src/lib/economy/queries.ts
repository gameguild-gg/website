import { auth, getToken } from '@/auth';
import {
  createServerClient,
  GeneratedApi,
  type APIControllersEconomySelfServiceCapability,
  type APIControllersEconomyKycStatus,
  type FinanceEconomyBountiesDurableBountyView as EconomyBountiesDurableBountyView,
  type FinanceEconomyContractsEconomyWalletSummary as EconomyContractsEconomyWalletSummary,
  type FinanceEconomyContractsEconomyWalletTransaction as EconomyContractsEconomyWalletTransaction,
  type FinanceEconomyFundingEconomyTopUpStatus as EconomyFundingEconomyTopUpStatus,
  type FinanceEconomyPayoutsConnectAccountSnapshot as EconomyPayoutsConnectAccountSnapshot,
  type FinanceEconomyPayoutsQueriesEconomyPayoutOperation as EconomyPayoutsQueriesEconomyPayoutOperation,
  type FinanceEconomyPayoutsQueriesEconomyPayoutRequestDto as EconomyPayoutsQueriesEconomyPayoutRequestDto,
} from '@game-guild/client';
import { cache } from 'react';

export interface EconomyWorkspaceData {
  capabilities: APIControllersEconomySelfServiceCapability[];
  issue: string | null;
  payoutOperations: EconomyPayoutsQueriesEconomyPayoutOperation[];
  payoutRequests: EconomyPayoutsQueriesEconomyPayoutRequestDto[];
  transactions: EconomyContractsEconomyWalletTransaction[];
  wallet: EconomyContractsEconomyWalletSummary | null;
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

async function createEconomyModules() {
  const session = await auth().catch(() => null);
  const client = createServerClient({
    baseUrl: getApiUrl(),
    auth: { getAccessToken: () => getToken() },
    tenant: {
      getTenantId: async () =>
        session && typeof session !== 'function' ? (session.tenantId ?? null) : null,
    },
  });

  return {
    adRewards: new GeneratedApi.EconomyAdRewardsModule(client),
    bounties: new GeneratedApi.EconomyBountiesModule(client),
    economy: new GeneratedApi.EconomyModule(client),
    kyc: new GeneratedApi.EconomyKycModule(client),
  };
}

function resultIssue(label: string, result: { ok: boolean; error?: { message?: string | null } }) {
  return result.ok ? null : `${label}: ${result.error?.message || 'unavailable'}`;
}

export const getEconomyWorkspaceData = cache(async (): Promise<EconomyWorkspaceData> => {
  const economy = await createEconomyModule();
  const [wallet, transactions, capabilities, payoutRequests, payoutOperations] = await Promise.all([
    economy.getEconomyWallet(),
    economy.getEconomyWalletTransactions({ take: 50 }),
    economy.getEconomyCapabilities(),
    economy.getEconomyPayoutRequests({ take: 50 }),
    economy.getEconomyPayoutsForGetEconomyPayouts({ take: 50 }),
  ]);

  const issues = [
    resultIssue('Wallet', wallet),
    resultIssue('Transactions', transactions),
    resultIssue('Capabilities', capabilities),
    resultIssue('Payout requests', payoutRequests),
    resultIssue('Payout operations', payoutOperations),
  ].filter((issue): issue is string => issue !== null);

  return {
    wallet: wallet.ok ? wallet.data : null,
    transactions: transactions.ok ? transactions.data : [],
    capabilities: capabilities.ok ? capabilities.data : [],
    payoutRequests: payoutRequests.ok ? payoutRequests.data : [],
    payoutOperations: payoutOperations.ok ? payoutOperations.data : [],
    issue: issues.length ? issues.join(' · ') : null,
  };
});

export interface EconomyTopUpsData {
  issue: string | null;
  topUps: EconomyFundingEconomyTopUpStatus[];
}

export const getEconomyTopUpsData = cache(async (): Promise<EconomyTopUpsData> => {
  const { economy } = await createEconomyModules();
  const result = await economy.getEconomyTopUpsForGetEconomyTopUps({ take: 100 });
  return {
    topUps: result.ok ? result.data : [],
    issue: resultIssue('Top-ups', result),
  };
});

export const getEconomyTopUp = cache(async (topUpId: string) => {
  if (!topUpId.trim()) return null;
  const { economy } = await createEconomyModules();
  const result = await economy.getEconomyTopUpsForGetEconomyTopUpsByTopUpId(topUpId.trim());
  return result.ok ? result.data : null;
});

export interface EconomyKycData {
  issue: string | null;
  status: APIControllersEconomyKycStatus | null;
}

export const getEconomyKycData = cache(async (): Promise<EconomyKycData> => {
  const { kyc } = await createEconomyModules();
  const result = await kyc.getEconomyKycStatus();
  return {
    status: result.ok ? result.data : null,
    issue: resultIssue('KYC', result),
  };
});

export interface EconomyPayoutsData {
  account: EconomyPayoutsConnectAccountSnapshot | null;
  issue: string | null;
  operations: EconomyPayoutsQueriesEconomyPayoutOperation[];
  requests: EconomyPayoutsQueriesEconomyPayoutRequestDto[];
}

export const getEconomyPayoutsData = cache(async (): Promise<EconomyPayoutsData> => {
  const { economy } = await createEconomyModules();
  const [account, requests, operations] = await Promise.all([
    economy.getEconomyPayoutsAccount(),
    economy.getEconomyPayoutRequests({ take: 100 }),
    economy.getEconomyPayoutsForGetEconomyPayouts({ take: 100 }),
  ]);
  const issues = [
    resultIssue('Payout account', account),
    resultIssue('Payout requests', requests),
    resultIssue('Payout operations', operations),
  ].filter((issue): issue is string => issue !== null);
  return {
    account: account.ok ? account.data : null,
    requests: requests.ok ? requests.data : [],
    operations: operations.ok ? operations.data : [],
    issue: issues.length ? issues.join(' | ') : null,
  };
});

export interface EconomyBountiesData {
  bounties: EconomyBountiesDurableBountyView[];
  issue: string | null;
}

export const getEconomyBountiesData = cache(async (): Promise<EconomyBountiesData> => {
  const { bounties } = await createEconomyModules();
  const result = await bounties.getEconomyBountiesForGetEconomyBounties();
  return {
    bounties: result.ok ? result.data : [],
    issue: resultIssue('Bounties', result),
  };
});

export const getEconomyBounty = cache(async (bountyId: string) => {
  if (!bountyId.trim()) return null;
  const { bounties } = await createEconomyModules();
  const result = await bounties.getEconomyBountiesForGetEconomyBountiesByBountyId(bountyId.trim());
  return result.ok ? result.data : null;
});

export const getEconomyPayoutOperation = cache(async (operationId: string) => {
  if (!operationId.trim()) return null;
  const { economy } = await createEconomyModules();
  const result = await economy.getEconomyPayoutsForGetEconomyPayoutsByOperationId(operationId.trim());
  return result.ok ? result.data : null;
});

export const getAdRewardSession = cache(async (sessionId: string) => {
  if (!sessionId.trim()) return null;
  const { adRewards } = await createEconomyModules();
  const result = await adRewards.getEconomyAdRewardsSessions(sessionId.trim());
  return result.ok ? result.data : null;
});
