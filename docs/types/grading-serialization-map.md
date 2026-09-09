# Grading: mapa de tipos, serializacao e fronteiras

## Objetivo

Este documento identifica a fonte de verdade dos contratos de grading e o
limite entre grading generico, quiz e persistencia. Ele descreve apenas o corte
novo, sem formatos legacy, dual-read ou aliases de compatibilidade.

## Direcao de dependencias

```text
@game-guild/grading <--- @game-guild/grading-adapter-quiz ---> @game-guild/quiz
        ^                              ^
        |                              |
@game-guild/quiz-content          composicao web

Learning.Assessments.Grading <--- Learning.Assessments.QuizAdapter
```

- grading nao importa quiz, quiz-content, quiz-surface ou o adapter;
- quiz e quiz-surface nao importam grading nem o adapter;
- quiz-content usa contratos genericos e projeta seu documento para a entrada
  publica do adapter;
- implementacoes C# de quiz ficam no assembly adapter;
- detalhes de quiz nao aparecem em rounds, stages ou resultados genericos.

## Arquivos autoritativos

| Area | TypeScript | C# |
| --- | --- | --- |
| Contratos genericos | [`grading/src/types.ts`](../../packages/features/grading/src/types.ts) | [`ExecutionContracts.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/ExecutionContracts.cs) |
| Scores e percentuais | [`values.ts`](../../packages/features/grading/src/values.ts) | [`AcademicValues.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/AcademicValues.cs) |
| Workflows | [`review-methods.ts`](../../packages/features/grading/src/review-methods.ts) | [`ReviewMethods.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/ReviewMethods.cs) |
| Validacao | [`execution-contracts.ts`](../../packages/features/grading/src/execution-contracts.ts) | [`GradingContractValidator.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/GradingContractValidator.cs) e [`GradingJson.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/GradingJson.cs) |
| Hash canonico | [`canonical-json.ts`](../../packages/features/grading/src/canonical-json.ts) | [`CanonicalJson.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Contracts/CanonicalJson.cs) |
| Registry | [`capabilities.ts`](../../packages/features/grading/src/capabilities.ts) | [`ReviewCapabilityRegistry.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Capabilities/ReviewCapabilityRegistry.cs) |
| Portas do runtime | tipos acima | [`ExecutionPorts.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Abstractions/ExecutionPorts.cs) |
| Adapter agregado de quiz | [`adapter.ts`](../../packages/features/grading-adapter-quiz/src/adapter.ts) | [`QuizAssessmentTypeAdapter.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizAssessmentTypeAdapter.cs) |
| Contrato do adapter quiz | [`contracts.ts`](../../packages/features/grading-adapter-quiz/src/contracts.ts) | [`QuizAdapterContracts.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizAdapterContracts.cs) |
| Detalhes internos do adapter | [`items.ts`](../../packages/features/grading-adapter-quiz/src/items.ts), [`responses.ts`](../../packages/features/grading-adapter-quiz/src/responses.ts), [`delivery.ts`](../../packages/features/grading-adapter-quiz/src/delivery.ts) e [`evaluation.ts`](../../packages/features/grading-adapter-quiz/src/evaluation.ts) | [`QuizItemProjector.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizItemProjector.cs), [`QuizAnswerDecoder.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizAnswerDecoder.cs), [`QuizDeliveryGenerator.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizDeliveryGenerator.cs) e [`QuizDeterministicReviewAlgorithm.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments.QuizAdapter/QuizDeterministicReviewAlgorithm.cs) |
| Documento de quiz | [`quiz-content/src/types.ts`](../../packages/features/quiz-content/src/types.ts) | persistido hoje em `ProgramContent.JsonBody` |

## Valores academicos

`ScoreValue` e um inteiro de unidades no intervalo `0..2147483647`.
`PercentValue` e um inteiro de unidades no intervalo `0..10000`. A escala fixa
e `100`: `100` representa `1` ponto ou `1%`, `50` representa `0.5` e `150`
representa `1.5`. JSON decimal e string numerica sao rejeitados.

Calculos usam inteiros escalados, intermediarios largos (`bigint`, `long` ou
`BigInteger`) e arredondamento half-up somente no ponto de quantizacao.
TypeScript usa `number` branded apenas depois de validar `Number.isSafeInteger`;
nenhum score, peso ou percentual academico persistido usa `decimal`, `numeric`,
`double`, `float` ou string.

`QuizEntry.points` e a unica fonte autoral mutavel de pontos de uma pergunta.
`ContentGradingDefinitionV2.items` nao repete `points`, ID ou capability.

## Workflow de review

`ReviewMethods` e uma bitmask numerica de configuracao:

| Flag | Valor |
| --- | ---: |
| `PeerReview` | 1 |
| `AIReview` | 2 |
| `AutomatedReview` | 4 |
| `InstructorReview` | 8 |
| `SelfReview` | 16 |

Valores validos: `0`, `1`, `2`, `4`, `8`, `9`, `10`, `12`, `16` e `24`.
Zero existe apenas em draft. Um workflow publicado possui um metodo primario
ou um metodo primario seguido de `InstructorReview`.

Stages e resultados usam o singular `AssessmentReviewMethod` no TypeScript e
`ReviewMethod` no C#. Ele serializa pelo nome, como `"AutomatedReview"`, e nao
como bitmask.

## Autoria de content

Grading desativado e a ausencia de `grading`; nao existe `enabled: false`
persistido.

```ts
interface ContentGradingDefinitionV2 {
  schemaVersion: 2;
  items: Record<string, { rubricRef?: string }>;
}

interface QuizContentDocumentV1 {
  schemaVersion: 1;
  order: Array<[itemId: string, "quiz"]>;
  blocks: Record<string, QuizEntry>;
  grading?: ContentGradingDefinitionV2;
}
```

A chave de `grading.items` e o ID autoral estavel de `order` e `blocks`.
[`grading-projection.ts`](../../packages/features/quiz-content/src/grading-projection.ts)
e a unica traducao do documento para `QuizGradingItemInputV1 { itemId, entry }`.

## Policy de assessment

Policies operacionais nao pertencem ao JSON do quiz:

```ts
interface AssessmentExecutionPolicyV1 {
  schemaVersion: 1;
  passingScore?: ScoreValue;
  maxAttempts?: number;
  attemptContribution?: {
    mode: "first-finalized" | "last-finalized" | "highest-finalized";
  };
  timeLimitMinutes?: number;
  availability: AssessmentAvailabilityPolicyV1;
  completion: AssessmentContentCompletionPolicyV1;
  resultRelease: AssessmentResultReleasePolicyV1;
  presentation: AssessmentPresentationPolicyV1;
  review: AssessmentReviewPolicyV1;
}
```

- `attemptContribution` e obrigatorio quando `maxAttempts > 1`;
- instantes usam UTC canonico como `2026-09-03T12:00:00.000Z`;
- `resultRelease` e uma uniao fechada `immediate | manual | scheduled`;
- `scheduled` exige `scheduledFor`; os outros modos o proibem;
- campos e versoes desconhecidos sao rejeitados;
- a fonte mutavel e o agregado Assessment; a policy e congelada na revisao,
  nao mantida como segundo draft JSON concorrente.

## Revisao imutavel

```text
AssessmentAuthoringSourceV1
  contentType + content + grading + policy

AssessmentExecutionManifestV1
  items[]   -> adapter agregado exato por tipo de assessment
  stages[]  -> handler e provider exatos
  policies[] -> implementacoes exatas

AssessmentExecutionSnapshotV1
  authoringSource + manifest + itemProjections[itemId]
```

`itemProjections` e criado no servidor. Para quiz, cada
`QuizItemProjectionV1` possui `itemId`, `itemType`, `maxScore`, referencia de
origem e `authoringEntry` privado congelado. A copia integra o snapshot, nao
volta ao draft e nao se torna segunda fonte mutavel.

Antes do hash, os validadores TypeScript e C# exigem o mesmo conjunto de IDs em
`grading.items`, `manifest.items` e `itemProjections`, conferem tipo e origem da
projecao e vinculam os stages exatamente ao workflow canonico da policy.

`AuthoringSourceHash` cobre somente `AssessmentAuthoringSourceV1`.
`ExecutionSnapshotHash` cobre o snapshot completo. Ambos usam SHA-256 sobre
JCS, versao `sha256-jcs-v1`. A API calcula esses hashes sobre o `JsonElement`
validado cujos bytes canonicos serao persistidos; nao recalcula identidade pela
serializacao posterior de um DTO C#. Durante a execucao, a entrega deve apontar
para o `ExecutionSnapshotHash` autoritativo da `GradingExecution`.

## Entrega concreta

```ts
interface AssessmentExecutionDeliveryV1<TLearnerPayload> {
  schemaVersion: 1;
  definitionRevisionId: string;
  executionSnapshotHash: string;
  itemOrder: string[];
  items: Record<string, {
    adapterKey: string;
    adapterVersion: string;
    learnerPayload: TLearnerPayload;
  }>;
}
```

`itemOrder` contem cada item uma vez. A entrega nao contem answer key,
tolerance privada, score calculado ou attachments `authorOnly`. `DeliveryHash`
cobre os bytes JCS completos. Resume e regrade da execucao reutilizam os mesmos
bytes. Antes da avaliacao, a API exige que IDs e versoes dos adapters da entrega
coincidam exatamente com o manifest fixado no snapshot.

## Resposta de quiz

O core recebe `AssessmentResponseEnvelopeV1<TPayload>` com `schemaVersion`,
`contentType`, `payloadSchema` e payload opaco. O adapter fecha esse contrato
como `quiz` + `quiz-answer/v1`, com respostas indexadas por item ID.

As 14 variantes estao em
[`answers.ts`](../../packages/features/quiz/src/answers/answers.ts) e nos schemas
estritos em [`answer-schemas.ts`](../../packages/features/quiz/src/answers/answer-schemas.ts).
Matching usa objeto `matches`, ordering usa `itemIds`, hotspot usa `{ x, y }` e
highlight usa spans. Delimitadores textuais, JSON dentro de strings, aliases de
`StructuredAnswerPayload` e campos extras sao rejeitados.

Fixture cross-language:
[`quiz-answer-envelope-v1.json`](../../packages/features/grading-adapter-quiz/fixtures/quiz-answer-envelope-v1.json).

## Resultado generico

`GradeItemResultV1` possui identidade do item, estado, score, `maxScore`,
feedback opcional, evidencias e identidade versionada do reviewer.
`GradeResultV1` agrega itens e pode ser `partial` sem falhar.

`AutomatedReview` avalia somente o deterministico. Item nao resolvido permanece
`pending` ou `unsupported` com score `null`; nao vira zero. Matching parcial usa
proporcao de pares corretos e Ordering parcial usa proporcao de posicoes
absolutas corretas.

## Contextos e capabilities

`AuthorTest` e `OfficialSubmission` sao independentes. Registry e manifest
resolvem `(kind, key, version, context)` exatamente. O adapter quiz registra
neste marco apenas `AuthorTest`, permitindo teste do instrutor sem afirmar
prontidao academica oficial.

Na API, [`AssessmentExecutionComponentResolver.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Capabilities/AssessmentExecutionComponentResolver.cs)
resolve o adapter agregado e os handlers genericos. O core nunca resolve nem
registra projector, gerador, decoder ou algoritmo especifico de quiz de forma
independente. O adapter resolvido deve coincidir com a identidade fixada no
manifest.

## Persistencia e invariantes

No corte aprovado nao havera migration incremental, backfill, dual-read ou
preservacao legacy. Bancos descartaveis nascerao do baseline limpo. JSON que
participa de hash sera persistido como texto/bytes canonicos. Scores e
percentuais relacionais usam colunas `integer` em unidades de escala `100`. O
payload generico antigo sera removido, nao mantido como alias.

Regras de manutencao:

1. Quiz autoral muda em quiz; workflow e resultado genericos mudam em grading.
2. Traducao entre os dominios muda somente no adapter.
3. Policy operacional nao entra no documento de quiz.
4. Answer key nao entra em entrega ou resposta learner-visible.
5. Stage singular nao usa a bitmask `ReviewMethods`.
6. Score ou percentual persistente usa somente inteiro de unidades no JSON e
   no banco; decimal humano existe apenas na borda de entrada/apresentacao.
7. Versao, campo ou shape desconhecido falha fechado.
8. Capability em `AuthorTest` nao implica `OfficialSubmission`.
