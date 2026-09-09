# 01. Contratos e persistência

## Objetivo

Definir contratos versionados e a menor evolução de persistência capaz de
suportar publicação explícita, definição imutável por execução, reviews
interativos, score preciso, regrade e efeitos externos confiáveis.

A implementação começa por quatro ADRs obrigatórios. Entidades EF, model
configuration e baseline de schema só podem ser alterados depois da aprovação
desses documentos:

1. lifecycle e revisão publicada de `Assessment`;
2. escala, precisão e ownership do limiar de aprovação;
3. histórico acadêmico, concorrência e outbox;
4. sujeito da tentativa e snapshot de participantes para assessments de grupo.

Este documento descreve o estado-alvo completo da persistência, mas não ordena
suas alterações como uma única fase. A sequência canônica em
[`08-implementation-sequence.md`](./08-implementation-sequence.md) distribui os
itens por fatias verticais e exige um `SCHEMA-GATE` antes de cada impacto
relacional. Cada fatia aprovada edita o mesmo baseline global inicial e recria os
bancos descartáveis; nunca adiciona uma migration incremental.

## Persistência atual

`Assessment` já possui:

- `GradingMethods` como inteiro bitmask;
- grupo, score máximo, tentativas, tempo e apresentação;
- `PassingScore` inteiro;
- `DefinitionPayload` em `jsonb` e versão de schema.

`AssessmentSubmission` já possui:

- identificação da tentativa e aluno;
- payload estruturado em `jsonb`;
- `Score`, `Passed`, `Feedback`, `GradedBy` e `GradedAt`;
- apenas os estados `InProgress`, `Submitted`, `Graded`, `Returned` e `Late`.

O modelo atual não possui publicação própria de assessment, revisão imutável
referenciada pela tentativa, resultado por item, rodada de regrade nem estágio
operacional consultável. Os scores de assessment e peer review são inteiros.

## Review methods

Renomear o conceito para `ReviewMethods` em entidade, enum, DTOs e schema final.
Como não há dados lançados, o schema de criação já nasce com o nome e a
constraint definitivos; não existe etapa de rename em banco.

Constraint-alvo:

```sql
"ReviewMethods" IN (0, 1, 2, 4, 8, 9, 10, 12, 16, 24)
```

O domínio rejeita `0` ao publicar. O banco pode mantê-lo para drafts. Os nomes
textuais antigos são substituídos atomicamente; os valores `1`, `2`, `4` e `8`
permanecem estáveis e `SelfReview = 16` é acrescentado.

Política pré-lançamento:

- não criar nem executar migration incremental que transforme um schema
  anterior no schema novo;
- não criar migration de dados, backfill ou compatibilidade legacy;
- manter os valores `1`, `2`, `4` e `8` porque continuam sendo os valores
  canônicos, não por compatibilidade com dados antigos;
- atualizar entidade, model configuration, DTOs e o schema inicial limpo em
  conjunto;
- recriar bancos locais, de desenvolvimento e de teste afetados;
- validar criação do banco do zero, constraints e round-trip.

A API atualmente inicializa o banco por `Database.MigrateAsync()`. Portanto,
"sem migrations" significa que não haverá uma sequência incremental nem
preservação de estado anterior: a cadeia histórica de desenvolvimento deve ser
substituída por um único baseline de criação EF limpo, ou o inicializador deve
ser trocado por outro mecanismo de criação antes desta fase. Acrescentar uma
migration ao fim da cadeia atual não atende ao plano. Essa escolha operacional
deve ser registrada no ADR de persistência antes do primeiro `SCHEMA-GATE`.

O snapshot EF não descreve todo o banco atual. Antes de substituir a cadeia, o
gate inventaria extensões, schemas, roles, grants, policies, funções,
procedures, triggers, views, índices especiais e dados estruturais instalados
por `migrationBuilder.Sql`. Cada artefato ativo recebe owner, dependências,
ordem de instalação, decisão explícita e teste funcional no banco vazio. O
baseline final reinstala diretamente o estado aprovado; deixar um artefato de
fora apenas porque ele não aparece no `IModel` é drift destrutivo, não limpeza
de legado.

## Ownership dos contratos

O contrato atual `ContentGradingDefinition` mistura configuração autoral da
questão com políticas operacionais do assessment. O alvo elimina essa
duplicidade no mesmo corte, sem aliases ou sincronização permanente:

```ts
interface ContentGradingDefinitionV2 {
  schemaVersion: 2;
  items: Record<string, GradingItemAuthoringV2>;
}

interface GradingItemAuthoringV2 {
  rubricRef?: string;
}

interface AssessmentExecutionPolicyV1 {
  schemaVersion: 1;
  passingScore?: ScoreValue;
  maxAttempts?: number;
  attemptContribution?: AttemptContributionPolicyV1;
  timeLimitMinutes?: number;
  availability: AssessmentAvailabilityPolicyV1;
  completion: AssessmentContentCompletionPolicyV1;
  resultRelease: AssessmentResultReleasePolicyV1;
  presentation: AssessmentPresentationPolicyV1;
  review: AssessmentReviewPolicyV1;
}

type AssessmentContentCompletionMode =
  | "on-submit"
  | "on-finalize"
  | "on-release"
  | "on-release-and-pass";

interface AssessmentContentCompletionPolicyV1 {
  mode: AssessmentContentCompletionMode;
}

type AssessmentResultReleasePolicyV1 =
  | { mode: "immediate" }
  | { mode: "manual" }
  | { mode: "scheduled"; scheduledFor: string };
```

`SEQ-00` aprova quais modos entram no primeiro corte e qual é o default. O
contrato permanece fechado e versionado mesmo quando apenas um modo for
inicialmente habilitado; não inferir conclusão de peso, workflow ou chamadas de
`ContentInteraction`. Não existe `on-pass` learner-visible antes do release: o
modo correspondente é `on-release-and-pass`, porque conclusão antecipada
revelaria o valor de `Passed` de uma rodada retida. O modo `scheduled` também é
reservado no contrato V1 desde o início, embora sua execução só seja habilitada
em `SEQ-15`; `scheduledFor` é um instante UTC canônico.

Fronteiras finais:

- quiz content possui enunciado, opções, answer key, pontos e feedback autoral
  por item;
- `@game-guild/grading` possui somente contratos, value objects, registry e
  algoritmos independentes do tipo de assessment, sem importar quiz;
- `@game-guild/grading-adapter-quiz` traduz questões e respostas de quiz para os
  contratos públicos de grading e concentra redaction, answer key, capacidades
  e avaliação determinística específicas de quiz;
- a chave de `ContentGradingDefinitionV2.items` é o ID autoral estável do item;
  seu valor contém somente configuração autoral adicional de grading. Ele não
  repete ID, pontos, tipo de questão nem capability executável;
- em quiz, `QuizEntry.points` é a única fonte mutável dos pontos e usa unidades
  inteiras escaladas compatíveis com `ScoreValue`. O adapter C#
  autoritativo projeta no prepare um snapshot imutável por item com `itemId`,
  `maxScore`, tipo de origem e referências privadas necessárias à correção; o
  package TypeScript comprova conformidade por fixtures, mas não fornece os
  bytes persistidos ao servidor;
- assessment possui tentativas, tempo, disponibilidade, atraso, apresentação
  da aplicação, conclusão do content avaliado, liberação de feedback,
  passing score e workflow de review;
- assessment possui também a policy que seleciona a contribuição efetiva para
  gradebook e integrações de score; a policy de conclusão decide separadamente
  em qual transição o content progride e como múltiplas tentativas afetam esse
  estado;
- `MaxScore` é derivado dos itens no servidor e projetado no assessment;
- `AssessmentDefinitionRevision` compõe os contratos autoral e operacional
  usados na execução;
- grupo e peso pertencem à projeção do gradebook e ficam fora dos dois JSONs.

Não manter `attempts`, `feedback`, `presentation` ou `passingScore` nos dois
lados. A gravação atômica garante consistência entre os agregados, mas não é
uma justificativa para duplicar ownership.

A direção de dependência é obrigatória:

```text
@game-guild/grading <- @game-guild/grading-adapter-quiz -> @game-guild/quiz
```

O adapter não redefine contratos genéricos e o core não reexporta símbolos de
quiz. Outros tipos avaliáveis recebem packages de integração equivalentes.

No servidor, o runtime de assessments/grading possui uma única porta genérica
`IAssessmentTypeAdapter`, resolvida por `contentType`, `adapterKey`, versão e
contexto. Ela agrega projeção autoral, geração de entrega,
decode/normalização e avaliação determinística. As implementações C# de quiz
vivem em um módulo/assembly adapter e são registradas uma única vez no
composition root. Core, autoria genérica, rounds, resultados e handlers
genéricos não referenciam DTOs, parsers, entidades ou namespaces de quiz.
Testes de arquitetura .NET tornam essa direção executável.

A entrada do adapter de quiz é
`QuizGradingItemInputV1 { itemId: string; entry: QuizEntry }`. `quiz-content`
monta essa lista de sua ordem e de seus blocos. Assim, o adapter conhece o ID
estável e a questão sem importar `block-list`, aceitar `BlockStorageLike` ou
redefinir o documento autoral.

`gradingKind` não pertence a `ContentGradingDefinitionV2`. Intenção autoral de
review pertence a `ReviewMethods` e às policies de review; suporte concreto do
tipo pertence ao adapter agregado e suporte dos stages aos seus handlers e
providers, todos fixados no `AssessmentExecutionManifestV1`. Alterar catálogo,
implementação ou capability não modifica a fonte autoral.

## Envelope de resposta

O runtime de grading persiste somente o envelope genérico:

```ts
interface AssessmentResponseEnvelopeV1<TPayload = unknown> {
  schemaVersion: 1;
  contentType: string;
  payloadSchema: string;
  payload: TPayload;
}
```

O core não interpreta `payload`. O manifest fixa o adapter por
`contentType + adapterKey + adapterVersion`, e o adapter específico valida o payload antes do
submit. Para quiz, `@game-guild/grading-adapter-quiz` possui
`QuizAnswerEnvelopeV1`, união discriminada fechada dos 14 tipos detalhada no
documento [`05`](./05-learner-attempts-and-results.md). Nenhum contrato do core
expõe `StructuredAnswer`, `contentBlockId` ou outra forma específica de quiz.

O envelope validado é canonicalizado e persistido uma única vez na
`GradingExecution`. Retry e regrade reutilizam os mesmos bytes. O baseline
remove `AssessmentSubmission.StructuredAnswerPayload`; não há alias, dual-read
ou cópia concorrente da resposta na submission.

`AssessmentExecutionPolicyV1` é um contrato materializado pela API e congelado
na revisão, não um segundo draft JSON. No agregado mutável:

- campos consultáveis como tentativas, tempo, datas, passing score e
  apresentação têm as colunas de `Assessment` como única fonte;
- o atual `Assessment.DefinitionPayload`, sua versão e o setter genérico são
  removidos. Se uma policy complexa sem coluna exigir persistência mutável, ela
  recebe contrato tipado, nome específico e aprovação no `SCHEMA-GATE`; não
  existe payload genérico substituto;
- nenhuma propriedade pode existir simultaneamente numa coluna e nesse payload;
- a API monta `AssessmentExecutionPolicyV1` dessas fontes ao validar, testar e
  publicar.

`attemptContribution` é obrigatório na revisão quando `maxAttempts > 1`. O ADR
de produto define a primeira política suportada antes do E2E oficial e se ela é
global ou configurável por assessment. O corte inicial deve selecionar uma
única tentativa finalizada, por exemplo primeira, última ou maior score. Uma
eventual média é outro modo, que deriva uma contribuição de várias tentativas e
fica fora desse corte inicial. Se a policy for configurável, sua fonte
persistente entra no `SCHEMA-GATE` da tentativa oficial; se for global, nenhuma
coluna é criada apenas para repetir uma constante. Enquanto nenhuma policy
estiver implementada de ponta a ponta, a API deve rejeitar `maxAttempts > 1`;
não é permitido projetar cada tentativa finalizada como uma contribuição
independente nem deixar cada consumer calcular sua própria contribuição.

## Capabilities por contexto

Readiness de test run e readiness acadêmica são capacidades distintas:

```ts
type ReviewExecutionContext = "author-test" | "official-submission";

interface ReviewCapabilityDescriptorV1 {
  method: AssessmentReviewMethod;
  contexts: ReviewExecutionContext[];
  handlerVersion: string;
  providerKey?: string;
}
```

Capability informa o que o deploy consegue executar agora; ela não escolhe a
implementação de uma revisão já preparada. A revisão fixa um manifest
executável:

```ts
interface AssessmentExecutionManifestV1 {
  schemaVersion: 1;
  items: Array<{
    itemId: string;
    itemType: string;
    adapterKey: string;
    adapterVersion: string;
  }>;
  stages: Array<{
    method: AssessmentReviewMethod;
    handlerKey: string;
    handlerVersion: string;
    providerKey?: string;
    providerPolicyVersion?: string;
  }>;
}
```

O adapter remove dados privados da definição, materializa o challenge concreto,
valida e normaliza a resposta e executa a avaliação determinística suportada.
Esses papéis podem ser componentes internos do package específico, mas formam
uma unidade versionada indivisível no manifest. Isso impede combinar versões
incompatíveis e reduz a integração de um novo tipo a um adapter e um registro.

Prepare resolve versões exatas e as incorpora ao manifest. Publish e start
revalidam os mesmos bytes, chaves e versões, sem reconstruir o manifest, mudar
`ExecutionSnapshotHash` ou usar fallback para a mais recente. Uma versão
referenciada por revisão ativa ou execução retida não pode ser removida do
deploy. Cada artefato anuncia um catálogo imutável das versões que suporta. Um
preflight de startup/deploy consulta revisões ativas, revisões retidas
elegíveis a regrade e execuções não terminais do ambiente e rejeita o artefato
antes de receber tráfego se alguma versão exata não puder ser resolvida.
Artefatos e handlers permanecem retidos enquanto a política de revisão,
regrade ou rollback depender deles. Regrade sempre reutiliza a revisão, o
manifest, a entrega e as respostas originais da `GradingExecution`; nunca
atualiza implementação implicitamente. Corrigir a mesma resposta contra uma
definição diferente exige nova submission e nova execução explicitamente
relacionadas ao caso anterior, e não uma rodada de regrade.

`PrepareRevision` exige adapter completo e capability `author-test` para o
workflow exercitado. `PublishRevision` exige `official-submission` para todos
os seus estágios. Um handler controlado pode comprovar o contrato de publish em
testes automatizados, mas nunca deve ser registrado na configuração de
produção. Saúde transitória de um provider continua sendo condição de runtime,
não motivo para reescrever a revisão.

## Lifecycle de publicação

`ProgramContent.Visibility` controla exposição do content, mas não prova que a
configuração avaliativa está validada. Assessments também podem existir sem
content. Portanto, publicação deve ser explícita no agregado de assessment.

Decisão-alvo:

- `Assessment` e eventuais fontes tipadas específicas de policy permanecem como
  definição operacional mutável e sem campos duplicados; o payload genérico
  atual deixa de existir;
- `Assessment.PublishedDefinitionRevisionId` aponta para a revisão executável
  atual ou fica `null` quando não há publicação ativa;
- `AssessmentDefinitionRevision` guarda um snapshot imutável criado por
  `PrepareRevision` ou, no publish direto, pela própria transação de publish;
- `AssessmentTestRun` pode referenciar uma revisão candidata ou a revisão ativa;
- `AssessmentSubmission` oficial só pode referenciar a revisão ativa no start;
- `PublishRevision(revisionId)` verifica se `AuthoringSourceHash` ainda
  coincide com o draft e então ativa a candidata sem recriá-la;
- unpublish remove o ponteiro ativo, mas não apaga revisões referenciadas por
  execuções anteriores.

Estado autoral derivado, sem coluna textual redundante:

```text
Draft           PublishedDefinitionRevisionId == null
Published       authoringHash(draft atual) == AuthoringSourceHash da ativa
ChangesPending  authoringHash(draft atual) != AuthoringSourceHash da ativa
```

Estrutura mínima a justificar no ADR:

```text
AssessmentDefinitionRevision
  Id
  AssessmentId
  RevisionNumber
  SchemaVersion
  AuthoringSourceHash
  AuthoringSourceHashVersion
  ExecutionSnapshotHash
  ExecutionSnapshotHashVersion
  ExecutionManifest
  DefinitionPayload jsonb
  CreatedBy
  CreatedAt
```

O payload imutável contém content autorizado para correção, projeção
learner-safe, answer keys, rubricas, pontos, limiar de aprovação,
`ReviewMethods`, policies de review e políticas de execução. Grupo e peso não
entram no snapshot de execução: continuam sendo configuração atual da projeção
do gradebook e podem ser alterados sem reinterpretar respostas.

Não reutilizar `Assessment.DefinitionPayload` como histórico sobrescrito, nem
copiar answer key completo para cada submission. A entidade de revisão é
justificada por identidade, retenção, referência e deduplicação entre
tentativas. Antes de alterar o baseline, o ADR deve confirmar que não surgiu outro
agregado genérico com essas garantias.

Revisões candidatas não ativadas podem ter retenção própria. Editar o draft não
altera a candidata já testada, mas impede ativá-la enquanto o hash divergir. O
professor precisa preparar e testar outra revisão ou restaurar o draft. Assim, o
publish ativa exatamente os bytes exercitados no test run.

### Contratos de identidade autoral e execução

Não calcular o hash serializando entidades EF ou objetos arbitrários. Criar um
DTO explícito `AssessmentAuthoringSourceV1` contendo somente dados controlados
pelo autor:

- `QuizContentDocument`, sua ordem, answer keys e pontos, sem cópias derivadas;
- `ContentGradingDefinitionV2`, limitado à configuração adicional por item;
- `AssessmentExecutionPolicyV1`, `ReviewMethods` e policies dos reviews;
- versões de schema e identificadores estáveis dos contratos.

Grupo, peso, título administrativo, timestamps e IDs da própria revisão não
entram no hash. O algoritmo inicial é `SHA-256` sobre UTF-8 de JSON canônico
RFC 8785 (JCS). Ordem de arrays continua semanticamente relevante; propriedades
de objetos são canonicalizadas. TypeScript e C# compartilham fixtures de bytes
e digest.

O digest desse DTO é `AuthoringSourceHash`, versionado por
`AuthoringSourceHashVersion = "sha256-jcs-v1"`. Ele é a única identidade usada
para comparar draft, candidata e revisão ativa. Mudança de catálogo, health ou
deploy não altera o estado autoral.

Criar separadamente `AssessmentExecutionSnapshotV1`, composto pelo
`AssessmentAuthoringSourceV1` canônico e pelo
`AssessmentExecutionManifestV1` resolvido no prepare. Seu digest é
`ExecutionSnapshotHash`, com
`ExecutionSnapshotHashVersion = "sha256-jcs-v1"`. Ele prova exatamente quais
bytes autorais e versões executáveis foram testados ou executados, mas não
participa de `Published` ou `ChangesPending`.

Fixtures cruzadas devem provar três casos: mudança autoral altera os dois
hashes; mudança apenas do manifest altera somente `ExecutionSnapshotHash`; e
health ou catálogo do deploy não altera nenhum hash já persistido.

O snapshot da revisão não contém a variação sorteada para uma tentativa. Cada
`GradingExecution` materializa uma única entrega concreta:

```ts
interface AssessmentExecutionDeliveryV1 {
  schemaVersion: 1;
  definitionRevisionId: string;
  executionSnapshotHash: string;
  itemOrder: string[];
  items: Record<string, {
    adapterKey: string;
    adapterVersion: string;
    learnerPayload: unknown;
  }>;
}
```

`itemOrder` é a única ordem canônica dos itens; a ordem de propriedades de
`items` não possui significado. Cada `learnerPayload` guarda exatamente os
prompts/challenges públicos, valores gerados e ordenações internas que o sujeito
recebeu. Não guarda answer key, rubrica, regra privada ou score. Na versão
inicial, o avaliador deve conseguir derivar todo resultado privado usando apenas
a revisão imutável e essa entrega pública concreta. Um gerador que produza
answer key privada aleatória não derivável não possui capability até que um novo
contrato de persistência seja aprovado.

O servidor aplica JCS ao envelope completo, persiste o JSON canônico como texto
e calcula `DeliveryHash` sobre exatamente seus bytes UTF-8. Não persistir apenas
uma representação `jsonb` e prometer identidade byte a byte por reserialização.
Uma seed pode ser registrada como evidência, mas nunca substitui a saída
materializada. Start concorrente, resume e retry reutilizam a mesma entrega;
restart cria outra `GradingExecution` e pode materializar outro challenge.

## Escala e aprovação

Adotar inteiros de ponto fixo com escala `100`. Nenhum valor acadêmico é
persistido como string decimal, `decimal`, `numeric`, `double` ou `float`:

```text
C# domínio         value objects sobre int32; intermediários long/BigInteger
TypeScript / JSON  number branded, inteiro validado
ScoreValue         integer, 0..2147483647; 100 unidades = 1 ponto
PercentValue       integer, 0..10000; 100 unidades = 1%
Exemplos           0, 50 (= 0.5 ponto), 150 (= 1.5 ponto), 10000 (= 100%)
Arredondamento     uma vez ao materializar o inteiro, half-up
```

No contrato público de quiz, `QuizEntry.points` é um inteiro de unidades no
mesmo formato de `ScoreValue`. O package de quiz valida inteiro e intervalo sem
importar implementação de grading; o adapter converte esse valor para o value
object do core. Inputs visuais podem aceitar texto decimal com no máximo duas
casas e convertê-lo deterministicamente, sem `parseFloat`, leitura dupla ou
formato antigo.

Aplicar a escala a `Assessment.MaxScore`, `Assessment.PassingScore`,
`AssessmentSubmission.Score`, `AssessmentPeerReview.Score`, resultados por item,
DTOs, filas, LTI/passback e contratos do package. Aplicar `PercentValue` a
`AssessmentGroup.WeightPercent`, `Program.PassingScore` e demais percentuais
acadêmicos persistidos. Agregações usam intermediários ampliados, validam
overflow e persistem novamente as unidades `int32`.

O banco valida intervalo e nullability. Igualdade, filtros, índices e `ORDER BY`
mantêm semântica numérica nativa. Soma, média, mediana, passing score e
ponderação usam somente aritmética inteira, com divisão e arredondamento
`half-up` explícitos; consultas SQL podem usar `bigint` como intermediário, mas
nunca `numeric`, `real` ou `double precision`. Testes de schema cobrem `0`, `1`,
`50`, `100`, os máximos e o limite percentual `10000`.

Gradebook e dashboards não recalculam o curso varrendo e agregando texto a cada
consulta. Consumers idempotentes mantêm projeções precomputadas por aluno,
curso e período, também em unidades inteiras. A API recalcula a projeção ao
receber alteração de resultado, tentativa escolhida, grupo ou peso. Consultas
podem filtrar, paginar, somar e ordenar com operadores inteiros.

Converter as colunas relacionais atuais de `decimal` para `integer` e redefinir
as colunas `integer` atuais como unidades escaladas é a alteração de schema
aprovada para o baseline limpo. Não
reaproveitar `RubricScoresPayload` ou `StructuredAnswerPayload` para evitar essa
decisão, pois isso misturaria responsabilidades.

O `SCHEMA-GATE` do núcleo em `SEQ-02` deve inventariar e aprovar no mesmo corte
todos os scores, pesos e percentuais acadêmicos já persistidos como tipos
numéricos. `SEQ-03` converte inclusive `AssessmentSubmission.Score`,
`AssessmentPeerReview.Score`, `Program.PassingScore`,
`AssessmentGroup.WeightPercent` e seus produtores e consumidores antes do
primeiro test run. Não é permitido deixar a conversão do percentual global ou
de um score existente para uma etapa posterior ao primeiro E2E oficial.

Semântica canônica:

- `Assessment.MaxScore`: soma ou escala total publicada do assessment;
- `Assessment.PassingScore`: limiar absoluto entre `0` e `MaxScore`, usado para
  definir `AssessmentSubmission.Passed`;
- `Program.PassingScore`: `PercentValue` global do curso, aplicado somente depois
  da consolidação ponderada do gradebook;
- `AssessmentGroup.WeightPercent`: `PercentValue` que decide a contribuição do
  grupo no gradebook;
- a revisão publicada captura `MaxScore` e `PassingScore` usados pela tentativa.

A UI do assessment deve exibir `Passing score` em pontos e impedir valor maior
que `MaxScore`. Não converter silenciosamente o percentual do curso para a
submissão. Se futuramente for desejado um limiar percentual por assessment,
isso será outro contrato explícito, não uma segunda interpretação da mesma
coluna.

`Program.PassingScore` e `AssessmentGroup.WeightPercent` estão mapeados como
`decimal` no código atual e fazem parte da conversão coordenada. Não pode restar
um caminho que converta esses valores para número de ponto flutuante no JSON ou
no cliente.

## Policies por review

Usar schema versionado dentro da definição operacional:

```ts
interface AssessmentReviewPolicyV1 {
  schemaVersion: 1;
  peer?: {
    reviewsPerReviewer: number;
    reviewsRequiredPerSubmission: number;
    minimumReviewsToFinalize: number;
    aggregation: "mean" | "median";
    claimLeaseMinutes: number;
    evidenceWindowMinutes: number;
    onInsufficientEvidence: "await-instructor-resolution";
  };
  ai?: {
    providerKey: string;
    policyVersion: string;
  };
  self?: {
    instructions?: string;
    requireFeedback: boolean;
  };
  instructor?: {
    requireOverrideReason: boolean;
  };
}
```

`PeerReviewsRequiredCount` hoje representa `reviewsPerReviewer`. Não reutilizar
esse campo como limiar recebido por submissão. O ADR decide seu rename ou sua
absorção no payload; duas fontes de verdade não permanecem.

## Estado corrente e histórico

Separar lifecycle da submission de lifecycle da avaliação. A submission
continua representando start/submit/late/return. `AssessmentTestRun` agrega um
ou mais `AssessmentTestRunSubject`, um para cada alvo sintético avaliado no
teste. `GradingExecution` é a raiz persistente compartilhada do pipeline e
possui exatamente um owner relacional, sem `ownerId` polimórfico:

```text
AssessmentTestRunSubject
  AssessmentTestRunId
  PersonaKey
  UNIQUE (AssessmentTestRunId, PersonaKey)

GradingExecution
  DefinitionRevisionId
  AssessmentTestRunSubjectId nullable
  AssessmentSubmissionId nullable
  ExecutionDeliverySchemaVersion
  ExecutionDeliveryCanonicalJson text
  DeliveryHash
  DeliveryHashVersion
  EvaluationState
  CurrentReviewMethod nullable
  ActiveGradeRoundId nullable
  EvaluationPayload jsonb nullable
  CHECK exatamente um owner preenchido
  UNIQUE parcial por AssessmentTestRunSubjectId
  UNIQUE parcial por AssessmentSubmissionId

AssessmentResultRelease
  GradeRoundId unique
  State
  ScheduledFor nullable
  ReleasedAt nullable
  Version
```

O contrato mínimo de fan-out da outbox, seja reutilizado ou implementado pelo
módulo, equivale a:

```text
AcademicOutboxMessage
  EventId unique
  EventType
  PayloadVersion
  Payload
  RequiredConsumerKeys
  OccurredAt

AcademicOutboxDelivery
  EventId
  ConsumerKey
  State
  AttemptCount
  NextAttemptAt nullable
  ConfirmedAt nullable
  LastError nullable
  UNIQUE (EventId, ConsumerKey)
```

`RequiredConsumerKeys` é congelado quando o evento é criado. O `SCHEMA-GATE`
pode mapear esse contrato para infraestrutura existente com outros nomes, mas
não pode reduzir a confirmação a um único `DeliveredAt` global.

`AssessmentResultRelease` somente existe para submission oficial. Cada sujeito
sintético do test run possui sua `GradingExecution` e resultado diagnóstico,
mas nunca recebe linha, estado ou evento de release acadêmico. A submission é
derivada por `GradeRound -> GradingExecution -> AssessmentSubmission`; o comando
recebe `submissionId` para autorização e escopo, porém valida essa cadeia. Não
duplicar o ID da submission na linha de release, pois isso permitiria uma
associação divergente sem uma constraint adicional. Como o owner oficial é uma
condição transitiva, o `SCHEMA-GATE` deve demonstrar uma invariante verificável
no banco: por desenho relacional quando possível ou por constraint de banco
aprovada e testada quando FKs simples não forem suficientes. Validação apenas
na aplicação não satisfaz esse gate.

A entrega pertence à execução, é imutável depois de materializada e é usada
por test run e submission oficial. Não criar um segundo snapshot em
`AssessmentSubmission` nem aceitar do browser variáveis, seed ou ordem que
determinem o challenge oficial.

Estados de avaliação:

```text
NotStarted
Pending
Running
AwaitingEvidence
Failed
Finalized
```

Finalização acadêmica e liberação ao aluno são estados ortogonais:

```text
EvaluationState.Finalized    resultado oficial pronto para gradebook e auditoria
ResultReleaseState.Withheld  resultado ainda invisível ao aluno
ResultReleaseState.Scheduled aguardando data ou fechamento configurado
ResultReleaseState.Released  resultado visível ao aluno
```

Uma rodada pode estar `Finalized` e continuar `Withheld` ou `Scheduled`.
Gradebook consome finalização; a projeção do aluno e notificações de resultado
consomem liberação. Release pertence à rodada, não apenas à submission.

Regrade cria uma nova rodada na mesma execução e aplica novamente a política de
liberação. Enquanto essa rodada estiver pendente, `Withheld` ou `Scheduled`, o
aluno continua vendo a última rodada previamente liberada, acompanhada de um
estado learner-safe de reavaliação em andamento. A projeção interna do gradebook
troca atomicamente para a nova rodada quando ela finaliza; a projeção learner só
troca quando a nova rodada é liberada. Retirar uma nota já liberada exige outro
comando explícito e auditado, fora do primeiro corte; iniciar regrade nunca a
oculta implicitamente.

Liberação manual é um caso de uso explícito, não uma edição direta do estado:

```ts
interface ReleaseGradeResultV1 {
  submissionId: string;
  expectedGradeRoundId: string;
  expectedReleaseVersion: string;
  idempotencyKey: string;
  reason?: string;
}
```

`ReleaseGradeResult` autoriza o instrutor no assessment, rejeita rodada
substituída e usa concorrência otimista. Para policy `immediate`, a transação que
finaliza a rodada persiste também uma solicitação durável e idempotente de
`ReleaseGradeResult`; ela não marca a rodada como liberada. Um worker/dispatcher
processa essa solicitação com identidade de serviço autorizada e chama a mesma
fronteira usada pela liberação manual. Assim, uma queda depois do commit e antes
do dispatch não deixa o resultado preso. Replay idêntico devolve o resultado
anterior; replay da mesma chave com payload diferente é erro. A transição válida
grava auditoria e exatamente um `GradeResultReleased` na outbox. Persistência
adicional para a solicitação ou deduplicação só pode ser proposta no
`SCHEMA-GATE` da tentativa oficial se a infraestrutura aprovada não atender ao
contrato.

`SEQ-15` habilita `ScheduleGradeResultRelease` e
`CancelScheduledGradeResultRelease` sobre a mesma autorização, concorrência,
idempotência e auditoria. O worker de vencimento usa `TimeProvider` e chama
`ReleaseGradeResult`; ele não edita o estado diretamente. Regrade aplica a
policy separadamente a cada rodada e nunca muda o release das anteriores.

Start, save de draft de tentativa ou evidência, submit, finalização de
evidência e release compartilham a mesma semântica idempotente:

```ts
interface IdempotentCommandEnvelopeV1<TPayload> {
  commandType: string;
  resourceScope: string;
  idempotencyKey: string;
  requestHash: string;
  payload: TPayload;
}
```

O servidor autoriza o ator antes de consultar qualquer replay. Mesma chave no
mesmo escopo e mesmo request hash retorna o outcome persistido; mesma chave com
hash diferente gera conflito. A persistência define unique constraint,
retenção e lifecycle do outcome, sem depender apenas de cache em processo.
Cada evento append-only gerado pelo comando participa da mesma transação e da
mesma deduplicação: replay idêntico não cria uma segunda entrada de auditoria.

O método e o payload detalham se a evidência aguardada vem de aluno, peer,
instrutor ou provider. Não criar cinco colunas booleanas ou cinco tabelas de
fila. `AssessmentSubmission.Score`, `Passed`, `Feedback`, `GradedBy` e
`GradedAt`, caso preservados como read model, são projeções da rodada ativa e
não uma segunda fonte de verdade.

Contratos genéricos de resultado:

```ts
type GradeItemStateV1 = "graded" | "pending" | "unsupported";

interface GradeItemResultV1 {
  itemId: string;
  state: GradeItemStateV1;
  score: ScoreValue | null;
  maxScore: ScoreValue;
  feedback?: string;
  evidenceRefs: string[];
  reviewMethod: AssessmentReviewMethod;
  handlerKey: string;
  handlerVersion: string;
  providerKey?: string;
}

interface GradeResultV1 {
  schemaVersion: 1;
  state: "partial" | "final";
  score: ScoreValue | null;
  maxScore: ScoreValue;
  items: GradeItemResultV1[];
  feedback?: string;
  evidenceRefs: string[];
}
```

`GradeResultV1` e `GradeItemResultV1` não possuem campos de quiz. Evidência
específica de um tipo de assessment usa referência para um envelope versionado
produzido pelo adapter correspondente. Resultado parcial mantém score total
`null`; somente um resultado `final`, com todos os itens `graded`, possui score
total e pode produzir efeito acadêmico. A ordem de `items` segue o `itemOrder`
da entrega materializada.

Contrato inicial do histórico:

```ts
interface AssessmentEvaluationV1 {
  schemaVersion: 1;
  activeRoundId: string;
  rounds: GradeRoundV1[];
}

interface GradeRoundV1 {
  id: string;
  supersedesRoundId?: string;
  reason: "initial" | "regrade";
  definitionRevisionId: string;
  configuredReviews: AssessmentReviewMethod[];
  currentReview: AssessmentReviewMethod | null;
  status: "pending" | "running" | "awaiting-evidence" |
    "awaiting-instructor-resolution" | "failed" | "finalized";
  stages: ReviewStageV1[];
  finalResult: GradeResultV1 | null;
  initiatedBy?: string;
  initiatedAt: string;
  finalizedAt?: string;
}

interface ReviewStageV1 {
  id: string;
  method: AssessmentReviewMethod;
  status: "pending" | "running" | "awaiting-evidence" |
    "awaiting-instructor-resolution" | "completed" | "failed";
  handlerVersion: string;
  providerKey?: string;
  actorIds?: string[];
  evidenceRefs?: string[];
  result?: GradeResultV1;
  startedAt?: string;
  completedAt?: string;
}
```

Em toda rodada, `definitionRevisionId` deve ser igual ao da
`GradingExecution`. Regrade não pode apontar uma revisão diferente.

`ReviewStageV1.result` pode ser parcial. Cada item declara `graded`, `pending`
ou `unsupported`; `GradeRoundV1.finalResult` só recebe valor quando não há item
pendente. Em `AutomatedReview`, item sem avaliador determinístico não falha o
estágio: ele permanece pendente para o próximo estágio ou para uma resolução
de workflow.

Rodadas anteriores são imutáveis. Regrade cria nova rodada que referencia a
anterior; não edita nem apaga o resultado antigo. `Score`, `Passed`, `Feedback`,
`GradedBy` e `GradedAt` podem continuar como projeção relacional da rodada ativa
finalizada na submission oficial.

O ADR deve definir controle otimista de concorrência e limite/retenção do JSON.
Se o volume real tornar o payload inadequado, a mesma fronteira pode ser
projetada em tabela de rodadas sem mudar os contratos públicos.

## Execução oficial e test run

O pipeline e `GradingExecution` são compartilhados, mas seus owners e efeitos
não. `AssessmentSubmission` continua exclusiva da aplicação oficial.
`AssessmentTestRun` pertence à autoria, agrega sujeitos sintéticos, possui
retenção curta e não cria gradebook, progresso, release ou filas acadêmicas.
FKs mutuamente exclusivas na execução preservam integridade e permitem que
rounds e handlers usem a mesma raiz sem misturar os agregados.

A conclusão comum do orquestrador é uma transição interna neutra da
`GradingExecution`. No contexto `AuthorTest`, ela persiste apenas o resultado
diagnóstico e pode gerar telemetria operacional correlacionada ao test run. No
contexto `OfficialSubmission`, o adapter acadêmico persiste a rodada oficial e
grava `GradeResultFinalized` na outbox. O test run nunca emite esse evento
acadêmico, mesmo quando seu resultado tem a mesma estrutura.

Não adicionar `IsTest` a `AssessmentSubmission`. Antes de criar a entidade,
registrar no ADR a consulta, retenção e isolamento. Participantes sintéticos de
`PeerReview` ficam em payload versionado do test run, não em enrollments ou
submissions oficiais.

## Assessments atribuídos a grupos

Decisão: uma atividade de grupo possui uma única submission, uma única rodada
de grading e um único resultado acadêmico. Não replicar a resposta em várias
`AssessmentSubmission` antes da avaliação.

O subsistema de grupos atua em duas bordas:

```text
antes do grading
  -> resolve o CourseGroup e congela os participantes da tentativa
  -> cria ou retoma uma única AssessmentSubmission coletiva

grading
  -> recebe submissionId, definitionRevisionId e respostas
  -> executa uma única rodada e produz um único GradeResult

depois da transição canônica aplicável
  -> projeta o mesmo resultado para os participantes congelados
  -> atualiza gradebook e consultas de score depois da finalização
  -> atualiza progresso na transição definida pela policy de conclusão
```

O grading não recebe lista de membros nem executa por participante. Para
preservar integridade relacional sem um `SubjectId` polimórfico sem FK, o
baseline deve representar o sujeito com referências mutuamente exclusivas:

```text
AssessmentSubmission
  IndividualEnrollmentId nullable
  CourseGroupId nullable
  StartedByUserId
  AttemptNumber
  CHECK exatamente uma referência de sujeito preenchida

AssessmentSubmissionParticipant
  SubmissionId
  EnrollmentId
  UserId
  CapturedAt
  UNIQUE (SubmissionId, EnrollmentId)
```

Uma tentativa individual possui `IndividualEnrollmentId`; uma tentativa
coletiva possui `CourseGroupId` e participantes congelados no start. Índices
parciais garantem um número de tentativa único por sujeito. `StartedByUserId`
registra o ator que abriu a tentativa, sem torná-lo dono exclusivo da entrega.

Regras:

- qualquer comando coletivo valida que o ator pertence ao snapshot;
- o start usa lock por `(AssessmentId, CourseGroupId)` e é idempotente;
- `SaveCollectiveAttemptDraftV1` exige `expectedVersion`, idempotency key e
  request hash, registra ator e incrementa a versão do draft compartilhado;
- cada mutação aceita grava, na mesma transação,
  `CollectiveAttemptDraftChanged` append-only com ator, versão anterior, nova
  versão, request hash e instante; replay idêntico não duplica esse registro;
- `SubmitCollectiveAttemptV1` exige `expectedVersion`, idempotency key e request
  hash e realiza uma única finalização atômica;
- `SaveCollectiveSelfReviewDraftV1` e `SubmitCollectiveSelfReviewV1` aplicam as
  mesmas regras ao draft compartilhado de evidência: concorrência otimista,
  envelope idempotente, outcome persistido e evento append-only por mutação
  aceita, sem duplicação em replay;
- na policy inicial, qualquer participante do snapshot pode salvar ou
  finalizar; `StartedByUserId` não concede autoridade exclusiva e o ator real
  é sempre auditado;
- versão obsoleta, payload conflitante e escrita após finalização são
  rejeitados sem sobrescrever respostas;
- entrada ou saída posterior no grupo não altera participantes de tentativa já
  iniciada;
- submit, review docente, finalização e regrade acontecem uma vez por submission;
- `GradeResultFinalized` é emitido uma vez e suas projeções individuais usam
  chave idempotente `(SubmissionId, GradeRoundId, EnrollmentId)`;
- liberação permite que todos os participantes consultem o mesmo resultado;
- eventual ajuste individual é outro conceito de gradebook, não outra rodada
  nem mutação do resultado coletivo.

O `FanOutGroupSubmitAsync` atual deve ser removido. O fan-out permitido ocorre
somente após finalização, em projeções individuais; ele nunca cria submissions,
reviews ou rodadas adicionais.

## Resultado de grading

`GradeResultV1` é independente do tipo de assessment. Para quiz, o adapter
produz os mesmos itens genéricos usando o ID estável da questão e pode anexar
evidências privadas versionadas por referência; detalhes de resposta ou de
algoritmo de quiz não entram no resultado público do core.

O payload de respostas nunca aceita score, answer key ou resultado. `SelfReview`
usa endpoint próprio. `PeerReview` mantém avaliações individuais como
evidências do agregado. `AIReview` registra provider, policy e versão sem
credenciais.

## Auditoria e outbox

O histórico acadêmico confiável é `GradeRoundV1`, persistido na mesma transação
que a projeção final da submission. O `AuditService` de compliance atual usa
contexto independente e tolera falhas; por isso `AuditLogs` não pode ser a única
evidência de aprovação, override ou regrade.

Na mesma transação que muda uma execução, gravar as mensagens duráveis
aplicáveis ao seu contexto:

```text
ReviewStageStarted
ReviewStageCompleted
ReviewStageFailed
InstructorReviewApproved
InstructorReviewOverridden
SubmissionRegraded
GradeResultFinalized
GradeResultReleased
AIReviewRequested
```

`GradeResultFinalized` e `GradeResultReleased` são exclusivos do contexto
`OfficialSubmission`. Test runs podem registrar eventos diagnósticos próprios
ou logs operacionais, mas esses eventos não são consumidos por gradebook,
progresso, notificações acadêmicas ou passback.

Um dispatcher idempotente projeta essas mensagens em `AuditLogs`, gradebook,
progresso, notificações, analytics e passback. A mensagem de outbox é persistida
uma vez, mas cada consumidor obrigatório possui uma confirmação durável e
deduplicada por `(EventId, ConsumerKey)`. Falha de um consumidor mantém somente
sua entrega pendente; confirmações anteriores não são apagadas nem repetem seus
efeitos. A mensagem só encerra o dispatch quando todas as entregas capturadas em
sua rota forem confirmadas.

Se não houver outbox genérico com essas garantias, o ADR deve apresentar a
outbox do módulo e sua entidade de delivery/inbox no `SCHEMA-GATE`. Consumidores
adicionados em uma versão futura recebem eventos novos; replay histórico exige
operação explícita e nunca acontece por simples mudança da configuração.
Publicar domain events somente depois de `SaveChanges` não oferece replay
suficiente.

O `ApplicationDbContext.SaveChangesAsync` atual publica domain events em
processo depois da escrita. Eventos acadêmicos duráveis devem ser identificados
antes do commit, transformados em rows de outbox na mesma transação e excluídos
desse dispatch direto. Eventos locais não duráveis podem manter o publisher
atual. Um evento acadêmico nunca percorre os dois caminhos.

`AIReview` nunca chama um provider externo dentro da transação que inicia o
estágio. A transação persiste estágio, `AIReviewRequested` e um `requestId`
idempotente. Um worker consome a outbox, chama o provider e grava a resposta
validada por uma inbox/deduplicação antes de anexar a evidência ao estágio.
Retries reutilizam a identidade lógica da requisição e registram cada tentativa
operacional sem criar outro grading stage.

## Tarefas

Esta lista é temática. Sua ordem de execução e seus gates são definidos no
documento `08`; marcar itens aqui não autoriza aplicar todo o schema de uma vez.

- [ ] aprovar ADR de lifecycle e `AssessmentDefinitionRevision`;
- [ ] aprovar ADR de `ScoreValue`, `PercentValue`, precisão e passing score;
- [ ] aprovar ADR de rodadas, concorrência e outbox com confirmação durável por
  `(EventId, ConsumerKey)`;
- [ ] inventariar e testar todo artefato SQL ativo fora do `IModel` antes de
  substituir a cadeia de migrations pelo baseline limpo;
- [ ] definir `GradingExecution` com owner relacional exclusivo entre sujeito
  sintético de test run e submission;
- [ ] modelar `AssessmentTestRunSubject` para que cada alvo multipersona possua
  uma execução independente;
- [ ] aprovar ADR de sujeito da submission e snapshot de participantes,
  preservando a decisão de uma única avaliação por grupo;
- [ ] renomear `GradingMethods` para `ReviewMethods` e aplicar constraint exata;
- [ ] adicionar `SelfReview = 16`;
- [ ] fechar `AssessmentReviewPolicyV1` e parsers versionados;
- [ ] remover `Assessment.DefinitionPayload`, sua versão e o setter genérico;
  qualquer fonte mutável de policy restante deve ser tipada, ter nome próprio e
  não replicar coluna relacional;
- [ ] criar revisão imutável candidata e ponteiro de publicação ativa;
- [ ] publicar candidata somente quando o hash do draft ainda coincidir;
- [ ] implementar unpublish versionado que preserve revisões e execuções;
- [ ] converter scores, pesos e percentuais acadêmicos para inteiros de escala
  `100` no baseline limpo;
- [ ] vincular submission e test run à revisão usada no start;
- [ ] fechar schemas de resposta, estágio, rodada e resultado;
- [ ] fechar capability descriptor por método e contexto de execução;
- [ ] definir as portas genéricas C# e isolar o adapter de quiz no servidor,
  com registro versionado no composition root e teste de arquitetura .NET que
  impeça dependência reversa;
- [ ] fechar `AssessmentExecutionManifestV1`, catálogo suportado, preflight de
  deploy, retenção de artefatos e inclusão no `ExecutionSnapshotHash`, cobrindo
  adapter agregado por tipo de assessment, handler e provider;
- [ ] definir a política inicial de contribuição de tentativas e rejeitar
  múltiplas tentativas enquanto ela não estiver implementada;
- [ ] fechar `AssessmentAuthoringSourceV1`,
  `AssessmentExecutionSnapshotV1`, JCS e fixtures dos dois hashes;
- [ ] adicionar persistência relacional mínima de `GradingExecution`;
- [ ] persistir `AssessmentExecutionDeliveryV1` e `DeliveryHash` de forma
  imutável na `GradingExecution`, como JSON canônico textual com `itemOrder`,
  após aprovação no `SCHEMA-GATE`;
- [ ] separar finalização acadêmica de liberação do resultado;
- [ ] persistir release somente por `GradeRoundId` e validar a submission pelo
  owner da execução;
- [ ] fechar `AssessmentContentCompletionPolicyV1` com
  `on-release-and-pass` e reservar `scheduled` em
  `AssessmentResultReleasePolicyV1`;
- [ ] persistir release por rodada e preservar a última rodada liberada enquanto
  uma substituta estiver em regrade ou retida;
- [ ] implementar `ReleaseGradeResult` com autorização, concorrência,
  idempotência, auditoria e round esperado;
- [ ] persistir, para policy `immediate`, uma solicitação idempotente de release
  na mesma transação da finalização e processá-la pelo comando comum;
- [ ] aplicar `IdempotentCommandEnvelopeV1` a start, saves de draft de tentativa
  ou evidência, submit, evidência final e release, com replay persistido,
  auditoria não duplicada e conflito por payload divergente;
- [ ] impedir criação de release para test run;
- [ ] impedir `GradeResultFinalized` e `GradeResultReleased` no contexto de
  test run;
- [ ] persistir histórico de regrade sem sobrescrita e sem trocar revisão,
  manifest, entrega ou respostas da execução;
- [ ] implementar outbox e consumers idempotentes, retirando eventos acadêmicos
  do dispatch em processo de `SaveChangesAsync` e mantendo receipt independente
  por consumer;
- [ ] implementar request/outbox e response/inbox idempotentes de `AIReview`;
- [ ] substituir `FanOutGroupSubmitAsync` por submission coletiva e projeções
  individuais pós-finalização;
- [ ] implementar draft coletivo versionado e submit final atômico com trilha
  append-only de cada mutação aceita e ator auditável;
- [ ] adicionar testes EF de criação limpa do schema, round-trip JSON e
  concorrência.

## Critério de saída

- draft, revisão publicada e alterações pendentes são distinguíveis;
- o professor pode testar uma candidata e ativar exatamente a mesma revisão;
- uma tentativa continua corrigível após edição ou unpublish do quiz;
- média, mediana e crédito parcial fazem round-trip pelo inteiro de unidades;
- score e percentual são comparados numericamente e projeções agregadas são
  recalculadas no domínio, sem `decimal` no banco;
- submissão e curso possuem limiares de aprovação sem conflito;
- resultado finalizado alimenta gradebook independentemente de estar liberado
  ao aluno;
- projeções learner usam somente rodadas liberadas e não permitem inferir score
  retido por agregados;
- somente a contribuição efetiva calculada por uma política canônica alimenta
  gradebook e integrações de score;
- progresso é projetado uma única vez conforme a policy de conclusão, sem ser
  inferido do peso ou de uma escrita genérica em `ContentInteraction`;
- conclusão dependente de aprovação só se torna learner-visible depois do
  release da rodada, sem revelar `Passed` retido;
- tentativa coletiva executa grading e regrade uma única vez e projeta o mesmo
  resultado para o snapshot de participantes;
- regrade permanece na revisão, manifest, entrega e respostas originais e
  release por rodada preserva a última visão learner;
- resposta, evidência de review e resultado são dados separados;
- toda execução possui exatamente um owner relacional, cada alvo sintético
  possui sua própria execução e test run não possui estado de release;
- capability de teste não autoriza publicação ou execução oficial;
- estado `Published`/`ChangesPending` depende somente de
  `AuthoringSourceHash`, nunca do manifest ou do deploy atual;
- toda execução resolve as versões exatas do manifest da revisão, inclusive
  após deploy ou regrade;
- preflight impede deploy incompatível antes do tráfego e a política de
  retenção preserva artefatos necessários a rollback e regrade;
- release manual passa por comando idempotente e nunca altera uma rodada
  obsoleta;
- regrade preserva todas as rodadas anteriores;
- gradebook, auditoria e notificações podem ser refeitos a partir de eventos
  duráveis;
- crash depois do commit não perde evento e retry não duplica efeito;
- nenhuma alteração de schema existe sem ownership e consulta documentados;
- o fluxo não possui migration incremental, migração de dados ou caminho
  substituído mantido por compatibilidade; o banco nasce do baseline de criação
  final com todos os artefatos SQL ativos aprovados, inclusive os que não são
  representados pelo `IModel`.
