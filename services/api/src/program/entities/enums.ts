// Enums based on the DBML schema

export enum Visibility {
  DRAFT = 'draft',
  PUBLISHED = 'published',
  ARCHIVED = 'archived',
}

export enum CertificateStatus {
  ACTIVE = 'active',
  EXPIRED = 'expired',
  REVOKED = 'revoked',
  PENDING = 'pending',
}

export enum VerificationMethod {
  CODE = 'code',
  BLOCKCHAIN = 'blockchain',
  BOTH = 'both',
}

export enum ProductType {
  PROGRAM = 'program',
  LEARNING_PATHWAY = 'learning_pathway',
  BUNDLE = 'bundle',
  SUBSCRIPTION = 'subscription',
  WORKSHOP = 'workshop',
  MENTORSHIP = 'mentorship',
  EBOOK = 'ebook',
  RESOURCE_PACK = 'resource_pack',
  COMMUNITY = 'community',
  CERTIFICATION = 'certification',
  OTHER = 'other',
}

export enum CertificateType {
  PROGRAM_COMPLETION = 'program_completion',
  PRODUCT_BUNDLE_COMPLETION = 'product_bundle_completion',
  LEARNING_PATHWAY = 'learning_pathway',
  SKILL_MASTERY = 'skill_mastery',
  EVENT_PARTICIPATION = 'event_participation',
  ASSESSMENT_PASSED = 'assessment_passed',
  PROJECT_COMPLETION = 'project_completion',
  SPECIALIZATION = 'specialization',
  PROFESSIONAL = 'professional',
  ACHIEVEMENT = 'achievement',
  INSTRUCTOR = 'instructor',
  TIME_INVESTMENT = 'time_investment',
  PEER_RECOGNITION = 'peer_recognition',
}

export enum SkillProficiencyLevel {
  AWARENESS = 'awareness',
  NOVICE = 'novice',
  BEGINNER = 'beginner',
  INTERMEDIATE = 'intermediate',
  ADVANCED = 'advanced',
  EXPERT = 'expert',
  MASTER = 'master',
}

export enum TagType {
  SKILL = 'skill',
  TOPIC = 'topic',
  TECHNOLOGY = 'technology',
  DIFFICULTY = 'difficulty',
  CATEGORY = 'category',
  INDUSTRY = 'industry',
  CERTIFICATION = 'certification',
}

export enum TagRelationshipType {
  RELATED = 'related',
  PARENT = 'parent',
  CHILD = 'child',
  REQUIRES = 'requires',
  SUGGESTED = 'suggested',
}

export enum GradingMethod {
  INSTRUCTOR = 'instructor',
  PEER = 'peer',
  AI = 'ai',
  AUTOMATED_TESTS = 'automated_tests',
}

export enum ProgramContentType {
  PAGE = 'page',
  ASSIGNMENT = 'assignment',
  QUESTIONNAIRE = 'questionnaire',
  DISCUSSION = 'discussion',
  CODE = 'code',
  CHALLENGE = 'challenge',
  REFLECTION = 'reflection',
  SURVEY = 'survey',
}

export enum ProgramRoleType {
  STUDENT = 'student',
  INSTRUCTOR = 'instructor',
  EDITOR = 'editor',
  ADMINISTRATOR = 'administrator',
  TEACHING_ASSISTANT = 'teaching_assistant',
}

export enum PromoCodeType {
  PERCENTAGE_OFF = 'percentage_off',
  FIXED_AMOUNT_OFF = 'fixed_amount_off',
  BUY_ONE_GET_ONE = 'buy_one_get_one',
  FIRST_MONTH_FREE = 'first_month_free',
}

export enum SubscriptionType {
  MONTHLY = 'monthly',
  QUARTERLY = 'quarterly',
  ANNUAL = 'annual',
  LIFETIME = 'lifetime',
}

export enum SubscriptionBillingInterval {
  DAY = 'day',
  WEEK = 'week',
  MONTH = 'month',
  YEAR = 'year',
}

export enum SubscriptionStatus {
  ACTIVE = 'active',
  TRIALING = 'trialing',
  PAST_DUE = 'past_due',
  CANCELED = 'canceled',
  INCOMPLETE = 'incomplete',
  INCOMPLETE_EXPIRED = 'incomplete_expired',
  UNPAID = 'unpaid',
}

export enum PaymentMethodType {
  CREDIT_CARD = 'credit_card',
  DEBIT_CARD = 'debit_card',
  CRYPTO_WALLET = 'crypto_wallet',
  WALLET_BALANCE = 'wallet_balance',
  BANK_TRANSFER = 'bank_transfer',
}

export enum PaymentMethodStatus {
  ACTIVE = 'active',
  INACTIVE = 'inactive',
  EXPIRED = 'expired',
  REMOVED = 'removed',
}

export enum TransactionStatus {
  PENDING = 'pending',
  PROCESSING = 'processing',
  COMPLETED = 'completed',
  FAILED = 'failed',
  REFUNDED = 'refunded',
  CANCELLED = 'cancelled',
}

export enum TransactionType {
  PURCHASE = 'purchase',
  REFUND = 'refund',
  WITHDRAWAL = 'withdrawal',
  DEPOSIT = 'deposit',
  TRANSFER = 'transfer',
  FEE = 'fee',
  ADJUSTMENT = 'adjustment',
}

export enum BalanceTransactionType {
  CREDIT = 'credit',
  DEBIT = 'debit',
  TRANSFER = 'transfer',
  REFUND = 'refund',
  FEE = 'fee',
  ADJUSTMENT = 'adjustment',
}

export enum BalanceTransactionStatus {
  PENDING = 'pending',
  COMPLETED = 'completed',
  FAILED = 'failed',
  REVERSED = 'reversed',
}

export enum WalletStatus {
  ACTIVE = 'active',
  FROZEN = 'frozen',
  CLOSED = 'closed',
}

export enum ProductAcquisitionType {
  PURCHASE = 'purchase',
  SUBSCRIPTION = 'subscription',
  FREE = 'free',
  GIFT = 'gift',
}

export enum ProductAccessStatus {
  ACTIVE = 'active',
  EXPIRED = 'expired',
  REVOKED = 'revoked',
  SUSPENDED = 'suspended',
}

export enum KycProvider {
  SUMSUB = 'sumsub',
  SHUFTI = 'shufti',
  ONFIDO = 'onfido',
  JUMIO = 'jumio',
  CUSTOM = 'custom',
}

export enum KycVerificationStatus {
  PENDING = 'pending',
  IN_PROGRESS = 'in_progress',
  APPROVED = 'approved',
  REJECTED = 'rejected',
  SUSPENDED = 'suspended',
  EXPIRED = 'expired',
}

export enum ProgressStatus {
  NOT_STARTED = 'not_started',
  IN_PROGRESS = 'in_progress',
  COMPLETED = 'completed',
  SKIPPED = 'skipped',
}

export enum FeedbackFormQuestionType {
  SHORT_ANSWER = 'short_answer',
  LONG_ANSWER = 'long_answer',
  MULTIPLE_CHOICE = 'multiple_choice',
  CHECKBOX = 'checkbox',
  RATING_SCALE = 'rating_scale',
  YES_NO = 'yes_no',
  DROPDOWN = 'dropdown',
}

export enum CertificateTagRelationshipType {
  REQUIREMENT = 'requirement',
  RECOMMENDATION = 'recommendation',
  PROVIDES = 'provides',
}

export enum ModerationStatus {
  PENDING = 'pending',
  APPROVED = 'approved',
  REJECTED = 'rejected',
  FLAGGED = 'flagged',
}

// Additional enums needed for the new entities
export enum GradeStatus {
  PENDING = 'pending',
  GRADED = 'graded',
  NEEDS_REVIEW = 'needs_review',
  DISPUTED = 'disputed',
}

export enum PricingType {
  ONE_TIME = 'one_time',
  SUBSCRIPTION = 'subscription',
  TIER_BASED = 'tier_based',
  USAGE_BASED = 'usage_based',
}

export enum CurrencyCode {
  USD = 'USD',
  EUR = 'EUR',
  GBP = 'GBP',
  CAD = 'CAD',
  AUD = 'AUD',
  JPY = 'JPY',
  BTC = 'BTC',
  ETH = 'ETH',
}

export enum PlanType {
  BASIC = 'basic',
  PREMIUM = 'premium',
  PRO = 'pro',
  ENTERPRISE = 'enterprise',
}

export enum DiscountType {
  PERCENTAGE = 'percentage',
  FIXED_AMOUNT = 'fixed_amount',
}

export enum ProductAccessType {
  FULL = 'full',
  TRIAL = 'trial',
  LIMITED = 'limited',
  PREVIEW = 'preview',
}

export enum KycStatus {
  PENDING = 'pending',
  APPROVED = 'approved',
  REJECTED = 'rejected',
  EXPIRED = 'expired',
}

export enum KycDocumentType {
  PASSPORT = 'passport',
  DRIVERS_LICENSE = 'drivers_license',
  NATIONAL_ID = 'national_id',
  UTILITY_BILL = 'utility_bill',
  BANK_STATEMENT = 'bank_statement',
}

export enum RatingType {
  PRODUCT = 'product',
  PROGRAM = 'program',
  CONTENT = 'content',
  INSTRUCTOR = 'instructor',
}
