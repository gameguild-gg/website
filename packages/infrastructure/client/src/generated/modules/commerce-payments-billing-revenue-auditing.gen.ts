/**
 * @game-guild/client - CommercePaymentsBillingRevenueAuditing Module
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 */

import type { ApiClient } from '../../runtime/client.js';
import type { Result } from '../../runtime/result/types.js';
import type { ApiError } from '../../runtime/errors/types.js';
import * as Types from '../types.gen.js';
import { safeParse } from '../../runtime/errors/validation.js';

/* eslint-disable @typescript-eslint/no-explicit-any */

export class CommercePaymentsBillingRevenueAuditingModule {
  constructor(private readonly client: ApiClient) {}

  /**
   * List reconciliation runs, newest first. Tenant-scoped for non-admin actors.
   *
   * Returns a paged list of revenue reconciliation runs, newest first. Non-admin actors only see runs of their own tenant.
   */
  async getBillingRevenueAuditing(query?: {
    tenantId?: string;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultRevenueReconciliationRun, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultRevenueReconciliationRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Run a reconciliation comparing an external accounting/ERP statement against internal
   * revenue events for an inclusive period. Requires the SystemAdmin role.
   *
   * Compares external accounting/ERP statement lines against internal revenue events for an inclusive period, records an immutable reconciliation run and persists every discrepancy (missing internal/external references, amount and currency mismatches, duplicate external references). When no inline lines are supplied, lines are read from the configured external statement source.
   */
  async postBillingRevenueAuditing(
    body: Types.CommercePaymentsRevenueAuditingControllerRunRevenueReconciliationInput,
  ): Promise<Result<Types.CommercePaymentsRevenueReconciliationRun, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRevenueAuditingControllerRunRevenueReconciliationInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRevenueReconciliationRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Trigger anomaly detection for the trailing days and persist alerts. Requires the
   * SystemAdmin role; the periodic worker performs the same pass automatically when
   * `RevenueAuditing:WorkerEnabled` is set.
   *
   * Evaluates daily net revenue for the trailing days against the configured baseline window and persists anomaly alerts (spikes/drops at or above the z-score threshold). Detection is idempotent per kind and day.
   */
  async postBillingRevenueAuditingAnomaliesDetect(
    body: Types.CommercePaymentsRevenueAuditingControllerDetectRevenueAnomaliesInput,
  ): Promise<Result<Types.CommercePaymentsRevenueAuditingControllerAnomalyDetectionResult, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing/anomalies/detect';

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRevenueAuditingControllerDetectRevenueAnomaliesInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRevenueAuditingControllerAnomalyDetectionResultSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List revenue anomaly alerts, newest detection first. Tenant-scoped for non-admin actors.
   *
   * Returns a paged list of revenue anomaly alerts, newest detection first, optionally filtered by status (Open, Acknowledged). Non-admin actors only see alerts of their own tenant.
   */
  async getBillingRevenueAuditingAnomalyAlerts(query?: {
    tenantId?: string;
    status?: Types.CommercePaymentsRevenueAnomalyStatus;
    skip?: number;
    take?: number;
  }): Promise<Result<Types.PagedResultRevenueAnomalyAlert, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing/anomaly-alerts';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultRevenueAnomalyAlertSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Acknowledge an open anomaly alert, recording the reviewing operator. Requires the
   * SystemAdmin role.
   *
   * Marks an open revenue anomaly alert as acknowledged, recording the reviewing operator and optional notes. The acting identity is taken from the authenticated actor, never from the request body.
   */
  async postBillingRevenueAuditingAnomalyAlertsAcknowledge(
    alertId: string,
    body: Types.CommercePaymentsRevenueAuditingControllerAcknowledgeRevenueAnomalyAlertInput,
  ): Promise<Result<void, ApiError>> {
    const url = `/api/v1/billing/revenue/auditing/anomaly-alerts/${alertId}/acknowledge`;

    // Validate request body
    const validatedBody = safeParse(Types.CommercePaymentsRevenueAuditingControllerAcknowledgeRevenueAnomalyAlertInputSchema, body, 'request');

    const result = await this.client.request({
      method: 'POST',
      path: url,
      body: validatedBody,
      requiresAuth: true,
    });

    return result as Result<void, ApiError>;
  }

  /**
   * Compliance report for an inclusive period: totals by event type, source and status,
   * uncounted events, reconciliation coverage and an attestation statement.
   *
   * Builds a compliance-grade summary for the inclusive period: revenue totals grouped by event type, source and processing status, uncounted (pending/failed) events, reconciliation coverage across the period, and an attestation statement suitable for filings.
   */
  async getBillingRevenueAuditingComplianceReport(query?: {
    fromUtc?: string;
    toUtc?: string;
    tenantId?: string;
  }): Promise<Result<Types.CommercePaymentsRevenueComplianceReport, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing/compliance-report';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRevenueComplianceReportSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Export the audit report for a period as CSV (RFC 4180) or JSON for external
   * accounting and ERP systems.
   *
   * Serializes the compliance summary and daily trend for the inclusive period as RFC 4180 CSV or JSON, ready for delivery to external accounting and ERP systems.
   */
  async getBillingRevenueAuditingExport(query?: { fromUtc?: string; toUtc?: string; format?: string; tenantId?: string }): Promise<Result<Blob, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing/export';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    return result as Result<Blob, ApiError>;
  }

  /**
   * Get one reconciliation run with its counters and summary.
   *
   * Returns one reconciliation run, including matched/discrepancy counters and the machine-readable summary captured at completion.
   */
  async getRunById(runId: string): Promise<Result<Types.CommercePaymentsRevenueReconciliationRun, ApiError>> {
    const url = `/api/v1/billing/revenue/auditing/runs/${runId}`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRevenueReconciliationRunSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * List the discrepancies recorded by a reconciliation run.
   *
   * Returns a paged list of the discrepancies recorded by a reconciliation run, optionally filtered by kind (MissingInternal, MissingExternal, AmountMismatch, CurrencyMismatch, DuplicateExternalReference).
   */
  async getBillingRevenueAuditingRunsDiscrepancies(
    runId: string,
    query?: { kind?: Types.CommercePaymentsRevenueDiscrepancyKind; skip?: number; take?: number },
  ): Promise<Result<Types.PagedResultRevenueReconciliationDiscrepancy, ApiError>> {
    const url = `/api/v1/billing/revenue/auditing/runs/${runId}/discrepancies`;

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.PagedResultRevenueReconciliationDiscrepancySchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }

  /**
   * Historical daily net-revenue trend for an inclusive period, with zero-activity days included.
   *
   * Returns one net-revenue point per UTC day in the inclusive period (credit total, debit total, net total and event count), including days without activity, plus range totals.
   */
  async getBillingRevenueAuditingTrends(query?: {
    fromUtc?: string;
    toUtc?: string;
    tenantId?: string;
  }): Promise<Result<Types.CommercePaymentsRevenueTrendReport, ApiError>> {
    const url = '/api/v1/billing/revenue/auditing/trends';

    const result = await this.client.request({
      method: 'GET',
      path: url,
      params: query,
      requiresAuth: true,
    });

    // Validate response
    if (result.ok) {
      const validatedData = safeParse(Types.CommercePaymentsRevenueTrendReportSchema, result.data, 'response');
      return { ok: true, data: validatedData };
    }

    return result;
  }
}

export function createCommercePaymentsBillingRevenueAuditingModule(client: ApiClient): CommercePaymentsBillingRevenueAuditingModule {
  return new CommercePaymentsBillingRevenueAuditingModule(client);
}
