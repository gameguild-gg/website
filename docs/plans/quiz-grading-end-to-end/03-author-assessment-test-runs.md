# 03. Test run de assessment pelo professor

## Objetivo

Entregar o primeiro fluxo vertical completo usando o professor como autor,
respondente de teste e, quando configurado, revisor final. Isso permite validar
content, assessment e grading antes de construir toda a jornada acadêmica do
aluno.

O test run deve reutilizar os mesmos contratos, projeção learner-safe,
orquestrador e handlers de estágio do fluxo oficial. A diferença está nos
efeitos externos: teste não cria progresso, nota acadêmica ou contribuição no
gradebook.

O test run cobre os reviews disponíveis sobre uma revisão candidata ou ativa.
Em `AIReview`, provider não registrado produz diagnóstico de readiness e impede
a execução; indisponibilidade transitória no runtime mantém o estágio pendente
para retry. O publish permanece bloqueado até haver capability
`OfficialSubmission`, ainda que `AuthorTest` esteja disponível.
`PeerReview` usa participantes de teste isolados para exercitar distribuição e
agregação sem matrículas reais.

## Três experiências distintas

| Experiência | Ownership | O que valida | Persistência acadêmica |
| --- | --- | --- | --- |
| Preview de content | content | renderização e interação autoral | nenhuma |
| Test run de assessment | assessment | resposta, workflow, grading, revisão e resultado | isolada |
| Tentativa oficial | assessment submission | jornada real do aluno e efeitos acadêmicos | oficial |

O preview de content pode usar o estado autoral para conferir apresentação. O
test run exige uma definição salva e uma revisão imutável candidata ou ativa,
usando a projeção que um aluno receberia. Ele não deve receber o JSON autoral
diretamente.

## Relação com o dry-run anterior

O plano anterior
[`learning-grading-part-4-authoring-preview-and-dry-run.md`](../learning-grading-part-4-authoring-preview-and-dry-run.md)
propõe um endpoint stateless para testar correção determinística. Esse endpoint
continua útil como primitiva de diagnóstico, mas não fecha:

- workflows assíncronos;
- `SelfReview`;
- múltiplas avaliações de pares;
- revisão final `InstructorReview`;
- reload e retomada;
- transições e resultado de cada estágio.

O `Assessment Test Run` amplia esse conceito para uma execução isolada e
persistida do pipeline.

## Por que não usar `AssessmentSubmission`

`AssessmentSubmission` representa tentativa acadêmica e exige conceitos como
aluno, enrollment, tentativa, progresso e gradebook. Usá-la para o professor
exigiria matrícula falsa ou um `IsTest` que todas as consultas precisariam
lembrar de filtrar.

Direção recomendada: um agregado próprio `AssessmentTestRun`, sem
`EnrollmentId`, `CourseGroupId` ou efeitos acadêmicos. Ele possui um ou mais
`AssessmentTestRunSubject`, um por alvo sintético avaliado. Cada subject é owner
de uma `GradingExecution`; a submission oficial é o outro tipo de owner
possível. O orquestrador trabalha contra o mesmo contrato e a mesma raiz de
grading, não diretamente contra a tabela do owner:

```text
IGradingExecutionContext
  OfficialAssessmentSubmissionContext
  AuthorAssessmentTestRunSubjectContext

GradingExecution
  exatamente um owner relacional entre AssessmentTestRunSubject e AssessmentSubmission
```

Essa separação evita duplicar regras e evita contaminar o modelo oficial.

## Persistência mínima proposta

Uma estrutura autoral e uma raiz compartilhada, sujeitas aos ADRs de `SEQ-00`.
Sua definição entra no baseline global aprovado em `SEQ-02`, sem transformação
incremental de banco existente:

```text
AssessmentTestRun
  Id
  AssessmentId
  DefinitionRevisionId
  AuthorId
  Status
  ExecutionPayload
  CreatedAt
  UpdatedAt
  ExpiresAt

AssessmentTestRunSubject
  Id
  AssessmentTestRunId
  PersonaKey
  CreatedAt

GradingExecution
  AssessmentTestRunSubjectId
  AssessmentSubmissionId null
  DefinitionRevisionId
  ExecutionDeliveryCanonicalJson
  DeliveryHash
  DeliveryHashVersion
  EvaluationState
  EvaluationPayload
```

O test run nunca possui `AssessmentResultRelease`, `ResultReleaseState` ou
evento `GradeResultFinalized`/`GradeResultReleased`. Sua conclusão usa a
transição interna comum da `GradingExecution`, mas produz apenas resultado
diagnóstico associado à execução autoral. Eventual evento de diagnóstico tem
nome e consumers próprios e nunca entra no pipeline acadêmico.

O modelo não pode possuir somente um `AuthorId` e um payload de resposta único
no test run, pois isso não representa distribuição entre pares.
Usar um payload multipersona explícito:

```ts
interface AssessmentTestExecutionV1 {
  schemaVersion: 1;
  participants: Array<{
    id: string;
    subjectId: string;
    label: string;
    response?: AssessmentResponseEnvelopeV1;
    selfReviewPayload?: QuizSelfReviewV1;
    status: "draft" | "submitted" | "reviewing" | "completed";
  }>;
  peerClaims: TestPeerClaimV1[];
  peerReviews: TestPeerReviewV1[];
}
```

Reviews não-peer criam um participante padrão. `PeerReview` cria pelo menos o
número de participantes exigido pela policy e mantém respostas, claims,
reviews e resultados separados por `participantId`. Payloads podem permanecer
em `jsonb` versionado porque a interação é sequencial pelo professor e possui
retenção curta. Não criar enrollment, `AssessmentSubmission` oficial ou tabelas
acadêmicas para simular participantes. A relação subject/execution é relacional
e não é duplicada em `ExecutionPayload`.

Cada subject possui sua própria `AssessmentExecutionDeliveryV1`, materializada
uma única vez na `GradingExecution`. Isso permite que personas recebam challenges
distintos sem regenerá-los em reload ou retry. Restart cria novas execuções e
pode gerar novas entregas; a execução anterior permanece reproduzível pelo
`DeliveryHash`. A entrega persiste JSON canônico textual e `itemOrder`
explícito; a ordem de propriedades de `items` nunca define apresentação.

Uma execução pode expirar e ser removida por retenção. Ela não entra no audit
log acadêmico; eventos de diagnóstico podem ser mantidos no próprio payload ou
em logs operacionais correlacionados pelo `testRunId`.

## Garantias de isolamento

Um contexto `AuthorTest` deve bloquear por construção:

- criação de `AssessmentSubmission`;
- consumo de tentativas do aluno;
- progresso e conclusão de conteúdo;
- gradebook e cálculo de nota do curso;
- notificações acadêmicas;
- LTI/passback;
- analytics de aprendizagem;
- distribuição real para pares.

O resultado é confiável como teste do pipeline, mas não é uma nota acadêmica.

## Fluxo da tela

Adicionar `Testar assessment` como ação principal no editor de assessment. A
execução deve abrir uma rota ou superfície full-screen focada na tarefa:

```text
1. Preparar ou escolher uma revisão candidata/ativa, criar o test run e
   materializar a entrega de cada subject
2. Responder como um ou mais participantes
3. Enviar respostas
4. Executar o review primário configurado
5. Revisar como instrutor, quando configurado
6. Inspecionar resultado, estágios e diagnósticos
```

A tela deve mostrar permanentemente:

- badge `Modo de teste`;
- revisão e workflow usados;
- estágio corrente;
- ação para reiniciar;
- indicação de que não haverá gradebook ou progresso.

Não exibir textos instrucionais genéricos dentro do produto. O estado de teste,
os nomes das etapas e os comandos devem tornar o fluxo autoexplicativo.

## Comportamento por método

### `InstructorReview`

O professor responde em persona de aluno e depois alterna para a etapa de
correção, usando a mesma superfície prevista para o SpeedGrader.

### `AutomatedReview`

Ao enviar, o servidor executa a correção determinística e mostra cobertura e
resultado por item. Itens sem regra determinística ficam pendentes, sem erro ou
score zero. Com revisão docente, a tela avança para completar os pendentes,
aprovar os avaliados ou aplicar override. Sem revisão docente, cobertura parcial
deixa a rodada aberta e exibe o diagnóstico necessário à decisão autoral.

### `SelfReview`

Depois de responder, o professor permanece na persona de aluno e preenche a
autoavaliação. Com revisão docente, alterna depois para revisor.

### `PeerReview`

O professor cria participantes de teste isolados, responde por cada persona e
executa a mesma regra de atribuição, anonimato, quantidade recebida e agregação
do fluxo oficial. Identidades sintéticas nunca entram nas filas ou tabelas
acadêmicas. A superfície permite alternar persona e mostra separadamente qual
submissão cada resultado avalia.

### `AIReview`

O teste usa o provider configurado. A tela mostra provider, versão, execução e
falhas operacionais sem transformar erro em nota. Um provider controlado pode
ser usado em testes automatizados; ausência de capability `AuthorTest` impede
iniciar o estágio, enquanto ausência de `OfficialSubmission` bloqueia publish
independentemente do resultado do teste. Provider temporariamente indisponível
mantém o estágio em retry com diagnóstico explícito.

A solicitação usa a mesma outbox e inbox idempotentes da execução oficial. O
contexto de teste muda apenas os efeitos posteriores; ele não introduz uma
chamada síncrona ou menos durável ao provider.

## API-alvo

Casos de uso equivalentes a:

```text
POST   /assessments/{assessmentId}/test-runs  { definitionRevisionId }
GET    /assessment-test-runs/{testRunId}
POST   /assessment-test-runs/{testRunId}/participants
POST   /assessment-test-runs/{testRunId}/participants/{participantId}/submit
POST   /assessment-test-runs/{testRunId}/participants/{participantId}/self-review
POST   /assessment-test-runs/{testRunId}/participants/{reviewerId}/peer-reviews/{claimId}
POST   /assessment-test-runs/{testRunId}/participants/{participantId}/instructor-review
POST   /assessment-test-runs/{testRunId}/restart
DELETE /assessment-test-runs/{testRunId}
```

Todos exigem permissão de gestão no assessment. O cliente nunca escolhe outro
workflow ou revisão depois que o test run foi criado.
O start também congela o `AssessmentExecutionManifestV1` da revisão. Cada
execução resolve somente o adapter agregado e os handlers nas versões exatas do
manifest; um deploy não pode alterar silenciosamente uma execução aberta.
O artefato já passou pelo preflight do ambiente antes de receber tráfego;
falha de resolução durante o test run continua sendo diagnóstico explícito,
mas não substitui esse bloqueio preventivo de deploy.

## Tarefas

- [ ] fechar o contrato `IGradingExecutionContext`;
- [ ] decidir e revisar a persistência de `AssessmentTestRun` antes de alterar
  o baseline de schema;
- [ ] criar test run vinculado a uma revisão candidata ou ativa;
- [ ] congelar e resolver exatamente o manifest executável da revisão;
- [ ] criar um subject sintético por alvo avaliado e uma execução por subject;
- [ ] materializar uma entrega concreta por execução e preservar seus bytes e
  `DeliveryHash` em resume/retry, incluindo `itemOrder` e ordenações internas;
- [ ] fechar `AssessmentTestExecutionV1` com participantes, claims, reviews e
  resultados por participante;
- [ ] reutilizar a projeção learner-safe;
- [ ] renderizar `QuizPlayer` em persona de aluno;
- [ ] submeter respostas estruturadas pelo orquestrador comum;
- [ ] implementar alternância de persona por estágio;
- [ ] implementar `SelfReview` na persona respondente;
- [ ] implementar participantes isolados e agregação de `PeerReview`;
- [ ] validar provider e executar `AIReview` quando disponível;
- [ ] exibir cobertura e resultado parcial de `AutomatedReview`;
- [ ] reutilizar a superfície de revisão docente;
- [ ] bloquear todos os efeitos acadêmicos no contexto de teste;
- [ ] permitir restart e retenção/limpeza;
- [ ] exibir resultado por item, transições e diagnósticos.

## Testes

- test run não cria enrollment nem submission oficial;
- ativar a revisão ao final do test run não inicia nem agenda tentativa de
  aluno;
- test run não altera gradebook, progresso, notificação ou passback;
- test run não emite `GradeResultFinalized` nem `GradeResultReleased`;
- answer key não aparece no payload da persona de aluno;
- resultado determinístico é idêntico ao avaliador oficial;
- cobertura parcial preserva itens corrigidos e deixa os demais pendentes sem
  falha;
- `SelfReview` registra a persona respondente;
- `PeerReview` usa a mesma política de distribuição e agregação do oficial;
- cada participante de peer possui resposta, assignments e resultado próprios;
- revisão com `AIReview` sem registro cria diagnóstico explícito, não executa e
  não pode ser publicada; indisponibilidade de runtime não produz resultado e
  pode ser retomada;
- revisão docente preserva resultado anterior e override;
- reload retoma o estágio correto;
- restart cria subjects e execuções limpos;
- autor sem permissão não acessa test run.

## Critério de saída

- professor percorre content -> assessment -> resposta -> grading -> revisão ->
  resultado sem depender de aluno matriculado;
- o pipeline utilizado é o mesmo que será chamado pela submissão oficial;
- publicar depois do teste ativa o mesmo `definitionRevisionId` quando o draft
  não mudou;
- o fluxo termina na autoria; a jornada oficial só começa por acesso posterior
  e independente de um aluno ao assessment publicado;
- nenhum efeito acadêmico é produzido;
- os nove workflows são representáveis nessa superfície;
- reviews com capability disponível percorrem o pipeline completo;
- `PeerReview` é realmente multipersona, não uma resposta única reutilizada;
- cada alvo multipersona possui execução e resultado próprios dentro do mesmo
  test run;
- `AIReview` sem registro bloqueia execução e publicação; provider indisponível
  deixa o test run pendente e retomável, sem nota.
