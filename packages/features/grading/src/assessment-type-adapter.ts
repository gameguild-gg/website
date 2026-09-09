import type { ExecutableComponentDescriptorV1 } from "./capabilities";
import type {
  AssessmentExecutionDeliveryV1,
  AssessmentItemManifestV1,
  AssessmentItemProjectionV1,
  AssessmentResponseEnvelopeV1,
  GradeResultV1,
  ReviewExecutionContext,
} from "./types";

/**
 * Complete executable boundary for one assessment content type.
 * A single key/version binds projection, learner delivery, response decoding,
 * and deterministic evaluation so the grading core never imports that type.
 */
export interface AssessmentTypeAdapterV1<
  TAuthoring,
  TProjection extends AssessmentItemProjectionV1,
  TLearnerPayload,
  TDecodedResponse,
  TEvaluationContext = undefined,
> {
  readonly key: string;
  readonly version: string;
  readonly contentType: string;
  readonly isCurrentForAuthoring: boolean;
  readonly contexts: readonly ReviewExecutionContext[];
  readonly capability: ExecutableComponentDescriptorV1 & {
    readonly kind: "assessment-type-adapter";
  };

  projectAuthoring(authoring: TAuthoring): readonly TProjection[];
  createManifest(items: readonly TProjection[]): AssessmentItemManifestV1[];
  createDelivery(
    definitionRevisionId: string,
    executionSnapshotHash: string,
    items: readonly TProjection[],
    itemOrder?: readonly string[],
  ): AssessmentExecutionDeliveryV1<TLearnerPayload>;
  decodeResponse(
    envelope: AssessmentResponseEnvelopeV1,
    items: readonly TProjection[],
  ): TDecodedResponse;
  evaluateDeterministic(
    items: readonly TProjection[],
    response: TDecodedResponse,
    context?: TEvaluationContext,
  ): GradeResultV1;
}
