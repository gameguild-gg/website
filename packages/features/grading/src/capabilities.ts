import type { AssessmentReviewMethod } from "./review-methods";
import type { AssessmentExecutionManifestV1, ReviewExecutionContext } from "./types";

export type ExecutableComponentKind =
  | "assessment-type-adapter"
  | "execution-policy";

export interface ExecutableComponentDescriptorV1 {
  kind: ExecutableComponentKind;
  key: string;
  version: string;
  contexts: readonly ReviewExecutionContext[];
}

export interface ReviewCapabilityDescriptorV1 {
  method: AssessmentReviewMethod;
  contexts: readonly ReviewExecutionContext[];
  handlerKey: string;
  handlerVersion: string;
  providerKey?: string;
}

export interface CapabilityResolutionIssueV1 {
  kind: ExecutableComponentKind | "review-method";
  key: string;
  version?: string;
  context: ReviewExecutionContext;
  message: string;
}

export interface IReviewCapabilityRegistry {
  registerComponent(descriptor: ExecutableComponentDescriptorV1): void;
  registerReview(descriptor: ReviewCapabilityDescriptorV1): void;
  resolveComponent(
    kind: ExecutableComponentKind,
    key: string,
    version: string,
    context: ReviewExecutionContext,
  ): ExecutableComponentDescriptorV1 | null;
  resolveReview(
    method: AssessmentReviewMethod,
    handlerKey: string,
    handlerVersion: string,
    context: ReviewExecutionContext,
  ): ReviewCapabilityDescriptorV1 | null;
  validateManifest(
    manifest: AssessmentExecutionManifestV1,
    context: ReviewExecutionContext,
  ): CapabilityResolutionIssueV1[];
}

export class ReviewCapabilityRegistry implements IReviewCapabilityRegistry {
  readonly #components = new Map<string, ExecutableComponentDescriptorV1>();
  readonly #reviews = new Map<string, ReviewCapabilityDescriptorV1>();

  registerComponent(descriptor: ExecutableComponentDescriptorV1): void {
    const key = componentRegistryKey(descriptor.kind, descriptor.key, descriptor.version);
    rejectDivergentRegistration(this.#components.get(key), descriptor, key);
    this.#components.set(key, freezeDescriptor(descriptor));
  }

  registerReview(descriptor: ReviewCapabilityDescriptorV1): void {
    const key = reviewRegistryKey(descriptor.method, descriptor.handlerKey, descriptor.handlerVersion);
    rejectDivergentRegistration(this.#reviews.get(key), descriptor, key);
    this.#reviews.set(key, freezeDescriptor(descriptor));
  }

  resolveComponent(
    kind: ExecutableComponentKind,
    key: string,
    version: string,
    context: ReviewExecutionContext,
  ): ExecutableComponentDescriptorV1 | null {
    const descriptor = this.#components.get(componentRegistryKey(kind, key, version));
    return descriptor?.contexts.includes(context) ? descriptor : null;
  }

  resolveReview(
    method: AssessmentReviewMethod,
    handlerKey: string,
    handlerVersion: string,
    context: ReviewExecutionContext,
  ): ReviewCapabilityDescriptorV1 | null {
    const descriptor = this.#reviews.get(reviewRegistryKey(method, handlerKey, handlerVersion));
    return descriptor?.contexts.includes(context) ? descriptor : null;
  }

  validateManifest(
    manifest: AssessmentExecutionManifestV1,
    context: ReviewExecutionContext,
  ): CapabilityResolutionIssueV1[] {
    const issues: CapabilityResolutionIssueV1[] = [];
    for (const item of manifest.items) {
      collectComponentIssue(issues, this, "assessment-type-adapter", item.adapterKey, item.adapterVersion, context);
    }
    for (const stage of manifest.stages) {
      if (!this.resolveReview(stage.method, stage.handlerKey, stage.handlerVersion, context)) {
        issues.push({
          kind: "review-method",
          key: stage.handlerKey,
          version: stage.handlerVersion,
          context,
          message: `${stage.method} handler ${stage.handlerKey}@${stage.handlerVersion} is unavailable for ${context}.`,
        });
      }
    }
    for (const policy of manifest.policies) {
      collectComponentIssue(issues, this, "execution-policy", policy.policyKey, policy.policyVersion, context);
    }
    return issues;
  }
}

function collectComponentIssue(
  issues: CapabilityResolutionIssueV1[],
  registry: IReviewCapabilityRegistry,
  kind: ExecutableComponentKind,
  key: string,
  version: string,
  context: ReviewExecutionContext,
): void {
  if (registry.resolveComponent(kind, key, version, context)) return;
  issues.push({
    kind,
    key,
    version,
    context,
    message: `${kind} ${key}@${version} is unavailable for ${context}.`,
  });
}

function componentRegistryKey(kind: ExecutableComponentKind, key: string, version: string): string {
  return `${kind}:${key}@${version}`;
}

function reviewRegistryKey(method: AssessmentReviewMethod, key: string, version: string): string {
  return `${method}:${key}@${version}`;
}

function freezeDescriptor<T extends { contexts: readonly ReviewExecutionContext[] }>(descriptor: T): T {
  return Object.freeze({ ...descriptor, contexts: Object.freeze([...descriptor.contexts]) });
}

function rejectDivergentRegistration<T>(current: T | undefined, next: T, key: string): void {
  if (!current) return;
  if (JSON.stringify(current) !== JSON.stringify(next)) {
    throw new Error(`Capability ${key} is already registered with a different descriptor.`);
  }
}
