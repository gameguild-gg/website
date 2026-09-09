# 04. Avaliadores e revisão do instrutor

## Objetivo

Implementar os cinco reviews sobre o mesmo lifecycle de estágio e fazer
`InstructorReview` funcionar tanto como review primário quanto como review
final de qualquer resultado não docente.

Cada handler deve ser integrado primeiro ao `AssessmentTestRun`. Depois, o
mesmo orquestrador será chamado por `AssessmentSubmission`, sem implementação
paralela para o aluno.

O registry declara suporte por contexto. Integrar um handler ao
`AssessmentTestRun` registra apenas `AuthorTest`; `OfficialSubmission` só é
declarado depois que autorização, owner oficial, eventos e efeitos acadêmicos
do mesmo handler passarem no E2E. Readiness de teste nunca libera publish.

## Lifecycle comum

Um contrato síncrono `Grade(...) -> result` não representa `PeerReview`,
`SelfReview` ou `InstructorReview`, pois esses métodos precisam aguardar ações
externas. A fronteira comum é um handler de estágio:

```csharp
interface IReviewStageHandler
{
    AssessmentReviewMethod Method { get; }
    Task<ReviewStageTransition> StartAsync(ReviewStageContext context);
    Task<ReviewStageTransition> AcceptEvidenceAsync(
        ReviewStageContext context,
        ReviewEvidence evidence);
    Task<ReviewStageTransition> TryCompleteAsync(ReviewStageContext context);
}
```

Transições possíveis:

```text
Running
AwaitingEvidence
Completed(GradeResultV1)
Failed(retryable | terminal)
```

`StartAsync` pode concluir o estágio imediatamente em `AutomatedReview` ou
enfileirar uma solicitação durável de provider. Reviews humanos normalmente
retornam `AwaitingEvidence`.
Cada endpoint de aluno, peer ou instrutor transforma uma ação autorizada em
`ReviewEvidence`; somente o handler configurado pode aceitá-la.

Algoritmos e integrações ficam atrás de portas genéricas, usadas pelos handlers:

```text
IAssessmentTypeAdapter.EvaluateDeterministicAsync
IAIReviewProvider
IPeerReviewAggregator
```

A avaliação determinística específica pertence ao adapter do tipo de
assessment. Não existe `IDeterministicQuizGrader` no core; quiz pode decompor
sua implementação internamente sem expor essas partes ao grading.

O orquestrador controla ordem, idempotência, rodada e persistência. O handler
controla a validade e a completude das evidências do método. Nenhum deles decide
sozinho se o resultado deve seguir para `InstructorReview`.
O registry do runtime resolve o adapter agregado, o handler e providers pelas
versões exatas do `AssessmentExecutionManifestV1`; capability atual não
autoriza trocar a implementação de uma revisão já preparada.

O orquestrador também não executa uma vez por integrante de grupo. Ele recebe um
`IGradingExecutionContext` para a submission canônica e produz um único
resultado. Resolução de membros acontece antes da criação do contexto; projeção
para enrollments acontece depois de `GradeResultFinalized`.

## `InstructorReview`

Prioridade de entrega: primeira.

- sozinho: submission entra diretamente na fila docente;
- combinado: recebe o resultado primário completo ou parcial, incluindo itens
  ainda pendentes;
- professor aprova sem alteração ou altera por item;
- override exige motivo;
- total é consolidado no servidor;
- regrade posterior preserva histórico e reutiliza a revisão, manifest, entrega
  e respostas originais da execução. Avaliar outra definição cria nova
  submission e nova execução.

O SpeedGrader deve mostrar questão, resposta, revisão, resultado anterior,
rubrica e feedback, não IDs e JSON crus.
Ele produz `InstructorReviewEvidenceV1`, approve, override ou regrade pelos
comandos do runtime. Não edita `AssessmentSubmission.Score` diretamente. O
endpoint `POST submissions/{submissionId}/grade`, `GradeSubmissionAsync` e a
action web anterior são substituídos no mesmo corte do primeiro E2E oficial.

## `AutomatedReview`

É correção determinística pelo sistema:

- usa answer key e regras puras;
- deve ser reproduzível e idempotente;
- roda exclusivamente no limite confiável do servidor;
- TypeScript e C# devem compartilhar fixtures de conformidade;
- avalia cada item deterministicamente suportado;
- marca itens sem avaliador exato como `pending` ou `unsupported`, sem falhar o
  estágio e sem atribuir zero;
- com instrutor, gera resultado primário parcial ou completo para revisão;
- sem instrutor, finaliza apenas quando todos os itens gradeáveis estiverem
  resolvidos; caso contrário, a rodada permanece `AwaitingEvidence` com motivo
  `UnresolvedItems`. Enquanto a política de produto não definir outra
  resolução, esse workflow não possui readiness para publish oficial.

`InstructorReview` recebe tanto os itens pendentes quanto os já avaliados. Ele
deve preencher os pendentes para finalizar e pode sobrescrever qualquer score
determinístico com motivo auditável.

O package `@game-guild/grading` é referência dos contratos, value objects,
agregação e algoritmos independentes do tipo de assessment. Ele não conhece
quiz. `@game-guild/grading-adapter-quiz` implementa a tradução e os algoritmos
determinísticos específicos das questões de quiz usando somente as APIs
públicas dos dois domínios. O browser pode executar preview não oficial por
esse adapter, mas nunca publica a nota; o servidor continua autoritativo e deve
passar pelas mesmas fixtures de conformidade versionadas.

No primeiro algoritmo de crédito parcial, Matching usa a proporção de pares
corretos e Ordering usa a proporção de itens na posição absoluta correta. A
fração é calculada de forma exata e quantizada uma única vez como `ScoreValue`.
Com `allowPartialCredit` falso ou ausente, o resultado permanece tudo-ou-nada.
IDs desconhecidos, duplicados ou cardinalidade excedente tornam a resposta
inválida; itens omitidos contam como incorretos. Qualquer mudança dessa
semântica exige nova versão de algoritmo e novos vetores cross-language.

## `SelfReview`

- o aluno envia score e feedback por contrato específico;
- servidor valida limites, autoria e itens;
- resultado identifica o aluno como autor do review;
- sem instrutor, finaliza;
- com instrutor, entra na fila de revisão.

Autoavaliação não deve reutilizar o payload de respostas nem o endpoint de
correção docente.

Em submission coletiva, o handler recebe uma única evidência de `SelfReview`
por rodada. Qualquer participante do snapshot pode editar o draft com
concorrência otimista, mas somente um submit final versionado fecha a evidência.
`SaveCollectiveSelfReviewDraftV1` e `SubmitCollectiveSelfReviewV1` usam o
envelope idempotente e outcome persistido. Cada mutação aceita grava um evento
append-only com ator, versões, request hash e instante na mesma transação;
replay idêntico não duplica auditoria. O handler não cria uma evidência ou um
resultado por participante.

Registrar primeiro somente a capability `AuthorTest`. `OfficialSubmission`
só é registrada depois que o mesmo handler passar nos E2Es oficiais individual
e coletivo, incluindo autorização, efeitos acadêmicos e corte do caminho antigo.
Esses E2Es usam registro oficial controlado no ambiente de teste; somente sua
aprovação libera o registro na configuração de produção.

## `PeerReview`

Reutilizar a implementação concreta de revisão entre alunos:

1. o aluno precisa enviar o próprio trabalho antes de revisar outro;
2. até completar `PeerReviewsRequiredCount`, ele reivindica uma submissão
   anônima elegível de outro aluno por um claim com lease;
3. o serviço prioriza submissões com menos revisões e exclui trabalho próprio,
   membros do mesmo grupo e submissões já revisadas pelo aluno;
4. o aluno envia feedback obrigatório e score direto ou por rubrica;
5. o autor recebe a revisão sem a identidade do revisor, enquanto o instrutor
   pode consultar essa identidade.

`AssessmentPeerReview` pode continuar persistindo cada claim/evidência
individual se o `SCHEMA-GATE` confirmar ownership e invariantes. Seu score,
quando persistido, usa `ScoreValue` textual e não é a autoridade do resultado
agregado. O handler de `PeerReview` acrescenta:

- limiar de reviews recebidos por submissão, separado da cota do revisor;
- agregação determinística conforme a policy publicada;
- `GradeResultV1` com contribuições individuais auditáveis;
- finalização direta ou transição para `InstructorReview`;
- idempotência quando o último review necessário chega simultaneamente.

Quando a submission-alvo é coletiva, os reviewers continuam sendo alunos
individuais. A distribuição exclui todos os participantes congelados do grupo
alvo, e a agregação das avaliações individuais finaliza uma única execução e
um único resultado do grupo.

Assim como em `SelfReview`, a primeira integração registra somente
`AuthorTest`. A capability `OfficialSubmission` e o publish só são liberados
depois que distribuição, claims, agregação, autorização e efeitos acadêmicos
passarem nos E2Es oficiais individual e coletivo. O registro oficial usado por
esses testes é controlado e não promove a configuração de produção.

### Claims e falta de evidência

O claim atual não pode ser definitivo ao ser atribuído. Definir:

- `ClaimedAt`, `ExpiresAt` e status `Assigned`, `Submitted`, `Expired` ou
  `Released`;
- cota do revisor conta somente reviews `Submitted`, não claims pendentes;
- claim expirado pode ser reatribuído e não bloqueia outro aluno;
- uma constraint impede duas reviews submetidas pelo mesmo par
  reviewer/submission;
- agregação inicia exatamente uma vez ao atingir
  `reviewsRequiredPerSubmission`;
- `minimumReviewsToFinalize` permite distinguir limiar ideal de evidência
  mínima no fechamento da janela.

Se a janela terminar sem evidência mínima, o estágio entra em
`AwaitingInstructorResolution`; ele não publica zero nem espera para sempre. O
instrutor usa comandos autorizados e idempotentes para estender prazo, reatribuir
claims ou finalizar por resolução docente com motivo. A última opção cria
evidência e auditoria próprias, preserva os reviews recebidos e não declara que
o limiar peer foi alcançado nem altera `ReviewMethods` da tentativa.

### Corte da implementação atual

Inventariar `PeerReviewsController`, `IPeerReviewAssignmentService`,
`PeerReviewAssignmentService`, `actions-peer-review.ts`, clients gerados,
workspace, `GradingQueueService`, `TasksService`, actions e painéis do
SpeedGrader, projeções de fila/tarefas e notificações existentes. Preservar
claim, anonimato e superfície de revisão quando úteis, mas transformar o submit
autorizado em `ReviewEvidence` do stage/round canônico. Dependências de
`PeerReviewAssignmentService.CanonicalRow` ou submissions irmãs para grupo
devem ter sido eliminadas no corte da tentativa coletiva e não podem reaparecer.

No mesmo corte E2E:

- remover atribuição direta de score final fora da `GradingExecution`;
- remover agregação ou fan-out por submissions irmãs;
- remover notificações disparadas diretamente pelo service/controller/action;
- emitir efeitos somente pelos eventos canônicos de stage, resultado e release;
- provar por testes de rota e service que não restou segunda autoridade.

## `AIReview`

Entregar a porta de integração, sem provider de IA concreto:

```text
IAIReviewProvider
  Key
  Capabilities
  GetHealthAsync
  ReviewAsync(AIReviewRequestV1) -> AIReviewResponseV1
```

O registry resolve o provider publicado no snapshot. A fronteira valida
resposta estruturada, score, limites, evidência e versão antes de produzir
`GradeResultV1`.

Requisitos mínimos:

- provider e policy identificados por chaves estáveis;
- segredo e prompt privado permanecem no servidor;
- timeout, cancelamento, retry e idempotência;
- ausência ou falha do provider nunca vira nota zero;
- provider ausente ou incompatível bloqueia publish;
- provider registrado e temporariamente indisponível mantém execução pendente
  para retry, sem invalidar a revisão publicada;
- custo e observabilidade podem ser anexados quando o provider os oferecer;
- provider controlado existe somente em testes de contrato.

A chamada externa não acontece na transação do submit nem diretamente em
`StartAsync`. O handler persiste o estágio e uma mensagem `AIReviewRequested`
com `requestId` estável. Um worker consome a outbox, chama o provider e grava a
resposta em uma inbox idempotente antes de entregá-la como `ReviewEvidence`.
Timeouts e retries criam tentativas operacionais, não novos estágios ou novas
rodadas.

O avaliador determinístico deve ser usado para itens que possuem regra exata;
IA não deve substituir uma correção mais reproduzível sem motivo.

## Orquestrador

Responsabilidades:

1. carregar snapshot e workflow da tentativa;
2. autorizar o ator ou serviço;
3. adquirir lock/idempotency key;
4. criar ou retomar a rodada e iniciar o estágio correto;
5. encaminhar evidências ao handler configurado;
6. validar e persistir a transição e o resultado;
7. avançar para `InstructorReview`, aguardar resolução de itens ou finalizar;
8. concluir a `GradingExecution` por uma transição interna neutra;
9. em `OfficialSubmission`, persistir a rodada oficial e gravar
   `GradeResultFinalized` na mesma transação;
10. ainda em `OfficialSubmission`, aplicar separadamente a policy de liberação
    por `ReleaseGradeResult` e emitir `GradeResultReleased` quando o aluno puder
    ver o resultado;
11. em `AuthorTest`, persistir somente o resultado diagnóstico, sem
    `GradeResultFinalized`, estado de release ou evento acadêmico.

Não usar uma cadeia de `if` espalhada por controllers. Criar uma fronteira de
aplicação explícita para resolução e execução de estágio.

## Tarefas

- [ ] fechar `IReviewStageHandler`, transições e registry;
- [ ] resolver handlers, projectors, geradores de entrega,
  decoders/normalizadores e algoritmos pelas versões exatas do manifest da
  revisão;
- [ ] separar handlers de avaliadores/providers internos;
- [ ] implementar orquestrador e transições idempotentes;
- [ ] concluir primeiro o fluxo `InstructorReview` isolado;
- [ ] implementar `AutomatedReview` com fixtures cross-language;
- [ ] representar cobertura parcial e itens pendentes sem falha de estágio;
- [ ] implementar `SelfReview` com autorização do aluno;
- [ ] adaptar claims e workspace de `PeerReview` à evidência do stage e ao
  resultado oficial;
- [ ] remover score, agregação, fan-out e notificação autoritativos do caminho
  antigo no mesmo corte E2E;
- [ ] implementar lease, expiração, reatribuição e resolução de evidência
  insuficiente para peer claims;
- [ ] criar interface, registry e contract tests de `AIReview`;
- [ ] executar `AIReview` por request/outbox e response/inbox idempotentes;
- [ ] implementar as quatro combinações com `InstructorReview` obrigatoriamente
  posterior ao resultado primário;
- [ ] permitir approve, override e regrade com motivo;
- [ ] adaptar SpeedGrader e fila mínima ao novo runtime e remover endpoint,
  service e action que atribuem score diretamente;
- [ ] impedir execução por ator ou método não configurado;
- [ ] criar filas e retries sem duplicar efeitos;
- [ ] garantir idempotência por submission coletiva, nunca por participante;
- [ ] implementar evidência única de `SelfReview` coletivo e excluir o grupo
  alvo inteiro da distribuição de `PeerReview`;
- [ ] registrar `AuthorTest` antes e `OfficialSubmission` somente depois dos
  E2Es oficiais individual e coletivo de `SelfReview` e `PeerReview`;
- [ ] adaptar filas, tarefas e painéis para projeções canônicas, sem
  `CanonicalRow` ou submissions irmãs;

## Testes por método

- resultado feliz com e sem instrutor;
- erro recuperável e erro definitivo;
- retry idempotente;
- método interativo permanece em `AwaitingEvidence` até a evidência válida;
- edição posterior da definição não altera a tentativa;
- score agregado equivale aos itens;
- resultado parcial automatizado preserva itens avaliados e pendentes;
- item sem regra determinística não gera falha nem score zero;
- ator sem permissão é rejeitado;
- conclusão fora de ordem é rejeitada;
- override e regrade preservam o resultado anterior;
- claim expirado não consome cota nem bloqueia reatribuição;
- peers insuficientes entram em `AwaitingInstructorResolution`; extensão,
  reatribuição e resolução docente são auditáveis e nenhuma delas fabrica
  conclusão peer ou score sintético;
- test run concluído não emite `GradeResultFinalized`, não cria release e não
  aciona consumer acadêmico;
- self review coletivo finaliza uma única evidência, e peer review coletivo
  agrega em um único resultado sem reviewers do grupo-alvo.
- replay de draft coletivo de self review não duplica versão nem auditoria;
- capability de test run nunca promove automaticamente capability oficial.

## Critério de saída

- os cinco métodos percorrem o mesmo lifecycle e produzem o mesmo contrato de
  resultado quando completos;
- `InstructorReview`, `AutomatedReview`, `SelfReview` e `PeerReview` chegam a um
  estado final coerente;
- `AIReview` executa no test run com capability `AuthorTest`, bloqueia
  publicação sem `OfficialSubmission` e aguarda retry sob indisponibilidade
  transitória;
- os nove workflows obedecem à mesma ordem canônica;
- `InstructorReview` combinado sempre ocorre por último;
- finalização acadêmica e liberação ao aluno são transições independentes;
- nenhuma nota oficial depende de cálculo confiado ao browser.
