# 05. Tentativas e resultados do aluno

## Objetivo

Unir o player real de quiz ao lifecycle oficial de `AssessmentSubmission`, sem
expor answer keys e cobrindo resposta, os cinco reviews e visualização do
resultado.

Esta fase começa depois que os workflows foram exercitados pelo professor no
`AssessmentTestRun`. Ela conecta enrollment, tentativa acadêmica e identidade
real do aluno ao pipeline já implementado; não recria grading ou revisão.

A fronteira de leitura learner-safe, porém, é pré-requisito anterior ao test
run: ainda em `SEQ-05`, todas as rotas learner/public deixam de retornar o DTO
autoral. Até o endpoint oficial de start existir, quiz avaliável fica
fail-closed para aluno em vez de continuar disponível por um endpoint genérico.

## Problemas atuais

- o viewer antigo renderiza `QuizPlayer`, mas usa o fluxo genérico de interação;
- `submitActivity`, `ProgramContentController.SubmitContent`,
  `ProgramWriteService.SubmitUserContentAsync`,
  `ProgramCrudController.MarkContentCompleted`,
  `ProgramCrudController.MarkMyContentCompleted`,
  `ContentInteractionController.UpdateProgress`,
  `ContentInteractionController.SubmitContent`,
  `ContentInteractionController.CompleteContent`, os services correspondentes e
  `ActivityGradeController` ainda formam rotas paralelas de resposta, progresso,
  conclusão ou nota;
- a rota oficial de assessment cria submission, mas usa textarea genérica;
- o endpoint genérico pode expor `ProgramContent.JsonBody` autoral;
- a API valida apenas se `StructuredAnswerPayload` é JSON sintaticamente válido;
- a tentativa não está vinculada claramente a um snapshot imutável da
  definição;
- `AssessmentsController`, `LearnerWorkspaceMapper`, dashboard e workspace leem
  `AssessmentSubmission.Score`, `Passed`, `Feedback` e `FinalGrade` diretamente,
  sem um gate comum de release;
- o score relacional inteiro não preserva agregações fracionárias;
- o aluno não vê resultado por questão no lifecycle oficial.

## Bundle learner-safe

Ao iniciar ou reabrir uma tentativa, retornar:

```ts
interface QuizAttemptBundleV1 {
  schemaVersion: 1;
  assessment: LearnerAssessmentPolicy;
  submission: LearnerAttempt;
  definitionRevisionId: string;
  deliveryHash: string;
  quiz: QuizLearnerContentDocument;
  workflow: LearnerWorkflowSummary;
}
```

`definitionRevisionId` precisa identificar uma
`AssessmentDefinitionRevision` publicada. O start é rejeitado quando o
assessment não possui revisão ativa. Reopen sempre retorna a revisão já presa à
tentativa, inclusive após alterações, nova publicação ou unpublish.

`quiz` é a projeção da `AssessmentExecutionDeliveryV1` persistida na
`GradingExecution`, não uma nova projeção gerada a cada GET. O servidor
materializa uma única vez os valores de `Numeric`/`Formula`, as ordenações e
outros challenges variáveis usando as versões fixadas no manifest. Resume e
retry preservam os mesmos bytes e `deliveryHash`; o browser nunca escolhe ou
devolve esses valores como autoridade.

A ordem dos itens vem de `AssessmentExecutionDeliveryV1.itemOrder`; nenhum
consumer pode inferi-la da ordem das propriedades de `items`. O JSON canônico
textual persistido é a fonte dos bytes e do hash. Todo dado privado de correção
é derivado da revisão imutável mais a entrega pública concreta; ele nunca é
acrescentado ao bundle learner.

Nunca retornar:

- answer keys;
- respostas corretas ocultas;
- rubrica privada do instrutor;
- prompts privados de IA;
- resultados de outros alunos ou pares;
- JSON autoral completo.

DTO autoral e DTO learner-safe são contratos distintos. O primeiro exige
permissão de gestão do curso e não pode ser selecionado por expansão, query ou
mapper genérico em uma rota learner/public. Quizzes avaliáveis são entregues ao
aluno somente pelo `QuizAttemptBundleV1`; rotas públicas de content podem
continuar servindo conteúdo não avaliável pela projeção pública apropriada.
Essas rotas genéricas também devem rejeitar no servidor qualquer submit de quiz
avaliado, marcação de conclusão, atualização de progresso ou grade direta.
`ContentInteraction` pode registrar consumo de conteúdo onde fizer sentido e,
se aprovado no ADR, receber a projeção canônica de conclusão como read model;
nunca armazena respostas nem aceita comando genérico de conclusão ou nota para
esse assessment.

## Submissão canônica

O runtime comum persiste um envelope independente do tipo de assessment:

```ts
interface AssessmentResponseEnvelopeV1<TPayload = unknown> {
  schemaVersion: 1;
  contentType: string;
  payloadSchema: string;
  payload: TPayload;
}
```

Para quiz, `@game-guild/grading-adapter-quiz` fecha o payload concreto:

```ts
interface QuizAnswerEnvelopeV1 {
  schemaVersion: 1;
  answers: Record<string, QuizAnswerV1>;
}

type QuizAnswerV1 =
  | { type: "SINGLE_CHOICE"; optionId: string | null }
  | { type: "MULTIPLE_CHOICE"; optionIds: string[] }
  | { type: "TRUE_FALSE"; value: boolean | null }
  | { type: "FILL_IN_THE_BLANK"; values: Record<string, string> }
  | { type: "SHORT_ANSWER"; value: string }
  | { type: "ESSAY"; richText: QuizRichTextAnswerV1; plainText: string }
  | { type: "MATCHING"; matches: Record<string, string> }
  | { type: "ORDERING"; itemIds: string[] }
  | { type: "CATEGORIZATION"; categoryIdsByItem: Record<string, string[]> }
  | { type: "RATING"; value: number | null }
  | { type: "NUMERIC"; value: string }
  | { type: "FORMULA"; expression: string }
  | { type: "HOTSPOT"; point: { x: number; y: number } | null }
  | { type: "HIGHLIGHT"; spans: Array<{ start: number; end: number }> };

interface QuizRichTextAnswerV1 {
  format: "lexical";
  schemaVersion: 1;
  document: Record<string, unknown> | null;
}
```

O `QuizPlayer` envia
`AssessmentResponseEnvelopeV1<QuizAnswerEnvelopeV1>` com
`contentType = "quiz"` e `payloadSchema = "quiz-answer-v1"`. Nenhuma variante
usa delimitadores textuais para representar objetos, JSON serializado dentro
de string ou número convertido em texto fora dos campos cujo domínio autoral é
textual, como `NUMERIC.value` e `FORMULA.expression`.

`QuizRichTextAnswerV1.document` é um subdocumento opaco para o core de
grading, mas não um JSON sem validação: o contrato de quiz fixa formato,
versão, tamanho, profundidade e parser learner-safe. O avaliador usa o
`plainText` persistido no mesmo envelope, salvo quando uma versão futura do
algoritmo declarar explicitamente suporte ao formato rico.

A API valida schema, tamanho, IDs, tipos, campos permitidos, revisão, janela,
tempo e idempotência. Score e feedback não pertencem a esse payload.
Challenge, variáveis geradas, seed e ordem inicial também não pertencem ao
payload: o decoder versionado usa a entrega persistida como contexto e rejeita
qualquer tentativa de substituí-la.
Start e submit usam `IdempotentCommandEnvelopeV1`: replay com a mesma chave e
request hash retorna o outcome persistido; mesma chave com payload diferente
gera conflito depois da autorização do ator.

O core trata `payload` como opaco até resolver pelo manifest o decoder exato de
`contentType + payloadSchema`. O adapter e os DTOs C# publicam schemas
equivalentes e fixtures para todas as 14 variantes, incluindo payloads
inválidos, limites, cardinalidade e versões desconhecidas. O JSON canônico
validado é persistido uma única vez para retry e regrade; nenhum alias de
`StructuredAnswerPayload` permanece no contrato novo.

## `SelfReview`

Quando o método primário for `SelfReview`, após a submissão o aluno recebe uma
etapa separada:

```ts
interface QuizSelfReviewV1 {
  schemaVersion: 1;
  submissionId: string;
  items: Array<{
    contentBlockId: string;
    score: ScoreValue;
    feedback?: string;
  }>;
  generalFeedback?: string;
}
```

O servidor valida autoria, limites e completude. Sem revisão docente, essa
etapa finaliza. Com `InstructorReview`, ela produz resultado primário e entra
na fila do professor.

Em submission coletiva existe uma única evidência de self review por round.
Qualquer participante congelado pode editar o draft com concorrência otimista;
um único submit final fecha a evidência e registra o ator real. O grading recebe
uma evidência do sujeito coletivo, nunca uma por integrante.
Save e submit usam `IdempotentCommandEnvelopeV1`, outcome persistido e uma
trilha append-only por mutação aceita; replay idêntico não duplica versão ou
auditoria.

A mesma surface de review atende `AuthorTest` e `OfficialSubmission`. Ela
carrega e retoma a evidência, salva draft com `expectedVersion`, trata conflito
de concorrência, realiza submit final idempotente e fica somente leitura depois
da finalização. No caso coletivo, todos os participantes autorizados veem o
mesmo draft e cada mutação registra o ator real.

## `PeerReview`

Após enviar o próprio trabalho, o aluno recebe tarefas de revisão até cumprir
sua cota. Cada workspace usa a projeção anônima existente. O score oficial da
submissão revisada só é calculado quando ela recebe o limiar configurado; a cota
de reviews que um aluno realiza não substitui esse limiar.

O aluno vê reviews recebidos conforme a política de feedback. Quando
`InstructorReview` também estiver configurado, o resultado agregado permanece
provisório até a revisão docente.

Claims expirados voltam à distribuição e não contam para a cota do revisor. Se
a janela encerrar sem evidência mínima, o aluno vê `Avaliação entre alunos
requer intervenção`, sem score zero ou estado final falso. O stage passa para
`AwaitingInstructorResolution`; um instrutor pode estender a janela, reatribuir
claims ou finalizar por resolução docente explícita. A última ação cria
evidência e auditoria próprias, sem fingir que o limiar peer foi atingido.

Quando o alvo é uma submission coletiva, os revisores continuam sendo alunos
individuais. Nenhum participante congelado do grupo-alvo pode receber claim
sobre essa submission. As evidências são agregadas uma vez para produzir o
resultado único do grupo.

## `AIReview`

O aluno não interage com provider, prompt ou credenciais. Após submeter, vê
somente o estado operacional permitido. Falha ou indisponibilidade mantém a
tentativa pendente; nunca aparece como score zero.

## `AutomatedReview`

O servidor devolve o progresso por item. Questões com regra determinística
podem aparecer como avaliadas internamente, enquanto questões sem cobertura
permanecem pendentes. Com `InstructorReview`, a tentativa segue para o
professor. Sem ele, a tentativa não entra em falha e não recebe zero; permanece
sem resultado final até a política de autoria resolver a configuração.

## Assessments de grupo

Ao iniciar um quiz coletivo, a API resolve o `CourseGroup`, congela os
participantes e cria ou retoma uma única `AssessmentSubmission` do grupo. Todos
os participantes autorizados consultam a mesma tentativa; `StartedByUserId`
registra quem a abriu, sem tornar essa pessoa dona exclusiva da entrega.

O submit executa um único pipeline. Review docente, finalização, liberação e
regrade também ocorrem uma vez. `GradeResultFinalized` projeta o mesmo resultado
no gradebook de cada participante; progresso é projetado na transição definida
pela policy de conclusão. Entradas ou saídas posteriores no grupo não alteram
essa tentativa.

Todos os participantes recebem a mesma entrega concreta da execução coletiva.
O resultado e a avaliação são únicos; apenas gradebook, progresso e visibilidade
são projetados uma vez por participante.

A UI deve identificar claramente a tentativa como coletiva e refletir mudanças
feitas por outro integrante. Escritas concorrentes exigem versionamento
otimista; o plano não permite que duas cópias independentes da mesma tentativa
sejam avaliadas.

`SaveCollectiveAttemptDraftV1(expectedVersion, idempotencyKey, requestHash)`
atualiza o draft compartilhado e grava, na mesma transação,
`CollectiveAttemptDraftChanged` append-only com
ator, versão anterior, nova versão, request hash e instante. Replay idêntico
reutiliza o outcome sem duplicar auditoria. `SubmitCollectiveAttemptV1(
expectedVersion, idempotencyKey, requestHash)` realiza uma única finalização
atômica. O servidor rejeita versão obsoleta, payload conflitante, escrita após
finalização e ator fora do snapshot. A retenção da trilha segue a tentativa;
ela não precisa replicar o conteúdo das respostas quando versões e hashes
permitirem reconstruir autoria e concorrência.

No primeiro corte coletivo, somente `InstructorReview` e `AutomatedReview` são
habilitados. `SelfReview` e `PeerReview` são habilitados nos marcos seguintes,
depois de seus E2Es cobrirem explicitamente o sujeito coletivo. `AIReview`
permanece bloqueado sem provider real.

## Projeção de progresso do content

Assessment e content permanecem agregados distintos, mas o quiz avaliado não
pode ser concluído por uma rota genérica. Antes do primeiro E2E oficial, um ADR
define a policy de conclusão capturada na revisão: qual transição canônica
projeta progresso e se a conclusão depende de submit, finalização, liberação ou
aprovação. O modo dependente de aprovação é `on-release-and-pass`, nunca
`on-pass`: conclusão learner-visible antes do release revelaria o valor retido
de `Passed`.

Um projector idempotente consome exclusivamente os eventos canônicos de
submission/resultado e atualiza o estado por assessment, content, enrollment e
transição de origem. Se `ContentInteraction` for reutilizado, ele passa a ser
somente read model dessa projeção para quiz avaliado. As rotas `complete`,
`update progress`, submit genérico e `ActivityGrade` rejeitam esse quiz. Para
grupo, a única transição coletiva gera uma projeção por participante congelado,
sem repetir grading.

## Estado mostrado ao aluno

Rótulos devem refletir a etapa real:

```text
Em andamento
Enviado
Aguardando avaliação entre alunos
Avaliação entre alunos requer intervenção
Aguardando avaliação por IA
Aguardando correção automática
Aguardando sua autoavaliação
Aguardando avaliação do instrutor
Aguardando revisão do instrutor
Reavaliação em andamento
Avaliado
Devolvido
```

Resultado provisório não deve parecer publicado. A política de feedback decide
quando resposta correta, feedback por item e justificativa ficam visíveis.

`EvaluationState.Finalized` permite gradebook e auditoria, mas não torna o
resultado visível. A rota do aluno só expõe nota e feedback depois de
`ResultReleaseState.Released`; estados `Withheld` e `Scheduled` retornam apenas
o status permitido, sem o resultado final.

Release é versionado por rodada. Se uma nota liberada entrar em regrade, o aluno
continua vendo a última rodada liberada e o estado `Reavaliação em andamento`.
A nova rodada substitui a visão learner somente após seu próprio release. A
projeção interna do gradebook pode usar a rodada finalizada mais recente, mas
workspace, dashboard, listagens e nota global learner usam apenas contribuições
liberadas e não retornam agregados que permitam inferir valores retidos.

## Tarefas

- [ ] criar endpoint idempotente de start/reopen;
- [ ] implementar projeção learner-safe no limite da API;
- [ ] substituir todos os read models learner que leem score diretamente,
  incluindo `AssessmentsController.GetSubmission`, `GetMySubmissions`, respostas
  de start/submit, `LearnerAssessmentSubmissionDto`,
  `LearnerAssessmentAttemptDto`, dashboard, workspace, mappers, records web e
  clients gerados;
- [ ] materializar e persistir `AssessmentExecutionDeliveryV1` no start e
  devolver sempre o mesmo `deliveryHash` em resume/retry;
- [ ] retirar JSON autoral de todos os DTOs e mappers acessíveis ao aluno antes
  de habilitar test run ou publish;
- [ ] bloquear o caminho genérico de execução de quiz avaliável até o start
  oficial estar disponível;
- [ ] rejeitar quiz avaliado nas rotas genéricas de submit, complete, update
  progress e grade direta; somente a projeção canônica pode atualizar o read
  model de progresso;
- [ ] implementar a policy e a projeção idempotente de conclusão de content para
  tentativa individual e coletiva;
- [ ] garantir que `on-release-and-pass` só projete conclusão depois do release
  da rodada aprovada, inclusive em dashboard, workspace e pré-requisitos;
- [ ] renderizar `QuizPlayer` na rota oficial de activities;
- [ ] enviar `AssessmentResponseEnvelopeV1<QuizAnswerEnvelopeV1>` para a
  `GradingExecution` pertencente à `AssessmentSubmission`;
- [ ] validar o schema completo no servidor;
- [ ] impor tempo, tentativas e janela pelo relógio do servidor;
- [ ] criar a superfície e endpoint de `SelfReview`;
- [ ] conectar tarefas e workspace existentes de `PeerReview` ao pipeline,
  transformando submit em evidência do stage canônico;
- [ ] remover do controller, service e actions antigos autoridade direta sobre
  score agregado e notificações;
- [ ] criar review read-only usando `quiz-surface`;
- [ ] exibir estágio, score e feedback conforme a política;
- [ ] consumir separadamente estado de avaliação e estado de liberação;
- [ ] manter release por rodada e preservar a última rodada learner-visible
  durante regrade;
- [ ] tratar reload, retry, dupla submissão e tentativa expirada;
- [ ] persistir outcome idempotente e rejeitar reuso de chave com payload
  divergente;
- [ ] criar/reabrir uma única tentativa por grupo com snapshot dos participantes;
- [ ] sincronizar estado coletivo com concorrência otimista;
- [ ] implementar draft coletivo versionado e submit final único com auditoria
  append-only de cada mutação aceita e do ator;
- [ ] mostrar estado de falha recuperável e evidência peer insuficiente sem
  expor dados operacionais privados;
- [ ] retirar quiz avaliado do caminho genérico `submitActivity`.
- [ ] remover os branches, DTOs, clients e testes antigos específicos de quiz
  no mesmo corte em que o player passa a usar `AssessmentSubmission`.

## Arquivos principais

```text
apps/web/src/app/[locale]/learn/courses/[slug]/activities/[activityId]/page.tsx
apps/web/src/components/learning/learner-activity-form.tsx
apps/web/src/components/courses/learning/activity-component.tsx
apps/web/src/lib/learner/activity-actions.ts
apps/web/src/lib/learner/activity-contracts.ts
packages/features/quiz-surface
apps/api/Source/Modules/GameGuild.Learning.Assessments/Services/AssessmentService.cs
apps/api/Source/Modules/GameGuild.Learning.Assessments/Controllers/AssessmentsController.cs
apps/api/Source/Modules/GameGuild.Learning.Workspaces/Queries/GetLearnerDashboardQuery.cs
apps/api/Source/Modules/GameGuild.Learning.Workspaces/Queries/GetLearnerCourseWorkspaceQuery.cs
apps/api/Source/Modules/GameGuild.Learning.Workspaces/Queries/LearnerWorkspaceMapper.cs
apps/web/src/lib/learner/records.ts
apps/api/Source/Modules/GameGuild.Learning.Courses/Controllers/ProgramContentController.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Controllers/ProgramCrudController.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Controllers/ContentInteractionController.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Controllers/ActivityGradeController.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Services/ProgramWriteService.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Services/ContentInteractionService.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Services/ContentProgressService.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Services/ActivityGradeService.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Extensions/ProgramContentMappingExtensions.cs
```

## Testes de segurança

- aluno não recebe answer key por endpoint específico ou genérico;
- acesso direto, expansão, mapper alternativo e rota pública nunca retornam
  `ProgramContent.JsonBody` autoral para quiz avaliável;
- aluno não acessa tentativa de outro usuário;
- payload não injeta score, feedback ou resultado;
- payload não substitui challenge, variáveis geradas, seed ou ordem congelada;
- IDs inexistentes ou de outra revisão são rejeitados;
- dupla submissão não executa o pipeline duas vezes;
- submit, complete, update progress e grade genéricos de quiz avaliado são
  rejeitados sem criar ou alterar diretamente `ContentInteraction`, progresso
  ou `ActivityGrade`;
- start/reopen concorrente preserva os mesmos bytes e `deliveryHash`, enquanto
  restart cria outra execução sem alterar a anterior;
- dois integrantes iniciando ou enviando simultaneamente continuam produzindo
  uma única tentativa, rodada e finalização;
- replay de save coletivo não duplica `CollectiveAttemptDraftChanged`, e cada
  versão aceita identifica o ator que a produziu;
- replay de save coletivo de self review não duplica versão, outcome ou evento
  append-only;
- integrante fora do snapshot não acessa a tentativa coletiva;
- alteração posterior do grupo não muda o snapshot nem as projeções existentes;
- `SelfReview` individual só aceita o aluno dono da tentativa; no coletivo,
  somente participantes congelados podem editar ou finalizar a evidência única;
- peer reviewer não acessa trabalho próprio, do mesmo grupo ou não atribuído;
- payload de `AIReview` não expõe configuração privada do provider;
- resultado provisório não é retornado como final;
- resultado finalizado, mas ainda não liberado, não expõe nota ou feedback;
- resultado retido não altera `FinalGrade`, percentual, denominador, breakdown
  ou qualquer outro agregado learner capaz de revelar a contribuição interna;
- resultado retido em policy `on-release-and-pass` não altera conclusão,
  progresso, pré-requisito, certificado ou outro sinal learner-visible capaz de
  revelar `Passed`;
- respostas de start/submit, detalhes, `my-submissions`, dashboard, workspace e
  clients gerados aplicam a mesma regra de release;
- regrade preserva a última rodada liberada até o release da nova e não troca
  revisão, manifest, entrega ou respostas da execução;
- evento de finalização não dispara notificação de resultado antes da liberação;
- tentativa iniciada continua usando a mesma revisão após nova publicação;
- unpublish bloqueia novo start, mas não interrompe tentativa já iniciada;
- score em inteiro de unidades faz round-trip sem perda; conversão para decimal
  humano ocorre somente na borda de apresentação.

## Critério de saída

- o player real utiliza somente `AssessmentSubmission` para quizzes avaliados;
- `submitActivity` e os endpoints genéricos não aceitam nem processam quiz
  avaliado;
- nenhuma rota learner/public possui autoridade concorrente para entregar o
  documento autoral;
- cada tentativa usa o mesmo snapshot da definição do início ao resultado;
- o aluno percorre os nove workflows sem caminho paralelo;
- estado, nota e feedback são exibidos sem vazar informação privada;
- finalização acadêmica e liberação ao aluno não são confundidas;
- nenhuma projeção learner permite inferir contribuição retida, e regrade mantém
  histórico e visibilidade por rodada;
- quiz de grupo entrega o mesmo resultado aos participantes sem duplicar o
  pipeline.
