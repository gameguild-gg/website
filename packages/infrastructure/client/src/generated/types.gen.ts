/**
 * @game-guild/client - Generated Types and Zod Schemas
 *
 * ⚠️  AUTO-GENERATED FILE - DO NOT EDIT MANUALLY
 *
 * Generated from: GameGuild API
 * API Version: 1.0
 */
/* eslint-disable @typescript-eslint/no-explicit-any */
import { z } from 'zod';

export interface AIAiChatMessage {
  content?: string | null;
  role?: string | null;
}

export interface AIAiChatInput {
  maxTokens?: number | null;
  messages?: Array<AIAiChatMessage> | null;
  model?: string | null;
  provider?: string | null;
  systemPrompt?: string | null;
  temperature?: number | null;
}

export interface AIAiCompletionOutput {
  finishReason?: string | null;
  model?: string | null;
  provider?: string | null;
  text?: string | null;
  usage?: AIAiUsage;
}

export interface AIAiConversationHistoryEntry {
  finishReason?: string | null;
  id?: string;
  model?: string | null;
  occurredAt?: string;
  outcome?: string | null;
  outcomeCode?: string | null;
  outcomeReason?: string | null;
  provider?: string | null;
  requestKind?: string | null;
  requestText?: string | null;
  responseText?: string | null;
  systemPrompt?: string | null;
  usage?: AIAiUsage;
  userId?: string | null;
}

export interface AIAiGenerateInput {
  maxTokens?: number | null;
  model?: string | null;
  prompt?: string | null;
  provider?: string | null;
  systemPrompt?: string | null;
  temperature?: number | null;
}

export interface AIAiGeneratedContentDraftInput {
  audience?: string | null;
  context?: string | null;
  maxTokens?: number | null;
  model?: string | null;
  provider?: string | null;
  subject?: string | null;
  tone?: string | null;
}

export type AIAiGeneratedContentKind = 'Email' | 'Report' | 'ListingDescription';

export interface AIAiGeneratedContentInput {
  audience?: string | null;
  context?: string | null;
  kind?: AIAiGeneratedContentKind;
  maxTokens?: number | null;
  model?: string | null;
  provider?: string | null;
  subject?: string | null;
  tone?: string | null;
}

export interface AIAiPromptTemplate {
  category?: string | null;
  createdAt?: string;
  createdByUserId?: string | null;
  description?: string | null;
  id?: string;
  isActive?: boolean;
  isSystemTemplate?: boolean;
  key?: string | null;
  name?: string | null;
  prompt?: string | null;
  systemPrompt?: string | null;
  tenantId?: string | null;
  updatedAt?: string;
  updatedByUserId?: string | null;
}

export interface AIAiPromptTemplateGenerateInput {
  maxTokens?: number | null;
  model?: string | null;
  provider?: string | null;
  temperature?: number | null;
  variables?: Record<string, string | null> | null;
}

export interface AIAiPromptTemplateRenderInput {
  variables?: Record<string, string | null> | null;
}

export interface AIAiPromptTemplateRenderOutput {
  key?: string | null;
  prompt?: string | null;
  systemPrompt?: string | null;
  templateId?: string;
  variables?: Record<string, string | null> | null;
}

export interface AIAiProviderStatus {
  baseUrl?: string | null;
  configured?: boolean;
  credentialsConfigured?: boolean;
  defaultModel?: string | null;
  provider?: string | null;
}

export interface AIAiQuotaStatus {
  currentUsage?: number;
  hardLimit?: number | null;
  isActive?: boolean;
  lastReset?: string | null;
  nextReset?: string | null;
  period?: string | null;
  remaining?: number;
  resourceType?: string | null;
  softLimit?: number | null;
  usagePercent?: number;
}

export interface AIAiQuotaStatusOutput {
  generatedAtUtc?: string;
  quotas?: Array<AIAiQuotaStatus> | null;
  tenantId?: string;
}

export interface AIAiStatusOutput {
  allowTenantOverrides?: boolean;
  defaultProvider?: string | null;
  enabled?: boolean;
  providers?: Array<AIAiProviderStatus> | null;
}

export interface AIAiUsage {
  inputTokens?: number | null;
  outputTokens?: number | null;
  totalTokens?: number | null;
}

export interface AICreateAiPromptTemplateInput {
  category?: string | null;
  description?: string | null;
  isActive?: boolean | null;
  key?: string | null;
  name?: string | null;
  prompt?: string | null;
  systemPrompt?: string | null;
}

export interface AIUpdateAiPromptTemplateInput {
  category?: string | null;
  description?: string | null;
  isActive?: boolean | null;
  name?: string | null;
  prompt?: string | null;
  systemPrompt?: string | null;
}

export interface APIControllersApplicationDetails {
  description?: string | null;
  informationalVersion?: string | null;
  name?: string | null;
  version?: string | null;
}

export interface APIControllersApplicationInfoOutput {
  application?: APIControllersApplicationDetails;
  build?: APIControllersBuildDetails;
  process?: APIControllersProcessDetails;
  runtime?: APIControllersRuntimeDetails;
  timestamp?: string;
}

export interface APIControllersBuildDetails {
  configuration?: string | null;
  framework?: string | null;
  timestamp?: string | null;
}

export interface APIControllersDependencyHealthItem {
  data?: Record<string, string> | null;
  description?: string | null;
  duration?: string;
  exception?: string | null;
  isHealthy?: boolean;
  name?: string | null;
  status?: string | null;
  tags?: Array<string> | null;
}

export interface APIControllersDependencyHealthOutput {
  dependencies?: Array<APIControllersDependencyHealthItem> | null;
  error?: string | null;
  healthyCount?: number;
  status?: string | null;
  timestamp?: string;
  totalDuration?: string;
  unhealthyCount?: number;
}

export interface APIControllersHealthinessOutput {
  checks?: Record<string, APIControllersHealthinessResponseItem> | null;
  duration?: string;
  error?: string | null;
  status?: string | null;
  timestamp?: string;
}

export interface APIControllersHealthinessResponseItem {
  data?: Record<string, Record<string, unknown>> | null;
  description?: string | null;
  duration?: string;
  status?: string | null;
}

export interface APIControllersLivenessOutput {
  alive?: boolean;
  status?: string | null;
  timestamp?: string;
  uptime?: string;
  version?: string | null;
}

export interface APIControllersProcessDetails {
  startTime?: string;
  uptime?: string;
}

export interface APIControllersReadinessOutput {
  error?: string | null;
  ready?: boolean;
  services?: Record<string, boolean> | null;
  status?: string | null;
  timestamp?: string;
}

export interface APIControllersRuntimeDetails {
  dotNetVersion?: string | null;
  osArchitecture?: string | null;
  osDescription?: string | null;
  processArchitecture?: string | null;
}

export type BillingCycle = 'Weekly' | 'Monthly' | 'Quarterly' | 'SemiAnnually' | 'Annually' | 'Biannually';

export interface BulkOperationError {
  errorCode?: string | null;
  errorMessage?: string | null;
  tenantId?: string;
  tenantName?: string | null;
}

export interface BulkOperationOutput {
  errors?: Array<BulkOperationError> | null;
  failedOperations?: number;
  isComplete?: boolean;
  successRate?: number;
  successfulOperations?: number;
  totalRequested?: number;
}

export interface CQRSIDomainEvent {
  eventId?: string;
  occurredAt?: string;
  version?: number;
}

export interface CommerceBillingInvoicePaymentRetryResult {
  accepted?: boolean;
  code?: string | null;
  invoiceId?: string;
  invoiceNumber?: string | null;
  invoiceStatus?: CommerceBillingInvoiceStatus;
  message?: string | null;
  retryScheduledAt?: string | null;
}

export type CommerceBillingInvoiceStatus = 'Draft' | 'Open' | 'Paid' | 'Void' | 'PastDue' | 'Uncollectible';

export type CommerceOrderChargeState = 'Succeeded' | 'Failed' | 'Processing' | 'RequiresAction' | 'RequiresReconciliation';

export interface CommerceOrdersAddOrderItemInput {
  productId?: string;
  productPricingId?: string;
  productPricingVersionId?: string;
  promoCode?: string | null;
  quantity?: number;
}

export interface CommerceOrdersCaptureOrderInput {
  paymentMethodId?: string | null;
}

export interface CommerceOrdersCompleteOrderInput {
  paymentId?: string | null;
  paymentMethod?: string | null;
  paymentProviderReference?: string | null;
}

export interface CommerceOrdersCreateOrderInput {
  idempotencyKey?: string | null;
}

export interface CommerceOrdersOrderCapture {
  clientActionToken?: string | null;
  createdAt?: string;
  currency?: string | null;
  discountTotal?: number;
  id?: string;
  idempotencyKey?: string | null;
  lineItems?: Array<CommerceOrdersOrderLineItem> | null;
  paidAt?: string | null;
  paymentId?: string | null;
  paymentMessage?: string | null;
  paymentMethod?: string | null;
  paymentProviderReference?: string | null;
  paymentState?: CommerceOrderChargeState;
  refundAmount?: number | null;
  refundReason?: string | null;
  refundedAt?: string | null;
  status?: CommerceOrdersOrderStatus;
  subtotal?: number;
  taxAmount?: number;
  total?: number;
  updatedAt?: string;
  userId?: string;
}

export interface CommerceOrdersOrder {
  createdAt?: string;
  currency?: string | null;
  discountTotal?: number;
  id?: string;
  idempotencyKey?: string | null;
  lineItems?: Array<CommerceOrdersOrderLineItem> | null;
  paidAt?: string | null;
  paymentMethod?: string | null;
  paymentProviderReference?: string | null;
  refundAmount?: number | null;
  refundReason?: string | null;
  refundedAt?: string | null;
  status?: CommerceOrdersOrderStatus;
  subtotal?: number;
  taxAmount?: number;
  total?: number;
  updatedAt?: string;
  userId?: string;
}

export interface CommerceOrdersOrderLineItem {
  basePrice?: number;
  currency?: string | null;
  discountAmount?: number;
  id?: string;
  isSubscription?: boolean;
  lineTotal?: number;
  priceVersion?: number;
  productId?: string;
  productName?: string | null;
  productPricingId?: string;
  productPricingVersionId?: string;
  promoCodesApplied?: string | null;
  quantity?: number;
  salePrice?: number | null;
  unitPrice?: number;
}

export type CommerceOrdersOrderStatus =
  'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled' | 'Refunded' | 'PartiallyRefunded' | 'Disputed' | 'Paid' | 'Fulfilled' | 'OnHold';

export interface CommercePaymentsBillingChargesControllerCancelBillingChargeInput {
  canceledBy?: string | null;
  cancellationReason?: string | null;
}

export interface CommercePaymentsBillingChargesControllerCreateBillingChargeInput {
  amount?: number;
  paymentMethodId?: string | null;
  subscriptionId?: string;
  tenantId?: string;
}

export interface CommercePaymentsBillingChargesControllerRefundBillingChargeInput {
  amount?: number | null;
  reason?: string | null;
}

export interface CommercePaymentsCalculateTaxInput {
  amount: number;
  applicableExemptions?: Array<string> | null;
  currency: string | null;
  customerType: string | null;
  customerVatNumber?: string | null;
  isTaxInclusive?: boolean;
  jurisdictionCode: string | null;
  productCategory?: string | null;
  transactionDate?: string | null;
}

export interface CommercePaymentsCreateTaxJurisdictionInput {
  code?: string | null;
  country?: string | null;
  defaultRate?: number;
  name?: string | null;
  state?: string | null;
  taxType?: string | null;
}

export interface CommercePaymentsCreateTaxRuleInput {
  customerType?: string | null;
  description?: string | null;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  jurisdictionCode?: string | null;
  productCategory?: string | null;
  rate?: number;
}

export interface CommercePaymentsCreateWalletInput {
  currency?: string | null;
}

export type CommercePaymentsCustomerType = 'B2C' | 'B2B';

export interface CommercePaymentsLockWalletInput {
  reason: string | null;
}

export interface CommercePaymentsModelsFreezeWalletInput {
  reason?: string | null;
}

export interface CommercePaymentsModelsPatchWalletInput {
  currency?: string | null;
  dailyLimit?: number | null;
  monthlyLimit?: number | null;
}

export interface CommercePaymentsPatchTaxJurisdictionInput {
  defaultRate?: number | null;
  isActive?: boolean | null;
  name?: string | null;
  taxType?: string | null;
}

export interface CommercePaymentsPatchTaxRuleInput {
  description?: string | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  isActive?: boolean | null;
  rate?: number | null;
}

export interface CommercePaymentsPaymentCancellationResult {
  canceledAt: string;
  canceledBy?: string | null;
  cancellationReason: string | null;
  errorMessage?: string | null;
  paymentId: string;
  refundAmount?: number | null;
  refundProcessed?: boolean;
  success: boolean;
}

export interface CommercePaymentsPaymentResult {
  amount?: Money;
  failureReason?: string | null;
  invoiceId?: string | null;
  paymentId?: string | null;
  paymentMethodId?: string | null;
  processedAt?: string | null;
  status?: CommercePaymentsPaymentStatus;
  success?: boolean;
  tenantId?: string;
  transactionId?: string | null;
}

export interface CommercePaymentsPaymentRetryResult {
  failureReason?: string | null;
  maxRetriesReached?: boolean;
  nextRetryAt?: string | null;
  paymentResult?: CommercePaymentsPaymentResult;
  retryAttempt?: number;
  success?: boolean;
}

export type CommercePaymentsPaymentStatus = 'Pending' | 'Processing' | 'Succeeded' | 'Failed' | 'Cancelled' | 'RequiresAction' | 'Refunded' | 'Disputed';

export interface CommercePaymentsPaymentsControllerCancelPaymentInput {
  canceledBy?: string | null;
  cancellationReason?: string | null;
  notes?: string | null;
}

export interface CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput {
  paymentMethodId?: string | null;
  subscriptionId?: string;
  tenantId?: string;
}

export interface CommercePaymentsPaymentsControllerCreateSetupIntentInput {
  customerEmail?: string | null;
  customerName?: string | null;
  subscriptionId?: string;
  tenantId?: string;
}

export interface CommercePaymentsPaymentsControllerCreateSetupIntentOutput {
  clientSecret?: string | null;
  customerId?: string | null;
  setupIntentId?: string | null;
  subscriptionId?: string;
}

export interface CommercePaymentsPaymentsControllerProcessPaymentInput {
  amount?: number;
  paymentMethodId?: string | null;
  subscriptionId?: string;
  tenantId?: string;
}

export interface CommercePaymentsPaymentsControllerRefundInput {
  amount?: number | null;
  reason?: string | null;
}

export interface CommercePaymentsProcessRefundResult {
  currency: string | null;
  errorMessage?: string | null;
  estimatedCompletionDate?: string | null;
  isSuccess?: boolean;
  isSuccessful?: boolean;
  paymentId: string;
  processedAt: string;
  processingFee?: number;
  reason: string | null;
  referenceNumber?: string | null;
  refundId: string;
  refundedAmount: number;
  status: CommercePaymentsTransactionStatus;
}

export interface CommercePaymentsTaxBreakdown {
  description?: string | null;
  jurisdictionCode?: string | null;
  rate?: number;
  taxAmount?: number;
  taxType?: CommercePaymentsTaxType;
  taxableAmount?: number;
}

export interface CommercePaymentsTaxCalculationResult {
  effectiveTaxRate?: number;
  exemptionReason?: string | null;
  isReverseCharge?: boolean;
  isTaxExempt?: boolean;
  jurisdictionCode?: string | null;
  jurisdictionName?: string | null;
  subtotalAmount?: number;
  taxAmount?: number;
  taxBreakdowns?: Array<CommercePaymentsTaxBreakdown> | null;
  taxDescription?: string | null;
  taxType?: CommercePaymentsTaxType;
  totalAmount?: number;
}

export interface CommercePaymentsTaxExemptionValidationResult {
  exemptionRate?: number;
  exemptionType?: string | null;
  isValid?: boolean;
  validFrom?: string | null;
  validTo?: string | null;
  validationMessage?: string | null;
  warnings?: Array<string> | null;
}

export interface CommercePaymentsTaxJurisdiction {
  childJurisdictions?: Array<CommercePaymentsTaxJurisdiction> | null;
  code: string;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isReverseChargeApplicable?: boolean;
  name: string;
  parentJurisdiction?: CommercePaymentsTaxJurisdiction;
  parentJurisdictionId?: string | null;
  taxRegistrationNumber?: string | null;
  taxRules?: Array<CommercePaymentsTaxRule> | null;
  tenantId?: string | null;
  type?: CommercePaymentsTaxJurisdictionType;
  updatedAt: string;
  version?: number;
}

export interface CommercePaymentsTaxJurisdictionDto {
  code?: string | null;
  country?: string | null;
  defaultRate?: number;
  id?: string;
  isActive?: boolean;
  name?: string | null;
  state?: string | null;
  taxType?: string | null;
}

export type CommercePaymentsTaxJurisdictionType = 'Country' | 'State' | 'Province' | 'Region' | 'City' | 'County' | 'District';

export interface CommercePaymentsTaxRate {
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  maximumTaxableAmount?: number | null;
  minimumTaxableAmount?: number | null;
  productCategory?: string | null;
  rate?: number;
  taxJurisdiction?: CommercePaymentsTaxJurisdiction;
  taxJurisdictionId: string;
  taxType?: CommercePaymentsTaxType;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface CommercePaymentsTaxRule {
  createdAt: string;
  customerTypeFilter?: CommercePaymentsCustomerType;
  defaultTaxRate?: CommercePaymentsTaxRate;
  defaultTaxRateId?: string | null;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  effectiveFrom?: string | null;
  effectiveTo?: string | null;
  exemptionConditions?: string | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isReverseCharge?: boolean;
  isTaxInclusive?: boolean;
  maximumAmount?: number | null;
  minimumAmount?: number | null;
  name: string;
  priority?: number;
  productCategories?: string | null;
  ruleType?: CommercePaymentsTaxRuleType;
  taxJurisdiction?: CommercePaymentsTaxJurisdiction;
  taxJurisdictionId: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface CommercePaymentsTaxRuleDto {
  customerType?: string | null;
  description?: string | null;
  effectiveFrom?: string;
  effectiveTo?: string | null;
  id?: string;
  isActive?: boolean;
  jurisdictionCode?: string | null;
  productCategory?: string | null;
  rate?: number;
}

export type CommercePaymentsTaxRuleType = 'Standard' | 'Reduced' | 'ZeroRated' | 'Exempt' | 'ReverseCharge' | 'WithholdingTax' | 'Compound' | 'Custom';

export type CommercePaymentsTaxType = 'VAT' | 'GST' | 'SalesTax' | 'ServiceTax' | 'WithholdingTax' | 'ExciseTax' | 'CustomsDuty' | 'Other';

export type CommercePaymentsTransactionStatus = 'Pending' | 'Processing' | 'Completed' | 'Failed' | 'Cancelled' | 'Reversed';

export interface CommercePaymentsUserWallet {
  balance?: number;
  createdAt: string;
  currency: string;
  dailyLimit?: number | null;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isLocked?: boolean;
  isNew?: boolean;
  lastTransactionAt?: string | null;
  lockReason?: string | null;
  monthlyLimit?: number | null;
  tenantId?: string | null;
  transactions?: Array<CommercePaymentsWalletTransaction> | null;
  updatedAt: string;
  userId: string;
  version?: number;
}

export interface CommercePaymentsValidateTaxExemptionInput {
  customerId?: string | null;
  customerVatNumber?: string | null;
  exemptionCertificateNumber?: string | null;
  exemptionType?: string | null;
  jurisdictionCode?: string | null;
  transactionDate?: string | null;
}

export interface CommercePaymentsWalletTransaction {
  amount?: number;
  balanceAfter?: number;
  createdAt: string;
  deletedAt?: string | null;
  description: string;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  metadata?: string | null;
  notes?: string | null;
  processedAt?: string | null;
  referenceId?: string | null;
  status?: CommercePaymentsTransactionStatus;
  tenantId?: string | null;
  type?: CommercePaymentsWalletTransactionType;
  updatedAt: string;
  version?: number;
  wallet?: CommercePaymentsUserWallet;
  walletId: string;
}

export type CommercePaymentsWalletTransactionType = 'Credit' | 'Debit' | 'TransferIn' | 'TransferOut' | 'Refund' | 'Fee' | 'Adjustment';

export interface CommerceProductsAddSupportTicketMessageInput {
  authorEmail?: string | null;
  authorName?: string | null;
  authorType?: CommerceProductsSupportTicketMessageAuthorType;
  authorUserId?: string;
  body?: string | null;
  isInternal?: boolean;
  tenantId?: string;
}

export interface CommerceProductsAppliedPromoCode {
  code?: string | null;
  discountAmount?: number;
  discountPercentage?: number | null;
}

export interface CommerceProductsApplyPromoCodesInput {
  orderAmount?: number;
  productId?: string | null;
  promoCodes?: Array<string> | null;
}

export interface CommerceProductsAssignSupportTicketInput {
  agentName?: string | null;
  agentUserId?: string;
  tenantId?: string;
}

export interface CommerceProductsBatchCreateProductsInput {
  products?: Array<CommerceProductsBatchProductCreateItem> | null;
  tenantId?: string | null;
}

export interface CommerceProductsBatchProductCreateItem {
  affiliateCommissionPercentage?: number;
  bundleItems?: Array<string> | null;
  creatorId?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  isBundle?: boolean;
  maxAffiliateDiscount?: number;
  name?: string | null;
  referralCommissionPercentage?: number;
  shortDescription?: string | null;
  type?: CommerceProductsProductType;
}

export interface CommerceProductsCheckMultipleAccessInput {
  productIds?: Array<string> | null;
}

export interface CommerceProductsCloseSupportTicketInput {
  agentName?: string | null;
  agentUserId?: string;
  closingNotes?: string | null;
  tenantId?: string;
}

export interface CommerceProductsCreateProductInput {
  affiliateCommissionPercentage?: number;
  bundleItems?: Array<string> | null;
  creatorId?: string | null;
  description?: string | null;
  imageUrl?: string | null;
  isBundle?: boolean;
  maxAffiliateDiscount?: number;
  name?: string | null;
  referralCommissionPercentage?: number;
  shortDescription?: string | null;
  tenantId?: string | null;
  type?: CommerceProductsProductType;
}

export interface CommerceProductsCreatePromoCodeInput {
  code?: string | null;
  currency?: string | null;
  description?: string | null;
  discountAmount?: number | null;
  discountPercentage?: number | null;
  isActive?: boolean;
  isExclusive?: boolean;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  minimumOrderAmount?: number | null;
  name?: string | null;
  productId?: string | null;
  stackingPriority?: number;
  type?: CommerceProductsPromoCodeType;
  validFrom?: string | null;
  validUntil?: string | null;
}

export interface CommerceProductsCreateSupportTicketInput {
  body?: string | null;
  category?: string | null;
  customerId?: string;
  customerName?: string | null;
  priority?: CommerceProductsSupportTicketPriority;
  reporterEmail?: string | null;
  reporterName?: string | null;
  reporterUserId?: string;
  subject?: string | null;
  tenantId?: string;
}

export interface CommerceProductsEntitlementCheckResult {
  hasAccess?: boolean;
  productId?: string;
}

export interface CommerceProductsEntitlementInfo {
  accessEndDate?: string | null;
  accessStartDate?: string | null;
  acquisitionType?: string | null;
  currency?: string | null;
  isSubscription?: boolean;
  pricePaid?: number;
  productId?: string;
  productName?: string | null;
  status?: string | null;
  subscriptionStatus?: string | null;
}

export interface CommerceProductsGrantEntitlementInput {
  acquisitionType?: CommerceProductsProductAcquisitionType;
  currency?: string | null;
  expiresAt?: string | null;
  pricePaid?: number;
  productId?: string;
  userId?: string;
}

export interface CommerceProductsPatchProductInput {
  affiliateCommissionPercentage?: number | null;
  bundleItems?: Array<string> | null;
  description?: string | null;
  expectedVersion?: number | null;
  imageUrl?: string | null;
  isBundle?: boolean | null;
  maxAffiliateDiscount?: number | null;
  name?: string | null;
  referralCommissionPercentage?: number | null;
  shortDescription?: string | null;
  type?: CommerceProductsProductType;
}

export interface CommerceProductsPatchPromoCodeInput {
  currency?: string | null;
  description?: string | null;
  discountAmount?: number | null;
  discountPercentage?: number | null;
  isActive?: boolean | null;
  isExclusive?: boolean | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  minimumOrderAmount?: number | null;
  name?: string | null;
  productId?: string | null;
  stackingPriority?: number | null;
  type?: CommerceProductsPromoCodeType;
  validFrom?: string | null;
  validUntil?: string | null;
}

export type CommerceProductsProductAcquisitionType = 'Purchase' | 'Subscription' | 'Grant' | 'PromoCode' | 'Bundle' | 'Trial' | 'Referral' | 'Free' | 'Gift';

export interface CommerceProductsProduct {
  affiliateCommissionPercentage?: number;
  bundleItems?: Array<string> | null;
  createdAt?: string;
  creatorId?: string | null;
  description?: string | null;
  id?: string;
  imageUrl?: string | null;
  isBundle?: boolean;
  isPublished?: boolean;
  maxAffiliateDiscount?: number;
  name?: string | null;
  pricing?: Array<CommerceProductsProductPricing> | null;
  referralCommissionPercentage?: number;
  shortDescription?: string | null;
  type?: CommerceProductsProductType;
  updatedAt?: string;
}

export interface CommerceProductsProductPricing {
  basePrice?: number;
  currency?: string | null;
  currentPrice?: number;
  id?: string;
  isDefault?: boolean;
  isSaleActive?: boolean;
  name?: string | null;
  productId?: string;
  saleEndDate?: string | null;
  salePrice?: number | null;
  saleStartDate?: string | null;
}

export type CommerceProductsProductType =
  | 'Program'
  | 'Course'
  | 'Bundle'
  | 'Subscription'
  | 'Workshop'
  | 'Mentorship'
  | 'Ebook'
  | 'ResourcePack'
  | 'Community'
  | 'Certification'
  | 'Physical'
  | 'Service'
  | 'LearningPathway'
  | 'Other';

export interface CommerceProductsPromoCodeApplicationResult {
  appliedCodes?: Array<CommerceProductsAppliedPromoCode> | null;
  finalAmount?: number;
  originalAmount?: number;
  rejectedCodes?: Array<CommerceProductsRejectedPromoCode> | null;
  totalDiscount?: number;
}

export interface CommerceProductsPromoCode {
  code?: string | null;
  createdAt?: string;
  currency?: string | null;
  description?: string | null;
  discountAmount?: number | null;
  discountPercentage?: number | null;
  id?: string;
  isActive?: boolean;
  isExclusive?: boolean;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  minimumOrderAmount?: number | null;
  name?: string | null;
  productId?: string | null;
  stackingPriority?: number;
  type?: CommerceProductsPromoCodeType;
  updatedAt?: string;
  usageCount?: number;
  validFrom?: string | null;
  validUntil?: string | null;
}

export type CommerceProductsPromoCodeType = 'PercentageOff' | 'FixedAmountOff' | 'FreeTrial' | 'BuyOneGetOne' | 'FreeShipping';

export interface CommerceProductsPromoCodeUsage {
  averageDiscountPerUse?: number;
  code?: string | null;
  firstUsedAt?: string | null;
  lastUsedAt?: string | null;
  maxUses?: number | null;
  promoCodeId?: string;
  remainingUses?: number | null;
  totalDiscountGiven?: number;
  totalUses?: number;
  uniqueUsers?: number;
}

export interface CommerceProductsPromoCodeValidationResult {
  code?: string | null;
  discountAmount?: number;
  discountPercentage?: number | null;
  errorMessage?: string | null;
  isValid?: boolean;
}

export interface CommerceProductsRejectedPromoCode {
  code?: string | null;
  reason?: string | null;
}

export interface CommerceProductsResolveSupportTicketInput {
  agentName?: string | null;
  agentUserId?: string;
  resolutionSummary?: string | null;
  tenantId?: string;
}

export interface CommerceProductsRevokeEntitlementInput {
  productId?: string;
  reason?: string | null;
  userId?: string;
}

export interface CommerceProductsSupportTicket {
  assignedToName?: string | null;
  assignedToUserId?: string | null;
  category?: string | null;
  closedAt?: string | null;
  customerId?: string;
  customerName?: string | null;
  firstResponseAt?: string | null;
  id?: string;
  lastMessageAt?: string | null;
  lastMessagePreview?: string | null;
  messageCount?: number;
  messages?: Array<CommerceProductsSupportTicketMessage> | null;
  openedAt?: string;
  priority?: CommerceProductsSupportTicketPriority;
  reporterEmail?: string | null;
  reporterName?: string | null;
  reporterUserId?: string;
  resolutionSummary?: string | null;
  resolvedAt?: string | null;
  responseDueBy?: string | null;
  status?: CommerceProductsSupportTicketStatus;
  subject?: string | null;
  tenantId?: string | null;
}

export type CommerceProductsSupportTicketMessageAuthorType = 'Customer' | 'Agent' | 'System';

export interface CommerceProductsSupportTicketMessage {
  authorEmail?: string | null;
  authorName?: string | null;
  authorType?: CommerceProductsSupportTicketMessageAuthorType;
  authorUserId?: string;
  body?: string | null;
  createdAt?: string;
  id?: string;
  isInternal?: boolean;
  ticketId?: string;
}

export type CommerceProductsSupportTicketPriority = 'Low' | 'Normal' | 'High' | 'Urgent';

export type CommerceProductsSupportTicketStatus = 'Open' | 'InProgress' | 'Resolved' | 'Closed' | 'Cancelled';

export interface CommerceProductsUpdateProductInput {
  affiliateCommissionPercentage?: number | null;
  bundleItems?: Array<string> | null;
  description?: string | null;
  expectedVersion?: number | null;
  imageUrl?: string | null;
  isBundle?: boolean | null;
  maxAffiliateDiscount?: number | null;
  name?: string | null;
  referralCommissionPercentage?: number | null;
  shortDescription?: string | null;
  type?: CommerceProductsProductType;
}

export interface CommerceProductsUpdatePromoCodeInput {
  currency?: string | null;
  description?: string | null;
  discountAmount?: number | null;
  discountPercentage?: number | null;
  isActive?: boolean | null;
  isExclusive?: boolean | null;
  maxUses?: number | null;
  maxUsesPerUser?: number | null;
  minimumOrderAmount?: number | null;
  name?: string | null;
  productId?: string | null;
  stackingPriority?: number | null;
  type?: CommerceProductsPromoCodeType;
  validFrom?: string | null;
  validUntil?: string | null;
}

export interface CommerceProductsValidatePromoCodeInput {
  code?: string | null;
  orderAmount?: number;
  productId?: string | null;
}

export interface CommerceSubscriptionsBillingHistory {
  amount?: number;
  billingDate?: string;
  createdAt?: string;
  currency?: string | null;
  description?: string | null;
  externalPaymentId?: string | null;
  id?: string;
  status?: string | null;
  subscriptionId?: string;
}

export interface CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput {
  effectiveDate?: string | null;
  note?: string | null;
  reason?: CommerceSubscriptionsCancellationReason;
}

export interface CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput {
  amount?: number;
  billingCycle?: BillingCycle;
  createdByUserId?: string;
  fulfilledOrderId?: string | null;
  planId?: string;
  startDate?: string | null;
  tenantId?: string;
  trialDays?: number | null;
}

export type CommerceSubscriptionsCancellationReason =
  'UserRequested' | 'PaymentFailed' | 'PlanDiscontinued' | 'PolicyViolation' | 'Downgrade' | 'TrialEnded' | 'Custom' | 'ExternalRequest';

export interface CommerceSubscriptionsClientModulesOutput {
  clientId?: string;
  featureFlags?: Record<string, boolean> | null;
  subscriptions?: PagedResultOfGameGuildCommerceSubscriptionsSubscription;
}

export interface CommerceSubscriptionsCreateClientInput {
  adminEmail?: string | null;
  cnpj?: string | null;
  description?: string | null;
  fiscalData?: Record<string, Record<string, unknown> | null> | null;
  name?: string | null;
  slug?: string | null;
  taxId?: string | null;
}

export interface CommerceSubscriptionsSubscription {
  amount?: Money;
  autoRenew?: boolean;
  billingCycle?: BillingCycle;
  billingCycleCount?: number;
  cancellationNote?: string | null;
  cancellationReason?: CommerceSubscriptionsCancellationReason;
  cancelledAt?: string | null;
  createdAt: string;
  createdByUserId: string;
  currentPeriodEnd?: string;
  currentPeriodStart?: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  endDate?: string | null;
  externalCustomerId?: string | null;
  externalId?: string | null;
  fulfilledOrderId?: string | null;
  id?: string;
  isActive?: boolean;
  isCancelled?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isTrialing?: boolean;
  lastModifyingOrderId?: string | null;
  lastPaymentAt?: string | null;
  lastPaymentIdempotencyKey?: string | null;
  lastProcessedBillingCycle?: number;
  lastRenewalIdempotencyKey?: string | null;
  lockedPriceVersionId?: string | null;
  metadata?: string | null;
  nextBillingDate?: string;
  plan?: CommerceSubscriptionsSubscriptionPlan;
  planId: string;
  rowVersion?: string | null;
  startDate?: string;
  status?: CommerceSubscriptionsSubscriptionStatus;
  tenantId?: string | null;
  trialEndDate?: string | null;
  updatedAt: string;
  version?: number;
}

export interface CommerceSubscriptionsSubscriptionChurnReport {
  activeSubscriptions?: number;
  cancelledInPeriod?: number;
  churnRate?: number;
  endDate?: string;
  generatedAt?: string;
  monthlyRecurringRevenue?: number;
  retentionRate?: number;
  startDate?: string;
  statusBreakdown?: Record<string, number> | null;
  tenantId?: string | null;
  totalSubscriptions?: number;
}

export interface CommerceSubscriptionsSubscriptionDowngradeResult {
  creditIssued?: Money;
  effectiveDate?: string | null;
  failureReason?: string | null;
  success?: boolean;
  updatedSubscription?: CommerceSubscriptionsSubscription;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput {
  autoRenew?: boolean;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput {
  effectiveDate?: string | null;
  note?: string | null;
  reason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput {
  effectiveDate?: string | null;
  newPlanId?: string;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput {
  convertToPaid?: boolean;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput {
  externalCustomerId?: string | null;
  externalSubscriptionId?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput {
  pauseUntil?: string | null;
  reason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput {
  trialDays?: number;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput {
  reason?: string | null;
}

export interface CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput {
  effectiveDate?: string | null;
  newPlanId?: string;
}

export interface CommerceSubscriptionsSubscriptionNotification {
  channel?: string | null;
  createdAt?: string;
  id?: string;
  isSent?: boolean;
  message?: string | null;
  recipientId?: string;
  sentAt?: string | null;
  subscriptionId?: string | null;
  tenantId?: string | null;
  title?: string | null;
}

export interface CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput {
  channel?: NotificationsNotificationChannel;
}

export interface CommerceSubscriptionsSubscriptionPlan {
  annualPriceInCents?: number | null;
  createdAt: string;
  currency: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  externalId?: string | null;
  features?: string | null;
  hasAdvancedAnalytics?: boolean;
  hasCustomBranding?: boolean;
  hasPrioritySupport?: boolean;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isFeatured?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  maxApiCallsPerMonth?: number | null;
  maxStorageMb?: number | null;
  maxUsers?: number | null;
  metadata?: string | null;
  monthlyPriceInCents?: number;
  name: string;
  slug: string;
  sortOrder?: number;
  subscriptions?: Array<CommerceSubscriptionsSubscription> | null;
  tenantId?: string | null;
  trialPeriodDays?: number;
  updatedAt: string;
  version?: number;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput {
  newName?: string | null;
  newSlug?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput {
  externalId?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput {
  featured?: boolean;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput {
  description?: string | null;
  name?: string | null;
  planId?: string;
  sortOrder?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput {
  features?: string | null;
  hasAdvancedAnalytics?: boolean | null;
  hasCustomBranding?: boolean | null;
  hasPrioritySupport?: boolean | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput {
  maxApiCallsPerMonth?: number | null;
  maxStorageMb?: number | null;
  maxUsers?: number | null;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput {
  annualPriceInCents?: number | null;
  monthlyPriceInCents?: number;
}

export interface CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput {
  apiCalls?: number;
  storageMb?: number;
  users?: number;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput {
  basePlanId?: string;
  comparePlanIds?: Array<string> | null;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput {
  currency?: string | null;
  description?: string | null;
  monthlyPriceInCents?: number;
  name?: string | null;
  slug?: string | null;
}

export interface CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput {
  annualPriceInCents?: number | null;
  description?: string | null;
  features?: string | null;
  hasAdvancedAnalytics?: boolean | null;
  hasCustomBranding?: boolean | null;
  hasPrioritySupport?: boolean | null;
  maxApiCallsPerMonth?: number | null;
  maxStorageMb?: number | null;
  maxUsers?: number | null;
  monthlyPriceInCents?: number;
  name?: string | null;
  slug?: string | null;
  sortOrder?: number | null;
}

export type CommerceSubscriptionsSubscriptionStatus = 'PendingActivation' | 'Active' | 'Trialing' | 'PastDue' | 'Suspended' | 'Cancelled' | 'Expired';

export interface CommerceSubscriptionsSubscriptionUpgradeResult {
  creditApplied?: Money;
  failureReason?: string | null;
  proratedAmount?: Money;
  success?: boolean;
  updatedSubscription?: CommerceSubscriptionsSubscription;
}

export interface CommerceSubscriptionsSubscriptionUsage {
  apiCallsThisMonth?: number;
  isOverLimit?: boolean;
  limitWarnings?: Array<string> | null;
  maxApiCallsPerMonth?: number | null;
  maxStorageMb?: number | null;
  maxUsers?: number | null;
  storageUsedMb?: number;
  subscriptionId?: string;
  usersCount?: number;
}

export interface CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput {
  amount?: number;
  billingCycle?: BillingCycle;
  createdByUserId?: string;
  currency?: string | null;
  fulfilledOrderId?: string | null;
  planId?: string;
  startDate?: string | null;
  tenantId?: string;
  trialDays?: number | null;
}

export interface CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput {
  autoRenew?: boolean | null;
  billingCycle?: BillingCycle;
  externalCustomerId?: string | null;
  externalSubscriptionId?: string | null;
  metadata?: string | null;
}

export interface CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput {
  amount?: number;
  autoRenew?: boolean;
  billingCycle?: BillingCycle;
  externalCustomerId?: string | null;
  externalSubscriptionId?: string | null;
  planId?: string;
}

export interface ComplianceFERPACompleteFerpaInspectionRequestBody {
  approved?: boolean;
  notes?: string | null;
  processedByUserId?: string;
}

export type ComplianceFERPAEducationRecordKind =
  'CourseEnrollment' | 'AssessmentSubmission' | 'Grade' | 'Certificate' | 'Attendance' | 'Communication' | 'SupportCase' | 'Custom';

export interface ComplianceFERPAFerpaDirectoryInformationPolicy {
  allowedFieldsJson?: string | null;
  annualNoticeSentAt?: string | null;
  id?: string;
  noticeUrl?: string | null;
  optOutEnabled?: boolean;
  tenantId?: string | null;
}

export type ComplianceFERPAFerpaDisclosureBasis =
  | 'StudentConsent'
  | 'GuardianConsent'
  | 'SchoolOfficial'
  | 'FinancialAid'
  | 'HealthOrSafetyEmergency'
  | 'AuditOrEvaluation'
  | 'CourtOrder'
  | 'DirectoryInformation'
  | 'Other';

export interface ComplianceFERPAFerpaDisclosureConsent {
  effectiveFrom?: string;
  expiresAt?: string | null;
  guardianUserId?: string | null;
  id?: string;
  isActive?: boolean;
  purpose?: string | null;
  recipient?: string | null;
  revokedAt?: string | null;
  scope?: string | null;
  studentUserId?: string;
}

export interface ComplianceFERPAFerpaDisclosureLog {
  basis?: ComplianceFERPAFerpaDisclosureBasis;
  disclosedAt?: string;
  disclosedByUserId?: string;
  id?: string;
  purpose?: string | null;
  recipient?: string | null;
  recordIdsJson?: string | null;
  studentUserId?: string;
}

export interface ComplianceFERPAFerpaEducationRecord {
  createdAt?: string;
  externalRecordId?: string | null;
  id?: string;
  isDirectoryInformation?: boolean;
  metadataJson?: string | null;
  protectionLevel?: ComplianceFERPAFerpaRecordProtectionLevel;
  recordKind?: ComplianceFERPAEducationRecordKind;
  retentionUntil?: string | null;
  studentUserId?: string;
  title?: string | null;
}

export interface ComplianceFERPAFerpaInspectionInput {
  deadline?: string;
  id?: string;
  processedAt?: string | null;
  processedByUserId?: string | null;
  processingNotes?: string | null;
  requestedByUserId?: string;
  status?: ComplianceFERPAFerpaRequestStatus;
  studentUserId?: string;
}

export type ComplianceFERPAFerpaRecordProtectionLevel = 'DirectoryInformation' | 'EducationRecord' | 'SensitiveEducationRecord' | 'Restricted';

export type ComplianceFERPAFerpaRequestStatus = 'Pending' | 'InReview' | 'Completed' | 'Denied' | 'Expired';

export interface ComplianceFERPAGrantFerpaDisclosureConsentCommand {
  effectiveFrom?: string;
  expiresAt?: string | null;
  guardianUserId?: string | null;
  purpose?: string | null;
  recipient?: string | null;
  scope?: string | null;
  studentUserId?: string;
}

export interface ComplianceFERPARecordFerpaDisclosureCommand {
  basis?: ComplianceFERPAFerpaDisclosureBasis;
  disclosedAt?: string;
  disclosedByUserId?: string;
  purpose?: string | null;
  recipient?: string | null;
  recordIdsJson?: string | null;
  scope?: string | null;
  studentUserId?: string;
}

export interface ComplianceFERPARegisterEducationRecordCommand {
  externalRecordId?: string | null;
  isDirectoryInformation?: boolean;
  metadataJson?: string | null;
  protectionLevel?: ComplianceFERPAFerpaRecordProtectionLevel;
  recordKind?: ComplianceFERPAEducationRecordKind;
  retentionUntil?: string | null;
  studentUserId?: string;
  tenantId?: string | null;
  title?: string | null;
}

export interface ComplianceFERPASubmitFerpaInspectionRequestCommand {
  deadline?: string;
  description?: string | null;
  requestedByUserId?: string;
  studentUserId?: string;
}

export interface ComplianceFERPAUpsertDirectoryInformationPolicyCommand {
  allowedFieldsJson?: string | null;
  annualNoticeSentAt?: string | null;
  noticeUrl?: string | null;
  optOutEnabled?: boolean;
  tenantId?: string | null;
}

export type ContentStatus = 'Draft' | 'Review' | 'Published' | 'Archived' | 'Deleted';

export type ContentVisibility = 'Private' | 'Internal' | 'Friends' | 'Protected' | 'Public';

export interface ContentPagesContentResource {
  authorId?: string | null;
  authorName?: string | null;
  body?: string | null;
  categorySlug?: string | null;
  coverImageUrl?: string | null;
  createdAt?: string;
  customData?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  id?: string;
  isFeatured?: boolean;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  locale?: string | null;
  metaDescription?: string | null;
  metaTitle?: string | null;
  ogImageUrl?: string | null;
  publishedAt?: string | null;
  readingTimeMinutes?: number | null;
  resourceType?: string | null;
  scheduledPublishAt?: string | null;
  slug?: string | null;
  sortOrder?: number;
  status?: string | null;
  structuredData?: string | null;
  summary?: string | null;
  tags?: string | null;
  title?: string | null;
  updatedAt?: string | null;
  videoUrl?: string | null;
  viewCount?: number;
}

export type ContentPagesContentResourceStatus = 'Draft' | 'InReview' | 'Published' | 'Archived';

export type ContentPagesContentResourceType = 'Article' | 'Tutorial' | 'Documentation' | 'Video' | 'Download' | 'ExternalLink' | 'Course' | 'Custom';

export interface ContentPagesCreateContentResource {
  body?: string | null;
  categorySlug?: string | null;
  coverImageUrl?: string | null;
  customData?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  isFeatured?: boolean;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  locale?: string | null;
  metaDescription?: string | null;
  metaTitle?: string | null;
  ogImageUrl?: string | null;
  readingTimeMinutes?: number | null;
  resourceType?: ContentPagesContentResourceType;
  slug?: string | null;
  sortOrder?: number;
  structuredData?: string | null;
  summary?: string | null;
  tags?: string | null;
  title?: string | null;
  videoUrl?: string | null;
}

export interface ContentPagesCreateMarketingLead {
  company?: string | null;
  email: string;
  locale?: string | null;
  message?: string | null;
  name?: string | null;
  pagePath?: string | null;
  plan?: string | null;
  referrer?: string | null;
  source: string;
  topic?: string | null;
  userAgent?: string | null;
}

export interface ContentPagesCreatePage {
  body?: string | null;
  canonicalUrl?: string | null;
  customData?: string | null;
  description?: string | null;
  locale?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  metaTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogTitle?: string | null;
  ogType?: string | null;
  pageType?: ContentPagesPageType;
  parentPageId?: string | null;
  robotsDirective?: string | null;
  slug?: string | null;
  sortOrder?: number;
  structuredData?: string | null;
  title?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
}

export interface ContentPagesCreatePageSection {
  cssClasses?: string | null;
  data?: string | null;
  heading?: string | null;
  isVisible?: boolean;
  sectionType?: ContentPagesSectionType;
  sortOrder?: number;
  subheading?: string | null;
}

export interface ContentPagesMarketingLead {
  company?: string | null;
  createdAt?: string;
  email?: string | null;
  id?: string;
  locale?: string | null;
  message?: string | null;
  name?: string | null;
  pagePath?: string | null;
  plan?: string | null;
  referrer?: string | null;
  source?: string | null;
  status?: string | null;
  topic?: string | null;
  updatedAt?: string | null;
  userAgent?: string | null;
}

export interface ContentPagesOpenGraphMetadata {
  canonicalUrl?: string | null;
  description?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogTitle?: string | null;
  ogType?: string | null;
  robotsDirective?: string | null;
  slug?: string | null;
  structuredData?: string | null;
  title?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
}

export interface ContentPagesPage {
  body?: string | null;
  canonicalUrl?: string | null;
  createdAt?: string;
  customData?: string | null;
  description?: string | null;
  id?: string;
  locale?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  metaTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogTitle?: string | null;
  ogType?: string | null;
  pageType?: string | null;
  parentPageId?: string | null;
  publishedAt?: string | null;
  robotsDirective?: string | null;
  scheduledPublishAt?: string | null;
  sections?: Array<ContentPagesPageSection> | null;
  slug?: string | null;
  sortOrder?: number;
  status?: string | null;
  structuredData?: string | null;
  title?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
  updatedAt?: string | null;
}

export interface ContentPagesPageSection {
  createdAt?: string;
  cssClasses?: string | null;
  data?: string | null;
  heading?: string | null;
  id?: string;
  isVisible?: boolean;
  pageId?: string;
  sectionType?: string | null;
  sortOrder?: number;
  subheading?: string | null;
  updatedAt?: string | null;
}

export type ContentPagesPageStatus = 'Draft' | 'Published' | 'Archived';

export type ContentPagesPageType = 'Landing' | 'Legal' | 'ResourceIndex' | 'Resource' | 'Custom';

export type ContentPagesSectionType =
  | 'Hero'
  | 'Features'
  | 'Testimonials'
  | 'Pricing'
  | 'CallToAction'
  | 'Faq'
  | 'RichText'
  | 'Gallery'
  | 'Stats'
  | 'Team'
  | 'LogoCloud'
  | 'Newsletter'
  | 'Contact'
  | 'ResourceCards'
  | 'Custom';

export interface ContentPagesSitemapEntry {
  locale?: string | null;
  slug?: string | null;
  updatedAt?: string | null;
}

export interface ContentPagesUpdateContentResource {
  body?: string | null;
  categorySlug?: string | null;
  coverImageUrl?: string | null;
  customData?: string | null;
  downloadUrl?: string | null;
  externalUrl?: string | null;
  isFeatured?: boolean | null;
  linkedEntityId?: string | null;
  linkedEntityType?: string | null;
  locale?: string | null;
  metaDescription?: string | null;
  metaTitle?: string | null;
  ogImageUrl?: string | null;
  readingTimeMinutes?: number | null;
  resourceType?: ContentPagesContentResourceType;
  scheduledPublishAt?: string | null;
  slug?: string | null;
  sortOrder?: number | null;
  status?: ContentPagesContentResourceStatus;
  structuredData?: string | null;
  summary?: string | null;
  tags?: string | null;
  title?: string | null;
  videoUrl?: string | null;
}

export interface ContentPagesUpdatePage {
  body?: string | null;
  canonicalUrl?: string | null;
  customData?: string | null;
  description?: string | null;
  locale?: string | null;
  metaDescription?: string | null;
  metaKeywords?: string | null;
  metaTitle?: string | null;
  ogDescription?: string | null;
  ogImageUrl?: string | null;
  ogTitle?: string | null;
  ogType?: string | null;
  pageType?: ContentPagesPageType;
  parentPageId?: string | null;
  robotsDirective?: string | null;
  scheduledPublishAt?: string | null;
  slug?: string | null;
  sortOrder?: number | null;
  status?: ContentPagesPageStatus;
  structuredData?: string | null;
  title?: string | null;
  twitterCard?: string | null;
  twitterSite?: string | null;
}

export interface ContentPagesUpdatePageSection {
  cssClasses?: string | null;
  data?: string | null;
  heading?: string | null;
  isVisible?: boolean | null;
  sectionType?: ContentPagesSectionType;
  sortOrder?: number | null;
  subheading?: string | null;
}

export interface FeaturesBulkEvaluationInput {
  context?: FeaturesFeatureContext;
  featureKeys?: Array<string> | null;
}

export interface FeaturesCapabilityAuditLog {
  capabilityKey?: string | null;
  changeReason?: string | null;
  changeType?: string | null;
  changedAt?: string;
  changedByUserId?: string | null;
  id?: string;
  newSource?: string | null;
  newValue?: boolean;
  oldSource?: string | null;
  oldValue?: boolean | null;
  tenantId?: string;
}

export interface FeaturesCapabilityCheckOutput {
  capability?: string | null;
  isEnabled?: boolean;
}

export interface FeaturesCreateFeatureInput {
  description?: string | null;
  isEnabled?: boolean;
  key?: string | null;
  name?: string | null;
  tenantId?: string | null;
}

export interface FeaturesFeatureContext {
  country?: string | null;
  customAttributes?: Record<string, Record<string, unknown>> | null;
  environment?: string | null;
  ipAddress?: string | null;
  permissions?: Array<string> | null;
  requestTime?: string;
  subscriptionPlanId?: string | null;
  tenantId?: string | null;
  userAgent?: string | null;
  userId?: string | null;
}

export interface FeaturesFeatureEvaluationInput {
  context?: FeaturesFeatureContext;
  defaultValue?: Record<string, unknown> | null;
  featureKey?: string | null;
}

export interface FeaturesFeatureFlag {
  createdAt: string;
  defaultValue?: Record<string, unknown> | null;
  deletedAt?: string | null;
  description?: string | null;
  environment?: string | null;
  id: string;
  isEnabled: boolean;
  key: string | null;
  name: string | null;
  targets?: Array<FeaturesFeatureFlagTarget> | null;
  tenantId?: string | null;
  type: FeaturesFeatureFlagType;
  updatedAt?: string | null;
}

export interface FeaturesFeatureFlagTarget {
  createdAt: string;
  customValue?: string | null;
  deletedAt?: string | null;
  featureFlagId: string;
  id: string;
  isEnabled: boolean;
  metadata?: string | null;
  priority?: number;
  rolloutPercentage?: number;
  targetIdentifier: string | null;
  targetType: string | null;
  updatedAt?: string | null;
}

export type FeaturesFeatureFlagType = 'Toggle' | 'Numeric' | 'String' | 'Percentage' | 'UserSegment';

export interface FeaturesSetCapabilityOverrideInput {
  capability?: string | null;
  expiresAt?: string | null;
  isEnabled?: boolean;
  reason?: string | null;
  source?: string | null;
}

export interface FeaturesToggleFeatureInput {
  environment?: string | null;
  featureKey?: string | null;
  isEnabled?: boolean;
  reason?: string | null;
  tenantId?: string | null;
}

export interface FeaturesUpdateFeatureInput {
  defaultValue?: string | null;
  description?: string | null;
  enabledValue?: string | null;
  isEnabled?: boolean | null;
  name?: string | null;
  rolloutPercentage?: number | null;
}

export interface Fido2NetLibAssertionOptions {
  allowCredentials?: Array<ObjectsPublicKeyCredentialDescriptor> | null;
  challenge?: string | null;
  extensions?: ObjectsAuthenticationExtensionsClientInputs;
  hints?: Array<ObjectsPublicKeyCredentialHint> | null;
  rpId?: string | null;
  timeout?: number;
  userVerification?: ObjectsUserVerificationRequirement;
}

export interface Fido2NetLibAuthenticatorSelection {
  authenticatorAttachment?: ObjectsAuthenticatorAttachment;
  requireResidentKey?: boolean;
  residentKey?: ObjectsResidentKeyRequirement;
  userVerification?: ObjectsUserVerificationRequirement;
}

export interface Fido2NetLibCredentialCreateOptions {
  attestation?: ObjectsAttestationConveyancePreference;
  attestationFormats?: Array<ObjectsAttestationStatementFormatIdentifier> | null;
  authenticatorSelection?: Fido2NetLibAuthenticatorSelection;
  challenge: string | null;
  excludeCredentials?: Array<ObjectsPublicKeyCredentialDescriptor> | null;
  extensions?: ObjectsAuthenticationExtensionsClientInputs;
  hints?: Array<ObjectsPublicKeyCredentialHint> | null;
  pubKeyCredParams: Array<Fido2NetLibPubKeyCredParam> | null;
  rp: Fido2NetLibPublicKeyCredentialRpEntity;
  timeout?: number;
  user: Fido2NetLibFido2User;
}

export interface Fido2NetLibFido2User {
  displayName?: string | null;
  id?: string | null;
  name?: string | null;
}

export interface Fido2NetLibPubKeyCredParam {
  alg?: ObjectsCOSEAlgorithm;
  type?: ObjectsPublicKeyCredentialType;
}

export interface Fido2NetLibPublicKeyCredentialRpEntity {
  icon?: string | null;
  id?: string | null;
  name?: string | null;
}

export interface GameJamsAddJamCriteriaInput {
  description?: string | null;
  maxScore?: number;
  name?: string | null;
  weight?: number;
}

export interface GameJamsCreateJamInput {
  createdBy?: string;
  description?: string | null;
  endDate?: string;
  maxParticipants?: number | null;
  name?: string | null;
  rules?: string | null;
  slug?: string | null;
  startDate?: string;
  submissionCriteria?: string | null;
  theme?: string | null;
  votingEndDate?: string | null;
}

export interface GameJamsJam {
  createdAt: string;
  createdBy: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  endDate: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  maxParticipants?: number | null;
  name: string;
  participantCount?: number;
  rules?: string | null;
  slug: string;
  startDate: string;
  status: GameJamsJamStatus;
  submissionCriteria?: string | null;
  tenantId?: string | null;
  theme?: string | null;
  updatedAt: string;
  version?: number;
  votingEndDate?: string | null;
}

export interface GameJamsJamCriteria {
  description?: string | null;
  id?: string;
  jamId?: string;
  maxScore?: number;
  name?: string | null;
  weight?: number;
}

export interface GameJamsJamDto {
  createdBy?: string;
  description?: string | null;
  endDate?: string;
  id?: string;
  maxParticipants?: number | null;
  name?: string | null;
  participantCount?: number;
  slug?: string | null;
  startDate?: string;
  status?: GameJamsJamStatus;
  theme?: string | null;
  votingEndDate?: string | null;
}

export interface GameJamsJamScore {
  createdAt: string;
  criteriaId: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  feedback?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  judgeUserId: string;
  score: number;
  submissionId: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface GameJamsJamScoreDto {
  criteriaId?: string;
  feedback?: string | null;
  id?: string;
  judgeUserId?: string;
  score?: number;
  submissionId?: string;
}

export type GameJamsJamStatus = 'Upcoming' | 'Active' | 'Voting' | 'Completed' | 'Cancelled';

export interface GameJamsJamSubmission {
  id?: string;
  jamId?: string;
  projectVersionId?: string;
  submissionNotes?: string | null;
  userId?: string;
}

export interface GameJamsScoreJamSubmissionInput {
  criteriaId?: string;
  feedback?: string | null;
  judgeUserId?: string;
  score?: number;
}

export interface GameJamsSubmitJamEntryInput {
  notes?: string | null;
  projectVersionId?: string;
  userId?: string;
}

export interface IdentityAuthenticationApiKey {
  createdAt?: string;
  expiresAt?: string | null;
  id?: string;
  isActive?: boolean;
  keyPrefix?: string | null;
  lastUsedAt?: string | null;
  name?: string | null;
  scopes?: Array<string> | null;
  usageCount?: number;
}

export interface IdentityAuthenticationAssignRoleToUserInput {
  expiresAt?: string | null;
  roleId?: string;
  userId?: string;
}

export interface IdentityAuthenticationBackupCodesOutput {
  codes?: Array<string> | null;
  generatedAt?: string;
}

export interface IdentityAuthenticationBackupCodesStatusOutput {
  hasBackupCodes: boolean;
  remainingCount: number;
  totalCount: number;
  usedCount: number;
}

export interface IdentityAuthenticationBeginWebAuthnAuthenticationInput {
  email?: string | null;
}

export interface IdentityAuthenticationBeginWebAuthnRegistrationInput {
  displayName?: string | null;
  email?: string | null;
  preferredAuthenticatorType?: IdentityAuthenticationWebAuthnAuthenticatorType;
}

export interface IdentityAuthenticationCleanupKeysInput {
  retentionDays?: number | null;
}

export interface IdentityAuthenticationCleanupResult {
  deletedCount?: number;
}

export interface IdentityAuthenticationClientCredentialsTokenOutput {
  accessToken?: string | null;
  expiresIn?: number;
  scope?: string | null;
  tokenType?: string | null;
}

export interface IdentityAuthenticationCompleteMfaSetupInput {
  code: string;
  secretKey: string;
}

export interface IdentityAuthenticationCompletePasswordResetInput {
  confirmPassword: string;
  newPassword: string;
  tenantId?: string | null;
  token: string;
}

export interface IdentityAuthenticationCompleteWebAuthnAuthenticationInput {
  assertionResponse?: string | null;
}

export interface IdentityAuthenticationCompleteWebAuthnRegistrationInput {
  attestationResponse?: string | null;
  friendlyName?: string | null;
  isPasswordless?: boolean;
}

export interface IdentityAuthenticationConsumeMagicLinkInput {
  deviceFingerprint?: string | null;
  tenantId?: string | null;
  token: string;
}

export interface IdentityAuthenticationCreateApiKeyCommand {
  expiresAt?: string | null;
  ipWhitelist?: string | null;
  name: string | null;
  scopes: Array<string> | null;
}

export interface IdentityAuthenticationCreateApiKeyOutput {
  apiKey?: string | null;
  createdAt?: string;
  expiresAt?: string | null;
  id?: string;
  keyPrefix?: string | null;
  name?: string | null;
  scopes?: Array<string> | null;
}

export interface IdentityAuthenticationCreateRoleInput {
  description?: string | null;
  name?: string | null;
  permissions?: Array<string> | null;
  tenantId?: string | null;
}

export interface IdentityAuthenticationCreateServiceAccountInput {
  allowedIpAddresses?: string | null;
  description?: string | null;
  expiresAt?: string | null;
  name?: string | null;
  scopes?: string | null;
  tenantId?: string | null;
}

export interface IdentityAuthenticationDeviceInfo {
  browser?: string | null;
  browserVersion?: string | null;
  deviceId?: string | null;
  deviceName?: string | null;
  deviceType?: string | null;
  fingerprint?: string | null;
  ipAddress?: string | null;
  isBot?: boolean;
  isMobile?: boolean;
  language?: string | null;
  operatingSystem?: string | null;
  osVersion?: string | null;
  screenResolution?: string | null;
  timezone?: string | null;
  userAgent?: string | null;
}

export interface IdentityAuthenticationDisableMfaInput {
  password: string;
}

export interface IdentityAuthenticationEmailVerificationOutput {
  message: string | null;
}

export interface IdentityAuthenticationEmailVerificationResult {
  email?: string | null;
  message?: string | null;
  success?: boolean;
  userId?: string | null;
  verifiedAt?: string | null;
}

export interface IdentityAuthenticationGitHubSignInOutput {
  authUrl: string | null;
}

export interface IdentityAuthenticationGoogleIdTokenInput {
  idToken: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationJwtKeyInfo {
  algorithm?: string | null;
  expiresAt?: string;
  isActive?: boolean;
  keyId?: string | null;
  keyVersion?: number;
  rotatedAt?: string | null;
  rotationReason?: string | null;
  validFrom?: string;
}

export interface IdentityAuthenticationLocalSignInInput {
  deviceFingerprint?: string | null;
  email: string;
  emailOrUsername?: string | null;
  password: string;
  tenantId?: string | null;
  username?: string | null;
}

export interface IdentityAuthenticationLocalSignUpInput {
  email: string;
  firstName?: string | null;
  lastName?: string | null;
  password: string;
  phoneNumber?: string | null;
  tenantId?: string | null;
  username: string;
}

export interface IdentityAuthenticationLocationInfo {
  city?: string | null;
  country?: string | null;
  countryCode?: string | null;
  displayLocation?: string | null;
  ipAddress?: string | null;
  isHosting?: boolean | null;
  isProxy?: boolean | null;
  isp?: string | null;
  latitude?: number | null;
  longitude?: number | null;
  organization?: string | null;
  postalCode?: string | null;
  region?: string | null;
  timezone?: string | null;
}

export interface IdentityAuthenticationLockServiceAccountInput {
  reason?: string | null;
}

export interface IdentityAuthenticationMagicLinkRequestResult {
  developmentPreviewToken?: string | null;
  expiresInMinutes?: number;
  message?: string | null;
  success?: boolean;
}

export interface IdentityAuthenticationMfaConfigurationOutput {
  backupCodesRemaining?: number;
  enabledAt?: string | null;
  enabledMethods?: Array<string> | null;
  isEnabled?: boolean;
}

export interface IdentityAuthenticationMfaErrorOutput {
  error: string | null;
}

export type IdentityAuthenticationMfaMethod = 'Totp' | 'BackupCode' | 'Sms' | 'Email' | 'WebAuthn';

export interface IdentityAuthenticationMfaMethodInfo {
  description: string | null;
  isAvailable: boolean;
  isEnabled: boolean;
  method: IdentityAuthenticationMfaMethod;
  name: string | null;
  priority: number;
}

export interface IdentityAuthenticationMfaMethodsOutput {
  defaultMethod?: IdentityAuthenticationMfaMethod;
  methods: Array<IdentityAuthenticationMfaMethodInfo> | null;
}

export interface IdentityAuthenticationMfaSetupOutput {
  backupCodes?: Array<string> | null;
  errorMessage?: string | null;
  isSuccess?: boolean;
  qrCodeData?: string | null;
  qrCodeUri?: string | null;
  secretKey?: string | null;
}

export interface IdentityAuthenticationMfaSuccessOutput {
  message: string | null;
}

export interface IdentityAuthenticationMfaVerificationOutput {
  accessToken?: string | null;
  isValid?: boolean;
  refreshToken?: string | null;
}

export interface IdentityAuthenticationOAuth2ErrorOutput {
  error?: string | null;
  errorDescription?: string | null;
}

export interface IdentityAuthenticationPasswordChangeInput {
  confirmPassword: string;
  currentPassword: string;
  newPassword: string;
  revokeOtherSessions?: boolean;
}

export interface IdentityAuthenticationPasswordChangeResult {
  message?: string | null;
  sessionsRevoked?: number;
  success?: boolean;
}

export interface IdentityAuthenticationPasswordResetRequestResult {
  expiresInMinutes?: number;
  message?: string | null;
  success?: boolean;
}

export interface IdentityAuthenticationPasswordResetResult {
  message?: string | null;
  success?: boolean;
  userId?: string | null;
}

export interface IdentityAuthenticationPatchServiceAccountInput {
  description?: string | null;
  expiresAt?: string | null;
  name?: string | null;
  scopes?: string | null;
}

export interface IdentityAuthenticationRefreshTokenInput {
  refreshToken: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRemoveRoleFromUserInput {
  roleId?: string;
  userId?: string;
}

export interface IdentityAuthenticationRequestMagicLinkInput {
  email: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRequestPasswordResetInput {
  email: string;
  tenantId?: string | null;
}

export interface IdentityAuthenticationRevokeApiKeyInput {
  reason?: string | null;
}

export interface IdentityAuthenticationRevokeRefreshTokenInput {
  ipAddress?: string | null;
  reason?: string | null;
  token: string;
}

export type IdentityAuthenticationRiskLevel = 'Low' | 'Medium' | 'High' | 'Critical';

export interface IdentityAuthenticationRotateKeyInput {
  reason?: string | null;
  validityDays?: number | null;
}

export interface IdentityAuthenticationSecretRotationOutput {
  clientSecret?: string | null;
  warning?: string | null;
}

export interface IdentityAuthenticationSendEmailVerificationInput {
  email: string;
}

export interface IdentityAuthenticationServiceAccountAuditEntry {
  action?: string | null;
  details?: string | null;
  id?: string;
  ipAddress?: string | null;
  performedBy?: string | null;
  timestamp?: string;
}

export interface IdentityAuthenticationServiceAccountAuditLogOutput {
  entries?: Array<IdentityAuthenticationServiceAccountAuditEntry> | null;
  page?: number;
  pageSize?: number;
  serviceAccountId?: string;
  totalCount?: number;
}

export interface IdentityAuthenticationServiceAccountCreatedOutput {
  clientId?: string | null;
  clientSecret?: string | null;
  createdAt?: string;
  description?: string | null;
  expiresAt?: string | null;
  id?: string;
  name?: string | null;
  scopes?: string | null;
  tenantId?: string | null;
  warning?: string | null;
}

export interface IdentityAuthenticationServiceAccountOutput {
  authenticationCount?: number;
  clientId?: string | null;
  createdAt?: string;
  createdBy?: string | null;
  description?: string | null;
  expiresAt?: string | null;
  id?: string;
  isActive?: boolean;
  isLocked?: boolean;
  lastAuthenticatedAt?: string | null;
  name?: string | null;
  scopes?: string | null;
  secretRotationCount?: number;
  tenantId?: string | null;
}

export interface IdentityAuthenticationSessionOutput {
  createdAt?: string;
  deviceInfo?: IdentityAuthenticationDeviceInfo;
  expiresAt?: string;
  id?: string;
  ipAddress?: string | null;
  isCurrent?: boolean;
  isTrustedDevice?: boolean;
  lastUsedAt?: string;
  location?: IdentityAuthenticationLocationInfo;
}

export interface IdentityAuthenticationSessionSecurityAnalysis {
  activeSessionCount?: number;
  analyzedAt?: string;
  isSuspicious?: boolean;
  metadata?: Record<string, string> | null;
  riskFactors?: Array<string> | null;
  riskLevel?: IdentityAuthenticationRiskLevel;
  riskScore?: number;
  securityFlags?: Array<string> | null;
  sessionId?: string;
  totalDeviceCount?: number;
  unusualActivityDetected?: boolean;
  userId?: string;
}

export interface IdentityAuthenticationSessionSuccessOutput {
  message: string | null;
}

export interface IdentityAuthenticationSessionTerminationOutput {
  message: string | null;
  terminatedCount: number;
}

export interface IdentityAuthenticationSignInOutput {
  accessToken?: string | null;
  accessTokenExpiresAt?: string;
  availableMethods?: Array<string> | null;
  availableTenants?: Array<TenantInfo> | null;
  email?: string | null;
  expiresAt?: string;
  expiresIn?: number;
  message?: string | null;
  mfaSessionId?: string | null;
  mfaToken?: string | null;
  refreshToken?: string | null;
  refreshTokenExpiresAt?: string;
  requiresMfa?: boolean;
  requiresStepUp?: boolean;
  riskFactors?: Array<string> | null;
  riskLevel?: IdentityAuthenticationRiskLevel;
  sessionId?: string;
  stepUpExpiresAt?: string | null;
  stepUpToken?: string | null;
  success?: boolean;
  tempToken?: string | null;
  tenantId?: string | null;
  user?: IdentityAuthenticationUser;
  userId?: string;
}

export interface IdentityAuthenticationSmsMfaSetupInput {
  phoneNumber: string | null;
}

export interface IdentityAuthenticationSmsMfaSetupOutput {
  expiresInSeconds: number;
  message: string | null;
  phoneNumberMasked: string | null;
}

export interface IdentityAuthenticationTrustDeviceInput {
  deviceName?: string | null;
}

export interface IdentityAuthenticationTrustedDeviceOutput {
  deviceInfo?: IdentityAuthenticationDeviceInfo;
  deviceName?: string | null;
  expiresAt?: string | null;
  id?: string;
  lastUsedAt?: string;
  trustedAt?: string;
}

export interface IdentityAuthenticationUpdateCredentialNameInput {
  friendlyName?: string | null;
}

export interface IdentityAuthenticationUpdateRoleInput {
  description?: string | null;
  isActive?: boolean | null;
  name?: string | null;
  permissions?: Array<string> | null;
}

export interface IdentityAuthenticationUpdateScopesInput {
  scopes?: string | null;
}

export interface IdentityAuthenticationUser {
  createdAt?: string;
  email?: string | null;
  emailVerified?: boolean;
  firstName?: string | null;
  id?: string;
  lastLoginAt?: string | null;
  lastName?: string | null;
  phoneNumber?: string | null;
  phoneNumberVerified?: boolean;
  username?: string | null;
}

export interface IdentityAuthenticationVerifyEmailInput {
  tenantId?: string | null;
  token: string;
}

export interface IdentityAuthenticationVerifyMfaInput {
  code: string;
  method?: IdentityAuthenticationMfaMethod;
  userId?: string;
}

export interface IdentityAuthenticationWeb3ChallengeInput {
  chainId?: string | null;
  walletAddress: string;
}

export interface IdentityAuthenticationWeb3ChallengeOutput {
  challenge?: string | null;
  expiresAt?: string;
  nonce?: string | null;
}

export interface IdentityAuthenticationWeb3VerifyInput {
  chainId: string;
  deviceFingerprint?: string | null;
  nonce: string;
  signature: string;
  tenantId?: string | null;
  walletAddress: string;
}

export interface IdentityAuthenticationWebAuthnAuthenticationOptionsResult {
  error?: string | null;
  options?: Fido2NetLibAssertionOptions;
  optionsJson?: string | null;
  sessionId?: string | null;
  success?: boolean;
}

export interface IdentityAuthenticationWebAuthnAuthenticationResult {
  accessToken?: string | null;
  accessTokenExpiresAt?: string | null;
  credentialId?: string | null;
  email?: string | null;
  error?: string | null;
  expiresIn?: number;
  isPasswordless?: boolean;
  refreshToken?: string | null;
  refreshTokenExpiresAt?: string | null;
  success?: boolean;
  userId?: string | null;
}

export type IdentityAuthenticationWebAuthnAuthenticatorType = 'Platform' | 'CrossPlatform';

export interface IdentityAuthenticationWebAuthnCredentialInfo {
  authenticatorType?: IdentityAuthenticationWebAuthnAuthenticatorType;
  backedUp?: boolean;
  createdAt?: string;
  friendlyName?: string | null;
  id?: string;
  isDefault?: boolean;
  isPasswordless?: boolean;
  lastUsedAt?: string | null;
}

export interface IdentityAuthenticationWebAuthnCredentialVerifyResult {
  error?: string | null;
  isExpired?: boolean;
  isRevoked?: boolean;
  isValid?: boolean;
  lastUsedAt?: string | null;
  signatureCount?: number;
  success?: boolean;
}

export interface IdentityAuthenticationWebAuthnRegistrationOptionsResult {
  error?: string | null;
  options?: Fido2NetLibCredentialCreateOptions;
  optionsJson?: string | null;
  sessionId?: string | null;
  success?: boolean;
}

export interface IdentityAuthenticationWebAuthnRegistrationResult {
  credentialId?: string | null;
  error?: string | null;
  friendlyName?: string | null;
  success?: boolean;
}

export interface IdentityAuthenticationWebAuthnStatusOutput {
  credentialCount?: number;
  hasPasswordlessCredential?: boolean;
  hasPlatformAuthenticator?: boolean;
  hasSecurityKey?: boolean;
  isEnabled?: boolean;
}

export type IdentityAuthorizationPermissionType =
  | 'Read'
  | 'Comment'
  | 'Reply'
  | 'Vote'
  | 'Share'
  | 'Report'
  | 'Follow'
  | 'Bookmark'
  | 'React'
  | 'Subscribe'
  | 'Mention'
  | 'Tag'
  | 'Categorize'
  | 'Collection'
  | 'Series'
  | 'CrossReference'
  | 'Translate'
  | 'Version'
  | 'Template'
  | 'Create'
  | 'Draft'
  | 'Submit'
  | 'Withdraw'
  | 'Archive'
  | 'Restore'
  | 'Delete'
  | 'HardDelete'
  | 'Backup'
  | 'Migrate'
  | 'Clone'
  | 'Edit'
  | 'Proofread'
  | 'FactCheck'
  | 'StyleGuide'
  | 'Plagiarism'
  | 'Seo'
  | 'Accessibility'
  | 'Legal'
  | 'Brand'
  | 'Guidelines'
  | 'Approve'
  | 'Reject'
  | 'RequestRevision'
  | 'Escalate'
  | 'Override'
  | 'Delegate'
  | 'FastTrack'
  | 'BatchApprove'
  | 'ConditionalApprove'
  | 'RequireReview'
  | 'Publish'
  | 'Unpublish'
  | 'Schedule'
  | 'SetPublishDate'
  | 'Visibility'
  | 'Feature'
  | 'Pin'
  | 'Sticky'
  | 'Highlight'
  | 'Promote'
  | 'Moderate'
  | 'Hide'
  | 'Flag'
  | 'Warn'
  | 'Suspend'
  | 'Ban'
  | 'Quarantine'
  | 'Review'
  | 'Investigate'
  | 'EscalateModeration'
  | 'Invite'
  | 'Assign'
  | 'Collaborate'
  | 'CoAuthor'
  | 'Contribute'
  | 'Suggest'
  | 'Track'
  | 'Merge'
  | 'Resolve'
  | 'Coordinate'
  | 'Score'
  | 'Rate'
  | 'Benchmark'
  | 'Metrics'
  | 'Analytics'
  | 'Performance'
  | 'Feedback'
  | 'Audit'
  | 'Standards'
  | 'Improvement'
  | 'Monetize'
  | 'Pricing'
  | 'Paywall'
  | 'Manage'
  | 'Admin'
  | 'Execute'
  | 'Export'
  | 'Import'
  | 'SystemAdmin'
  | 'TenantAdmin'
  | 'UserManagement'
  | 'Configure';

export interface IdentityTenantsAddTenantMemberOutput {
  memberId?: string | null;
  message?: string | null;
  success?: boolean;
}

export interface IdentityTenantsAddUserMembershipInput {
  invitedByEmail?: string | null;
  inviteeEmail?: string | null;
  inviteeName?: string | null;
  requiresAcceptance?: boolean;
  role?: string | null;
  tenantId?: string;
}

export interface IdentityTenantsArchiveInput {
  reason?: string | null;
}

export interface IdentityTenantsBulkActivateTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkArchiveTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkCreateTenantItem {
  adminEmail?: string | null;
  description?: string | null;
  name?: string | null;
  slug?: string | null;
}

export interface IdentityTenantsBulkCreateTenantsCommand {
  tenants?: Array<IdentityTenantsBulkCreateTenantItem> | null;
}

export interface IdentityTenantsBulkDeactivateTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkDeleteTenantsCommand {
  hardDelete?: boolean;
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkPurgeTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkUndeleteTenantsCommand {
  tenantIds?: Array<string> | null;
}

export interface IdentityTenantsBulkUpdateTenantItem {
  description?: string | null;
  name?: string | null;
  tenantId?: string;
}

export interface IdentityTenantsBulkUpdateTenantsCommand {
  updates?: Array<IdentityTenantsBulkUpdateTenantItem> | null;
}

export interface IdentityTenantsCreateTenantInput {
  adminEmail?: string | null;
  description?: string | null;
  name?: string | null;
  slug?: string | null;
}

export interface IdentityTenantsGetUserMembershipsOutput {
  memberships?: Array<IdentityTenantsUserMembership> | null;
  totalCount?: number;
}

export interface IdentityTenantsMembershipCountOutput {
  count?: number;
}

export interface IdentityTenantsRecoverInput {
  reason?: string | null;
}

export interface IdentityTenantsReplaceTenantMetadataInput {
  adminNotes?: string | null;
  businessInfo?: IdentityTenantsUpdateTenantBusinessInfoInput;
  contactInfo?: IdentityTenantsUpdateTenantContactInfoInput;
  customFields?: Record<string, Record<string, unknown> | null> | null;
  externalReferences?: Record<string, string> | null;
  tags?: Array<string> | null;
}

export interface IdentityTenantsReplaceTenantSettingsInput {
  businessRules?: IdentityTenantsUpdateTenantBusinessRulesInput;
  featureFlags?: Record<string, boolean> | null;
  integrationSettings?: IdentityTenantsUpdateTenantIntegrationSettingsInput;
  securitySettings?: IdentityTenantsUpdateTenantSecuritySettingsInput;
  systemConfiguration?: IdentityTenantsUpdateTenantSystemConfigurationInput;
  systemLimits?: IdentityTenantsUpdateTenantSystemLimitsInput;
  userInterfaceSettings?: IdentityTenantsUpdateTenantUiSettingsInput;
}

export interface IdentityTenantsSlugValidation {
  isAvailable?: boolean;
  isValid?: boolean;
  suggestedAlternatives?: Array<string> | null;
}

export interface IdentityTenantsTenant {
  activeMemberCount?: number;
  adminEmail?: string | null;
  archivedAt?: string | null;
  canAcceptMembers?: boolean;
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  hasActiveMembers?: boolean;
  id?: string;
  isActive?: boolean;
  isArchived?: boolean;
  isDefault?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  name: string;
  slug: string;
  tenantDomains?: Array<IdentityTenantsTenantDomain> | null;
  tenantId?: string | null;
  tenantMembers?: Array<IdentityTenantsTenantMember> | null;
  tenantSettings?: IdentityTenantsTenantSettings;
  tenantStatistics?: IdentityTenantsTenantStatistics;
  updatedAt: string;
  usageTrackingRecords?: Array<IdentityTenantsUsageTracking> | null;
  version?: number;
}

export interface IdentityTenantsTenantAddress {
  city?: string | null;
  country?: string | null;
  postalCode?: string | null;
  state?: string | null;
  street?: string | null;
}

export interface IdentityTenantsTenantAuditLogEntry {
  action?: string | null;
  actorEmail?: string | null;
  actorId?: string | null;
  actorName?: string | null;
  afterValues?: Record<string, Record<string, unknown> | null> | null;
  beforeValues?: Record<string, Record<string, unknown> | null> | null;
  correlationId?: string | null;
  id?: string;
  ipAddress?: string | null;
  metadata?: Record<string, string> | null;
  tenantId?: string;
  timestamp?: string;
  userAgent?: string | null;
}

export interface IdentityTenantsTenantBranding {
  companyName?: string | null;
  faviconUrl?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

export interface IdentityTenantsTenantBusinessInfo {
  complianceRequirements?: Array<string> | null;
  geographicRegion?: string | null;
  industry?: string | null;
  organizationSize?: string | null;
  tenantType?: string | null;
}

export interface IdentityTenantsTenantBusinessRules {
  approvalRules?: Record<string, Record<string, unknown> | null> | null;
  notificationRules?: Record<string, Record<string, unknown> | null> | null;
  validationRules?: Record<string, Record<string, unknown> | null> | null;
  workflowRules?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantContactInfo {
  address?: IdentityTenantsTenantAddress;
  organizationName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
  website?: string | null;
}

export interface IdentityTenantsTenantCurrencySettings {
  decimalPlaces?: number;
  defaultCurrency?: string | null;
  displayFormat?: string | null;
}

export interface IdentityTenantsTenantDomain {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  fullDomain?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isMainDomain?: boolean;
  isNew?: boolean;
  isSecondaryDomain?: boolean;
  subdomain?: string | null;
  tenant?: IdentityTenantsTenant;
  tenantId: string;
  topLevelDomain: string;
  updatedAt: string;
  userGroupId?: string | null;
  version?: number;
}

export interface IdentityTenantsTenantIntegrationSettings {
  apiKeys?: Record<string, string> | null;
  externalServices?: Record<string, Record<string, unknown> | null> | null;
  ssoConfiguration?: Record<string, Record<string, unknown> | null> | null;
  webhookSettings?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsTenantMember {
  childMembers?: Array<IdentityTenantsTenantMember> | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  joinedAt?: string;
  leaveReason?: string | null;
  leftAt?: string | null;
  metadata?: string | null;
  parentMember?: IdentityTenantsTenantMember;
  parentMemberId?: string | null;
  role: string;
  tenant?: IdentityTenantsTenant;
  tenantId: string;
  updatedAt: string;
  userId: string;
  version?: number;
}

export interface IdentityTenantsTenantMetadata {
  adminNotes?: string | null;
  businessInfo?: IdentityTenantsTenantBusinessInfo;
  contactInfo?: IdentityTenantsTenantContactInfo;
  createdAt?: string;
  customFields?: Record<string, Record<string, unknown> | null> | null;
  externalReferences?: Record<string, string> | null;
  id?: string;
  tags?: Array<string> | null;
  updatedAt?: string;
}

export interface IdentityTenantsTenantSecuritySettings {
  apiRateLimits?: Record<string, number> | null;
  ipWhitelist?: Array<string> | null;
  passwordPolicy?: Record<string, Record<string, unknown> | null> | null;
  sessionTimeout?: number;
  twoFactorRequired?: boolean;
}

export interface IdentityTenantsTenantSettings {
  allowUserRegistration?: boolean;
  brandingSettings?: string | null;
  createdAt: string;
  defaultCurrency?: string | null;
  defaultLanguage?: string | null;
  defaultTimezone?: string | null;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  enableApiAccess?: boolean;
  enableAuditLogging?: boolean;
  id?: string;
  integrationSettingsJson?: string | null;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  maxUsers?: number | null;
  notificationSettings?: string | null;
  requireRegistrationApproval?: boolean;
  requireTwoFactorAuth?: boolean;
  securitySettings?: string | null;
  storageQuota?: number | null;
  tenant?: IdentityTenantsTenant;
  tenantId: string;
  updatedAt: string;
  version?: number;
}

export interface IdentityTenantsTenantSettingsDto {
  businessRules?: IdentityTenantsTenantBusinessRules;
  createdAt?: string;
  featureFlags?: Record<string, boolean> | null;
  id?: string;
  integrationSettings?: IdentityTenantsTenantIntegrationSettings;
  securitySettings?: IdentityTenantsTenantSecuritySettings;
  systemConfiguration?: IdentityTenantsTenantSystemConfiguration;
  systemLimits?: IdentityTenantsTenantSystemLimits;
  updatedAt?: string;
  userInterfaceSettings?: IdentityTenantsTenantUiSettings;
}

export interface IdentityTenantsTenantStatistics {
  activeMembers?: number;
  apiCalls?: number;
  createdAt: string;
  customMetrics?: string | null;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  inactiveMembers?: number;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  membersLeft?: number;
  newMembers?: number;
  statisticDate?: string;
  storageUsed?: number;
  tenant?: IdentityTenantsTenant;
  tenantId: string;
  totalMembers?: number;
  updatedAt: string;
  version?: number;
}

export interface IdentityTenantsTenantSystemConfiguration {
  currencySettings?: IdentityTenantsTenantCurrencySettings;
  customConfiguration?: Record<string, Record<string, unknown> | null> | null;
  dateFormat?: string | null;
  locale?: string | null;
  numberFormat?: string | null;
  timeZone?: string | null;
}

export interface IdentityTenantsTenantSystemLimits {
  customLimits?: Record<string, number> | null;
  maxApiCalls?: number;
  maxProjects?: number;
  maxStorage?: number;
  maxUsers?: number;
}

export interface IdentityTenantsTenantUiSettings {
  branding?: IdentityTenantsTenantBranding;
  componentSettings?: Record<string, Record<string, unknown> | null> | null;
  customCss?: string | null;
  layout?: Record<string, Record<string, unknown> | null> | null;
  theme?: string | null;
}

export interface IdentityTenantsTenantValidationError {
  code?: string | null;
  field?: string | null;
  message?: string | null;
}

export interface IdentityTenantsTenantValidationOutput {
  errors?: Array<IdentityTenantsTenantValidationError> | null;
  isValid?: boolean;
  slugValidation?: IdentityTenantsSlugValidation;
  suggestions?: Array<string> | null;
  warnings?: Array<IdentityTenantsTenantValidationWarning> | null;
}

export interface IdentityTenantsTenantValidationWarning {
  code?: string | null;
  field?: string | null;
  message?: string | null;
}

export interface IdentityTenantsUpdateTenantAddressInput {
  city?: string | null;
  country?: string | null;
  postalCode?: string | null;
  state?: string | null;
  street?: string | null;
}

export interface IdentityTenantsUpdateTenantBrandingInput {
  companyName?: string | null;
  faviconUrl?: string | null;
  logoUrl?: string | null;
  primaryColor?: string | null;
  secondaryColor?: string | null;
}

export interface IdentityTenantsUpdateTenantBusinessInfoInput {
  complianceRequirements?: Array<string> | null;
  geographicRegion?: string | null;
  industry?: string | null;
  organizationSize?: string | null;
  tenantType?: string | null;
}

export interface IdentityTenantsUpdateTenantBusinessRulesInput {
  approvalRules?: Record<string, Record<string, unknown> | null> | null;
  notificationRules?: Record<string, Record<string, unknown> | null> | null;
  validationRules?: Record<string, Record<string, unknown> | null> | null;
  workflowRules?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateTenantContactInfoInput {
  address?: IdentityTenantsUpdateTenantAddressInput;
  organizationName?: string | null;
  primaryContactEmail?: string | null;
  primaryContactName?: string | null;
  primaryContactPhone?: string | null;
  website?: string | null;
}

export interface IdentityTenantsUpdateTenantCurrencySettingsInput {
  decimalPlaces?: number | null;
  defaultCurrency?: string | null;
  displayFormat?: string | null;
}

export interface IdentityTenantsUpdateTenantFeatureFlagsInput {
  featureFlags?: Record<string, boolean> | null;
}

export interface IdentityTenantsUpdateTenantIntegrationSettingsInput {
  apiKeys?: Record<string, string> | null;
  externalServices?: Record<string, Record<string, unknown> | null> | null;
  ssoConfiguration?: Record<string, Record<string, unknown> | null> | null;
  webhookSettings?: Record<string, Record<string, unknown> | null> | null;
}

export interface IdentityTenantsUpdateTenantMemberInviteOutput {
  inviteStatus?: string | null;
  memberId?: string | null;
  message?: string | null;
  success?: boolean;
}

export interface IdentityTenantsUpdateTenantMemberRoleOutput {
  memberId?: string;
  message?: string | null;
  newRole?: string | null;
  success?: boolean;
}

export interface IdentityTenantsUpdateTenantMetadataInput {
  adminNotes?: string | null;
  businessInfo?: IdentityTenantsUpdateTenantBusinessInfoInput;
  contactInfo?: IdentityTenantsUpdateTenantContactInfoInput;
  customFields?: Record<string, Record<string, unknown> | null> | null;
  externalReferences?: Record<string, string> | null;
  tags?: Array<string> | null;
}

export interface IdentityTenantsUpdateTenantInput {
  description?: string | null;
  name?: string | null;
}

export interface IdentityTenantsUpdateTenantSecuritySettingsInput {
  apiRateLimits?: Record<string, number> | null;
  ipWhitelist?: Array<string> | null;
  passwordPolicy?: Record<string, Record<string, unknown> | null> | null;
  sessionTimeout?: number | null;
  twoFactorRequired?: boolean | null;
}

export interface IdentityTenantsUpdateTenantSettingsInput {
  businessRules?: IdentityTenantsUpdateTenantBusinessRulesInput;
  featureFlags?: Record<string, boolean> | null;
  integrationSettings?: IdentityTenantsUpdateTenantIntegrationSettingsInput;
  securitySettings?: IdentityTenantsUpdateTenantSecuritySettingsInput;
  systemConfiguration?: IdentityTenantsUpdateTenantSystemConfigurationInput;
  systemLimits?: IdentityTenantsUpdateTenantSystemLimitsInput;
  userInterfaceSettings?: IdentityTenantsUpdateTenantUiSettingsInput;
}

export interface IdentityTenantsUpdateTenantSystemConfigurationInput {
  currencySettings?: IdentityTenantsUpdateTenantCurrencySettingsInput;
  customConfiguration?: Record<string, Record<string, unknown> | null> | null;
  dateFormat?: string | null;
  locale?: string | null;
  numberFormat?: string | null;
  timeZone?: string | null;
}

export interface IdentityTenantsUpdateTenantSystemLimitsInput {
  customLimits?: Record<string, number> | null;
  maxApiCalls?: number | null;
  maxProjects?: number | null;
  maxStorage?: number | null;
  maxUsers?: number | null;
}

export interface IdentityTenantsUpdateTenantTagsInput {
  tags?: Array<string> | null;
}

export interface IdentityTenantsUpdateTenantUiSettingsInput {
  branding?: IdentityTenantsUpdateTenantBrandingInput;
  componentSettings?: Record<string, Record<string, unknown> | null> | null;
  customCss?: string | null;
  layout?: Record<string, Record<string, unknown> | null> | null;
  theme?: string | null;
}

export interface IdentityTenantsUpdateUserMembershipInviteInput {
  actorEmail?: string | null;
}

export interface IdentityTenantsUpdateUserMembershipRoleInput {
  role?: string | null;
}

export interface IdentityTenantsUsageTracking {
  cost?: number;
  createdAt: string;
  date?: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  metadata?: string | null;
  resourceType: string;
  tenant?: IdentityTenantsTenant;
  tenantId: string;
  unit?: string | null;
  updatedAt: string;
  usageAmount?: number;
  version?: number;
}

export interface IdentityTenantsUserMembership {
  acceptedAt?: string | null;
  cancelledAt?: string | null;
  inviteResendCount?: number;
  inviteStatus?: string | null;
  invitedAt?: string | null;
  invitedByEmail?: string | null;
  inviteeEmail?: string | null;
  inviteeName?: string | null;
  isActive?: boolean;
  joinedAt?: string;
  lastInviteSentAt?: string | null;
  leftAt?: string | null;
  membershipId?: string;
  role?: string | null;
  tenantDescription?: string | null;
  tenantId?: string;
  tenantIsActive?: boolean;
  tenantName?: string | null;
  tenantSlug?: string | null;
}

export interface IdentityTenantsValidateTenantInput {
  adminEmail?: string | null;
  name?: string | null;
  slug?: string | null;
}

export interface IdentityUsersBulkActivateUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkActivateUsersOutput {
  activatedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkCreateUsersInput {
  users?: Array<IdentityUsersCreateUserRequestItem> | null;
}

export interface IdentityUsersBulkCreateUsersOutput {
  createdUserIds?: Array<string> | null;
  failedEmails?: Array<string> | null;
}

export interface IdentityUsersBulkDeactivateUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkDeactivateUsersOutput {
  deactivatedUsers?: Array<IdentityUsersUserDto> | null;
  failedUserIds?: Array<string> | null;
}

export interface IdentityUsersBulkDeleteUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkNotificationInput {
  filterCriteria?: IdentityUsersNotificationFilterCriteria;
  notificationIds?: Array<string> | null;
  operation?: string | null;
}

export interface IdentityUsersBulkPurgeUsersInput {
  strategy?: IdentityUsersPurgeStrategy;
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkRestoreUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkRestoreUsersOutput {
  failedUserIds?: Array<string> | null;
  restoredUsers?: Array<IdentityUsersUserDto> | null;
}

export interface IdentityUsersBulkSuspendUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkSuspendUsersOutput {
  failedUserIds?: Array<string> | null;
  suspendedUsers?: Array<IdentityUsersUserDto> | null;
}

export interface IdentityUsersBulkUnsuspendUsersInput {
  userIds?: Array<string> | null;
}

export interface IdentityUsersBulkUnsuspendUsersOutput {
  failedUserIds?: Array<string> | null;
  unsuspendedUsers?: Array<IdentityUsersUserDto> | null;
}

export interface IdentityUsersBulkUpdateUsersInput {
  updates?: Array<IdentityUsersUpdateUserRequestItem> | null;
}

export interface IdentityUsersCreateUserInput {
  email?: string | null;
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersCreateUserRequestItem {
  email?: string | null;
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersNotificationAction {
  id?: string | null;
  isPrimary?: boolean;
  text?: string | null;
  type?: string | null;
  url?: string | null;
}

export interface IdentityUsersNotificationFilterCriteria {
  categories?: Array<string> | null;
  dateFrom?: string | null;
  dateTo?: string | null;
  isArchived?: boolean | null;
  isRead?: boolean | null;
  priorities?: Array<string> | null;
  types?: Array<string> | null;
}

export type IdentityUsersNotificationPriority = 'Low' | 'Normal' | 'High' | 'Urgent' | 'Critical';

export type IdentityUsersProfileVisibility = 'Private' | 'FriendsOnly' | 'Public';

export type IdentityUsersPurgeStrategy = 'Immediate' | 'Scheduled' | 'GracePeriod';

export interface IdentityUsersReplaceUserAccessibilityPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserLocalizationPreferencesInput {
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserMetadataInput {
  customFields?: Record<string, Record<string, unknown>> | null;
  externalReferences?: Record<string, string> | null;
  tags?: Array<string> | null;
}

export interface IdentityUsersReplaceUserNotificationPreferencesInput {
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserPrivacyPreferencesInput {
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersReplaceUserProfileInput {
  bio?: string | null;
  company?: string | null;
  displayName?: string | null;
  jobTitle?: string | null;
  language?: string | null;
  location?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean;
  showLocation?: boolean;
  timeZone?: string | null;
  website?: string | null;
}

export interface IdentityUsersUpdateUserAccessibilityPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserLocalizationPreferencesInput {
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserMetadataInput {
  customFields?: Record<string, Record<string, unknown>> | null;
  externalReferences?: Record<string, string> | null;
  tagsToAdd?: Array<string> | null;
  tagsToRemove?: Array<string> | null;
}

export interface IdentityUsersUpdateUserNotificationPreferencesInput {
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserPreferencesInput {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserPrivacyPreferencesInput {
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUpdateUserProfileInput {
  bio?: string | null;
  company?: string | null;
  displayName?: string | null;
  jobTitle?: string | null;
  language?: string | null;
  location?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean | null;
  showLocation?: boolean | null;
  timeZone?: string | null;
  website?: string | null;
}

export interface IdentityUsersUpdateUserInput {
  name?: string | null;
  phoneNumber?: string | null;
}

export interface IdentityUsersUpdateUserRequestItem {
  name?: string | null;
  phoneNumber?: string | null;
  userId?: string;
}

export interface IdentityUsersUser {
  canPerformActions?: boolean;
  canSignIn?: boolean;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  email: string;
  hasPassword?: boolean;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isEmailVerified?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isSuspended?: boolean;
  lastLoginAt?: string | null;
  lastSeenAt?: string | null;
  metadata?: IdentityUsersUserMetadata;
  name: string;
  notifications?: Array<IdentityUsersUserNotification> | null;
  passwordHash?: string | null;
  phoneNumber?: string | null;
  preferences?: IdentityUsersUserPreferences;
  profile?: IdentityUsersUserProfile;
  status?: IdentityUsersUserStatus;
  tenantId?: string | null;
  tenantMemberships?: Array<IdentityTenantsTenantMember> | null;
  tokenVersion?: number;
  updatedAt: string;
  username?: string | null;
  version?: number;
}

export interface IdentityUsersUserAccessibilityPreferences {
  colorScheme?: string | null;
  customSettings?: Record<string, Record<string, unknown>> | null;
  fontSize?: number;
  highContrast?: boolean;
  keyboardNavigation?: boolean;
  largeText?: boolean;
  reducedMotion?: boolean;
  screenReader?: boolean;
}

export interface IdentityUsersUserDto {
  createdAt?: string;
  email?: string | null;
  id?: string;
  isActive?: boolean;
  lastSeenAt?: string | null;
  name?: string | null;
  phoneNumber?: string | null;
  updatedAt?: string | null;
}

export interface IdentityUsersUserLocalizationPreferences {
  currency?: string | null;
  customSettings?: Record<string, Record<string, unknown>> | null;
  dateFormat?: string | null;
  language?: string | null;
  numberFormat?: Record<string, Record<string, unknown>> | null;
  timeFormat?: string | null;
  timezone?: string | null;
}

export interface IdentityUsersUserMetadata {
  createdAt: string;
  customFields?: string | null;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  externalReferences?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  notes?: string | null;
  tags?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
}

export interface IdentityUsersUserMetadataDto {
  createdAt?: string;
  customFields?: Record<string, Record<string, unknown>> | null;
  externalReferences?: Record<string, string> | null;
  id?: string;
  tags?: Array<string> | null;
  updatedAt?: string | null;
  userId?: string;
  version?: string | null;
}

export interface IdentityUsersUserNotification {
  actionUrl?: string | null;
  archivedAt?: string | null;
  content: string;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isArchived?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isRead?: boolean;
  metadata?: string | null;
  priority?: IdentityUsersNotificationPriority;
  readAt?: string | null;
  relatedEntityId?: string | null;
  relatedEntityType?: string | null;
  senderId?: string | null;
  source?: string | null;
  tenantId?: string | null;
  title: string;
  type: string;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
}

export interface IdentityUsersUserNotificationDetail {
  actions?: Array<IdentityUsersNotificationAction> | null;
  notification?: IdentityUsersUserNotificationDto;
  relatedNotifications?: Array<IdentityUsersUserNotificationDto> | null;
}

export interface IdentityUsersUserNotificationDto {
  actionText?: string | null;
  actionUrl?: string | null;
  archivedAt?: string | null;
  category?: string | null;
  createdAt?: string;
  expiresAt?: string | null;
  id?: string;
  imageUrl?: string | null;
  isArchived?: boolean;
  isRead?: boolean;
  message?: string | null;
  metadata?: Record<string, Record<string, unknown>> | null;
  priority?: string | null;
  readAt?: string | null;
  title?: string | null;
  type?: string | null;
  updatedAt?: string | null;
  userId?: string;
  version?: string | null;
}

export interface IdentityUsersUserNotificationPreferences {
  categoryPreferences?: Record<string, Record<string, unknown>> | null;
  emailEnabled?: boolean;
  frequency?: string | null;
  inAppEnabled?: boolean;
  pushEnabled?: boolean;
  quietHours?: Record<string, Record<string, unknown>> | null;
  smsEnabled?: boolean;
}

export interface IdentityUsersUserPreferences {
  accessibilityPreferences?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  generalPreferences?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  localizationPreferences?: string | null;
  notificationPreferences?: string | null;
  privacyPreferences?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
}

export interface IdentityUsersUserPreferencesDto {
  accessibilityPreferences?: Record<string, Record<string, unknown>> | null;
  createdAt?: string;
  generalPreferences?: Record<string, Record<string, unknown>> | null;
  id?: string;
  localizationPreferences?: Record<string, Record<string, unknown>> | null;
  notificationPreferences?: Record<string, Record<string, unknown>> | null;
  privacyPreferences?: Record<string, Record<string, unknown>> | null;
  updatedAt?: string | null;
  userId?: string;
  version?: string | null;
}

export interface IdentityUsersUserPrivacyPreferences {
  activityTracking?: boolean;
  analyticsCookies?: boolean;
  customSettings?: Record<string, Record<string, unknown>> | null;
  dataCollection?: Record<string, Record<string, unknown>> | null;
  marketingEmails?: boolean;
  personalizedContent?: boolean;
  profileVisibility?: string | null;
  thirdPartySharing?: Record<string, Record<string, unknown>> | null;
}

export interface IdentityUsersUserProfile {
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  bio?: string | null;
  company?: string | null;
  createdAt: string;
  dateOfBirth?: string | null;
  deletedAt?: string | null;
  displayName?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  gender?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isVerified?: boolean;
  jobTitle?: string | null;
  location?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
  visibility?: IdentityUsersProfileVisibility;
  website?: string | null;
}

export interface IdentityUsersUserProfileDto {
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  bio?: string | null;
  company?: string | null;
  createdAt?: string;
  displayName?: string | null;
  id?: string;
  jobTitle?: string | null;
  language?: string | null;
  location?: string | null;
  profileVisibility?: string | null;
  showEmail?: boolean;
  showLocation?: boolean;
  timeZone?: string | null;
  updatedAt?: string | null;
  userId?: string;
  version?: string | null;
  website?: string | null;
}

export interface IdentityUsersUserStatus {
  isActive?: boolean;
  isSuspended?: boolean;
}

export interface KeyValuePairStringAuthenticationExtensionsPRFValues {
  key?: string | null;
  value?: ObjectsAuthenticationExtensionsPRFValues;
}

export interface LaunchPadCreateLaunchPlanInput {
  channels?: Array<string> | null;
  checklistItems?: Array<LaunchPadLaunchChecklistItemInput> | null;
  name?: string | null;
  positioning?: string | null;
  projectId?: string;
  targetLaunchAt?: string | null;
}

export interface LaunchPadLaunchChecklistItem {
  category: string;
  completedAt?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isComplete?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isRequired?: boolean;
  launchPlan?: LaunchPadLaunchPlan;
  launchPlanId?: string;
  tenantId?: string | null;
  title: string;
  updatedAt: string;
  version?: number;
}

export interface LaunchPadLaunchChecklistItemInput {
  category?: string | null;
  isComplete?: boolean;
  isRequired?: boolean;
  title?: string | null;
}

export interface LaunchPadLaunchPlan {
  channels?: Array<string> | null;
  checklistItems?: Array<LaunchPadLaunchChecklistItem> | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  launchedAt?: string | null;
  name: string;
  positioning?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  readinessPercent?: number;
  status?: LaunchPadLaunchPlanStatus;
  targetLaunchAt?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export type LaunchPadLaunchPlanStatus = 'Draft' | 'Preparing' | 'Ready' | 'Launched' | 'Paused';

export interface LearningAssessmentsAssessmentDefinition {
  assessmentId?: string;
  definition?: Record<string, unknown>;
  definitionSchemaVersion?: number;
}

export interface LearningAssessmentsAssessment {
  allowLateSubmissions?: boolean;
  assessmentGroupId?: string | null;
  assessmentGroupName?: string | null;
  assessmentGroupOrder?: number | null;
  assessmentGroupWeightPercent?: number | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  courseId?: string;
  description?: string | null;
  dueAt?: string | null;
  id?: string;
  isAvailable?: boolean;
  isRequired?: boolean;
  lateSubmissionDeadline?: string | null;
  maxAttempts?: number | null;
  maxScore?: number;
  order?: number;
  passingScore?: number;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  timeLimitMinutes?: number | null;
  title?: string | null;
  type?: LearningAssessmentsAssessmentType;
}

export interface LearningAssessmentsAssessmentGroupAnalytics {
  assessmentCount?: number;
  averagePercent?: number;
  distribution?: Array<LearningAssessmentsAssessmentScoreBucket> | null;
  gradedCount?: number;
  groupId?: string | null;
  groupName?: string | null;
  passRate?: number;
  ungradedCount?: number;
  weightPercent?: number | null;
}

export interface LearningAssessmentsAssessmentGroup {
  courseId?: string;
  description?: string | null;
  id?: string;
  name?: string | null;
  order?: number;
  weightPercent?: number;
}

export type LearningAssessmentsAssessmentPresentationMode = 'SingleStep' | 'Continuous';

export interface LearningAssessmentsAssessmentScoreBucket {
  count?: number;
  label?: string | null;
  maxPercent?: number;
  minPercent?: number;
}

export interface LearningAssessmentsAssessmentSubmission {
  assessmentId?: string;
  attemptNumber?: number;
  codePayload?: string | null;
  enrollmentId?: string;
  feedback?: string | null;
  filePayload?: string | null;
  gradedAt?: string | null;
  gradedBy?: string | null;
  id?: string;
  isLate?: boolean;
  mediaPayload?: string | null;
  passed?: boolean | null;
  projectPayload?: string | null;
  score?: number | null;
  startedAt?: string;
  status?: LearningAssessmentsSubmissionStatus;
  structuredAnswerPayload?: string | null;
  submittedAt?: string | null;
  submittedModalities?: LearningAssessmentsSubmissionModality;
  textPayload?: string | null;
  urlPayload?: string | null;
  userId?: string;
}

export type LearningAssessmentsAssessmentType = 'Quiz' | 'Exam' | 'Assignment' | 'Project' | 'PeerReview' | 'SelfAssessment';

export interface LearningAssessmentsAssignAssessmentGroupInput {
  assessmentGroupId?: string | null;
  clearAssessmentGroup?: boolean;
}

export interface LearningAssessmentsCanAttemptOutput {
  canAttempt?: boolean;
  currentAttemptCount?: number;
}

export interface LearningAssessmentsCourseAssessmentAnalytics {
  assessmentCount?: number;
  averagePercent?: number;
  courseId?: string;
  distribution?: Array<LearningAssessmentsAssessmentScoreBucket> | null;
  gradedCount?: number;
  groups?: Array<LearningAssessmentsAssessmentGroupAnalytics> | null;
  passRate?: number;
  ungradedCount?: number;
}

export interface LearningAssessmentsCreateAssessmentGroupInput {
  courseId?: string;
  description?: string | null;
  name?: string | null;
  order?: number;
  weightPercent?: number;
}

export interface LearningAssessmentsCreateAssessmentInput {
  allowLateSubmissions?: boolean;
  assessmentGroupId?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  courseId?: string;
  description?: string | null;
  dueAt?: string | null;
  isRequired?: boolean;
  lateSubmissionDeadline?: string | null;
  maxAttempts?: number | null;
  maxScore?: number;
  passingScore?: number;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  timeLimitMinutes?: number | null;
  title?: string | null;
  type?: LearningAssessmentsAssessmentType;
}

export interface LearningAssessmentsGradeSubmissionInput {
  feedback?: string | null;
  gradedBy?: string | null;
  score?: number;
}

export interface LearningAssessmentsInteractiveVideoAssessmentCue {
  assessmentId?: string;
  contentId?: string;
  cueId?: string | null;
  cuePositionSeconds?: number | null;
  id?: string;
}

export interface LearningAssessmentsLearnerAssessmentAttempt {
  definition?: Record<string, unknown>;
  definitionSchemaVersion?: number;
  submission?: LearningAssessmentsLearnerAssessmentSubmission;
}

export interface LearningAssessmentsLearnerAssessmentSubmission {
  assessmentId?: string;
  attemptNumber?: number;
  codePayload?: string | null;
  enrollmentId?: string;
  feedback?: string | null;
  filePayload?: string | null;
  gradedAt?: string | null;
  id?: string;
  isLate?: boolean;
  mediaPayload?: string | null;
  passed?: boolean | null;
  projectPayload?: string | null;
  score?: number | null;
  startedAt?: string;
  status?: LearningAssessmentsSubmissionStatus;
  structuredAnswerPayload?: string | null;
  submittedAt?: string | null;
  submittedModalities?: LearningAssessmentsSubmissionModality;
  textPayload?: string | null;
  urlPayload?: string | null;
}

export interface LearningAssessmentsLearnerInteractiveVideoAssessmentCue {
  cueId?: string | null;
  cuePositionSeconds?: number | null;
}

export interface LearningAssessmentsLinkInteractiveVideoCueInput {
  contentId?: string;
  cueId?: string | null;
  cuePositionSeconds?: number | null;
}

export interface LearningAssessmentsStartSubmissionInput {
  enrollmentId?: string;
}

/** A comma-separated combination of the declared flag names. */
export type LearningAssessmentsSubmissionModality = string;

export type LearningAssessmentsSubmissionStatus = 'InProgress' | 'Submitted' | 'Graded' | 'Returned' | 'Late';

export interface LearningAssessmentsSubmitAssessmentInput {
  codePayload?: string | null;
  filePayload?: string | null;
  mediaPayload?: string | null;
  projectPayload?: string | null;
  structuredAnswerPayload?: string | null;
  textPayload?: string | null;
  urlPayload?: string | null;
}

export interface LearningAssessmentsUpdateAssessmentDefinitionInput {
  definition?: Record<string, unknown>;
  definitionSchemaVersion?: number;
}

export interface LearningAssessmentsUpdateAssessmentGroupInput {
  description?: string | null;
  name?: string | null;
  order?: number | null;
  weightPercent?: number | null;
}

export interface LearningAssessmentsUpdateAssessmentInput {
  allowLateSubmissions?: boolean | null;
  assessmentGroupId?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  clearAssessmentGroupId?: boolean;
  clearContentId?: boolean;
  clearDueAt?: boolean;
  clearLateSubmissionDeadline?: boolean;
  contentId?: string | null;
  description?: string | null;
  dueAt?: string | null;
  isRequired?: boolean | null;
  lateSubmissionDeadline?: string | null;
  maxAttempts?: number | null;
  maxScore?: number | null;
  passingScore?: number | null;
  presentationMode?: LearningAssessmentsAssessmentPresentationMode;
  submissionModalities?: LearningAssessmentsSubmissionModality;
  timeLimitMinutes?: number | null;
  title?: string | null;
}

export interface LearningCertificatesCertificate {
  certificateNumber?: string | null;
  courseId?: string;
  courseName?: string | null;
  enrollmentId?: string;
  expiresAt?: string | null;
  id?: string;
  issuedAt?: string;
  recipientName?: string | null;
  status?: LearningCertificatesCertificateStatus;
  templateId?: string;
  userId?: string;
}

export type LearningCertificatesCertificateStatus = 'Active' | 'Expired' | 'Revoked';

export interface LearningCertificatesCertificateTemplateDetail {
  courseId?: string;
  createdAt?: string;
  description?: string | null;
  id?: string;
  isActive?: boolean;
  isDefault?: boolean;
  name?: string | null;
  templateHtml?: string | null;
  templateStyles?: string | null;
  tenantId?: string | null;
  updatedAt?: string;
}

export interface LearningCertificatesCertificateTemplate {
  courseId?: string;
  createdAt?: string;
  description?: string | null;
  id?: string;
  isActive?: boolean;
  isDefault?: boolean;
  name?: string | null;
  tenantId?: string | null;
  updatedAt?: string;
}

export interface LearningCertificatesCertificateVerificationResult {
  certificateNumber?: string | null;
  courseName?: string | null;
  expiresAt?: string | null;
  isValid?: boolean;
  issuedAt?: string;
  message?: string | null;
  recipientName?: string | null;
  status?: LearningCertificatesCertificateStatus;
}

export interface LearningCertificatesCreateCertificateTemplateInput {
  courseId?: string;
  name?: string | null;
  templateHtml?: string | null;
}

export interface LearningCertificatesIssueCertificateInput {
  courseId?: string;
  enrollmentId?: string;
  templateId?: string;
  userId?: string;
}

export interface LearningCertificatesRevokeCertificateInput {
  reason?: string | null;
}

export interface LearningCertificatesUpdateCertificateTemplateInput {
  description?: string | null;
  isActive?: boolean;
  isDefault?: boolean;
  name?: string | null;
  templateHtml?: string | null;
  templateStyles?: string | null;
}

export interface LearningCohortsApplyCohortScheduleInput {
  confirmAdvisories?: boolean;
  expectedVersion?: number;
  rules?: LearningCohortsPreviewCohortScheduleInput;
}

export interface LearningCohortsAvailableCohortContent {
  availableFrom?: string | null;
  availableUntil?: string | null;
  body?: string | null;
  contentId?: string;
  description?: string | null;
  dueAt?: string | null;
  instructionalWeek?: number;
  parentId?: string | null;
  sortOrder?: number;
  title?: string | null;
  type?: LearningCoursesProgramContentType;
}

export interface LearningCohortsCohortCalendarEntry {
  availableFrom?: string | null;
  cohortId?: string;
  cohortName?: string | null;
  dueAt?: string | null;
  endsAt?: string | null;
  itemId?: string;
  startsAt?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
  title?: string | null;
  type?: LearningCohortsCohortScheduleItemType;
}

export interface LearningCohortsCohort {
  availableSpots?: number;
  canEnroll?: boolean;
  conflictCount?: number;
  courseId?: string;
  createdAt?: string;
  currentEnrollmentCount?: number;
  description?: string | null;
  endDate?: string;
  id?: string;
  instructorId?: string | null;
  isOpen?: boolean;
  maxCapacity?: number;
  meetingSchedule?: string | null;
  name?: string | null;
  nextMeetingAt?: string | null;
  schedule?: LearningCohortsCohortScheduleSummary;
  startDate?: string;
  status?: LearningCohortsCohortStatus;
  tenantId?: string | null;
}

export type LearningCohortsCohortPacingMode = 'OneModulePerWeek' | 'OneLessonPerMeeting' | 'FixedLessonsPerWeek' | 'Manual';

export type LearningCohortsCohortReleasePolicy = 'Weekly' | 'BeforeMeeting' | 'Manual' | 'Immediately';

export interface LearningCohortsCohortScheduleConflict {
  assessmentId?: string | null;
  code?: string | null;
  message?: string | null;
  programContentId?: string | null;
  severity?: LearningCohortsScheduleConflictSeverity;
}

export interface LearningCohortsCohortSchedule {
  cohortId?: string;
  id?: string;
  items?: Array<LearningCohortsCohortScheduleItem> | null;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingDurationMinutes?: number;
  meetingStartTime?: string;
  pacingMode?: LearningCohortsCohortPacingMode;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  timezoneId?: string | null;
  unitsPerPeriod?: number;
  unscheduledContentIds?: Array<string> | null;
  version?: number;
}

export interface LearningCohortsCohortScheduleItem {
  assessmentId?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  endsAt?: string | null;
  id?: string;
  instructionalWeek?: number;
  location?: string | null;
  meetingUrl?: string | null;
  programContentId?: string | null;
  sortOrder?: number;
  startsAt?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
  title?: string | null;
  type?: LearningCohortsCohortScheduleItemType;
  visibilityOverride?: LearningCohortsCohortVisibilityOverride;
}

export type LearningCohortsCohortScheduleItemStatus = 'Draft' | 'Scheduled' | 'Published' | 'Completed' | 'Cancelled';

export type LearningCohortsCohortScheduleItemType = 'ContentRelease' | 'LiveSession' | 'AssessmentWindow' | 'Milestone';

export interface LearningCohortsCohortSchedulePreview {
  calculatedEndDate?: string;
  conflicts?: Array<LearningCohortsCohortScheduleConflict> | null;
  hasBlockingConflicts?: boolean;
  items?: Array<LearningCohortsCohortSchedulePreviewItem> | null;
}

export interface LearningCohortsCohortSchedulePreviewItem {
  assessmentId?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  endsAt?: string | null;
  instructionalWeek?: number;
  programContentId?: string | null;
  sortOrder?: number;
  startsAt?: string | null;
  title?: string | null;
  type?: LearningCohortsCohortScheduleItemType;
}

export interface LearningCohortsCohortScheduleSummary {
  itemCount?: number;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingStartTime?: string;
  pacingMode?: LearningCohortsCohortPacingMode;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  timezoneId?: string | null;
  version?: number;
}

export type LearningCohortsCohortStatus = 'Scheduled' | 'Active' | 'Completed' | 'Cancelled';

export type LearningCohortsCohortVisibilityOverride = 'Inherited' | 'Hidden' | 'Visible';

export interface LearningCohortsCourseCohortCalendar {
  courseId?: string;
  entries?: Array<LearningCohortsCohortCalendarEntry> | null;
}

export interface LearningCohortsCreateCohortInput {
  courseId?: string;
  description?: string | null;
  endDate?: string;
  instructorId?: string | null;
  maxCapacity?: number;
  meetingSchedule?: string | null;
  name?: string | null;
  startDate?: string;
  tenantId?: string | null;
}

export interface LearningCohortsPreviewCohortScheduleInput {
  assessmentDueOffsetDays?: number;
  cohortEndDate?: string;
  firstInstructionalDate?: string;
  meetingDays?: Array<SystemDayOfWeek> | null;
  meetingDurationMinutes?: number;
  meetingStartTime?: string;
  pacingMode?: LearningCohortsCohortPacingMode;
  releasePolicy?: LearningCohortsCohortReleasePolicy;
  skippedDates?: Array<string> | null;
  timezoneId?: string | null;
  unitsPerPeriod?: number;
}

export type LearningCohortsScheduleConflictSeverity = 'Advisory' | 'Blocking';

export type LearningCohortsScheduleShiftScope = 'Single' | 'Following';

export interface LearningCohortsShiftCohortScheduleInput {
  days?: number;
  expectedVersion?: number;
  scope?: LearningCohortsScheduleShiftScope;
}

export interface LearningCohortsUpdateCohortInput {
  description?: string | null;
  endDate?: string | null;
  instructorId?: string | null;
  maxCapacity?: number | null;
  meetingSchedule?: string | null;
  name?: string | null;
  startDate?: string | null;
}

export interface LearningCohortsUpdateCohortScheduleItemInput {
  availableFrom?: string | null;
  availableUntil?: string | null;
  dueAt?: string | null;
  endsAt?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  startsAt?: string | null;
  status?: LearningCohortsCohortScheduleItemStatus;
  title?: string | null;
  visibilityOverride?: LearningCohortsCohortVisibilityOverride;
}

export interface LearningCohortsUpdateCohortScheduleInput {
  expectedVersion?: number;
  item?: LearningCohortsUpdateCohortScheduleItemInput;
}

export interface LearningCoursesActivityGrade {
  contentInteraction?: LearningCoursesContentInteractionSummary;
  contentInteractionId?: string;
  createdAt?: string;
  feedback?: string | null;
  grade?: number;
  gradePercentage?: string | null;
  gradedAt?: string;
  grader?: LearningCoursesGraderSummary;
  graderProgramUserId?: string | null;
  gradingDetails?: string | null;
  hasFeedback?: boolean;
  hasGradingDetails?: boolean;
  id?: string;
  isPassingGrade?: boolean;
  updatedAt?: string;
}

export interface LearningCoursesActivitySettings {}

export interface LearningCoursesCircularDependencyCheckResult {
  wouldCreateCycle?: boolean;
}

export interface LearningCoursesCloneProgram {
  newDescription?: string | null;
  newTitle?: string | null;
}

export interface LearningCoursesCompleteContentInput {
  contentId?: string;
  programUserId?: string;
}

export interface LearningCoursesCompleteCourseCheckoutInput {
  paymentMethod?: string | null;
  paymentProviderReference?: string | null;
  productId?: string;
}

export interface LearningCoursesCompleteCourseCheckoutOutput {
  alreadyHadAccess?: boolean;
  amount?: number;
  courseId?: string;
  currency?: string | null;
  enrollmentIds?: Array<string> | null;
  entitlementId?: string;
  learningUrl?: string | null;
  paymentProviderReference?: string | null;
  productId?: string;
}

export interface LearningCoursesCompletionRates {
  completionTrends?: Array<LearningCoursesCompletionTrend> | null;
  contentCompletionRates?: Record<string, number> | null;
  overallCompletionRate?: number;
  programId?: string;
}

export interface LearningCoursesCompletionTrend {
  completedCount?: number;
  date?: string;
  rate?: number;
  totalCount?: number;
}

export interface LearningCoursesContentInteraction {
  canModify?: boolean;
  completedAt?: string | null;
  completionPercentage?: number;
  content?: LearningCoursesContentSummary;
  contentId?: string;
  createdAt?: string;
  durationInMinutes?: number;
  durationInSeconds?: number;
  firstAccessedAt?: string | null;
  id?: string;
  isCompleted?: boolean;
  isSubmitted?: boolean;
  lastAccessedAt?: string | null;
  programUser?: LearningCoursesProgramUserSummary;
  programUserId?: string;
  status?: LearningCoursesProgressStatus;
  submissionData?: string | null;
  submittedAt?: string | null;
  timeSpentMinutes?: number | null;
  timeSpentSeconds?: number;
  updatedAt?: string;
}

export interface LearningCoursesContentInteractionEvent {
  durationSeconds?: number | null;
  id?: string;
  idempotencyKey?: string | null;
  interactionId?: string;
  occurredAt?: string;
  payload?: string | null;
  positionSeconds?: number | null;
  progressPercentage?: number | null;
  type?: LearningCoursesContentInteractionEventType;
}

export type LearningCoursesContentInteractionEventType =
  'Opened' | 'Heartbeat' | 'Progressed' | 'Paused' | 'Resumed' | 'Seeked' | 'Completed' | 'QuizPresented' | 'QuizAnswered';

export interface LearningCoursesContentInteractionSummary {
  content?: LearningCoursesContentSummary;
  contentId?: string;
  id?: string;
  programUserId?: string;
  status?: string | null;
  student?: LearningCoursesStudentSummary;
  submittedAt?: string | null;
}

export interface LearningCoursesContentProgress {
  completedAt?: string | null;
  completionPercentage?: number;
  contentId?: string;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  status?: LearningCoursesProgressStatus;
  title?: string | null;
}

export interface LearningCoursesContentStats {
  contentByType?: {
    Assignment?: number;
    Challenge?: number;
    Code?: number;
    Discussion?: number;
    Lesson?: number;
    Module?: number;
    Page?: number;
    Project?: number;
    Questionnaire?: number;
    Reflection?: number;
    Survey?: number;
  } | null;
  contentByVisibility?: { Internal?: number; Private?: number; Public?: number; Restricted?: number } | null;
  nestedContent?: number;
  optionalContent?: number;
  programId?: string;
  requiredContent?: number;
  topLevelContent?: number;
  totalContent?: number;
}

export interface LearningCoursesContentSummary {
  contentType?: string | null;
  estimatedMinutes?: number | null;
  id?: string;
  title?: string | null;
}

export interface LearningCoursesCourseSupportTicketMessageInput {
  isInternal?: boolean;
  message?: string | null;
}

export interface LearningCoursesCreateActivityGrade {
  contentInteractionId?: string;
  feedback?: string | null;
  grade?: number;
  graderProgramUserId?: string;
  gradingDetails?: string | null;
}

export interface LearningCoursesCreatePrerequisiteApiInput {
  courseId?: string;
  description?: string | null;
  displayOrder?: number;
  minimumGrade?: number | null;
  prerequisiteCourseId?: string;
  prerequisiteGroup?: string | null;
  type?: LearningCoursesPrerequisiteType;
}

export interface LearningCoursesCreateProductFromProgram {
  basePrice?: number;
  currency?: string | null;
  description?: string | null;
  name?: string | null;
}

export interface LearningCoursesCreateProgramContent {
  activitySettings?: LearningCoursesActivitySettings;
  body?: string | null;
  description?: string | null;
  estimatedMinutes?: number | null;
  gradingMethod?: LearningCoursesGradingMethod;
  isRequired?: boolean;
  lessonFormat?: LearningCoursesLessonContentFormat;
  maxPoints?: number | null;
  parentId?: string | null;
  programId: string;
  sortOrder?: number;
  title: string;
  type: LearningCoursesProgramContentType;
  visibility?: LearningCoursesVisibility;
}

export interface LearningCoursesCreateProgram {
  creatorId?: string | null;
  description?: string | null;
  slug?: string | null;
  thumbnail?: string | null;
  title?: string | null;
}

export interface LearningCoursesEngagementMetrics {
  averageSessionDuration?: string;
  contentEngagement?: Record<string, number> | null;
  dailyActiveUsers?: number;
  monthlyActiveUsers?: number;
  programId?: string;
  retentionRate?: number;
  totalSessions?: number;
  weeklyActiveUsers?: number;
}

export type LearningCoursesEnrollmentStatus = 'Open' | 'Active' | 'Paused' | 'Cancelled' | 'Expired' | 'Completed' | 'Closed' | 'InviteOnly' | 'Waitlist';

export interface LearningCoursesGradeStatistics {
  averageGrade?: number;
  averageGradeFormatted?: string | null;
  hasGrades?: boolean;
  maxGrade?: number;
  minGrade?: number;
  passingRate?: number;
  passingRateFormatted?: string | null;
  totalGrades?: number;
}

export interface LearningCoursesGraderSummary {
  id?: string;
  role?: string | null;
  userDisplayName?: string | null;
  userEmail?: string | null;
}

export type LearningCoursesGradingMethod = 'None' | 'Instructor' | 'Peer' | 'Ai' | 'AutomatedTests';

export type LearningCoursesLessonContentFormat = 'Markdown' | 'Lexical' | 'RevealJs' | 'Video';

export interface LearningCoursesMonetization {
  currency?: string | null;
  isSubscription?: boolean;
  price?: number;
  subscriptionDurationDays?: number | null;
}

export interface LearningCoursesMoveContent {
  contentId: string;
  newParentId?: string | null;
  newSortOrder: number;
}

export interface LearningCoursesPrerequisiteCheckResult {
  isSatisfied?: boolean;
  prerequisites?: Array<LearningCoursesPrerequisiteStatus> | null;
}

export interface LearningCoursesPrerequisite {
  courseId?: string;
  createdAt?: string;
  description?: string | null;
  displayOrder?: number;
  id?: string;
  minimumGrade?: number | null;
  prerequisiteCourseId?: string;
  prerequisiteCourseName?: string | null;
  prerequisiteGroup?: string | null;
  tenantId?: string | null;
  type?: LearningCoursesPrerequisiteType;
}

export interface LearningCoursesPrerequisiteStatus {
  achievedGrade?: number | null;
  courseName?: string | null;
  isSatisfied?: boolean;
  prerequisiteCourseId?: string;
  prerequisiteId?: string;
  reason?: string | null;
  requiredGrade?: number | null;
  type?: LearningCoursesPrerequisiteType;
}

export type LearningCoursesPrerequisiteType = 'Required' | 'Recommended' | 'Corequisite';

export interface LearningCoursesPricing {
  currency?: string | null;
  isMonetizationEnabled?: boolean;
  isSubscription?: boolean;
  price?: number;
  subscriptionDurationDays?: number | null;
}

export interface LearningCoursesProgramAnalytics {
  activeUsers?: number;
  additionalMetrics?: Record<string, Record<string, unknown>> | null;
  averageCompletionTime?: string;
  completedUsers?: number;
  completionRate?: number;
  lastActivity?: string | null;
  programId?: string;
  title?: string | null;
  totalUsers?: number;
  totalViews?: number;
}

export interface LearningCoursesProgramContent {
  activitySettings?: LearningCoursesActivitySettings;
  body?: Record<string, unknown> | null;
  children?: Array<LearningCoursesProgramContent> | null;
  childrenCount?: number;
  createdAt?: string;
  description?: string | null;
  estimatedMinutes?: number | null;
  gradingMethod?: LearningCoursesGradingMethod;
  id?: string;
  isRequired?: boolean;
  lessonFormat?: LearningCoursesLessonContentFormat;
  maxPoints?: number | null;
  parentId?: string | null;
  parentTitle?: string | null;
  programId?: string;
  programTitle?: string | null;
  sortOrder?: number;
  title?: string | null;
  type?: LearningCoursesProgramContentType;
  updatedAt?: string | null;
  visibility?: LearningCoursesVisibility;
}

/** Legacy values Page and Challenge are normalized on read and are not valid for new content. */
export type LearningCoursesProgramContentType =
  'Lesson' | 'Assignment' | 'Questionnaire' | 'Discussion' | 'Code' | 'Reflection' | 'Survey' | 'Project' | 'Module';

export type LearningCoursesProgramDifficulty = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export interface LearningCoursesProgram {
  averageRating?: number;
  category?: ProgramCategory;
  createdAt?: string;
  creatorId?: string | null;
  currentEnrollments?: number;
  description?: string | null;
  difficulty?: LearningCoursesProgramDifficulty;
  enrollmentDeadline?: string | null;
  enrollmentStatus?: LearningCoursesEnrollmentStatus;
  estimatedHours?: number | null;
  id?: string;
  isEnrollmentOpen?: boolean;
  maxEnrollments?: number | null;
  metadata?: string | null;
  skillsProvided?: string | null;
  skillsRequired?: string | null;
  slug?: string | null;
  status?: ContentStatus;
  thumbnail?: string | null;
  title?: string | null;
  totalRatings?: number;
  updatedAt?: string | null;
  videoShowcaseUrl?: string | null;
  visibility?: ContentVisibility;
}

export interface LearningCoursesProgramUserSummary {
  id?: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
}

export type LearningCoursesProgressStatus = 'NotStarted' | 'InProgress' | 'Completed' | 'Submitted';

export interface LearningCoursesRecordContentInteractionEventInput {
  durationSeconds?: number | null;
  idempotencyKey?: string | null;
  occurredAt?: string | null;
  payload?: string | null;
  positionSeconds?: number | null;
  progressPercentage?: number | null;
  type?: LearningCoursesContentInteractionEventType;
}

export interface LearningCoursesReflectionResponseResult {
  body?: string | null;
  respondentUserId?: string | null;
  responseId?: string;
  submittedAt?: string | null;
}

export interface LearningCoursesRejectProgram {
  reason?: string | null;
}

export interface LearningCoursesReorderContent {
  contentIds?: Array<string> | null;
}

export interface LearningCoursesReorderPrerequisitesInput {
  prerequisiteIds?: Array<string> | null;
}

export interface LearningCoursesResolveCourseSupportTicketInput {
  summary?: string | null;
}

export interface LearningCoursesRevenueAnalytics {
  averageRevenuePerUser?: number;
  conversionRate?: number;
  monthlyPurchases?: number;
  monthlyRevenue?: number;
  programId?: string;
  revenueChart?: Array<LearningCoursesRevenueChart> | null;
  totalPurchases?: number;
  totalRevenue?: number;
}

export interface LearningCoursesRevenueChart {
  date?: string;
  purchases?: number;
  revenue?: number;
}

export interface LearningCoursesScheduleProgram {
  publishAt?: string;
}

export interface LearningCoursesSearchContent {
  isRequired?: boolean | null;
  parentId?: string | null;
  programId: string;
  searchTerm: string;
  type?: LearningCoursesProgramContentType;
  visibility?: LearningCoursesVisibility;
}

export interface LearningCoursesSendCourseStudentMessageInput {
  message?: string | null;
  subject?: string | null;
  userIds?: Array<string> | null;
}

export interface LearningCoursesSendCourseStudentMessageOutput {
  sent?: number;
}

export interface LearningCoursesStartContentInput {
  contentId?: string;
  programUserId?: string;
}

export interface LearningCoursesStudentSummary {
  id?: string;
  userDisplayName?: string | null;
  userEmail?: string | null;
}

export interface LearningCoursesSubmitContentInput {
  contentId?: string;
  programUserId?: string;
  submissionData?: string | null;
}

export interface LearningCoursesSubmitUserContent {
  submissionData: string;
}

export interface LearningCoursesSurveyResponseResult {
  answers?: Record<string, Record<string, unknown>> | null;
  respondentUserId?: string | null;
  responseId?: string;
  submittedAt?: string | null;
}

export interface LearningCoursesUpdateActivityGrade {
  feedback?: string | null;
  grade?: number | null;
  gradingDetails?: string | null;
}

export interface LearningCoursesUpdatePrerequisiteApiInput {
  description?: string | null;
  displayOrder?: number | null;
  minimumGrade?: number | null;
  prerequisiteGroup?: string | null;
  type?: LearningCoursesPrerequisiteType;
}

export interface LearningCoursesUpdatePricing {
  currency?: string | null;
  isSubscription?: boolean | null;
  price?: number | null;
  subscriptionDurationDays?: number | null;
}

export interface LearningCoursesUpdateProgramContent {
  activitySettings?: LearningCoursesActivitySettings;
  body?: string | null;
  description?: string | null;
  estimatedMinutes?: number | null;
  gradingMethod?: LearningCoursesGradingMethod;
  id: string;
  isRequired?: boolean | null;
  lessonFormat?: LearningCoursesLessonContentFormat;
  maxPoints?: number | null;
  sortOrder?: number | null;
  title?: string | null;
  type?: LearningCoursesProgramContentType;
  visibility?: LearningCoursesVisibility;
}

export interface LearningCoursesUpdateProgram {
  category?: ProgramCategory;
  clearEnrollmentDeadline?: boolean;
  clearMaxEnrollments?: boolean;
  creatorId?: string | null;
  description?: string | null;
  difficulty?: LearningCoursesProgramDifficulty;
  enrollmentDeadline?: string | null;
  enrollmentStatus?: LearningCoursesEnrollmentStatus;
  estimatedHours?: number | null;
  maxEnrollments?: number | null;
  metadata?: string | null;
  skillsProvided?: string | null;
  skillsRequired?: string | null;
  slug?: string | null;
  thumbnail?: string | null;
  title?: string | null;
  videoShowcaseUrl?: string | null;
  visibility?: ContentVisibility;
}

export interface LearningCoursesUpdateProgress {
  additionalData?: Record<string, Record<string, unknown>> | null;
  lastAccessedAt?: string | null;
  status?: LearningCoursesProgressStatus;
}

export interface LearningCoursesUpdateProgressInput {
  completionPercentage?: number;
  contentId?: string;
  programUserId?: string;
}

export interface LearningCoursesUpdateTimeSpentInput {
  additionalMinutes?: number;
  contentId?: string;
  programUserId?: string;
}

export interface LearningCoursesUserProgress {
  completedAt?: string | null;
  completionPercentage?: number;
  contentProgress?: Array<LearningCoursesContentProgress> | null;
  courseId?: string;
  enrollmentId?: string;
  lastAccessedAt?: string | null;
  startedAt?: string | null;
  userId?: string;
}

export type LearningCoursesVisibility = 'Public' | 'Internal' | 'Private' | 'Restricted';

export interface LearningEnrollmentsEnrollUserInput {
  cohortId?: string | null;
  courseId?: string;
  userId?: string;
}

export interface LearningEnrollmentsEnrollment {
  cohortId?: string | null;
  completedAt?: string | null;
  courseId?: string;
  droppedAt?: string | null;
  enrolledAt?: string;
  id?: string;
  lastActivityAt?: string | null;
  progress?: number;
  status?: LearningEnrollmentsEnrollmentStatus;
  userId?: string;
}

export type LearningEnrollmentsEnrollmentStatus = 'Active' | 'Paused' | 'Completed' | 'Dropped' | 'Expired';

export interface LearningEnrollmentsUpdateEnrollmentProgressInput {
  progress?: number;
}

export type LearningExperienceDiscoveryCollectionType = 'Curated' | 'Category' | 'Skill' | 'Career' | 'Trending' | 'NewReleases';

export interface LearningExperienceDiscoveryCourseCollection {
  courseCount?: number;
  createdAt?: string;
  curatorId?: string;
  description?: string | null;
  id?: string;
  imageUrl?: string | null;
  isFeatured?: boolean;
  isPublished?: boolean;
  slug?: string | null;
  tenantId?: string | null;
  title?: string | null;
  type?: LearningExperienceDiscoveryCollectionType;
  updatedAt?: string;
}

export interface LearningExperienceDiscoveryCreateCourseCollection {
  description?: string | null;
  imageUrl?: string | null;
  title?: string | null;
  type?: LearningExperienceDiscoveryCollectionType;
}

export interface LearningExperienceDiscoveryCreateFeaturedContent {
  courseId?: string | null;
  displayOrder?: number;
  endsAt?: string | null;
  imageUrl?: string | null;
  learningPathId?: string | null;
  linkUrl?: string | null;
  startsAt?: string | null;
  subtitle?: string | null;
  targetAudience?: string | null;
  title?: string | null;
  type?: LearningExperienceDiscoveryFeaturedContentType;
}

export interface LearningExperienceDiscoveryFeaturedContent {
  courseId?: string | null;
  createdAt?: string;
  displayOrder?: number;
  endsAt?: string | null;
  id?: string;
  imageUrl?: string | null;
  isActive?: boolean;
  learningPathId?: string | null;
  linkUrl?: string | null;
  startsAt?: string | null;
  subtitle?: string | null;
  targetAudience?: string | null;
  tenantId?: string | null;
  title?: string | null;
  type?: LearningExperienceDiscoveryFeaturedContentType;
  updatedAt?: string;
}

export type LearningExperienceDiscoveryFeaturedContentType =
  'HeroBanner' | 'CategoryHighlight' | 'NewRelease' | 'TopRated' | 'TrendingNow' | 'StaffPick' | 'SeasonalPromotion';

export interface LearningExperienceDiscoveryPopularSearchResult {
  clickThroughRate?: number;
  query?: string | null;
  searchCount?: number;
  totalClicks?: number;
}

export interface LearningExperienceDiscoveryRecordSearchClick {
  clickedCourseId?: string;
  searchId?: string;
}

export interface LearningExperienceDiscoveryRecordSearch {
  filters?: string | null;
  query?: string | null;
  resultCount?: number;
}

export interface LearningExperienceDiscoverySearchHistory {
  clickedCourseId?: string | null;
  createdAt?: string;
  filters?: string | null;
  id?: string;
  query?: string | null;
  resultCount?: number;
  userId?: string | null;
}

export interface LearningExperienceDiscoveryUpdateCourseCollection {
  description?: string | null;
  imageUrl?: string | null;
  isFeatured?: boolean | null;
  title?: string | null;
}

export interface LearningExperienceDiscoveryUpdateFeaturedContent {
  displayOrder?: number | null;
  endsAt?: string | null;
  imageUrl?: string | null;
  isActive?: boolean | null;
  linkUrl?: string | null;
  startsAt?: string | null;
  subtitle?: string | null;
  targetAudience?: string | null;
  title?: string | null;
}

export interface LearningExperienceLearningPathsAddCourseToPath {
  courseId?: string;
  isRequired?: boolean;
  order?: number;
}

export interface LearningExperienceLearningPathsCourseOrder {
  courseId?: string;
  order?: number;
}

export interface LearningExperienceLearningPathsCreateLearningPath {
  description?: string | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  estimatedHours?: number;
  imageUrl?: string | null;
  title?: string | null;
}

export interface LearningExperienceLearningPathsLearningPathCourse {
  courseId?: string;
  isRequired?: boolean;
  order?: number;
}

export interface LearningExperienceLearningPathsLearningPathDetail {
  completionCount?: number;
  courses?: Array<LearningExperienceLearningPathsLearningPathCourse> | null;
  createdAt?: string;
  creatorId?: string;
  description?: string | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  enrollmentCount?: number;
  estimatedHours?: number;
  id?: string;
  imageUrl?: string | null;
  isFeatured?: boolean;
  isPublished?: boolean;
  slug?: string | null;
  tenantId?: string | null;
  title?: string | null;
  updatedAt?: string;
}

export type LearningExperienceLearningPathsLearningPathDifficulty = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export interface LearningExperienceLearningPathsLearningPath {
  completionCount?: number;
  courseCount?: number;
  createdAt?: string;
  creatorId?: string;
  description?: string | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  enrollmentCount?: number;
  estimatedHours?: number;
  id?: string;
  imageUrl?: string | null;
  isFeatured?: boolean;
  isPublished?: boolean;
  slug?: string | null;
  tenantId?: string | null;
  title?: string | null;
  updatedAt?: string;
}

export interface LearningExperienceLearningPathsLearningPathEnrollment {
  completedAt?: string | null;
  coursesCompleted?: number;
  createdAt?: string;
  enrolledAt?: string;
  id?: string;
  learningPathId?: string;
  progress?: number;
  status?: LearningExperienceLearningPathsLearningPathEnrollmentStatus;
  totalCourses?: number;
  updatedAt?: string;
  userId?: string;
}

export type LearningExperienceLearningPathsLearningPathEnrollmentStatus = 'InProgress' | 'Completed' | 'Abandoned';

export interface LearningExperienceLearningPathsLearningPathStatistics {
  activeEnrollments?: number;
  averageCompletionTime?: string;
  averageProgress?: number;
  completedEnrollments?: number;
  completionRate?: number;
  learningPathId?: string;
  totalEnrollments?: number;
}

export interface LearningExperienceLearningPathsReorderCourses {
  courses?: Array<LearningExperienceLearningPathsCourseOrder> | null;
}

export interface LearningExperienceLearningPathsUpdateLearningPath {
  description?: string | null;
  difficulty?: LearningExperienceLearningPathsLearningPathDifficulty;
  estimatedHours?: number | null;
  imageUrl?: string | null;
  isFeatured?: boolean | null;
  title?: string | null;
}

export interface LearningExperienceLearningPathsUpdatePathProgress {
  coursesCompleted?: number;
}

export interface LearningExperienceRecommendationsAddSkillInput {
  skill?: string | null;
}

export interface LearningExperienceRecommendationsCreateOrUpdateLearningProfile {
  learningGoals?: Array<string> | null;
  preferredCategories?: Array<string> | null;
  preferredDifficulty?: string | null;
  preferredDuration?: string | null;
  skills?: Array<string> | null;
}

export interface LearningExperienceRecommendationsPopularCourse {
  averageRating?: number;
  category?: string | null;
  courseId?: string;
  description?: string | null;
  enrollmentCount?: number;
  thumbnail?: string | null;
  title?: string | null;
  totalRatings?: number;
}

export interface LearningExperienceRecommendationsRecommendation {
  courseId?: string;
  createdAt?: string;
  expiresAt?: string;
  id?: string;
  isDismissed?: boolean;
  isViewed?: boolean;
  reason?: string | null;
  score?: number;
  type?: LearningExperienceRecommendationsRecommendationType;
  userId?: string;
}

export interface LearningExperienceRecommendationsRecommendationStatistics {
  byType?: {
    BasedOnHistory?: number;
    InstructorFollowed?: number;
    NextInPath?: number;
    PeerRecommended?: number;
    PersonalizedAI?: number;
    PopularInCategory?: number;
    SimilarToCompleted?: number;
    TrendingNow?: number;
  } | null;
  convertedCount?: number;
  dismissedCount?: number;
  totalRecommendations?: number;
  viewedCount?: number;
}

export type LearningExperienceRecommendationsRecommendationType =
  'PersonalizedAI' | 'PopularInCategory' | 'TrendingNow' | 'BasedOnHistory' | 'SimilarToCompleted' | 'NextInPath' | 'InstructorFollowed' | 'PeerRecommended';

export interface LearningExperienceRecommendationsSimilarCourse {
  category?: string | null;
  courseId?: string;
  description?: string | null;
  matchingTags?: Array<string> | null;
  similarityScore?: number;
  thumbnail?: string | null;
  title?: string | null;
}

export interface LearningExperienceRecommendationsTrendingCourse {
  category?: string | null;
  courseId?: string;
  description?: string | null;
  recentEnrollments?: number;
  thumbnail?: string | null;
  title?: string | null;
  trendScore?: number;
}

export interface LearningExperienceRecommendationsUserLearningProfile {
  createdAt?: string;
  id?: string;
  lastActivityAt?: string | null;
  learningGoals?: Array<string> | null;
  preferredCategories?: Array<string> | null;
  preferredDifficulty?: string | null;
  preferredDuration?: string | null;
  skills?: Array<string> | null;
  totalCoursesCompleted?: number;
  totalHoursLearned?: number;
  updatedAt?: string;
  userId?: string;
}

export interface LearningExperienceSocialControllersUpdateReviewModerationInput {
  isApproved?: boolean;
  isFeatured?: boolean;
}

export type LearningExperienceSocialFeedItemType =
  | 'NewCourse'
  | 'PopularCourse'
  | 'TrendingDiscussion'
  | 'FeaturedReview'
  | 'LearningPathSuggestion'
  | 'CourseUpdate'
  | 'InstructorActivity'
  | 'PeerActivity'
  | 'AchievementUnlocked'
  | 'SkillMilestone';

export interface LearningExperienceSocialServicesCourseDiscussion {
  authorId?: string;
  content?: string | null;
  contentId?: string | null;
  courseId?: string;
  createdAt?: string;
  id?: string;
  isPinned?: boolean;
  isResolved?: boolean;
  lastActivityAt?: string | null;
  replyCount?: number;
  title?: string | null;
  viewCount?: number;
}

export interface LearningExperienceSocialServicesCourseLike {
  courseId?: string;
  createdAt?: string;
  id?: string;
  userId?: string;
}

export interface LearningExperienceSocialServicesCourseRatingStats {
  averageRating?: number;
  courseId?: string;
  featuredReviewCount?: number;
  fiveStarCount?: number;
  fourStarCount?: number;
  oneStarCount?: number;
  threeStarCount?: number;
  totalReviews?: number;
  twoStarCount?: number;
}

export interface LearningExperienceSocialServicesCourseReview {
  content?: string | null;
  courseId?: string;
  createdAt?: string;
  helpfulCount?: number;
  id?: string;
  isApproved?: boolean;
  isFeatured?: boolean;
  isVerifiedPurchase?: boolean;
  rating?: number;
  title?: string | null;
  userId?: string;
}

export interface LearningExperienceSocialServicesCourseWishlist {
  courseId?: string;
  createdAt?: string;
  id?: string;
  notifyOnSale?: boolean;
  notifyOnUpdate?: boolean;
  userId?: string;
}

export interface LearningExperienceSocialServicesCreateDiscussionInput {
  content?: string | null;
  contentId?: string | null;
  courseId?: string;
  title?: string | null;
}

export interface LearningExperienceSocialServicesCreateReplyInput {
  content?: string | null;
  discussionId?: string;
  parentReplyId?: string | null;
}

export interface LearningExperienceSocialServicesCreateReviewInput {
  content?: string | null;
  courseId?: string;
  enrollmentId?: string | null;
  rating?: number;
  title?: string | null;
}

export interface LearningExperienceSocialServicesDiscussionReply {
  authorId?: string;
  content?: string | null;
  createdAt?: string;
  discussionId?: string;
  id?: string;
  isAcceptedAnswer?: boolean;
  parentReplyId?: string | null;
  upvoteCount?: number;
}

export interface LearningExperienceSocialServicesPersonalizedFeedItem {
  courseId?: string | null;
  createdAt?: string;
  discussionId?: string | null;
  expiresAt?: string;
  id?: string;
  isViewed?: boolean;
  itemType?: LearningExperienceSocialFeedItemType;
  learningPathId?: string | null;
  reason?: string | null;
  relevanceScore?: number;
  reviewId?: string | null;
}

export interface LearningExperienceSocialServicesWishlistPreferencesInput {
  notifyOnSale?: boolean;
  notifyOnUpdate?: boolean;
}

export interface LearningWorkspacesLearnerAnnouncement {
  content?: string | null;
  courseId?: string;
  courseSlug?: string | null;
  courseTitle?: string | null;
  createdAt?: string;
  discussionId?: string;
  lastActivityAt?: string | null;
  title?: string | null;
}

export interface LearningWorkspacesLearnerAssessmentDeadline {
  assessmentId?: string;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  courseId?: string;
  courseSlug?: string | null;
  courseTitle?: string | null;
  dueAt?: string | null;
  groupId?: string | null;
  maxScore?: number;
  passingScore?: number;
  submissionStatus?: string | null;
  title?: string | null;
  type?: string | null;
}

export interface LearningWorkspacesLearnerAssessment {
  allowLateSubmissions?: boolean;
  assessmentId?: string;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  description?: string | null;
  dueAt?: string | null;
  groupId?: string | null;
  isRequired?: boolean;
  lateSubmissionDeadline?: string | null;
  maxAttempts?: number | null;
  maxScore?: number;
  order?: number;
  passingScore?: number;
  presentationMode?: string | null;
  submissionModalities?: string | null;
  timeLimitMinutes?: number | null;
  title?: string | null;
  type?: string | null;
}

export interface LearningWorkspacesLearnerAssessmentGroup {
  description?: string | null;
  groupId?: string;
  name?: string | null;
  order?: number;
  weightPercent?: number;
}

export interface LearningWorkspacesLearnerAssessmentSubmission {
  assessmentId?: string;
  attemptNumber?: number;
  enrollmentId?: string;
  feedback?: string | null;
  gradedAt?: string | null;
  isLate?: boolean;
  passed?: boolean | null;
  score?: number | null;
  startedAt?: string;
  status?: string | null;
  submissionId?: string;
  submittedAt?: string | null;
}

export interface LearningWorkspacesLearnerCertificate {
  certificateId?: string;
  certificateNumber?: string | null;
  courseId?: string;
  courseName?: string | null;
  enrollmentId?: string;
  expiresAt?: string | null;
  issuedAt?: string;
  recipientName?: string | null;
  status?: string | null;
  verificationUrl?: string | null;
}

export interface LearningWorkspacesLearnerCohort {
  cohortId?: string;
  currentEnrollmentCount?: number;
  description?: string | null;
  endDate?: string;
  instructorId?: string | null;
  maxCapacity?: number;
  meetingSchedule?: string | null;
  name?: string | null;
  startDate?: string;
  status?: string | null;
}

export interface LearningWorkspacesLearnerContent {
  activitySettings?: string | null;
  body?: string | null;
  contentId?: string;
  description?: string | null;
  estimatedMinutes?: number | null;
  gradingMethod?: string | null;
  isRequired?: boolean;
  lessonFormat?: string | null;
  maxPoints?: number | null;
  parentId?: string | null;
  sortOrder?: number;
  title?: string | null;
  type?: string | null;
  visibility?: string | null;
}

export interface LearningWorkspacesLearnerContentProgress {
  attempts?: number;
  completedAt?: string | null;
  contentId?: string;
  firstAccessedAt?: string | null;
  lastAccessedAt?: string | null;
  maxScore?: number | null;
  progressPercentage?: number;
  score?: number | null;
  status?: string | null;
  timeSpentSeconds?: number;
}

export interface LearningWorkspacesLearnerCourseSummary {
  category?: string | null;
  completedItems?: number;
  completionStatus?: string | null;
  courseId?: string;
  currentContentId?: string | null;
  currentContentTitle?: string | null;
  currentContentType?: string | null;
  description?: string | null;
  difficulty?: string | null;
  enrolledAt?: string;
  enrollmentId?: string;
  enrollmentStatus?: string | null;
  estimatedHours?: number | null;
  finalGrade?: number | null;
  progressPercentage?: number;
  remainingMinutes?: number;
  slug?: string | null;
  thumbnail?: string | null;
  title?: string | null;
  totalItems?: number;
}

export interface LearningWorkspacesLearnerCourseWorkspace {
  assessmentGroups?: Array<LearningWorkspacesLearnerAssessmentGroup> | null;
  assessments?: Array<LearningWorkspacesLearnerAssessment> | null;
  calendar?: Array<LearningWorkspacesLearnerScheduleEntry> | null;
  certificates?: Array<LearningWorkspacesLearnerCertificate> | null;
  cohort?: LearningWorkspacesLearnerCohort;
  content?: Array<LearningWorkspacesLearnerContent> | null;
  course?: LearningWorkspacesLearnerCourseSummary;
  discussions?: Array<LearningWorkspacesLearnerDiscussion> | null;
  progress?: Array<LearningWorkspacesLearnerContentProgress> | null;
  submissions?: Array<LearningWorkspacesLearnerAssessmentSubmission> | null;
}

export interface LearningWorkspacesLearnerDashboard {
  announcements?: Array<LearningWorkspacesLearnerAnnouncement> | null;
  certificates?: Array<LearningWorkspacesLearnerCertificate> | null;
  courses?: Array<LearningWorkspacesLearnerCourseSummary> | null;
  deadlines?: Array<LearningWorkspacesLearnerAssessmentDeadline> | null;
  grades?: Array<LearningWorkspacesLearnerGradeSummary> | null;
  upcoming?: Array<LearningWorkspacesLearnerScheduleEntry> | null;
}

export interface LearningWorkspacesLearnerDiscussion {
  authorId?: string;
  content?: string | null;
  contentId?: string | null;
  createdAt?: string;
  discussionId?: string;
  isPinned?: boolean;
  isResolved?: boolean;
  lastActivityAt?: string | null;
  replyCount?: number;
  title?: string | null;
  viewCount?: number;
}

export interface LearningWorkspacesLearnerGradeItem {
  assessmentId?: string;
  availableFrom?: string | null;
  availableUntil?: string | null;
  contentId?: string | null;
  dueAt?: string | null;
  feedback?: string | null;
  gradedAt?: string | null;
  groupId?: string | null;
  maxScore?: number;
  passed?: boolean | null;
  passingScore?: number;
  score?: number | null;
  submissionStatus?: string | null;
  title?: string | null;
  type?: string | null;
}

export interface LearningWorkspacesLearnerGradeSummary {
  courseId?: string;
  courseSlug?: string | null;
  courseTitle?: string | null;
  earnedPoints?: number | null;
  finalGrade?: number | null;
  gradedAssessments?: number;
  groups?: Array<LearningWorkspacesLearnerAssessmentGroup> | null;
  items?: Array<LearningWorkspacesLearnerGradeItem> | null;
  percentage?: number | null;
  possiblePoints?: number | null;
  totalAssessments?: number;
}

export interface LearningWorkspacesLearnerScheduleEntry {
  assessmentId?: string | null;
  availableFrom?: string | null;
  availableUntil?: string | null;
  cohortId?: string;
  cohortName?: string | null;
  contentId?: string | null;
  courseId?: string;
  courseSlug?: string | null;
  courseTitle?: string | null;
  dueAt?: string | null;
  endsAt?: string | null;
  location?: string | null;
  meetingUrl?: string | null;
  scheduleItemId?: string;
  startsAt?: string | null;
  status?: string | null;
  title?: string | null;
  type?: string | null;
}

export interface LearningWorkspacesLearnerSearchResult {
  courseId?: string;
  courseSlug?: string | null;
  description?: string | null;
  id?: string;
  kind?: string | null;
  route?: string | null;
  title?: string | null;
}

export interface Money {
  amount?: number;
  currency?: string | null;
}

export interface MvcProblemDetails {
  detail?: string | null;
  instance?: string | null;
  status?: number | null;
  title?: string | null;
  type?: string | null;
  [key: string]: any;
}

export type NotificationsNotificationChannel = 'InApp' | 'Email' | 'Push' | 'Sms' | 'Slack' | 'Discord' | 'Webhook';

export type ObjectsAttestationConveyancePreference = 'None' | 'Indirect' | 'Direct' | 'Enterprise';

export type ObjectsAttestationStatementFormatIdentifier = 'Packed' | 'Tpm' | 'AndroidKey' | 'AndroidSafetyNet' | 'FidoU2f' | 'Apple' | 'None';

export interface ObjectsAuthenticationExtensionsClientInputs {
  credProps?: boolean | null;
  credentialProtectionPolicy?: ObjectsCredentialProtectionPolicy;
  enforceCredentialProtectionPolicy?: boolean | null;
  'example.extension.bool'?: boolean | null;
  exts?: boolean | null;
  largeBlob?: ObjectsAuthenticationExtensionsLargeBlobInputs;
  prf?: ObjectsAuthenticationExtensionsPRFInputs;
  uvm?: boolean | null;
}

export interface ObjectsAuthenticationExtensionsLargeBlobInputs {
  read?: boolean;
  support?: ObjectsLargeBlobSupport;
  write?: string | null;
}

export interface ObjectsAuthenticationExtensionsPRFInputs {
  eval?: ObjectsAuthenticationExtensionsPRFValues;
  evalByCredential?: KeyValuePairStringAuthenticationExtensionsPRFValues;
}

export interface ObjectsAuthenticationExtensionsPRFValues {
  first: string | null;
  second?: string | null;
}

export type ObjectsAuthenticatorAttachment = 'Platform' | 'CrossPlatform';

export type ObjectsAuthenticatorTransport = 'Usb' | 'Nfc' | 'Ble' | 'SmartCard' | 'Hybrid' | 'Internal';

export type ObjectsCOSEAlgorithm = 'RS1' | 'RS512' | 'RS384' | 'RS256' | 'ES256K' | 'PS512' | 'PS384' | 'PS256' | 'ES512' | 'ES384' | 'EdDSA' | 'ES256';

export type ObjectsCredentialProtectionPolicy = 'UserVerificationOptional' | 'UserVerificationOptionalWithCredentialIdList' | 'UserVerificationRequired';

export type ObjectsLargeBlobSupport = 'Required' | 'Preferred';

export interface ObjectsPublicKeyCredentialDescriptor {
  id?: string | null;
  transports?: Array<ObjectsAuthenticatorTransport> | null;
  type?: ObjectsPublicKeyCredentialType;
}

export type ObjectsPublicKeyCredentialHint = 'SecurityKey' | 'ClientDevice' | 'Hybrid';

export type ObjectsPublicKeyCredentialType = 'PublicKey' | 'Invalid';

export type ObjectsResidentKeyRequirement = 'Required' | 'Preferred' | 'Discouraged';

export type ObjectsUserVerificationRequirement = 'Required' | 'Preferred' | 'Discouraged';

export interface PagedResultOfGameGuildCommerceProductsProductDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<CommerceProductsProduct> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildCommerceProductsPromoCodeDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<CommerceProductsPromoCode> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildCommerceProductsSupportTicketDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<CommerceProductsSupportTicket> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildCommerceSubscriptionsSubscription {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<CommerceSubscriptionsSubscription> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildCommerceSubscriptionsSubscriptionNotificationDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<CommerceSubscriptionsSubscriptionNotification> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildIdentityTenantsTenant {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<IdentityTenantsTenant> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildIdentityTenantsTenantAuditLogEntry {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<IdentityTenantsTenantAuditLogEntry> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildIdentityUsersUserDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<IdentityUsersUserDto> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildIdentityUsersUserNotificationDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<IdentityUsersUserNotificationDto> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export interface PagedResultOfGameGuildIdentityUsersUserProfileDto {
  hasNextPage?: boolean;
  hasPreviousPage?: boolean;
  items?: Array<IdentityUsersUserProfileDto> | null;
  pageNumber?: number;
  pageSize?: number;
  skip?: number;
  take?: number;
  totalCount?: number;
  totalPages?: number;
}

export type ProgramCategory =
  | 'General'
  | 'Programming'
  | 'DataScience'
  | 'WebDevelopment'
  | 'MobileDevelopment'
  | 'GameDevelopment'
  | 'AI'
  | 'Cybersecurity'
  | 'DevOps'
  | 'Database'
  | 'Business'
  | 'Design'
  | 'Marketing'
  | 'ProjectManagement'
  | 'PersonalDevelopment'
  | 'CreativeArts'
  | 'Science'
  | 'Language'
  | 'Other';

export interface ProjectsAddCollaboratorInput {
  email?: string | null;
  expiresAt?: string | null;
  message?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  requireAcceptance?: boolean;
}

export interface ProjectsAddProjectCollaboratorInput {
  permissions?: string | null;
  role?: string | null;
  userId: string;
}

export interface ProjectsCollaborator {
  id?: string;
  isActive?: boolean;
  joinedAt?: string;
  permissions?: string | null;
  role?: string | null;
  userId?: string;
  userName?: string | null;
}

export interface ProjectsCreateProjectInput {
  categoryId?: string | null;
  description?: string | null;
  downloadUrl?: string | null;
  imageUrl?: string | null;
  repositoryUrl?: string | null;
  shortDescription?: string | null;
  status?: ContentStatus;
  tags?: Array<string> | null;
  title: string;
  type?: ProjectsProjectType;
  visibility?: ContentVisibility;
  websiteUrl?: string | null;
}

export type ProjectsDevelopmentStatus = 'Planning' | 'InDevelopment' | 'Alpha' | 'Beta' | 'Released' | 'Completed' | 'OnHold' | 'Cancelled' | 'Archived';

export interface ProjectsEffectivePermission {
  expiresAt?: string | null;
  isOwner?: boolean;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  resourceId?: string;
  resourceType?: string | null;
}

export interface ProjectsInvitationResult {
  errorMessage?: string | null;
  invitationId?: string | null;
  success?: boolean;
}

export interface ProjectsInviteProjectCollaboratorInput {
  email?: string | null;
  expiresAt?: string | null;
  permissions?: string | null;
  role?: string | null;
  userId?: string | null;
}

export interface ProjectsLinkProjectStoreProductInput {
  productId?: string;
}

export interface ProjectsPermissionUpdateResult {
  errorMessage?: string | null;
  success?: boolean;
}

export interface ProjectsProject {
  averageRating?: number | null;
  category?: ProjectsProjectCategory;
  categoryId?: string | null;
  collaborators?: Array<ProjectsProjectCollaborator> | null;
  copyright?: string | null;
  createdAt: string;
  createdBy?: IdentityUsersUser;
  createdById?: string | null;
  deletedAt?: string | null;
  description?: string | null;
  developmentStatus?: ProjectsDevelopmentStatus;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  downloadUrl?: string | null;
  featuredImageUrl?: string | null;
  feedbackCount?: number;
  feedbacks?: Array<ProjectsProjectFeedback> | null;
  followerCount?: number;
  followers?: Array<ProjectsProjectFollower> | null;
  id?: string;
  imageUrl?: string | null;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isInJam?: boolean;
  isNew?: boolean;
  jamSubmissions?: Array<ProjectsProjectJamSubmission> | null;
  latestVersion?: ProjectsProjectVersion;
  license?: string | null;
  projectMetadata?: ProjectsProjectMetadata;
  publishedAt?: string | null;
  releases?: Array<ProjectsProjectRelease> | null;
  repositoryUrl?: string | null;
  shortDescription?: string | null;
  slug: string;
  socialLinks?: string | null;
  status: ContentStatus;
  tags?: string | null;
  teamCount?: number;
  teams?: Array<ProjectsProjectTeam> | null;
  tenantId?: string | null;
  title: string;
  type?: ProjectsProjectType;
  updatedAt: string;
  version?: number;
  versions?: Array<ProjectsProjectVersion> | null;
  visibility: ContentVisibility;
  websiteUrl?: string | null;
}

export interface ProjectsProjectCategory {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  name: string;
  projects?: Array<ProjectsProject> | null;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface ProjectsProjectCollaborator {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  joinedAt?: string;
  leftAt?: string | null;
  permissions: string;
  project?: ProjectsProject;
  projectId?: string;
  role: string;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export interface ProjectsProjectCollaboratorDto {
  email?: string | null;
  expiresAt?: string | null;
  invitedBy?: string | null;
  isOwner?: boolean;
  joinedAt?: string;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
  profilePictureUrl?: string | null;
  role?: string | null;
  userId?: string;
  userName?: string | null;
}

export interface ProjectsProjectFeedback {
  categories?: string | null;
  content?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  helpfulVotes?: number;
  id?: string;
  isDeleted?: boolean;
  isFeatured?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isVerified?: boolean;
  platform?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  projectVersion?: string | null;
  rating?: number;
  status?: ContentStatus;
  tenantId?: string | null;
  title: string;
  totalVotes?: number;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export interface ProjectsProjectFollower {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  emailNotifications?: boolean;
  followedAt?: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  notificationSettings?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  pushNotifications?: boolean;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export interface ProjectsProjectInvitation {
  expiresAt?: string | null;
  id?: string;
  invitedAt?: string;
  invitedByUserId?: string;
  invitedEmail?: string | null;
  invitedUserId?: string | null;
  permissions?: string | null;
  projectId?: string;
  projectTitle?: string | null;
  respondedAt?: string | null;
  role?: string | null;
  status?: ProjectsProjectInvitationStatus;
  token?: string | null;
}

export type ProjectsProjectInvitationStatus = 'Pending' | 'Accepted' | 'Declined' | 'Revoked' | 'Expired';

export interface ProjectsProjectJamSubmission {
  awardDetails?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  finalScore?: number | null;
  hasAward?: boolean;
  id?: string;
  isDeleted?: boolean;
  isEligible?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  jam?: GameJamsJam;
  jamId?: string | null;
  metadata?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  ranking?: number | null;
  scores?: Array<GameJamsJamScore> | null;
  submissionNotes?: string | null;
  submittedAt?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface ProjectsProjectMetadata {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  downloadCount?: number;
  followerCount?: number;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  project: ProjectsProject;
  projectId?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
  viewCount?: number;
}

export interface ProjectsProjectRelease {
  buildNumber?: string | null;
  checksum?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  downloadCount?: number;
  downloadUrl?: string | null;
  fileSize?: number | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isLatest?: boolean;
  isNew?: boolean;
  isPrerelease?: boolean;
  project?: ProjectsProject;
  projectId?: string;
  releaseMetadata?: string | null;
  releaseNotes?: string | null;
  releaseType?: string | null;
  releaseVersion: string;
  releasedAt?: string;
  status?: ContentStatus;
  supportedPlatforms?: string | null;
  systemRequirements?: string | null;
  tenantId?: string | null;
  title: string;
  updatedAt: string;
  version?: number;
}

export interface ProjectsProjectRoleTemplate {
  description?: string | null;
  name?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
}

export interface ProjectsProjectStatistics {
  activeTeamCount?: number;
  averageRating?: number | null;
  awardCount?: number;
  calculatedAt?: string;
  collaboratorCount?: number;
  downloadsLast30Days?: number;
  feedbackCount?: number;
  followerCount?: number;
  jamSubmissionCount?: number;
  newFollowersLast30Days?: number;
  popularityRank?: number | null;
  projectId?: string;
  releaseCount?: number;
  totalDownloads?: number;
  trendingScore?: number;
  viewsLast30Days?: number;
}

export interface ProjectsProjectStoreProductProjection {
  linkId?: string;
  productId?: string;
  projectId?: string;
}

export interface ProjectsProjectTeam {
  assignedAt?: string;
  contributionPercentage?: number;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  endedAt?: string | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  notes?: string | null;
  permissions?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  role: string;
  team?: ProjectsTeam;
  teamId?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export type ProjectsProjectType = 'Game' | 'Tool' | 'Art' | 'Music' | 'Educational' | 'Plugin' | 'Template' | 'Library' | 'Other';

export interface ProjectsProjectVersion {
  createdAt: string;
  createdBy: IdentityUsersUser;
  createdById?: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  downloadCount?: number;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  project: ProjectsProject;
  projectId?: string;
  releaseNotes?: string | null;
  status: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
  versionNumber: string;
}

export interface ProjectsShareProjectInput {
  permissions?: string | null;
  role?: string | null;
  userId: string;
}

export interface ProjectsShareProjectWithRoleInput {
  expiresAt?: string | null;
  message?: string | null;
  notifyUsers?: boolean;
  requireAcceptance?: boolean;
  roleName?: string | null;
  userEmails?: Array<string> | null;
  userIds?: Array<string> | null;
}

export interface ProjectsShareResult {
  errorMessage?: string | null;
  failureCount?: number;
  success?: boolean;
  successCount?: number;
}

export interface ProjectsTeam {
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  members?: Array<ProjectsTeamMember> | null;
  name: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface ProjectsTeamMember {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  joinedAt?: string;
  role?: string | null;
  team?: ProjectsTeam;
  teamId?: string;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export interface ProjectsUpdateCollaboratorInput {
  expiresAt?: string | null;
  permissions?: Array<IdentityAuthorizationPermissionType> | null;
}

export interface ProjectsUpdateProjectCollaboratorInput {
  permissions?: string | null;
  role?: string | null;
}

export interface ProjectsUpdateProjectInput {
  categoryId?: string | null;
  description?: string | null;
  downloadUrl?: string | null;
  imageUrl?: string | null;
  repositoryUrl?: string | null;
  shortDescription?: string | null;
  status?: ContentStatus;
  tags?: Array<string> | null;
  title?: string | null;
  type?: ProjectsProjectType;
  visibility?: ContentVisibility;
  websiteUrl?: string | null;
}

export interface ResourcesArchiveResourceUsageRecordsInput {
  olderThan?: string;
}

export interface ResourcesCheckResourceQuotaInput {
  amount?: number;
}

export interface ResourcesCleanupOrphanedResourcesInput {
  dryRun?: boolean;
  resourceTypes?: Array<ResourcesResourceUsageType> | null;
}

export interface ResourcesEffectiveSettingOutput {
  isUserOverride?: boolean;
  key?: string | null;
  value?: string | null;
}

export interface ResourcesRecordTenantResourceUsageInput {
  count?: number;
  metadata?: Record<string, string> | null;
  periodEnd?: string;
  periodStart?: string;
  resourceUsageType?: ResourcesResourceUsageType;
}

export interface ResourcesRecordUserResourceUsageInput {
  count?: number;
  metadata?: Record<string, string> | null;
  periodEnd?: string;
  periodStart?: string;
  resourceUsageType?: ResourcesResourceUsageType;
}

export interface ResourcesResourceMetadata {
  category?: string | null;
  createdAt: string;
  dataType?: string | null;
  deletedAt?: string | null;
  description?: string | null;
  displayOrder?: number;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isSystemManaged?: boolean;
  key: string;
  resourceId?: string | null;
  rowVersion?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  userId?: string | null;
  value?: string | null;
  version?: number;
}

export interface ResourcesResourceQuotaEnforcementResult {
  currentUsage?: number;
  excessAmount?: number;
  hardLimit?: number | null;
  isAllowed?: boolean;
  isHardLimitExceeded?: boolean;
  isSoftLimitExceeded?: boolean;
  message?: string | null;
  nextReset?: string | null;
  remainingQuota?: number | null;
  softLimit?: number | null;
  type?: ResourcesResourceUsageType;
  usagePercentage?: number;
}

export type ResourcesResourceQuotaPeriod = 'Daily' | 'Weekly' | 'Monthly' | 'Quarterly' | 'Yearly' | 'Unlimited';

export interface ResourcesResourceQuotaOutput {
  currentUsage?: number;
  description?: string | null;
  hardLimit?: number | null;
  id?: string;
  isActive?: boolean;
  isHardLimitExceeded?: boolean;
  isSoftLimitExceeded?: boolean;
  lastResetDate?: string;
  limit?: number;
  nextResetDate?: string;
  period?: ResourcesResourceQuotaPeriod;
  remainingQuota?: number;
  shouldReset?: boolean;
  softLimit?: number | null;
  softLimitPercentage?: number;
  tenantId?: string;
  type?: ResourcesResourceUsageType;
  usagePercentage?: number;
}

export interface ResourcesResourceSettings {
  allowUserOverride?: boolean;
  category?: string | null;
  createdAt: string;
  dataType?: string | null;
  defaultValue?: string | null;
  deletedAt?: string | null;
  description?: string | null;
  displayOrder?: number;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isSystemManaged?: boolean;
  key: string;
  rowVersion?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  userId?: string | null;
  validationRules?: string | null;
  value?: string | null;
  version?: number;
}

export type ResourcesResourceUsageType =
  | 'Users'
  | 'Projects'
  | 'Storage'
  | 'ApiCalls'
  | 'Programs'
  | 'Courses'
  | 'FeatureFlags'
  | 'SubscriptionPlans'
  | 'Products'
  | 'TestingSessions'
  | 'Roles'
  | 'Tenants'
  | 'Subscriptions'
  | 'SLOs'
  | 'AccessReviewCampaigns'
  | 'SoDRules'
  | 'AbacPolicies'
  | 'ConditionalPolicies'
  | 'Wallets'
  | 'Disputes'
  | 'PromoCodes'
  | 'Orders'
  | 'AuditEntries'
  | 'Assets'
  | 'AssetStorage'
  | 'AssetDownloads'
  | 'AssetTransformations'
  | 'AiRequests'
  | 'AiTokens';

export interface ResourcesSetQuotaInput {
  hardLimit?: number | null;
  isActive?: boolean;
  period?: ResourcesResourceQuotaPeriod;
  resetTime?: string | null;
  softLimit?: number | null;
}

export interface ResourcesSetResourceMetadataInput {
  category?: string | null;
  dataType?: string | null;
  description?: string | null;
  displayOrder?: number | null;
  value?: string | null;
}

export interface ResourcesSetResourceSettingsInput {
  allowUserOverride?: boolean | null;
  category?: string | null;
  dataType?: string | null;
  defaultValue?: string | null;
  description?: string | null;
  displayOrder?: number | null;
  validationRules?: string | null;
  value?: string | null;
}

export interface ResourcesSetUserResourceSettingsInput {
  value?: string | null;
}

export interface ResourcesToggleResourceQuotaInput {
  isActive?: boolean;
}

export type ResourcesTrendGranularity = 'Daily' | 'Weekly' | 'Monthly';

export interface ResourcesUsageRecord {
  averagePerDay?: number | null;
  count?: number;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  metadata?: string | null;
  peakUsage?: number | null;
  peakUsageDate?: string | null;
  periodEnd?: string;
  periodStart?: string;
  resourceId?: string | null;
  resourceQuotaId?: string | null;
  source?: string | null;
  tenantId?: string | null;
  type?: ResourcesResourceUsageType;
  updatedAt: string;
  usageAmount?: number;
  userId?: string | null;
  version?: number;
}

export interface ResourcesUsageTrendDataPoint {
  period?: string;
  tenantCount?: number;
  totalUsage?: number;
}

export interface ResourcesUsageTrendsResult {
  dataPoints?: Array<ResourcesUsageTrendDataPoint> | null;
  endDate?: string;
  granularity?: ResourcesTrendGranularity;
  startDate?: string;
  type?: ResourcesResourceUsageType;
}

export interface SocialBlogBlogPost {
  allowComments?: boolean;
  authorId?: string;
  commentsCount?: number;
  content?: string | null;
  coverImageUrl?: string | null;
  createdAt?: string;
  excerpt?: string | null;
  id?: string;
  isFeatured?: boolean;
  likesCount?: number;
  publishedAt?: string | null;
  readTimeMinutes?: number;
  slug?: string | null;
  status?: SocialBlogBlogPostStatus;
  tenantId?: string | null;
  title?: string | null;
  updatedAt?: string;
  viewsCount?: number;
}

export type SocialBlogBlogPostStatus = 'Draft' | 'Published' | 'Archived';

export interface SocialBlogCreateBlogPostInput {
  authorId?: string;
  content?: string | null;
  slug?: string | null;
  tenantId?: string | null;
  title?: string | null;
}

export interface SocialFeedAddFeedItemInput {
  authorId?: string;
  contentCreatedAt?: string | null;
  contentId?: string;
  contentType?: SocialFeedFeedContentType;
  reason?: SocialFeedFeedItemReason;
  relevanceScore?: number;
  userId?: string;
}

export type SocialFeedFeedContentType = 'Post' | 'BlogPost' | 'CourseReview' | 'ProjectUpdate' | 'Achievement' | 'CourseCompletion';

export interface SocialFeedFeedItem {
  authorId?: string;
  contentCreatedAt?: string;
  contentId?: string;
  contentType?: SocialFeedFeedContentType;
  createdAt?: string;
  id?: string;
  isHidden?: boolean;
  isRead?: boolean;
  reason?: SocialFeedFeedItemReason;
  relevanceScore?: number;
  userId?: string;
}

export type SocialFeedFeedItemReason = 'Following' | 'Trending' | 'Recommended' | 'Mentioned' | 'Replied' | 'Liked' | 'InNetwork';

export interface SocialGroupsApproveSocialGroupMemberInput {
  approvedByUserId?: string;
}

export interface SocialGroupsChangeSocialGroupMemberRoleInput {
  role?: SocialGroupsSocialGroupMemberRole;
}

export interface SocialGroupsCreateSocialGroupInput {
  description?: string | null;
  name?: string | null;
  ownerId?: string;
  slug?: string | null;
  tenantId?: string | null;
  type?: SocialGroupsSocialGroupType;
  visibility?: SocialGroupsSocialGroupVisibility;
}

export interface SocialGroupsJoinSocialGroupInput {
  requestedRole?: SocialGroupsSocialGroupMemberRole;
  userId?: string;
}

export interface SocialGroupsSocialGroup {
  createdAt?: string;
  description?: string | null;
  id?: string;
  memberCount?: number;
  name?: string | null;
  ownerId?: string;
  pendingMemberCount?: number;
  slug?: string | null;
  status?: SocialGroupsSocialGroupStatus;
  tenantId?: string | null;
  type?: SocialGroupsSocialGroupType;
  updatedAt?: string;
  visibility?: SocialGroupsSocialGroupVisibility;
}

export interface SocialGroupsSocialGroupMember {
  approvedByUserId?: string | null;
  groupId?: string;
  id?: string;
  joinedAt?: string | null;
  removedAt?: string | null;
  requestedAt?: string;
  role?: SocialGroupsSocialGroupMemberRole;
  status?: SocialGroupsSocialGroupMembershipStatus;
  userId?: string;
}

export type SocialGroupsSocialGroupMemberRole = 'Owner' | 'Admin' | 'Moderator' | 'Member';

export type SocialGroupsSocialGroupMembershipStatus = 'Pending' | 'Active' | 'Rejected' | 'Removed';

export type SocialGroupsSocialGroupStatus = 'Active' | 'Archived' | 'Suspended';

export type SocialGroupsSocialGroupType = 'StudyGroup' | 'ProjectTeam' | 'InterestCommunity' | 'CourseCohort' | 'Institution' | 'GameJamTeam';

export type SocialGroupsSocialGroupVisibility = 'Public' | 'Private' | 'InviteOnly';

export interface SocialGroupsUpdateSocialGroupInput {
  description?: string | null;
  name?: string | null;
  slug?: string | null;
  type?: SocialGroupsSocialGroupType;
  visibility?: SocialGroupsSocialGroupVisibility;
}

export interface SocialProfilesAddProfilePortfolioItemBody {
  description?: string | null;
  displayOrder?: number;
  imageUrl?: string | null;
  isPinned?: boolean;
  projectId?: string | null;
  title?: string | null;
  url?: string | null;
}

export interface SocialProfilesAddProfileSkillBody {
  displayOrder?: number;
  name?: string | null;
  proficiency?: SocialProfilesProfileSkillProficiency;
}

export type SocialProfilesProfileAvailabilityStatus = 'NotSet' | 'OpenToWork' | 'OpenToCollaborate' | 'Busy' | 'Hidden';

export interface SocialProfilesProfilePortfolioItem {
  description?: string | null;
  displayOrder?: number;
  id?: string;
  imageUrl?: string | null;
  isPinned?: boolean;
  profileId?: string;
  projectId?: string | null;
  title?: string | null;
  url?: string | null;
}

export interface SocialProfilesProfileSkill {
  displayOrder?: number;
  id?: string;
  name?: string | null;
  proficiency?: SocialProfilesProfileSkillProficiency;
  profileId?: string;
}

export type SocialProfilesProfileSkillProficiency = 'Beginner' | 'Intermediate' | 'Advanced' | 'Expert';

export type SocialProfilesProfileVisibility = 'Private' | 'Connections' | 'Public';

export interface SocialProfilesSocialProfile {
  availabilityStatus?: SocialProfilesProfileAvailabilityStatus;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  bio?: string | null;
  completenessScore?: number;
  displayName?: string | null;
  followerCount?: number;
  followingCount?: number;
  handle?: string | null;
  headline?: string | null;
  id?: string;
  location?: string | null;
  portfolioItems?: Array<SocialProfilesProfilePortfolioItem> | null;
  postCount?: number;
  projectCount?: number;
  showActivity?: boolean;
  showPortfolio?: boolean;
  showSkills?: boolean;
  skills?: Array<SocialProfilesProfileSkill> | null;
  socialLinksJson?: string | null;
  timeZone?: string | null;
  userId?: string;
  verifiedAt?: string | null;
  visibility?: SocialProfilesProfileVisibility;
  websiteUrl?: string | null;
}

export interface SocialProfilesUpdateProfilePortfolioItemBody {
  description?: string | null;
  displayOrder?: number;
  imageUrl?: string | null;
  isPinned?: boolean;
  title?: string | null;
  url?: string | null;
}

export interface SocialProfilesUpdateProfilePrivacyBody {
  showActivity?: boolean;
  showPortfolio?: boolean;
  showSkills?: boolean;
  visibility?: SocialProfilesProfileVisibility;
}

export interface SocialProfilesUpdateProfileStatsBody {
  followerCount?: number;
  followingCount?: number;
  postCount?: number;
  projectCount?: number;
}

export interface SocialProfilesUpdateSocialProfileBody {
  availabilityStatus?: SocialProfilesProfileAvailabilityStatus;
  avatarUrl?: string | null;
  bannerUrl?: string | null;
  bio?: string | null;
  displayName?: string | null;
  handle?: string | null;
  headline?: string | null;
  location?: string | null;
  socialLinksJson?: string | null;
  timeZone?: string | null;
  websiteUrl?: string | null;
}

export interface SocialReactionsReaction {
  createdAt?: string;
  id?: string;
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  type?: SocialReactionsReactionType;
  updatedAt?: string;
  userId?: string;
}

export type SocialReactionsReactionTargetType = 'Post' | 'Comment' | 'BlogPost' | 'CourseReview' | 'Discussion' | 'Reply';

export type SocialReactionsReactionType = 'Like' | 'Love' | 'Insightful' | 'Celebrate' | 'Support' | 'Curious';

export interface SocialReactionsRemoveReactionInput {
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  userId?: string;
}

export interface SocialReactionsSetReactionInput {
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  type?: SocialReactionsReactionType;
  userId?: string;
}

export interface SocialReactionsTargetReactionSummary {
  counts?: { Celebrate?: number; Curious?: number; Insightful?: number; Like?: number; Love?: number; Support?: number } | null;
  targetId?: string;
  targetType?: SocialReactionsReactionTargetType;
  total?: number;
}

export type SystemDayOfWeek = 'Sunday' | 'Monday' | 'Tuesday' | 'Wednesday' | 'Thursday' | 'Friday' | 'Saturday';

export interface TenantInfo {
  id?: string;
  isActive?: boolean;
  name?: string | null;
  slug?: string | null;
}

export interface TestingLabAddTestingEventCommitteeMemberInput {
  isChair?: boolean;
  userId?: string;
}

export interface TestingLabAssignTestingLabRoleInput {
  expiresAt?: string | null;
  roleName?: string | null;
  tenantId?: string | null;
}

export interface TestingLabAssignTestingProjectApplicationSlotInput {
  slotId?: string;
}

export interface TestingLabAssignTestingProjectToTesterInput {
  applicationId?: string;
}

export type TestingLabAttendanceStatus = 'Registered' | 'Present' | 'Completed' | 'NoShow';

export interface TestingLabCancelTestingEventInput {
  reason?: string | null;
}

export interface TestingLabCastTestingApplicationVoteInput {
  comments?: string | null;
  decision?: TestingLabTestingApplicationVoteDecision;
}

export interface TestingLabConfigureTestingEventLearningInput {
  cohortId?: string | null;
  courseId?: string;
  learningActivityId?: string;
  requirement?: TestingLabTestingLearningCompletionRequirement;
}

export interface TestingLabCreateSimpleTestingInput {
  description?: string | null;
  downloadUrl?: string | null;
  endDate?: string | null;
  feedbackFormContent?: string | null;
  instructionsContent?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsUrl?: string | null;
  maxTesters?: number | null;
  projectId?: string | null;
  startDate?: string | null;
  teamIdentifier?: string | null;
  title: string;
  versionNumber: string;
}

export interface TestingLabCreateTestingEventInput {
  applicationsCloseAt?: string;
  applicationsOpenAt?: string;
  approvalMode?: TestingLabTestingEventApprovalMode;
  description?: string | null;
  endsAt?: string;
  mode?: TestingLabTestingEventMode;
  name?: string | null;
  requiresFeedback?: boolean;
  startsAt?: string;
}

export interface TestingLabCreateTestingLabRoleInput {
  description?: string | null;
  name?: string | null;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabCreateTestingLabSettings {
  allowPublicSignups?: boolean;
  defaultSessionDuration: number;
  description?: string | null;
  enableNotifications?: boolean;
  labName: string;
  maxSimultaneousSessions: number;
  requireApproval?: boolean;
  timezone: string;
}

export interface TestingLabCreateTestingLocation {
  address?: string | null;
  city?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  country?: string | null;
  description?: string | null;
  equipmentAvailable?: string | null;
  isVirtual?: boolean;
  maxProjectsCapacity?: number;
  maxTestersCapacity?: number;
  name?: string | null;
  postalCode?: string | null;
  state?: string | null;
  status?: TestingLabLocationStatus;
  virtualUrl?: string | null;
}

export interface TestingLabCreateTestingInput {
  description?: string | null;
  downloadUrl?: string | null;
  endDate: string;
  feedbackFormContent?: string | null;
  instructionsContent?: string | null;
  instructionsFileId?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsUrl?: string | null;
  maxTesters?: number | null;
  projectVersionId: string;
  startDate: string;
  status: TestingLabTestingRequestStatus;
  title: string;
}

export interface TestingLabCreateTestingSession {
  endTime: string;
  locationId: string;
  managerUserId: string;
  maxProjects: number;
  maxTesters: number;
  sessionDate: string;
  sessionName: string;
  startTime: string;
  status: TestingLabSessionStatus;
  testingRequestId: string;
}

export interface TestingLabDecideTestingProjectApplicationInput {
  rationale?: string | null;
  slotId?: string | null;
}

export type TestingLabFeedbackFormType = 'General' | 'BugReport' | 'Usability' | 'Performance' | 'Accessibility';

export type TestingLabFeedbackQuality = 'Low' | 'Medium' | 'High';

export interface TestingLabFeedbackQualityRating {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  feedback?: TestingLabTestingFeedback;
  feedbackId: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNegative?: boolean;
  isNew?: boolean;
  isPositive?: boolean;
  qualityRating: number;
  ratedBy?: IdentityUsersUser;
  ratedByUserId: string;
  reason?: string | null;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface TestingLabFeedbackInput {
  additionalNotes?: string | null;
  feedbackData?: string | null;
  feedbackFormId?: string;
  sessionId?: string | null;
  testingContext?: TestingLabTestingContext;
}

export interface TestingLabGrantResourcePermissionInput {
  action?: string | null;
  expiresAt?: string | null;
  tenantId?: string | null;
}

export type TestingLabInstructionType = 'Text' | 'Url' | 'File';

export interface TestingLabLinkSessionProjectInput {
  notes?: string | null;
  projectId?: string;
  projectVersionId?: string | null;
}

export type TestingLabLocationStatus = 'Active' | 'Maintenance' | 'Inactive';

export type TestingLabParticipationStatus = 'Registered' | 'Active' | 'Completed' | 'Withdrawn' | 'Suspended';

export interface TestingLabPublicTestingEventProjection {
  applicationCount?: number;
  applicationsCloseAt?: string;
  applicationsOpenAt?: string;
  approvalMode?: TestingLabTestingEventApprovalMode;
  description?: string | null;
  endsAt?: string;
  id?: string;
  mode?: TestingLabTestingEventMode;
  name?: string | null;
  requiresFeedback?: boolean;
  slots?: Array<TestingLabPublicTestingEventSlotProjection> | null;
  startsAt?: string;
  status?: TestingLabTestingEventStatus;
}

export interface TestingLabPublicTestingEventSlotProjection {
  approvedProjectCount?: number;
  availableProjectCount?: number | null;
  availableTesterCount?: number | null;
  campusName?: string | null;
  endsAt?: string;
  eventId?: string;
  id?: string;
  maxProjects?: number | null;
  maxTesters?: number | null;
  mode?: TestingLabTestingEventMode;
  registeredTesterCount?: number;
  roomName?: string | null;
  startsAt?: string;
}

export interface TestingLabRateFeedbackQuality {
  quality?: TestingLabFeedbackQuality;
}

export interface TestingLabRegisterTestingEventSlotInput {
  notes?: string | null;
}

export type TestingLabRegistrationStatus = 'Registered' | 'Confirmed' | 'Cancelled' | 'Attended' | 'NoShow';

export type TestingLabRegistrationType = 'ProjectMember' | 'Tester';

export interface TestingLabReportFeedback {
  reason?: string | null;
}

export interface TestingLabSessionProjectProjection {
  isActive?: boolean;
  linkId?: string;
  projectId?: string;
  projectVersionId?: string | null;
  sessionId?: string;
}

export interface TestingLabSessionRegistration {
  attendanceDuration?: string | null;
  attendanceStatus?: TestingLabAttendanceStatus;
  attendedAt?: string | null;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  confirmedAt?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isCheckedIn?: boolean;
  isCheckedOut?: boolean;
  isConfirmed?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  notes?: string | null;
  registeredAt: string;
  registrationNotes?: string | null;
  registrationType?: TestingLabRegistrationType;
  session?: TestingLabTestingSession;
  sessionId: string;
  status?: TestingLabRegistrationStatus;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
}

export interface TestingLabSessionRegistrationInput {
  notes?: string | null;
  registrationType?: TestingLabRegistrationType;
}

export type TestingLabSessionStatus = 'Scheduled' | 'Active' | 'Completed' | 'Cancelled';

export interface TestingLabSessionWaitlist {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  position: number;
  registrationNotes?: string | null;
  registrationType: TestingLabRegistrationType;
  session?: TestingLabTestingSession;
  sessionId?: string;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export interface TestingLabSubmitFeedback {
  additionalNotes?: string | null;
  feedbackResponses: string;
  overallRating?: number | null;
  sessionId?: string | null;
  testingRequestId: string;
  wouldRecommend?: boolean | null;
}

export interface TestingLabSubmitTestingEventFeedbackInput {
  additionalNotes?: string | null;
  feedbackData?: string | null;
  overallRating?: number | null;
  wouldRecommend?: boolean | null;
}

export interface TestingLabSubmitTestingProjectApplicationInput {
  preferredAvailability?: string | null;
  projectId?: string;
  projectVersionId?: string | null;
}

export type TestingLabTestingApplicationStatus = 'Pending' | 'UnderReview' | 'Approved' | 'Rejected' | 'Waitlisted' | 'Withdrawn';

export interface TestingLabTestingApplicationVote {
  application?: TestingLabTestingProjectApplication;
  applicationId?: string;
  comments?: string | null;
  createdAt: string;
  decision?: TestingLabTestingApplicationVoteDecision;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  reviewer?: IdentityUsersUser;
  reviewerId?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export type TestingLabTestingApplicationVoteDecision = 'Approve' | 'Reject' | 'Abstain';

export interface TestingLabTestingApplicationVoteProjection {
  comments?: string | null;
  createdAt?: string;
  decision?: TestingLabTestingApplicationVoteDecision;
  id?: string;
  reviewerId?: string;
}

export interface TestingLabTestingCommitteeMember {
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  event?: TestingLabTestingEvent;
  eventId?: string;
  id?: string;
  isActive?: boolean;
  isChair?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  tenantId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId?: string;
  version?: number;
}

export type TestingLabTestingContext = 'Online' | 'InPerson';

export interface TestingLabTestingEvent {
  applications?: Array<TestingLabTestingProjectApplication> | null;
  applicationsCloseAt?: string;
  applicationsOpenAt?: string;
  approvalMode?: TestingLabTestingEventApprovalMode;
  cancellationReason?: string | null;
  cancelledAt?: string | null;
  cohortId?: string | null;
  committeeMembers?: Array<TestingLabTestingCommitteeMember> | null;
  courseId?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  endsAt?: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  learningActivityId?: string | null;
  learningCompletionRequirement?: TestingLabTestingLearningCompletionRequirement;
  manager?: IdentityUsersUser;
  managerUserId?: string;
  mode?: TestingLabTestingEventMode;
  name: string;
  requiresFeedback?: boolean;
  slots?: Array<TestingLabTestingEventSlot> | null;
  startsAt?: string;
  status?: TestingLabTestingEventStatus;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export type TestingLabTestingEventApprovalMode = 'ManagerOnly' | 'Committee';

export interface TestingLabTestingEventCommitteeMemberProjection {
  eventId?: string;
  id?: string;
  isActive?: boolean;
  isChair?: boolean;
  userEmail?: string | null;
  userId?: string;
  userName?: string | null;
}

export interface TestingLabTestingEventFeedbackProjection {
  additionalNotes?: string | null;
  applicationId?: string;
  eventId?: string;
  feedbackData?: string | null;
  id?: string;
  overallRating?: number | null;
  submittedAt?: string;
  testerUserId?: string;
  wouldRecommend?: boolean | null;
}

export interface TestingLabTestingEventFeedbackReviewProjection {
  applicationId?: string;
  eventId?: string;
  feedback?: TestingLabTestingEventFeedbackProjection;
  fulfilledAt?: string | null;
  obligationId?: string;
  slotId?: string;
  status?: TestingLabTestingFeedbackObligationStatus;
  testerUserId?: string;
}

export type TestingLabTestingEventMode = 'Online' | 'InPerson' | 'Hybrid';

export interface TestingLabTestingEventProjection {
  applicationCount?: number;
  applicationsCloseAt?: string;
  applicationsOpenAt?: string;
  approvalMode?: TestingLabTestingEventApprovalMode;
  cohortId?: string | null;
  courseId?: string | null;
  description?: string | null;
  endsAt?: string;
  id?: string;
  learningActivityId?: string | null;
  learningCompletionRequirement?: TestingLabTestingLearningCompletionRequirement;
  managerUserId?: string;
  mode?: TestingLabTestingEventMode;
  name?: string | null;
  requiresFeedback?: boolean;
  slotCount?: number;
  startsAt?: string;
  status?: TestingLabTestingEventStatus;
  tenantId?: string | null;
}

export interface TestingLabTestingEventSlot {
  campusName?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  endsAt?: string;
  event?: TestingLabTestingEvent;
  eventId?: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isProjectCapacityUnlimited?: boolean;
  isTesterCapacityUnlimited?: boolean;
  location?: TestingLabTestingLocation;
  locationId?: string | null;
  maxProjects?: number | null;
  maxTesters?: number | null;
  meetingUrl?: string | null;
  mode?: TestingLabTestingEventMode;
  roomName?: string | null;
  startsAt?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface TestingLabTestingEventSlotProjection {
  approvedProjectCount?: number;
  campusName?: string | null;
  endsAt?: string;
  eventId?: string;
  id?: string;
  locationId?: string | null;
  maxProjects?: number | null;
  maxTesters?: number | null;
  meetingUrl?: string | null;
  mode?: TestingLabTestingEventMode;
  registeredTesterCount?: number;
  roomName?: string | null;
  startsAt?: string;
}

export type TestingLabTestingEventStatus = 'Draft' | 'ApplicationsOpen' | 'ApplicationsClosed' | 'Scheduled' | 'Active' | 'Completed' | 'Cancelled';

export interface TestingLabTestingFeedback {
  additionalNotes?: string | null;
  application?: TestingLabTestingProjectApplication;
  applicationId?: string | null;
  averageQualityRating?: number | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  event?: TestingLabTestingEvent;
  eventId?: string | null;
  feedbackData: string;
  feedbackForm?: TestingLabTestingFeedbackForm;
  feedbackFormId?: string | null;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNegative?: boolean;
  isNew?: boolean;
  isPositive?: boolean;
  isReported?: boolean;
  overallRating?: number | null;
  qualityRating?: TestingLabFeedbackQuality;
  qualityRatings?: Array<TestingLabFeedbackQualityRating> | null;
  reportReason?: string | null;
  reportedAt?: string | null;
  reportedBy?: IdentityUsersUser;
  reportedById?: string | null;
  reportedByUserId?: string | null;
  session?: TestingLabTestingSession;
  sessionId?: string | null;
  tenantId?: string | null;
  testingContext: TestingLabTestingContext;
  testingRequest?: TestingLabTestingInput;
  testingRequestId?: string | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
  wouldRecommend?: boolean | null;
}

export interface TestingLabTestingFeedbackForm {
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  formData: string;
  formSchema?: string | null;
  formType?: TestingLabFeedbackFormType;
  formVersion?: number;
  id?: string;
  isActive?: boolean;
  isDeleted?: boolean;
  isForOnline?: boolean;
  isForSessions?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  name: string;
  submissionCount?: number;
  tagArray?: Array<string> | null;
  tags?: string | null;
  tenantId?: string | null;
  testingRequestId?: string | null;
  updatedAt: string;
  version?: number;
}

export interface TestingLabTestingFeedbackObligationProjection {
  applicationId?: string;
  eventId?: string;
  feedbackId?: string | null;
  fulfilledAt?: string | null;
  id?: string;
  slotId?: string;
  status?: TestingLabTestingFeedbackObligationStatus;
  testerUserId?: string;
}

export type TestingLabTestingFeedbackObligationStatus = 'Pending' | 'Fulfilled' | 'Waived';

export interface TestingLabTestingLabAnalyticsReportProjection {
  current?: TestingLabTestingLabAnalyticsSummaryProjection;
  events?: Array<TestingLabTestingLabEventAnalyticsProjection> | null;
  fromDate?: string;
  generatedAt?: string;
  locations?: TestingLabTestingLabLocationAnalyticsProjection;
  previous?: TestingLabTestingLabAnalyticsSummaryProjection;
  toDate?: string;
  trend?: Array<TestingLabTestingLabAnalyticsTrendProjection> | null;
}

export interface TestingLabTestingLabAnalyticsSummaryProjection {
  applications?: number;
  approvedProjects?: number;
  attendedTesters?: number;
  averageRating?: number | null;
  capacity?: number;
  completedEvents?: number;
  events?: number;
  feedback?: number;
  fillRate?: number;
  recommendationRate?: number | null;
  registeredTesters?: number;
}

export interface TestingLabTestingLabAnalyticsTrendProjection {
  applications?: number;
  attendance?: number;
  date?: string;
  events?: number;
  feedback?: number;
  registrations?: number;
}

export interface TestingLabTestingLabEventAnalyticsProjection {
  applications?: number;
  approvedProjects?: number;
  attendedTesters?: number;
  averageRating?: number | null;
  capacity?: number;
  eventId?: string;
  feedback?: number;
  fillRate?: number;
  mode?: TestingLabTestingEventMode;
  name?: string | null;
  registeredTesters?: number;
  startsAt?: string;
  status?: TestingLabTestingEventStatus;
}

export interface TestingLabTestingLabLocationAnalyticsProjection {
  active?: number;
  total?: number;
}

export interface TestingLabTestingLabPermissions {
  canApproveRequests?: boolean;
  canCreateFeedback?: boolean;
  canCreateLocations?: boolean;
  canCreateRequests?: boolean;
  canCreateSessions?: boolean;
  canDeleteFeedback?: boolean;
  canDeleteLocations?: boolean;
  canDeleteRequests?: boolean;
  canDeleteSessions?: boolean;
  canEditFeedback?: boolean;
  canEditLocations?: boolean;
  canEditRequests?: boolean;
  canEditSessions?: boolean;
  canManageParticipants?: boolean;
  canModerateFeedback?: boolean;
  canViewFeedback?: boolean;
  canViewLocations?: boolean;
  canViewParticipants?: boolean;
  canViewRequests?: boolean;
  canViewSessions?: boolean;
}

export interface TestingLabTestingLabResourcePermission {
  action?: string | null;
  expiresAt?: string | null;
  resourceId?: string;
  resourceType?: string | null;
}

export interface TestingLabTestingLabRoleTemplate {
  description?: string | null;
  id?: string;
  isSystemRole?: boolean;
  name?: string | null;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabTestingLabSettings {
  allowPublicSignups?: boolean;
  createdAt?: string;
  defaultSessionDuration?: number;
  description?: string | null;
  enableNotifications?: boolean;
  id?: string;
  labName?: string | null;
  maxSimultaneousSessions?: number;
  requireApproval?: boolean;
  tenantId?: string | null;
  timezone?: string | null;
  updatedAt?: string;
}

/** A comma-separated combination of the declared flag names. */
export type TestingLabTestingLearningCompletionRequirement = string;

export interface TestingLabTestingLocation {
  activeSessionCount?: number;
  address?: string | null;
  capacity?: number | null;
  city?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  country?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  equipment?: string | null;
  equipmentAvailable?: string | null;
  fullAddress?: string | null;
  id?: string;
  isAvailable?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  isVirtual?: boolean;
  maxProjectsCapacity?: number;
  maxTestersCapacity?: number;
  name: string;
  postalCode?: string | null;
  sessions?: Array<TestingLabTestingSession> | null;
  state?: string | null;
  status?: TestingLabLocationStatus;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
  virtualUrl?: string | null;
}

export type TestingLabTestingMode = 'Online' | 'InPerson' | 'Hybrid';

export interface TestingLabTestingParticipant {
  canProvideFeedback?: boolean;
  completedAt?: string | null;
  createdAt: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  feedbackCount?: number;
  id?: string;
  instructionsAcknowledged: boolean;
  instructionsAcknowledgedAt?: string | null;
  isActive?: boolean;
  isCompleted?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  notes?: string | null;
  participationDuration?: string | null;
  startedAt: string;
  status?: TestingLabParticipationStatus;
  tenantId?: string | null;
  testingRequest?: TestingLabTestingInput;
  testingRequestId: string;
  timeSpentMinutes?: number | null;
  updatedAt: string;
  user?: IdentityUsersUser;
  userId: string;
  version?: number;
}

export interface TestingLabTestingParticipantDirectoryItemProjection {
  avatarUrl?: string | null;
  campusName?: string | null;
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  completedAt?: string | null;
  endsAt?: string;
  eventId?: string;
  eventName?: string | null;
  mode?: TestingLabTestingEventMode;
  notes?: string | null;
  pendingFeedbackCount?: number;
  registeredAt?: string;
  registrationId?: string;
  roomName?: string | null;
  slotId?: string;
  startsAt?: string;
  status?: TestingLabTestingSlotRegistrationStatus;
  userEmail?: string | null;
  userId?: string;
  userName?: string | null;
  waitlistPosition?: number | null;
}

export interface TestingLabTestingParticipantDirectoryProjection {
  attendedCount?: number;
  checkedInCount?: number;
  completedCount?: number;
  items?: Array<TestingLabTestingParticipantDirectoryItemProjection> | null;
  noShowCount?: number;
  registeredCount?: number;
  totalCount?: number;
  waitlistedCount?: number;
}

export type TestingLabTestingPriority = 'Low' | 'Medium' | 'High' | 'Critical';

export interface TestingLabTestingProjectApplication {
  assignedSlot?: TestingLabTestingEventSlot;
  assignedSlotId?: string | null;
  createdAt: string;
  decidedAt?: string | null;
  decidedBy?: IdentityUsersUser;
  decidedByUserId?: string | null;
  decisionRationale?: string | null;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  event?: TestingLabTestingEvent;
  eventId?: string;
  id?: string;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  preferredAvailability?: string | null;
  project?: ProjectsProject;
  projectId?: string;
  projectVersion?: ProjectsProjectVersion;
  projectVersionId?: string | null;
  status?: TestingLabTestingApplicationStatus;
  submittedBy?: IdentityUsersUser;
  submittedByUserId?: string;
  tenantId?: string | null;
  updatedAt: string;
  version?: number;
  votes?: Array<TestingLabTestingApplicationVote> | null;
}

export interface TestingLabTestingProjectApplicationProjection {
  assignedSlotId?: string | null;
  decidedAt?: string | null;
  decidedByUserId?: string | null;
  decisionRationale?: string | null;
  eventId?: string;
  id?: string;
  preferredAvailability?: string | null;
  projectId?: string;
  projectVersionId?: string | null;
  status?: TestingLabTestingApplicationStatus;
  submittedByUserId?: string;
  votes?: Array<TestingLabTestingApplicationVoteProjection> | null;
}

export interface TestingLabTestingInput {
  acceptsNewTesters?: boolean;
  availableSpots?: number | null;
  createdAt: string;
  createdBy?: IdentityUsersUser;
  createdById: string;
  currentTesterCount?: number;
  daysRemaining?: number | null;
  deletedAt?: string | null;
  description?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  downloadUrl?: string | null;
  duration?: string;
  endDate: string;
  estimatedDurationHours?: number | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  feedbackFormContent?: string | null;
  feedbackForms?: Array<TestingLabTestingFeedbackForm> | null;
  id?: string;
  instructionsContent?: string | null;
  instructionsFileId?: string | null;
  instructionsType: TestingLabInstructionType;
  instructionsUrl?: string | null;
  isActive?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  maxTesters?: number | null;
  mode?: TestingLabTestingMode;
  participants?: Array<TestingLabTestingParticipant> | null;
  priority?: TestingLabTestingPriority;
  projectVersion?: ProjectsProjectVersion;
  projectVersionId?: string | null;
  sessions?: Array<TestingLabTestingSession> | null;
  startDate: string;
  status: TestingLabTestingRequestStatus;
  tenantId?: string | null;
  title: string;
  updatedAt: string;
  version?: number;
}

export type TestingLabTestingRequestStatus = 'Draft' | 'Open' | 'Active' | 'InProgress' | 'Paused' | 'Completed' | 'Cancelled';

export interface TestingLabTestingSession {
  allowsRegistration?: boolean;
  availableSpots?: number;
  createdAt: string;
  createdBy?: IdentityUsersUser;
  createdById: string;
  deletedAt?: string | null;
  domainEvents?: Array<CQRSIDomainEvent> | null;
  duration?: string;
  endTime: string;
  eventSlot?: TestingLabTestingEventSlot;
  eventSlotId?: string | null;
  feedback?: Array<TestingLabTestingFeedback> | null;
  id?: string;
  isActive?: boolean;
  isCompleted?: boolean;
  isDeleted?: boolean;
  isGlobal?: boolean;
  isNew?: boolean;
  location?: TestingLabTestingLocation;
  locationId: string;
  manager?: IdentityUsersUser;
  managerId: string;
  managerUserId?: string;
  maxProjects: number;
  maxTesters: number;
  registeredProjectCount?: number;
  registeredProjectMemberCount?: number;
  registeredTesterCount?: number;
  registrations?: Array<TestingLabSessionRegistration> | null;
  sessionDate: string;
  sessionName: string;
  startTime: string;
  status: TestingLabSessionStatus;
  tenantId?: string | null;
  testingRequest?: TestingLabTestingInput;
  testingRequestId: string;
  updatedAt: string;
  version?: number;
}

export interface TestingLabTestingSlotRegistrationProjection {
  checkedInAt?: string | null;
  checkedOutAt?: string | null;
  completedAt?: string | null;
  eventId?: string;
  id?: string;
  notes?: string | null;
  pendingFeedbackCount?: number;
  promotedAt?: string | null;
  registeredAt?: string;
  slotId?: string;
  status?: TestingLabTestingSlotRegistrationStatus;
  userId?: string;
  waitlistPosition?: number | null;
}

export type TestingLabTestingSlotRegistrationStatus = 'Registered' | 'Waitlisted' | 'CheckedIn' | 'Attended' | 'Completed' | 'Cancelled' | 'NoShow';

export interface TestingLabUpdateAttendance {
  attendanceStatus?: TestingLabAttendanceStatus;
  userId?: string;
}

export interface TestingLabUpdateTestingEventInput {
  applicationsCloseAt?: string;
  applicationsOpenAt?: string;
  approvalMode?: TestingLabTestingEventApprovalMode;
  description?: string | null;
  endsAt?: string;
  mode?: TestingLabTestingEventMode;
  name?: string | null;
  requiresFeedback?: boolean;
  startsAt?: string;
}

export interface TestingLabUpdateTestingLabRoleInput {
  description?: string | null;
  name?: string | null;
  permissions?: TestingLabTestingLabPermissions;
}

export interface TestingLabUpdateTestingLabSettings {
  allowPublicSignups?: boolean | null;
  defaultSessionDuration?: number | null;
  description?: string | null;
  enableNotifications?: boolean | null;
  labName?: string | null;
  maxSimultaneousSessions?: number | null;
  requireApproval?: boolean | null;
  timezone?: string | null;
}

export interface TestingLabUpdateTestingLocation {
  address?: string | null;
  city?: string | null;
  contactEmail?: string | null;
  contactPhone?: string | null;
  country?: string | null;
  description?: string | null;
  equipmentAvailable?: string | null;
  isVirtual?: boolean | null;
  maxProjectsCapacity?: number | null;
  maxTestersCapacity?: number | null;
  name?: string | null;
  postalCode?: string | null;
  state?: string | null;
  status?: TestingLabLocationStatus;
  virtualUrl?: string | null;
}

export interface TestingLabUpsertTestingEventSlotInput {
  campusName?: string | null;
  endsAt?: string;
  locationId?: string | null;
  maxProjects?: number | null;
  maxTesters?: number | null;
  meetingUrl?: string | null;
  mode?: TestingLabTestingEventMode;
  roomName?: string | null;
  startsAt?: string;
}

export interface TestingLabUserTestingLabPermissions {
  assignedRoles?: Array<string> | null;
  permissions?: TestingLabTestingLabPermissions;
  resourcePermissions?: Array<TestingLabTestingLabResourcePermission> | null;
  tenantId?: string | null;
  userId?: string;
}

// Zod Schema Declarations (to handle circular references)
export let AIAiChatMessageSchema: z.ZodType<AIAiChatMessage>;
export let AIAiChatInputSchema: z.ZodType<AIAiChatInput>;
export let AIAiCompletionOutputSchema: z.ZodType<AIAiCompletionOutput>;
export let AIAiConversationHistoryEntrySchema: z.ZodType<AIAiConversationHistoryEntry>;
export let AIAiGenerateInputSchema: z.ZodType<AIAiGenerateInput>;
export let AIAiGeneratedContentDraftInputSchema: z.ZodType<AIAiGeneratedContentDraftInput>;
export let AIAiGeneratedContentKindSchema: z.ZodType<AIAiGeneratedContentKind>;
export let AIAiGeneratedContentInputSchema: z.ZodType<AIAiGeneratedContentInput>;
export let AIAiPromptTemplateSchema: z.ZodType<AIAiPromptTemplate>;
export let AIAiPromptTemplateGenerateInputSchema: z.ZodType<AIAiPromptTemplateGenerateInput>;
export let AIAiPromptTemplateRenderInputSchema: z.ZodType<AIAiPromptTemplateRenderInput>;
export let AIAiPromptTemplateRenderOutputSchema: z.ZodType<AIAiPromptTemplateRenderOutput>;
export let AIAiProviderStatusSchema: z.ZodType<AIAiProviderStatus>;
export let AIAiQuotaStatusSchema: z.ZodType<AIAiQuotaStatus>;
export let AIAiQuotaStatusOutputSchema: z.ZodType<AIAiQuotaStatusOutput>;
export let AIAiStatusOutputSchema: z.ZodType<AIAiStatusOutput>;
export let AIAiUsageSchema: z.ZodType<AIAiUsage>;
export let AICreateAiPromptTemplateInputSchema: z.ZodType<AICreateAiPromptTemplateInput>;
export let AIUpdateAiPromptTemplateInputSchema: z.ZodType<AIUpdateAiPromptTemplateInput>;
export let APIControllersApplicationDetailsSchema: z.ZodType<APIControllersApplicationDetails>;
export let APIControllersApplicationInfoOutputSchema: z.ZodType<APIControllersApplicationInfoOutput>;
export let APIControllersBuildDetailsSchema: z.ZodType<APIControllersBuildDetails>;
export let APIControllersDependencyHealthItemSchema: z.ZodType<APIControllersDependencyHealthItem>;
export let APIControllersDependencyHealthOutputSchema: z.ZodType<APIControllersDependencyHealthOutput>;
export let APIControllersHealthinessOutputSchema: z.ZodType<APIControllersHealthinessOutput>;
export let APIControllersHealthinessResponseItemSchema: z.ZodType<APIControllersHealthinessResponseItem>;
export let APIControllersLivenessOutputSchema: z.ZodType<APIControllersLivenessOutput>;
export let APIControllersProcessDetailsSchema: z.ZodType<APIControllersProcessDetails>;
export let APIControllersReadinessOutputSchema: z.ZodType<APIControllersReadinessOutput>;
export let APIControllersRuntimeDetailsSchema: z.ZodType<APIControllersRuntimeDetails>;
export let BillingCycleSchema: z.ZodType<BillingCycle>;
export let BulkOperationErrorSchema: z.ZodType<BulkOperationError>;
export let BulkOperationOutputSchema: z.ZodType<BulkOperationOutput>;
export let CQRSIDomainEventSchema: z.ZodType<CQRSIDomainEvent>;
export let CommerceBillingInvoicePaymentRetryResultSchema: z.ZodType<CommerceBillingInvoicePaymentRetryResult>;
export let CommerceBillingInvoiceStatusSchema: z.ZodType<CommerceBillingInvoiceStatus>;
export let CommerceOrderChargeStateSchema: z.ZodType<CommerceOrderChargeState>;
export let CommerceOrdersAddOrderItemInputSchema: z.ZodType<CommerceOrdersAddOrderItemInput>;
export let CommerceOrdersCaptureOrderInputSchema: z.ZodType<CommerceOrdersCaptureOrderInput>;
export let CommerceOrdersCompleteOrderInputSchema: z.ZodType<CommerceOrdersCompleteOrderInput>;
export let CommerceOrdersCreateOrderInputSchema: z.ZodType<CommerceOrdersCreateOrderInput>;
export let CommerceOrdersOrderCaptureSchema: z.ZodType<CommerceOrdersOrderCapture>;
export let CommerceOrdersOrderSchema: z.ZodType<CommerceOrdersOrder>;
export let CommerceOrdersOrderLineItemSchema: z.ZodType<CommerceOrdersOrderLineItem>;
export let CommerceOrdersOrderStatusSchema: z.ZodType<CommerceOrdersOrderStatus>;
export let CommercePaymentsBillingChargesControllerCancelBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerCancelBillingChargeInput>;
export let CommercePaymentsBillingChargesControllerCreateBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerCreateBillingChargeInput>;
export let CommercePaymentsBillingChargesControllerRefundBillingChargeInputSchema: z.ZodType<CommercePaymentsBillingChargesControllerRefundBillingChargeInput>;
export let CommercePaymentsCalculateTaxInputSchema: z.ZodType<CommercePaymentsCalculateTaxInput>;
export let CommercePaymentsCreateTaxJurisdictionInputSchema: z.ZodType<CommercePaymentsCreateTaxJurisdictionInput>;
export let CommercePaymentsCreateTaxRuleInputSchema: z.ZodType<CommercePaymentsCreateTaxRuleInput>;
export let CommercePaymentsCreateWalletInputSchema: z.ZodType<CommercePaymentsCreateWalletInput>;
export let CommercePaymentsCustomerTypeSchema: z.ZodType<CommercePaymentsCustomerType>;
export let CommercePaymentsLockWalletInputSchema: z.ZodType<CommercePaymentsLockWalletInput>;
export let CommercePaymentsModelsFreezeWalletInputSchema: z.ZodType<CommercePaymentsModelsFreezeWalletInput>;
export let CommercePaymentsModelsPatchWalletInputSchema: z.ZodType<CommercePaymentsModelsPatchWalletInput>;
export let CommercePaymentsPatchTaxJurisdictionInputSchema: z.ZodType<CommercePaymentsPatchTaxJurisdictionInput>;
export let CommercePaymentsPatchTaxRuleInputSchema: z.ZodType<CommercePaymentsPatchTaxRuleInput>;
export let CommercePaymentsPaymentCancellationResultSchema: z.ZodType<CommercePaymentsPaymentCancellationResult>;
export let CommercePaymentsPaymentResultSchema: z.ZodType<CommercePaymentsPaymentResult>;
export let CommercePaymentsPaymentRetryResultSchema: z.ZodType<CommercePaymentsPaymentRetryResult>;
export let CommercePaymentsPaymentStatusSchema: z.ZodType<CommercePaymentsPaymentStatus>;
export let CommercePaymentsPaymentsControllerCancelPaymentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCancelPaymentInput>;
export let CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput>;
export let CommercePaymentsPaymentsControllerCreateSetupIntentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerCreateSetupIntentInput>;
export let CommercePaymentsPaymentsControllerCreateSetupIntentOutputSchema: z.ZodType<CommercePaymentsPaymentsControllerCreateSetupIntentOutput>;
export let CommercePaymentsPaymentsControllerProcessPaymentInputSchema: z.ZodType<CommercePaymentsPaymentsControllerProcessPaymentInput>;
export let CommercePaymentsPaymentsControllerRefundInputSchema: z.ZodType<CommercePaymentsPaymentsControllerRefundInput>;
export let CommercePaymentsProcessRefundResultSchema: z.ZodType<CommercePaymentsProcessRefundResult>;
export let CommercePaymentsTaxBreakdownSchema: z.ZodType<CommercePaymentsTaxBreakdown>;
export let CommercePaymentsTaxCalculationResultSchema: z.ZodType<CommercePaymentsTaxCalculationResult>;
export let CommercePaymentsTaxExemptionValidationResultSchema: z.ZodType<CommercePaymentsTaxExemptionValidationResult>;
export let CommercePaymentsTaxJurisdictionSchema: z.ZodType<CommercePaymentsTaxJurisdiction>;
export let CommercePaymentsTaxJurisdictionDtoSchema: z.ZodType<CommercePaymentsTaxJurisdictionDto>;
export let CommercePaymentsTaxJurisdictionTypeSchema: z.ZodType<CommercePaymentsTaxJurisdictionType>;
export let CommercePaymentsTaxRateSchema: z.ZodType<CommercePaymentsTaxRate>;
export let CommercePaymentsTaxRuleSchema: z.ZodType<CommercePaymentsTaxRule>;
export let CommercePaymentsTaxRuleDtoSchema: z.ZodType<CommercePaymentsTaxRuleDto>;
export let CommercePaymentsTaxRuleTypeSchema: z.ZodType<CommercePaymentsTaxRuleType>;
export let CommercePaymentsTaxTypeSchema: z.ZodType<CommercePaymentsTaxType>;
export let CommercePaymentsTransactionStatusSchema: z.ZodType<CommercePaymentsTransactionStatus>;
export let CommercePaymentsUserWalletSchema: z.ZodType<CommercePaymentsUserWallet>;
export let CommercePaymentsValidateTaxExemptionInputSchema: z.ZodType<CommercePaymentsValidateTaxExemptionInput>;
export let CommercePaymentsWalletTransactionSchema: z.ZodType<CommercePaymentsWalletTransaction>;
export let CommercePaymentsWalletTransactionTypeSchema: z.ZodType<CommercePaymentsWalletTransactionType>;
export let CommerceProductsAddSupportTicketMessageInputSchema: z.ZodType<CommerceProductsAddSupportTicketMessageInput>;
export let CommerceProductsAppliedPromoCodeSchema: z.ZodType<CommerceProductsAppliedPromoCode>;
export let CommerceProductsApplyPromoCodesInputSchema: z.ZodType<CommerceProductsApplyPromoCodesInput>;
export let CommerceProductsAssignSupportTicketInputSchema: z.ZodType<CommerceProductsAssignSupportTicketInput>;
export let CommerceProductsBatchCreateProductsInputSchema: z.ZodType<CommerceProductsBatchCreateProductsInput>;
export let CommerceProductsBatchProductCreateItemSchema: z.ZodType<CommerceProductsBatchProductCreateItem>;
export let CommerceProductsCheckMultipleAccessInputSchema: z.ZodType<CommerceProductsCheckMultipleAccessInput>;
export let CommerceProductsCloseSupportTicketInputSchema: z.ZodType<CommerceProductsCloseSupportTicketInput>;
export let CommerceProductsCreateProductInputSchema: z.ZodType<CommerceProductsCreateProductInput>;
export let CommerceProductsCreatePromoCodeInputSchema: z.ZodType<CommerceProductsCreatePromoCodeInput>;
export let CommerceProductsCreateSupportTicketInputSchema: z.ZodType<CommerceProductsCreateSupportTicketInput>;
export let CommerceProductsEntitlementCheckResultSchema: z.ZodType<CommerceProductsEntitlementCheckResult>;
export let CommerceProductsEntitlementInfoSchema: z.ZodType<CommerceProductsEntitlementInfo>;
export let CommerceProductsGrantEntitlementInputSchema: z.ZodType<CommerceProductsGrantEntitlementInput>;
export let CommerceProductsPatchProductInputSchema: z.ZodType<CommerceProductsPatchProductInput>;
export let CommerceProductsPatchPromoCodeInputSchema: z.ZodType<CommerceProductsPatchPromoCodeInput>;
export let CommerceProductsProductAcquisitionTypeSchema: z.ZodType<CommerceProductsProductAcquisitionType>;
export let CommerceProductsProductSchema: z.ZodType<CommerceProductsProduct>;
export let CommerceProductsProductPricingSchema: z.ZodType<CommerceProductsProductPricing>;
export let CommerceProductsProductTypeSchema: z.ZodType<CommerceProductsProductType>;
export let CommerceProductsPromoCodeApplicationResultSchema: z.ZodType<CommerceProductsPromoCodeApplicationResult>;
export let CommerceProductsPromoCodeSchema: z.ZodType<CommerceProductsPromoCode>;
export let CommerceProductsPromoCodeTypeSchema: z.ZodType<CommerceProductsPromoCodeType>;
export let CommerceProductsPromoCodeUsageSchema: z.ZodType<CommerceProductsPromoCodeUsage>;
export let CommerceProductsPromoCodeValidationResultSchema: z.ZodType<CommerceProductsPromoCodeValidationResult>;
export let CommerceProductsRejectedPromoCodeSchema: z.ZodType<CommerceProductsRejectedPromoCode>;
export let CommerceProductsResolveSupportTicketInputSchema: z.ZodType<CommerceProductsResolveSupportTicketInput>;
export let CommerceProductsRevokeEntitlementInputSchema: z.ZodType<CommerceProductsRevokeEntitlementInput>;
export let CommerceProductsSupportTicketSchema: z.ZodType<CommerceProductsSupportTicket>;
export let CommerceProductsSupportTicketMessageAuthorTypeSchema: z.ZodType<CommerceProductsSupportTicketMessageAuthorType>;
export let CommerceProductsSupportTicketMessageSchema: z.ZodType<CommerceProductsSupportTicketMessage>;
export let CommerceProductsSupportTicketPrioritySchema: z.ZodType<CommerceProductsSupportTicketPriority>;
export let CommerceProductsSupportTicketStatusSchema: z.ZodType<CommerceProductsSupportTicketStatus>;
export let CommerceProductsUpdateProductInputSchema: z.ZodType<CommerceProductsUpdateProductInput>;
export let CommerceProductsUpdatePromoCodeInputSchema: z.ZodType<CommerceProductsUpdatePromoCodeInput>;
export let CommerceProductsValidatePromoCodeInputSchema: z.ZodType<CommerceProductsValidatePromoCodeInput>;
export let CommerceSubscriptionsBillingHistorySchema: z.ZodType<CommerceSubscriptionsBillingHistory>;
export let CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput>;
export let CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput>;
export let CommerceSubscriptionsCancellationReasonSchema: z.ZodType<CommerceSubscriptionsCancellationReason>;
export let CommerceSubscriptionsClientModulesOutputSchema: z.ZodType<CommerceSubscriptionsClientModulesOutput>;
export let CommerceSubscriptionsCreateClientInputSchema: z.ZodType<CommerceSubscriptionsCreateClientInput>;
export let CommerceSubscriptionsSubscriptionSchema: z.ZodType<CommerceSubscriptionsSubscription>;
export let CommerceSubscriptionsSubscriptionChurnReportSchema: z.ZodType<CommerceSubscriptionsSubscriptionChurnReport>;
export let CommerceSubscriptionsSubscriptionDowngradeResultSchema: z.ZodType<CommerceSubscriptionsSubscriptionDowngradeResult>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerCancelInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput>;
export let CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput>;
export let CommerceSubscriptionsSubscriptionNotificationSchema: z.ZodType<CommerceSubscriptionsSubscriptionNotification>;
export let CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput>;
export let CommerceSubscriptionsSubscriptionPlanSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlan>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput>;
export let CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput>;
export let CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput>;
export let CommerceSubscriptionsSubscriptionStatusSchema: z.ZodType<CommerceSubscriptionsSubscriptionStatus>;
export let CommerceSubscriptionsSubscriptionUpgradeResultSchema: z.ZodType<CommerceSubscriptionsSubscriptionUpgradeResult>;
export let CommerceSubscriptionsSubscriptionUsageSchema: z.ZodType<CommerceSubscriptionsSubscriptionUsage>;
export let CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput>;
export let CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInputSchema: z.ZodType<CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput>;
export let ComplianceFERPACompleteFerpaInspectionRequestBodySchema: z.ZodType<ComplianceFERPACompleteFerpaInspectionRequestBody>;
export let ComplianceFERPAEducationRecordKindSchema: z.ZodType<ComplianceFERPAEducationRecordKind>;
export let ComplianceFERPAFerpaDirectoryInformationPolicySchema: z.ZodType<ComplianceFERPAFerpaDirectoryInformationPolicy>;
export let ComplianceFERPAFerpaDisclosureBasisSchema: z.ZodType<ComplianceFERPAFerpaDisclosureBasis>;
export let ComplianceFERPAFerpaDisclosureConsentSchema: z.ZodType<ComplianceFERPAFerpaDisclosureConsent>;
export let ComplianceFERPAFerpaDisclosureLogSchema: z.ZodType<ComplianceFERPAFerpaDisclosureLog>;
export let ComplianceFERPAFerpaEducationRecordSchema: z.ZodType<ComplianceFERPAFerpaEducationRecord>;
export let ComplianceFERPAFerpaInspectionInputSchema: z.ZodType<ComplianceFERPAFerpaInspectionInput>;
export let ComplianceFERPAFerpaRecordProtectionLevelSchema: z.ZodType<ComplianceFERPAFerpaRecordProtectionLevel>;
export let ComplianceFERPAFerpaRequestStatusSchema: z.ZodType<ComplianceFERPAFerpaRequestStatus>;
export let ComplianceFERPAGrantFerpaDisclosureConsentCommandSchema: z.ZodType<ComplianceFERPAGrantFerpaDisclosureConsentCommand>;
export let ComplianceFERPARecordFerpaDisclosureCommandSchema: z.ZodType<ComplianceFERPARecordFerpaDisclosureCommand>;
export let ComplianceFERPARegisterEducationRecordCommandSchema: z.ZodType<ComplianceFERPARegisterEducationRecordCommand>;
export let ComplianceFERPASubmitFerpaInspectionRequestCommandSchema: z.ZodType<ComplianceFERPASubmitFerpaInspectionRequestCommand>;
export let ComplianceFERPAUpsertDirectoryInformationPolicyCommandSchema: z.ZodType<ComplianceFERPAUpsertDirectoryInformationPolicyCommand>;
export let ContentStatusSchema: z.ZodType<ContentStatus>;
export let ContentVisibilitySchema: z.ZodType<ContentVisibility>;
export let ContentPagesContentResourceSchema: z.ZodType<ContentPagesContentResource>;
export let ContentPagesContentResourceStatusSchema: z.ZodType<ContentPagesContentResourceStatus>;
export let ContentPagesContentResourceTypeSchema: z.ZodType<ContentPagesContentResourceType>;
export let ContentPagesCreateContentResourceSchema: z.ZodType<ContentPagesCreateContentResource>;
export let ContentPagesCreateMarketingLeadSchema: z.ZodType<ContentPagesCreateMarketingLead>;
export let ContentPagesCreatePageSchema: z.ZodType<ContentPagesCreatePage>;
export let ContentPagesCreatePageSectionSchema: z.ZodType<ContentPagesCreatePageSection>;
export let ContentPagesMarketingLeadSchema: z.ZodType<ContentPagesMarketingLead>;
export let ContentPagesOpenGraphMetadataSchema: z.ZodType<ContentPagesOpenGraphMetadata>;
export let ContentPagesPageSchema: z.ZodType<ContentPagesPage>;
export let ContentPagesPageSectionSchema: z.ZodType<ContentPagesPageSection>;
export let ContentPagesPageStatusSchema: z.ZodType<ContentPagesPageStatus>;
export let ContentPagesPageTypeSchema: z.ZodType<ContentPagesPageType>;
export let ContentPagesSectionTypeSchema: z.ZodType<ContentPagesSectionType>;
export let ContentPagesSitemapEntrySchema: z.ZodType<ContentPagesSitemapEntry>;
export let ContentPagesUpdateContentResourceSchema: z.ZodType<ContentPagesUpdateContentResource>;
export let ContentPagesUpdatePageSchema: z.ZodType<ContentPagesUpdatePage>;
export let ContentPagesUpdatePageSectionSchema: z.ZodType<ContentPagesUpdatePageSection>;
export let FeaturesBulkEvaluationInputSchema: z.ZodType<FeaturesBulkEvaluationInput>;
export let FeaturesCapabilityAuditLogSchema: z.ZodType<FeaturesCapabilityAuditLog>;
export let FeaturesCapabilityCheckOutputSchema: z.ZodType<FeaturesCapabilityCheckOutput>;
export let FeaturesCreateFeatureInputSchema: z.ZodType<FeaturesCreateFeatureInput>;
export let FeaturesFeatureContextSchema: z.ZodType<FeaturesFeatureContext>;
export let FeaturesFeatureEvaluationInputSchema: z.ZodType<FeaturesFeatureEvaluationInput>;
export let FeaturesFeatureFlagSchema: z.ZodType<FeaturesFeatureFlag>;
export let FeaturesFeatureFlagTargetSchema: z.ZodType<FeaturesFeatureFlagTarget>;
export let FeaturesFeatureFlagTypeSchema: z.ZodType<FeaturesFeatureFlagType>;
export let FeaturesSetCapabilityOverrideInputSchema: z.ZodType<FeaturesSetCapabilityOverrideInput>;
export let FeaturesToggleFeatureInputSchema: z.ZodType<FeaturesToggleFeatureInput>;
export let FeaturesUpdateFeatureInputSchema: z.ZodType<FeaturesUpdateFeatureInput>;
export let Fido2NetLibAssertionOptionsSchema: z.ZodType<Fido2NetLibAssertionOptions>;
export let Fido2NetLibAuthenticatorSelectionSchema: z.ZodType<Fido2NetLibAuthenticatorSelection>;
export let Fido2NetLibCredentialCreateOptionsSchema: z.ZodType<Fido2NetLibCredentialCreateOptions>;
export let Fido2NetLibFido2UserSchema: z.ZodType<Fido2NetLibFido2User>;
export let Fido2NetLibPubKeyCredParamSchema: z.ZodType<Fido2NetLibPubKeyCredParam>;
export let Fido2NetLibPublicKeyCredentialRpEntitySchema: z.ZodType<Fido2NetLibPublicKeyCredentialRpEntity>;
export let GameJamsAddJamCriteriaInputSchema: z.ZodType<GameJamsAddJamCriteriaInput>;
export let GameJamsCreateJamInputSchema: z.ZodType<GameJamsCreateJamInput>;
export let GameJamsJamSchema: z.ZodType<GameJamsJam>;
export let GameJamsJamCriteriaSchema: z.ZodType<GameJamsJamCriteria>;
export let GameJamsJamDtoSchema: z.ZodType<GameJamsJamDto>;
export let GameJamsJamScoreSchema: z.ZodType<GameJamsJamScore>;
export let GameJamsJamScoreDtoSchema: z.ZodType<GameJamsJamScoreDto>;
export let GameJamsJamStatusSchema: z.ZodType<GameJamsJamStatus>;
export let GameJamsJamSubmissionSchema: z.ZodType<GameJamsJamSubmission>;
export let GameJamsScoreJamSubmissionInputSchema: z.ZodType<GameJamsScoreJamSubmissionInput>;
export let GameJamsSubmitJamEntryInputSchema: z.ZodType<GameJamsSubmitJamEntryInput>;
export let IdentityAuthenticationApiKeySchema: z.ZodType<IdentityAuthenticationApiKey>;
export let IdentityAuthenticationAssignRoleToUserInputSchema: z.ZodType<IdentityAuthenticationAssignRoleToUserInput>;
export let IdentityAuthenticationBackupCodesOutputSchema: z.ZodType<IdentityAuthenticationBackupCodesOutput>;
export let IdentityAuthenticationBackupCodesStatusOutputSchema: z.ZodType<IdentityAuthenticationBackupCodesStatusOutput>;
export let IdentityAuthenticationBeginWebAuthnAuthenticationInputSchema: z.ZodType<IdentityAuthenticationBeginWebAuthnAuthenticationInput>;
export let IdentityAuthenticationBeginWebAuthnRegistrationInputSchema: z.ZodType<IdentityAuthenticationBeginWebAuthnRegistrationInput>;
export let IdentityAuthenticationCleanupKeysInputSchema: z.ZodType<IdentityAuthenticationCleanupKeysInput>;
export let IdentityAuthenticationCleanupResultSchema: z.ZodType<IdentityAuthenticationCleanupResult>;
export let IdentityAuthenticationClientCredentialsTokenOutputSchema: z.ZodType<IdentityAuthenticationClientCredentialsTokenOutput>;
export let IdentityAuthenticationCompleteMfaSetupInputSchema: z.ZodType<IdentityAuthenticationCompleteMfaSetupInput>;
export let IdentityAuthenticationCompletePasswordResetInputSchema: z.ZodType<IdentityAuthenticationCompletePasswordResetInput>;
export let IdentityAuthenticationCompleteWebAuthnAuthenticationInputSchema: z.ZodType<IdentityAuthenticationCompleteWebAuthnAuthenticationInput>;
export let IdentityAuthenticationCompleteWebAuthnRegistrationInputSchema: z.ZodType<IdentityAuthenticationCompleteWebAuthnRegistrationInput>;
export let IdentityAuthenticationConsumeMagicLinkInputSchema: z.ZodType<IdentityAuthenticationConsumeMagicLinkInput>;
export let IdentityAuthenticationCreateApiKeyCommandSchema: z.ZodType<IdentityAuthenticationCreateApiKeyCommand>;
export let IdentityAuthenticationCreateApiKeyOutputSchema: z.ZodType<IdentityAuthenticationCreateApiKeyOutput>;
export let IdentityAuthenticationCreateRoleInputSchema: z.ZodType<IdentityAuthenticationCreateRoleInput>;
export let IdentityAuthenticationCreateServiceAccountInputSchema: z.ZodType<IdentityAuthenticationCreateServiceAccountInput>;
export let IdentityAuthenticationDeviceInfoSchema: z.ZodType<IdentityAuthenticationDeviceInfo>;
export let IdentityAuthenticationDisableMfaInputSchema: z.ZodType<IdentityAuthenticationDisableMfaInput>;
export let IdentityAuthenticationEmailVerificationOutputSchema: z.ZodType<IdentityAuthenticationEmailVerificationOutput>;
export let IdentityAuthenticationEmailVerificationResultSchema: z.ZodType<IdentityAuthenticationEmailVerificationResult>;
export let IdentityAuthenticationGitHubSignInOutputSchema: z.ZodType<IdentityAuthenticationGitHubSignInOutput>;
export let IdentityAuthenticationGoogleIdTokenInputSchema: z.ZodType<IdentityAuthenticationGoogleIdTokenInput>;
export let IdentityAuthenticationJwtKeyInfoSchema: z.ZodType<IdentityAuthenticationJwtKeyInfo>;
export let IdentityAuthenticationLocalSignInInputSchema: z.ZodType<IdentityAuthenticationLocalSignInInput>;
export let IdentityAuthenticationLocalSignUpInputSchema: z.ZodType<IdentityAuthenticationLocalSignUpInput>;
export let IdentityAuthenticationLocationInfoSchema: z.ZodType<IdentityAuthenticationLocationInfo>;
export let IdentityAuthenticationLockServiceAccountInputSchema: z.ZodType<IdentityAuthenticationLockServiceAccountInput>;
export let IdentityAuthenticationMagicLinkRequestResultSchema: z.ZodType<IdentityAuthenticationMagicLinkRequestResult>;
export let IdentityAuthenticationMfaConfigurationOutputSchema: z.ZodType<IdentityAuthenticationMfaConfigurationOutput>;
export let IdentityAuthenticationMfaErrorOutputSchema: z.ZodType<IdentityAuthenticationMfaErrorOutput>;
export let IdentityAuthenticationMfaMethodSchema: z.ZodType<IdentityAuthenticationMfaMethod>;
export let IdentityAuthenticationMfaMethodInfoSchema: z.ZodType<IdentityAuthenticationMfaMethodInfo>;
export let IdentityAuthenticationMfaMethodsOutputSchema: z.ZodType<IdentityAuthenticationMfaMethodsOutput>;
export let IdentityAuthenticationMfaSetupOutputSchema: z.ZodType<IdentityAuthenticationMfaSetupOutput>;
export let IdentityAuthenticationMfaSuccessOutputSchema: z.ZodType<IdentityAuthenticationMfaSuccessOutput>;
export let IdentityAuthenticationMfaVerificationOutputSchema: z.ZodType<IdentityAuthenticationMfaVerificationOutput>;
export let IdentityAuthenticationOAuth2ErrorOutputSchema: z.ZodType<IdentityAuthenticationOAuth2ErrorOutput>;
export let IdentityAuthenticationPasswordChangeInputSchema: z.ZodType<IdentityAuthenticationPasswordChangeInput>;
export let IdentityAuthenticationPasswordChangeResultSchema: z.ZodType<IdentityAuthenticationPasswordChangeResult>;
export let IdentityAuthenticationPasswordResetRequestResultSchema: z.ZodType<IdentityAuthenticationPasswordResetRequestResult>;
export let IdentityAuthenticationPasswordResetResultSchema: z.ZodType<IdentityAuthenticationPasswordResetResult>;
export let IdentityAuthenticationPatchServiceAccountInputSchema: z.ZodType<IdentityAuthenticationPatchServiceAccountInput>;
export let IdentityAuthenticationRefreshTokenInputSchema: z.ZodType<IdentityAuthenticationRefreshTokenInput>;
export let IdentityAuthenticationRemoveRoleFromUserInputSchema: z.ZodType<IdentityAuthenticationRemoveRoleFromUserInput>;
export let IdentityAuthenticationRequestMagicLinkInputSchema: z.ZodType<IdentityAuthenticationRequestMagicLinkInput>;
export let IdentityAuthenticationRequestPasswordResetInputSchema: z.ZodType<IdentityAuthenticationRequestPasswordResetInput>;
export let IdentityAuthenticationRevokeApiKeyInputSchema: z.ZodType<IdentityAuthenticationRevokeApiKeyInput>;
export let IdentityAuthenticationRevokeRefreshTokenInputSchema: z.ZodType<IdentityAuthenticationRevokeRefreshTokenInput>;
export let IdentityAuthenticationRiskLevelSchema: z.ZodType<IdentityAuthenticationRiskLevel>;
export let IdentityAuthenticationRotateKeyInputSchema: z.ZodType<IdentityAuthenticationRotateKeyInput>;
export let IdentityAuthenticationSecretRotationOutputSchema: z.ZodType<IdentityAuthenticationSecretRotationOutput>;
export let IdentityAuthenticationSendEmailVerificationInputSchema: z.ZodType<IdentityAuthenticationSendEmailVerificationInput>;
export let IdentityAuthenticationServiceAccountAuditEntrySchema: z.ZodType<IdentityAuthenticationServiceAccountAuditEntry>;
export let IdentityAuthenticationServiceAccountAuditLogOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountAuditLogOutput>;
export let IdentityAuthenticationServiceAccountCreatedOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountCreatedOutput>;
export let IdentityAuthenticationServiceAccountOutputSchema: z.ZodType<IdentityAuthenticationServiceAccountOutput>;
export let IdentityAuthenticationSessionOutputSchema: z.ZodType<IdentityAuthenticationSessionOutput>;
export let IdentityAuthenticationSessionSecurityAnalysisSchema: z.ZodType<IdentityAuthenticationSessionSecurityAnalysis>;
export let IdentityAuthenticationSessionSuccessOutputSchema: z.ZodType<IdentityAuthenticationSessionSuccessOutput>;
export let IdentityAuthenticationSessionTerminationOutputSchema: z.ZodType<IdentityAuthenticationSessionTerminationOutput>;
export let IdentityAuthenticationSignInOutputSchema: z.ZodType<IdentityAuthenticationSignInOutput>;
export let IdentityAuthenticationSmsMfaSetupInputSchema: z.ZodType<IdentityAuthenticationSmsMfaSetupInput>;
export let IdentityAuthenticationSmsMfaSetupOutputSchema: z.ZodType<IdentityAuthenticationSmsMfaSetupOutput>;
export let IdentityAuthenticationTrustDeviceInputSchema: z.ZodType<IdentityAuthenticationTrustDeviceInput>;
export let IdentityAuthenticationTrustedDeviceOutputSchema: z.ZodType<IdentityAuthenticationTrustedDeviceOutput>;
export let IdentityAuthenticationUpdateCredentialNameInputSchema: z.ZodType<IdentityAuthenticationUpdateCredentialNameInput>;
export let IdentityAuthenticationUpdateRoleInputSchema: z.ZodType<IdentityAuthenticationUpdateRoleInput>;
export let IdentityAuthenticationUpdateScopesInputSchema: z.ZodType<IdentityAuthenticationUpdateScopesInput>;
export let IdentityAuthenticationUserSchema: z.ZodType<IdentityAuthenticationUser>;
export let IdentityAuthenticationVerifyEmailInputSchema: z.ZodType<IdentityAuthenticationVerifyEmailInput>;
export let IdentityAuthenticationVerifyMfaInputSchema: z.ZodType<IdentityAuthenticationVerifyMfaInput>;
export let IdentityAuthenticationWeb3ChallengeInputSchema: z.ZodType<IdentityAuthenticationWeb3ChallengeInput>;
export let IdentityAuthenticationWeb3ChallengeOutputSchema: z.ZodType<IdentityAuthenticationWeb3ChallengeOutput>;
export let IdentityAuthenticationWeb3VerifyInputSchema: z.ZodType<IdentityAuthenticationWeb3VerifyInput>;
export let IdentityAuthenticationWebAuthnAuthenticationOptionsResultSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticationOptionsResult>;
export let IdentityAuthenticationWebAuthnAuthenticationResultSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticationResult>;
export let IdentityAuthenticationWebAuthnAuthenticatorTypeSchema: z.ZodType<IdentityAuthenticationWebAuthnAuthenticatorType>;
export let IdentityAuthenticationWebAuthnCredentialInfoSchema: z.ZodType<IdentityAuthenticationWebAuthnCredentialInfo>;
export let IdentityAuthenticationWebAuthnCredentialVerifyResultSchema: z.ZodType<IdentityAuthenticationWebAuthnCredentialVerifyResult>;
export let IdentityAuthenticationWebAuthnRegistrationOptionsResultSchema: z.ZodType<IdentityAuthenticationWebAuthnRegistrationOptionsResult>;
export let IdentityAuthenticationWebAuthnRegistrationResultSchema: z.ZodType<IdentityAuthenticationWebAuthnRegistrationResult>;
export let IdentityAuthenticationWebAuthnStatusOutputSchema: z.ZodType<IdentityAuthenticationWebAuthnStatusOutput>;
export let IdentityAuthorizationPermissionTypeSchema: z.ZodType<IdentityAuthorizationPermissionType>;
export let IdentityTenantsAddTenantMemberOutputSchema: z.ZodType<IdentityTenantsAddTenantMemberOutput>;
export let IdentityTenantsAddUserMembershipInputSchema: z.ZodType<IdentityTenantsAddUserMembershipInput>;
export let IdentityTenantsArchiveInputSchema: z.ZodType<IdentityTenantsArchiveInput>;
export let IdentityTenantsBulkActivateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkActivateTenantsCommand>;
export let IdentityTenantsBulkArchiveTenantsCommandSchema: z.ZodType<IdentityTenantsBulkArchiveTenantsCommand>;
export let IdentityTenantsBulkCreateTenantItemSchema: z.ZodType<IdentityTenantsBulkCreateTenantItem>;
export let IdentityTenantsBulkCreateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkCreateTenantsCommand>;
export let IdentityTenantsBulkDeactivateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkDeactivateTenantsCommand>;
export let IdentityTenantsBulkDeleteTenantsCommandSchema: z.ZodType<IdentityTenantsBulkDeleteTenantsCommand>;
export let IdentityTenantsBulkPurgeTenantsCommandSchema: z.ZodType<IdentityTenantsBulkPurgeTenantsCommand>;
export let IdentityTenantsBulkUndeleteTenantsCommandSchema: z.ZodType<IdentityTenantsBulkUndeleteTenantsCommand>;
export let IdentityTenantsBulkUpdateTenantItemSchema: z.ZodType<IdentityTenantsBulkUpdateTenantItem>;
export let IdentityTenantsBulkUpdateTenantsCommandSchema: z.ZodType<IdentityTenantsBulkUpdateTenantsCommand>;
export let IdentityTenantsCreateTenantInputSchema: z.ZodType<IdentityTenantsCreateTenantInput>;
export let IdentityTenantsGetUserMembershipsOutputSchema: z.ZodType<IdentityTenantsGetUserMembershipsOutput>;
export let IdentityTenantsMembershipCountOutputSchema: z.ZodType<IdentityTenantsMembershipCountOutput>;
export let IdentityTenantsRecoverInputSchema: z.ZodType<IdentityTenantsRecoverInput>;
export let IdentityTenantsReplaceTenantMetadataInputSchema: z.ZodType<IdentityTenantsReplaceTenantMetadataInput>;
export let IdentityTenantsReplaceTenantSettingsInputSchema: z.ZodType<IdentityTenantsReplaceTenantSettingsInput>;
export let IdentityTenantsSlugValidationSchema: z.ZodType<IdentityTenantsSlugValidation>;
export let IdentityTenantsTenantSchema: z.ZodType<IdentityTenantsTenant>;
export let IdentityTenantsTenantAddressSchema: z.ZodType<IdentityTenantsTenantAddress>;
export let IdentityTenantsTenantAuditLogEntrySchema: z.ZodType<IdentityTenantsTenantAuditLogEntry>;
export let IdentityTenantsTenantBrandingSchema: z.ZodType<IdentityTenantsTenantBranding>;
export let IdentityTenantsTenantBusinessInfoSchema: z.ZodType<IdentityTenantsTenantBusinessInfo>;
export let IdentityTenantsTenantBusinessRulesSchema: z.ZodType<IdentityTenantsTenantBusinessRules>;
export let IdentityTenantsTenantContactInfoSchema: z.ZodType<IdentityTenantsTenantContactInfo>;
export let IdentityTenantsTenantCurrencySettingsSchema: z.ZodType<IdentityTenantsTenantCurrencySettings>;
export let IdentityTenantsTenantDomainSchema: z.ZodType<IdentityTenantsTenantDomain>;
export let IdentityTenantsTenantIntegrationSettingsSchema: z.ZodType<IdentityTenantsTenantIntegrationSettings>;
export let IdentityTenantsTenantMemberSchema: z.ZodType<IdentityTenantsTenantMember>;
export let IdentityTenantsTenantMetadataSchema: z.ZodType<IdentityTenantsTenantMetadata>;
export let IdentityTenantsTenantSecuritySettingsSchema: z.ZodType<IdentityTenantsTenantSecuritySettings>;
export let IdentityTenantsTenantSettingsSchema: z.ZodType<IdentityTenantsTenantSettings>;
export let IdentityTenantsTenantSettingsDtoSchema: z.ZodType<IdentityTenantsTenantSettingsDto>;
export let IdentityTenantsTenantStatisticsSchema: z.ZodType<IdentityTenantsTenantStatistics>;
export let IdentityTenantsTenantSystemConfigurationSchema: z.ZodType<IdentityTenantsTenantSystemConfiguration>;
export let IdentityTenantsTenantSystemLimitsSchema: z.ZodType<IdentityTenantsTenantSystemLimits>;
export let IdentityTenantsTenantUiSettingsSchema: z.ZodType<IdentityTenantsTenantUiSettings>;
export let IdentityTenantsTenantValidationErrorSchema: z.ZodType<IdentityTenantsTenantValidationError>;
export let IdentityTenantsTenantValidationOutputSchema: z.ZodType<IdentityTenantsTenantValidationOutput>;
export let IdentityTenantsTenantValidationWarningSchema: z.ZodType<IdentityTenantsTenantValidationWarning>;
export let IdentityTenantsUpdateTenantAddressInputSchema: z.ZodType<IdentityTenantsUpdateTenantAddressInput>;
export let IdentityTenantsUpdateTenantBrandingInputSchema: z.ZodType<IdentityTenantsUpdateTenantBrandingInput>;
export let IdentityTenantsUpdateTenantBusinessInfoInputSchema: z.ZodType<IdentityTenantsUpdateTenantBusinessInfoInput>;
export let IdentityTenantsUpdateTenantBusinessRulesInputSchema: z.ZodType<IdentityTenantsUpdateTenantBusinessRulesInput>;
export let IdentityTenantsUpdateTenantContactInfoInputSchema: z.ZodType<IdentityTenantsUpdateTenantContactInfoInput>;
export let IdentityTenantsUpdateTenantCurrencySettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantCurrencySettingsInput>;
export let IdentityTenantsUpdateTenantFeatureFlagsInputSchema: z.ZodType<IdentityTenantsUpdateTenantFeatureFlagsInput>;
export let IdentityTenantsUpdateTenantIntegrationSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantIntegrationSettingsInput>;
export let IdentityTenantsUpdateTenantMemberInviteOutputSchema: z.ZodType<IdentityTenantsUpdateTenantMemberInviteOutput>;
export let IdentityTenantsUpdateTenantMemberRoleOutputSchema: z.ZodType<IdentityTenantsUpdateTenantMemberRoleOutput>;
export let IdentityTenantsUpdateTenantMetadataInputSchema: z.ZodType<IdentityTenantsUpdateTenantMetadataInput>;
export let IdentityTenantsUpdateTenantInputSchema: z.ZodType<IdentityTenantsUpdateTenantInput>;
export let IdentityTenantsUpdateTenantSecuritySettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSecuritySettingsInput>;
export let IdentityTenantsUpdateTenantSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSettingsInput>;
export let IdentityTenantsUpdateTenantSystemConfigurationInputSchema: z.ZodType<IdentityTenantsUpdateTenantSystemConfigurationInput>;
export let IdentityTenantsUpdateTenantSystemLimitsInputSchema: z.ZodType<IdentityTenantsUpdateTenantSystemLimitsInput>;
export let IdentityTenantsUpdateTenantTagsInputSchema: z.ZodType<IdentityTenantsUpdateTenantTagsInput>;
export let IdentityTenantsUpdateTenantUiSettingsInputSchema: z.ZodType<IdentityTenantsUpdateTenantUiSettingsInput>;
export let IdentityTenantsUpdateUserMembershipInviteInputSchema: z.ZodType<IdentityTenantsUpdateUserMembershipInviteInput>;
export let IdentityTenantsUpdateUserMembershipRoleInputSchema: z.ZodType<IdentityTenantsUpdateUserMembershipRoleInput>;
export let IdentityTenantsUsageTrackingSchema: z.ZodType<IdentityTenantsUsageTracking>;
export let IdentityTenantsUserMembershipSchema: z.ZodType<IdentityTenantsUserMembership>;
export let IdentityTenantsValidateTenantInputSchema: z.ZodType<IdentityTenantsValidateTenantInput>;
export let IdentityUsersBulkActivateUsersInputSchema: z.ZodType<IdentityUsersBulkActivateUsersInput>;
export let IdentityUsersBulkActivateUsersOutputSchema: z.ZodType<IdentityUsersBulkActivateUsersOutput>;
export let IdentityUsersBulkCreateUsersInputSchema: z.ZodType<IdentityUsersBulkCreateUsersInput>;
export let IdentityUsersBulkCreateUsersOutputSchema: z.ZodType<IdentityUsersBulkCreateUsersOutput>;
export let IdentityUsersBulkDeactivateUsersInputSchema: z.ZodType<IdentityUsersBulkDeactivateUsersInput>;
export let IdentityUsersBulkDeactivateUsersOutputSchema: z.ZodType<IdentityUsersBulkDeactivateUsersOutput>;
export let IdentityUsersBulkDeleteUsersInputSchema: z.ZodType<IdentityUsersBulkDeleteUsersInput>;
export let IdentityUsersBulkNotificationInputSchema: z.ZodType<IdentityUsersBulkNotificationInput>;
export let IdentityUsersBulkPurgeUsersInputSchema: z.ZodType<IdentityUsersBulkPurgeUsersInput>;
export let IdentityUsersBulkRestoreUsersInputSchema: z.ZodType<IdentityUsersBulkRestoreUsersInput>;
export let IdentityUsersBulkRestoreUsersOutputSchema: z.ZodType<IdentityUsersBulkRestoreUsersOutput>;
export let IdentityUsersBulkSuspendUsersInputSchema: z.ZodType<IdentityUsersBulkSuspendUsersInput>;
export let IdentityUsersBulkSuspendUsersOutputSchema: z.ZodType<IdentityUsersBulkSuspendUsersOutput>;
export let IdentityUsersBulkUnsuspendUsersInputSchema: z.ZodType<IdentityUsersBulkUnsuspendUsersInput>;
export let IdentityUsersBulkUnsuspendUsersOutputSchema: z.ZodType<IdentityUsersBulkUnsuspendUsersOutput>;
export let IdentityUsersBulkUpdateUsersInputSchema: z.ZodType<IdentityUsersBulkUpdateUsersInput>;
export let IdentityUsersCreateUserInputSchema: z.ZodType<IdentityUsersCreateUserInput>;
export let IdentityUsersCreateUserRequestItemSchema: z.ZodType<IdentityUsersCreateUserRequestItem>;
export let IdentityUsersNotificationActionSchema: z.ZodType<IdentityUsersNotificationAction>;
export let IdentityUsersNotificationFilterCriteriaSchema: z.ZodType<IdentityUsersNotificationFilterCriteria>;
export let IdentityUsersNotificationPrioritySchema: z.ZodType<IdentityUsersNotificationPriority>;
export let IdentityUsersProfileVisibilitySchema: z.ZodType<IdentityUsersProfileVisibility>;
export let IdentityUsersPurgeStrategySchema: z.ZodType<IdentityUsersPurgeStrategy>;
export let IdentityUsersReplaceUserAccessibilityPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserAccessibilityPreferencesInput>;
export let IdentityUsersReplaceUserLocalizationPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserLocalizationPreferencesInput>;
export let IdentityUsersReplaceUserMetadataInputSchema: z.ZodType<IdentityUsersReplaceUserMetadataInput>;
export let IdentityUsersReplaceUserNotificationPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserNotificationPreferencesInput>;
export let IdentityUsersReplaceUserPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserPreferencesInput>;
export let IdentityUsersReplaceUserPrivacyPreferencesInputSchema: z.ZodType<IdentityUsersReplaceUserPrivacyPreferencesInput>;
export let IdentityUsersReplaceUserProfileInputSchema: z.ZodType<IdentityUsersReplaceUserProfileInput>;
export let IdentityUsersUpdateUserAccessibilityPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserAccessibilityPreferencesInput>;
export let IdentityUsersUpdateUserLocalizationPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserLocalizationPreferencesInput>;
export let IdentityUsersUpdateUserMetadataInputSchema: z.ZodType<IdentityUsersUpdateUserMetadataInput>;
export let IdentityUsersUpdateUserNotificationPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserNotificationPreferencesInput>;
export let IdentityUsersUpdateUserPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserPreferencesInput>;
export let IdentityUsersUpdateUserPrivacyPreferencesInputSchema: z.ZodType<IdentityUsersUpdateUserPrivacyPreferencesInput>;
export let IdentityUsersUpdateUserProfileInputSchema: z.ZodType<IdentityUsersUpdateUserProfileInput>;
export let IdentityUsersUpdateUserInputSchema: z.ZodType<IdentityUsersUpdateUserInput>;
export let IdentityUsersUpdateUserRequestItemSchema: z.ZodType<IdentityUsersUpdateUserRequestItem>;
export let IdentityUsersUserSchema: z.ZodType<IdentityUsersUser>;
export let IdentityUsersUserAccessibilityPreferencesSchema: z.ZodType<IdentityUsersUserAccessibilityPreferences>;
export let IdentityUsersUserDtoSchema: z.ZodType<IdentityUsersUserDto>;
export let IdentityUsersUserLocalizationPreferencesSchema: z.ZodType<IdentityUsersUserLocalizationPreferences>;
export let IdentityUsersUserMetadataSchema: z.ZodType<IdentityUsersUserMetadata>;
export let IdentityUsersUserMetadataDtoSchema: z.ZodType<IdentityUsersUserMetadataDto>;
export let IdentityUsersUserNotificationSchema: z.ZodType<IdentityUsersUserNotification>;
export let IdentityUsersUserNotificationDetailSchema: z.ZodType<IdentityUsersUserNotificationDetail>;
export let IdentityUsersUserNotificationDtoSchema: z.ZodType<IdentityUsersUserNotificationDto>;
export let IdentityUsersUserNotificationPreferencesSchema: z.ZodType<IdentityUsersUserNotificationPreferences>;
export let IdentityUsersUserPreferencesSchema: z.ZodType<IdentityUsersUserPreferences>;
export let IdentityUsersUserPreferencesDtoSchema: z.ZodType<IdentityUsersUserPreferencesDto>;
export let IdentityUsersUserPrivacyPreferencesSchema: z.ZodType<IdentityUsersUserPrivacyPreferences>;
export let IdentityUsersUserProfileSchema: z.ZodType<IdentityUsersUserProfile>;
export let IdentityUsersUserProfileDtoSchema: z.ZodType<IdentityUsersUserProfileDto>;
export let IdentityUsersUserStatusSchema: z.ZodType<IdentityUsersUserStatus>;
export let KeyValuePairStringAuthenticationExtensionsPRFValuesSchema: z.ZodType<KeyValuePairStringAuthenticationExtensionsPRFValues>;
export let LaunchPadCreateLaunchPlanInputSchema: z.ZodType<LaunchPadCreateLaunchPlanInput>;
export let LaunchPadLaunchChecklistItemSchema: z.ZodType<LaunchPadLaunchChecklistItem>;
export let LaunchPadLaunchChecklistItemInputSchema: z.ZodType<LaunchPadLaunchChecklistItemInput>;
export let LaunchPadLaunchPlanSchema: z.ZodType<LaunchPadLaunchPlan>;
export let LaunchPadLaunchPlanStatusSchema: z.ZodType<LaunchPadLaunchPlanStatus>;
export let LearningAssessmentsAssessmentDefinitionSchema: z.ZodType<LearningAssessmentsAssessmentDefinition>;
export let LearningAssessmentsAssessmentSchema: z.ZodType<LearningAssessmentsAssessment>;
export let LearningAssessmentsAssessmentGroupAnalyticsSchema: z.ZodType<LearningAssessmentsAssessmentGroupAnalytics>;
export let LearningAssessmentsAssessmentGroupSchema: z.ZodType<LearningAssessmentsAssessmentGroup>;
export let LearningAssessmentsAssessmentPresentationModeSchema: z.ZodType<LearningAssessmentsAssessmentPresentationMode>;
export let LearningAssessmentsAssessmentScoreBucketSchema: z.ZodType<LearningAssessmentsAssessmentScoreBucket>;
export let LearningAssessmentsAssessmentSubmissionSchema: z.ZodType<LearningAssessmentsAssessmentSubmission>;
export let LearningAssessmentsAssessmentTypeSchema: z.ZodType<LearningAssessmentsAssessmentType>;
export let LearningAssessmentsAssignAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsAssignAssessmentGroupInput>;
export let LearningAssessmentsCanAttemptOutputSchema: z.ZodType<LearningAssessmentsCanAttemptOutput>;
export let LearningAssessmentsCourseAssessmentAnalyticsSchema: z.ZodType<LearningAssessmentsCourseAssessmentAnalytics>;
export let LearningAssessmentsCreateAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsCreateAssessmentGroupInput>;
export let LearningAssessmentsCreateAssessmentInputSchema: z.ZodType<LearningAssessmentsCreateAssessmentInput>;
export let LearningAssessmentsGradeSubmissionInputSchema: z.ZodType<LearningAssessmentsGradeSubmissionInput>;
export let LearningAssessmentsInteractiveVideoAssessmentCueSchema: z.ZodType<LearningAssessmentsInteractiveVideoAssessmentCue>;
export let LearningAssessmentsLearnerAssessmentAttemptSchema: z.ZodType<LearningAssessmentsLearnerAssessmentAttempt>;
export let LearningAssessmentsLearnerAssessmentSubmissionSchema: z.ZodType<LearningAssessmentsLearnerAssessmentSubmission>;
export let LearningAssessmentsLearnerInteractiveVideoAssessmentCueSchema: z.ZodType<LearningAssessmentsLearnerInteractiveVideoAssessmentCue>;
export let LearningAssessmentsLinkInteractiveVideoCueInputSchema: z.ZodType<LearningAssessmentsLinkInteractiveVideoCueInput>;
export let LearningAssessmentsStartSubmissionInputSchema: z.ZodType<LearningAssessmentsStartSubmissionInput>;
export let LearningAssessmentsSubmissionModalitySchema: z.ZodType<LearningAssessmentsSubmissionModality>;
export let LearningAssessmentsSubmissionStatusSchema: z.ZodType<LearningAssessmentsSubmissionStatus>;
export let LearningAssessmentsSubmitAssessmentInputSchema: z.ZodType<LearningAssessmentsSubmitAssessmentInput>;
export let LearningAssessmentsUpdateAssessmentDefinitionInputSchema: z.ZodType<LearningAssessmentsUpdateAssessmentDefinitionInput>;
export let LearningAssessmentsUpdateAssessmentGroupInputSchema: z.ZodType<LearningAssessmentsUpdateAssessmentGroupInput>;
export let LearningAssessmentsUpdateAssessmentInputSchema: z.ZodType<LearningAssessmentsUpdateAssessmentInput>;
export let LearningCertificatesCertificateSchema: z.ZodType<LearningCertificatesCertificate>;
export let LearningCertificatesCertificateStatusSchema: z.ZodType<LearningCertificatesCertificateStatus>;
export let LearningCertificatesCertificateTemplateDetailSchema: z.ZodType<LearningCertificatesCertificateTemplateDetail>;
export let LearningCertificatesCertificateTemplateSchema: z.ZodType<LearningCertificatesCertificateTemplate>;
export let LearningCertificatesCertificateVerificationResultSchema: z.ZodType<LearningCertificatesCertificateVerificationResult>;
export let LearningCertificatesCreateCertificateTemplateInputSchema: z.ZodType<LearningCertificatesCreateCertificateTemplateInput>;
export let LearningCertificatesIssueCertificateInputSchema: z.ZodType<LearningCertificatesIssueCertificateInput>;
export let LearningCertificatesRevokeCertificateInputSchema: z.ZodType<LearningCertificatesRevokeCertificateInput>;
export let LearningCertificatesUpdateCertificateTemplateInputSchema: z.ZodType<LearningCertificatesUpdateCertificateTemplateInput>;
export let LearningCohortsApplyCohortScheduleInputSchema: z.ZodType<LearningCohortsApplyCohortScheduleInput>;
export let LearningCohortsAvailableCohortContentSchema: z.ZodType<LearningCohortsAvailableCohortContent>;
export let LearningCohortsCohortCalendarEntrySchema: z.ZodType<LearningCohortsCohortCalendarEntry>;
export let LearningCohortsCohortSchema: z.ZodType<LearningCohortsCohort>;
export let LearningCohortsCohortPacingModeSchema: z.ZodType<LearningCohortsCohortPacingMode>;
export let LearningCohortsCohortReleasePolicySchema: z.ZodType<LearningCohortsCohortReleasePolicy>;
export let LearningCohortsCohortScheduleConflictSchema: z.ZodType<LearningCohortsCohortScheduleConflict>;
export let LearningCohortsCohortScheduleSchema: z.ZodType<LearningCohortsCohortSchedule>;
export let LearningCohortsCohortScheduleItemSchema: z.ZodType<LearningCohortsCohortScheduleItem>;
export let LearningCohortsCohortScheduleItemStatusSchema: z.ZodType<LearningCohortsCohortScheduleItemStatus>;
export let LearningCohortsCohortScheduleItemTypeSchema: z.ZodType<LearningCohortsCohortScheduleItemType>;
export let LearningCohortsCohortSchedulePreviewSchema: z.ZodType<LearningCohortsCohortSchedulePreview>;
export let LearningCohortsCohortSchedulePreviewItemSchema: z.ZodType<LearningCohortsCohortSchedulePreviewItem>;
export let LearningCohortsCohortScheduleSummarySchema: z.ZodType<LearningCohortsCohortScheduleSummary>;
export let LearningCohortsCohortStatusSchema: z.ZodType<LearningCohortsCohortStatus>;
export let LearningCohortsCohortVisibilityOverrideSchema: z.ZodType<LearningCohortsCohortVisibilityOverride>;
export let LearningCohortsCourseCohortCalendarSchema: z.ZodType<LearningCohortsCourseCohortCalendar>;
export let LearningCohortsCreateCohortInputSchema: z.ZodType<LearningCohortsCreateCohortInput>;
export let LearningCohortsPreviewCohortScheduleInputSchema: z.ZodType<LearningCohortsPreviewCohortScheduleInput>;
export let LearningCohortsScheduleConflictSeveritySchema: z.ZodType<LearningCohortsScheduleConflictSeverity>;
export let LearningCohortsScheduleShiftScopeSchema: z.ZodType<LearningCohortsScheduleShiftScope>;
export let LearningCohortsShiftCohortScheduleInputSchema: z.ZodType<LearningCohortsShiftCohortScheduleInput>;
export let LearningCohortsUpdateCohortInputSchema: z.ZodType<LearningCohortsUpdateCohortInput>;
export let LearningCohortsUpdateCohortScheduleItemInputSchema: z.ZodType<LearningCohortsUpdateCohortScheduleItemInput>;
export let LearningCohortsUpdateCohortScheduleInputSchema: z.ZodType<LearningCohortsUpdateCohortScheduleInput>;
export let LearningCoursesActivityGradeSchema: z.ZodType<LearningCoursesActivityGrade>;
export let LearningCoursesActivitySettingsSchema: z.ZodType<LearningCoursesActivitySettings>;
export let LearningCoursesCircularDependencyCheckResultSchema: z.ZodType<LearningCoursesCircularDependencyCheckResult>;
export let LearningCoursesCloneProgramSchema: z.ZodType<LearningCoursesCloneProgram>;
export let LearningCoursesCompleteContentInputSchema: z.ZodType<LearningCoursesCompleteContentInput>;
export let LearningCoursesCompleteCourseCheckoutInputSchema: z.ZodType<LearningCoursesCompleteCourseCheckoutInput>;
export let LearningCoursesCompleteCourseCheckoutOutputSchema: z.ZodType<LearningCoursesCompleteCourseCheckoutOutput>;
export let LearningCoursesCompletionRatesSchema: z.ZodType<LearningCoursesCompletionRates>;
export let LearningCoursesCompletionTrendSchema: z.ZodType<LearningCoursesCompletionTrend>;
export let LearningCoursesContentInteractionSchema: z.ZodType<LearningCoursesContentInteraction>;
export let LearningCoursesContentInteractionEventSchema: z.ZodType<LearningCoursesContentInteractionEvent>;
export let LearningCoursesContentInteractionEventTypeSchema: z.ZodType<LearningCoursesContentInteractionEventType>;
export let LearningCoursesContentInteractionSummarySchema: z.ZodType<LearningCoursesContentInteractionSummary>;
export let LearningCoursesContentProgressSchema: z.ZodType<LearningCoursesContentProgress>;
export let LearningCoursesContentStatsSchema: z.ZodType<LearningCoursesContentStats>;
export let LearningCoursesContentSummarySchema: z.ZodType<LearningCoursesContentSummary>;
export let LearningCoursesCourseSupportTicketMessageInputSchema: z.ZodType<LearningCoursesCourseSupportTicketMessageInput>;
export let LearningCoursesCreateActivityGradeSchema: z.ZodType<LearningCoursesCreateActivityGrade>;
export let LearningCoursesCreatePrerequisiteApiInputSchema: z.ZodType<LearningCoursesCreatePrerequisiteApiInput>;
export let LearningCoursesCreateProductFromProgramSchema: z.ZodType<LearningCoursesCreateProductFromProgram>;
export let LearningCoursesCreateProgramContentSchema: z.ZodType<LearningCoursesCreateProgramContent>;
export let LearningCoursesCreateProgramSchema: z.ZodType<LearningCoursesCreateProgram>;
export let LearningCoursesEngagementMetricsSchema: z.ZodType<LearningCoursesEngagementMetrics>;
export let LearningCoursesEnrollmentStatusSchema: z.ZodType<LearningCoursesEnrollmentStatus>;
export let LearningCoursesGradeStatisticsSchema: z.ZodType<LearningCoursesGradeStatistics>;
export let LearningCoursesGraderSummarySchema: z.ZodType<LearningCoursesGraderSummary>;
export let LearningCoursesGradingMethodSchema: z.ZodType<LearningCoursesGradingMethod>;
export let LearningCoursesLessonContentFormatSchema: z.ZodType<LearningCoursesLessonContentFormat>;
export let LearningCoursesMonetizationSchema: z.ZodType<LearningCoursesMonetization>;
export let LearningCoursesMoveContentSchema: z.ZodType<LearningCoursesMoveContent>;
export let LearningCoursesPrerequisiteCheckResultSchema: z.ZodType<LearningCoursesPrerequisiteCheckResult>;
export let LearningCoursesPrerequisiteSchema: z.ZodType<LearningCoursesPrerequisite>;
export let LearningCoursesPrerequisiteStatusSchema: z.ZodType<LearningCoursesPrerequisiteStatus>;
export let LearningCoursesPrerequisiteTypeSchema: z.ZodType<LearningCoursesPrerequisiteType>;
export let LearningCoursesPricingSchema: z.ZodType<LearningCoursesPricing>;
export let LearningCoursesProgramAnalyticsSchema: z.ZodType<LearningCoursesProgramAnalytics>;
export let LearningCoursesProgramContentSchema: z.ZodType<LearningCoursesProgramContent>;
export let LearningCoursesProgramContentTypeSchema: z.ZodType<LearningCoursesProgramContentType>;
export let LearningCoursesProgramDifficultySchema: z.ZodType<LearningCoursesProgramDifficulty>;
export let LearningCoursesProgramSchema: z.ZodType<LearningCoursesProgram>;
export let LearningCoursesProgramUserSummarySchema: z.ZodType<LearningCoursesProgramUserSummary>;
export let LearningCoursesProgressStatusSchema: z.ZodType<LearningCoursesProgressStatus>;
export let LearningCoursesRecordContentInteractionEventInputSchema: z.ZodType<LearningCoursesRecordContentInteractionEventInput>;
export let LearningCoursesReflectionResponseResultSchema: z.ZodType<LearningCoursesReflectionResponseResult>;
export let LearningCoursesRejectProgramSchema: z.ZodType<LearningCoursesRejectProgram>;
export let LearningCoursesReorderContentSchema: z.ZodType<LearningCoursesReorderContent>;
export let LearningCoursesReorderPrerequisitesInputSchema: z.ZodType<LearningCoursesReorderPrerequisitesInput>;
export let LearningCoursesResolveCourseSupportTicketInputSchema: z.ZodType<LearningCoursesResolveCourseSupportTicketInput>;
export let LearningCoursesRevenueAnalyticsSchema: z.ZodType<LearningCoursesRevenueAnalytics>;
export let LearningCoursesRevenueChartSchema: z.ZodType<LearningCoursesRevenueChart>;
export let LearningCoursesScheduleProgramSchema: z.ZodType<LearningCoursesScheduleProgram>;
export let LearningCoursesSearchContentSchema: z.ZodType<LearningCoursesSearchContent>;
export let LearningCoursesSendCourseStudentMessageInputSchema: z.ZodType<LearningCoursesSendCourseStudentMessageInput>;
export let LearningCoursesSendCourseStudentMessageOutputSchema: z.ZodType<LearningCoursesSendCourseStudentMessageOutput>;
export let LearningCoursesStartContentInputSchema: z.ZodType<LearningCoursesStartContentInput>;
export let LearningCoursesStudentSummarySchema: z.ZodType<LearningCoursesStudentSummary>;
export let LearningCoursesSubmitContentInputSchema: z.ZodType<LearningCoursesSubmitContentInput>;
export let LearningCoursesSubmitUserContentSchema: z.ZodType<LearningCoursesSubmitUserContent>;
export let LearningCoursesSurveyResponseResultSchema: z.ZodType<LearningCoursesSurveyResponseResult>;
export let LearningCoursesUpdateActivityGradeSchema: z.ZodType<LearningCoursesUpdateActivityGrade>;
export let LearningCoursesUpdatePrerequisiteApiInputSchema: z.ZodType<LearningCoursesUpdatePrerequisiteApiInput>;
export let LearningCoursesUpdatePricingSchema: z.ZodType<LearningCoursesUpdatePricing>;
export let LearningCoursesUpdateProgramContentSchema: z.ZodType<LearningCoursesUpdateProgramContent>;
export let LearningCoursesUpdateProgramSchema: z.ZodType<LearningCoursesUpdateProgram>;
export let LearningCoursesUpdateProgressSchema: z.ZodType<LearningCoursesUpdateProgress>;
export let LearningCoursesUpdateProgressInputSchema: z.ZodType<LearningCoursesUpdateProgressInput>;
export let LearningCoursesUpdateTimeSpentInputSchema: z.ZodType<LearningCoursesUpdateTimeSpentInput>;
export let LearningCoursesUserProgressSchema: z.ZodType<LearningCoursesUserProgress>;
export let LearningCoursesVisibilitySchema: z.ZodType<LearningCoursesVisibility>;
export let LearningEnrollmentsEnrollUserInputSchema: z.ZodType<LearningEnrollmentsEnrollUserInput>;
export let LearningEnrollmentsEnrollmentSchema: z.ZodType<LearningEnrollmentsEnrollment>;
export let LearningEnrollmentsEnrollmentStatusSchema: z.ZodType<LearningEnrollmentsEnrollmentStatus>;
export let LearningEnrollmentsUpdateEnrollmentProgressInputSchema: z.ZodType<LearningEnrollmentsUpdateEnrollmentProgressInput>;
export let LearningExperienceDiscoveryCollectionTypeSchema: z.ZodType<LearningExperienceDiscoveryCollectionType>;
export let LearningExperienceDiscoveryCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryCourseCollection>;
export let LearningExperienceDiscoveryCreateCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryCreateCourseCollection>;
export let LearningExperienceDiscoveryCreateFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryCreateFeaturedContent>;
export let LearningExperienceDiscoveryFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryFeaturedContent>;
export let LearningExperienceDiscoveryFeaturedContentTypeSchema: z.ZodType<LearningExperienceDiscoveryFeaturedContentType>;
export let LearningExperienceDiscoveryPopularSearchResultSchema: z.ZodType<LearningExperienceDiscoveryPopularSearchResult>;
export let LearningExperienceDiscoveryRecordSearchClickSchema: z.ZodType<LearningExperienceDiscoveryRecordSearchClick>;
export let LearningExperienceDiscoveryRecordSearchSchema: z.ZodType<LearningExperienceDiscoveryRecordSearch>;
export let LearningExperienceDiscoverySearchHistorySchema: z.ZodType<LearningExperienceDiscoverySearchHistory>;
export let LearningExperienceDiscoveryUpdateCourseCollectionSchema: z.ZodType<LearningExperienceDiscoveryUpdateCourseCollection>;
export let LearningExperienceDiscoveryUpdateFeaturedContentSchema: z.ZodType<LearningExperienceDiscoveryUpdateFeaturedContent>;
export let LearningExperienceLearningPathsAddCourseToPathSchema: z.ZodType<LearningExperienceLearningPathsAddCourseToPath>;
export let LearningExperienceLearningPathsCourseOrderSchema: z.ZodType<LearningExperienceLearningPathsCourseOrder>;
export let LearningExperienceLearningPathsCreateLearningPathSchema: z.ZodType<LearningExperienceLearningPathsCreateLearningPath>;
export let LearningExperienceLearningPathsLearningPathCourseSchema: z.ZodType<LearningExperienceLearningPathsLearningPathCourse>;
export let LearningExperienceLearningPathsLearningPathDetailSchema: z.ZodType<LearningExperienceLearningPathsLearningPathDetail>;
export let LearningExperienceLearningPathsLearningPathDifficultySchema: z.ZodType<LearningExperienceLearningPathsLearningPathDifficulty>;
export let LearningExperienceLearningPathsLearningPathSchema: z.ZodType<LearningExperienceLearningPathsLearningPath>;
export let LearningExperienceLearningPathsLearningPathEnrollmentSchema: z.ZodType<LearningExperienceLearningPathsLearningPathEnrollment>;
export let LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema: z.ZodType<LearningExperienceLearningPathsLearningPathEnrollmentStatus>;
export let LearningExperienceLearningPathsLearningPathStatisticsSchema: z.ZodType<LearningExperienceLearningPathsLearningPathStatistics>;
export let LearningExperienceLearningPathsReorderCoursesSchema: z.ZodType<LearningExperienceLearningPathsReorderCourses>;
export let LearningExperienceLearningPathsUpdateLearningPathSchema: z.ZodType<LearningExperienceLearningPathsUpdateLearningPath>;
export let LearningExperienceLearningPathsUpdatePathProgressSchema: z.ZodType<LearningExperienceLearningPathsUpdatePathProgress>;
export let LearningExperienceRecommendationsAddSkillInputSchema: z.ZodType<LearningExperienceRecommendationsAddSkillInput>;
export let LearningExperienceRecommendationsCreateOrUpdateLearningProfileSchema: z.ZodType<LearningExperienceRecommendationsCreateOrUpdateLearningProfile>;
export let LearningExperienceRecommendationsPopularCourseSchema: z.ZodType<LearningExperienceRecommendationsPopularCourse>;
export let LearningExperienceRecommendationsRecommendationSchema: z.ZodType<LearningExperienceRecommendationsRecommendation>;
export let LearningExperienceRecommendationsRecommendationStatisticsSchema: z.ZodType<LearningExperienceRecommendationsRecommendationStatistics>;
export let LearningExperienceRecommendationsRecommendationTypeSchema: z.ZodType<LearningExperienceRecommendationsRecommendationType>;
export let LearningExperienceRecommendationsSimilarCourseSchema: z.ZodType<LearningExperienceRecommendationsSimilarCourse>;
export let LearningExperienceRecommendationsTrendingCourseSchema: z.ZodType<LearningExperienceRecommendationsTrendingCourse>;
export let LearningExperienceRecommendationsUserLearningProfileSchema: z.ZodType<LearningExperienceRecommendationsUserLearningProfile>;
export let LearningExperienceSocialControllersUpdateReviewModerationInputSchema: z.ZodType<LearningExperienceSocialControllersUpdateReviewModerationInput>;
export let LearningExperienceSocialFeedItemTypeSchema: z.ZodType<LearningExperienceSocialFeedItemType>;
export let LearningExperienceSocialServicesCourseDiscussionSchema: z.ZodType<LearningExperienceSocialServicesCourseDiscussion>;
export let LearningExperienceSocialServicesCourseLikeSchema: z.ZodType<LearningExperienceSocialServicesCourseLike>;
export let LearningExperienceSocialServicesCourseRatingStatsSchema: z.ZodType<LearningExperienceSocialServicesCourseRatingStats>;
export let LearningExperienceSocialServicesCourseReviewSchema: z.ZodType<LearningExperienceSocialServicesCourseReview>;
export let LearningExperienceSocialServicesCourseWishlistSchema: z.ZodType<LearningExperienceSocialServicesCourseWishlist>;
export let LearningExperienceSocialServicesCreateDiscussionInputSchema: z.ZodType<LearningExperienceSocialServicesCreateDiscussionInput>;
export let LearningExperienceSocialServicesCreateReplyInputSchema: z.ZodType<LearningExperienceSocialServicesCreateReplyInput>;
export let LearningExperienceSocialServicesCreateReviewInputSchema: z.ZodType<LearningExperienceSocialServicesCreateReviewInput>;
export let LearningExperienceSocialServicesDiscussionReplySchema: z.ZodType<LearningExperienceSocialServicesDiscussionReply>;
export let LearningExperienceSocialServicesPersonalizedFeedItemSchema: z.ZodType<LearningExperienceSocialServicesPersonalizedFeedItem>;
export let LearningExperienceSocialServicesWishlistPreferencesInputSchema: z.ZodType<LearningExperienceSocialServicesWishlistPreferencesInput>;
export let LearningWorkspacesLearnerAnnouncementSchema: z.ZodType<LearningWorkspacesLearnerAnnouncement>;
export let LearningWorkspacesLearnerAssessmentDeadlineSchema: z.ZodType<LearningWorkspacesLearnerAssessmentDeadline>;
export let LearningWorkspacesLearnerAssessmentSchema: z.ZodType<LearningWorkspacesLearnerAssessment>;
export let LearningWorkspacesLearnerAssessmentGroupSchema: z.ZodType<LearningWorkspacesLearnerAssessmentGroup>;
export let LearningWorkspacesLearnerAssessmentSubmissionSchema: z.ZodType<LearningWorkspacesLearnerAssessmentSubmission>;
export let LearningWorkspacesLearnerCertificateSchema: z.ZodType<LearningWorkspacesLearnerCertificate>;
export let LearningWorkspacesLearnerCohortSchema: z.ZodType<LearningWorkspacesLearnerCohort>;
export let LearningWorkspacesLearnerContentSchema: z.ZodType<LearningWorkspacesLearnerContent>;
export let LearningWorkspacesLearnerContentProgressSchema: z.ZodType<LearningWorkspacesLearnerContentProgress>;
export let LearningWorkspacesLearnerCourseSummarySchema: z.ZodType<LearningWorkspacesLearnerCourseSummary>;
export let LearningWorkspacesLearnerCourseWorkspaceSchema: z.ZodType<LearningWorkspacesLearnerCourseWorkspace>;
export let LearningWorkspacesLearnerDashboardSchema: z.ZodType<LearningWorkspacesLearnerDashboard>;
export let LearningWorkspacesLearnerDiscussionSchema: z.ZodType<LearningWorkspacesLearnerDiscussion>;
export let LearningWorkspacesLearnerGradeItemSchema: z.ZodType<LearningWorkspacesLearnerGradeItem>;
export let LearningWorkspacesLearnerGradeSummarySchema: z.ZodType<LearningWorkspacesLearnerGradeSummary>;
export let LearningWorkspacesLearnerScheduleEntrySchema: z.ZodType<LearningWorkspacesLearnerScheduleEntry>;
export let LearningWorkspacesLearnerSearchResultSchema: z.ZodType<LearningWorkspacesLearnerSearchResult>;
export let MoneySchema: z.ZodType<Money>;
export let MvcProblemDetailsSchema: z.ZodType<MvcProblemDetails>;
export let NotificationsNotificationChannelSchema: z.ZodType<NotificationsNotificationChannel>;
export let ObjectsAttestationConveyancePreferenceSchema: z.ZodType<ObjectsAttestationConveyancePreference>;
export let ObjectsAttestationStatementFormatIdentifierSchema: z.ZodType<ObjectsAttestationStatementFormatIdentifier>;
export let ObjectsAuthenticationExtensionsClientInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsClientInputs>;
export let ObjectsAuthenticationExtensionsLargeBlobInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsLargeBlobInputs>;
export let ObjectsAuthenticationExtensionsPRFInputsSchema: z.ZodType<ObjectsAuthenticationExtensionsPRFInputs>;
export let ObjectsAuthenticationExtensionsPRFValuesSchema: z.ZodType<ObjectsAuthenticationExtensionsPRFValues>;
export let ObjectsAuthenticatorAttachmentSchema: z.ZodType<ObjectsAuthenticatorAttachment>;
export let ObjectsAuthenticatorTransportSchema: z.ZodType<ObjectsAuthenticatorTransport>;
export let ObjectsCOSEAlgorithmSchema: z.ZodType<ObjectsCOSEAlgorithm>;
export let ObjectsCredentialProtectionPolicySchema: z.ZodType<ObjectsCredentialProtectionPolicy>;
export let ObjectsLargeBlobSupportSchema: z.ZodType<ObjectsLargeBlobSupport>;
export let ObjectsPublicKeyCredentialDescriptorSchema: z.ZodType<ObjectsPublicKeyCredentialDescriptor>;
export let ObjectsPublicKeyCredentialHintSchema: z.ZodType<ObjectsPublicKeyCredentialHint>;
export let ObjectsPublicKeyCredentialTypeSchema: z.ZodType<ObjectsPublicKeyCredentialType>;
export let ObjectsResidentKeyRequirementSchema: z.ZodType<ObjectsResidentKeyRequirement>;
export let ObjectsUserVerificationRequirementSchema: z.ZodType<ObjectsUserVerificationRequirement>;
export let PagedResultOfGameGuildCommerceProductsProductDtoSchema: z.ZodType<PagedResultOfGameGuildCommerceProductsProductDto>;
export let PagedResultOfGameGuildCommerceProductsPromoCodeDtoSchema: z.ZodType<PagedResultOfGameGuildCommerceProductsPromoCodeDto>;
export let PagedResultOfGameGuildCommerceProductsSupportTicketDtoSchema: z.ZodType<PagedResultOfGameGuildCommerceProductsSupportTicketDto>;
export let PagedResultOfGameGuildCommerceSubscriptionsSubscriptionSchema: z.ZodType<PagedResultOfGameGuildCommerceSubscriptionsSubscription>;
export let PagedResultOfGameGuildCommerceSubscriptionsSubscriptionNotificationDtoSchema: z.ZodType<PagedResultOfGameGuildCommerceSubscriptionsSubscriptionNotificationDto>;
export let PagedResultOfGameGuildIdentityTenantsTenantSchema: z.ZodType<PagedResultOfGameGuildIdentityTenantsTenant>;
export let PagedResultOfGameGuildIdentityTenantsTenantAuditLogEntrySchema: z.ZodType<PagedResultOfGameGuildIdentityTenantsTenantAuditLogEntry>;
export let PagedResultOfGameGuildIdentityUsersUserDtoSchema: z.ZodType<PagedResultOfGameGuildIdentityUsersUserDto>;
export let PagedResultOfGameGuildIdentityUsersUserNotificationDtoSchema: z.ZodType<PagedResultOfGameGuildIdentityUsersUserNotificationDto>;
export let PagedResultOfGameGuildIdentityUsersUserProfileDtoSchema: z.ZodType<PagedResultOfGameGuildIdentityUsersUserProfileDto>;
export let ProgramCategorySchema: z.ZodType<ProgramCategory>;
export let ProjectsAddCollaboratorInputSchema: z.ZodType<ProjectsAddCollaboratorInput>;
export let ProjectsAddProjectCollaboratorInputSchema: z.ZodType<ProjectsAddProjectCollaboratorInput>;
export let ProjectsCollaboratorSchema: z.ZodType<ProjectsCollaborator>;
export let ProjectsCreateProjectInputSchema: z.ZodType<ProjectsCreateProjectInput>;
export let ProjectsDevelopmentStatusSchema: z.ZodType<ProjectsDevelopmentStatus>;
export let ProjectsEffectivePermissionSchema: z.ZodType<ProjectsEffectivePermission>;
export let ProjectsInvitationResultSchema: z.ZodType<ProjectsInvitationResult>;
export let ProjectsInviteProjectCollaboratorInputSchema: z.ZodType<ProjectsInviteProjectCollaboratorInput>;
export let ProjectsLinkProjectStoreProductInputSchema: z.ZodType<ProjectsLinkProjectStoreProductInput>;
export let ProjectsPermissionUpdateResultSchema: z.ZodType<ProjectsPermissionUpdateResult>;
export let ProjectsProjectSchema: z.ZodType<ProjectsProject>;
export let ProjectsProjectCategorySchema: z.ZodType<ProjectsProjectCategory>;
export let ProjectsProjectCollaboratorSchema: z.ZodType<ProjectsProjectCollaborator>;
export let ProjectsProjectCollaboratorDtoSchema: z.ZodType<ProjectsProjectCollaboratorDto>;
export let ProjectsProjectFeedbackSchema: z.ZodType<ProjectsProjectFeedback>;
export let ProjectsProjectFollowerSchema: z.ZodType<ProjectsProjectFollower>;
export let ProjectsProjectInvitationSchema: z.ZodType<ProjectsProjectInvitation>;
export let ProjectsProjectInvitationStatusSchema: z.ZodType<ProjectsProjectInvitationStatus>;
export let ProjectsProjectJamSubmissionSchema: z.ZodType<ProjectsProjectJamSubmission>;
export let ProjectsProjectMetadataSchema: z.ZodType<ProjectsProjectMetadata>;
export let ProjectsProjectReleaseSchema: z.ZodType<ProjectsProjectRelease>;
export let ProjectsProjectRoleTemplateSchema: z.ZodType<ProjectsProjectRoleTemplate>;
export let ProjectsProjectStatisticsSchema: z.ZodType<ProjectsProjectStatistics>;
export let ProjectsProjectStoreProductProjectionSchema: z.ZodType<ProjectsProjectStoreProductProjection>;
export let ProjectsProjectTeamSchema: z.ZodType<ProjectsProjectTeam>;
export let ProjectsProjectTypeSchema: z.ZodType<ProjectsProjectType>;
export let ProjectsProjectVersionSchema: z.ZodType<ProjectsProjectVersion>;
export let ProjectsShareProjectInputSchema: z.ZodType<ProjectsShareProjectInput>;
export let ProjectsShareProjectWithRoleInputSchema: z.ZodType<ProjectsShareProjectWithRoleInput>;
export let ProjectsShareResultSchema: z.ZodType<ProjectsShareResult>;
export let ProjectsTeamSchema: z.ZodType<ProjectsTeam>;
export let ProjectsTeamMemberSchema: z.ZodType<ProjectsTeamMember>;
export let ProjectsUpdateCollaboratorInputSchema: z.ZodType<ProjectsUpdateCollaboratorInput>;
export let ProjectsUpdateProjectCollaboratorInputSchema: z.ZodType<ProjectsUpdateProjectCollaboratorInput>;
export let ProjectsUpdateProjectInputSchema: z.ZodType<ProjectsUpdateProjectInput>;
export let ResourcesArchiveResourceUsageRecordsInputSchema: z.ZodType<ResourcesArchiveResourceUsageRecordsInput>;
export let ResourcesCheckResourceQuotaInputSchema: z.ZodType<ResourcesCheckResourceQuotaInput>;
export let ResourcesCleanupOrphanedResourcesInputSchema: z.ZodType<ResourcesCleanupOrphanedResourcesInput>;
export let ResourcesEffectiveSettingOutputSchema: z.ZodType<ResourcesEffectiveSettingOutput>;
export let ResourcesRecordTenantResourceUsageInputSchema: z.ZodType<ResourcesRecordTenantResourceUsageInput>;
export let ResourcesRecordUserResourceUsageInputSchema: z.ZodType<ResourcesRecordUserResourceUsageInput>;
export let ResourcesResourceMetadataSchema: z.ZodType<ResourcesResourceMetadata>;
export let ResourcesResourceQuotaEnforcementResultSchema: z.ZodType<ResourcesResourceQuotaEnforcementResult>;
export let ResourcesResourceQuotaPeriodSchema: z.ZodType<ResourcesResourceQuotaPeriod>;
export let ResourcesResourceQuotaOutputSchema: z.ZodType<ResourcesResourceQuotaOutput>;
export let ResourcesResourceSettingsSchema: z.ZodType<ResourcesResourceSettings>;
export let ResourcesResourceUsageTypeSchema: z.ZodType<ResourcesResourceUsageType>;
export let ResourcesSetQuotaInputSchema: z.ZodType<ResourcesSetQuotaInput>;
export let ResourcesSetResourceMetadataInputSchema: z.ZodType<ResourcesSetResourceMetadataInput>;
export let ResourcesSetResourceSettingsInputSchema: z.ZodType<ResourcesSetResourceSettingsInput>;
export let ResourcesSetUserResourceSettingsInputSchema: z.ZodType<ResourcesSetUserResourceSettingsInput>;
export let ResourcesToggleResourceQuotaInputSchema: z.ZodType<ResourcesToggleResourceQuotaInput>;
export let ResourcesTrendGranularitySchema: z.ZodType<ResourcesTrendGranularity>;
export let ResourcesUsageRecordSchema: z.ZodType<ResourcesUsageRecord>;
export let ResourcesUsageTrendDataPointSchema: z.ZodType<ResourcesUsageTrendDataPoint>;
export let ResourcesUsageTrendsResultSchema: z.ZodType<ResourcesUsageTrendsResult>;
export let SocialBlogBlogPostSchema: z.ZodType<SocialBlogBlogPost>;
export let SocialBlogBlogPostStatusSchema: z.ZodType<SocialBlogBlogPostStatus>;
export let SocialBlogCreateBlogPostInputSchema: z.ZodType<SocialBlogCreateBlogPostInput>;
export let SocialFeedAddFeedItemInputSchema: z.ZodType<SocialFeedAddFeedItemInput>;
export let SocialFeedFeedContentTypeSchema: z.ZodType<SocialFeedFeedContentType>;
export let SocialFeedFeedItemSchema: z.ZodType<SocialFeedFeedItem>;
export let SocialFeedFeedItemReasonSchema: z.ZodType<SocialFeedFeedItemReason>;
export let SocialGroupsApproveSocialGroupMemberInputSchema: z.ZodType<SocialGroupsApproveSocialGroupMemberInput>;
export let SocialGroupsChangeSocialGroupMemberRoleInputSchema: z.ZodType<SocialGroupsChangeSocialGroupMemberRoleInput>;
export let SocialGroupsCreateSocialGroupInputSchema: z.ZodType<SocialGroupsCreateSocialGroupInput>;
export let SocialGroupsJoinSocialGroupInputSchema: z.ZodType<SocialGroupsJoinSocialGroupInput>;
export let SocialGroupsSocialGroupSchema: z.ZodType<SocialGroupsSocialGroup>;
export let SocialGroupsSocialGroupMemberSchema: z.ZodType<SocialGroupsSocialGroupMember>;
export let SocialGroupsSocialGroupMemberRoleSchema: z.ZodType<SocialGroupsSocialGroupMemberRole>;
export let SocialGroupsSocialGroupMembershipStatusSchema: z.ZodType<SocialGroupsSocialGroupMembershipStatus>;
export let SocialGroupsSocialGroupStatusSchema: z.ZodType<SocialGroupsSocialGroupStatus>;
export let SocialGroupsSocialGroupTypeSchema: z.ZodType<SocialGroupsSocialGroupType>;
export let SocialGroupsSocialGroupVisibilitySchema: z.ZodType<SocialGroupsSocialGroupVisibility>;
export let SocialGroupsUpdateSocialGroupInputSchema: z.ZodType<SocialGroupsUpdateSocialGroupInput>;
export let SocialProfilesAddProfilePortfolioItemBodySchema: z.ZodType<SocialProfilesAddProfilePortfolioItemBody>;
export let SocialProfilesAddProfileSkillBodySchema: z.ZodType<SocialProfilesAddProfileSkillBody>;
export let SocialProfilesProfileAvailabilityStatusSchema: z.ZodType<SocialProfilesProfileAvailabilityStatus>;
export let SocialProfilesProfilePortfolioItemSchema: z.ZodType<SocialProfilesProfilePortfolioItem>;
export let SocialProfilesProfileSkillSchema: z.ZodType<SocialProfilesProfileSkill>;
export let SocialProfilesProfileSkillProficiencySchema: z.ZodType<SocialProfilesProfileSkillProficiency>;
export let SocialProfilesProfileVisibilitySchema: z.ZodType<SocialProfilesProfileVisibility>;
export let SocialProfilesSocialProfileSchema: z.ZodType<SocialProfilesSocialProfile>;
export let SocialProfilesUpdateProfilePortfolioItemBodySchema: z.ZodType<SocialProfilesUpdateProfilePortfolioItemBody>;
export let SocialProfilesUpdateProfilePrivacyBodySchema: z.ZodType<SocialProfilesUpdateProfilePrivacyBody>;
export let SocialProfilesUpdateProfileStatsBodySchema: z.ZodType<SocialProfilesUpdateProfileStatsBody>;
export let SocialProfilesUpdateSocialProfileBodySchema: z.ZodType<SocialProfilesUpdateSocialProfileBody>;
export let SocialReactionsReactionSchema: z.ZodType<SocialReactionsReaction>;
export let SocialReactionsReactionTargetTypeSchema: z.ZodType<SocialReactionsReactionTargetType>;
export let SocialReactionsReactionTypeSchema: z.ZodType<SocialReactionsReactionType>;
export let SocialReactionsRemoveReactionInputSchema: z.ZodType<SocialReactionsRemoveReactionInput>;
export let SocialReactionsSetReactionInputSchema: z.ZodType<SocialReactionsSetReactionInput>;
export let SocialReactionsTargetReactionSummarySchema: z.ZodType<SocialReactionsTargetReactionSummary>;
export let SystemDayOfWeekSchema: z.ZodType<SystemDayOfWeek>;
export let TenantInfoSchema: z.ZodType<TenantInfo>;
export let TestingLabAddTestingEventCommitteeMemberInputSchema: z.ZodType<TestingLabAddTestingEventCommitteeMemberInput>;
export let TestingLabAssignTestingLabRoleInputSchema: z.ZodType<TestingLabAssignTestingLabRoleInput>;
export let TestingLabAssignTestingProjectApplicationSlotInputSchema: z.ZodType<TestingLabAssignTestingProjectApplicationSlotInput>;
export let TestingLabAssignTestingProjectToTesterInputSchema: z.ZodType<TestingLabAssignTestingProjectToTesterInput>;
export let TestingLabAttendanceStatusSchema: z.ZodType<TestingLabAttendanceStatus>;
export let TestingLabCancelTestingEventInputSchema: z.ZodType<TestingLabCancelTestingEventInput>;
export let TestingLabCastTestingApplicationVoteInputSchema: z.ZodType<TestingLabCastTestingApplicationVoteInput>;
export let TestingLabConfigureTestingEventLearningInputSchema: z.ZodType<TestingLabConfigureTestingEventLearningInput>;
export let TestingLabCreateSimpleTestingInputSchema: z.ZodType<TestingLabCreateSimpleTestingInput>;
export let TestingLabCreateTestingEventInputSchema: z.ZodType<TestingLabCreateTestingEventInput>;
export let TestingLabCreateTestingLabRoleInputSchema: z.ZodType<TestingLabCreateTestingLabRoleInput>;
export let TestingLabCreateTestingLabSettingsSchema: z.ZodType<TestingLabCreateTestingLabSettings>;
export let TestingLabCreateTestingLocationSchema: z.ZodType<TestingLabCreateTestingLocation>;
export let TestingLabCreateTestingInputSchema: z.ZodType<TestingLabCreateTestingInput>;
export let TestingLabCreateTestingSessionSchema: z.ZodType<TestingLabCreateTestingSession>;
export let TestingLabDecideTestingProjectApplicationInputSchema: z.ZodType<TestingLabDecideTestingProjectApplicationInput>;
export let TestingLabFeedbackFormTypeSchema: z.ZodType<TestingLabFeedbackFormType>;
export let TestingLabFeedbackQualitySchema: z.ZodType<TestingLabFeedbackQuality>;
export let TestingLabFeedbackQualityRatingSchema: z.ZodType<TestingLabFeedbackQualityRating>;
export let TestingLabFeedbackInputSchema: z.ZodType<TestingLabFeedbackInput>;
export let TestingLabGrantResourcePermissionInputSchema: z.ZodType<TestingLabGrantResourcePermissionInput>;
export let TestingLabInstructionTypeSchema: z.ZodType<TestingLabInstructionType>;
export let TestingLabLinkSessionProjectInputSchema: z.ZodType<TestingLabLinkSessionProjectInput>;
export let TestingLabLocationStatusSchema: z.ZodType<TestingLabLocationStatus>;
export let TestingLabParticipationStatusSchema: z.ZodType<TestingLabParticipationStatus>;
export let TestingLabPublicTestingEventProjectionSchema: z.ZodType<TestingLabPublicTestingEventProjection>;
export let TestingLabPublicTestingEventSlotProjectionSchema: z.ZodType<TestingLabPublicTestingEventSlotProjection>;
export let TestingLabRateFeedbackQualitySchema: z.ZodType<TestingLabRateFeedbackQuality>;
export let TestingLabRegisterTestingEventSlotInputSchema: z.ZodType<TestingLabRegisterTestingEventSlotInput>;
export let TestingLabRegistrationStatusSchema: z.ZodType<TestingLabRegistrationStatus>;
export let TestingLabRegistrationTypeSchema: z.ZodType<TestingLabRegistrationType>;
export let TestingLabReportFeedbackSchema: z.ZodType<TestingLabReportFeedback>;
export let TestingLabSessionProjectProjectionSchema: z.ZodType<TestingLabSessionProjectProjection>;
export let TestingLabSessionRegistrationSchema: z.ZodType<TestingLabSessionRegistration>;
export let TestingLabSessionRegistrationInputSchema: z.ZodType<TestingLabSessionRegistrationInput>;
export let TestingLabSessionStatusSchema: z.ZodType<TestingLabSessionStatus>;
export let TestingLabSessionWaitlistSchema: z.ZodType<TestingLabSessionWaitlist>;
export let TestingLabSubmitFeedbackSchema: z.ZodType<TestingLabSubmitFeedback>;
export let TestingLabSubmitTestingEventFeedbackInputSchema: z.ZodType<TestingLabSubmitTestingEventFeedbackInput>;
export let TestingLabSubmitTestingProjectApplicationInputSchema: z.ZodType<TestingLabSubmitTestingProjectApplicationInput>;
export let TestingLabTestingApplicationStatusSchema: z.ZodType<TestingLabTestingApplicationStatus>;
export let TestingLabTestingApplicationVoteSchema: z.ZodType<TestingLabTestingApplicationVote>;
export let TestingLabTestingApplicationVoteDecisionSchema: z.ZodType<TestingLabTestingApplicationVoteDecision>;
export let TestingLabTestingApplicationVoteProjectionSchema: z.ZodType<TestingLabTestingApplicationVoteProjection>;
export let TestingLabTestingCommitteeMemberSchema: z.ZodType<TestingLabTestingCommitteeMember>;
export let TestingLabTestingContextSchema: z.ZodType<TestingLabTestingContext>;
export let TestingLabTestingEventSchema: z.ZodType<TestingLabTestingEvent>;
export let TestingLabTestingEventApprovalModeSchema: z.ZodType<TestingLabTestingEventApprovalMode>;
export let TestingLabTestingEventCommitteeMemberProjectionSchema: z.ZodType<TestingLabTestingEventCommitteeMemberProjection>;
export let TestingLabTestingEventFeedbackProjectionSchema: z.ZodType<TestingLabTestingEventFeedbackProjection>;
export let TestingLabTestingEventFeedbackReviewProjectionSchema: z.ZodType<TestingLabTestingEventFeedbackReviewProjection>;
export let TestingLabTestingEventModeSchema: z.ZodType<TestingLabTestingEventMode>;
export let TestingLabTestingEventProjectionSchema: z.ZodType<TestingLabTestingEventProjection>;
export let TestingLabTestingEventSlotSchema: z.ZodType<TestingLabTestingEventSlot>;
export let TestingLabTestingEventSlotProjectionSchema: z.ZodType<TestingLabTestingEventSlotProjection>;
export let TestingLabTestingEventStatusSchema: z.ZodType<TestingLabTestingEventStatus>;
export let TestingLabTestingFeedbackSchema: z.ZodType<TestingLabTestingFeedback>;
export let TestingLabTestingFeedbackFormSchema: z.ZodType<TestingLabTestingFeedbackForm>;
export let TestingLabTestingFeedbackObligationProjectionSchema: z.ZodType<TestingLabTestingFeedbackObligationProjection>;
export let TestingLabTestingFeedbackObligationStatusSchema: z.ZodType<TestingLabTestingFeedbackObligationStatus>;
export let TestingLabTestingLabAnalyticsReportProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsReportProjection>;
export let TestingLabTestingLabAnalyticsSummaryProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsSummaryProjection>;
export let TestingLabTestingLabAnalyticsTrendProjectionSchema: z.ZodType<TestingLabTestingLabAnalyticsTrendProjection>;
export let TestingLabTestingLabEventAnalyticsProjectionSchema: z.ZodType<TestingLabTestingLabEventAnalyticsProjection>;
export let TestingLabTestingLabLocationAnalyticsProjectionSchema: z.ZodType<TestingLabTestingLabLocationAnalyticsProjection>;
export let TestingLabTestingLabPermissionsSchema: z.ZodType<TestingLabTestingLabPermissions>;
export let TestingLabTestingLabResourcePermissionSchema: z.ZodType<TestingLabTestingLabResourcePermission>;
export let TestingLabTestingLabRoleTemplateSchema: z.ZodType<TestingLabTestingLabRoleTemplate>;
export let TestingLabTestingLabSettingsSchema: z.ZodType<TestingLabTestingLabSettings>;
export let TestingLabTestingLearningCompletionRequirementSchema: z.ZodType<TestingLabTestingLearningCompletionRequirement>;
export let TestingLabTestingLocationSchema: z.ZodType<TestingLabTestingLocation>;
export let TestingLabTestingModeSchema: z.ZodType<TestingLabTestingMode>;
export let TestingLabTestingParticipantSchema: z.ZodType<TestingLabTestingParticipant>;
export let TestingLabTestingParticipantDirectoryItemProjectionSchema: z.ZodType<TestingLabTestingParticipantDirectoryItemProjection>;
export let TestingLabTestingParticipantDirectoryProjectionSchema: z.ZodType<TestingLabTestingParticipantDirectoryProjection>;
export let TestingLabTestingPrioritySchema: z.ZodType<TestingLabTestingPriority>;
export let TestingLabTestingProjectApplicationSchema: z.ZodType<TestingLabTestingProjectApplication>;
export let TestingLabTestingProjectApplicationProjectionSchema: z.ZodType<TestingLabTestingProjectApplicationProjection>;
export let TestingLabTestingInputSchema: z.ZodType<TestingLabTestingInput>;
export let TestingLabTestingRequestStatusSchema: z.ZodType<TestingLabTestingRequestStatus>;
export let TestingLabTestingSessionSchema: z.ZodType<TestingLabTestingSession>;
export let TestingLabTestingSlotRegistrationProjectionSchema: z.ZodType<TestingLabTestingSlotRegistrationProjection>;
export let TestingLabTestingSlotRegistrationStatusSchema: z.ZodType<TestingLabTestingSlotRegistrationStatus>;
export let TestingLabUpdateAttendanceSchema: z.ZodType<TestingLabUpdateAttendance>;
export let TestingLabUpdateTestingEventInputSchema: z.ZodType<TestingLabUpdateTestingEventInput>;
export let TestingLabUpdateTestingLabRoleInputSchema: z.ZodType<TestingLabUpdateTestingLabRoleInput>;
export let TestingLabUpdateTestingLabSettingsSchema: z.ZodType<TestingLabUpdateTestingLabSettings>;
export let TestingLabUpdateTestingLocationSchema: z.ZodType<TestingLabUpdateTestingLocation>;
export let TestingLabUpsertTestingEventSlotInputSchema: z.ZodType<TestingLabUpsertTestingEventSlotInput>;
export let TestingLabUserTestingLabPermissionsSchema: z.ZodType<TestingLabUserTestingLabPermissions>;

// Zod Schema Definitions
/** Zod schema for AIAiChatMessage */
AIAiChatMessageSchema = z.object({
  content: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
});

/** Zod schema for AIAiChatInput */
AIAiChatInputSchema = z.object({
  maxTokens: z.number().int().nullable().optional(),
  messages: z
    .array(z.lazy(() => AIAiChatMessageSchema))
    .nullable()
    .optional(),
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  temperature: z.number().nullable().optional(),
});

/** Zod schema for AIAiCompletionOutput */
AIAiCompletionOutputSchema = z.object({
  finishReason: z.string().nullable().optional(),
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  text: z.string().nullable().optional(),
  usage: z.lazy(() => AIAiUsageSchema).optional(),
});

/** Zod schema for AIAiConversationHistoryEntry */
AIAiConversationHistoryEntrySchema = z.object({
  finishReason: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  model: z.string().nullable().optional(),
  occurredAt: z.string().datetime().optional(),
  outcome: z.string().nullable().optional(),
  outcomeCode: z.string().nullable().optional(),
  outcomeReason: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  requestKind: z.string().nullable().optional(),
  requestText: z.string().nullable().optional(),
  responseText: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  usage: z.lazy(() => AIAiUsageSchema).optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AIAiGenerateInput */
AIAiGenerateInputSchema = z.object({
  maxTokens: z.number().int().nullable().optional(),
  model: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  temperature: z.number().nullable().optional(),
});

/** Zod schema for AIAiGeneratedContentDraftInput */
AIAiGeneratedContentDraftInputSchema = z.object({
  audience: z.string().nullable().optional(),
  context: z.string().nullable().optional(),
  maxTokens: z.number().int().nullable().optional(),
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  subject: z.string().nullable().optional(),
  tone: z.string().nullable().optional(),
});

/** Zod schema for AIAiGeneratedContentKind */
AIAiGeneratedContentKindSchema = z.enum(['Email', 'Report', 'ListingDescription']);

/** Zod schema for AIAiGeneratedContentInput */
AIAiGeneratedContentInputSchema = z.object({
  audience: z.string().nullable().optional(),
  context: z.string().nullable().optional(),
  kind: z.lazy(() => AIAiGeneratedContentKindSchema).optional(),
  maxTokens: z.number().int().nullable().optional(),
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  subject: z.string().nullable().optional(),
  tone: z.string().nullable().optional(),
});

/** Zod schema for AIAiPromptTemplate */
AIAiPromptTemplateSchema = z.object({
  category: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  createdByUserId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isSystemTemplate: z.boolean().optional(),
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
  updatedByUserId: z.string().uuid().nullable().optional(),
});

/** Zod schema for AIAiPromptTemplateGenerateInput */
AIAiPromptTemplateGenerateInputSchema = z.object({
  maxTokens: z.number().int().nullable().optional(),
  model: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
  temperature: z.number().nullable().optional(),
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AIAiPromptTemplateRenderInput */
AIAiPromptTemplateRenderInputSchema = z.object({
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AIAiPromptTemplateRenderOutput */
AIAiPromptTemplateRenderOutputSchema = z.object({
  key: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
  templateId: z.string().uuid().optional(),
  variables: z.record(z.string(), z.string().nullable()).nullable().optional(),
});

/** Zod schema for AIAiProviderStatus */
AIAiProviderStatusSchema = z.object({
  baseUrl: z.string().nullable().optional(),
  configured: z.boolean().optional(),
  credentialsConfigured: z.boolean().optional(),
  defaultModel: z.string().nullable().optional(),
  provider: z.string().nullable().optional(),
});

/** Zod schema for AIAiQuotaStatus */
AIAiQuotaStatusSchema = z.object({
  currentUsage: z.number().int().optional(),
  hardLimit: z.number().int().nullable().optional(),
  isActive: z.boolean().optional(),
  lastReset: z.string().datetime().nullable().optional(),
  nextReset: z.string().datetime().nullable().optional(),
  period: z.string().nullable().optional(),
  remaining: z.number().int().optional(),
  resourceType: z.string().nullable().optional(),
  softLimit: z.number().int().nullable().optional(),
  usagePercent: z.number().optional(),
});

/** Zod schema for AIAiQuotaStatusOutput */
AIAiQuotaStatusOutputSchema = z.object({
  generatedAtUtc: z.string().datetime().optional(),
  quotas: z
    .array(z.lazy(() => AIAiQuotaStatusSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for AIAiStatusOutput */
AIAiStatusOutputSchema = z.object({
  allowTenantOverrides: z.boolean().optional(),
  defaultProvider: z.string().nullable().optional(),
  enabled: z.boolean().optional(),
  providers: z
    .array(z.lazy(() => AIAiProviderStatusSchema))
    .nullable()
    .optional(),
});

/** Zod schema for AIAiUsage */
AIAiUsageSchema = z.object({
  inputTokens: z.number().int().nullable().optional(),
  outputTokens: z.number().int().nullable().optional(),
  totalTokens: z.number().int().nullable().optional(),
});

/** Zod schema for AICreateAiPromptTemplateInput */
AICreateAiPromptTemplateInputSchema = z.object({
  category: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
});

/** Zod schema for AIUpdateAiPromptTemplateInput */
AIUpdateAiPromptTemplateInputSchema = z.object({
  category: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  name: z.string().nullable().optional(),
  prompt: z.string().nullable().optional(),
  systemPrompt: z.string().nullable().optional(),
});

/** Zod schema for APIControllersApplicationDetails */
APIControllersApplicationDetailsSchema = z.object({
  description: z.string().nullable().optional(),
  informationalVersion: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for APIControllersApplicationInfoOutput */
APIControllersApplicationInfoOutputSchema = z.object({
  application: z.lazy(() => APIControllersApplicationDetailsSchema).optional(),
  build: z.lazy(() => APIControllersBuildDetailsSchema).optional(),
  process: z.lazy(() => APIControllersProcessDetailsSchema).optional(),
  runtime: z.lazy(() => APIControllersRuntimeDetailsSchema).optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for APIControllersBuildDetails */
APIControllersBuildDetailsSchema = z.object({
  configuration: z.string().nullable().optional(),
  framework: z.string().nullable().optional(),
  timestamp: z.string().datetime().nullable().optional(),
});

/** Zod schema for APIControllersDependencyHealthItem */
APIControllersDependencyHealthItemSchema = z.object({
  data: z.record(z.string(), z.string()).nullable().optional(),
  description: z.string().nullable().optional(),
  duration: z.string().optional(),
  exception: z.string().nullable().optional(),
  isHealthy: z.boolean().optional(),
  name: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for APIControllersDependencyHealthOutput */
APIControllersDependencyHealthOutputSchema = z.object({
  dependencies: z
    .array(z.lazy(() => APIControllersDependencyHealthItemSchema))
    .nullable()
    .optional(),
  error: z.string().nullable().optional(),
  healthyCount: z.number().int().optional(),
  status: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
  totalDuration: z.string().optional(),
  unhealthyCount: z.number().int().optional(),
});

/** Zod schema for APIControllersHealthinessOutput */
APIControllersHealthinessOutputSchema = z.object({
  checks: z
    .record(
      z.string(),
      z.lazy(() => APIControllersHealthinessResponseItemSchema),
    )
    .nullable()
    .optional(),
  duration: z.string().optional(),
  error: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for APIControllersHealthinessResponseItem */
APIControllersHealthinessResponseItemSchema = z.object({
  data: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  description: z.string().nullable().optional(),
  duration: z.string().optional(),
  status: z.string().nullable().optional(),
});

/** Zod schema for APIControllersLivenessOutput */
APIControllersLivenessOutputSchema = z.object({
  alive: z.boolean().optional(),
  status: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
  uptime: z.string().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for APIControllersProcessDetails */
APIControllersProcessDetailsSchema = z.object({
  startTime: z.string().datetime().optional(),
  uptime: z.string().optional(),
});

/** Zod schema for APIControllersReadinessOutput */
APIControllersReadinessOutputSchema = z.object({
  error: z.string().nullable().optional(),
  ready: z.boolean().optional(),
  services: z.record(z.string(), z.boolean()).nullable().optional(),
  status: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for APIControllersRuntimeDetails */
APIControllersRuntimeDetailsSchema = z.object({
  dotNetVersion: z.string().nullable().optional(),
  osArchitecture: z.string().nullable().optional(),
  osDescription: z.string().nullable().optional(),
  processArchitecture: z.string().nullable().optional(),
});

/** Zod schema for BillingCycle */
BillingCycleSchema = z.enum(['Weekly', 'Monthly', 'Quarterly', 'SemiAnnually', 'Annually', 'Biannually']);

/** Zod schema for BulkOperationError */
BulkOperationErrorSchema = z.object({
  errorCode: z.string().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
  tenantName: z.string().nullable().optional(),
});

/** Zod schema for BulkOperationOutput */
BulkOperationOutputSchema = z.object({
  errors: z
    .array(z.lazy(() => BulkOperationErrorSchema))
    .nullable()
    .optional(),
  failedOperations: z.number().int().optional(),
  isComplete: z.boolean().optional(),
  successRate: z.number().optional(),
  successfulOperations: z.number().int().optional(),
  totalRequested: z.number().int().optional(),
});

/** Zod schema for CQRSIDomainEvent */
CQRSIDomainEventSchema = z.object({
  eventId: z.string().uuid().optional(),
  occurredAt: z.string().datetime().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for CommerceBillingInvoicePaymentRetryResult */
CommerceBillingInvoicePaymentRetryResultSchema = z.object({
  accepted: z.boolean().optional(),
  code: z.string().nullable().optional(),
  invoiceId: z.string().uuid().optional(),
  invoiceNumber: z.string().nullable().optional(),
  invoiceStatus: z.lazy(() => CommerceBillingInvoiceStatusSchema).optional(),
  message: z.string().nullable().optional(),
  retryScheduledAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceBillingInvoiceStatus */
CommerceBillingInvoiceStatusSchema = z.enum(['Draft', 'Open', 'Paid', 'Void', 'PastDue', 'Uncollectible']);

/** Zod schema for CommerceOrderChargeState */
CommerceOrderChargeStateSchema = z.enum(['Succeeded', 'Failed', 'Processing', 'RequiresAction', 'RequiresReconciliation']);

/** Zod schema for CommerceOrdersAddOrderItemInput */
CommerceOrdersAddOrderItemInputSchema = z.object({
  productId: z.string().uuid().optional(),
  productPricingId: z.string().uuid().optional(),
  productPricingVersionId: z.string().uuid().optional(),
  promoCode: z.string().nullable().optional(),
  quantity: z.number().int().optional(),
});

/** Zod schema for CommerceOrdersCaptureOrderInput */
CommerceOrdersCaptureOrderInputSchema = z.object({
  paymentMethodId: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersCompleteOrderInput */
CommerceOrdersCompleteOrderInputSchema = z.object({
  paymentId: z.string().uuid().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersCreateOrderInput */
CommerceOrdersCreateOrderInputSchema = z.object({
  idempotencyKey: z.string().nullable().optional(),
});

/** Zod schema for CommerceOrdersOrderCapture */
CommerceOrdersOrderCaptureSchema = z.object({
  clientActionToken: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  currency: z.string().nullable().optional(),
  discountTotal: z.number().optional(),
  id: z.string().uuid().optional(),
  idempotencyKey: z.string().nullable().optional(),
  lineItems: z
    .array(z.lazy(() => CommerceOrdersOrderLineItemSchema))
    .nullable()
    .optional(),
  paidAt: z.string().datetime().nullable().optional(),
  paymentId: z.string().uuid().nullable().optional(),
  paymentMessage: z.string().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  paymentState: z.lazy(() => CommerceOrderChargeStateSchema).optional(),
  refundAmount: z.number().nullable().optional(),
  refundReason: z.string().nullable().optional(),
  refundedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => CommerceOrdersOrderStatusSchema).optional(),
  subtotal: z.number().optional(),
  taxAmount: z.number().optional(),
  total: z.number().optional(),
  updatedAt: z.string().datetime().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for CommerceOrdersOrder */
CommerceOrdersOrderSchema = z.object({
  createdAt: z.string().datetime().optional(),
  currency: z.string().nullable().optional(),
  discountTotal: z.number().optional(),
  id: z.string().uuid().optional(),
  idempotencyKey: z.string().nullable().optional(),
  lineItems: z
    .array(z.lazy(() => CommerceOrdersOrderLineItemSchema))
    .nullable()
    .optional(),
  paidAt: z.string().datetime().nullable().optional(),
  paymentMethod: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  refundAmount: z.number().nullable().optional(),
  refundReason: z.string().nullable().optional(),
  refundedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => CommerceOrdersOrderStatusSchema).optional(),
  subtotal: z.number().optional(),
  taxAmount: z.number().optional(),
  total: z.number().optional(),
  updatedAt: z.string().datetime().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for CommerceOrdersOrderLineItem */
CommerceOrdersOrderLineItemSchema = z.object({
  basePrice: z.number().optional(),
  currency: z.string().nullable().optional(),
  discountAmount: z.number().optional(),
  id: z.string().uuid().optional(),
  isSubscription: z.boolean().optional(),
  lineTotal: z.number().optional(),
  priceVersion: z.number().int().optional(),
  productId: z.string().uuid().optional(),
  productName: z.string().nullable().optional(),
  productPricingId: z.string().uuid().optional(),
  productPricingVersionId: z.string().uuid().optional(),
  promoCodesApplied: z.string().nullable().optional(),
  quantity: z.number().int().optional(),
  salePrice: z.number().nullable().optional(),
  unitPrice: z.number().optional(),
});

/** Zod schema for CommerceOrdersOrderStatus */
CommerceOrdersOrderStatusSchema = z.enum([
  'Pending',
  'Processing',
  'Completed',
  'Failed',
  'Cancelled',
  'Refunded',
  'PartiallyRefunded',
  'Disputed',
  'Paid',
  'Fulfilled',
  'OnHold',
]);

/** Zod schema for CommercePaymentsBillingChargesControllerCancelBillingChargeInput */
CommercePaymentsBillingChargesControllerCancelBillingChargeInputSchema = z.object({
  canceledBy: z.string().uuid().nullable().optional(),
  cancellationReason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsBillingChargesControllerCreateBillingChargeInput */
CommercePaymentsBillingChargesControllerCreateBillingChargeInputSchema = z.object({
  amount: z.number().optional(),
  paymentMethodId: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommercePaymentsBillingChargesControllerRefundBillingChargeInput */
CommercePaymentsBillingChargesControllerRefundBillingChargeInputSchema = z.object({
  amount: z.number().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsCalculateTaxInput */
CommercePaymentsCalculateTaxInputSchema = z.object({
  amount: z.number(),
  applicableExemptions: z.array(z.string()).nullable().optional(),
  currency: z.string().nullable(),
  customerType: z.string().nullable(),
  customerVatNumber: z.string().nullable().optional(),
  isTaxInclusive: z.boolean().optional(),
  jurisdictionCode: z.string().nullable(),
  productCategory: z.string().nullable().optional(),
  transactionDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommercePaymentsCreateTaxJurisdictionInput */
CommercePaymentsCreateTaxJurisdictionInputSchema = z.object({
  code: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  defaultRate: z.number().optional(),
  name: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsCreateTaxRuleInput */
CommercePaymentsCreateTaxRuleInputSchema = z.object({
  customerType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  productCategory: z.string().nullable().optional(),
  rate: z.number().optional(),
});

/** Zod schema for CommercePaymentsCreateWalletInput */
CommercePaymentsCreateWalletInputSchema = z.object({
  currency: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsCustomerType */
CommercePaymentsCustomerTypeSchema = z.enum(['B2C', 'B2B']);

/** Zod schema for CommercePaymentsLockWalletInput */
CommercePaymentsLockWalletInputSchema = z.object({
  reason: z.string().nullable(),
});

/** Zod schema for CommercePaymentsModelsFreezeWalletInput */
CommercePaymentsModelsFreezeWalletInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsModelsPatchWalletInput */
CommercePaymentsModelsPatchWalletInputSchema = z.object({
  currency: z.string().nullable().optional(),
  dailyLimit: z.number().nullable().optional(),
  monthlyLimit: z.number().nullable().optional(),
});

/** Zod schema for CommercePaymentsPatchTaxJurisdictionInput */
CommercePaymentsPatchTaxJurisdictionInputSchema = z.object({
  defaultRate: z.number().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  name: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPatchTaxRuleInput */
CommercePaymentsPatchTaxRuleInputSchema = z.object({
  description: z.string().nullable().optional(),
  effectiveFrom: z.string().datetime().nullable().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  rate: z.number().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentCancellationResult */
CommercePaymentsPaymentCancellationResultSchema = z.object({
  canceledAt: z.string().datetime(),
  canceledBy: z.string().uuid().nullable().optional(),
  cancellationReason: z.string().nullable(),
  errorMessage: z.string().nullable().optional(),
  paymentId: z.string().uuid(),
  refundAmount: z.number().nullable().optional(),
  refundProcessed: z.boolean().optional(),
  success: z.boolean(),
});

/** Zod schema for CommercePaymentsPaymentResult */
CommercePaymentsPaymentResultSchema = z.object({
  amount: z.lazy(() => MoneySchema).optional(),
  failureReason: z.string().nullable().optional(),
  invoiceId: z.string().uuid().nullable().optional(),
  paymentId: z.string().nullable().optional(),
  paymentMethodId: z.string().nullable().optional(),
  processedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => CommercePaymentsPaymentStatusSchema).optional(),
  success: z.boolean().optional(),
  tenantId: z.string().uuid().optional(),
  transactionId: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentRetryResult */
CommercePaymentsPaymentRetryResultSchema = z.object({
  failureReason: z.string().nullable().optional(),
  maxRetriesReached: z.boolean().optional(),
  nextRetryAt: z.string().datetime().nullable().optional(),
  paymentResult: z.lazy(() => CommercePaymentsPaymentResultSchema).optional(),
  retryAttempt: z.number().int().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for CommercePaymentsPaymentStatus */
CommercePaymentsPaymentStatusSchema = z.enum(['Pending', 'Processing', 'Succeeded', 'Failed', 'Cancelled', 'RequiresAction', 'Refunded', 'Disputed']);

/** Zod schema for CommercePaymentsPaymentsControllerCancelPaymentInput */
CommercePaymentsPaymentsControllerCancelPaymentInputSchema = z.object({
  canceledBy: z.string().uuid().nullable().optional(),
  cancellationReason: z.string().nullable().optional(),
  notes: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInput */
CommercePaymentsPaymentsControllerCompleteSubscriptionCheckoutInputSchema = z.object({
  paymentMethodId: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCreateSetupIntentInput */
CommercePaymentsPaymentsControllerCreateSetupIntentInputSchema = z.object({
  customerEmail: z.string().nullable().optional(),
  customerName: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerCreateSetupIntentOutput */
CommercePaymentsPaymentsControllerCreateSetupIntentOutputSchema = z.object({
  clientSecret: z.string().nullable().optional(),
  customerId: z.string().nullable().optional(),
  setupIntentId: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerProcessPaymentInput */
CommercePaymentsPaymentsControllerProcessPaymentInputSchema = z.object({
  amount: z.number().optional(),
  paymentMethodId: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommercePaymentsPaymentsControllerRefundInput */
CommercePaymentsPaymentsControllerRefundInputSchema = z.object({
  amount: z.number().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsProcessRefundResult */
CommercePaymentsProcessRefundResultSchema = z.object({
  currency: z.string().nullable(),
  errorMessage: z.string().nullable().optional(),
  estimatedCompletionDate: z.string().datetime().nullable().optional(),
  isSuccess: z.boolean().optional(),
  isSuccessful: z.boolean().optional(),
  paymentId: z.string().uuid(),
  processedAt: z.string().datetime(),
  processingFee: z.number().optional(),
  reason: z.string().nullable(),
  referenceNumber: z.string().nullable().optional(),
  refundId: z.string().uuid(),
  refundedAmount: z.number(),
  status: z.lazy(() => CommercePaymentsTransactionStatusSchema),
});

/** Zod schema for CommercePaymentsTaxBreakdown */
CommercePaymentsTaxBreakdownSchema = z.object({
  description: z.string().nullable().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  rate: z.number().optional(),
  taxAmount: z.number().optional(),
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  taxableAmount: z.number().optional(),
});

/** Zod schema for CommercePaymentsTaxCalculationResult */
CommercePaymentsTaxCalculationResultSchema = z.object({
  effectiveTaxRate: z.number().optional(),
  exemptionReason: z.string().nullable().optional(),
  isReverseCharge: z.boolean().optional(),
  isTaxExempt: z.boolean().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  jurisdictionName: z.string().nullable().optional(),
  subtotalAmount: z.number().optional(),
  taxAmount: z.number().optional(),
  taxBreakdowns: z
    .array(z.lazy(() => CommercePaymentsTaxBreakdownSchema))
    .nullable()
    .optional(),
  taxDescription: z.string().nullable().optional(),
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  totalAmount: z.number().optional(),
});

/** Zod schema for CommercePaymentsTaxExemptionValidationResult */
CommercePaymentsTaxExemptionValidationResultSchema = z.object({
  exemptionRate: z.number().optional(),
  exemptionType: z.string().nullable().optional(),
  isValid: z.boolean().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validTo: z.string().datetime().nullable().optional(),
  validationMessage: z.string().nullable().optional(),
  warnings: z.array(z.string()).nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdiction */
CommercePaymentsTaxJurisdictionSchema = z.object({
  childJurisdictions: z
    .array(z.lazy(() => CommercePaymentsTaxJurisdictionSchema))
    .nullable()
    .optional(),
  code: z.string().min(1).max(20),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isReverseChargeApplicable: z.boolean().optional(),
  name: z.string().min(1).max(200),
  parentJurisdiction: z.lazy(() => CommercePaymentsTaxJurisdictionSchema).optional(),
  parentJurisdictionId: z.string().uuid().nullable().optional(),
  taxRegistrationNumber: z.string().max(100).nullable().optional(),
  taxRules: z
    .array(z.lazy(() => CommercePaymentsTaxRuleSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => CommercePaymentsTaxJurisdictionTypeSchema).optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdictionDto */
CommercePaymentsTaxJurisdictionDtoSchema = z.object({
  code: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  defaultRate: z.number().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  name: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  taxType: z.string().nullable().optional(),
});

/** Zod schema for CommercePaymentsTaxJurisdictionType */
CommercePaymentsTaxJurisdictionTypeSchema = z.enum(['Country', 'State', 'Province', 'Region', 'City', 'County', 'District']);

/** Zod schema for CommercePaymentsTaxRate */
CommercePaymentsTaxRateSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  maximumTaxableAmount: z.number().nullable().optional(),
  minimumTaxableAmount: z.number().nullable().optional(),
  productCategory: z.string().max(100).nullable().optional(),
  rate: z.number().optional(),
  taxJurisdiction: z.lazy(() => CommercePaymentsTaxJurisdictionSchema).optional(),
  taxJurisdictionId: z.string().uuid(),
  taxType: z.lazy(() => CommercePaymentsTaxTypeSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for CommercePaymentsTaxRule */
CommercePaymentsTaxRuleSchema = z.object({
  createdAt: z.string().datetime(),
  customerTypeFilter: z.lazy(() => CommercePaymentsCustomerTypeSchema).optional(),
  defaultTaxRate: z.lazy(() => CommercePaymentsTaxRateSchema).optional(),
  defaultTaxRateId: z.string().uuid().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(1000).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  effectiveFrom: z.string().datetime().nullable().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  exemptionConditions: z.string().max(2000).nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isReverseCharge: z.boolean().optional(),
  isTaxInclusive: z.boolean().optional(),
  maximumAmount: z.number().nullable().optional(),
  minimumAmount: z.number().nullable().optional(),
  name: z.string().min(1).max(200),
  priority: z.number().int().optional(),
  productCategories: z.string().max(2000).nullable().optional(),
  ruleType: z.lazy(() => CommercePaymentsTaxRuleTypeSchema).optional(),
  taxJurisdiction: z.lazy(() => CommercePaymentsTaxJurisdictionSchema).optional(),
  taxJurisdictionId: z.string().uuid(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for CommercePaymentsTaxRuleDto */
CommercePaymentsTaxRuleDtoSchema = z.object({
  customerType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  effectiveFrom: z.string().datetime().optional(),
  effectiveTo: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  productCategory: z.string().nullable().optional(),
  rate: z.number().optional(),
});

/** Zod schema for CommercePaymentsTaxRuleType */
CommercePaymentsTaxRuleTypeSchema = z.enum(['Standard', 'Reduced', 'ZeroRated', 'Exempt', 'ReverseCharge', 'WithholdingTax', 'Compound', 'Custom']);

/** Zod schema for CommercePaymentsTaxType */
CommercePaymentsTaxTypeSchema = z.enum(['VAT', 'GST', 'SalesTax', 'ServiceTax', 'WithholdingTax', 'ExciseTax', 'CustomsDuty', 'Other']);

/** Zod schema for CommercePaymentsTransactionStatus */
CommercePaymentsTransactionStatusSchema = z.enum(['Pending', 'Processing', 'Completed', 'Failed', 'Cancelled', 'Reversed']);

/** Zod schema for CommercePaymentsUserWallet */
CommercePaymentsUserWalletSchema = z.object({
  balance: z.number().optional(),
  createdAt: z.string().datetime(),
  currency: z.string().min(1).max(3),
  dailyLimit: z.number().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isLocked: z.boolean().optional(),
  isNew: z.boolean().optional(),
  lastTransactionAt: z.string().datetime().nullable().optional(),
  lockReason: z.string().max(500).nullable().optional(),
  monthlyLimit: z.number().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  transactions: z
    .array(z.lazy(() => CommercePaymentsWalletTransactionSchema))
    .nullable()
    .optional(),
  updatedAt: z.string().datetime(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for CommercePaymentsValidateTaxExemptionInput */
CommercePaymentsValidateTaxExemptionInputSchema = z.object({
  customerId: z.string().uuid().nullable().optional(),
  customerVatNumber: z.string().nullable().optional(),
  exemptionCertificateNumber: z.string().nullable().optional(),
  exemptionType: z.string().nullable().optional(),
  jurisdictionCode: z.string().nullable().optional(),
  transactionDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommercePaymentsWalletTransaction */
CommercePaymentsWalletTransactionSchema = z.object({
  amount: z.number().optional(),
  balanceAfter: z.number().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().min(1).max(500),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  metadata: z.string().max(2000).nullable().optional(),
  notes: z.string().max(1000).nullable().optional(),
  processedAt: z.string().datetime().nullable().optional(),
  referenceId: z.string().max(200).nullable().optional(),
  status: z.lazy(() => CommercePaymentsTransactionStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => CommercePaymentsWalletTransactionTypeSchema).optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  wallet: z.lazy(() => CommercePaymentsUserWalletSchema).optional(),
  walletId: z.string().uuid(),
});

/** Zod schema for CommercePaymentsWalletTransactionType */
CommercePaymentsWalletTransactionTypeSchema = z.enum(['Credit', 'Debit', 'TransferIn', 'TransferOut', 'Refund', 'Fee', 'Adjustment']);

/** Zod schema for CommerceProductsAddSupportTicketMessageInput */
CommerceProductsAddSupportTicketMessageInputSchema = z.object({
  authorEmail: z.string().nullable().optional(),
  authorName: z.string().nullable().optional(),
  authorType: z.lazy(() => CommerceProductsSupportTicketMessageAuthorTypeSchema).optional(),
  authorUserId: z.string().uuid().optional(),
  body: z.string().nullable().optional(),
  isInternal: z.boolean().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsAppliedPromoCode */
CommerceProductsAppliedPromoCodeSchema = z.object({
  code: z.string().nullable().optional(),
  discountAmount: z.number().optional(),
  discountPercentage: z.number().nullable().optional(),
});

/** Zod schema for CommerceProductsApplyPromoCodesInput */
CommerceProductsApplyPromoCodesInputSchema = z.object({
  orderAmount: z.number().optional(),
  productId: z.string().uuid().nullable().optional(),
  promoCodes: z.array(z.string()).nullable().optional(),
});

/** Zod schema for CommerceProductsAssignSupportTicketInput */
CommerceProductsAssignSupportTicketInputSchema = z.object({
  agentName: z.string().nullable().optional(),
  agentUserId: z.string().uuid().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsBatchCreateProductsInput */
CommerceProductsBatchCreateProductsInputSchema = z.object({
  products: z
    .array(z.lazy(() => CommerceProductsBatchProductCreateItemSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsBatchProductCreateItem */
CommerceProductsBatchProductCreateItemSchema = z.object({
  affiliateCommissionPercentage: z.number().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isBundle: z.boolean().optional(),
  maxAffiliateDiscount: z.number().optional(),
  name: z.string().nullable().optional(),
  referralCommissionPercentage: z.number().optional(),
  shortDescription: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
});

/** Zod schema for CommerceProductsCheckMultipleAccessInput */
CommerceProductsCheckMultipleAccessInputSchema = z.object({
  productIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for CommerceProductsCloseSupportTicketInput */
CommerceProductsCloseSupportTicketInputSchema = z.object({
  agentName: z.string().nullable().optional(),
  agentUserId: z.string().uuid().optional(),
  closingNotes: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsCreateProductInput */
CommerceProductsCreateProductInputSchema = z.object({
  affiliateCommissionPercentage: z.number().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isBundle: z.boolean().optional(),
  maxAffiliateDiscount: z.number().optional(),
  name: z.string().nullable().optional(),
  referralCommissionPercentage: z.number().optional(),
  shortDescription: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
});

/** Zod schema for CommerceProductsCreatePromoCodeInput */
CommerceProductsCreatePromoCodeInputSchema = z.object({
  code: z.string().nullable().optional(),
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  discountPercentage: z.number().nullable().optional(),
  isActive: z.boolean().optional(),
  isExclusive: z.boolean().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
  stackingPriority: z.number().int().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsCreateSupportTicketInput */
CommerceProductsCreateSupportTicketInputSchema = z.object({
  body: z.string().nullable().optional(),
  category: z.string().nullable().optional(),
  customerId: z.string().uuid().optional(),
  customerName: z.string().nullable().optional(),
  priority: z.lazy(() => CommerceProductsSupportTicketPrioritySchema).optional(),
  reporterEmail: z.string().nullable().optional(),
  reporterName: z.string().nullable().optional(),
  reporterUserId: z.string().uuid().optional(),
  subject: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsEntitlementCheckResult */
CommerceProductsEntitlementCheckResultSchema = z.object({
  hasAccess: z.boolean().optional(),
  productId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsEntitlementInfo */
CommerceProductsEntitlementInfoSchema = z.object({
  accessEndDate: z.string().datetime().nullable().optional(),
  accessStartDate: z.string().datetime().nullable().optional(),
  acquisitionType: z.string().nullable().optional(),
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().optional(),
  pricePaid: z.number().optional(),
  productId: z.string().uuid().optional(),
  productName: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  subscriptionStatus: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsGrantEntitlementInput */
CommerceProductsGrantEntitlementInputSchema = z.object({
  acquisitionType: z.lazy(() => CommerceProductsProductAcquisitionTypeSchema).optional(),
  currency: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  pricePaid: z.number().optional(),
  productId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsPatchProductInput */
CommerceProductsPatchProductInputSchema = z.object({
  affiliateCommissionPercentage: z.number().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  description: z.string().nullable().optional(),
  expectedVersion: z.number().int().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isBundle: z.boolean().nullable().optional(),
  maxAffiliateDiscount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  referralCommissionPercentage: z.number().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
});

/** Zod schema for CommerceProductsPatchPromoCodeInput */
CommerceProductsPatchPromoCodeInputSchema = z.object({
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  discountPercentage: z.number().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  isExclusive: z.boolean().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
  stackingPriority: z.number().int().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsProductAcquisitionType */
CommerceProductsProductAcquisitionTypeSchema = z.enum(['Purchase', 'Subscription', 'Grant', 'PromoCode', 'Bundle', 'Trial', 'Referral', 'Free', 'Gift']);

/** Zod schema for CommerceProductsProduct */
CommerceProductsProductSchema = z.object({
  affiliateCommissionPercentage: z.number().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  createdAt: z.string().datetime().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isBundle: z.boolean().optional(),
  isPublished: z.boolean().optional(),
  maxAffiliateDiscount: z.number().optional(),
  name: z.string().nullable().optional(),
  pricing: z
    .array(z.lazy(() => CommerceProductsProductPricingSchema))
    .nullable()
    .optional(),
  referralCommissionPercentage: z.number().optional(),
  shortDescription: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for CommerceProductsProductPricing */
CommerceProductsProductPricingSchema = z.object({
  basePrice: z.number().optional(),
  currency: z.string().nullable().optional(),
  currentPrice: z.number().optional(),
  id: z.string().uuid().optional(),
  isDefault: z.boolean().optional(),
  isSaleActive: z.boolean().optional(),
  name: z.string().nullable().optional(),
  productId: z.string().uuid().optional(),
  saleEndDate: z.string().datetime().nullable().optional(),
  salePrice: z.number().nullable().optional(),
  saleStartDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsProductType */
CommerceProductsProductTypeSchema = z.enum([
  'Program',
  'Course',
  'Bundle',
  'Subscription',
  'Workshop',
  'Mentorship',
  'Ebook',
  'ResourcePack',
  'Community',
  'Certification',
  'Physical',
  'Service',
  'LearningPathway',
  'Other',
]);

/** Zod schema for CommerceProductsPromoCodeApplicationResult */
CommerceProductsPromoCodeApplicationResultSchema = z.object({
  appliedCodes: z
    .array(z.lazy(() => CommerceProductsAppliedPromoCodeSchema))
    .nullable()
    .optional(),
  finalAmount: z.number().optional(),
  originalAmount: z.number().optional(),
  rejectedCodes: z
    .array(z.lazy(() => CommerceProductsRejectedPromoCodeSchema))
    .nullable()
    .optional(),
  totalDiscount: z.number().optional(),
});

/** Zod schema for CommerceProductsPromoCode */
CommerceProductsPromoCodeSchema = z.object({
  code: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  discountPercentage: z.number().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isExclusive: z.boolean().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
  stackingPriority: z.number().int().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
  usageCount: z.number().int().optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsPromoCodeType */
CommerceProductsPromoCodeTypeSchema = z.enum(['PercentageOff', 'FixedAmountOff', 'FreeTrial', 'BuyOneGetOne', 'FreeShipping']);

/** Zod schema for CommerceProductsPromoCodeUsage */
CommerceProductsPromoCodeUsageSchema = z.object({
  averageDiscountPerUse: z.number().optional(),
  code: z.string().nullable().optional(),
  firstUsedAt: z.string().datetime().nullable().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  promoCodeId: z.string().uuid().optional(),
  remainingUses: z.number().int().nullable().optional(),
  totalDiscountGiven: z.number().optional(),
  totalUses: z.number().int().optional(),
  uniqueUsers: z.number().int().optional(),
});

/** Zod schema for CommerceProductsPromoCodeValidationResult */
CommerceProductsPromoCodeValidationResultSchema = z.object({
  code: z.string().nullable().optional(),
  discountAmount: z.number().optional(),
  discountPercentage: z.number().nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  isValid: z.boolean().optional(),
});

/** Zod schema for CommerceProductsRejectedPromoCode */
CommerceProductsRejectedPromoCodeSchema = z.object({
  code: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceProductsResolveSupportTicketInput */
CommerceProductsResolveSupportTicketInputSchema = z.object({
  agentName: z.string().nullable().optional(),
  agentUserId: z.string().uuid().optional(),
  resolutionSummary: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsRevokeEntitlementInput */
CommerceProductsRevokeEntitlementInputSchema = z.object({
  productId: z.string().uuid().optional(),
  reason: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsSupportTicket */
CommerceProductsSupportTicketSchema = z.object({
  assignedToName: z.string().nullable().optional(),
  assignedToUserId: z.string().uuid().nullable().optional(),
  category: z.string().nullable().optional(),
  closedAt: z.string().datetime().nullable().optional(),
  customerId: z.string().uuid().optional(),
  customerName: z.string().nullable().optional(),
  firstResponseAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  lastMessageAt: z.string().datetime().nullable().optional(),
  lastMessagePreview: z.string().nullable().optional(),
  messageCount: z.number().int().optional(),
  messages: z
    .array(z.lazy(() => CommerceProductsSupportTicketMessageSchema))
    .nullable()
    .optional(),
  openedAt: z.string().datetime().optional(),
  priority: z.lazy(() => CommerceProductsSupportTicketPrioritySchema).optional(),
  reporterEmail: z.string().nullable().optional(),
  reporterName: z.string().nullable().optional(),
  reporterUserId: z.string().uuid().optional(),
  resolutionSummary: z.string().nullable().optional(),
  resolvedAt: z.string().datetime().nullable().optional(),
  responseDueBy: z.string().datetime().nullable().optional(),
  status: z.lazy(() => CommerceProductsSupportTicketStatusSchema).optional(),
  subject: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceProductsSupportTicketMessageAuthorType */
CommerceProductsSupportTicketMessageAuthorTypeSchema = z.enum(['Customer', 'Agent', 'System']);

/** Zod schema for CommerceProductsSupportTicketMessage */
CommerceProductsSupportTicketMessageSchema = z.object({
  authorEmail: z.string().nullable().optional(),
  authorName: z.string().nullable().optional(),
  authorType: z.lazy(() => CommerceProductsSupportTicketMessageAuthorTypeSchema).optional(),
  authorUserId: z.string().uuid().optional(),
  body: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isInternal: z.boolean().optional(),
  ticketId: z.string().uuid().optional(),
});

/** Zod schema for CommerceProductsSupportTicketPriority */
CommerceProductsSupportTicketPrioritySchema = z.enum(['Low', 'Normal', 'High', 'Urgent']);

/** Zod schema for CommerceProductsSupportTicketStatus */
CommerceProductsSupportTicketStatusSchema = z.enum(['Open', 'InProgress', 'Resolved', 'Closed', 'Cancelled']);

/** Zod schema for CommerceProductsUpdateProductInput */
CommerceProductsUpdateProductInputSchema = z.object({
  affiliateCommissionPercentage: z.number().nullable().optional(),
  bundleItems: z.array(z.string().uuid()).nullable().optional(),
  description: z.string().nullable().optional(),
  expectedVersion: z.number().int().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isBundle: z.boolean().nullable().optional(),
  maxAffiliateDiscount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  referralCommissionPercentage: z.number().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  type: z.lazy(() => CommerceProductsProductTypeSchema).optional(),
});

/** Zod schema for CommerceProductsUpdatePromoCodeInput */
CommerceProductsUpdatePromoCodeInputSchema = z.object({
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  discountAmount: z.number().nullable().optional(),
  discountPercentage: z.number().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  isExclusive: z.boolean().nullable().optional(),
  maxUses: z.number().int().nullable().optional(),
  maxUsesPerUser: z.number().int().nullable().optional(),
  minimumOrderAmount: z.number().nullable().optional(),
  name: z.string().nullable().optional(),
  productId: z.string().uuid().nullable().optional(),
  stackingPriority: z.number().int().nullable().optional(),
  type: z.lazy(() => CommerceProductsPromoCodeTypeSchema).optional(),
  validFrom: z.string().datetime().nullable().optional(),
  validUntil: z.string().datetime().nullable().optional(),
});

/** Zod schema for CommerceProductsValidatePromoCodeInput */
CommerceProductsValidatePromoCodeInputSchema = z.object({
  code: z.string().nullable().optional(),
  orderAmount: z.number().optional(),
  productId: z.string().uuid().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsBillingHistory */
CommerceSubscriptionsBillingHistorySchema = z.object({
  amount: z.number().optional(),
  billingDate: z.string().datetime().optional(),
  createdAt: z.string().datetime().optional(),
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  externalPaymentId: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  status: z.string().nullable().optional(),
  subscriptionId: z.string().uuid().optional(),
});

/** Zod schema for CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInput */
CommerceSubscriptionsBillingSubscriptionsControllerCancelBillingSubscriptionInputSchema = z.object({
  effectiveDate: z.string().datetime().nullable().optional(),
  note: z.string().nullable().optional(),
  reason: z.lazy(() => CommerceSubscriptionsCancellationReasonSchema).optional(),
});

/** Zod schema for CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInput */
CommerceSubscriptionsBillingSubscriptionsControllerCreateBillingSubscriptionInputSchema = z.object({
  amount: z.number().optional(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  createdByUserId: z.string().uuid().optional(),
  fulfilledOrderId: z.string().uuid().nullable().optional(),
  planId: z.string().uuid().optional(),
  startDate: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().optional(),
  trialDays: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsCancellationReason */
CommerceSubscriptionsCancellationReasonSchema = z.enum([
  'UserRequested',
  'PaymentFailed',
  'PlanDiscontinued',
  'PolicyViolation',
  'Downgrade',
  'TrialEnded',
  'Custom',
  'ExternalRequest',
]);

/** Zod schema for CommerceSubscriptionsClientModulesOutput */
CommerceSubscriptionsClientModulesOutputSchema = z.object({
  clientId: z.string().uuid().optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  subscriptions: z.lazy(() => PagedResultOfGameGuildCommerceSubscriptionsSubscriptionSchema).optional(),
});

/** Zod schema for CommerceSubscriptionsCreateClientInput */
CommerceSubscriptionsCreateClientInputSchema = z.object({
  adminEmail: z.string().nullable().optional(),
  cnpj: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  fiscalData: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  taxId: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscription */
CommerceSubscriptionsSubscriptionSchema = z.object({
  amount: z.lazy(() => MoneySchema).optional(),
  autoRenew: z.boolean().optional(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  billingCycleCount: z.number().int().optional(),
  cancellationNote: z.string().max(1000).nullable().optional(),
  cancellationReason: z.lazy(() => CommerceSubscriptionsCancellationReasonSchema).optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime(),
  createdByUserId: z.string().uuid(),
  currentPeriodEnd: z.string().datetime().optional(),
  currentPeriodStart: z.string().datetime().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  endDate: z.string().datetime().nullable().optional(),
  externalCustomerId: z.string().max(100).nullable().optional(),
  externalId: z.string().max(100).nullable().optional(),
  fulfilledOrderId: z.string().uuid().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isCancelled: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isTrialing: z.boolean().optional(),
  lastModifyingOrderId: z.string().uuid().nullable().optional(),
  lastPaymentAt: z.string().datetime().nullable().optional(),
  lastPaymentIdempotencyKey: z.string().max(100).nullable().optional(),
  lastProcessedBillingCycle: z.number().int().optional(),
  lastRenewalIdempotencyKey: z.string().max(100).nullable().optional(),
  lockedPriceVersionId: z.string().uuid().nullable().optional(),
  metadata: z.string().max(2000).nullable().optional(),
  nextBillingDate: z.string().datetime().optional(),
  plan: z.lazy(() => CommerceSubscriptionsSubscriptionPlanSchema).optional(),
  planId: z.string().uuid(),
  rowVersion: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  status: z.lazy(() => CommerceSubscriptionsSubscriptionStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  trialEndDate: z.string().datetime().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionChurnReport */
CommerceSubscriptionsSubscriptionChurnReportSchema = z.object({
  activeSubscriptions: z.number().int().optional(),
  cancelledInPeriod: z.number().int().optional(),
  churnRate: z.number().optional(),
  endDate: z.string().datetime().optional(),
  generatedAt: z.string().datetime().optional(),
  monthlyRecurringRevenue: z.number().optional(),
  retentionRate: z.number().optional(),
  startDate: z.string().datetime().optional(),
  statusBreakdown: z.record(z.string(), z.number().int()).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  totalSubscriptions: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionDowngradeResult */
CommerceSubscriptionsSubscriptionDowngradeResultSchema = z.object({
  creditIssued: z.lazy(() => MoneySchema).optional(),
  effectiveDate: z.string().datetime().nullable().optional(),
  failureReason: z.string().nullable().optional(),
  success: z.boolean().optional(),
  updatedSubscription: z.lazy(() => CommerceSubscriptionsSubscriptionSchema).optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInput */
CommerceSubscriptionsSubscriptionLifecycleControllerAutoRenewInputSchema = z.object({
  autoRenew: z.boolean().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerCancelInput */
CommerceSubscriptionsSubscriptionLifecycleControllerCancelInputSchema = z.object({
  effectiveDate: z.string().datetime().nullable().optional(),
  note: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInput */
CommerceSubscriptionsSubscriptionLifecycleControllerDowngradeInputSchema = z.object({
  effectiveDate: z.string().datetime().nullable().optional(),
  newPlanId: z.string().uuid().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInput */
CommerceSubscriptionsSubscriptionLifecycleControllerEndTrialInputSchema = z.object({
  convertToPaid: z.boolean().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInput */
CommerceSubscriptionsSubscriptionLifecycleControllerExternalIdsInputSchema = z.object({
  externalCustomerId: z.string().nullable().optional(),
  externalSubscriptionId: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInput */
CommerceSubscriptionsSubscriptionLifecycleControllerPauseSubscriptionInputSchema = z.object({
  pauseUntil: z.string().datetime().nullable().optional(),
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInput */
CommerceSubscriptionsSubscriptionLifecycleControllerStartTrialInputSchema = z.object({
  trialDays: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInput */
CommerceSubscriptionsSubscriptionLifecycleControllerSuspendInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInput */
CommerceSubscriptionsSubscriptionLifecycleControllerUpgradeInputSchema = z.object({
  effectiveDate: z.string().datetime().nullable().optional(),
  newPlanId: z.string().uuid().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionNotification */
CommerceSubscriptionsSubscriptionNotificationSchema = z.object({
  channel: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isSent: z.boolean().optional(),
  message: z.string().nullable().optional(),
  recipientId: z.string().uuid().optional(),
  sentAt: z.string().datetime().nullable().optional(),
  subscriptionId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInput */
CommerceSubscriptionsSubscriptionNotificationsControllerResendSubscriptionNotificationInputSchema = z.object({
  channel: z.lazy(() => NotificationsNotificationChannelSchema).optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlan */
CommerceSubscriptionsSubscriptionPlanSchema = z.object({
  annualPriceInCents: z.number().int().nullable().optional(),
  createdAt: z.string().datetime(),
  currency: z.string().min(1).max(3),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(1000).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  externalId: z.string().max(100).nullable().optional(),
  features: z.string().max(2000).nullable().optional(),
  hasAdvancedAnalytics: z.boolean().optional(),
  hasCustomBranding: z.boolean().optional(),
  hasPrioritySupport: z.boolean().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  maxUsers: z.number().int().nullable().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  monthlyPriceInCents: z.number().int().optional(),
  name: z.string().min(1).max(100),
  slug: z.string().min(1).max(50),
  sortOrder: z.number().int().optional(),
  subscriptions: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  trialPeriodDays: z.number().int().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerCloneSubscriptionPlanInputSchema = z.object({
  newName: z.string().nullable().optional(),
  newSlug: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerSetExternalIdInputSchema = z.object({
  externalId: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerSetFeaturedInputSchema = z.object({
  featured: z.boolean().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateDetailsInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  planId: z.string().uuid().optional(),
  sortOrder: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateFeaturesInputSchema = z.object({
  features: z.string().nullable().optional(),
  hasAdvancedAnalytics: z.boolean().nullable().optional(),
  hasCustomBranding: z.boolean().nullable().optional(),
  hasPrioritySupport: z.boolean().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdateLimitsInputSchema = z.object({
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  maxUsers: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerUpdatePricingInputSchema = z.object({
  annualPriceInCents: z.number().int().nullable().optional(),
  monthlyPriceInCents: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInput */
CommerceSubscriptionsSubscriptionPlanOperationsControllerValidateLimitsInputSchema = z.object({
  apiCalls: z.number().int().optional(),
  storageMb: z.number().int().optional(),
  users: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerComparePlansInputSchema = z.object({
  basePlanId: z.string().uuid().optional(),
  comparePlanIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerCreatePlanInputSchema = z.object({
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  monthlyPriceInCents: z.number().int().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInput */
CommerceSubscriptionsSubscriptionPlansCrudControllerPutSubscriptionPlanInputSchema = z.object({
  annualPriceInCents: z.number().int().nullable().optional(),
  description: z.string().nullable().optional(),
  features: z.string().nullable().optional(),
  hasAdvancedAnalytics: z.boolean().nullable().optional(),
  hasCustomBranding: z.boolean().nullable().optional(),
  hasPrioritySupport: z.boolean().nullable().optional(),
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  maxUsers: z.number().int().nullable().optional(),
  monthlyPriceInCents: z.number().int().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionStatus */
CommerceSubscriptionsSubscriptionStatusSchema = z.enum(['PendingActivation', 'Active', 'Trialing', 'PastDue', 'Suspended', 'Cancelled', 'Expired']);

/** Zod schema for CommerceSubscriptionsSubscriptionUpgradeResult */
CommerceSubscriptionsSubscriptionUpgradeResultSchema = z.object({
  creditApplied: z.lazy(() => MoneySchema).optional(),
  failureReason: z.string().nullable().optional(),
  proratedAmount: z.lazy(() => MoneySchema).optional(),
  success: z.boolean().optional(),
  updatedSubscription: z.lazy(() => CommerceSubscriptionsSubscriptionSchema).optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionUsage */
CommerceSubscriptionsSubscriptionUsageSchema = z.object({
  apiCallsThisMonth: z.number().int().optional(),
  isOverLimit: z.boolean().optional(),
  limitWarnings: z.array(z.string()).nullable().optional(),
  maxApiCallsPerMonth: z.number().int().nullable().optional(),
  maxStorageMb: z.number().int().nullable().optional(),
  maxUsers: z.number().int().nullable().optional(),
  storageUsedMb: z.number().int().optional(),
  subscriptionId: z.string().uuid().optional(),
  usersCount: z.number().int().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerCreateSubscriptionInputSchema = z.object({
  amount: z.number().optional(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  createdByUserId: z.string().uuid().optional(),
  currency: z.string().nullable().optional(),
  fulfilledOrderId: z.string().uuid().nullable().optional(),
  planId: z.string().uuid().optional(),
  startDate: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().optional(),
  trialDays: z.number().int().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerPatchSubscriptionInputSchema = z.object({
  autoRenew: z.boolean().nullable().optional(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  externalCustomerId: z.string().nullable().optional(),
  externalSubscriptionId: z.string().nullable().optional(),
  metadata: z.string().nullable().optional(),
});

/** Zod schema for CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInput */
CommerceSubscriptionsSubscriptionsControllerPutSubscriptionInputSchema = z.object({
  amount: z.number().optional(),
  autoRenew: z.boolean().optional(),
  billingCycle: z.lazy(() => BillingCycleSchema).optional(),
  externalCustomerId: z.string().nullable().optional(),
  externalSubscriptionId: z.string().nullable().optional(),
  planId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPACompleteFerpaInspectionRequestBody */
ComplianceFERPACompleteFerpaInspectionRequestBodySchema = z.object({
  approved: z.boolean().optional(),
  notes: z.string().nullable().optional(),
  processedByUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPAEducationRecordKind */
ComplianceFERPAEducationRecordKindSchema = z.enum([
  'CourseEnrollment',
  'AssessmentSubmission',
  'Grade',
  'Certificate',
  'Attendance',
  'Communication',
  'SupportCase',
  'Custom',
]);

/** Zod schema for ComplianceFERPAFerpaDirectoryInformationPolicy */
ComplianceFERPAFerpaDirectoryInformationPolicySchema = z.object({
  allowedFieldsJson: z.string().nullable().optional(),
  annualNoticeSentAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  noticeUrl: z.string().nullable().optional(),
  optOutEnabled: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ComplianceFERPAFerpaDisclosureBasis */
ComplianceFERPAFerpaDisclosureBasisSchema = z.enum([
  'StudentConsent',
  'GuardianConsent',
  'SchoolOfficial',
  'FinancialAid',
  'HealthOrSafetyEmergency',
  'AuditOrEvaluation',
  'CourtOrder',
  'DirectoryInformation',
  'Other',
]);

/** Zod schema for ComplianceFERPAFerpaDisclosureConsent */
ComplianceFERPAFerpaDisclosureConsentSchema = z.object({
  effectiveFrom: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  guardianUserId: z.string().uuid().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  purpose: z.string().nullable().optional(),
  recipient: z.string().nullable().optional(),
  revokedAt: z.string().datetime().nullable().optional(),
  scope: z.string().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPAFerpaDisclosureLog */
ComplianceFERPAFerpaDisclosureLogSchema = z.object({
  basis: z.lazy(() => ComplianceFERPAFerpaDisclosureBasisSchema).optional(),
  disclosedAt: z.string().datetime().optional(),
  disclosedByUserId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  purpose: z.string().nullable().optional(),
  recipient: z.string().nullable().optional(),
  recordIdsJson: z.string().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPAFerpaEducationRecord */
ComplianceFERPAFerpaEducationRecordSchema = z.object({
  createdAt: z.string().datetime().optional(),
  externalRecordId: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isDirectoryInformation: z.boolean().optional(),
  metadataJson: z.string().nullable().optional(),
  protectionLevel: z.lazy(() => ComplianceFERPAFerpaRecordProtectionLevelSchema).optional(),
  recordKind: z.lazy(() => ComplianceFERPAEducationRecordKindSchema).optional(),
  retentionUntil: z.string().datetime().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPAFerpaInspectionInput */
ComplianceFERPAFerpaInspectionInputSchema = z.object({
  deadline: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  processedAt: z.string().datetime().nullable().optional(),
  processedByUserId: z.string().uuid().nullable().optional(),
  processingNotes: z.string().nullable().optional(),
  requestedByUserId: z.string().uuid().optional(),
  status: z.lazy(() => ComplianceFERPAFerpaRequestStatusSchema).optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPAFerpaRecordProtectionLevel */
ComplianceFERPAFerpaRecordProtectionLevelSchema = z.enum(['DirectoryInformation', 'EducationRecord', 'SensitiveEducationRecord', 'Restricted']);

/** Zod schema for ComplianceFERPAFerpaRequestStatus */
ComplianceFERPAFerpaRequestStatusSchema = z.enum(['Pending', 'InReview', 'Completed', 'Denied', 'Expired']);

/** Zod schema for ComplianceFERPAGrantFerpaDisclosureConsentCommand */
ComplianceFERPAGrantFerpaDisclosureConsentCommandSchema = z.object({
  effectiveFrom: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  guardianUserId: z.string().uuid().nullable().optional(),
  purpose: z.string().nullable().optional(),
  recipient: z.string().nullable().optional(),
  scope: z.string().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPARecordFerpaDisclosureCommand */
ComplianceFERPARecordFerpaDisclosureCommandSchema = z.object({
  basis: z.lazy(() => ComplianceFERPAFerpaDisclosureBasisSchema).optional(),
  disclosedAt: z.string().datetime().optional(),
  disclosedByUserId: z.string().uuid().optional(),
  purpose: z.string().nullable().optional(),
  recipient: z.string().nullable().optional(),
  recordIdsJson: z.string().nullable().optional(),
  scope: z.string().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPARegisterEducationRecordCommand */
ComplianceFERPARegisterEducationRecordCommandSchema = z.object({
  externalRecordId: z.string().nullable().optional(),
  isDirectoryInformation: z.boolean().optional(),
  metadataJson: z.string().nullable().optional(),
  protectionLevel: z.lazy(() => ComplianceFERPAFerpaRecordProtectionLevelSchema).optional(),
  recordKind: z.lazy(() => ComplianceFERPAEducationRecordKindSchema).optional(),
  retentionUntil: z.string().datetime().nullable().optional(),
  studentUserId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for ComplianceFERPASubmitFerpaInspectionRequestCommand */
ComplianceFERPASubmitFerpaInspectionRequestCommandSchema = z.object({
  deadline: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  requestedByUserId: z.string().uuid().optional(),
  studentUserId: z.string().uuid().optional(),
});

/** Zod schema for ComplianceFERPAUpsertDirectoryInformationPolicyCommand */
ComplianceFERPAUpsertDirectoryInformationPolicyCommandSchema = z.object({
  allowedFieldsJson: z.string().nullable().optional(),
  annualNoticeSentAt: z.string().datetime().nullable().optional(),
  noticeUrl: z.string().nullable().optional(),
  optOutEnabled: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ContentStatus */
ContentStatusSchema = z.enum(['Draft', 'Review', 'Published', 'Archived', 'Deleted']);

/** Zod schema for ContentVisibility */
ContentVisibilitySchema = z.enum(['Private', 'Internal', 'Friends', 'Protected', 'Public']);

/** Zod schema for ContentPagesContentResource */
ContentPagesContentResourceSchema = z.object({
  authorId: z.string().uuid().nullable().optional(),
  authorName: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  customData: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isFeatured: z.boolean().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  resourceType: z.string().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  status: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
  viewCount: z.number().int().optional(),
});

/** Zod schema for ContentPagesContentResourceStatus */
ContentPagesContentResourceStatusSchema = z.enum(['Draft', 'InReview', 'Published', 'Archived']);

/** Zod schema for ContentPagesContentResourceType */
ContentPagesContentResourceTypeSchema = z.enum(['Article', 'Tutorial', 'Documentation', 'Video', 'Download', 'ExternalLink', 'Course', 'Custom']);

/** Zod schema for ContentPagesCreateContentResource */
ContentPagesCreateContentResourceSchema = z.object({
  body: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  resourceType: z.lazy(() => ContentPagesContentResourceTypeSchema).optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  structuredData: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesCreateMarketingLead */
ContentPagesCreateMarketingLeadSchema = z.object({
  company: z.string().max(200).nullable().optional(),
  email: z.string().email().min(1).max(200),
  locale: z.string().max(10).nullable().optional(),
  message: z.string().max(4000).nullable().optional(),
  name: z.string().max(120).nullable().optional(),
  pagePath: z.string().max(300).nullable().optional(),
  plan: z.string().max(60).nullable().optional(),
  referrer: z.string().max(2000).nullable().optional(),
  source: z.string().min(1).max(40),
  topic: z.string().max(40).nullable().optional(),
  userAgent: z.string().max(500).nullable().optional(),
});

/** Zod schema for ContentPagesCreatePage */
ContentPagesCreatePageSchema = z.object({
  body: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  pageType: z.lazy(() => ContentPagesPageTypeSchema).optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  structuredData: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesCreatePageSection */
ContentPagesCreatePageSectionSchema = z.object({
  cssClasses: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  heading: z.string().nullable().optional(),
  isVisible: z.boolean().optional(),
  sectionType: z.lazy(() => ContentPagesSectionTypeSchema).optional(),
  sortOrder: z.number().int().optional(),
  subheading: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesMarketingLead */
ContentPagesMarketingLeadSchema = z.object({
  company: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  email: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  locale: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  pagePath: z.string().nullable().optional(),
  plan: z.string().nullable().optional(),
  referrer: z.string().nullable().optional(),
  source: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  topic: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  userAgent: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesOpenGraphMetadata */
ContentPagesOpenGraphMetadataSchema = z.object({
  canonicalUrl: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesPage */
ContentPagesPageSchema = z.object({
  body: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  customData: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  pageType: z.string().nullable().optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  sections: z
    .array(z.lazy(() => ContentPagesPageSectionSchema))
    .nullable()
    .optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  status: z.string().nullable().optional(),
  structuredData: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesPageSection */
ContentPagesPageSectionSchema = z.object({
  createdAt: z.string().datetime().optional(),
  cssClasses: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  heading: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isVisible: z.boolean().optional(),
  pageId: z.string().uuid().optional(),
  sectionType: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  subheading: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesPageStatus */
ContentPagesPageStatusSchema = z.enum(['Draft', 'Published', 'Archived']);

/** Zod schema for ContentPagesPageType */
ContentPagesPageTypeSchema = z.enum(['Landing', 'Legal', 'ResourceIndex', 'Resource', 'Custom']);

/** Zod schema for ContentPagesSectionType */
ContentPagesSectionTypeSchema = z.enum([
  'Hero',
  'Features',
  'Testimonials',
  'Pricing',
  'CallToAction',
  'Faq',
  'RichText',
  'Gallery',
  'Stats',
  'Team',
  'LogoCloud',
  'Newsletter',
  'Contact',
  'ResourceCards',
  'Custom',
]);

/** Zod schema for ContentPagesSitemapEntry */
ContentPagesSitemapEntrySchema = z.object({
  locale: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for ContentPagesUpdateContentResource */
ContentPagesUpdateContentResourceSchema = z.object({
  body: z.string().nullable().optional(),
  categorySlug: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  externalUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().nullable().optional(),
  linkedEntityId: z.string().uuid().nullable().optional(),
  linkedEntityType: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  readingTimeMinutes: z.number().int().nullable().optional(),
  resourceType: z.lazy(() => ContentPagesContentResourceTypeSchema).optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  status: z.lazy(() => ContentPagesContentResourceStatusSchema).optional(),
  structuredData: z.string().nullable().optional(),
  summary: z.string().nullable().optional(),
  tags: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  videoUrl: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesUpdatePage */
ContentPagesUpdatePageSchema = z.object({
  body: z.string().nullable().optional(),
  canonicalUrl: z.string().nullable().optional(),
  customData: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  metaDescription: z.string().nullable().optional(),
  metaKeywords: z.string().nullable().optional(),
  metaTitle: z.string().nullable().optional(),
  ogDescription: z.string().nullable().optional(),
  ogImageUrl: z.string().nullable().optional(),
  ogTitle: z.string().nullable().optional(),
  ogType: z.string().nullable().optional(),
  pageType: z.lazy(() => ContentPagesPageTypeSchema).optional(),
  parentPageId: z.string().uuid().nullable().optional(),
  robotsDirective: z.string().nullable().optional(),
  scheduledPublishAt: z.string().datetime().nullable().optional(),
  slug: z.string().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  status: z.lazy(() => ContentPagesPageStatusSchema).optional(),
  structuredData: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  twitterCard: z.string().nullable().optional(),
  twitterSite: z.string().nullable().optional(),
});

/** Zod schema for ContentPagesUpdatePageSection */
ContentPagesUpdatePageSectionSchema = z.object({
  cssClasses: z.string().nullable().optional(),
  data: z.string().nullable().optional(),
  heading: z.string().nullable().optional(),
  isVisible: z.boolean().nullable().optional(),
  sectionType: z.lazy(() => ContentPagesSectionTypeSchema).optional(),
  sortOrder: z.number().int().nullable().optional(),
  subheading: z.string().nullable().optional(),
});

/** Zod schema for FeaturesBulkEvaluationInput */
FeaturesBulkEvaluationInputSchema = z.object({
  context: z.lazy(() => FeaturesFeatureContextSchema).optional(),
  featureKeys: z.array(z.string()).nullable().optional(),
});

/** Zod schema for FeaturesCapabilityAuditLog */
FeaturesCapabilityAuditLogSchema = z.object({
  capabilityKey: z.string().nullable().optional(),
  changeReason: z.string().nullable().optional(),
  changeType: z.string().nullable().optional(),
  changedAt: z.string().datetime().optional(),
  changedByUserId: z.string().uuid().nullable().optional(),
  id: z.string().uuid().optional(),
  newSource: z.string().nullable().optional(),
  newValue: z.boolean().optional(),
  oldSource: z.string().nullable().optional(),
  oldValue: z.boolean().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for FeaturesCapabilityCheckOutput */
FeaturesCapabilityCheckOutputSchema = z.object({
  capability: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for FeaturesCreateFeatureInput */
FeaturesCreateFeatureInputSchema = z.object({
  description: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
  key: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for FeaturesFeatureContext */
FeaturesFeatureContextSchema = z.object({
  country: z.string().nullable().optional(),
  customAttributes: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  environment: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  requestTime: z.string().datetime().optional(),
  subscriptionPlanId: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userAgent: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for FeaturesFeatureEvaluationInput */
FeaturesFeatureEvaluationInputSchema = z.object({
  context: z.lazy(() => FeaturesFeatureContextSchema).optional(),
  defaultValue: z.record(z.string(), z.unknown()).nullable().optional(),
  featureKey: z.string().nullable().optional(),
});

/** Zod schema for FeaturesFeatureFlag */
FeaturesFeatureFlagSchema = z.object({
  createdAt: z.string().datetime(),
  defaultValue: z.record(z.string(), z.unknown()).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  environment: z.string().nullable().optional(),
  id: z.string().uuid(),
  isEnabled: z.boolean(),
  key: z.string().nullable(),
  name: z.string().nullable(),
  targets: z
    .array(z.lazy(() => FeaturesFeatureFlagTargetSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => FeaturesFeatureFlagTypeSchema),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for FeaturesFeatureFlagTarget */
FeaturesFeatureFlagTargetSchema = z.object({
  createdAt: z.string().datetime(),
  customValue: z.string().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  featureFlagId: z.string().uuid(),
  id: z.string().uuid(),
  isEnabled: z.boolean(),
  metadata: z.string().nullable().optional(),
  priority: z.number().int().optional(),
  rolloutPercentage: z.number().int().optional(),
  targetIdentifier: z.string().nullable(),
  targetType: z.string().nullable(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for FeaturesFeatureFlagType */
FeaturesFeatureFlagTypeSchema = z.enum(['Toggle', 'Numeric', 'String', 'Percentage', 'UserSegment']);

/** Zod schema for FeaturesSetCapabilityOverrideInput */
FeaturesSetCapabilityOverrideInputSchema = z.object({
  capability: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  isEnabled: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  source: z.string().nullable().optional(),
});

/** Zod schema for FeaturesToggleFeatureInput */
FeaturesToggleFeatureInputSchema = z.object({
  environment: z.string().nullable().optional(),
  featureKey: z.string().nullable().optional(),
  isEnabled: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for FeaturesUpdateFeatureInput */
FeaturesUpdateFeatureInputSchema = z.object({
  defaultValue: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  enabledValue: z.string().nullable().optional(),
  isEnabled: z.boolean().nullable().optional(),
  name: z.string().nullable().optional(),
  rolloutPercentage: z.number().int().nullable().optional(),
});

/** Zod schema for Fido2NetLibAssertionOptions */
Fido2NetLibAssertionOptionsSchema = z.object({
  allowCredentials: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialDescriptorSchema))
    .nullable()
    .optional(),
  challenge: z.string().nullable().optional(),
  extensions: z.lazy(() => ObjectsAuthenticationExtensionsClientInputsSchema).optional(),
  hints: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialHintSchema))
    .nullable()
    .optional(),
  rpId: z.string().nullable().optional(),
  timeout: z.number().int().optional(),
  userVerification: z.lazy(() => ObjectsUserVerificationRequirementSchema).optional(),
});

/** Zod schema for Fido2NetLibAuthenticatorSelection */
Fido2NetLibAuthenticatorSelectionSchema = z.object({
  authenticatorAttachment: z.lazy(() => ObjectsAuthenticatorAttachmentSchema).optional(),
  requireResidentKey: z.boolean().optional(),
  residentKey: z.lazy(() => ObjectsResidentKeyRequirementSchema).optional(),
  userVerification: z.lazy(() => ObjectsUserVerificationRequirementSchema).optional(),
});

/** Zod schema for Fido2NetLibCredentialCreateOptions */
Fido2NetLibCredentialCreateOptionsSchema = z.object({
  attestation: z.lazy(() => ObjectsAttestationConveyancePreferenceSchema).optional(),
  attestationFormats: z
    .array(z.lazy(() => ObjectsAttestationStatementFormatIdentifierSchema))
    .nullable()
    .optional(),
  authenticatorSelection: z.lazy(() => Fido2NetLibAuthenticatorSelectionSchema).optional(),
  challenge: z.string().nullable(),
  excludeCredentials: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialDescriptorSchema))
    .nullable()
    .optional(),
  extensions: z.lazy(() => ObjectsAuthenticationExtensionsClientInputsSchema).optional(),
  hints: z
    .array(z.lazy(() => ObjectsPublicKeyCredentialHintSchema))
    .nullable()
    .optional(),
  pubKeyCredParams: z.array(z.lazy(() => Fido2NetLibPubKeyCredParamSchema)).nullable(),
  rp: z.lazy(() => Fido2NetLibPublicKeyCredentialRpEntitySchema),
  timeout: z.number().int().optional(),
  user: z.lazy(() => Fido2NetLibFido2UserSchema),
});

/** Zod schema for Fido2NetLibFido2User */
Fido2NetLibFido2UserSchema = z.object({
  displayName: z.string().nullable().optional(),
  id: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for Fido2NetLibPubKeyCredParam */
Fido2NetLibPubKeyCredParamSchema = z.object({
  alg: z.lazy(() => ObjectsCOSEAlgorithmSchema).optional(),
  type: z.lazy(() => ObjectsPublicKeyCredentialTypeSchema).optional(),
});

/** Zod schema for Fido2NetLibPublicKeyCredentialRpEntity */
Fido2NetLibPublicKeyCredentialRpEntitySchema = z.object({
  icon: z.string().nullable().optional(),
  id: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for GameJamsAddJamCriteriaInput */
GameJamsAddJamCriteriaInputSchema = z.object({
  description: z.string().nullable().optional(),
  maxScore: z.number().int().optional(),
  name: z.string().nullable().optional(),
  weight: z.number().optional(),
});

/** Zod schema for GameJamsCreateJamInput */
GameJamsCreateJamInputSchema = z.object({
  createdBy: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().optional(),
  maxParticipants: z.number().int().nullable().optional(),
  name: z.string().nullable().optional(),
  rules: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  submissionCriteria: z.string().nullable().optional(),
  theme: z.string().nullable().optional(),
  votingEndDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for GameJamsJam */
GameJamsJamSchema = z.object({
  createdAt: z.string().datetime(),
  createdBy: z.string().uuid(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  endDate: z.string().datetime(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  maxParticipants: z.number().int().nullable().optional(),
  name: z.string().min(1).max(255),
  participantCount: z.number().int().optional(),
  rules: z.string().nullable().optional(),
  slug: z.string().min(1).max(255),
  startDate: z.string().datetime(),
  status: z.lazy(() => GameJamsJamStatusSchema),
  submissionCriteria: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  theme: z.string().max(500).nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  votingEndDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for GameJamsJamCriteria */
GameJamsJamCriteriaSchema = z.object({
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  jamId: z.string().uuid().optional(),
  maxScore: z.number().int().optional(),
  name: z.string().nullable().optional(),
  weight: z.number().optional(),
});

/** Zod schema for GameJamsJamDto */
GameJamsJamDtoSchema = z.object({
  createdBy: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  maxParticipants: z.number().int().nullable().optional(),
  name: z.string().nullable().optional(),
  participantCount: z.number().int().optional(),
  slug: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  status: z.lazy(() => GameJamsJamStatusSchema).optional(),
  theme: z.string().nullable().optional(),
  votingEndDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for GameJamsJamScore */
GameJamsJamScoreSchema = z.object({
  createdAt: z.string().datetime(),
  criteriaId: z.string().uuid(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  feedback: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  judgeUserId: z.string().uuid(),
  score: z.number().int(),
  submissionId: z.string().uuid(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for GameJamsJamScoreDto */
GameJamsJamScoreDtoSchema = z.object({
  criteriaId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  judgeUserId: z.string().uuid().optional(),
  score: z.number().int().optional(),
  submissionId: z.string().uuid().optional(),
});

/** Zod schema for GameJamsJamStatus */
GameJamsJamStatusSchema = z.enum(['Upcoming', 'Active', 'Voting', 'Completed', 'Cancelled']);

/** Zod schema for GameJamsJamSubmission */
GameJamsJamSubmissionSchema = z.object({
  id: z.string().uuid().optional(),
  jamId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().optional(),
  submissionNotes: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for GameJamsScoreJamSubmissionInput */
GameJamsScoreJamSubmissionInputSchema = z.object({
  criteriaId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  judgeUserId: z.string().uuid().optional(),
  score: z.number().int().optional(),
});

/** Zod schema for GameJamsSubmitJamEntryInput */
GameJamsSubmitJamEntryInputSchema = z.object({
  notes: z.string().nullable().optional(),
  projectVersionId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationApiKey */
IdentityAuthenticationApiKeySchema = z.object({
  createdAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  keyPrefix: z.string().nullable().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  name: z.string().nullable().optional(),
  scopes: z.array(z.string()).nullable().optional(),
  usageCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationAssignRoleToUserInput */
IdentityAuthenticationAssignRoleToUserInputSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  roleId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationBackupCodesOutput */
IdentityAuthenticationBackupCodesOutputSchema = z.object({
  codes: z.array(z.string()).nullable().optional(),
  generatedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationBackupCodesStatusOutput */
IdentityAuthenticationBackupCodesStatusOutputSchema = z.object({
  hasBackupCodes: z.boolean(),
  remainingCount: z.number().int(),
  totalCount: z.number().int(),
  usedCount: z.number().int(),
});

/** Zod schema for IdentityAuthenticationBeginWebAuthnAuthenticationInput */
IdentityAuthenticationBeginWebAuthnAuthenticationInputSchema = z.object({
  email: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationBeginWebAuthnRegistrationInput */
IdentityAuthenticationBeginWebAuthnRegistrationInputSchema = z.object({
  displayName: z.string().nullable().optional(),
  email: z.string().nullable().optional(),
  preferredAuthenticatorType: z.lazy(() => IdentityAuthenticationWebAuthnAuthenticatorTypeSchema).optional(),
});

/** Zod schema for IdentityAuthenticationCleanupKeysInput */
IdentityAuthenticationCleanupKeysInputSchema = z.object({
  retentionDays: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCleanupResult */
IdentityAuthenticationCleanupResultSchema = z.object({
  deletedCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationClientCredentialsTokenOutput */
IdentityAuthenticationClientCredentialsTokenOutputSchema = z.object({
  accessToken: z.string().nullable().optional(),
  expiresIn: z.number().int().optional(),
  scope: z.string().nullable().optional(),
  tokenType: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCompleteMfaSetupInput */
IdentityAuthenticationCompleteMfaSetupInputSchema = z.object({
  code: z.string().min(1),
  secretKey: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationCompletePasswordResetInput */
IdentityAuthenticationCompletePasswordResetInputSchema = z.object({
  confirmPassword: z.string().min(1),
  newPassword: z.string().min(8),
  tenantId: z.string().uuid().nullable().optional(),
  token: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationCompleteWebAuthnAuthenticationInput */
IdentityAuthenticationCompleteWebAuthnAuthenticationInputSchema = z.object({
  assertionResponse: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCompleteWebAuthnRegistrationInput */
IdentityAuthenticationCompleteWebAuthnRegistrationInputSchema = z.object({
  attestationResponse: z.string().nullable().optional(),
  friendlyName: z.string().nullable().optional(),
  isPasswordless: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationConsumeMagicLinkInput */
IdentityAuthenticationConsumeMagicLinkInputSchema = z.object({
  deviceFingerprint: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  token: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationCreateApiKeyCommand */
IdentityAuthenticationCreateApiKeyCommandSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  ipWhitelist: z.string().nullable().optional(),
  name: z.string().nullable(),
  scopes: z.array(z.string()).nullable(),
});

/** Zod schema for IdentityAuthenticationCreateApiKeyOutput */
IdentityAuthenticationCreateApiKeyOutputSchema = z.object({
  apiKey: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  keyPrefix: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  scopes: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCreateRoleInput */
IdentityAuthenticationCreateRoleInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationCreateServiceAccountInput */
IdentityAuthenticationCreateServiceAccountInputSchema = z.object({
  allowedIpAddresses: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  name: z.string().nullable().optional(),
  scopes: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationDeviceInfo */
IdentityAuthenticationDeviceInfoSchema = z.object({
  browser: z.string().nullable().optional(),
  browserVersion: z.string().nullable().optional(),
  deviceId: z.string().nullable().optional(),
  deviceName: z.string().nullable().optional(),
  deviceType: z.string().nullable().optional(),
  fingerprint: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  isBot: z.boolean().optional(),
  isMobile: z.boolean().optional(),
  language: z.string().nullable().optional(),
  operatingSystem: z.string().nullable().optional(),
  osVersion: z.string().nullable().optional(),
  screenResolution: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
  userAgent: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationDisableMfaInput */
IdentityAuthenticationDisableMfaInputSchema = z.object({
  password: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationEmailVerificationOutput */
IdentityAuthenticationEmailVerificationOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationEmailVerificationResult */
IdentityAuthenticationEmailVerificationResultSchema = z.object({
  email: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
  userId: z.string().uuid().nullable().optional(),
  verifiedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationGitHubSignInOutput */
IdentityAuthenticationGitHubSignInOutputSchema = z.object({
  authUrl: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationGoogleIdTokenInput */
IdentityAuthenticationGoogleIdTokenInputSchema = z.object({
  idToken: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationJwtKeyInfo */
IdentityAuthenticationJwtKeyInfoSchema = z.object({
  algorithm: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  isActive: z.boolean().optional(),
  keyId: z.string().nullable().optional(),
  keyVersion: z.number().int().optional(),
  rotatedAt: z.string().datetime().nullable().optional(),
  rotationReason: z.string().nullable().optional(),
  validFrom: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationLocalSignInInput */
IdentityAuthenticationLocalSignInInputSchema = z.object({
  deviceFingerprint: z.string().nullable().optional(),
  email: z.string().email().min(1),
  emailOrUsername: z.string().nullable().optional(),
  password: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
  username: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationLocalSignUpInput */
IdentityAuthenticationLocalSignUpInputSchema = z.object({
  email: z.string().email().min(1),
  firstName: z.string().nullable().optional(),
  lastName: z.string().nullable().optional(),
  password: z.string().min(8),
  phoneNumber: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  username: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationLocationInfo */
IdentityAuthenticationLocationInfoSchema = z.object({
  city: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  countryCode: z.string().nullable().optional(),
  displayLocation: z.string().nullable().optional(),
  ipAddress: z.string().nullable().optional(),
  isHosting: z.boolean().nullable().optional(),
  isProxy: z.boolean().nullable().optional(),
  isp: z.string().nullable().optional(),
  latitude: z.number().nullable().optional(),
  longitude: z.number().nullable().optional(),
  organization: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  region: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationLockServiceAccountInput */
IdentityAuthenticationLockServiceAccountInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationMagicLinkRequestResult */
IdentityAuthenticationMagicLinkRequestResultSchema = z.object({
  developmentPreviewToken: z.string().nullable().optional(),
  expiresInMinutes: z.number().int().optional(),
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationMfaConfigurationOutput */
IdentityAuthenticationMfaConfigurationOutputSchema = z.object({
  backupCodesRemaining: z.number().int().optional(),
  enabledAt: z.string().datetime().nullable().optional(),
  enabledMethods: z.array(z.string()).nullable().optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationMfaErrorOutput */
IdentityAuthenticationMfaErrorOutputSchema = z.object({
  error: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationMfaMethod */
IdentityAuthenticationMfaMethodSchema = z.enum(['Totp', 'BackupCode', 'Sms', 'Email', 'WebAuthn']);

/** Zod schema for IdentityAuthenticationMfaMethodInfo */
IdentityAuthenticationMfaMethodInfoSchema = z.object({
  description: z.string().nullable(),
  isAvailable: z.boolean(),
  isEnabled: z.boolean(),
  method: z.lazy(() => IdentityAuthenticationMfaMethodSchema),
  name: z.string().nullable(),
  priority: z.number().int(),
});

/** Zod schema for IdentityAuthenticationMfaMethodsOutput */
IdentityAuthenticationMfaMethodsOutputSchema = z.object({
  defaultMethod: z.lazy(() => IdentityAuthenticationMfaMethodSchema).optional(),
  methods: z.array(z.lazy(() => IdentityAuthenticationMfaMethodInfoSchema)).nullable(),
});

/** Zod schema for IdentityAuthenticationMfaSetupOutput */
IdentityAuthenticationMfaSetupOutputSchema = z.object({
  backupCodes: z.array(z.string()).nullable().optional(),
  errorMessage: z.string().nullable().optional(),
  isSuccess: z.boolean().optional(),
  qrCodeData: z.string().nullable().optional(),
  qrCodeUri: z.string().nullable().optional(),
  secretKey: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationMfaSuccessOutput */
IdentityAuthenticationMfaSuccessOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationMfaVerificationOutput */
IdentityAuthenticationMfaVerificationOutputSchema = z.object({
  accessToken: z.string().nullable().optional(),
  isValid: z.boolean().optional(),
  refreshToken: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationOAuth2ErrorOutput */
IdentityAuthenticationOAuth2ErrorOutputSchema = z.object({
  error: z.string().nullable().optional(),
  errorDescription: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordChangeInput */
IdentityAuthenticationPasswordChangeInputSchema = z.object({
  confirmPassword: z.string().min(1),
  currentPassword: z.string().min(1),
  newPassword: z.string().min(8),
  revokeOtherSessions: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordChangeResult */
IdentityAuthenticationPasswordChangeResultSchema = z.object({
  message: z.string().nullable().optional(),
  sessionsRevoked: z.number().int().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordResetRequestResult */
IdentityAuthenticationPasswordResetRequestResultSchema = z.object({
  expiresInMinutes: z.number().int().optional(),
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationPasswordResetResult */
IdentityAuthenticationPasswordResetResultSchema = z.object({
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationPatchServiceAccountInput */
IdentityAuthenticationPatchServiceAccountInputSchema = z.object({
  description: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  name: z.string().nullable().optional(),
  scopes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRefreshTokenInput */
IdentityAuthenticationRefreshTokenInputSchema = z.object({
  refreshToken: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRemoveRoleFromUserInput */
IdentityAuthenticationRemoveRoleFromUserInputSchema = z.object({
  roleId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationRequestMagicLinkInput */
IdentityAuthenticationRequestMagicLinkInputSchema = z.object({
  email: z.string().email().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRequestPasswordResetInput */
IdentityAuthenticationRequestPasswordResetInputSchema = z.object({
  email: z.string().email().min(1),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRevokeApiKeyInput */
IdentityAuthenticationRevokeApiKeyInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationRevokeRefreshTokenInput */
IdentityAuthenticationRevokeRefreshTokenInputSchema = z.object({
  ipAddress: z.string().nullable().optional(),
  reason: z.string().nullable().optional(),
  token: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationRiskLevel */
IdentityAuthenticationRiskLevelSchema = z.enum(['Low', 'Medium', 'High', 'Critical']);

/** Zod schema for IdentityAuthenticationRotateKeyInput */
IdentityAuthenticationRotateKeyInputSchema = z.object({
  reason: z.string().nullable().optional(),
  validityDays: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSecretRotationOutput */
IdentityAuthenticationSecretRotationOutputSchema = z.object({
  clientSecret: z.string().nullable().optional(),
  warning: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSendEmailVerificationInput */
IdentityAuthenticationSendEmailVerificationInputSchema = z.object({
  email: z.string().email().min(1),
});

/** Zod schema for IdentityAuthenticationServiceAccountAuditEntry */
IdentityAuthenticationServiceAccountAuditEntrySchema = z.object({
  action: z.string().nullable().optional(),
  details: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  ipAddress: z.string().nullable().optional(),
  performedBy: z.string().nullable().optional(),
  timestamp: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountAuditLogOutput */
IdentityAuthenticationServiceAccountAuditLogOutputSchema = z.object({
  entries: z
    .array(z.lazy(() => IdentityAuthenticationServiceAccountAuditEntrySchema))
    .nullable()
    .optional(),
  page: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  serviceAccountId: z.string().uuid().optional(),
  totalCount: z.number().int().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountCreatedOutput */
IdentityAuthenticationServiceAccountCreatedOutputSchema = z.object({
  clientId: z.string().nullable().optional(),
  clientSecret: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  scopes: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  warning: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationServiceAccountOutput */
IdentityAuthenticationServiceAccountOutputSchema = z.object({
  authenticationCount: z.number().int().optional(),
  clientId: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  createdBy: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isLocked: z.boolean().optional(),
  lastAuthenticatedAt: z.string().datetime().nullable().optional(),
  name: z.string().nullable().optional(),
  scopes: z.string().nullable().optional(),
  secretRotationCount: z.number().int().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationSessionOutput */
IdentityAuthenticationSessionOutputSchema = z.object({
  createdAt: z.string().datetime().optional(),
  deviceInfo: z.lazy(() => IdentityAuthenticationDeviceInfoSchema).optional(),
  expiresAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  ipAddress: z.string().nullable().optional(),
  isCurrent: z.boolean().optional(),
  isTrustedDevice: z.boolean().optional(),
  lastUsedAt: z.string().datetime().optional(),
  location: z.lazy(() => IdentityAuthenticationLocationInfoSchema).optional(),
});

/** Zod schema for IdentityAuthenticationSessionSecurityAnalysis */
IdentityAuthenticationSessionSecurityAnalysisSchema = z.object({
  activeSessionCount: z.number().int().optional(),
  analyzedAt: z.string().datetime().optional(),
  isSuspicious: z.boolean().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
  riskFactors: z.array(z.string()).nullable().optional(),
  riskLevel: z.lazy(() => IdentityAuthenticationRiskLevelSchema).optional(),
  riskScore: z.number().int().optional(),
  securityFlags: z.array(z.string()).nullable().optional(),
  sessionId: z.string().uuid().optional(),
  totalDeviceCount: z.number().int().optional(),
  unusualActivityDetected: z.boolean().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationSessionSuccessOutput */
IdentityAuthenticationSessionSuccessOutputSchema = z.object({
  message: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationSessionTerminationOutput */
IdentityAuthenticationSessionTerminationOutputSchema = z.object({
  message: z.string().nullable(),
  terminatedCount: z.number().int(),
});

/** Zod schema for IdentityAuthenticationSignInOutput */
IdentityAuthenticationSignInOutputSchema = z.object({
  accessToken: z.string().nullable().optional(),
  accessTokenExpiresAt: z.string().datetime().optional(),
  availableMethods: z.array(z.string()).nullable().optional(),
  availableTenants: z
    .array(z.lazy(() => TenantInfoSchema))
    .nullable()
    .optional(),
  email: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  expiresIn: z.number().int().optional(),
  message: z.string().nullable().optional(),
  mfaSessionId: z.string().nullable().optional(),
  mfaToken: z.string().nullable().optional(),
  refreshToken: z.string().nullable().optional(),
  refreshTokenExpiresAt: z.string().datetime().optional(),
  requiresMfa: z.boolean().optional(),
  requiresStepUp: z.boolean().optional(),
  riskFactors: z.array(z.string()).nullable().optional(),
  riskLevel: z.lazy(() => IdentityAuthenticationRiskLevelSchema).optional(),
  sessionId: z.string().uuid().optional(),
  stepUpExpiresAt: z.string().datetime().nullable().optional(),
  stepUpToken: z.string().nullable().optional(),
  success: z.boolean().optional(),
  tempToken: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  user: z.lazy(() => IdentityAuthenticationUserSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationSmsMfaSetupInput */
IdentityAuthenticationSmsMfaSetupInputSchema = z.object({
  phoneNumber: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationSmsMfaSetupOutput */
IdentityAuthenticationSmsMfaSetupOutputSchema = z.object({
  expiresInSeconds: z.number().int(),
  message: z.string().nullable(),
  phoneNumberMasked: z.string().nullable(),
});

/** Zod schema for IdentityAuthenticationTrustDeviceInput */
IdentityAuthenticationTrustDeviceInputSchema = z.object({
  deviceName: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationTrustedDeviceOutput */
IdentityAuthenticationTrustedDeviceOutputSchema = z.object({
  deviceInfo: z.lazy(() => IdentityAuthenticationDeviceInfoSchema).optional(),
  deviceName: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  lastUsedAt: z.string().datetime().optional(),
  trustedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateCredentialNameInput */
IdentityAuthenticationUpdateCredentialNameInputSchema = z.object({
  friendlyName: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateRoleInput */
IdentityAuthenticationUpdateRoleInputSchema = z.object({
  description: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  name: z.string().nullable().optional(),
  permissions: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUpdateScopesInput */
IdentityAuthenticationUpdateScopesInputSchema = z.object({
  scopes: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationUser */
IdentityAuthenticationUserSchema = z.object({
  createdAt: z.string().datetime().optional(),
  email: z.string().nullable().optional(),
  emailVerified: z.boolean().optional(),
  firstName: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  lastLoginAt: z.string().datetime().nullable().optional(),
  lastName: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
  phoneNumberVerified: z.boolean().optional(),
  username: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationVerifyEmailInput */
IdentityAuthenticationVerifyEmailInputSchema = z.object({
  tenantId: z.string().uuid().nullable().optional(),
  token: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationVerifyMfaInput */
IdentityAuthenticationVerifyMfaInputSchema = z.object({
  code: z.string().min(1),
  method: z.lazy(() => IdentityAuthenticationMfaMethodSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityAuthenticationWeb3ChallengeInput */
IdentityAuthenticationWeb3ChallengeInputSchema = z.object({
  chainId: z.string().nullable().optional(),
  walletAddress: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationWeb3ChallengeOutput */
IdentityAuthenticationWeb3ChallengeOutputSchema = z.object({
  challenge: z.string().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  nonce: z.string().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWeb3VerifyInput */
IdentityAuthenticationWeb3VerifyInputSchema = z.object({
  chainId: z.string().min(1),
  deviceFingerprint: z.string().nullable().optional(),
  nonce: z.string().min(1),
  signature: z.string().min(1),
  tenantId: z.string().uuid().nullable().optional(),
  walletAddress: z.string().min(1),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticationOptionsResult */
IdentityAuthenticationWebAuthnAuthenticationOptionsResultSchema = z.object({
  error: z.string().nullable().optional(),
  options: z.lazy(() => Fido2NetLibAssertionOptionsSchema).optional(),
  optionsJson: z.string().nullable().optional(),
  sessionId: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticationResult */
IdentityAuthenticationWebAuthnAuthenticationResultSchema = z.object({
  accessToken: z.string().nullable().optional(),
  accessTokenExpiresAt: z.string().datetime().nullable().optional(),
  credentialId: z.string().uuid().nullable().optional(),
  email: z.string().nullable().optional(),
  error: z.string().nullable().optional(),
  expiresIn: z.number().int().optional(),
  isPasswordless: z.boolean().optional(),
  refreshToken: z.string().nullable().optional(),
  refreshTokenExpiresAt: z.string().datetime().nullable().optional(),
  success: z.boolean().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnAuthenticatorType */
IdentityAuthenticationWebAuthnAuthenticatorTypeSchema = z.enum(['Platform', 'CrossPlatform']);

/** Zod schema for IdentityAuthenticationWebAuthnCredentialInfo */
IdentityAuthenticationWebAuthnCredentialInfoSchema = z.object({
  authenticatorType: z.lazy(() => IdentityAuthenticationWebAuthnAuthenticatorTypeSchema).optional(),
  backedUp: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  friendlyName: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isDefault: z.boolean().optional(),
  isPasswordless: z.boolean().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnCredentialVerifyResult */
IdentityAuthenticationWebAuthnCredentialVerifyResultSchema = z.object({
  error: z.string().nullable().optional(),
  isExpired: z.boolean().optional(),
  isRevoked: z.boolean().optional(),
  isValid: z.boolean().optional(),
  lastUsedAt: z.string().datetime().nullable().optional(),
  signatureCount: z.number().int().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnRegistrationOptionsResult */
IdentityAuthenticationWebAuthnRegistrationOptionsResultSchema = z.object({
  error: z.string().nullable().optional(),
  options: z.lazy(() => Fido2NetLibCredentialCreateOptionsSchema).optional(),
  optionsJson: z.string().nullable().optional(),
  sessionId: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnRegistrationResult */
IdentityAuthenticationWebAuthnRegistrationResultSchema = z.object({
  credentialId: z.string().uuid().nullable().optional(),
  error: z.string().nullable().optional(),
  friendlyName: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityAuthenticationWebAuthnStatusOutput */
IdentityAuthenticationWebAuthnStatusOutputSchema = z.object({
  credentialCount: z.number().int().optional(),
  hasPasswordlessCredential: z.boolean().optional(),
  hasPlatformAuthenticator: z.boolean().optional(),
  hasSecurityKey: z.boolean().optional(),
  isEnabled: z.boolean().optional(),
});

/** Zod schema for IdentityAuthorizationPermissionType */
IdentityAuthorizationPermissionTypeSchema = z.enum([
  'Read',
  'Comment',
  'Reply',
  'Vote',
  'Share',
  'Report',
  'Follow',
  'Bookmark',
  'React',
  'Subscribe',
  'Mention',
  'Tag',
  'Categorize',
  'Collection',
  'Series',
  'CrossReference',
  'Translate',
  'Version',
  'Template',
  'Create',
  'Draft',
  'Submit',
  'Withdraw',
  'Archive',
  'Restore',
  'Delete',
  'HardDelete',
  'Backup',
  'Migrate',
  'Clone',
  'Edit',
  'Proofread',
  'FactCheck',
  'StyleGuide',
  'Plagiarism',
  'Seo',
  'Accessibility',
  'Legal',
  'Brand',
  'Guidelines',
  'Approve',
  'Reject',
  'RequestRevision',
  'Escalate',
  'Override',
  'Delegate',
  'FastTrack',
  'BatchApprove',
  'ConditionalApprove',
  'RequireReview',
  'Publish',
  'Unpublish',
  'Schedule',
  'SetPublishDate',
  'Visibility',
  'Feature',
  'Pin',
  'Sticky',
  'Highlight',
  'Promote',
  'Moderate',
  'Hide',
  'Flag',
  'Warn',
  'Suspend',
  'Ban',
  'Quarantine',
  'Review',
  'Investigate',
  'EscalateModeration',
  'Invite',
  'Assign',
  'Collaborate',
  'CoAuthor',
  'Contribute',
  'Suggest',
  'Track',
  'Merge',
  'Resolve',
  'Coordinate',
  'Score',
  'Rate',
  'Benchmark',
  'Metrics',
  'Analytics',
  'Performance',
  'Feedback',
  'Audit',
  'Standards',
  'Improvement',
  'Monetize',
  'Pricing',
  'Paywall',
  'Manage',
  'Admin',
  'Execute',
  'Export',
  'Import',
  'SystemAdmin',
  'TenantAdmin',
  'UserManagement',
  'Configure',
]);

/** Zod schema for IdentityTenantsAddTenantMemberOutput */
IdentityTenantsAddTenantMemberOutputSchema = z.object({
  memberId: z.string().uuid().nullable().optional(),
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsAddUserMembershipInput */
IdentityTenantsAddUserMembershipInputSchema = z.object({
  invitedByEmail: z.string().nullable().optional(),
  inviteeEmail: z.string().nullable().optional(),
  inviteeName: z.string().nullable().optional(),
  requiresAcceptance: z.boolean().optional(),
  role: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for IdentityTenantsArchiveInput */
IdentityTenantsArchiveInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkActivateTenantsCommand */
IdentityTenantsBulkActivateTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkArchiveTenantsCommand */
IdentityTenantsBulkArchiveTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkCreateTenantItem */
IdentityTenantsBulkCreateTenantItemSchema = z.object({
  adminEmail: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkCreateTenantsCommand */
IdentityTenantsBulkCreateTenantsCommandSchema = z.object({
  tenants: z
    .array(z.lazy(() => IdentityTenantsBulkCreateTenantItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsBulkDeactivateTenantsCommand */
IdentityTenantsBulkDeactivateTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkDeleteTenantsCommand */
IdentityTenantsBulkDeleteTenantsCommandSchema = z.object({
  hardDelete: z.boolean().optional(),
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkPurgeTenantsCommand */
IdentityTenantsBulkPurgeTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkUndeleteTenantsCommand */
IdentityTenantsBulkUndeleteTenantsCommandSchema = z.object({
  tenantIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityTenantsBulkUpdateTenantItem */
IdentityTenantsBulkUpdateTenantItemSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
});

/** Zod schema for IdentityTenantsBulkUpdateTenantsCommand */
IdentityTenantsBulkUpdateTenantsCommandSchema = z.object({
  updates: z
    .array(z.lazy(() => IdentityTenantsBulkUpdateTenantItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsCreateTenantInput */
IdentityTenantsCreateTenantInputSchema = z.object({
  adminEmail: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsGetUserMembershipsOutput */
IdentityTenantsGetUserMembershipsOutputSchema = z.object({
  memberships: z
    .array(z.lazy(() => IdentityTenantsUserMembershipSchema))
    .nullable()
    .optional(),
  totalCount: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsMembershipCountOutput */
IdentityTenantsMembershipCountOutputSchema = z.object({
  count: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsRecoverInput */
IdentityTenantsRecoverInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsReplaceTenantMetadataInput */
IdentityTenantsReplaceTenantMetadataInputSchema = z.object({
  adminNotes: z.string().nullable().optional(),
  businessInfo: z.lazy(() => IdentityTenantsUpdateTenantBusinessInfoInputSchema).optional(),
  contactInfo: z.lazy(() => IdentityTenantsUpdateTenantContactInfoInputSchema).optional(),
  customFields: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsReplaceTenantSettingsInput */
IdentityTenantsReplaceTenantSettingsInputSchema = z.object({
  businessRules: z.lazy(() => IdentityTenantsUpdateTenantBusinessRulesInputSchema).optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  integrationSettings: z.lazy(() => IdentityTenantsUpdateTenantIntegrationSettingsInputSchema).optional(),
  securitySettings: z.lazy(() => IdentityTenantsUpdateTenantSecuritySettingsInputSchema).optional(),
  systemConfiguration: z.lazy(() => IdentityTenantsUpdateTenantSystemConfigurationInputSchema).optional(),
  systemLimits: z.lazy(() => IdentityTenantsUpdateTenantSystemLimitsInputSchema).optional(),
  userInterfaceSettings: z.lazy(() => IdentityTenantsUpdateTenantUiSettingsInputSchema).optional(),
});

/** Zod schema for IdentityTenantsSlugValidation */
IdentityTenantsSlugValidationSchema = z.object({
  isAvailable: z.boolean().optional(),
  isValid: z.boolean().optional(),
  suggestedAlternatives: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenant */
IdentityTenantsTenantSchema = z.object({
  activeMemberCount: z.number().int().optional(),
  adminEmail: z.string().max(255).nullable().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  canAcceptMembers: z.boolean().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  hasActiveMembers: z.boolean().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isArchived: z.boolean().optional(),
  isDefault: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  name: z.string().min(1).max(100),
  slug: z.string().min(1).max(255),
  tenantDomains: z
    .array(z.lazy(() => IdentityTenantsTenantDomainSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  tenantMembers: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  tenantSettings: z.lazy(() => IdentityTenantsTenantSettingsSchema).optional(),
  tenantStatistics: z.lazy(() => IdentityTenantsTenantStatisticsSchema).optional(),
  updatedAt: z.string().datetime(),
  usageTrackingRecords: z
    .array(z.lazy(() => IdentityTenantsUsageTrackingSchema))
    .nullable()
    .optional(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantAddress */
IdentityTenantsTenantAddressSchema = z.object({
  city: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  street: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantAuditLogEntry */
IdentityTenantsTenantAuditLogEntrySchema = z.object({
  action: z.string().nullable().optional(),
  actorEmail: z.string().nullable().optional(),
  actorId: z.string().uuid().nullable().optional(),
  actorName: z.string().nullable().optional(),
  afterValues: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  beforeValues: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  correlationId: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  ipAddress: z.string().nullable().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
  tenantId: z.string().uuid().optional(),
  timestamp: z.string().datetime().optional(),
  userAgent: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBranding */
IdentityTenantsTenantBrandingSchema = z.object({
  companyName: z.string().nullable().optional(),
  faviconUrl: z.string().nullable().optional(),
  logoUrl: z.string().nullable().optional(),
  primaryColor: z.string().nullable().optional(),
  secondaryColor: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBusinessInfo */
IdentityTenantsTenantBusinessInfoSchema = z.object({
  complianceRequirements: z.array(z.string()).nullable().optional(),
  geographicRegion: z.string().nullable().optional(),
  industry: z.string().nullable().optional(),
  organizationSize: z.string().nullable().optional(),
  tenantType: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantBusinessRules */
IdentityTenantsTenantBusinessRulesSchema = z.object({
  approvalRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  notificationRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  validationRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  workflowRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantContactInfo */
IdentityTenantsTenantContactInfoSchema = z.object({
  address: z.lazy(() => IdentityTenantsTenantAddressSchema).optional(),
  organizationName: z.string().nullable().optional(),
  primaryContactEmail: z.string().nullable().optional(),
  primaryContactName: z.string().nullable().optional(),
  primaryContactPhone: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantCurrencySettings */
IdentityTenantsTenantCurrencySettingsSchema = z.object({
  decimalPlaces: z.number().int().optional(),
  defaultCurrency: z.string().nullable().optional(),
  displayFormat: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantDomain */
IdentityTenantsTenantDomainSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  fullDomain: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isMainDomain: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isSecondaryDomain: z.boolean().optional(),
  subdomain: z.string().max(100).nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
  tenantId: z.string().uuid(),
  topLevelDomain: z.string().min(1).max(255),
  updatedAt: z.string().datetime(),
  userGroupId: z.string().uuid().nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantIntegrationSettings */
IdentityTenantsTenantIntegrationSettingsSchema = z.object({
  apiKeys: z.record(z.string(), z.string()).nullable().optional(),
  externalServices: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  ssoConfiguration: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  webhookSettings: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantMember */
IdentityTenantsTenantMemberSchema = z.object({
  childMembers: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leaveReason: z.string().max(500).nullable().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  parentMember: z.lazy(() => IdentityTenantsTenantMemberSchema).optional(),
  parentMemberId: z.string().uuid().nullable().optional(),
  role: z.string().min(1).max(100),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
  tenantId: z.string().uuid(),
  updatedAt: z.string().datetime(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantMetadata */
IdentityTenantsTenantMetadataSchema = z.object({
  adminNotes: z.string().nullable().optional(),
  businessInfo: z.lazy(() => IdentityTenantsTenantBusinessInfoSchema).optional(),
  contactInfo: z.lazy(() => IdentityTenantsTenantContactInfoSchema).optional(),
  createdAt: z.string().datetime().optional(),
  customFields: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  id: z.string().uuid().optional(),
  tags: z.array(z.string()).nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for IdentityTenantsTenantSecuritySettings */
IdentityTenantsTenantSecuritySettingsSchema = z.object({
  apiRateLimits: z.record(z.string(), z.number().int()).nullable().optional(),
  ipWhitelist: z.array(z.string()).nullable().optional(),
  passwordPolicy: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  sessionTimeout: z.number().int().optional(),
  twoFactorRequired: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsTenantSettings */
IdentityTenantsTenantSettingsSchema = z.object({
  allowUserRegistration: z.boolean().optional(),
  brandingSettings: z.string().max(5000).nullable().optional(),
  createdAt: z.string().datetime(),
  defaultCurrency: z.string().max(3).nullable().optional(),
  defaultLanguage: z.string().max(10).nullable().optional(),
  defaultTimezone: z.string().max(50).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  enableApiAccess: z.boolean().optional(),
  enableAuditLogging: z.boolean().optional(),
  id: z.string().uuid().optional(),
  integrationSettingsJson: z.string().nullable().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  maxUsers: z.number().int().nullable().optional(),
  notificationSettings: z.string().max(5000).nullable().optional(),
  requireRegistrationApproval: z.boolean().optional(),
  requireTwoFactorAuth: z.boolean().optional(),
  securitySettings: z.string().max(5000).nullable().optional(),
  storageQuota: z.number().int().nullable().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
  tenantId: z.string().uuid(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantSettingsDto */
IdentityTenantsTenantSettingsDtoSchema = z.object({
  businessRules: z.lazy(() => IdentityTenantsTenantBusinessRulesSchema).optional(),
  createdAt: z.string().datetime().optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  id: z.string().uuid().optional(),
  integrationSettings: z.lazy(() => IdentityTenantsTenantIntegrationSettingsSchema).optional(),
  securitySettings: z.lazy(() => IdentityTenantsTenantSecuritySettingsSchema).optional(),
  systemConfiguration: z.lazy(() => IdentityTenantsTenantSystemConfigurationSchema).optional(),
  systemLimits: z.lazy(() => IdentityTenantsTenantSystemLimitsSchema).optional(),
  updatedAt: z.string().datetime().optional(),
  userInterfaceSettings: z.lazy(() => IdentityTenantsTenantUiSettingsSchema).optional(),
});

/** Zod schema for IdentityTenantsTenantStatistics */
IdentityTenantsTenantStatisticsSchema = z.object({
  activeMembers: z.number().int().optional(),
  apiCalls: z.number().int().optional(),
  createdAt: z.string().datetime(),
  customMetrics: z.string().max(10000).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  inactiveMembers: z.number().int().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  membersLeft: z.number().int().optional(),
  newMembers: z.number().int().optional(),
  statisticDate: z.string().datetime().optional(),
  storageUsed: z.number().int().optional(),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
  tenantId: z.string().uuid(),
  totalMembers: z.number().int().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantSystemConfiguration */
IdentityTenantsTenantSystemConfigurationSchema = z.object({
  currencySettings: z.lazy(() => IdentityTenantsTenantCurrencySettingsSchema).optional(),
  customConfiguration: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  numberFormat: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantSystemLimits */
IdentityTenantsTenantSystemLimitsSchema = z.object({
  customLimits: z.record(z.string(), z.number().int()).nullable().optional(),
  maxApiCalls: z.number().int().optional(),
  maxProjects: z.number().int().optional(),
  maxStorage: z.number().int().optional(),
  maxUsers: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsTenantUiSettings */
IdentityTenantsTenantUiSettingsSchema = z.object({
  branding: z.lazy(() => IdentityTenantsTenantBrandingSchema).optional(),
  componentSettings: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  customCss: z.string().nullable().optional(),
  layout: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  theme: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantValidationError */
IdentityTenantsTenantValidationErrorSchema = z.object({
  code: z.string().nullable().optional(),
  field: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsTenantValidationOutput */
IdentityTenantsTenantValidationOutputSchema = z.object({
  errors: z
    .array(z.lazy(() => IdentityTenantsTenantValidationErrorSchema))
    .nullable()
    .optional(),
  isValid: z.boolean().optional(),
  slugValidation: z.lazy(() => IdentityTenantsSlugValidationSchema).optional(),
  suggestions: z.array(z.string()).nullable().optional(),
  warnings: z
    .array(z.lazy(() => IdentityTenantsTenantValidationWarningSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityTenantsTenantValidationWarning */
IdentityTenantsTenantValidationWarningSchema = z.object({
  code: z.string().nullable().optional(),
  field: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantAddressInput */
IdentityTenantsUpdateTenantAddressInputSchema = z.object({
  city: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  street: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBrandingInput */
IdentityTenantsUpdateTenantBrandingInputSchema = z.object({
  companyName: z.string().nullable().optional(),
  faviconUrl: z.string().nullable().optional(),
  logoUrl: z.string().nullable().optional(),
  primaryColor: z.string().nullable().optional(),
  secondaryColor: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBusinessInfoInput */
IdentityTenantsUpdateTenantBusinessInfoInputSchema = z.object({
  complianceRequirements: z.array(z.string()).nullable().optional(),
  geographicRegion: z.string().nullable().optional(),
  industry: z.string().nullable().optional(),
  organizationSize: z.string().nullable().optional(),
  tenantType: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantBusinessRulesInput */
IdentityTenantsUpdateTenantBusinessRulesInputSchema = z.object({
  approvalRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  notificationRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  validationRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  workflowRules: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantContactInfoInput */
IdentityTenantsUpdateTenantContactInfoInputSchema = z.object({
  address: z.lazy(() => IdentityTenantsUpdateTenantAddressInputSchema).optional(),
  organizationName: z.string().nullable().optional(),
  primaryContactEmail: z.string().nullable().optional(),
  primaryContactName: z.string().nullable().optional(),
  primaryContactPhone: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantCurrencySettingsInput */
IdentityTenantsUpdateTenantCurrencySettingsInputSchema = z.object({
  decimalPlaces: z.number().int().nullable().optional(),
  defaultCurrency: z.string().nullable().optional(),
  displayFormat: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantFeatureFlagsInput */
IdentityTenantsUpdateTenantFeatureFlagsInputSchema = z.object({
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantIntegrationSettingsInput */
IdentityTenantsUpdateTenantIntegrationSettingsInputSchema = z.object({
  apiKeys: z.record(z.string(), z.string()).nullable().optional(),
  externalServices: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  ssoConfiguration: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  webhookSettings: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMemberInviteOutput */
IdentityTenantsUpdateTenantMemberInviteOutputSchema = z.object({
  inviteStatus: z.string().nullable().optional(),
  memberId: z.string().uuid().nullable().optional(),
  message: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMemberRoleOutput */
IdentityTenantsUpdateTenantMemberRoleOutputSchema = z.object({
  memberId: z.string().uuid().optional(),
  message: z.string().nullable().optional(),
  newRole: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantMetadataInput */
IdentityTenantsUpdateTenantMetadataInputSchema = z.object({
  adminNotes: z.string().nullable().optional(),
  businessInfo: z.lazy(() => IdentityTenantsUpdateTenantBusinessInfoInputSchema).optional(),
  contactInfo: z.lazy(() => IdentityTenantsUpdateTenantContactInfoInputSchema).optional(),
  customFields: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantInput */
IdentityTenantsUpdateTenantInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSecuritySettingsInput */
IdentityTenantsUpdateTenantSecuritySettingsInputSchema = z.object({
  apiRateLimits: z.record(z.string(), z.number().int()).nullable().optional(),
  ipWhitelist: z.array(z.string()).nullable().optional(),
  passwordPolicy: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  sessionTimeout: z.number().int().nullable().optional(),
  twoFactorRequired: z.boolean().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSettingsInput */
IdentityTenantsUpdateTenantSettingsInputSchema = z.object({
  businessRules: z.lazy(() => IdentityTenantsUpdateTenantBusinessRulesInputSchema).optional(),
  featureFlags: z.record(z.string(), z.boolean()).nullable().optional(),
  integrationSettings: z.lazy(() => IdentityTenantsUpdateTenantIntegrationSettingsInputSchema).optional(),
  securitySettings: z.lazy(() => IdentityTenantsUpdateTenantSecuritySettingsInputSchema).optional(),
  systemConfiguration: z.lazy(() => IdentityTenantsUpdateTenantSystemConfigurationInputSchema).optional(),
  systemLimits: z.lazy(() => IdentityTenantsUpdateTenantSystemLimitsInputSchema).optional(),
  userInterfaceSettings: z.lazy(() => IdentityTenantsUpdateTenantUiSettingsInputSchema).optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSystemConfigurationInput */
IdentityTenantsUpdateTenantSystemConfigurationInputSchema = z.object({
  currencySettings: z.lazy(() => IdentityTenantsUpdateTenantCurrencySettingsInputSchema).optional(),
  customConfiguration: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  locale: z.string().nullable().optional(),
  numberFormat: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantSystemLimitsInput */
IdentityTenantsUpdateTenantSystemLimitsInputSchema = z.object({
  customLimits: z.record(z.string(), z.number().int()).nullable().optional(),
  maxApiCalls: z.number().int().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  maxStorage: z.number().int().nullable().optional(),
  maxUsers: z.number().int().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantTagsInput */
IdentityTenantsUpdateTenantTagsInputSchema = z.object({
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateTenantUiSettingsInput */
IdentityTenantsUpdateTenantUiSettingsInputSchema = z.object({
  branding: z.lazy(() => IdentityTenantsUpdateTenantBrandingInputSchema).optional(),
  componentSettings: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  customCss: z.string().nullable().optional(),
  layout: z.record(z.string(), z.record(z.string(), z.unknown()).nullable()).nullable().optional(),
  theme: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateUserMembershipInviteInput */
IdentityTenantsUpdateUserMembershipInviteInputSchema = z.object({
  actorEmail: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUpdateUserMembershipRoleInput */
IdentityTenantsUpdateUserMembershipRoleInputSchema = z.object({
  role: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsUsageTracking */
IdentityTenantsUsageTrackingSchema = z.object({
  cost: z.number().optional(),
  createdAt: z.string().datetime(),
  date: z.string().datetime().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  metadata: z.string().max(4000).nullable().optional(),
  resourceType: z.string().min(1).max(100),
  tenant: z.lazy(() => IdentityTenantsTenantSchema).optional(),
  tenantId: z.string().uuid(),
  unit: z.string().max(50).nullable().optional(),
  updatedAt: z.string().datetime(),
  usageAmount: z.number().int().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityTenantsUserMembership */
IdentityTenantsUserMembershipSchema = z.object({
  acceptedAt: z.string().datetime().nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  inviteResendCount: z.number().int().optional(),
  inviteStatus: z.string().nullable().optional(),
  invitedAt: z.string().datetime().nullable().optional(),
  invitedByEmail: z.string().nullable().optional(),
  inviteeEmail: z.string().nullable().optional(),
  inviteeName: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  lastInviteSentAt: z.string().datetime().nullable().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  membershipId: z.string().uuid().optional(),
  role: z.string().nullable().optional(),
  tenantDescription: z.string().nullable().optional(),
  tenantId: z.string().uuid().optional(),
  tenantIsActive: z.boolean().optional(),
  tenantName: z.string().nullable().optional(),
  tenantSlug: z.string().nullable().optional(),
});

/** Zod schema for IdentityTenantsValidateTenantInput */
IdentityTenantsValidateTenantInputSchema = z.object({
  adminEmail: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersBulkActivateUsersInput */
IdentityUsersBulkActivateUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkActivateUsersOutput */
IdentityUsersBulkActivateUsersOutputSchema = z.object({
  activatedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkCreateUsersInput */
IdentityUsersBulkCreateUsersInputSchema = z.object({
  users: z
    .array(z.lazy(() => IdentityUsersCreateUserRequestItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersBulkCreateUsersOutput */
IdentityUsersBulkCreateUsersOutputSchema = z.object({
  createdUserIds: z.array(z.string().uuid()).nullable().optional(),
  failedEmails: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeactivateUsersInput */
IdentityUsersBulkDeactivateUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeactivateUsersOutput */
IdentityUsersBulkDeactivateUsersOutputSchema = z.object({
  deactivatedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkDeleteUsersInput */
IdentityUsersBulkDeleteUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkNotificationInput */
IdentityUsersBulkNotificationInputSchema = z.object({
  filterCriteria: z.lazy(() => IdentityUsersNotificationFilterCriteriaSchema).optional(),
  notificationIds: z.array(z.string().uuid()).nullable().optional(),
  operation: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersBulkPurgeUsersInput */
IdentityUsersBulkPurgeUsersInputSchema = z.object({
  strategy: z.lazy(() => IdentityUsersPurgeStrategySchema).optional(),
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkRestoreUsersInput */
IdentityUsersBulkRestoreUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkRestoreUsersOutput */
IdentityUsersBulkRestoreUsersOutputSchema = z.object({
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
  restoredUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersBulkSuspendUsersInput */
IdentityUsersBulkSuspendUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkSuspendUsersOutput */
IdentityUsersBulkSuspendUsersOutputSchema = z.object({
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
  suspendedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersBulkUnsuspendUsersInput */
IdentityUsersBulkUnsuspendUsersInputSchema = z.object({
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for IdentityUsersBulkUnsuspendUsersOutput */
IdentityUsersBulkUnsuspendUsersOutputSchema = z.object({
  failedUserIds: z.array(z.string().uuid()).nullable().optional(),
  unsuspendedUsers: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersBulkUpdateUsersInput */
IdentityUsersBulkUpdateUsersInputSchema = z.object({
  updates: z
    .array(z.lazy(() => IdentityUsersUpdateUserRequestItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersCreateUserInput */
IdentityUsersCreateUserInputSchema = z.object({
  email: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersCreateUserRequestItem */
IdentityUsersCreateUserRequestItemSchema = z.object({
  email: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersNotificationAction */
IdentityUsersNotificationActionSchema = z.object({
  id: z.string().nullable().optional(),
  isPrimary: z.boolean().optional(),
  text: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersNotificationFilterCriteria */
IdentityUsersNotificationFilterCriteriaSchema = z.object({
  categories: z.array(z.string()).nullable().optional(),
  dateFrom: z.string().datetime().nullable().optional(),
  dateTo: z.string().datetime().nullable().optional(),
  isArchived: z.boolean().nullable().optional(),
  isRead: z.boolean().nullable().optional(),
  priorities: z.array(z.string()).nullable().optional(),
  types: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersNotificationPriority */
IdentityUsersNotificationPrioritySchema = z.enum(['Low', 'Normal', 'High', 'Urgent', 'Critical']);

/** Zod schema for IdentityUsersProfileVisibility */
IdentityUsersProfileVisibilitySchema = z.enum(['Private', 'FriendsOnly', 'Public']);

/** Zod schema for IdentityUsersPurgeStrategy */
IdentityUsersPurgeStrategySchema = z.enum(['Immediate', 'Scheduled', 'GracePeriod']);

/** Zod schema for IdentityUsersReplaceUserAccessibilityPreferencesInput */
IdentityUsersReplaceUserAccessibilityPreferencesInputSchema = z.object({
  accessibilityPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserLocalizationPreferencesInput */
IdentityUsersReplaceUserLocalizationPreferencesInputSchema = z.object({
  localizationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserMetadataInput */
IdentityUsersReplaceUserMetadataInputSchema = z.object({
  customFields: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  tags: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserNotificationPreferencesInput */
IdentityUsersReplaceUserNotificationPreferencesInputSchema = z.object({
  notificationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserPreferencesInput */
IdentityUsersReplaceUserPreferencesInputSchema = z.object({
  accessibilityPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  generalPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  notificationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  privacyPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserPrivacyPreferencesInput */
IdentityUsersReplaceUserPrivacyPreferencesInputSchema = z.object({
  privacyPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersReplaceUserProfileInput */
IdentityUsersReplaceUserProfileInputSchema = z.object({
  bio: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  jobTitle: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().optional(),
  showLocation: z.boolean().optional(),
  timeZone: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserAccessibilityPreferencesInput */
IdentityUsersUpdateUserAccessibilityPreferencesInputSchema = z.object({
  accessibilityPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserLocalizationPreferencesInput */
IdentityUsersUpdateUserLocalizationPreferencesInputSchema = z.object({
  localizationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserMetadataInput */
IdentityUsersUpdateUserMetadataInputSchema = z.object({
  customFields: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  tagsToAdd: z.array(z.string()).nullable().optional(),
  tagsToRemove: z.array(z.string()).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserNotificationPreferencesInput */
IdentityUsersUpdateUserNotificationPreferencesInputSchema = z.object({
  notificationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserPreferencesInput */
IdentityUsersUpdateUserPreferencesInputSchema = z.object({
  accessibilityPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  generalPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  notificationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  privacyPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserPrivacyPreferencesInput */
IdentityUsersUpdateUserPrivacyPreferencesInputSchema = z.object({
  privacyPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserProfileInput */
IdentityUsersUpdateUserProfileInputSchema = z.object({
  bio: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  jobTitle: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().nullable().optional(),
  showLocation: z.boolean().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserInput */
IdentityUsersUpdateUserInputSchema = z.object({
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUpdateUserRequestItem */
IdentityUsersUpdateUserRequestItemSchema = z.object({
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for IdentityUsersUser */
IdentityUsersUserSchema = z.object({
  canPerformActions: z.boolean().optional(),
  canSignIn: z.boolean().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  email: z.string().email().min(1).max(255),
  hasPassword: z.boolean().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isEmailVerified: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isSuspended: z.boolean().optional(),
  lastLoginAt: z.string().datetime().nullable().optional(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  metadata: z.lazy(() => IdentityUsersUserMetadataSchema).optional(),
  name: z.string().min(1).max(100),
  notifications: z
    .array(z.lazy(() => IdentityUsersUserNotificationSchema))
    .nullable()
    .optional(),
  passwordHash: z.string().max(512).nullable().optional(),
  phoneNumber: z.string().max(20).nullable().optional(),
  preferences: z.lazy(() => IdentityUsersUserPreferencesSchema).optional(),
  profile: z.lazy(() => IdentityUsersUserProfileSchema).optional(),
  status: z.lazy(() => IdentityUsersUserStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  tenantMemberships: z
    .array(z.lazy(() => IdentityTenantsTenantMemberSchema))
    .nullable()
    .optional(),
  tokenVersion: z.number().int().optional(),
  updatedAt: z.string().datetime(),
  username: z.string().max(256).nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityUsersUserAccessibilityPreferences */
IdentityUsersUserAccessibilityPreferencesSchema = z.object({
  colorScheme: z.string().nullable().optional(),
  customSettings: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  fontSize: z.number().int().optional(),
  highContrast: z.boolean().optional(),
  keyboardNavigation: z.boolean().optional(),
  largeText: z.boolean().optional(),
  reducedMotion: z.boolean().optional(),
  screenReader: z.boolean().optional(),
});

/** Zod schema for IdentityUsersUserDto */
IdentityUsersUserDtoSchema = z.object({
  createdAt: z.string().datetime().optional(),
  email: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  lastSeenAt: z.string().datetime().nullable().optional(),
  name: z.string().nullable().optional(),
  phoneNumber: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for IdentityUsersUserLocalizationPreferences */
IdentityUsersUserLocalizationPreferencesSchema = z.object({
  currency: z.string().nullable().optional(),
  customSettings: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  dateFormat: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  numberFormat: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  timeFormat: z.string().nullable().optional(),
  timezone: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserMetadata */
IdentityUsersUserMetadataSchema = z.object({
  createdAt: z.string().datetime(),
  customFields: z.string().max(50000).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  externalReferences: z.string().max(25000).nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  notes: z.string().max(2000).nullable().optional(),
  tags: z.string().max(10000).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityUsersUserMetadataDto */
IdentityUsersUserMetadataDtoSchema = z.object({
  createdAt: z.string().datetime().optional(),
  customFields: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  externalReferences: z.record(z.string(), z.string()).nullable().optional(),
  id: z.string().uuid().optional(),
  tags: z.array(z.string()).nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  userId: z.string().uuid().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserNotification */
IdentityUsersUserNotificationSchema = z.object({
  actionUrl: z.string().max(500).nullable().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  content: z.string().min(1).max(2000),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isArchived: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isRead: z.boolean().optional(),
  metadata: z.string().max(10000).nullable().optional(),
  priority: z.lazy(() => IdentityUsersNotificationPrioritySchema).optional(),
  readAt: z.string().datetime().nullable().optional(),
  relatedEntityId: z.string().uuid().nullable().optional(),
  relatedEntityType: z.string().max(100).nullable().optional(),
  senderId: z.string().uuid().nullable().optional(),
  source: z.string().max(100).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(200),
  type: z.string().min(1).max(50),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityUsersUserNotificationDetail */
IdentityUsersUserNotificationDetailSchema = z.object({
  actions: z
    .array(z.lazy(() => IdentityUsersNotificationActionSchema))
    .nullable()
    .optional(),
  notification: z.lazy(() => IdentityUsersUserNotificationDtoSchema).optional(),
  relatedNotifications: z
    .array(z.lazy(() => IdentityUsersUserNotificationDtoSchema))
    .nullable()
    .optional(),
});

/** Zod schema for IdentityUsersUserNotificationDto */
IdentityUsersUserNotificationDtoSchema = z.object({
  actionText: z.string().nullable().optional(),
  actionUrl: z.string().nullable().optional(),
  archivedAt: z.string().datetime().nullable().optional(),
  category: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isArchived: z.boolean().optional(),
  isRead: z.boolean().optional(),
  message: z.string().nullable().optional(),
  metadata: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  priority: z.string().nullable().optional(),
  readAt: z.string().datetime().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  userId: z.string().uuid().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserNotificationPreferences */
IdentityUsersUserNotificationPreferencesSchema = z.object({
  categoryPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  emailEnabled: z.boolean().optional(),
  frequency: z.string().nullable().optional(),
  inAppEnabled: z.boolean().optional(),
  pushEnabled: z.boolean().optional(),
  quietHours: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  smsEnabled: z.boolean().optional(),
});

/** Zod schema for IdentityUsersUserPreferences */
IdentityUsersUserPreferencesSchema = z.object({
  accessibilityPreferences: z.string().max(10000).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  generalPreferences: z.string().max(10000).nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  localizationPreferences: z.string().max(10000).nullable().optional(),
  notificationPreferences: z.string().max(10000).nullable().optional(),
  privacyPreferences: z.string().max(10000).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for IdentityUsersUserPreferencesDto */
IdentityUsersUserPreferencesDtoSchema = z.object({
  accessibilityPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  createdAt: z.string().datetime().optional(),
  generalPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  id: z.string().uuid().optional(),
  localizationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  notificationPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  privacyPreferences: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  userId: z.string().uuid().optional(),
  version: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserPrivacyPreferences */
IdentityUsersUserPrivacyPreferencesSchema = z.object({
  activityTracking: z.boolean().optional(),
  analyticsCookies: z.boolean().optional(),
  customSettings: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  dataCollection: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  marketingEmails: z.boolean().optional(),
  personalizedContent: z.boolean().optional(),
  profileVisibility: z.string().nullable().optional(),
  thirdPartySharing: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
});

/** Zod schema for IdentityUsersUserProfile */
IdentityUsersUserProfileSchema = z.object({
  avatarUrl: z.string().max(500).nullable().optional(),
  bannerUrl: z.string().max(500).nullable().optional(),
  bio: z.string().max(1000).nullable().optional(),
  company: z.string().max(100).nullable().optional(),
  createdAt: z.string().datetime(),
  dateOfBirth: z.string().date().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  displayName: z.string().max(100).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  gender: z.string().max(20).nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isVerified: z.boolean().optional(),
  jobTitle: z.string().max(100).nullable().optional(),
  location: z.string().max(100).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
  visibility: z.lazy(() => IdentityUsersProfileVisibilitySchema).optional(),
  website: z.string().max(255).nullable().optional(),
});

/** Zod schema for IdentityUsersUserProfileDto */
IdentityUsersUserProfileDtoSchema = z.object({
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  company: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  displayName: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  jobTitle: z.string().nullable().optional(),
  language: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  profileVisibility: z.string().nullable().optional(),
  showEmail: z.boolean().optional(),
  showLocation: z.boolean().optional(),
  timeZone: z.string().nullable().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  userId: z.string().uuid().optional(),
  version: z.string().nullable().optional(),
  website: z.string().nullable().optional(),
});

/** Zod schema for IdentityUsersUserStatus */
IdentityUsersUserStatusSchema = z.object({
  isActive: z.boolean().optional(),
  isSuspended: z.boolean().optional(),
});

/** Zod schema for KeyValuePairStringAuthenticationExtensionsPRFValues */
KeyValuePairStringAuthenticationExtensionsPRFValuesSchema = z.object({
  key: z.string().nullable().optional(),
  value: z.lazy(() => ObjectsAuthenticationExtensionsPRFValuesSchema).optional(),
});

/** Zod schema for LaunchPadCreateLaunchPlanInput */
LaunchPadCreateLaunchPlanInputSchema = z.object({
  channels: z.array(z.string()).nullable().optional(),
  checklistItems: z
    .array(z.lazy(() => LaunchPadLaunchChecklistItemInputSchema))
    .nullable()
    .optional(),
  name: z.string().nullable().optional(),
  positioning: z.string().nullable().optional(),
  projectId: z.string().uuid().optional(),
  targetLaunchAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchChecklistItem */
LaunchPadLaunchChecklistItemSchema = z.object({
  category: z.string().min(1).max(100),
  completedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isComplete: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isRequired: z.boolean().optional(),
  launchPlan: z.lazy(() => LaunchPadLaunchPlanSchema).optional(),
  launchPlanId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(200),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for LaunchPadLaunchChecklistItemInput */
LaunchPadLaunchChecklistItemInputSchema = z.object({
  category: z.string().nullable().optional(),
  isComplete: z.boolean().optional(),
  isRequired: z.boolean().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LaunchPadLaunchPlan */
LaunchPadLaunchPlanSchema = z.object({
  channels: z.array(z.string()).nullable().optional(),
  checklistItems: z
    .array(z.lazy(() => LaunchPadLaunchChecklistItemSchema))
    .nullable()
    .optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  launchedAt: z.string().datetime().nullable().optional(),
  name: z.string().min(1).max(200),
  positioning: z.string().max(1000).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  readinessPercent: z.number().int().optional(),
  status: z.lazy(() => LaunchPadLaunchPlanStatusSchema).optional(),
  targetLaunchAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for LaunchPadLaunchPlanStatus */
LaunchPadLaunchPlanStatusSchema = z.enum(['Draft', 'Preparing', 'Ready', 'Launched', 'Paused']);

/** Zod schema for LearningAssessmentsAssessmentDefinition */
LearningAssessmentsAssessmentDefinitionSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  definition: z.record(z.string(), z.unknown()).optional(),
  definitionSchemaVersion: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsAssessment */
LearningAssessmentsAssessmentSchema = z.object({
  allowLateSubmissions: z.boolean().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  assessmentGroupName: z.string().nullable().optional(),
  assessmentGroupOrder: z.number().int().nullable().optional(),
  assessmentGroupWeightPercent: z.number().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isAvailable: z.boolean().optional(),
  isRequired: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  maxScore: z.number().int().optional(),
  order: z.number().int().optional(),
  passingScore: z.number().int().optional(),
  presentationMode: z.lazy(() => LearningAssessmentsAssessmentPresentationModeSchema).optional(),
  submissionModalities: z.lazy(() => LearningAssessmentsSubmissionModalitySchema).optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningAssessmentsAssessmentTypeSchema).optional(),
});

/** Zod schema for LearningAssessmentsAssessmentGroupAnalytics */
LearningAssessmentsAssessmentGroupAnalyticsSchema = z.object({
  assessmentCount: z.number().int().optional(),
  averagePercent: z.number().optional(),
  distribution: z
    .array(z.lazy(() => LearningAssessmentsAssessmentScoreBucketSchema))
    .nullable()
    .optional(),
  gradedCount: z.number().int().optional(),
  groupId: z.string().uuid().nullable().optional(),
  groupName: z.string().nullable().optional(),
  passRate: z.number().optional(),
  ungradedCount: z.number().int().optional(),
  weightPercent: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentGroup */
LearningAssessmentsAssessmentGroupSchema = z.object({
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  order: z.number().int().optional(),
  weightPercent: z.number().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentPresentationMode */
LearningAssessmentsAssessmentPresentationModeSchema = z.enum(['SingleStep', 'Continuous']);

/** Zod schema for LearningAssessmentsAssessmentScoreBucket */
LearningAssessmentsAssessmentScoreBucketSchema = z.object({
  count: z.number().int().optional(),
  label: z.string().nullable().optional(),
  maxPercent: z.number().int().optional(),
  minPercent: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentSubmission */
LearningAssessmentsAssessmentSubmissionSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  codePayload: z.string().nullable().optional(),
  enrollmentId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  gradedBy: z.string().uuid().nullable().optional(),
  id: z.string().uuid().optional(),
  isLate: z.boolean().optional(),
  mediaPayload: z.string().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  score: z.number().int().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  status: z.lazy(() => LearningAssessmentsSubmissionStatusSchema).optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  submittedModalities: z.lazy(() => LearningAssessmentsSubmissionModalitySchema).optional(),
  textPayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningAssessmentsAssessmentType */
LearningAssessmentsAssessmentTypeSchema = z.enum(['Quiz', 'Exam', 'Assignment', 'Project', 'PeerReview', 'SelfAssessment']);

/** Zod schema for LearningAssessmentsAssignAssessmentGroupInput */
LearningAssessmentsAssignAssessmentGroupInputSchema = z.object({
  assessmentGroupId: z.string().uuid().nullable().optional(),
  clearAssessmentGroup: z.boolean().optional(),
});

/** Zod schema for LearningAssessmentsCanAttemptOutput */
LearningAssessmentsCanAttemptOutputSchema = z.object({
  canAttempt: z.boolean().optional(),
  currentAttemptCount: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsCourseAssessmentAnalytics */
LearningAssessmentsCourseAssessmentAnalyticsSchema = z.object({
  assessmentCount: z.number().int().optional(),
  averagePercent: z.number().optional(),
  courseId: z.string().uuid().optional(),
  distribution: z
    .array(z.lazy(() => LearningAssessmentsAssessmentScoreBucketSchema))
    .nullable()
    .optional(),
  gradedCount: z.number().int().optional(),
  groups: z
    .array(z.lazy(() => LearningAssessmentsAssessmentGroupAnalyticsSchema))
    .nullable()
    .optional(),
  passRate: z.number().optional(),
  ungradedCount: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsCreateAssessmentGroupInput */
LearningAssessmentsCreateAssessmentGroupInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  order: z.number().int().optional(),
  weightPercent: z.number().optional(),
});

/** Zod schema for LearningAssessmentsCreateAssessmentInput */
LearningAssessmentsCreateAssessmentInputSchema = z.object({
  allowLateSubmissions: z.boolean().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  isRequired: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  maxScore: z.number().int().optional(),
  passingScore: z.number().int().optional(),
  presentationMode: z.lazy(() => LearningAssessmentsAssessmentPresentationModeSchema).optional(),
  submissionModalities: z.lazy(() => LearningAssessmentsSubmissionModalitySchema).optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningAssessmentsAssessmentTypeSchema).optional(),
});

/** Zod schema for LearningAssessmentsGradeSubmissionInput */
LearningAssessmentsGradeSubmissionInputSchema = z.object({
  feedback: z.string().nullable().optional(),
  gradedBy: z.string().uuid().nullable().optional(),
  score: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsInteractiveVideoAssessmentCue */
LearningAssessmentsInteractiveVideoAssessmentCueSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  contentId: z.string().uuid().optional(),
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
  id: z.string().uuid().optional(),
});

/** Zod schema for LearningAssessmentsLearnerAssessmentAttempt */
LearningAssessmentsLearnerAssessmentAttemptSchema = z.object({
  definition: z.record(z.string(), z.unknown()).optional(),
  definitionSchemaVersion: z.number().int().optional(),
  submission: z.lazy(() => LearningAssessmentsLearnerAssessmentSubmissionSchema).optional(),
});

/** Zod schema for LearningAssessmentsLearnerAssessmentSubmission */
LearningAssessmentsLearnerAssessmentSubmissionSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  codePayload: z.string().nullable().optional(),
  enrollmentId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isLate: z.boolean().optional(),
  mediaPayload: z.string().nullable().optional(),
  passed: z.boolean().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  score: z.number().int().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  status: z.lazy(() => LearningAssessmentsSubmissionStatusSchema).optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  submittedModalities: z.lazy(() => LearningAssessmentsSubmissionModalitySchema).optional(),
  textPayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsLearnerInteractiveVideoAssessmentCue */
LearningAssessmentsLearnerInteractiveVideoAssessmentCueSchema = z.object({
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsLinkInteractiveVideoCueInput */
LearningAssessmentsLinkInteractiveVideoCueInputSchema = z.object({
  contentId: z.string().uuid().optional(),
  cueId: z.string().nullable().optional(),
  cuePositionSeconds: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsStartSubmissionInput */
LearningAssessmentsStartSubmissionInputSchema = z.object({
  enrollmentId: z.string().uuid().optional(),
});

/** Zod schema for LearningAssessmentsSubmissionModality. A comma-separated combination of the declared flag names. */
LearningAssessmentsSubmissionModalitySchema = z.string();

/** Zod schema for LearningAssessmentsSubmissionStatus */
LearningAssessmentsSubmissionStatusSchema = z.enum(['InProgress', 'Submitted', 'Graded', 'Returned', 'Late']);

/** Zod schema for LearningAssessmentsSubmitAssessmentInput */
LearningAssessmentsSubmitAssessmentInputSchema = z.object({
  codePayload: z.string().nullable().optional(),
  filePayload: z.string().nullable().optional(),
  mediaPayload: z.string().nullable().optional(),
  projectPayload: z.string().nullable().optional(),
  structuredAnswerPayload: z.string().nullable().optional(),
  textPayload: z.string().nullable().optional(),
  urlPayload: z.string().nullable().optional(),
});

/** Zod schema for LearningAssessmentsUpdateAssessmentDefinitionInput */
LearningAssessmentsUpdateAssessmentDefinitionInputSchema = z.object({
  definition: z.record(z.string(), z.unknown()).optional(),
  definitionSchemaVersion: z.number().int().optional(),
});

/** Zod schema for LearningAssessmentsUpdateAssessmentGroupInput */
LearningAssessmentsUpdateAssessmentGroupInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  order: z.number().int().nullable().optional(),
  weightPercent: z.number().nullable().optional(),
});

/** Zod schema for LearningAssessmentsUpdateAssessmentInput */
LearningAssessmentsUpdateAssessmentInputSchema = z.object({
  allowLateSubmissions: z.boolean().nullable().optional(),
  assessmentGroupId: z.string().uuid().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  clearAssessmentGroupId: z.boolean().optional(),
  clearContentId: z.boolean().optional(),
  clearDueAt: z.boolean().optional(),
  clearLateSubmissionDeadline: z.boolean().optional(),
  contentId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  isRequired: z.boolean().nullable().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  maxScore: z.number().int().nullable().optional(),
  passingScore: z.number().int().nullable().optional(),
  presentationMode: z.lazy(() => LearningAssessmentsAssessmentPresentationModeSchema).optional(),
  submissionModalities: z.lazy(() => LearningAssessmentsSubmissionModalitySchema).optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesCertificate */
LearningCertificatesCertificateSchema = z.object({
  certificateNumber: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  courseName: z.string().nullable().optional(),
  enrollmentId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  issuedAt: z.string().datetime().optional(),
  recipientName: z.string().nullable().optional(),
  status: z.lazy(() => LearningCertificatesCertificateStatusSchema).optional(),
  templateId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningCertificatesCertificateStatus */
LearningCertificatesCertificateStatusSchema = z.enum(['Active', 'Expired', 'Revoked']);

/** Zod schema for LearningCertificatesCertificateTemplateDetail */
LearningCertificatesCertificateTemplateDetailSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDefault: z.boolean().optional(),
  name: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
  templateStyles: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCertificatesCertificateTemplate */
LearningCertificatesCertificateTemplateSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDefault: z.boolean().optional(),
  name: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCertificatesCertificateVerificationResult */
LearningCertificatesCertificateVerificationResultSchema = z.object({
  certificateNumber: z.string().nullable().optional(),
  courseName: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  isValid: z.boolean().optional(),
  issuedAt: z.string().datetime().optional(),
  message: z.string().nullable().optional(),
  recipientName: z.string().nullable().optional(),
  status: z.lazy(() => LearningCertificatesCertificateStatusSchema).optional(),
});

/** Zod schema for LearningCertificatesCreateCertificateTemplateInput */
LearningCertificatesCreateCertificateTemplateInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesIssueCertificateInput */
LearningCertificatesIssueCertificateInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  templateId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningCertificatesRevokeCertificateInput */
LearningCertificatesRevokeCertificateInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for LearningCertificatesUpdateCertificateTemplateInput */
LearningCertificatesUpdateCertificateTemplateInputSchema = z.object({
  description: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  isDefault: z.boolean().optional(),
  name: z.string().nullable().optional(),
  templateHtml: z.string().nullable().optional(),
  templateStyles: z.string().nullable().optional(),
});

/** Zod schema for LearningCohortsApplyCohortScheduleInput */
LearningCohortsApplyCohortScheduleInputSchema = z.object({
  confirmAdvisories: z.boolean().optional(),
  expectedVersion: z.number().int().optional(),
  rules: z.lazy(() => LearningCohortsPreviewCohortScheduleInputSchema).optional(),
});

/** Zod schema for LearningCohortsAvailableCohortContent */
LearningCohortsAvailableCohortContentSchema = z.object({
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  body: z.string().nullable().optional(),
  contentId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  instructionalWeek: z.number().int().optional(),
  parentId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
});

/** Zod schema for LearningCohortsCohortCalendarEntry */
LearningCohortsCohortCalendarEntrySchema = z.object({
  availableFrom: z.string().datetime().nullable().optional(),
  cohortId: z.string().uuid().optional(),
  cohortName: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  itemId: z.string().uuid().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCohortsCohortScheduleItemStatusSchema).optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
});

/** Zod schema for LearningCohortsCohort */
LearningCohortsCohortSchema = z.object({
  availableSpots: z.number().int().optional(),
  canEnroll: z.boolean().optional(),
  conflictCount: z.number().int().optional(),
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  currentEnrollmentCount: z.number().int().optional(),
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  isOpen: z.boolean().optional(),
  maxCapacity: z.number().int().optional(),
  meetingSchedule: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  nextMeetingAt: z.string().datetime().nullable().optional(),
  schedule: z.lazy(() => LearningCohortsCohortScheduleSummarySchema).optional(),
  startDate: z.string().datetime().optional(),
  status: z.lazy(() => LearningCohortsCohortStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCohortsCohortPacingMode */
LearningCohortsCohortPacingModeSchema = z.enum(['OneModulePerWeek', 'OneLessonPerMeeting', 'FixedLessonsPerWeek', 'Manual']);

/** Zod schema for LearningCohortsCohortReleasePolicy */
LearningCohortsCohortReleasePolicySchema = z.enum(['Weekly', 'BeforeMeeting', 'Manual', 'Immediately']);

/** Zod schema for LearningCohortsCohortScheduleConflict */
LearningCohortsCohortScheduleConflictSchema = z.object({
  assessmentId: z.string().uuid().nullable().optional(),
  code: z.string().nullable().optional(),
  message: z.string().nullable().optional(),
  programContentId: z.string().uuid().nullable().optional(),
  severity: z.lazy(() => LearningCohortsScheduleConflictSeveritySchema).optional(),
});

/** Zod schema for LearningCohortsCohortSchedule */
LearningCohortsCohortScheduleSchema = z.object({
  cohortId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  items: z
    .array(z.lazy(() => LearningCohortsCohortScheduleItemSchema))
    .nullable()
    .optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingDurationMinutes: z.number().int().optional(),
  meetingStartTime: z.string().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  releasePolicy: z.lazy(() => LearningCohortsCohortReleasePolicySchema).optional(),
  timezoneId: z.string().nullable().optional(),
  unitsPerPeriod: z.number().int().optional(),
  unscheduledContentIds: z.array(z.string().uuid()).nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for LearningCohortsCohortScheduleItem */
LearningCohortsCohortScheduleItemSchema = z.object({
  assessmentId: z.string().uuid().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  instructionalWeek: z.number().int().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  programContentId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCohortsCohortScheduleItemStatusSchema).optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
  visibilityOverride: z.lazy(() => LearningCohortsCohortVisibilityOverrideSchema).optional(),
});

/** Zod schema for LearningCohortsCohortScheduleItemStatus */
LearningCohortsCohortScheduleItemStatusSchema = z.enum(['Draft', 'Scheduled', 'Published', 'Completed', 'Cancelled']);

/** Zod schema for LearningCohortsCohortScheduleItemType */
LearningCohortsCohortScheduleItemTypeSchema = z.enum(['ContentRelease', 'LiveSession', 'AssessmentWindow', 'Milestone']);

/** Zod schema for LearningCohortsCohortSchedulePreview */
LearningCohortsCohortSchedulePreviewSchema = z.object({
  calculatedEndDate: z.string().date().optional(),
  conflicts: z
    .array(z.lazy(() => LearningCohortsCohortScheduleConflictSchema))
    .nullable()
    .optional(),
  hasBlockingConflicts: z.boolean().optional(),
  items: z
    .array(z.lazy(() => LearningCohortsCohortSchedulePreviewItemSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCohortsCohortSchedulePreviewItem */
LearningCohortsCohortSchedulePreviewItemSchema = z.object({
  assessmentId: z.string().uuid().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  instructionalWeek: z.number().int().optional(),
  programContentId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningCohortsCohortScheduleItemTypeSchema).optional(),
});

/** Zod schema for LearningCohortsCohortScheduleSummary */
LearningCohortsCohortScheduleSummarySchema = z.object({
  itemCount: z.number().int().optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingStartTime: z.string().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  releasePolicy: z.lazy(() => LearningCohortsCohortReleasePolicySchema).optional(),
  timezoneId: z.string().nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for LearningCohortsCohortStatus */
LearningCohortsCohortStatusSchema = z.enum(['Scheduled', 'Active', 'Completed', 'Cancelled']);

/** Zod schema for LearningCohortsCohortVisibilityOverride */
LearningCohortsCohortVisibilityOverrideSchema = z.enum(['Inherited', 'Hidden', 'Visible']);

/** Zod schema for LearningCohortsCourseCohortCalendar */
LearningCohortsCourseCohortCalendarSchema = z.object({
  courseId: z.string().uuid().optional(),
  entries: z
    .array(z.lazy(() => LearningCohortsCohortCalendarEntrySchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCohortsCreateCohortInput */
LearningCohortsCreateCohortInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  maxCapacity: z.number().int().optional(),
  meetingSchedule: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningCohortsPreviewCohortScheduleInput */
LearningCohortsPreviewCohortScheduleInputSchema = z.object({
  assessmentDueOffsetDays: z.number().int().optional(),
  cohortEndDate: z.string().date().optional(),
  firstInstructionalDate: z.string().date().optional(),
  meetingDays: z
    .array(z.lazy(() => SystemDayOfWeekSchema))
    .nullable()
    .optional(),
  meetingDurationMinutes: z.number().int().optional(),
  meetingStartTime: z.string().optional(),
  pacingMode: z.lazy(() => LearningCohortsCohortPacingModeSchema).optional(),
  releasePolicy: z.lazy(() => LearningCohortsCohortReleasePolicySchema).optional(),
  skippedDates: z.array(z.string().date()).nullable().optional(),
  timezoneId: z.string().nullable().optional(),
  unitsPerPeriod: z.number().int().optional(),
});

/** Zod schema for LearningCohortsScheduleConflictSeverity */
LearningCohortsScheduleConflictSeveritySchema = z.enum(['Advisory', 'Blocking']);

/** Zod schema for LearningCohortsScheduleShiftScope */
LearningCohortsScheduleShiftScopeSchema = z.enum(['Single', 'Following']);

/** Zod schema for LearningCohortsShiftCohortScheduleInput */
LearningCohortsShiftCohortScheduleInputSchema = z.object({
  days: z.number().int().optional(),
  expectedVersion: z.number().int().optional(),
  scope: z.lazy(() => LearningCohortsScheduleShiftScopeSchema).optional(),
});

/** Zod schema for LearningCohortsUpdateCohortInput */
LearningCohortsUpdateCohortInputSchema = z.object({
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  maxCapacity: z.number().int().nullable().optional(),
  meetingSchedule: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCohortsUpdateCohortScheduleItemInput */
LearningCohortsUpdateCohortScheduleItemInputSchema = z.object({
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCohortsCohortScheduleItemStatusSchema).optional(),
  title: z.string().nullable().optional(),
  visibilityOverride: z.lazy(() => LearningCohortsCohortVisibilityOverrideSchema).optional(),
});

/** Zod schema for LearningCohortsUpdateCohortScheduleInput */
LearningCohortsUpdateCohortScheduleInputSchema = z.object({
  expectedVersion: z.number().int().optional(),
  item: z.lazy(() => LearningCohortsUpdateCohortScheduleItemInputSchema).optional(),
});

/** Zod schema for LearningCoursesActivityGrade */
LearningCoursesActivityGradeSchema = z.object({
  contentInteraction: z.lazy(() => LearningCoursesContentInteractionSummarySchema).optional(),
  contentInteractionId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  feedback: z.string().nullable().optional(),
  grade: z.number().optional(),
  gradePercentage: z.string().nullable().optional(),
  gradedAt: z.string().datetime().optional(),
  grader: z.lazy(() => LearningCoursesGraderSummarySchema).optional(),
  graderProgramUserId: z.string().uuid().nullable().optional(),
  gradingDetails: z.string().nullable().optional(),
  hasFeedback: z.boolean().optional(),
  hasGradingDetails: z.boolean().optional(),
  id: z.string().uuid().optional(),
  isPassingGrade: z.boolean().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCoursesActivitySettings */
LearningCoursesActivitySettingsSchema = z.object({});

/** Zod schema for LearningCoursesCircularDependencyCheckResult */
LearningCoursesCircularDependencyCheckResultSchema = z.object({
  wouldCreateCycle: z.boolean().optional(),
});

/** Zod schema for LearningCoursesCloneProgram */
LearningCoursesCloneProgramSchema = z.object({
  newDescription: z.string().nullable().optional(),
  newTitle: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCompleteContentInput */
LearningCoursesCompleteContentInputSchema = z.object({
  contentId: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesCompleteCourseCheckoutInput */
LearningCoursesCompleteCourseCheckoutInputSchema = z.object({
  paymentMethod: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  productId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesCompleteCourseCheckoutOutput */
LearningCoursesCompleteCourseCheckoutOutputSchema = z.object({
  alreadyHadAccess: z.boolean().optional(),
  amount: z.number().optional(),
  courseId: z.string().uuid().optional(),
  currency: z.string().nullable().optional(),
  enrollmentIds: z.array(z.string().uuid()).nullable().optional(),
  entitlementId: z.string().uuid().optional(),
  learningUrl: z.string().nullable().optional(),
  paymentProviderReference: z.string().nullable().optional(),
  productId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesCompletionRates */
LearningCoursesCompletionRatesSchema = z.object({
  completionTrends: z
    .array(z.lazy(() => LearningCoursesCompletionTrendSchema))
    .nullable()
    .optional(),
  contentCompletionRates: z.record(z.string(), z.number()).nullable().optional(),
  overallCompletionRate: z.number().optional(),
  programId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesCompletionTrend */
LearningCoursesCompletionTrendSchema = z.object({
  completedCount: z.number().int().optional(),
  date: z.string().datetime().optional(),
  rate: z.number().optional(),
  totalCount: z.number().int().optional(),
});

/** Zod schema for LearningCoursesContentInteraction */
LearningCoursesContentInteractionSchema = z.object({
  canModify: z.boolean().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  completionPercentage: z.number().optional(),
  content: z.lazy(() => LearningCoursesContentSummarySchema).optional(),
  contentId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  durationInMinutes: z.number().int().optional(),
  durationInSeconds: z.number().int().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isCompleted: z.boolean().optional(),
  isSubmitted: z.boolean().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  programUser: z.lazy(() => LearningCoursesProgramUserSummarySchema).optional(),
  programUserId: z.string().uuid().optional(),
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
  submissionData: z.string().nullable().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
  timeSpentMinutes: z.number().int().nullable().optional(),
  timeSpentSeconds: z.number().int().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCoursesContentInteractionEvent */
LearningCoursesContentInteractionEventSchema = z.object({
  durationSeconds: z.number().int().nullable().optional(),
  id: z.string().uuid().optional(),
  idempotencyKey: z.string().nullable().optional(),
  interactionId: z.string().uuid().optional(),
  occurredAt: z.string().datetime().optional(),
  payload: z.string().nullable().optional(),
  positionSeconds: z.number().nullable().optional(),
  progressPercentage: z.number().nullable().optional(),
  type: z.lazy(() => LearningCoursesContentInteractionEventTypeSchema).optional(),
});

/** Zod schema for LearningCoursesContentInteractionEventType */
LearningCoursesContentInteractionEventTypeSchema = z.enum([
  'Opened',
  'Heartbeat',
  'Progressed',
  'Paused',
  'Resumed',
  'Seeked',
  'Completed',
  'QuizPresented',
  'QuizAnswered',
]);

/** Zod schema for LearningCoursesContentInteractionSummary */
LearningCoursesContentInteractionSummarySchema = z.object({
  content: z.lazy(() => LearningCoursesContentSummarySchema).optional(),
  contentId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
  status: z.string().nullable().optional(),
  student: z.lazy(() => LearningCoursesStudentSummarySchema).optional(),
  submittedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesContentProgress */
LearningCoursesContentProgressSchema = z.object({
  completedAt: z.string().datetime().nullable().optional(),
  completionPercentage: z.number().optional(),
  contentId: z.string().uuid().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesContentStats */
LearningCoursesContentStatsSchema = z.object({
  contentByType: z
    .object({
      Assignment: z.number().int(),
      Challenge: z.number().int(),
      Code: z.number().int(),
      Discussion: z.number().int(),
      Lesson: z.number().int(),
      Module: z.number().int(),
      Page: z.number().int(),
      Project: z.number().int(),
      Questionnaire: z.number().int(),
      Reflection: z.number().int(),
      Survey: z.number().int(),
    })
    .nullable()
    .optional(),
  contentByVisibility: z
    .object({
      Internal: z.number().int(),
      Private: z.number().int(),
      Public: z.number().int(),
      Restricted: z.number().int(),
    })
    .nullable()
    .optional(),
  nestedContent: z.number().int().optional(),
  optionalContent: z.number().int().optional(),
  programId: z.string().uuid().optional(),
  requiredContent: z.number().int().optional(),
  topLevelContent: z.number().int().optional(),
  totalContent: z.number().int().optional(),
});

/** Zod schema for LearningCoursesContentSummary */
LearningCoursesContentSummarySchema = z.object({
  contentType: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  id: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCourseSupportTicketMessageInput */
LearningCoursesCourseSupportTicketMessageInputSchema = z.object({
  isInternal: z.boolean().optional(),
  message: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreateActivityGrade */
LearningCoursesCreateActivityGradeSchema = z.object({
  contentInteractionId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  grade: z.number().optional(),
  graderProgramUserId: z.string().uuid().optional(),
  gradingDetails: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreatePrerequisiteApiInput */
LearningCoursesCreatePrerequisiteApiInputSchema = z.object({
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  minimumGrade: z.number().int().nullable().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
});

/** Zod schema for LearningCoursesCreateProductFromProgram */
LearningCoursesCreateProductFromProgramSchema = z.object({
  basePrice: z.number().optional(),
  currency: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesCreateProgramContent */
LearningCoursesCreateProgramContentSchema = z.object({
  activitySettings: z.lazy(() => LearningCoursesActivitySettingsSchema).optional(),
  body: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  gradingMethod: z.lazy(() => LearningCoursesGradingMethodSchema).optional(),
  isRequired: z.boolean().optional(),
  lessonFormat: z.lazy(() => LearningCoursesLessonContentFormatSchema).optional(),
  maxPoints: z.number().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
  programId: z.string().uuid(),
  sortOrder: z.number().int().optional(),
  title: z.string().min(0).max(255),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesCreateProgram */
LearningCoursesCreateProgramSchema = z.object({
  creatorId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesEngagementMetrics */
LearningCoursesEngagementMetricsSchema = z.object({
  averageSessionDuration: z.string().optional(),
  contentEngagement: z.record(z.string(), z.number().int()).nullable().optional(),
  dailyActiveUsers: z.number().int().optional(),
  monthlyActiveUsers: z.number().int().optional(),
  programId: z.string().uuid().optional(),
  retentionRate: z.number().optional(),
  totalSessions: z.number().int().optional(),
  weeklyActiveUsers: z.number().int().optional(),
});

/** Zod schema for LearningCoursesEnrollmentStatus */
LearningCoursesEnrollmentStatusSchema = z.enum(['Open', 'Active', 'Paused', 'Cancelled', 'Expired', 'Completed', 'Closed', 'InviteOnly', 'Waitlist']);

/** Zod schema for LearningCoursesGradeStatistics */
LearningCoursesGradeStatisticsSchema = z.object({
  averageGrade: z.number().optional(),
  averageGradeFormatted: z.string().nullable().optional(),
  hasGrades: z.boolean().optional(),
  maxGrade: z.number().optional(),
  minGrade: z.number().optional(),
  passingRate: z.number().optional(),
  passingRateFormatted: z.string().nullable().optional(),
  totalGrades: z.number().int().optional(),
});

/** Zod schema for LearningCoursesGraderSummary */
LearningCoursesGraderSummarySchema = z.object({
  id: z.string().uuid().optional(),
  role: z.string().nullable().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesGradingMethod */
LearningCoursesGradingMethodSchema = z.enum(['None', 'Instructor', 'Peer', 'Ai', 'AutomatedTests']);

/** Zod schema for LearningCoursesLessonContentFormat */
LearningCoursesLessonContentFormatSchema = z.enum(['Markdown', 'Lexical', 'RevealJs', 'Video']);

/** Zod schema for LearningCoursesMonetization */
LearningCoursesMonetizationSchema = z.object({
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().optional(),
  price: z.number().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesMoveContent */
LearningCoursesMoveContentSchema = z.object({
  contentId: z.string().uuid(),
  newParentId: z.string().uuid().nullable().optional(),
  newSortOrder: z.number().int(),
});

/** Zod schema for LearningCoursesPrerequisiteCheckResult */
LearningCoursesPrerequisiteCheckResultSchema = z.object({
  isSatisfied: z.boolean().optional(),
  prerequisites: z
    .array(z.lazy(() => LearningCoursesPrerequisiteStatusSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningCoursesPrerequisite */
LearningCoursesPrerequisiteSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  id: z.string().uuid().optional(),
  minimumGrade: z.number().int().nullable().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  prerequisiteCourseName: z.string().nullable().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
});

/** Zod schema for LearningCoursesPrerequisiteStatus */
LearningCoursesPrerequisiteStatusSchema = z.object({
  achievedGrade: z.number().int().nullable().optional(),
  courseName: z.string().nullable().optional(),
  isSatisfied: z.boolean().optional(),
  prerequisiteCourseId: z.string().uuid().optional(),
  prerequisiteId: z.string().uuid().optional(),
  reason: z.string().nullable().optional(),
  requiredGrade: z.number().int().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
});

/** Zod schema for LearningCoursesPrerequisiteType */
LearningCoursesPrerequisiteTypeSchema = z.enum(['Required', 'Recommended', 'Corequisite']);

/** Zod schema for LearningCoursesPricing */
LearningCoursesPricingSchema = z.object({
  currency: z.string().nullable().optional(),
  isMonetizationEnabled: z.boolean().optional(),
  isSubscription: z.boolean().optional(),
  price: z.number().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesProgramAnalytics */
LearningCoursesProgramAnalyticsSchema = z.object({
  activeUsers: z.number().int().optional(),
  additionalMetrics: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  averageCompletionTime: z.string().optional(),
  completedUsers: z.number().int().optional(),
  completionRate: z.number().optional(),
  lastActivity: z.string().datetime().nullable().optional(),
  programId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
  totalUsers: z.number().int().optional(),
  totalViews: z.number().int().optional(),
});

/** Zod schema for LearningCoursesProgramContent */
LearningCoursesProgramContentSchema = z.object({
  activitySettings: z.lazy(() => LearningCoursesActivitySettingsSchema).optional(),
  body: z.record(z.string(), z.unknown()).nullable().optional(),
  children: z
    .array(z.lazy(() => LearningCoursesProgramContentSchema))
    .nullable()
    .optional(),
  childrenCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  gradingMethod: z.lazy(() => LearningCoursesGradingMethodSchema).optional(),
  id: z.string().uuid().optional(),
  isRequired: z.boolean().optional(),
  lessonFormat: z.lazy(() => LearningCoursesLessonContentFormatSchema).optional(),
  maxPoints: z.number().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
  parentTitle: z.string().nullable().optional(),
  programId: z.string().uuid().optional(),
  programTitle: z.string().nullable().optional(),
  sortOrder: z.number().int().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesProgramContentType. Legacy values Page and Challenge are normalized on read and are not valid for new content. */
LearningCoursesProgramContentTypeSchema = z.enum(['Lesson', 'Assignment', 'Questionnaire', 'Discussion', 'Code', 'Reflection', 'Survey', 'Project', 'Module']);

/** Zod schema for LearningCoursesProgramDifficulty */
LearningCoursesProgramDifficultySchema = z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert']);

/** Zod schema for LearningCoursesProgram */
LearningCoursesProgramSchema = z.object({
  averageRating: z.number().optional(),
  category: z.lazy(() => ProgramCategorySchema).optional(),
  createdAt: z.string().datetime().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  currentEnrollments: z.number().int().optional(),
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningCoursesProgramDifficultySchema).optional(),
  enrollmentDeadline: z.string().datetime().nullable().optional(),
  enrollmentStatus: z.lazy(() => LearningCoursesEnrollmentStatusSchema).optional(),
  estimatedHours: z.number().int().nullable().optional(),
  id: z.string().uuid().optional(),
  isEnrollmentOpen: z.boolean().optional(),
  maxEnrollments: z.number().int().nullable().optional(),
  metadata: z.string().nullable().optional(),
  skillsProvided: z.string().nullable().optional(),
  skillsRequired: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  totalRatings: z.number().int().optional(),
  updatedAt: z.string().datetime().nullable().optional(),
  videoShowcaseUrl: z.string().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesProgramUserSummary */
LearningCoursesProgramUserSummarySchema = z.object({
  id: z.string().uuid().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesProgressStatus */
LearningCoursesProgressStatusSchema = z.enum(['NotStarted', 'InProgress', 'Completed', 'Submitted']);

/** Zod schema for LearningCoursesRecordContentInteractionEventInput */
LearningCoursesRecordContentInteractionEventInputSchema = z.object({
  durationSeconds: z.number().int().nullable().optional(),
  idempotencyKey: z.string().nullable().optional(),
  occurredAt: z.string().datetime().nullable().optional(),
  payload: z.string().nullable().optional(),
  positionSeconds: z.number().nullable().optional(),
  progressPercentage: z.number().nullable().optional(),
  type: z.lazy(() => LearningCoursesContentInteractionEventTypeSchema).optional(),
});

/** Zod schema for LearningCoursesReflectionResponseResult */
LearningCoursesReflectionResponseResultSchema = z.object({
  body: z.string().nullable().optional(),
  respondentUserId: z.string().uuid().nullable().optional(),
  responseId: z.string().uuid().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesRejectProgram */
LearningCoursesRejectProgramSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesReorderContent */
LearningCoursesReorderContentSchema = z.object({
  contentIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCoursesReorderPrerequisitesInput */
LearningCoursesReorderPrerequisitesInputSchema = z.object({
  prerequisiteIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCoursesResolveCourseSupportTicketInput */
LearningCoursesResolveCourseSupportTicketInputSchema = z.object({
  summary: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesRevenueAnalytics */
LearningCoursesRevenueAnalyticsSchema = z.object({
  averageRevenuePerUser: z.number().optional(),
  conversionRate: z.number().optional(),
  monthlyPurchases: z.number().int().optional(),
  monthlyRevenue: z.number().optional(),
  programId: z.string().uuid().optional(),
  revenueChart: z
    .array(z.lazy(() => LearningCoursesRevenueChartSchema))
    .nullable()
    .optional(),
  totalPurchases: z.number().int().optional(),
  totalRevenue: z.number().optional(),
});

/** Zod schema for LearningCoursesRevenueChart */
LearningCoursesRevenueChartSchema = z.object({
  date: z.string().datetime().optional(),
  purchases: z.number().int().optional(),
  revenue: z.number().optional(),
});

/** Zod schema for LearningCoursesScheduleProgram */
LearningCoursesScheduleProgramSchema = z.object({
  publishAt: z.string().datetime().optional(),
});

/** Zod schema for LearningCoursesSearchContent */
LearningCoursesSearchContentSchema = z.object({
  isRequired: z.boolean().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
  programId: z.string().uuid(),
  searchTerm: z.string().min(0).max(255),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesSendCourseStudentMessageInput */
LearningCoursesSendCourseStudentMessageInputSchema = z.object({
  message: z.string().nullable().optional(),
  subject: z.string().nullable().optional(),
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for LearningCoursesSendCourseStudentMessageOutput */
LearningCoursesSendCourseStudentMessageOutputSchema = z.object({
  sent: z.number().int().optional(),
});

/** Zod schema for LearningCoursesStartContentInput */
LearningCoursesStartContentInputSchema = z.object({
  contentId: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesStudentSummary */
LearningCoursesStudentSummarySchema = z.object({
  id: z.string().uuid().optional(),
  userDisplayName: z.string().nullable().optional(),
  userEmail: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesSubmitContentInput */
LearningCoursesSubmitContentInputSchema = z.object({
  contentId: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
  submissionData: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesSubmitUserContent */
LearningCoursesSubmitUserContentSchema = z.object({
  submissionData: z.string().min(1),
});

/** Zod schema for LearningCoursesSurveyResponseResult */
LearningCoursesSurveyResponseResultSchema = z.object({
  answers: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  respondentUserId: z.string().uuid().nullable().optional(),
  responseId: z.string().uuid().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdateActivityGrade */
LearningCoursesUpdateActivityGradeSchema = z.object({
  feedback: z.string().nullable().optional(),
  grade: z.number().nullable().optional(),
  gradingDetails: z.string().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdatePrerequisiteApiInput */
LearningCoursesUpdatePrerequisiteApiInputSchema = z.object({
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  minimumGrade: z.number().int().nullable().optional(),
  prerequisiteGroup: z.string().nullable().optional(),
  type: z.lazy(() => LearningCoursesPrerequisiteTypeSchema).optional(),
});

/** Zod schema for LearningCoursesUpdatePricing */
LearningCoursesUpdatePricingSchema = z.object({
  currency: z.string().nullable().optional(),
  isSubscription: z.boolean().nullable().optional(),
  price: z.number().nullable().optional(),
  subscriptionDurationDays: z.number().int().nullable().optional(),
});

/** Zod schema for LearningCoursesUpdateProgramContent */
LearningCoursesUpdateProgramContentSchema = z.object({
  activitySettings: z.lazy(() => LearningCoursesActivitySettingsSchema).optional(),
  body: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  gradingMethod: z.lazy(() => LearningCoursesGradingMethodSchema).optional(),
  id: z.string().uuid(),
  isRequired: z.boolean().nullable().optional(),
  lessonFormat: z.lazy(() => LearningCoursesLessonContentFormatSchema).optional(),
  maxPoints: z.number().nullable().optional(),
  sortOrder: z.number().int().nullable().optional(),
  title: z.string().min(0).max(255).nullable().optional(),
  type: z.lazy(() => LearningCoursesProgramContentTypeSchema).optional(),
  visibility: z.lazy(() => LearningCoursesVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesUpdateProgram */
LearningCoursesUpdateProgramSchema = z.object({
  category: z.lazy(() => ProgramCategorySchema).optional(),
  clearEnrollmentDeadline: z.boolean().optional(),
  clearMaxEnrollments: z.boolean().optional(),
  creatorId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningCoursesProgramDifficultySchema).optional(),
  enrollmentDeadline: z.string().datetime().nullable().optional(),
  enrollmentStatus: z.lazy(() => LearningCoursesEnrollmentStatusSchema).optional(),
  estimatedHours: z.number().int().nullable().optional(),
  maxEnrollments: z.number().int().nullable().optional(),
  metadata: z.string().nullable().optional(),
  skillsProvided: z.string().nullable().optional(),
  skillsRequired: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  videoShowcaseUrl: z.string().nullable().optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
});

/** Zod schema for LearningCoursesUpdateProgress */
LearningCoursesUpdateProgressSchema = z.object({
  additionalData: z.record(z.string(), z.record(z.string(), z.unknown())).nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  status: z.lazy(() => LearningCoursesProgressStatusSchema).optional(),
});

/** Zod schema for LearningCoursesUpdateProgressInput */
LearningCoursesUpdateProgressInputSchema = z.object({
  completionPercentage: z.number().optional(),
  contentId: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesUpdateTimeSpentInput */
LearningCoursesUpdateTimeSpentInputSchema = z.object({
  additionalMinutes: z.number().int().optional(),
  contentId: z.string().uuid().optional(),
  programUserId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesUserProgress */
LearningCoursesUserProgressSchema = z.object({
  completedAt: z.string().datetime().nullable().optional(),
  completionPercentage: z.number().optional(),
  contentProgress: z
    .array(z.lazy(() => LearningCoursesContentProgressSchema))
    .nullable()
    .optional(),
  courseId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  startedAt: z.string().datetime().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningCoursesVisibility */
LearningCoursesVisibilitySchema = z.enum(['Public', 'Internal', 'Private', 'Restricted']);

/** Zod schema for LearningEnrollmentsEnrollUserInput */
LearningEnrollmentsEnrollUserInputSchema = z.object({
  cohortId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningEnrollmentsEnrollment */
LearningEnrollmentsEnrollmentSchema = z.object({
  cohortId: z.string().uuid().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  courseId: z.string().uuid().optional(),
  droppedAt: z.string().datetime().nullable().optional(),
  enrolledAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  progress: z.number().int().optional(),
  status: z.lazy(() => LearningEnrollmentsEnrollmentStatusSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningEnrollmentsEnrollmentStatus */
LearningEnrollmentsEnrollmentStatusSchema = z.enum(['Active', 'Paused', 'Completed', 'Dropped', 'Expired']);

/** Zod schema for LearningEnrollmentsUpdateEnrollmentProgressInput */
LearningEnrollmentsUpdateEnrollmentProgressInputSchema = z.object({
  progress: z.number().int().optional(),
});

/** Zod schema for LearningExperienceDiscoveryCollectionType */
LearningExperienceDiscoveryCollectionTypeSchema = z.enum(['Curated', 'Category', 'Skill', 'Career', 'Trending', 'NewReleases']);

/** Zod schema for LearningExperienceDiscoveryCourseCollection */
LearningExperienceDiscoveryCourseCollectionSchema = z.object({
  courseCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  curatorId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().optional(),
  isPublished: z.boolean().optional(),
  slug: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningExperienceDiscoveryCollectionTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceDiscoveryCreateCourseCollection */
LearningExperienceDiscoveryCreateCourseCollectionSchema = z.object({
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningExperienceDiscoveryCollectionTypeSchema).optional(),
});

/** Zod schema for LearningExperienceDiscoveryCreateFeaturedContent */
LearningExperienceDiscoveryCreateFeaturedContentSchema = z.object({
  courseId: z.string().uuid().nullable().optional(),
  displayOrder: z.number().int().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  targetAudience: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningExperienceDiscoveryFeaturedContentTypeSchema).optional(),
});

/** Zod schema for LearningExperienceDiscoveryFeaturedContent */
LearningExperienceDiscoveryFeaturedContentSchema = z.object({
  courseId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  displayOrder: z.number().int().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isActive: z.boolean().optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  targetAudience: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => LearningExperienceDiscoveryFeaturedContentTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceDiscoveryFeaturedContentType */
LearningExperienceDiscoveryFeaturedContentTypeSchema = z.enum([
  'HeroBanner',
  'CategoryHighlight',
  'NewRelease',
  'TopRated',
  'TrendingNow',
  'StaffPick',
  'SeasonalPromotion',
]);

/** Zod schema for LearningExperienceDiscoveryPopularSearchResult */
LearningExperienceDiscoveryPopularSearchResultSchema = z.object({
  clickThroughRate: z.number().optional(),
  query: z.string().nullable().optional(),
  searchCount: z.number().int().optional(),
  totalClicks: z.number().int().optional(),
});

/** Zod schema for LearningExperienceDiscoveryRecordSearchClick */
LearningExperienceDiscoveryRecordSearchClickSchema = z.object({
  clickedCourseId: z.string().uuid().optional(),
  searchId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceDiscoveryRecordSearch */
LearningExperienceDiscoveryRecordSearchSchema = z.object({
  filters: z.string().nullable().optional(),
  query: z.string().nullable().optional(),
  resultCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceDiscoverySearchHistory */
LearningExperienceDiscoverySearchHistorySchema = z.object({
  clickedCourseId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  filters: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  query: z.string().nullable().optional(),
  resultCount: z.number().int().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryUpdateCourseCollection */
LearningExperienceDiscoveryUpdateCourseCollectionSchema = z.object({
  description: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceDiscoveryUpdateFeaturedContent */
LearningExperienceDiscoveryUpdateFeaturedContentSchema = z.object({
  displayOrder: z.number().int().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isActive: z.boolean().nullable().optional(),
  linkUrl: z.string().nullable().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  subtitle: z.string().nullable().optional(),
  targetAudience: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceLearningPathsAddCourseToPath */
LearningExperienceLearningPathsAddCourseToPathSchema = z.object({
  courseId: z.string().uuid().optional(),
  isRequired: z.boolean().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsCourseOrder */
LearningExperienceLearningPathsCourseOrderSchema = z.object({
  courseId: z.string().uuid().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsCreateLearningPath */
LearningExperienceLearningPathsCreateLearningPathSchema = z.object({
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema).optional(),
  estimatedHours: z.number().int().optional(),
  imageUrl: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathCourse */
LearningExperienceLearningPathsLearningPathCourseSchema = z.object({
  courseId: z.string().uuid().optional(),
  isRequired: z.boolean().optional(),
  order: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathDetail */
LearningExperienceLearningPathsLearningPathDetailSchema = z.object({
  completionCount: z.number().int().optional(),
  courses: z
    .array(z.lazy(() => LearningExperienceLearningPathsLearningPathCourseSchema))
    .nullable()
    .optional(),
  createdAt: z.string().datetime().optional(),
  creatorId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema).optional(),
  enrollmentCount: z.number().int().optional(),
  estimatedHours: z.number().int().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().optional(),
  isPublished: z.boolean().optional(),
  slug: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathDifficulty */
LearningExperienceLearningPathsLearningPathDifficultySchema = z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert']);

/** Zod schema for LearningExperienceLearningPathsLearningPath */
LearningExperienceLearningPathsLearningPathSchema = z.object({
  completionCount: z.number().int().optional(),
  courseCount: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  creatorId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema).optional(),
  enrollmentCount: z.number().int().optional(),
  estimatedHours: z.number().int().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().optional(),
  isPublished: z.boolean().optional(),
  slug: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathEnrollment */
LearningExperienceLearningPathsLearningPathEnrollmentSchema = z.object({
  completedAt: z.string().datetime().nullable().optional(),
  coursesCompleted: z.number().int().optional(),
  createdAt: z.string().datetime().optional(),
  enrolledAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  learningPathId: z.string().uuid().optional(),
  progress: z.number().int().optional(),
  status: z.lazy(() => LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema).optional(),
  totalCourses: z.number().int().optional(),
  updatedAt: z.string().datetime().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceLearningPathsLearningPathEnrollmentStatus */
LearningExperienceLearningPathsLearningPathEnrollmentStatusSchema = z.enum(['InProgress', 'Completed', 'Abandoned']);

/** Zod schema for LearningExperienceLearningPathsLearningPathStatistics */
LearningExperienceLearningPathsLearningPathStatisticsSchema = z.object({
  activeEnrollments: z.number().int().optional(),
  averageCompletionTime: z.string().optional(),
  averageProgress: z.number().optional(),
  completedEnrollments: z.number().int().optional(),
  completionRate: z.number().optional(),
  learningPathId: z.string().uuid().optional(),
  totalEnrollments: z.number().int().optional(),
});

/** Zod schema for LearningExperienceLearningPathsReorderCourses */
LearningExperienceLearningPathsReorderCoursesSchema = z.object({
  courses: z
    .array(z.lazy(() => LearningExperienceLearningPathsCourseOrderSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningExperienceLearningPathsUpdateLearningPath */
LearningExperienceLearningPathsUpdateLearningPathSchema = z.object({
  description: z.string().nullable().optional(),
  difficulty: z.lazy(() => LearningExperienceLearningPathsLearningPathDifficultySchema).optional(),
  estimatedHours: z.number().int().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  isFeatured: z.boolean().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceLearningPathsUpdatePathProgress */
LearningExperienceLearningPathsUpdatePathProgressSchema = z.object({
  coursesCompleted: z.number().int().optional(),
});

/** Zod schema for LearningExperienceRecommendationsAddSkillInput */
LearningExperienceRecommendationsAddSkillInputSchema = z.object({
  skill: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceRecommendationsCreateOrUpdateLearningProfile */
LearningExperienceRecommendationsCreateOrUpdateLearningProfileSchema = z.object({
  learningGoals: z.array(z.string()).nullable().optional(),
  preferredCategories: z.array(z.string()).nullable().optional(),
  preferredDifficulty: z.string().nullable().optional(),
  preferredDuration: z.string().nullable().optional(),
  skills: z.array(z.string()).nullable().optional(),
});

/** Zod schema for LearningExperienceRecommendationsPopularCourse */
LearningExperienceRecommendationsPopularCourseSchema = z.object({
  averageRating: z.number().optional(),
  category: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  enrollmentCount: z.number().int().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  totalRatings: z.number().int().optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendation */
LearningExperienceRecommendationsRecommendationSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  expiresAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isDismissed: z.boolean().optional(),
  isViewed: z.boolean().optional(),
  reason: z.string().nullable().optional(),
  score: z.number().optional(),
  type: z.lazy(() => LearningExperienceRecommendationsRecommendationTypeSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendationStatistics */
LearningExperienceRecommendationsRecommendationStatisticsSchema = z.object({
  byType: z
    .object({
      BasedOnHistory: z.number().int(),
      InstructorFollowed: z.number().int(),
      NextInPath: z.number().int(),
      PeerRecommended: z.number().int(),
      PersonalizedAI: z.number().int(),
      PopularInCategory: z.number().int(),
      SimilarToCompleted: z.number().int(),
      TrendingNow: z.number().int(),
    })
    .nullable()
    .optional(),
  convertedCount: z.number().int().optional(),
  dismissedCount: z.number().int().optional(),
  totalRecommendations: z.number().int().optional(),
  viewedCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceRecommendationsRecommendationType */
LearningExperienceRecommendationsRecommendationTypeSchema = z.enum([
  'PersonalizedAI',
  'PopularInCategory',
  'TrendingNow',
  'BasedOnHistory',
  'SimilarToCompleted',
  'NextInPath',
  'InstructorFollowed',
  'PeerRecommended',
]);

/** Zod schema for LearningExperienceRecommendationsSimilarCourse */
LearningExperienceRecommendationsSimilarCourseSchema = z.object({
  category: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  matchingTags: z.array(z.string()).nullable().optional(),
  similarityScore: z.number().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceRecommendationsTrendingCourse */
LearningExperienceRecommendationsTrendingCourseSchema = z.object({
  category: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  recentEnrollments: z.number().int().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  trendScore: z.number().optional(),
});

/** Zod schema for LearningExperienceRecommendationsUserLearningProfile */
LearningExperienceRecommendationsUserLearningProfileSchema = z.object({
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  learningGoals: z.array(z.string()).nullable().optional(),
  preferredCategories: z.array(z.string()).nullable().optional(),
  preferredDifficulty: z.string().nullable().optional(),
  preferredDuration: z.string().nullable().optional(),
  skills: z.array(z.string()).nullable().optional(),
  totalCoursesCompleted: z.number().int().optional(),
  totalHoursLearned: z.number().int().optional(),
  updatedAt: z.string().datetime().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceSocialControllersUpdateReviewModerationInput */
LearningExperienceSocialControllersUpdateReviewModerationInputSchema = z.object({
  isApproved: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
});

/** Zod schema for LearningExperienceSocialFeedItemType */
LearningExperienceSocialFeedItemTypeSchema = z.enum([
  'NewCourse',
  'PopularCourse',
  'TrendingDiscussion',
  'FeaturedReview',
  'LearningPathSuggestion',
  'CourseUpdate',
  'InstructorActivity',
  'PeerActivity',
  'AchievementUnlocked',
  'SkillMilestone',
]);

/** Zod schema for LearningExperienceSocialServicesCourseDiscussion */
LearningExperienceSocialServicesCourseDiscussionSchema = z.object({
  authorId: z.string().uuid().optional(),
  content: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isPinned: z.boolean().optional(),
  isResolved: z.boolean().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  replyCount: z.number().int().optional(),
  title: z.string().nullable().optional(),
  viewCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseLike */
LearningExperienceSocialServicesCourseLikeSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseRatingStats */
LearningExperienceSocialServicesCourseRatingStatsSchema = z.object({
  averageRating: z.number().optional(),
  courseId: z.string().uuid().optional(),
  featuredReviewCount: z.number().int().optional(),
  fiveStarCount: z.number().int().optional(),
  fourStarCount: z.number().int().optional(),
  oneStarCount: z.number().int().optional(),
  threeStarCount: z.number().int().optional(),
  totalReviews: z.number().int().optional(),
  twoStarCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseReview */
LearningExperienceSocialServicesCourseReviewSchema = z.object({
  content: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  helpfulCount: z.number().int().optional(),
  id: z.string().uuid().optional(),
  isApproved: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  isVerifiedPurchase: z.boolean().optional(),
  rating: z.number().int().optional(),
  title: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCourseWishlist */
LearningExperienceSocialServicesCourseWishlistSchema = z.object({
  courseId: z.string().uuid().optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  notifyOnSale: z.boolean().optional(),
  notifyOnUpdate: z.boolean().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateDiscussionInput */
LearningExperienceSocialServicesCreateDiscussionInputSchema = z.object({
  content: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateReplyInput */
LearningExperienceSocialServicesCreateReplyInputSchema = z.object({
  content: z.string().nullable().optional(),
  discussionId: z.string().uuid().optional(),
  parentReplyId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesCreateReviewInput */
LearningExperienceSocialServicesCreateReviewInputSchema = z.object({
  content: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  enrollmentId: z.string().uuid().nullable().optional(),
  rating: z.number().int().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesDiscussionReply */
LearningExperienceSocialServicesDiscussionReplySchema = z.object({
  authorId: z.string().uuid().optional(),
  content: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  discussionId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  isAcceptedAnswer: z.boolean().optional(),
  parentReplyId: z.string().uuid().nullable().optional(),
  upvoteCount: z.number().int().optional(),
});

/** Zod schema for LearningExperienceSocialServicesPersonalizedFeedItem */
LearningExperienceSocialServicesPersonalizedFeedItemSchema = z.object({
  courseId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  discussionId: z.string().uuid().nullable().optional(),
  expiresAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isViewed: z.boolean().optional(),
  itemType: z.lazy(() => LearningExperienceSocialFeedItemTypeSchema).optional(),
  learningPathId: z.string().uuid().nullable().optional(),
  reason: z.string().nullable().optional(),
  relevanceScore: z.number().optional(),
  reviewId: z.string().uuid().nullable().optional(),
});

/** Zod schema for LearningExperienceSocialServicesWishlistPreferencesInput */
LearningExperienceSocialServicesWishlistPreferencesInputSchema = z.object({
  notifyOnSale: z.boolean().optional(),
  notifyOnUpdate: z.boolean().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAnnouncement */
LearningWorkspacesLearnerAnnouncementSchema = z.object({
  content: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  courseTitle: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  discussionId: z.string().uuid().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentDeadline */
LearningWorkspacesLearnerAssessmentDeadlineSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  courseTitle: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  maxScore: z.number().int().optional(),
  passingScore: z.number().int().optional(),
  submissionStatus: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessment */
LearningWorkspacesLearnerAssessmentSchema = z.object({
  allowLateSubmissions: z.boolean().optional(),
  assessmentId: z.string().uuid().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  isRequired: z.boolean().optional(),
  lateSubmissionDeadline: z.string().datetime().nullable().optional(),
  maxAttempts: z.number().int().nullable().optional(),
  maxScore: z.number().int().optional(),
  order: z.number().int().optional(),
  passingScore: z.number().int().optional(),
  presentationMode: z.string().nullable().optional(),
  submissionModalities: z.string().nullable().optional(),
  timeLimitMinutes: z.number().int().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentGroup */
LearningWorkspacesLearnerAssessmentGroupSchema = z.object({
  description: z.string().nullable().optional(),
  groupId: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  order: z.number().int().optional(),
  weightPercent: z.number().optional(),
});

/** Zod schema for LearningWorkspacesLearnerAssessmentSubmission */
LearningWorkspacesLearnerAssessmentSubmissionSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  attemptNumber: z.number().int().optional(),
  enrollmentId: z.string().uuid().optional(),
  feedback: z.string().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  isLate: z.boolean().optional(),
  passed: z.boolean().nullable().optional(),
  score: z.number().int().nullable().optional(),
  startedAt: z.string().datetime().optional(),
  status: z.string().nullable().optional(),
  submissionId: z.string().uuid().optional(),
  submittedAt: z.string().datetime().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCertificate */
LearningWorkspacesLearnerCertificateSchema = z.object({
  certificateId: z.string().uuid().optional(),
  certificateNumber: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  courseName: z.string().nullable().optional(),
  enrollmentId: z.string().uuid().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  issuedAt: z.string().datetime().optional(),
  recipientName: z.string().nullable().optional(),
  status: z.string().nullable().optional(),
  verificationUrl: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCohort */
LearningWorkspacesLearnerCohortSchema = z.object({
  cohortId: z.string().uuid().optional(),
  currentEnrollmentCount: z.number().int().optional(),
  description: z.string().nullable().optional(),
  endDate: z.string().datetime().optional(),
  instructorId: z.string().uuid().nullable().optional(),
  maxCapacity: z.number().int().optional(),
  meetingSchedule: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  startDate: z.string().datetime().optional(),
  status: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerContent */
LearningWorkspacesLearnerContentSchema = z.object({
  activitySettings: z.string().nullable().optional(),
  body: z.string().nullable().optional(),
  contentId: z.string().uuid().optional(),
  description: z.string().nullable().optional(),
  estimatedMinutes: z.number().int().nullable().optional(),
  gradingMethod: z.string().nullable().optional(),
  isRequired: z.boolean().optional(),
  lessonFormat: z.string().nullable().optional(),
  maxPoints: z.number().int().nullable().optional(),
  parentId: z.string().uuid().nullable().optional(),
  sortOrder: z.number().int().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
  visibility: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerContentProgress */
LearningWorkspacesLearnerContentProgressSchema = z.object({
  attempts: z.number().int().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().optional(),
  firstAccessedAt: z.string().datetime().nullable().optional(),
  lastAccessedAt: z.string().datetime().nullable().optional(),
  maxScore: z.number().nullable().optional(),
  progressPercentage: z.number().optional(),
  score: z.number().nullable().optional(),
  status: z.string().nullable().optional(),
  timeSpentSeconds: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCourseSummary */
LearningWorkspacesLearnerCourseSummarySchema = z.object({
  category: z.string().nullable().optional(),
  completedItems: z.number().int().optional(),
  completionStatus: z.string().nullable().optional(),
  courseId: z.string().uuid().optional(),
  currentContentId: z.string().uuid().nullable().optional(),
  currentContentTitle: z.string().nullable().optional(),
  currentContentType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  difficulty: z.string().nullable().optional(),
  enrolledAt: z.string().datetime().optional(),
  enrollmentId: z.string().uuid().optional(),
  enrollmentStatus: z.string().nullable().optional(),
  estimatedHours: z.number().int().nullable().optional(),
  finalGrade: z.number().nullable().optional(),
  progressPercentage: z.number().optional(),
  remainingMinutes: z.number().int().optional(),
  slug: z.string().nullable().optional(),
  thumbnail: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  totalItems: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerCourseWorkspace */
LearningWorkspacesLearnerCourseWorkspaceSchema = z.object({
  assessmentGroups: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentGroupSchema))
    .nullable()
    .optional(),
  assessments: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentSchema))
    .nullable()
    .optional(),
  calendar: z
    .array(z.lazy(() => LearningWorkspacesLearnerScheduleEntrySchema))
    .nullable()
    .optional(),
  certificates: z
    .array(z.lazy(() => LearningWorkspacesLearnerCertificateSchema))
    .nullable()
    .optional(),
  cohort: z.lazy(() => LearningWorkspacesLearnerCohortSchema).optional(),
  content: z
    .array(z.lazy(() => LearningWorkspacesLearnerContentSchema))
    .nullable()
    .optional(),
  course: z.lazy(() => LearningWorkspacesLearnerCourseSummarySchema).optional(),
  discussions: z
    .array(z.lazy(() => LearningWorkspacesLearnerDiscussionSchema))
    .nullable()
    .optional(),
  progress: z
    .array(z.lazy(() => LearningWorkspacesLearnerContentProgressSchema))
    .nullable()
    .optional(),
  submissions: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentSubmissionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningWorkspacesLearnerDashboard */
LearningWorkspacesLearnerDashboardSchema = z.object({
  announcements: z
    .array(z.lazy(() => LearningWorkspacesLearnerAnnouncementSchema))
    .nullable()
    .optional(),
  certificates: z
    .array(z.lazy(() => LearningWorkspacesLearnerCertificateSchema))
    .nullable()
    .optional(),
  courses: z
    .array(z.lazy(() => LearningWorkspacesLearnerCourseSummarySchema))
    .nullable()
    .optional(),
  deadlines: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentDeadlineSchema))
    .nullable()
    .optional(),
  grades: z
    .array(z.lazy(() => LearningWorkspacesLearnerGradeSummarySchema))
    .nullable()
    .optional(),
  upcoming: z
    .array(z.lazy(() => LearningWorkspacesLearnerScheduleEntrySchema))
    .nullable()
    .optional(),
});

/** Zod schema for LearningWorkspacesLearnerDiscussion */
LearningWorkspacesLearnerDiscussionSchema = z.object({
  authorId: z.string().uuid().optional(),
  content: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  discussionId: z.string().uuid().optional(),
  isPinned: z.boolean().optional(),
  isResolved: z.boolean().optional(),
  lastActivityAt: z.string().datetime().nullable().optional(),
  replyCount: z.number().int().optional(),
  title: z.string().nullable().optional(),
  viewCount: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerGradeItem */
LearningWorkspacesLearnerGradeItemSchema = z.object({
  assessmentId: z.string().uuid().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  feedback: z.string().nullable().optional(),
  gradedAt: z.string().datetime().nullable().optional(),
  groupId: z.string().uuid().nullable().optional(),
  maxScore: z.number().int().optional(),
  passed: z.boolean().nullable().optional(),
  passingScore: z.number().int().optional(),
  score: z.number().int().nullable().optional(),
  submissionStatus: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerGradeSummary */
LearningWorkspacesLearnerGradeSummarySchema = z.object({
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  courseTitle: z.string().nullable().optional(),
  earnedPoints: z.number().nullable().optional(),
  finalGrade: z.number().nullable().optional(),
  gradedAssessments: z.number().int().optional(),
  groups: z
    .array(z.lazy(() => LearningWorkspacesLearnerAssessmentGroupSchema))
    .nullable()
    .optional(),
  items: z
    .array(z.lazy(() => LearningWorkspacesLearnerGradeItemSchema))
    .nullable()
    .optional(),
  percentage: z.number().nullable().optional(),
  possiblePoints: z.number().nullable().optional(),
  totalAssessments: z.number().int().optional(),
});

/** Zod schema for LearningWorkspacesLearnerScheduleEntry */
LearningWorkspacesLearnerScheduleEntrySchema = z.object({
  assessmentId: z.string().uuid().nullable().optional(),
  availableFrom: z.string().datetime().nullable().optional(),
  availableUntil: z.string().datetime().nullable().optional(),
  cohortId: z.string().uuid().optional(),
  cohortName: z.string().nullable().optional(),
  contentId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  courseTitle: z.string().nullable().optional(),
  dueAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().nullable().optional(),
  location: z.string().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  scheduleItemId: z.string().uuid().optional(),
  startsAt: z.string().datetime().nullable().optional(),
  status: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.string().nullable().optional(),
});

/** Zod schema for LearningWorkspacesLearnerSearchResult */
LearningWorkspacesLearnerSearchResultSchema = z.object({
  courseId: z.string().uuid().optional(),
  courseSlug: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  kind: z.string().nullable().optional(),
  route: z.string().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for Money */
MoneySchema = z.object({
  amount: z.number().optional(),
  currency: z.string().nullable().optional(),
});

/** Zod schema for MvcProblemDetails */
MvcProblemDetailsSchema = z
  .object({
    detail: z.string().nullable().optional(),
    instance: z.string().nullable().optional(),
    status: z.number().int().nullable().optional(),
    title: z.string().nullable().optional(),
    type: z.string().nullable().optional(),
  })
  .catchall(z.record(z.string(), z.unknown()));

/** Zod schema for NotificationsNotificationChannel */
NotificationsNotificationChannelSchema = z.enum(['InApp', 'Email', 'Push', 'Sms', 'Slack', 'Discord', 'Webhook']);

/** Zod schema for ObjectsAttestationConveyancePreference */
ObjectsAttestationConveyancePreferenceSchema = z.enum(['None', 'Indirect', 'Direct', 'Enterprise']);

/** Zod schema for ObjectsAttestationStatementFormatIdentifier */
ObjectsAttestationStatementFormatIdentifierSchema = z.enum(['Packed', 'Tpm', 'AndroidKey', 'AndroidSafetyNet', 'FidoU2f', 'Apple', 'None']);

/** Zod schema for ObjectsAuthenticationExtensionsClientInputs */
ObjectsAuthenticationExtensionsClientInputsSchema = z.object({
  credProps: z.boolean().nullable().optional(),
  credentialProtectionPolicy: z.lazy(() => ObjectsCredentialProtectionPolicySchema).optional(),
  enforceCredentialProtectionPolicy: z.boolean().nullable().optional(),
  'example.extension.bool': z.boolean().nullable().optional(),
  exts: z.boolean().nullable().optional(),
  largeBlob: z.lazy(() => ObjectsAuthenticationExtensionsLargeBlobInputsSchema).optional(),
  prf: z.lazy(() => ObjectsAuthenticationExtensionsPRFInputsSchema).optional(),
  uvm: z.boolean().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsLargeBlobInputs */
ObjectsAuthenticationExtensionsLargeBlobInputsSchema = z.object({
  read: z.boolean().optional(),
  support: z.lazy(() => ObjectsLargeBlobSupportSchema).optional(),
  write: z.string().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsPRFInputs */
ObjectsAuthenticationExtensionsPRFInputsSchema = z.object({
  eval: z.lazy(() => ObjectsAuthenticationExtensionsPRFValuesSchema).optional(),
  evalByCredential: z.lazy(() => KeyValuePairStringAuthenticationExtensionsPRFValuesSchema).optional(),
});

/** Zod schema for ObjectsAuthenticationExtensionsPRFValues */
ObjectsAuthenticationExtensionsPRFValuesSchema = z.object({
  first: z.string().nullable(),
  second: z.string().nullable().optional(),
});

/** Zod schema for ObjectsAuthenticatorAttachment */
ObjectsAuthenticatorAttachmentSchema = z.enum(['Platform', 'CrossPlatform']);

/** Zod schema for ObjectsAuthenticatorTransport */
ObjectsAuthenticatorTransportSchema = z.enum(['Usb', 'Nfc', 'Ble', 'SmartCard', 'Hybrid', 'Internal']);

/** Zod schema for ObjectsCOSEAlgorithm */
ObjectsCOSEAlgorithmSchema = z.enum(['RS1', 'RS512', 'RS384', 'RS256', 'ES256K', 'PS512', 'PS384', 'PS256', 'ES512', 'ES384', 'EdDSA', 'ES256']);

/** Zod schema for ObjectsCredentialProtectionPolicy */
ObjectsCredentialProtectionPolicySchema = z.enum(['UserVerificationOptional', 'UserVerificationOptionalWithCredentialIdList', 'UserVerificationRequired']);

/** Zod schema for ObjectsLargeBlobSupport */
ObjectsLargeBlobSupportSchema = z.enum(['Required', 'Preferred']);

/** Zod schema for ObjectsPublicKeyCredentialDescriptor */
ObjectsPublicKeyCredentialDescriptorSchema = z.object({
  id: z.string().nullable().optional(),
  transports: z
    .array(z.lazy(() => ObjectsAuthenticatorTransportSchema))
    .nullable()
    .optional(),
  type: z.lazy(() => ObjectsPublicKeyCredentialTypeSchema).optional(),
});

/** Zod schema for ObjectsPublicKeyCredentialHint */
ObjectsPublicKeyCredentialHintSchema = z.enum(['SecurityKey', 'ClientDevice', 'Hybrid']);

/** Zod schema for ObjectsPublicKeyCredentialType */
ObjectsPublicKeyCredentialTypeSchema = z.enum(['PublicKey', 'Invalid']);

/** Zod schema for ObjectsResidentKeyRequirement */
ObjectsResidentKeyRequirementSchema = z.enum(['Required', 'Preferred', 'Discouraged']);

/** Zod schema for ObjectsUserVerificationRequirement */
ObjectsUserVerificationRequirementSchema = z.enum(['Required', 'Preferred', 'Discouraged']);

/** Zod schema for PagedResultOfGameGuildCommerceProductsProductDto */
PagedResultOfGameGuildCommerceProductsProductDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => CommerceProductsProductSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildCommerceProductsPromoCodeDto */
PagedResultOfGameGuildCommerceProductsPromoCodeDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => CommerceProductsPromoCodeSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildCommerceProductsSupportTicketDto */
PagedResultOfGameGuildCommerceProductsSupportTicketDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => CommerceProductsSupportTicketSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildCommerceSubscriptionsSubscription */
PagedResultOfGameGuildCommerceSubscriptionsSubscriptionSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildCommerceSubscriptionsSubscriptionNotificationDto */
PagedResultOfGameGuildCommerceSubscriptionsSubscriptionNotificationDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => CommerceSubscriptionsSubscriptionNotificationSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildIdentityTenantsTenant */
PagedResultOfGameGuildIdentityTenantsTenantSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => IdentityTenantsTenantSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildIdentityTenantsTenantAuditLogEntry */
PagedResultOfGameGuildIdentityTenantsTenantAuditLogEntrySchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => IdentityTenantsTenantAuditLogEntrySchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildIdentityUsersUserDto */
PagedResultOfGameGuildIdentityUsersUserDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => IdentityUsersUserDtoSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildIdentityUsersUserNotificationDto */
PagedResultOfGameGuildIdentityUsersUserNotificationDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => IdentityUsersUserNotificationDtoSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for PagedResultOfGameGuildIdentityUsersUserProfileDto */
PagedResultOfGameGuildIdentityUsersUserProfileDtoSchema = z.object({
  hasNextPage: z.boolean().optional(),
  hasPreviousPage: z.boolean().optional(),
  items: z
    .array(z.lazy(() => IdentityUsersUserProfileDtoSchema))
    .nullable()
    .optional(),
  pageNumber: z.number().int().optional(),
  pageSize: z.number().int().optional(),
  skip: z.number().int().optional(),
  take: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  totalPages: z.number().int().optional(),
});

/** Zod schema for ProgramCategory */
ProgramCategorySchema = z.enum([
  'General',
  'Programming',
  'DataScience',
  'WebDevelopment',
  'MobileDevelopment',
  'GameDevelopment',
  'AI',
  'Cybersecurity',
  'DevOps',
  'Database',
  'Business',
  'Design',
  'Marketing',
  'ProjectManagement',
  'PersonalDevelopment',
  'CreativeArts',
  'Science',
  'Language',
  'Other',
]);

/** Zod schema for ProjectsAddCollaboratorInput */
ProjectsAddCollaboratorInputSchema = z.object({
  email: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  message: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  requireAcceptance: z.boolean().optional(),
});

/** Zod schema for ProjectsAddProjectCollaboratorInput */
ProjectsAddProjectCollaboratorInputSchema = z.object({
  permissions: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  userId: z.string().uuid(),
});

/** Zod schema for ProjectsCollaborator */
ProjectsCollaboratorSchema = z.object({
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  permissions: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
});

/** Zod schema for ProjectsCreateProjectInput */
ProjectsCreateProjectInputSchema = z.object({
  categoryId: z.string().uuid().nullable().optional(),
  description: z.string().min(0).max(2000).nullable().optional(),
  downloadUrl: z.string().url().nullable().optional(),
  imageUrl: z.string().url().nullable().optional(),
  repositoryUrl: z.string().url().nullable().optional(),
  shortDescription: z.string().min(0).max(500).nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  tags: z.array(z.string()).nullable().optional(),
  title: z.string().min(1).max(255),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  websiteUrl: z.string().url().nullable().optional(),
});

/** Zod schema for ProjectsDevelopmentStatus */
ProjectsDevelopmentStatusSchema = z.enum(['Planning', 'InDevelopment', 'Alpha', 'Beta', 'Released', 'Completed', 'OnHold', 'Cancelled', 'Archived']);

/** Zod schema for ProjectsEffectivePermission */
ProjectsEffectivePermissionSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  isOwner: z.boolean().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  resourceId: z.string().uuid().optional(),
  resourceType: z.string().nullable().optional(),
});

/** Zod schema for ProjectsInvitationResult */
ProjectsInvitationResultSchema = z.object({
  errorMessage: z.string().nullable().optional(),
  invitationId: z.string().uuid().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for ProjectsInviteProjectCollaboratorInput */
ProjectsInviteProjectCollaboratorInputSchema = z.object({
  email: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  permissions: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  userId: z.string().uuid().nullable().optional(),
});

/** Zod schema for ProjectsLinkProjectStoreProductInput */
ProjectsLinkProjectStoreProductInputSchema = z.object({
  productId: z.string().uuid().optional(),
});

/** Zod schema for ProjectsPermissionUpdateResult */
ProjectsPermissionUpdateResultSchema = z.object({
  errorMessage: z.string().nullable().optional(),
  success: z.boolean().optional(),
});

/** Zod schema for ProjectsProject */
ProjectsProjectSchema = z.object({
  averageRating: z.number().nullable().optional(),
  category: z.lazy(() => ProjectsProjectCategorySchema).optional(),
  categoryId: z.string().uuid().nullable().optional(),
  collaborators: z
    .array(z.lazy(() => ProjectsProjectCollaboratorSchema))
    .nullable()
    .optional(),
  copyright: z.string().max(500).nullable().optional(),
  createdAt: z.string().datetime(),
  createdBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  createdById: z.string().uuid().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  developmentStatus: z.lazy(() => ProjectsDevelopmentStatusSchema).optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  downloadUrl: z.string().max(500).nullable().optional(),
  featuredImageUrl: z.string().max(1000).nullable().optional(),
  feedbackCount: z.number().int().optional(),
  feedbacks: z
    .array(z.lazy(() => ProjectsProjectFeedbackSchema))
    .nullable()
    .optional(),
  followerCount: z.number().int().optional(),
  followers: z
    .array(z.lazy(() => ProjectsProjectFollowerSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().max(500).nullable().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isInJam: z.boolean().optional(),
  isNew: z.boolean().optional(),
  jamSubmissions: z
    .array(z.lazy(() => ProjectsProjectJamSubmissionSchema))
    .nullable()
    .optional(),
  latestVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  license: z.string().max(200).nullable().optional(),
  projectMetadata: z.lazy(() => ProjectsProjectMetadataSchema).optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  releases: z
    .array(z.lazy(() => ProjectsProjectReleaseSchema))
    .nullable()
    .optional(),
  repositoryUrl: z.string().max(500).nullable().optional(),
  shortDescription: z.string().max(500).nullable().optional(),
  slug: z.string().min(1).max(500),
  socialLinks: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema),
  tags: z.string().nullable().optional(),
  teamCount: z.number().int().optional(),
  teams: z
    .array(z.lazy(() => ProjectsProjectTeamSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(500),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  versions: z
    .array(z.lazy(() => ProjectsProjectVersionSchema))
    .nullable()
    .optional(),
  visibility: z.lazy(() => ContentVisibilitySchema),
  websiteUrl: z.string().max(500).nullable().optional(),
});

/** Zod schema for ProjectsProjectCategory */
ProjectsProjectCategorySchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  name: z.string().min(1).max(50),
  projects: z
    .array(z.lazy(() => ProjectsProjectSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectCollaborator */
ProjectsProjectCollaboratorSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  leftAt: z.string().datetime().nullable().optional(),
  permissions: z.string().min(1).max(500),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  role: z.string().min(1).max(100),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectCollaboratorDto */
ProjectsProjectCollaboratorDtoSchema = z.object({
  email: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  invitedBy: z.string().nullable().optional(),
  isOwner: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
  profilePictureUrl: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProjectFeedback */
ProjectsProjectFeedbackSchema = z.object({
  categories: z.string().max(500).nullable().optional(),
  content: z.string().max(2000).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  helpfulVotes: z.number().int().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isFeatured: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isVerified: z.boolean().optional(),
  platform: z.string().max(100).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  projectVersion: z.string().max(50).nullable().optional(),
  rating: z.number().int().min(1).max(5).optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(200),
  totalVotes: z.number().int().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectFollower */
ProjectsProjectFollowerSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  emailNotifications: z.boolean().optional(),
  followedAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  notificationSettings: z.string().max(1000).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  pushNotifications: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectInvitation */
ProjectsProjectInvitationSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  invitedAt: z.string().datetime().optional(),
  invitedByUserId: z.string().uuid().optional(),
  invitedEmail: z.string().nullable().optional(),
  invitedUserId: z.string().uuid().nullable().optional(),
  permissions: z.string().nullable().optional(),
  projectId: z.string().uuid().optional(),
  projectTitle: z.string().nullable().optional(),
  respondedAt: z.string().datetime().nullable().optional(),
  role: z.string().nullable().optional(),
  status: z.lazy(() => ProjectsProjectInvitationStatusSchema).optional(),
  token: z.string().nullable().optional(),
});

/** Zod schema for ProjectsProjectInvitationStatus */
ProjectsProjectInvitationStatusSchema = z.enum(['Pending', 'Accepted', 'Declined', 'Revoked', 'Expired']);

/** Zod schema for ProjectsProjectJamSubmission */
ProjectsProjectJamSubmissionSchema = z.object({
  awardDetails: z.string().max(1000).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  finalScore: z.number().nullable().optional(),
  hasAward: z.boolean().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isEligible: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  jam: z.lazy(() => GameJamsJamSchema).optional(),
  jamId: z.string().uuid().nullable().optional(),
  metadata: z.string().max(2000).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  ranking: z.number().int().nullable().optional(),
  scores: z
    .array(z.lazy(() => GameJamsJamScoreSchema))
    .nullable()
    .optional(),
  submissionNotes: z.string().max(2000).nullable().optional(),
  submittedAt: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectMetadata */
ProjectsProjectMetadataSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  downloadCount: z.number().int().optional(),
  followerCount: z.number().int().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  project: z.lazy(() => ProjectsProjectSchema),
  projectId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  viewCount: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectRelease */
ProjectsProjectReleaseSchema = z.object({
  buildNumber: z.string().max(100).nullable().optional(),
  checksum: z.string().max(128).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  downloadCount: z.number().int().optional(),
  downloadUrl: z.string().max(500).nullable().optional(),
  fileSize: z.number().int().nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isLatest: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isPrerelease: z.boolean().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  releaseMetadata: z.string().max(2000).nullable().optional(),
  releaseNotes: z.string().nullable().optional(),
  releaseType: z.string().max(50).nullable().optional(),
  releaseVersion: z.string().min(1).max(50),
  releasedAt: z.string().datetime().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  supportedPlatforms: z.string().max(500).nullable().optional(),
  systemRequirements: z.string().max(1000).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(200),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectRoleTemplate */
ProjectsProjectRoleTemplateSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsProjectStatistics */
ProjectsProjectStatisticsSchema = z.object({
  activeTeamCount: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  awardCount: z.number().int().optional(),
  calculatedAt: z.string().datetime().optional(),
  collaboratorCount: z.number().int().optional(),
  downloadsLast30Days: z.number().int().optional(),
  feedbackCount: z.number().int().optional(),
  followerCount: z.number().int().optional(),
  jamSubmissionCount: z.number().int().optional(),
  newFollowersLast30Days: z.number().int().optional(),
  popularityRank: z.number().int().nullable().optional(),
  projectId: z.string().uuid().optional(),
  releaseCount: z.number().int().optional(),
  totalDownloads: z.number().int().optional(),
  trendingScore: z.number().optional(),
  viewsLast30Days: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectStoreProductProjection */
ProjectsProjectStoreProductProjectionSchema = z.object({
  linkId: z.string().uuid().optional(),
  productId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
});

/** Zod schema for ProjectsProjectTeam */
ProjectsProjectTeamSchema = z.object({
  assignedAt: z.string().datetime().optional(),
  contributionPercentage: z.number().min(0).max(100).optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  endedAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  notes: z.string().max(1000).nullable().optional(),
  permissions: z.string().max(1000).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  role: z.string().min(1).max(100),
  team: z.lazy(() => ProjectsTeamSchema).optional(),
  teamId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsProjectType */
ProjectsProjectTypeSchema = z.enum(['Game', 'Tool', 'Art', 'Music', 'Educational', 'Plugin', 'Template', 'Library', 'Other']);

/** Zod schema for ProjectsProjectVersion */
ProjectsProjectVersionSchema = z.object({
  createdAt: z.string().datetime(),
  createdBy: z.lazy(() => IdentityUsersUserSchema),
  createdById: z.string().uuid().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  downloadCount: z.number().int().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  project: z.lazy(() => ProjectsProjectSchema),
  projectId: z.string().uuid().optional(),
  releaseNotes: z.string().nullable().optional(),
  status: z.string().min(1).max(50),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  versionNumber: z.string().min(1).max(50),
});

/** Zod schema for ProjectsShareProjectInput */
ProjectsShareProjectInputSchema = z.object({
  permissions: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
  userId: z.string().uuid(),
});

/** Zod schema for ProjectsShareProjectWithRoleInput */
ProjectsShareProjectWithRoleInputSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  message: z.string().nullable().optional(),
  notifyUsers: z.boolean().optional(),
  requireAcceptance: z.boolean().optional(),
  roleName: z.string().nullable().optional(),
  userEmails: z.array(z.string()).nullable().optional(),
  userIds: z.array(z.string().uuid()).nullable().optional(),
});

/** Zod schema for ProjectsShareResult */
ProjectsShareResultSchema = z.object({
  errorMessage: z.string().nullable().optional(),
  failureCount: z.number().int().optional(),
  success: z.boolean().optional(),
  successCount: z.number().int().optional(),
});

/** Zod schema for ProjectsTeam */
ProjectsTeamSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(2000).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  members: z
    .array(z.lazy(() => ProjectsTeamMemberSchema))
    .nullable()
    .optional(),
  name: z.string().min(1).max(200),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsTeamMember */
ProjectsTeamMemberSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  joinedAt: z.string().datetime().optional(),
  role: z.string().max(100).nullable().optional(),
  team: z.lazy(() => ProjectsTeamSchema).optional(),
  teamId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ProjectsUpdateCollaboratorInput */
ProjectsUpdateCollaboratorInputSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  permissions: z
    .array(z.lazy(() => IdentityAuthorizationPermissionTypeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ProjectsUpdateProjectCollaboratorInput */
ProjectsUpdateProjectCollaboratorInputSchema = z.object({
  permissions: z.string().nullable().optional(),
  role: z.string().nullable().optional(),
});

/** Zod schema for ProjectsUpdateProjectInput */
ProjectsUpdateProjectInputSchema = z.object({
  categoryId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  downloadUrl: z.string().nullable().optional(),
  imageUrl: z.string().nullable().optional(),
  repositoryUrl: z.string().nullable().optional(),
  shortDescription: z.string().nullable().optional(),
  status: z.lazy(() => ContentStatusSchema).optional(),
  tags: z.array(z.string()).nullable().optional(),
  title: z.string().nullable().optional(),
  type: z.lazy(() => ProjectsProjectTypeSchema).optional(),
  visibility: z.lazy(() => ContentVisibilitySchema).optional(),
  websiteUrl: z.string().nullable().optional(),
});

/** Zod schema for ResourcesArchiveResourceUsageRecordsInput */
ResourcesArchiveResourceUsageRecordsInputSchema = z.object({
  olderThan: z.string().datetime().optional(),
});

/** Zod schema for ResourcesCheckResourceQuotaInput */
ResourcesCheckResourceQuotaInputSchema = z.object({
  amount: z.number().int().optional(),
});

/** Zod schema for ResourcesCleanupOrphanedResourcesInput */
ResourcesCleanupOrphanedResourcesInputSchema = z.object({
  dryRun: z.boolean().optional(),
  resourceTypes: z
    .array(z.lazy(() => ResourcesResourceUsageTypeSchema))
    .nullable()
    .optional(),
});

/** Zod schema for ResourcesEffectiveSettingOutput */
ResourcesEffectiveSettingOutputSchema = z.object({
  isUserOverride: z.boolean().optional(),
  key: z.string().nullable().optional(),
  value: z.string().nullable().optional(),
});

/** Zod schema for ResourcesRecordTenantResourceUsageInput */
ResourcesRecordTenantResourceUsageInputSchema = z.object({
  count: z.number().int().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
  periodEnd: z.string().datetime().optional(),
  periodStart: z.string().datetime().optional(),
  resourceUsageType: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
});

/** Zod schema for ResourcesRecordUserResourceUsageInput */
ResourcesRecordUserResourceUsageInputSchema = z.object({
  count: z.number().int().optional(),
  metadata: z.record(z.string(), z.string()).nullable().optional(),
  periodEnd: z.string().datetime().optional(),
  periodStart: z.string().datetime().optional(),
  resourceUsageType: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
});

/** Zod schema for ResourcesResourceMetadata */
ResourcesResourceMetadataSchema = z.object({
  category: z.string().max(100).nullable().optional(),
  createdAt: z.string().datetime(),
  dataType: z.string().max(50).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  displayOrder: z.number().int().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isSystemManaged: z.boolean().optional(),
  key: z.string().min(1).max(100),
  resourceId: z.string().uuid().nullable().optional(),
  rowVersion: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  userId: z.string().uuid().nullable().optional(),
  value: z.string().max(4000).nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ResourcesResourceQuotaEnforcementResult */
ResourcesResourceQuotaEnforcementResultSchema = z.object({
  currentUsage: z.number().int().optional(),
  excessAmount: z.number().int().optional(),
  hardLimit: z.number().int().nullable().optional(),
  isAllowed: z.boolean().optional(),
  isHardLimitExceeded: z.boolean().optional(),
  isSoftLimitExceeded: z.boolean().optional(),
  message: z.string().nullable().optional(),
  nextReset: z.string().datetime().nullable().optional(),
  remainingQuota: z.number().int().nullable().optional(),
  softLimit: z.number().int().nullable().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  usagePercentage: z.number().optional(),
});

/** Zod schema for ResourcesResourceQuotaPeriod */
ResourcesResourceQuotaPeriodSchema = z.enum(['Daily', 'Weekly', 'Monthly', 'Quarterly', 'Yearly', 'Unlimited']);

/** Zod schema for ResourcesResourceQuotaOutput */
ResourcesResourceQuotaOutputSchema = z.object({
  currentUsage: z.number().int().optional(),
  description: z.string().nullable().optional(),
  hardLimit: z.number().int().nullable().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isHardLimitExceeded: z.boolean().optional(),
  isSoftLimitExceeded: z.boolean().optional(),
  lastResetDate: z.string().datetime().optional(),
  limit: z.number().int().optional(),
  nextResetDate: z.string().datetime().optional(),
  period: z.lazy(() => ResourcesResourceQuotaPeriodSchema).optional(),
  remainingQuota: z.number().int().optional(),
  shouldReset: z.boolean().optional(),
  softLimit: z.number().int().nullable().optional(),
  softLimitPercentage: z.number().optional(),
  tenantId: z.string().uuid().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  usagePercentage: z.number().optional(),
});

/** Zod schema for ResourcesResourceSettings */
ResourcesResourceSettingsSchema = z.object({
  allowUserOverride: z.boolean().optional(),
  category: z.string().max(100).nullable().optional(),
  createdAt: z.string().datetime(),
  dataType: z.string().max(50).nullable().optional(),
  defaultValue: z.string().max(4000).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(500).nullable().optional(),
  displayOrder: z.number().int().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isSystemManaged: z.boolean().optional(),
  key: z.string().min(1).max(100),
  rowVersion: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  userId: z.string().uuid().nullable().optional(),
  validationRules: z.string().max(1000).nullable().optional(),
  value: z.string().max(4000).nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ResourcesResourceUsageType */
ResourcesResourceUsageTypeSchema = z.enum([
  'Users',
  'Projects',
  'Storage',
  'ApiCalls',
  'Programs',
  'Courses',
  'FeatureFlags',
  'SubscriptionPlans',
  'Products',
  'TestingSessions',
  'Roles',
  'Tenants',
  'Subscriptions',
  'SLOs',
  'AccessReviewCampaigns',
  'SoDRules',
  'AbacPolicies',
  'ConditionalPolicies',
  'Wallets',
  'Disputes',
  'PromoCodes',
  'Orders',
  'AuditEntries',
  'Assets',
  'AssetStorage',
  'AssetDownloads',
  'AssetTransformations',
  'AiRequests',
  'AiTokens',
]);

/** Zod schema for ResourcesSetQuotaInput */
ResourcesSetQuotaInputSchema = z.object({
  hardLimit: z.number().int().nullable().optional(),
  isActive: z.boolean().optional(),
  period: z.lazy(() => ResourcesResourceQuotaPeriodSchema).optional(),
  resetTime: z.string().nullable().optional(),
  softLimit: z.number().int().nullable().optional(),
});

/** Zod schema for ResourcesSetResourceMetadataInput */
ResourcesSetResourceMetadataInputSchema = z.object({
  category: z.string().nullable().optional(),
  dataType: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  value: z.string().nullable().optional(),
});

/** Zod schema for ResourcesSetResourceSettingsInput */
ResourcesSetResourceSettingsInputSchema = z.object({
  allowUserOverride: z.boolean().nullable().optional(),
  category: z.string().nullable().optional(),
  dataType: z.string().nullable().optional(),
  defaultValue: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().nullable().optional(),
  validationRules: z.string().nullable().optional(),
  value: z.string().nullable().optional(),
});

/** Zod schema for ResourcesSetUserResourceSettingsInput */
ResourcesSetUserResourceSettingsInputSchema = z.object({
  value: z.string().nullable().optional(),
});

/** Zod schema for ResourcesToggleResourceQuotaInput */
ResourcesToggleResourceQuotaInputSchema = z.object({
  isActive: z.boolean().optional(),
});

/** Zod schema for ResourcesTrendGranularity */
ResourcesTrendGranularitySchema = z.enum(['Daily', 'Weekly', 'Monthly']);

/** Zod schema for ResourcesUsageRecord */
ResourcesUsageRecordSchema = z.object({
  averagePerDay: z.number().nullable().optional(),
  count: z.number().int().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  metadata: z.string().max(1000).nullable().optional(),
  peakUsage: z.number().int().nullable().optional(),
  peakUsageDate: z.string().datetime().nullable().optional(),
  periodEnd: z.string().datetime().optional(),
  periodStart: z.string().datetime().optional(),
  resourceId: z.string().uuid().nullable().optional(),
  resourceQuotaId: z.string().uuid().nullable().optional(),
  source: z.string().max(50).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
  updatedAt: z.string().datetime(),
  usageAmount: z.number().int().optional(),
  userId: z.string().uuid().nullable().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for ResourcesUsageTrendDataPoint */
ResourcesUsageTrendDataPointSchema = z.object({
  period: z.string().datetime().optional(),
  tenantCount: z.number().int().optional(),
  totalUsage: z.number().int().optional(),
});

/** Zod schema for ResourcesUsageTrendsResult */
ResourcesUsageTrendsResultSchema = z.object({
  dataPoints: z
    .array(z.lazy(() => ResourcesUsageTrendDataPointSchema))
    .nullable()
    .optional(),
  endDate: z.string().datetime().optional(),
  granularity: z.lazy(() => ResourcesTrendGranularitySchema).optional(),
  startDate: z.string().datetime().optional(),
  type: z.lazy(() => ResourcesResourceUsageTypeSchema).optional(),
});

/** Zod schema for SocialBlogBlogPost */
SocialBlogBlogPostSchema = z.object({
  allowComments: z.boolean().optional(),
  authorId: z.string().uuid().optional(),
  commentsCount: z.number().int().optional(),
  content: z.string().nullable().optional(),
  coverImageUrl: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  excerpt: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isFeatured: z.boolean().optional(),
  likesCount: z.number().int().optional(),
  publishedAt: z.string().datetime().nullable().optional(),
  readTimeMinutes: z.number().int().optional(),
  slug: z.string().nullable().optional(),
  status: z.lazy(() => SocialBlogBlogPostStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
  viewsCount: z.number().int().optional(),
});

/** Zod schema for SocialBlogBlogPostStatus */
SocialBlogBlogPostStatusSchema = z.enum(['Draft', 'Published', 'Archived']);

/** Zod schema for SocialBlogCreateBlogPostInput */
SocialBlogCreateBlogPostInputSchema = z.object({
  authorId: z.string().uuid().optional(),
  content: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
});

/** Zod schema for SocialFeedAddFeedItemInput */
SocialFeedAddFeedItemInputSchema = z.object({
  authorId: z.string().uuid().optional(),
  contentCreatedAt: z.string().datetime().nullable().optional(),
  contentId: z.string().uuid().optional(),
  contentType: z.lazy(() => SocialFeedFeedContentTypeSchema).optional(),
  reason: z.lazy(() => SocialFeedFeedItemReasonSchema).optional(),
  relevanceScore: z.number().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialFeedFeedContentType */
SocialFeedFeedContentTypeSchema = z.enum(['Post', 'BlogPost', 'CourseReview', 'ProjectUpdate', 'Achievement', 'CourseCompletion']);

/** Zod schema for SocialFeedFeedItem */
SocialFeedFeedItemSchema = z.object({
  authorId: z.string().uuid().optional(),
  contentCreatedAt: z.string().datetime().optional(),
  contentId: z.string().uuid().optional(),
  contentType: z.lazy(() => SocialFeedFeedContentTypeSchema).optional(),
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isHidden: z.boolean().optional(),
  isRead: z.boolean().optional(),
  reason: z.lazy(() => SocialFeedFeedItemReasonSchema).optional(),
  relevanceScore: z.number().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialFeedFeedItemReason */
SocialFeedFeedItemReasonSchema = z.enum(['Following', 'Trending', 'Recommended', 'Mentioned', 'Replied', 'Liked', 'InNetwork']);

/** Zod schema for SocialGroupsApproveSocialGroupMemberInput */
SocialGroupsApproveSocialGroupMemberInputSchema = z.object({
  approvedByUserId: z.string().uuid().optional(),
});

/** Zod schema for SocialGroupsChangeSocialGroupMemberRoleInput */
SocialGroupsChangeSocialGroupMemberRoleInputSchema = z.object({
  role: z.lazy(() => SocialGroupsSocialGroupMemberRoleSchema).optional(),
});

/** Zod schema for SocialGroupsCreateSocialGroupInput */
SocialGroupsCreateSocialGroupInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  ownerId: z.string().uuid().optional(),
  slug: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
});

/** Zod schema for SocialGroupsJoinSocialGroupInput */
SocialGroupsJoinSocialGroupInputSchema = z.object({
  requestedRole: z.lazy(() => SocialGroupsSocialGroupMemberRoleSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialGroupsSocialGroup */
SocialGroupsSocialGroupSchema = z.object({
  createdAt: z.string().datetime().optional(),
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  memberCount: z.number().int().optional(),
  name: z.string().nullable().optional(),
  ownerId: z.string().uuid().optional(),
  pendingMemberCount: z.number().int().optional(),
  slug: z.string().nullable().optional(),
  status: z.lazy(() => SocialGroupsSocialGroupStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
});

/** Zod schema for SocialGroupsSocialGroupMember */
SocialGroupsSocialGroupMemberSchema = z.object({
  approvedByUserId: z.string().uuid().nullable().optional(),
  groupId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  joinedAt: z.string().datetime().nullable().optional(),
  removedAt: z.string().datetime().nullable().optional(),
  requestedAt: z.string().datetime().optional(),
  role: z.lazy(() => SocialGroupsSocialGroupMemberRoleSchema).optional(),
  status: z.lazy(() => SocialGroupsSocialGroupMembershipStatusSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialGroupsSocialGroupMemberRole */
SocialGroupsSocialGroupMemberRoleSchema = z.enum(['Owner', 'Admin', 'Moderator', 'Member']);

/** Zod schema for SocialGroupsSocialGroupMembershipStatus */
SocialGroupsSocialGroupMembershipStatusSchema = z.enum(['Pending', 'Active', 'Rejected', 'Removed']);

/** Zod schema for SocialGroupsSocialGroupStatus */
SocialGroupsSocialGroupStatusSchema = z.enum(['Active', 'Archived', 'Suspended']);

/** Zod schema for SocialGroupsSocialGroupType */
SocialGroupsSocialGroupTypeSchema = z.enum(['StudyGroup', 'ProjectTeam', 'InterestCommunity', 'CourseCohort', 'Institution', 'GameJamTeam']);

/** Zod schema for SocialGroupsSocialGroupVisibility */
SocialGroupsSocialGroupVisibilitySchema = z.enum(['Public', 'Private', 'InviteOnly']);

/** Zod schema for SocialGroupsUpdateSocialGroupInput */
SocialGroupsUpdateSocialGroupInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
  type: z.lazy(() => SocialGroupsSocialGroupTypeSchema).optional(),
  visibility: z.lazy(() => SocialGroupsSocialGroupVisibilitySchema).optional(),
});

/** Zod schema for SocialProfilesAddProfilePortfolioItemBody */
SocialProfilesAddProfilePortfolioItemBodySchema = z.object({
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  projectId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
});

/** Zod schema for SocialProfilesAddProfileSkillBody */
SocialProfilesAddProfileSkillBodySchema = z.object({
  displayOrder: z.number().int().optional(),
  name: z.string().nullable().optional(),
  proficiency: z.lazy(() => SocialProfilesProfileSkillProficiencySchema).optional(),
});

/** Zod schema for SocialProfilesProfileAvailabilityStatus */
SocialProfilesProfileAvailabilityStatusSchema = z.enum(['NotSet', 'OpenToWork', 'OpenToCollaborate', 'Busy', 'Hidden']);

/** Zod schema for SocialProfilesProfilePortfolioItem */
SocialProfilesProfilePortfolioItemSchema = z.object({
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  id: z.string().uuid().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  profileId: z.string().uuid().optional(),
  projectId: z.string().uuid().nullable().optional(),
  title: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
});

/** Zod schema for SocialProfilesProfileSkill */
SocialProfilesProfileSkillSchema = z.object({
  displayOrder: z.number().int().optional(),
  id: z.string().uuid().optional(),
  name: z.string().nullable().optional(),
  proficiency: z.lazy(() => SocialProfilesProfileSkillProficiencySchema).optional(),
  profileId: z.string().uuid().optional(),
});

/** Zod schema for SocialProfilesProfileSkillProficiency */
SocialProfilesProfileSkillProficiencySchema = z.enum(['Beginner', 'Intermediate', 'Advanced', 'Expert']);

/** Zod schema for SocialProfilesProfileVisibility */
SocialProfilesProfileVisibilitySchema = z.enum(['Private', 'Connections', 'Public']);

/** Zod schema for SocialProfilesSocialProfile */
SocialProfilesSocialProfileSchema = z.object({
  availabilityStatus: z.lazy(() => SocialProfilesProfileAvailabilityStatusSchema).optional(),
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  completenessScore: z.number().int().optional(),
  displayName: z.string().nullable().optional(),
  followerCount: z.number().int().optional(),
  followingCount: z.number().int().optional(),
  handle: z.string().nullable().optional(),
  headline: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  location: z.string().nullable().optional(),
  portfolioItems: z
    .array(z.lazy(() => SocialProfilesProfilePortfolioItemSchema))
    .nullable()
    .optional(),
  postCount: z.number().int().optional(),
  projectCount: z.number().int().optional(),
  showActivity: z.boolean().optional(),
  showPortfolio: z.boolean().optional(),
  showSkills: z.boolean().optional(),
  skills: z
    .array(z.lazy(() => SocialProfilesProfileSkillSchema))
    .nullable()
    .optional(),
  socialLinksJson: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  verifiedAt: z.string().datetime().nullable().optional(),
  visibility: z.lazy(() => SocialProfilesProfileVisibilitySchema).optional(),
  websiteUrl: z.string().nullable().optional(),
});

/** Zod schema for SocialProfilesUpdateProfilePortfolioItemBody */
SocialProfilesUpdateProfilePortfolioItemBodySchema = z.object({
  description: z.string().nullable().optional(),
  displayOrder: z.number().int().optional(),
  imageUrl: z.string().nullable().optional(),
  isPinned: z.boolean().optional(),
  title: z.string().nullable().optional(),
  url: z.string().nullable().optional(),
});

/** Zod schema for SocialProfilesUpdateProfilePrivacyBody */
SocialProfilesUpdateProfilePrivacyBodySchema = z.object({
  showActivity: z.boolean().optional(),
  showPortfolio: z.boolean().optional(),
  showSkills: z.boolean().optional(),
  visibility: z.lazy(() => SocialProfilesProfileVisibilitySchema).optional(),
});

/** Zod schema for SocialProfilesUpdateProfileStatsBody */
SocialProfilesUpdateProfileStatsBodySchema = z.object({
  followerCount: z.number().int().optional(),
  followingCount: z.number().int().optional(),
  postCount: z.number().int().optional(),
  projectCount: z.number().int().optional(),
});

/** Zod schema for SocialProfilesUpdateSocialProfileBody */
SocialProfilesUpdateSocialProfileBodySchema = z.object({
  availabilityStatus: z.lazy(() => SocialProfilesProfileAvailabilityStatusSchema).optional(),
  avatarUrl: z.string().nullable().optional(),
  bannerUrl: z.string().nullable().optional(),
  bio: z.string().nullable().optional(),
  displayName: z.string().nullable().optional(),
  handle: z.string().nullable().optional(),
  headline: z.string().nullable().optional(),
  location: z.string().nullable().optional(),
  socialLinksJson: z.string().nullable().optional(),
  timeZone: z.string().nullable().optional(),
  websiteUrl: z.string().nullable().optional(),
});

/** Zod schema for SocialReactionsReaction */
SocialReactionsReactionSchema = z.object({
  createdAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  type: z.lazy(() => SocialReactionsReactionTypeSchema).optional(),
  updatedAt: z.string().datetime().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialReactionsReactionTargetType */
SocialReactionsReactionTargetTypeSchema = z.enum(['Post', 'Comment', 'BlogPost', 'CourseReview', 'Discussion', 'Reply']);

/** Zod schema for SocialReactionsReactionType */
SocialReactionsReactionTypeSchema = z.enum(['Like', 'Love', 'Insightful', 'Celebrate', 'Support', 'Curious']);

/** Zod schema for SocialReactionsRemoveReactionInput */
SocialReactionsRemoveReactionInputSchema = z.object({
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialReactionsSetReactionInput */
SocialReactionsSetReactionInputSchema = z.object({
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  type: z.lazy(() => SocialReactionsReactionTypeSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for SocialReactionsTargetReactionSummary */
SocialReactionsTargetReactionSummarySchema = z.object({
  counts: z
    .object({
      Celebrate: z.number().int(),
      Curious: z.number().int(),
      Insightful: z.number().int(),
      Like: z.number().int(),
      Love: z.number().int(),
      Support: z.number().int(),
    })
    .nullable()
    .optional(),
  targetId: z.string().uuid().optional(),
  targetType: z.lazy(() => SocialReactionsReactionTargetTypeSchema).optional(),
  total: z.number().int().optional(),
});

/** Zod schema for SystemDayOfWeek */
SystemDayOfWeekSchema = z.enum(['Sunday', 'Monday', 'Tuesday', 'Wednesday', 'Thursday', 'Friday', 'Saturday']);

/** Zod schema for TenantInfo */
TenantInfoSchema = z.object({
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  name: z.string().nullable().optional(),
  slug: z.string().nullable().optional(),
});

/** Zod schema for TestingLabAddTestingEventCommitteeMemberInput */
TestingLabAddTestingEventCommitteeMemberInputSchema = z.object({
  isChair: z.boolean().optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabAssignTestingLabRoleInput */
TestingLabAssignTestingLabRoleInputSchema = z.object({
  expiresAt: z.string().datetime().nullable().optional(),
  roleName: z.string().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabAssignTestingProjectApplicationSlotInput */
TestingLabAssignTestingProjectApplicationSlotInputSchema = z.object({
  slotId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabAssignTestingProjectToTesterInput */
TestingLabAssignTestingProjectToTesterInputSchema = z.object({
  applicationId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabAttendanceStatus */
TestingLabAttendanceStatusSchema = z.enum(['Registered', 'Present', 'Completed', 'NoShow']);

/** Zod schema for TestingLabCancelTestingEventInput */
TestingLabCancelTestingEventInputSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for TestingLabCastTestingApplicationVoteInput */
TestingLabCastTestingApplicationVoteInputSchema = z.object({
  comments: z.string().nullable().optional(),
  decision: z.lazy(() => TestingLabTestingApplicationVoteDecisionSchema).optional(),
});

/** Zod schema for TestingLabConfigureTestingEventLearningInput */
TestingLabConfigureTestingEventLearningInputSchema = z.object({
  cohortId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().optional(),
  learningActivityId: z.string().uuid().optional(),
  requirement: z.lazy(() => TestingLabTestingLearningCompletionRequirementSchema).optional(),
});

/** Zod schema for TestingLabCreateSimpleTestingInput */
TestingLabCreateSimpleTestingInputSchema = z.object({
  description: z.string().nullable().optional(),
  downloadUrl: z.string().max(1000).nullable().optional(),
  endDate: z.string().datetime().nullable().optional(),
  feedbackFormContent: z.string().nullable().optional(),
  instructionsContent: z.string().nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsUrl: z.string().max(500).nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  projectId: z.string().uuid().nullable().optional(),
  startDate: z.string().datetime().nullable().optional(),
  teamIdentifier: z.string().max(100).nullable().optional(),
  title: z.string().min(1).max(255),
  versionNumber: z.string().min(1).max(50),
});

/** Zod schema for TestingLabCreateTestingEventInput */
TestingLabCreateTestingEventInputSchema = z.object({
  applicationsCloseAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  approvalMode: z.lazy(() => TestingLabTestingEventApprovalModeSchema).optional(),
  description: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().nullable().optional(),
  requiresFeedback: z.boolean().optional(),
  startsAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabCreateTestingLabRoleInput */
TestingLabCreateTestingLabRoleInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabCreateTestingLabSettings */
TestingLabCreateTestingLabSettingsSchema = z.object({
  allowPublicSignups: z.boolean().optional(),
  defaultSessionDuration: z.number().int().min(15).max(480),
  description: z.string().max(1000).nullable().optional(),
  enableNotifications: z.boolean().optional(),
  labName: z.string().min(1).max(255),
  maxSimultaneousSessions: z.number().int().min(1).max(100),
  requireApproval: z.boolean().optional(),
  timezone: z.string().min(1).max(50),
});

/** Zod schema for TestingLabCreateTestingLocation */
TestingLabCreateTestingLocationSchema = z.object({
  address: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  contactEmail: z.string().nullable().optional(),
  contactPhone: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  isVirtual: z.boolean().optional(),
  maxProjectsCapacity: z.number().int().optional(),
  maxTestersCapacity: z.number().int().optional(),
  name: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
  virtualUrl: z.string().nullable().optional(),
});

/** Zod schema for TestingLabCreateTestingInput */
TestingLabCreateTestingInputSchema = z.object({
  description: z.string().nullable().optional(),
  downloadUrl: z.string().max(1000).nullable().optional(),
  endDate: z.string().datetime(),
  feedbackFormContent: z.string().nullable().optional(),
  instructionsContent: z.string().nullable().optional(),
  instructionsFileId: z.string().uuid().nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsUrl: z.string().max(500).nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  projectVersionId: z.string().uuid(),
  startDate: z.string().datetime(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema),
  title: z.string().min(1).max(255),
});

/** Zod schema for TestingLabCreateTestingSession */
TestingLabCreateTestingSessionSchema = z.object({
  endTime: z.string().datetime(),
  locationId: z.string().uuid(),
  managerUserId: z.string().uuid(),
  maxProjects: z.number().int(),
  maxTesters: z.number().int(),
  sessionDate: z.string().datetime(),
  sessionName: z.string().min(1).max(255),
  startTime: z.string().datetime(),
  status: z.lazy(() => TestingLabSessionStatusSchema),
  testingRequestId: z.string().uuid(),
});

/** Zod schema for TestingLabDecideTestingProjectApplicationInput */
TestingLabDecideTestingProjectApplicationInputSchema = z.object({
  rationale: z.string().nullable().optional(),
  slotId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabFeedbackFormType */
TestingLabFeedbackFormTypeSchema = z.enum(['General', 'BugReport', 'Usability', 'Performance', 'Accessibility']);

/** Zod schema for TestingLabFeedbackQuality */
TestingLabFeedbackQualitySchema = z.enum(['Low', 'Medium', 'High']);

/** Zod schema for TestingLabFeedbackQualityRating */
TestingLabFeedbackQualityRatingSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  feedback: z.lazy(() => TestingLabTestingFeedbackSchema).optional(),
  feedbackId: z.string().uuid(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNegative: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isPositive: z.boolean().optional(),
  qualityRating: z.number().int().min(1).max(5),
  ratedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  ratedByUserId: z.string().uuid(),
  reason: z.string().max(500).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabFeedbackInput */
TestingLabFeedbackInputSchema = z.object({
  additionalNotes: z.string().nullable().optional(),
  feedbackData: z.string().nullable().optional(),
  feedbackFormId: z.string().uuid().optional(),
  sessionId: z.string().uuid().nullable().optional(),
  testingContext: z.lazy(() => TestingLabTestingContextSchema).optional(),
});

/** Zod schema for TestingLabGrantResourcePermissionInput */
TestingLabGrantResourcePermissionInputSchema = z.object({
  action: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabInstructionType */
TestingLabInstructionTypeSchema = z.enum(['Text', 'Url', 'File']);

/** Zod schema for TestingLabLinkSessionProjectInput */
TestingLabLinkSessionProjectInputSchema = z.object({
  notes: z.string().nullable().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabLocationStatus */
TestingLabLocationStatusSchema = z.enum(['Active', 'Maintenance', 'Inactive']);

/** Zod schema for TestingLabParticipationStatus */
TestingLabParticipationStatusSchema = z.enum(['Registered', 'Active', 'Completed', 'Withdrawn', 'Suspended']);

/** Zod schema for TestingLabPublicTestingEventProjection */
TestingLabPublicTestingEventProjectionSchema = z.object({
  applicationCount: z.number().int().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  approvalMode: z.lazy(() => TestingLabTestingEventApprovalModeSchema).optional(),
  description: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().nullable().optional(),
  requiresFeedback: z.boolean().optional(),
  slots: z
    .array(z.lazy(() => TestingLabPublicTestingEventSlotProjectionSchema))
    .nullable()
    .optional(),
  startsAt: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
});

/** Zod schema for TestingLabPublicTestingEventSlotProjection */
TestingLabPublicTestingEventSlotProjectionSchema = z.object({
  approvedProjectCount: z.number().int().optional(),
  availableProjectCount: z.number().int().nullable().optional(),
  availableTesterCount: z.number().int().nullable().optional(),
  campusName: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  maxProjects: z.number().int().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  registeredTesterCount: z.number().int().optional(),
  roomName: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabRateFeedbackQuality */
TestingLabRateFeedbackQualitySchema = z.object({
  quality: z.lazy(() => TestingLabFeedbackQualitySchema).optional(),
});

/** Zod schema for TestingLabRegisterTestingEventSlotInput */
TestingLabRegisterTestingEventSlotInputSchema = z.object({
  notes: z.string().nullable().optional(),
});

/** Zod schema for TestingLabRegistrationStatus */
TestingLabRegistrationStatusSchema = z.enum(['Registered', 'Confirmed', 'Cancelled', 'Attended', 'NoShow']);

/** Zod schema for TestingLabRegistrationType */
TestingLabRegistrationTypeSchema = z.enum(['ProjectMember', 'Tester']);

/** Zod schema for TestingLabReportFeedback */
TestingLabReportFeedbackSchema = z.object({
  reason: z.string().nullable().optional(),
});

/** Zod schema for TestingLabSessionProjectProjection */
TestingLabSessionProjectProjectionSchema = z.object({
  isActive: z.boolean().optional(),
  linkId: z.string().uuid().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  sessionId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabSessionRegistration */
TestingLabSessionRegistrationSchema = z.object({
  attendanceDuration: z.string().nullable().optional(),
  attendanceStatus: z.lazy(() => TestingLabAttendanceStatusSchema).optional(),
  attendedAt: z.string().datetime().nullable().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  confirmedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isCheckedIn: z.boolean().optional(),
  isCheckedOut: z.boolean().optional(),
  isConfirmed: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  notes: z.string().nullable().optional(),
  registeredAt: z.string().datetime(),
  registrationNotes: z.string().nullable().optional(),
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema).optional(),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  sessionId: z.string().uuid(),
  status: z.lazy(() => TestingLabRegistrationStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabSessionRegistrationInput */
TestingLabSessionRegistrationInputSchema = z.object({
  notes: z.string().nullable().optional(),
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema).optional(),
});

/** Zod schema for TestingLabSessionStatus */
TestingLabSessionStatusSchema = z.enum(['Scheduled', 'Active', 'Completed', 'Cancelled']);

/** Zod schema for TestingLabSessionWaitlist */
TestingLabSessionWaitlistSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  position: z.number().int(),
  registrationNotes: z.string().nullable().optional(),
  registrationType: z.lazy(() => TestingLabRegistrationTypeSchema),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  sessionId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabSubmitFeedback */
TestingLabSubmitFeedbackSchema = z.object({
  additionalNotes: z.string().nullable().optional(),
  feedbackResponses: z.string().min(1),
  overallRating: z.number().int().min(1).max(10).nullable().optional(),
  sessionId: z.string().uuid().nullable().optional(),
  testingRequestId: z.string().uuid(),
  wouldRecommend: z.boolean().nullable().optional(),
});

/** Zod schema for TestingLabSubmitTestingEventFeedbackInput */
TestingLabSubmitTestingEventFeedbackInputSchema = z.object({
  additionalNotes: z.string().nullable().optional(),
  feedbackData: z.string().nullable().optional(),
  overallRating: z.number().int().nullable().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
});

/** Zod schema for TestingLabSubmitTestingProjectApplicationInput */
TestingLabSubmitTestingProjectApplicationInputSchema = z.object({
  preferredAvailability: z.string().nullable().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabTestingApplicationStatus */
TestingLabTestingApplicationStatusSchema = z.enum(['Pending', 'UnderReview', 'Approved', 'Rejected', 'Waitlisted', 'Withdrawn']);

/** Zod schema for TestingLabTestingApplicationVote */
TestingLabTestingApplicationVoteSchema = z.object({
  application: z.lazy(() => TestingLabTestingProjectApplicationSchema).optional(),
  applicationId: z.string().uuid().optional(),
  comments: z.string().max(2000).nullable().optional(),
  createdAt: z.string().datetime(),
  decision: z.lazy(() => TestingLabTestingApplicationVoteDecisionSchema).optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  reviewer: z.lazy(() => IdentityUsersUserSchema).optional(),
  reviewerId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingApplicationVoteDecision */
TestingLabTestingApplicationVoteDecisionSchema = z.enum(['Approve', 'Reject', 'Abstain']);

/** Zod schema for TestingLabTestingApplicationVoteProjection */
TestingLabTestingApplicationVoteProjectionSchema = z.object({
  comments: z.string().nullable().optional(),
  createdAt: z.string().datetime().optional(),
  decision: z.lazy(() => TestingLabTestingApplicationVoteDecisionSchema).optional(),
  id: z.string().uuid().optional(),
  reviewerId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabTestingCommitteeMember */
TestingLabTestingCommitteeMemberSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isChair: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid().optional(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingContext */
TestingLabTestingContextSchema = z.enum(['Online', 'InPerson']);

/** Zod schema for TestingLabTestingEvent */
TestingLabTestingEventSchema = z.object({
  applications: z
    .array(z.lazy(() => TestingLabTestingProjectApplicationSchema))
    .nullable()
    .optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  approvalMode: z.lazy(() => TestingLabTestingEventApprovalModeSchema).optional(),
  cancellationReason: z.string().max(1000).nullable().optional(),
  cancelledAt: z.string().datetime().nullable().optional(),
  cohortId: z.string().uuid().nullable().optional(),
  committeeMembers: z
    .array(z.lazy(() => TestingLabTestingCommitteeMemberSchema))
    .nullable()
    .optional(),
  courseId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().max(2000).nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  endsAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  learningActivityId: z.string().uuid().nullable().optional(),
  learningCompletionRequirement: z.lazy(() => TestingLabTestingLearningCompletionRequirementSchema).optional(),
  manager: z.lazy(() => IdentityUsersUserSchema).optional(),
  managerUserId: z.string().uuid().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().min(1).max(255),
  requiresFeedback: z.boolean().optional(),
  slots: z
    .array(z.lazy(() => TestingLabTestingEventSlotSchema))
    .nullable()
    .optional(),
  startsAt: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingEventApprovalMode */
TestingLabTestingEventApprovalModeSchema = z.enum(['ManagerOnly', 'Committee']);

/** Zod schema for TestingLabTestingEventCommitteeMemberProjection */
TestingLabTestingEventCommitteeMemberProjectionSchema = z.object({
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isChair: z.boolean().optional(),
  userEmail: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
});

/** Zod schema for TestingLabTestingEventFeedbackProjection */
TestingLabTestingEventFeedbackProjectionSchema = z.object({
  additionalNotes: z.string().nullable().optional(),
  applicationId: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  feedbackData: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  overallRating: z.number().int().nullable().optional(),
  submittedAt: z.string().datetime().optional(),
  testerUserId: z.string().uuid().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
});

/** Zod schema for TestingLabTestingEventFeedbackReviewProjection */
TestingLabTestingEventFeedbackReviewProjectionSchema = z.object({
  applicationId: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  feedback: z.lazy(() => TestingLabTestingEventFeedbackProjectionSchema).optional(),
  fulfilledAt: z.string().datetime().nullable().optional(),
  obligationId: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  status: z.lazy(() => TestingLabTestingFeedbackObligationStatusSchema).optional(),
  testerUserId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabTestingEventMode */
TestingLabTestingEventModeSchema = z.enum(['Online', 'InPerson', 'Hybrid']);

/** Zod schema for TestingLabTestingEventProjection */
TestingLabTestingEventProjectionSchema = z.object({
  applicationCount: z.number().int().optional(),
  applicationsCloseAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  approvalMode: z.lazy(() => TestingLabTestingEventApprovalModeSchema).optional(),
  cohortId: z.string().uuid().nullable().optional(),
  courseId: z.string().uuid().nullable().optional(),
  description: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  id: z.string().uuid().optional(),
  learningActivityId: z.string().uuid().nullable().optional(),
  learningCompletionRequirement: z.lazy(() => TestingLabTestingLearningCompletionRequirementSchema).optional(),
  managerUserId: z.string().uuid().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().nullable().optional(),
  requiresFeedback: z.boolean().optional(),
  slotCount: z.number().int().optional(),
  startsAt: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
});

/** Zod schema for TestingLabTestingEventSlot */
TestingLabTestingEventSlotSchema = z.object({
  campusName: z.string().max(200).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  endsAt: z.string().datetime().optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isProjectCapacityUnlimited: z.boolean().optional(),
  isTesterCapacityUnlimited: z.boolean().optional(),
  location: z.lazy(() => TestingLabTestingLocationSchema).optional(),
  locationId: z.string().uuid().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  meetingUrl: z.string().max(1000).nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  roomName: z.string().max(200).nullable().optional(),
  startsAt: z.string().datetime().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingEventSlotProjection */
TestingLabTestingEventSlotProjectionSchema = z.object({
  approvedProjectCount: z.number().int().optional(),
  campusName: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  locationId: z.string().uuid().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  registeredTesterCount: z.number().int().optional(),
  roomName: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingEventStatus */
TestingLabTestingEventStatusSchema = z.enum(['Draft', 'ApplicationsOpen', 'ApplicationsClosed', 'Scheduled', 'Active', 'Completed', 'Cancelled']);

/** Zod schema for TestingLabTestingFeedback */
TestingLabTestingFeedbackSchema = z.object({
  additionalNotes: z.string().nullable().optional(),
  application: z.lazy(() => TestingLabTestingProjectApplicationSchema).optional(),
  applicationId: z.string().uuid().nullable().optional(),
  averageQualityRating: z.number().nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  eventId: z.string().uuid().nullable().optional(),
  feedbackData: z.string().min(1),
  feedbackForm: z.lazy(() => TestingLabTestingFeedbackFormSchema).optional(),
  feedbackFormId: z.string().uuid().nullable().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNegative: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isPositive: z.boolean().optional(),
  isReported: z.boolean().optional(),
  overallRating: z.number().int().min(1).max(10).nullable().optional(),
  qualityRating: z.lazy(() => TestingLabFeedbackQualitySchema).optional(),
  qualityRatings: z
    .array(z.lazy(() => TestingLabFeedbackQualityRatingSchema))
    .nullable()
    .optional(),
  reportReason: z.string().max(500).nullable().optional(),
  reportedAt: z.string().datetime().nullable().optional(),
  reportedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  reportedById: z.string().uuid().nullable().optional(),
  reportedByUserId: z.string().uuid().nullable().optional(),
  session: z.lazy(() => TestingLabTestingSessionSchema).optional(),
  sessionId: z.string().uuid().nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  testingContext: z.lazy(() => TestingLabTestingContextSchema),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  testingRequestId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
  wouldRecommend: z.boolean().nullable().optional(),
});

/** Zod schema for TestingLabTestingFeedbackForm */
TestingLabTestingFeedbackFormSchema = z.object({
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  formData: z.string().min(1),
  formSchema: z.string().nullable().optional(),
  formType: z.lazy(() => TestingLabFeedbackFormTypeSchema).optional(),
  formVersion: z.number().int().optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isForOnline: z.boolean().optional(),
  isForSessions: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  name: z.string().min(1).max(200),
  submissionCount: z.number().int().optional(),
  tagArray: z.array(z.string()).nullable().optional(),
  tags: z.string().max(500).nullable().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  testingRequestId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingFeedbackObligationProjection */
TestingLabTestingFeedbackObligationProjectionSchema = z.object({
  applicationId: z.string().uuid().optional(),
  eventId: z.string().uuid().optional(),
  feedbackId: z.string().uuid().nullable().optional(),
  fulfilledAt: z.string().datetime().nullable().optional(),
  id: z.string().uuid().optional(),
  slotId: z.string().uuid().optional(),
  status: z.lazy(() => TestingLabTestingFeedbackObligationStatusSchema).optional(),
  testerUserId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabTestingFeedbackObligationStatus */
TestingLabTestingFeedbackObligationStatusSchema = z.enum(['Pending', 'Fulfilled', 'Waived']);

/** Zod schema for TestingLabTestingLabAnalyticsReportProjection */
TestingLabTestingLabAnalyticsReportProjectionSchema = z.object({
  current: z.lazy(() => TestingLabTestingLabAnalyticsSummaryProjectionSchema).optional(),
  events: z
    .array(z.lazy(() => TestingLabTestingLabEventAnalyticsProjectionSchema))
    .nullable()
    .optional(),
  fromDate: z.string().datetime().optional(),
  generatedAt: z.string().datetime().optional(),
  locations: z.lazy(() => TestingLabTestingLabLocationAnalyticsProjectionSchema).optional(),
  previous: z.lazy(() => TestingLabTestingLabAnalyticsSummaryProjectionSchema).optional(),
  toDate: z.string().datetime().optional(),
  trend: z
    .array(z.lazy(() => TestingLabTestingLabAnalyticsTrendProjectionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingLabAnalyticsSummaryProjection */
TestingLabTestingLabAnalyticsSummaryProjectionSchema = z.object({
  applications: z.number().int().optional(),
  approvedProjects: z.number().int().optional(),
  attendedTesters: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  capacity: z.number().int().optional(),
  completedEvents: z.number().int().optional(),
  events: z.number().int().optional(),
  feedback: z.number().int().optional(),
  fillRate: z.number().optional(),
  recommendationRate: z.number().nullable().optional(),
  registeredTesters: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingLabAnalyticsTrendProjection */
TestingLabTestingLabAnalyticsTrendProjectionSchema = z.object({
  applications: z.number().int().optional(),
  attendance: z.number().int().optional(),
  date: z.string().datetime().optional(),
  events: z.number().int().optional(),
  feedback: z.number().int().optional(),
  registrations: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingLabEventAnalyticsProjection */
TestingLabTestingLabEventAnalyticsProjectionSchema = z.object({
  applications: z.number().int().optional(),
  approvedProjects: z.number().int().optional(),
  attendedTesters: z.number().int().optional(),
  averageRating: z.number().nullable().optional(),
  capacity: z.number().int().optional(),
  eventId: z.string().uuid().optional(),
  feedback: z.number().int().optional(),
  fillRate: z.number().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().nullable().optional(),
  registeredTesters: z.number().int().optional(),
  startsAt: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingEventStatusSchema).optional(),
});

/** Zod schema for TestingLabTestingLabLocationAnalyticsProjection */
TestingLabTestingLabLocationAnalyticsProjectionSchema = z.object({
  active: z.number().int().optional(),
  total: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingLabPermissions */
TestingLabTestingLabPermissionsSchema = z.object({
  canApproveRequests: z.boolean().optional(),
  canCreateFeedback: z.boolean().optional(),
  canCreateLocations: z.boolean().optional(),
  canCreateRequests: z.boolean().optional(),
  canCreateSessions: z.boolean().optional(),
  canDeleteFeedback: z.boolean().optional(),
  canDeleteLocations: z.boolean().optional(),
  canDeleteRequests: z.boolean().optional(),
  canDeleteSessions: z.boolean().optional(),
  canEditFeedback: z.boolean().optional(),
  canEditLocations: z.boolean().optional(),
  canEditRequests: z.boolean().optional(),
  canEditSessions: z.boolean().optional(),
  canManageParticipants: z.boolean().optional(),
  canModerateFeedback: z.boolean().optional(),
  canViewFeedback: z.boolean().optional(),
  canViewLocations: z.boolean().optional(),
  canViewParticipants: z.boolean().optional(),
  canViewRequests: z.boolean().optional(),
  canViewSessions: z.boolean().optional(),
});

/** Zod schema for TestingLabTestingLabResourcePermission */
TestingLabTestingLabResourcePermissionSchema = z.object({
  action: z.string().nullable().optional(),
  expiresAt: z.string().datetime().nullable().optional(),
  resourceId: z.string().uuid().optional(),
  resourceType: z.string().nullable().optional(),
});

/** Zod schema for TestingLabTestingLabRoleTemplate */
TestingLabTestingLabRoleTemplateSchema = z.object({
  description: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isSystemRole: z.boolean().optional(),
  name: z.string().nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabTestingLabSettings */
TestingLabTestingLabSettingsSchema = z.object({
  allowPublicSignups: z.boolean().optional(),
  createdAt: z.string().datetime().optional(),
  defaultSessionDuration: z.number().int().optional(),
  description: z.string().nullable().optional(),
  enableNotifications: z.boolean().optional(),
  id: z.string().uuid().optional(),
  labName: z.string().nullable().optional(),
  maxSimultaneousSessions: z.number().int().optional(),
  requireApproval: z.boolean().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  timezone: z.string().nullable().optional(),
  updatedAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabTestingLearningCompletionRequirement. A comma-separated combination of the declared flag names. */
TestingLabTestingLearningCompletionRequirementSchema = z.string();

/** Zod schema for TestingLabTestingLocation */
TestingLabTestingLocationSchema = z.object({
  activeSessionCount: z.number().int().optional(),
  address: z.string().max(500).nullable().optional(),
  capacity: z.number().int().nullable().optional(),
  city: z.string().max(100).nullable().optional(),
  contactEmail: z.string().max(255).nullable().optional(),
  contactPhone: z.string().max(50).nullable().optional(),
  country: z.string().max(100).nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  equipment: z.string().nullable().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  fullAddress: z.string().nullable().optional(),
  id: z.string().uuid().optional(),
  isAvailable: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  isVirtual: z.boolean().optional(),
  maxProjectsCapacity: z.number().int().optional(),
  maxTestersCapacity: z.number().int().optional(),
  name: z.string().min(1).max(200),
  postalCode: z.string().max(20).nullable().optional(),
  sessions: z
    .array(z.lazy(() => TestingLabTestingSessionSchema))
    .nullable()
    .optional(),
  state: z.string().max(100).nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  virtualUrl: z.string().max(500).nullable().optional(),
});

/** Zod schema for TestingLabTestingMode */
TestingLabTestingModeSchema = z.enum(['Online', 'InPerson', 'Hybrid']);

/** Zod schema for TestingLabTestingParticipant */
TestingLabTestingParticipantSchema = z.object({
  canProvideFeedback: z.boolean().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  createdAt: z.string().datetime(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  feedbackCount: z.number().int().optional(),
  id: z.string().uuid().optional(),
  instructionsAcknowledged: z.boolean(),
  instructionsAcknowledgedAt: z.string().datetime().nullable().optional(),
  isActive: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  notes: z.string().nullable().optional(),
  participationDuration: z.string().nullable().optional(),
  startedAt: z.string().datetime(),
  status: z.lazy(() => TestingLabParticipationStatusSchema).optional(),
  tenantId: z.string().uuid().nullable().optional(),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  testingRequestId: z.string().uuid(),
  timeSpentMinutes: z.number().int().nullable().optional(),
  updatedAt: z.string().datetime(),
  user: z.lazy(() => IdentityUsersUserSchema).optional(),
  userId: z.string().uuid(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingParticipantDirectoryItemProjection */
TestingLabTestingParticipantDirectoryItemProjectionSchema = z.object({
  avatarUrl: z.string().nullable().optional(),
  campusName: z.string().nullable().optional(),
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  eventId: z.string().uuid().optional(),
  eventName: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  notes: z.string().nullable().optional(),
  pendingFeedbackCount: z.number().int().optional(),
  registeredAt: z.string().datetime().optional(),
  registrationId: z.string().uuid().optional(),
  roomName: z.string().nullable().optional(),
  slotId: z.string().uuid().optional(),
  startsAt: z.string().datetime().optional(),
  status: z.lazy(() => TestingLabTestingSlotRegistrationStatusSchema).optional(),
  userEmail: z.string().nullable().optional(),
  userId: z.string().uuid().optional(),
  userName: z.string().nullable().optional(),
  waitlistPosition: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabTestingParticipantDirectoryProjection */
TestingLabTestingParticipantDirectoryProjectionSchema = z.object({
  attendedCount: z.number().int().optional(),
  checkedInCount: z.number().int().optional(),
  completedCount: z.number().int().optional(),
  items: z
    .array(z.lazy(() => TestingLabTestingParticipantDirectoryItemProjectionSchema))
    .nullable()
    .optional(),
  noShowCount: z.number().int().optional(),
  registeredCount: z.number().int().optional(),
  totalCount: z.number().int().optional(),
  waitlistedCount: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingPriority */
TestingLabTestingPrioritySchema = z.enum(['Low', 'Medium', 'High', 'Critical']);

/** Zod schema for TestingLabTestingProjectApplication */
TestingLabTestingProjectApplicationSchema = z.object({
  assignedSlot: z.lazy(() => TestingLabTestingEventSlotSchema).optional(),
  assignedSlotId: z.string().uuid().nullable().optional(),
  createdAt: z.string().datetime(),
  decidedAt: z.string().datetime().nullable().optional(),
  decidedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  decidedByUserId: z.string().uuid().nullable().optional(),
  decisionRationale: z.string().max(2000).nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  event: z.lazy(() => TestingLabTestingEventSchema).optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  preferredAvailability: z.string().max(1000).nullable().optional(),
  project: z.lazy(() => ProjectsProjectSchema).optional(),
  projectId: z.string().uuid().optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  status: z.lazy(() => TestingLabTestingApplicationStatusSchema).optional(),
  submittedBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  submittedByUserId: z.string().uuid().optional(),
  tenantId: z.string().uuid().nullable().optional(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
  votes: z
    .array(z.lazy(() => TestingLabTestingApplicationVoteSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingProjectApplicationProjection */
TestingLabTestingProjectApplicationProjectionSchema = z.object({
  assignedSlotId: z.string().uuid().nullable().optional(),
  decidedAt: z.string().datetime().nullable().optional(),
  decidedByUserId: z.string().uuid().nullable().optional(),
  decisionRationale: z.string().nullable().optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  preferredAvailability: z.string().nullable().optional(),
  projectId: z.string().uuid().optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  status: z.lazy(() => TestingLabTestingApplicationStatusSchema).optional(),
  submittedByUserId: z.string().uuid().optional(),
  votes: z
    .array(z.lazy(() => TestingLabTestingApplicationVoteProjectionSchema))
    .nullable()
    .optional(),
});

/** Zod schema for TestingLabTestingInput */
TestingLabTestingInputSchema = z.object({
  acceptsNewTesters: z.boolean().optional(),
  availableSpots: z.number().int().nullable().optional(),
  createdAt: z.string().datetime(),
  createdBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  createdById: z.string().uuid(),
  currentTesterCount: z.number().int().optional(),
  daysRemaining: z.number().int().nullable().optional(),
  deletedAt: z.string().datetime().nullable().optional(),
  description: z.string().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  downloadUrl: z.string().max(1000).nullable().optional(),
  duration: z.string().optional(),
  endDate: z.string().datetime(),
  estimatedDurationHours: z.number().int().nullable().optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  feedbackFormContent: z.string().nullable().optional(),
  feedbackForms: z
    .array(z.lazy(() => TestingLabTestingFeedbackFormSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  instructionsContent: z.string().nullable().optional(),
  instructionsFileId: z.string().uuid().nullable().optional(),
  instructionsType: z.lazy(() => TestingLabInstructionTypeSchema),
  instructionsUrl: z.string().max(500).nullable().optional(),
  isActive: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  maxTesters: z.number().int().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingModeSchema).optional(),
  participants: z
    .array(z.lazy(() => TestingLabTestingParticipantSchema))
    .nullable()
    .optional(),
  priority: z.lazy(() => TestingLabTestingPrioritySchema).optional(),
  projectVersion: z.lazy(() => ProjectsProjectVersionSchema).optional(),
  projectVersionId: z.string().uuid().nullable().optional(),
  sessions: z
    .array(z.lazy(() => TestingLabTestingSessionSchema))
    .nullable()
    .optional(),
  startDate: z.string().datetime(),
  status: z.lazy(() => TestingLabTestingRequestStatusSchema),
  tenantId: z.string().uuid().nullable().optional(),
  title: z.string().min(1).max(255),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingRequestStatus */
TestingLabTestingRequestStatusSchema = z.enum(['Draft', 'Open', 'Active', 'InProgress', 'Paused', 'Completed', 'Cancelled']);

/** Zod schema for TestingLabTestingSession */
TestingLabTestingSessionSchema = z.object({
  allowsRegistration: z.boolean().optional(),
  availableSpots: z.number().int().optional(),
  createdAt: z.string().datetime(),
  createdBy: z.lazy(() => IdentityUsersUserSchema).optional(),
  createdById: z.string().uuid(),
  deletedAt: z.string().datetime().nullable().optional(),
  domainEvents: z
    .array(z.lazy(() => CQRSIDomainEventSchema))
    .nullable()
    .optional(),
  duration: z.string().optional(),
  endTime: z.string().datetime(),
  eventSlot: z.lazy(() => TestingLabTestingEventSlotSchema).optional(),
  eventSlotId: z.string().uuid().nullable().optional(),
  feedback: z
    .array(z.lazy(() => TestingLabTestingFeedbackSchema))
    .nullable()
    .optional(),
  id: z.string().uuid().optional(),
  isActive: z.boolean().optional(),
  isCompleted: z.boolean().optional(),
  isDeleted: z.boolean().optional(),
  isGlobal: z.boolean().optional(),
  isNew: z.boolean().optional(),
  location: z.lazy(() => TestingLabTestingLocationSchema).optional(),
  locationId: z.string().uuid(),
  manager: z.lazy(() => IdentityUsersUserSchema).optional(),
  managerId: z.string().uuid(),
  managerUserId: z.string().uuid().optional(),
  maxProjects: z.number().int(),
  maxTesters: z.number().int(),
  registeredProjectCount: z.number().int().optional(),
  registeredProjectMemberCount: z.number().int().optional(),
  registeredTesterCount: z.number().int().optional(),
  registrations: z
    .array(z.lazy(() => TestingLabSessionRegistrationSchema))
    .nullable()
    .optional(),
  sessionDate: z.string().datetime(),
  sessionName: z.string().min(1).max(255),
  startTime: z.string().datetime(),
  status: z.lazy(() => TestingLabSessionStatusSchema),
  tenantId: z.string().uuid().nullable().optional(),
  testingRequest: z.lazy(() => TestingLabTestingInputSchema).optional(),
  testingRequestId: z.string().uuid(),
  updatedAt: z.string().datetime(),
  version: z.number().int().optional(),
});

/** Zod schema for TestingLabTestingSlotRegistrationProjection */
TestingLabTestingSlotRegistrationProjectionSchema = z.object({
  checkedInAt: z.string().datetime().nullable().optional(),
  checkedOutAt: z.string().datetime().nullable().optional(),
  completedAt: z.string().datetime().nullable().optional(),
  eventId: z.string().uuid().optional(),
  id: z.string().uuid().optional(),
  notes: z.string().nullable().optional(),
  pendingFeedbackCount: z.number().int().optional(),
  promotedAt: z.string().datetime().nullable().optional(),
  registeredAt: z.string().datetime().optional(),
  slotId: z.string().uuid().optional(),
  status: z.lazy(() => TestingLabTestingSlotRegistrationStatusSchema).optional(),
  userId: z.string().uuid().optional(),
  waitlistPosition: z.number().int().nullable().optional(),
});

/** Zod schema for TestingLabTestingSlotRegistrationStatus */
TestingLabTestingSlotRegistrationStatusSchema = z.enum(['Registered', 'Waitlisted', 'CheckedIn', 'Attended', 'Completed', 'Cancelled', 'NoShow']);

/** Zod schema for TestingLabUpdateAttendance */
TestingLabUpdateAttendanceSchema = z.object({
  attendanceStatus: z.lazy(() => TestingLabAttendanceStatusSchema).optional(),
  userId: z.string().uuid().optional(),
});

/** Zod schema for TestingLabUpdateTestingEventInput */
TestingLabUpdateTestingEventInputSchema = z.object({
  applicationsCloseAt: z.string().datetime().optional(),
  applicationsOpenAt: z.string().datetime().optional(),
  approvalMode: z.lazy(() => TestingLabTestingEventApprovalModeSchema).optional(),
  description: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  name: z.string().nullable().optional(),
  requiresFeedback: z.boolean().optional(),
  startsAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabUpdateTestingLabRoleInput */
TestingLabUpdateTestingLabRoleInputSchema = z.object({
  description: z.string().nullable().optional(),
  name: z.string().nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
});

/** Zod schema for TestingLabUpdateTestingLabSettings */
TestingLabUpdateTestingLabSettingsSchema = z.object({
  allowPublicSignups: z.boolean().nullable().optional(),
  defaultSessionDuration: z.number().int().min(15).max(480).nullable().optional(),
  description: z.string().max(1000).nullable().optional(),
  enableNotifications: z.boolean().nullable().optional(),
  labName: z.string().max(255).nullable().optional(),
  maxSimultaneousSessions: z.number().int().min(1).max(100).nullable().optional(),
  requireApproval: z.boolean().nullable().optional(),
  timezone: z.string().max(50).nullable().optional(),
});

/** Zod schema for TestingLabUpdateTestingLocation */
TestingLabUpdateTestingLocationSchema = z.object({
  address: z.string().nullable().optional(),
  city: z.string().nullable().optional(),
  contactEmail: z.string().nullable().optional(),
  contactPhone: z.string().nullable().optional(),
  country: z.string().nullable().optional(),
  description: z.string().nullable().optional(),
  equipmentAvailable: z.string().nullable().optional(),
  isVirtual: z.boolean().nullable().optional(),
  maxProjectsCapacity: z.number().int().nullable().optional(),
  maxTestersCapacity: z.number().int().nullable().optional(),
  name: z.string().nullable().optional(),
  postalCode: z.string().nullable().optional(),
  state: z.string().nullable().optional(),
  status: z.lazy(() => TestingLabLocationStatusSchema).optional(),
  virtualUrl: z.string().nullable().optional(),
});

/** Zod schema for TestingLabUpsertTestingEventSlotInput */
TestingLabUpsertTestingEventSlotInputSchema = z.object({
  campusName: z.string().nullable().optional(),
  endsAt: z.string().datetime().optional(),
  locationId: z.string().uuid().nullable().optional(),
  maxProjects: z.number().int().nullable().optional(),
  maxTesters: z.number().int().nullable().optional(),
  meetingUrl: z.string().nullable().optional(),
  mode: z.lazy(() => TestingLabTestingEventModeSchema).optional(),
  roomName: z.string().nullable().optional(),
  startsAt: z.string().datetime().optional(),
});

/** Zod schema for TestingLabUserTestingLabPermissions */
TestingLabUserTestingLabPermissionsSchema = z.object({
  assignedRoles: z.array(z.string()).nullable().optional(),
  permissions: z.lazy(() => TestingLabTestingLabPermissionsSchema).optional(),
  resourcePermissions: z
    .array(z.lazy(() => TestingLabTestingLabResourcePermissionSchema))
    .nullable()
    .optional(),
  tenantId: z.string().uuid().nullable().optional(),
  userId: z.string().uuid().optional(),
});
