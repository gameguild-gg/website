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
| Views e comandos do runtime | [`types.ts`](../../packages/features/grading/src/types.ts) | [`GradingRuntimeContracts.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Runtime/GradingRuntimeContracts.cs) |
| Lifecycle de stages | contratos acima | [`GradingExecutionOrchestrator.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Runtime/GradingExecutionOrchestrator.cs) e handlers em [`Runtime`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Runtime) |
| Persistencia do runtime | nao aplicavel | [`GradingEntities.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Persistence/GradingEntities.cs) e [`GradingPersistenceModelConfiguration.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Persistence/GradingPersistenceModelConfiguration.cs) |
| Claims peer | views genericas em `types.ts` | [`IPeerReviewAssignmentService.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Services/IPeerReviewAssignmentService.cs) e [`PeerReviewAssignmentService.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Services/PeerReviewAssignmentService.cs) |
| Release e consumers | views genericas em `types.ts` | [`GradeResultReleaseScheduleService.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Runtime/GradeResultReleaseScheduleService.cs), [`GradeReleaseConsumers.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Runtime/GradeReleaseConsumers.cs) e [`AcademicOutbox.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Persistence/AcademicOutbox.cs) |
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

## Lifecycle de review e evidencias

Todos os metodos usam a mesma porta `IReviewStageHandler`:

```text
StartAsync
  -> Completed
  -> AwaitingEvidence
  -> AwaitingInstructorResolution
  -> RetryableFailure
  -> TerminalFailure

AcceptEvidenceAsync -> mesma uniao de transicoes
TryCompleteAsync    -> mesma uniao de transicoes
```

`GradingExecutionOrchestrator` e a unica autoridade para ordenar stages,
persistir transicoes, abrir/finalizar rounds e produzir o resultado. Cada
handler valida somente a evidencia do seu metodo. Nao existe branch de quiz no
orquestrador nem runtime separado para um review.

`ReviewEvidence` persiste payload JCS, hash, schema, produtor exclusivo
(`ProducedByActorId` ou `ProducedByService`) e stage. Um regrade de
`SelfReview` ou `PeerReview` cria uma evidencia do tipo
`review-evidence-reference`, com `SourceReviewEvidenceId` e o hash da evidencia
final anterior. Isso preserva proveniencia sem registrar outra acao humana.

## Self review

`SelfReviewEvidenceV1` possui `schemaVersion: 1`, itens com `itemId`,
`ScoreValue` e feedback opcional, alem de feedback global opcional. Score e
evidencia continuam separados da resposta do assessment.

`SelfReviewRuntimeStateV1` e a view compartilhada por test run e tentativa
oficial. Ela expoe somente o estado necessario para resume: stage, status,
instrucoes, exigencia de feedback, versao do draft, draft atual, ator da ultima
mutacao e evidencia final. Em sujeito coletivo existe um unico draft versionado
e uma unica evidencia final para o snapshot de participantes.

## Peer review

`PeerReviewClaim` e a autoridade de atribuicao. Ele referencia somente o
`ReviewStage`; assessment, round, execution e submission alvo sao derivados
desse relacionamento. A identidade e exclusiva: `ReviewerUserId` em
`OfficialSubmission` ou `ReviewerTestRunSubjectId` em `AuthorTest`.

Estados persistidos: `assigned | submitted | expired | cancelled`. Lease,
deadline do stage, cota do reviewer, minimo de evidencias e agregacao sao
independentes. A projecao learner-facing omite a identidade do reviewee e do
reviewer por shape; a projecao de equipe preserva a identidade para auditoria.
`PeerReviewClaimView` transporta internamente os JSONs canonicos de delivery e
response da execucao. Depois de validar ownership e capability, o
`AnonymousReviewSubmissionDto` expoe esses dois envelopes sem IDs de usuario,
score calculado ou feedback docente. O workspace de quiz interpreta os
envelopes por `@game-guild/grading-adapter-quiz` e renderiza somente a entrega
learner-safe e a resposta submetida; o core de grading continua sem importar
quiz.

Submits, retomadas e operacoes docentes peer usam o mesmo advisory lock por
`GradingExecution`. Extensao, reatribuicao e resolucao de insuficiencia usam
`GradingCommandReceipt` com ator, idempotency key e request hash; replay igual
reutiliza o outcome e replay divergente falha fechado.
Insuficiencia nao produz zero: o stage entra em
`AwaitingInstructorResolution` ate uma operacao explicita de extensao,
reatribuicao ou resolucao docente.

## AI review

`AIReviewProviderRequestV1` congela request ID, execution, round, stage,
contexto, hash do snapshot, snapshot, delivery, response, resultado anterior e
identidade do provider/policy. A outbox e gravada antes da chamada externa.

`AIReviewProviderResponseV1` repete a correlacao autoritativa e acrescenta
modelo e versao. `RequestId` e a chave de idempotencia. O modulo de Assessments
declara `IAIReviewProvider`; provider concreto e credenciais sao compostos fora
dele. Sem provider compativel, publish falha fechado e a UI nao oferece o
metodo.

## Release e efeitos

`GradeResultReleaseSchedule` possui um registro por `GradeRound`, instante UTC,
estado `scheduled | cancelled | released`, versao de concorrencia e produtor
exclusivo actor/service. Ele e operacional e mutavel; `GradeResultRelease`
continua sendo o fato final append-only. O worker de vencimento chama o mesmo
caso de uso de release e nao escreve o fato final diretamente.

Eventos `GradeResultFinalized` e `GradeResultReleased` saem pela outbox
academica. Cada consumer confirma independentemente por `(EventId,
ConsumerKey)`. Gradebook/progresso, notificacao e passback nao recalculam o
resultado e uma falha posterior nao desfaz consumers ja confirmados.

## Contextos e capabilities

`AuthorTest` e `OfficialSubmission` sao independentes. Registry e manifest
resolvem `(kind, key, version, context)` exatamente. O adapter quiz e os
handlers implementados registram ambos os contextos. `AuthorTest` usa sujeitos
sinteticos e nunca cria enrollment, submission academica, gradebook, progresso,
notificacao ou passback. `OfficialSubmission` usa a tentativa e o sujeito
congelados e pode produzir efeitos somente depois da finalizacao/release
definidos pela policy.

Na API, [`AssessmentExecutionComponentResolver.cs`](../../apps/api/Source/Modules/GameGuild.Learning.Assessments/Grading/Capabilities/AssessmentExecutionComponentResolver.cs)
resolve o adapter agregado e os handlers genericos. O core nunca resolve nem
registra projector, gerador, decoder ou algoritmo especifico de quiz de forma
independente. O adapter resolvido deve coincidir com a identidade fixada no
manifest. `AIReview` so registra uma capability publicavel quando o registry
possui provider compativel para o contexto e a policy congelados.

## Persistencia e invariantes

O schema atual e produzido pela cadeia historica de migrations. A Parte 4 nao
edita nem remove migrations anteriores e acrescenta dois passos incrementais:

1. `AddReviewEvidenceReuseReference`: adiciona a autorreferencia opcional e seu
   indice em `ReviewEvidence`;
2. `AddPeerReviewClaimsAndReleaseSchedules`: adiciona
   `ReviewStage.EvidenceDeadlineAt`, `PeerReviewClaims` e
   `GradeResultReleaseSchedules`, com FKs, checks e indices de lease/vencimento.

`AssessmentPeerReview` foi preservado por seguranca de upgrade, mas nao e uma
segunda autoridade do runtime novo. JSON que participa de hash e persistido
como texto/bytes canonicos. Scores e percentuais relacionais usam colunas
`integer` em unidades de escala `100`; nenhum calculo academico usa decimal.

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
9. Evidencia humana reutilizada referencia a origem; nao e copiada como nova
   acao do usuario.
10. Claim peer pertence ao stage, e schedule de release pertence ao round.
11. Consumer de efeito observa evento canonico; nunca executa review nem
    recalcula nota.
