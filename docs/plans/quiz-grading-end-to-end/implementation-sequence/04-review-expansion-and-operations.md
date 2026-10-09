# Parte 4. Expansão de reviews e operação

Status: não iniciada; gate de entrada `04-0` aprovado em 2026-10-08.

## Objetivo

Expandir o E2E principal com `SelfReview`, `PeerReview`, a porta durável de
`AIReview` e a operação acadêmica avançada. Esta parte contém a preparação
`SEQ-12A` e os marcos `SEQ-12` a `SEQ-16`. Ela preserva os invariantes de
domínio, ownership, autorização, rounds, release e gradebook aprovados nas
partes anteriores, mas começa reconciliando a porta interna de stages com o
lifecycle assíncrono já especificado para reviews humanos e externos. Essa
evolução é única e genérica; não cria runtimes por método. A parte usa as
personas e capacidades contextuais comprovadas na Parte 3.

Regras globais: [`08-implementation-sequence.md`](../08-implementation-sequence.md).

## Pré-requisitos

- Parte 1 concluída, testada e aprovada;
- primeira implementação da Parte 2 concluída e o plano de
  [`fechamento da Parte 2`](./02a-core-grading-e2e-closeout.md) integralmente
  aprovado;
- caminho crítico da
  [`Parte 3`](./03-contextual-access-and-personas.md) concluído, com
  `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` aprovados; a delegação operacional de
  equipe em `ACCESS-03B` não bloqueia esta parte;
- fluxo oficial individual e coletivo sem autoridade paralela;
- gradebook mínimo, release e auditoria básica funcionando de forma
  idempotente.

## Gate de entrada `04-0`

Gate aprovado em 2026-10-08. `CLOSE-01` a `CLOSE-04`, `ACCESS-03A`,
`ACCESS-04` e `ACCESS-05` estão concluídos. A delegação operacional
`ACCESS-03B` permanece externa e não bloqueante. A aprovação libera somente
`SEQ-12A`; ela não autoriza schema, migration, capability oficial nova nem
provider de produção.

A aprovação está sustentada pelas seguintes evidências:

- ausência de submit, score, agregação ou notificação peer autoritativos fora da
  `GradingExecution`;
- ausência de `CanonicalRow` e submissions irmãs como representação de tentativa
  coletiva;
- E2Es oficiais individual e coletivo via HTTP + PostgreSQL;
- criação limpa e upgrade populado pela cadeia real, sem editar migrations
  históricas;
- owner, ao menos dois learners e outsider comprovados por sessões e
  autorização contextuais, sem role de produto ou bypass administrativo;
- `StaffReview` restrito à equipe e ausente da autorização contextual de
  `SelfReview` e `PeerReview`;
- suíte acumulada aprovada.

As evidências concretas do gate, incluindo os E2Es PostgreSQL com sessões reais
e a suíte acumulada, permanecem registradas na
[`Parte 3`](./03-contextual-access-and-personas.md#evidências-executadas).

## Execução e gates de persistência

Executar um marco por vez. Cada marco encerra contrato, API, surface e testes
aplicáveis antes do seguinte; esta parte não deve ser implementada como um único
corte. Todo `SCHEMA-GATE` interrompe a execução e exige apresentação e aprovação
explícita antes de editar entidades EF, configurações, migrations, snapshot ou
tabelas.

O estado atual já permite classificar os gates:

- `SEQ-12A`: nenhuma mudança relacional é prevista. Se a evolução do lifecycle
  ou do test run multipersona demonstrar necessidade de schema, interromper e
  apresentar o delta;
- `SEQ-12`: gate realmente condicional. `ReviewEvidence` e
  `GradingCommandReceipt` podem permitir um event stream append-only de draft,
  mas isso deve ser provado sob concorrência antes de concluir delta zero;
- `SEQ-13`: existe delta relacional esperado. `AssessmentPeerReview` atual não
  representa lease, expiração, reatribuição, ownership pelo stage/round nem os
  estados operacionais exigidos. O desenho exato continua não aprovado;
- `SEQ-14`: gate condicional. O módulo possui outbox acadêmica e receipts, mas
  não pode importar o inbox do host `GameGuild.API`;
- `SEQ-15`: existe delta relacional esperado para agendamento por rodada.
  `Assessment.ResultReleaseScheduledFor` guarda a policy autoral, enquanto
  `GradeResultRelease` representa apenas uma liberação final append-only. O
  desenho exato continua não aprovado.

Nenhuma das duas propostas esperadas autoriza antecipadamente uma migration.
Cada uma deve listar ownership, lifecycle, campos, constraints, índices,
retenção, concorrência, alternativa rejeitada e impacto no catálogo PostgreSQL.

## Fora do escopo

- reabrir decisões centrais sem novo ADR e avaliação de impacto;
- substituir o runtime entregue na Parte 2 por implementações por método;
- disponibilizar `AIReview` em produção sem provider real aprovado.

## `SEQ-12A`. Generalizar stages e test run

### Resultado

Todos os métodos usam a mesma máquina de estados para iniciar, aguardar
evidência, aceitar evidência, retomar e concluir. O test run representa um ou
vários sujeitos sem enrollment, submission oficial ou efeitos acadêmicos. Essa
fundação é concluída antes de implementar `SelfReview`.

### Lifecycle genérico

- evoluir `IReviewStageHandler` do contrato exclusivamente síncrono
  `ExecuteAsync -> GradeResultV1` para operações equivalentes a `StartAsync`,
  `AcceptEvidenceAsync` e `TryCompleteAsync`, com uma transição versionada que
  possa representar conclusão, espera por evidência, espera por resolução
  docente e falha retryable ou terminal;
- manter o orquestrador responsável por ordem, rounds, idempotência,
  persistência e avanço entre stages. Ele reage à transição retornada e não
  contém branches por `ReviewMethod`;
- manter cada handler responsável por validar a evidência e decidir quando seu
  método está completo;
- adaptar `AutomatedReview` para normalmente concluir em `StartAsync` e
  `InstructorReview` para aguardar e aceitar evidência docente pelo mesmo
  lifecycle;
- preservar os casos de uso autenticados e idempotentes atuais de resolução
  docente, fazendo-os entregar evidência ao dispatcher genérico em vez de
  chamar uma mutação exclusiva do orquestrador;
- resolver handler, key, versão, provider e contexto exclusivamente pelo
  manifest congelado. Capability atual nunca troca o handler de uma execução já
  criada;
- não criar um service, orquestrador ou tabela por método.

### Test run multipersona

- ampliar os contratos e views de test run para uma coleção explícita de
  subjects e execuções; o caso de uma persona continua sendo a mesma estrutura
  com um elemento;
- criar deterministicamente as personas sintéticas exigidas pela policy e
  materializar uma entrega imutável por subject;
- manter respostas, evidências, claims e resultados vinculados à execução do
  subject correto, sem payload global que duplique essas relações;
- concluir o `AssessmentTestRun` somente quando todas as execuções obrigatórias
  estiverem terminais; a conclusão de uma execução não pode concluir o run
  multipersona inteiro;
- manter o instrutor como ator autenticado do test run e as personas como
  sujeitos simulados. Não criar enrollment, `AssessmentSubmission`, gradebook,
  release, progresso, notificação ou passback;
- atualizar restart, reload/resume e cancelamento para operar sobre o conjunto
  completo de subjects e execuções.

### Regra de regrade para os métodos adicionais

- `AutomatedReview` e `AIReview` executam novamente usando revisão, manifest,
  entrega, resposta e versões já congelados;
- `SelfReview` e `PeerReview` reutilizam por referência as evidências humanas
  finalizadas da rodada substituída. A nova rodada registra explicitamente a
  origem e o hash dessas evidências, sem copiá-las como nova ação humana e sem
  convocar novamente aluno ou peers;
- `InstructorReview` sempre exige uma nova resolução docente para a nova rodada,
  podendo apresentar o resultado anterior como contexto somente leitura;
- solicitar uma nova coleta de self ou peer review é outro caso de uso,
  explicitamente fora deste corte. Um regrade comum nunca o faz implicitamente;
- os `SCHEMA-GATE`s de `SEQ-12` e `SEQ-13` devem provar que referências
  cross-round são íntegras e auditáveis ou apresentar o delta necessário.

### Gate

- os E2Es atuais de `AutomatedReview`, `InstructorReview` e da combinação entre
  ambos continuam aprovados em `AuthorTest` e `OfficialSubmission`;
- o orquestrador não possui branch por método para iniciar ou aceitar evidência;
- resolução docente passa pelo contrato genérico sem perder autorização,
  idempotência ou auditoria;
- test run com uma persona preserva o comportamento atual;
- test run com múltiplos subjects não conclui prematuramente e retoma cada
  execução correta após reload;
- regrade não solicita silenciosamente nova evidência humana e preserva a
  proveniência da evidência reutilizada;
- nenhuma mudança de schema é realizada sem um novo gate explícito.

## `SEQ-12`. Completar `SelfReview`

### Resultado

`SelfReview` é validado no test run e em submissions oficiais individuais e
coletivas. Somente ao final deste marco ele é considerado workflow completo.

### `SCHEMA-GATE` de SelfReview

Antes de alterar persistência, provar se o modelo genérico de evidências já
garante:

- uma evidência por execução, round e método;
- draft compartilhado com versão de concorrência para sujeito coletivo;
- um único submit final atômico;
- trilha append-only de cada mutação aceita do draft com ator, versões
  anterior e nova, request hash e instante;
- deduplicação durável de save e submit por escopo, idempotency key e request
  hash, com outcome persistido e sem evento duplicado em replay;
- ator, instante e versão registrados na finalização;
- imutabilidade da evidência finalizada;
- referência íntegra e auditável à evidência finalizada da rodada anterior
  quando um regrade reutilizá-la.

Se o núcleo atender integralmente, registrar o gate com delta relacional zero.
Se não atender, apresentar tabelas, colunas, constraints e índices necessários
e obter aprovação antes de criar uma migration incremental forward-only. Provar
criação limpa, upgrade de banco populado pela migration anterior e preservação
de dados e artefatos SQL ativos.

### Implementação

- criar contrato próprio de autoavaliação por item;
- criar um comando idempotente de submit individual e manter os comandos
  versionados de save/submit coletivo; ambos entregam evidência ao dispatcher
  genérico concluído em `SEQ-12A`;
- validar ator, sujeito, escala, limites e momento de envio;
- manter resposta acadêmica e evidência de self review separadas;
- produzir resultado direto ou encaminhar ao instrutor;
- representar a persona no test run sem confundi-la com o ator autenticado;
- registrar inicialmente somente a capability `AuthorTest` e concluir os E2Es
  de test run direto e combinado com instrutor;
- conectar o mesmo handler à tentativa oficial individual e coletiva; somente
  depois de autorização, efeitos acadêmicos e ambos os E2Es oficiais passarem,
  registrar `OfficialSubmission` na configuração de produção e permitir
  publish. Os E2Es usam registro oficial controlado no ambiente de teste, sem
  antecipar essa promoção;
- para grupo, manter uma única evidência compartilhada por round;
- permitir que qualquer participante do snapshot edite o draft coletivo com
  concorrência otimista;
- implementar `SaveCollectiveSelfReviewDraftV1(expectedVersion,
  idempotencyKey, requestHash)` sobre `IdempotentCommandEnvelopeV1`; cada save
  aceito grava na mesma transação um evento append-only e replay idêntico
  reutiliza o outcome sem duplicá-lo;
- aceitar um único submit final da evidência coletiva e registrar o ator real;
- estender a surface compartilhada de review para `SelfReview`, reutilizando o
  mesmo componente nos contextos `AuthorTest` e `OfficialSubmission`, sem criar
  um runtime ou formulário divergente por contexto;
- implementar na surface carregamento e resume da evidência individual ou
  coletiva, save versionado, conflito por `expectedVersion`, retry idempotente,
  submit final e estado somente leitura depois da finalização;
- no contexto coletivo, mostrar o estado compartilhado mais recente e atribuir
  cada mutação ao participante autenticado, sem apresentar uma evidência por
  integrante;
- em regrade, referenciar a evidência finalizada da rodada substituída e
  recalcular o resultado do novo stage sem solicitar outra ação do aluno;
- aplicar release e gradebook já entregues aos dois tipos de sujeito;
- executar o fluxo oficial individual com a sessão learner comprovada em
  `ACCESS-05`; no coletivo, cada mutação usa a sessão do participante real e o
  snapshot de participantes da submission única;

### Gate

- somente o sujeito individual ou participante congelado do grupo pode realizar
  o self review;
- score não pode exceder limites nem entrar no answer payload;
- submissions coletivas preservam uma evidência e um resultado, não um por
  integrante;
- dois participantes não finalizam evidências coletivas concorrentes;
- saves concorrentes ou repetidos não perdem versões nem duplicam auditoria;
- capability `AuthorTest` não autoriza publish; `OfficialSubmission` só existe
  depois dos E2Es oficiais individual e coletivo;
- direto e combinado com instrutor passam no test run e nos E2Es individual e
  coletivo;
- regrade individual e coletivo reutiliza a evidência humana correta, preserva
  sua autoria e não cria um segundo submit do aluno;
- testes de interface cobrem reload/resume, conflito de versão, retry, submit
  final e bloqueio de edição posterior nos contextos de teste e oficial;
- não existe implementação paralela específica da UI ou do runtime.

## `SEQ-13`. Completar `PeerReview`

### Resultado

As revisões entre alunos produzem um resultado agregado exatamente uma vez no
test run e na distribuição acadêmica real, inclusive quando o alvo é uma
submission coletiva.

### `SCHEMA-GATE` de peer

Antes de alterar a persistência, inventariar explicitamente
`PeerReviewsController`, `IPeerReviewAssignmentService`,
`PeerReviewAssignmentService`, `actions-peer-review.ts`, o workspace atual, os
clients gerados, `GradingQueueService`, `TasksService`, actions e painéis do
SpeedGrader, projeções de tarefas/fila e os produtores de notificação. Incluir
também `GameGuild.Learning.Courses.IPeerReviewService` e seus modelos no
inventário: ausência de implementação ou uso ativo deve ser documentada, não
presumida como autorização para remoção.
Consumir a evidência de `CLOSE-01` de que dependências de `CanonicalRow`,
submissions irmãs e submit peer autoritativo anterior foram eliminadas ou
tornadas fail-closed. Se qualquer uma reaparecer, interromper `SEQ-13` e retornar
ao fechamento da Parte 2. Com essa base comprovada, apresentar uma proposta
relacional obrigatória para:

- lease, expiração, reatribuição e idempotência de claims;
- ownership canônico do claim pelo `ReviewStage`, derivando round, execução e
  alvo sem duplicar IDs capazes de divergir;
- identidade exclusiva do revisor real em `OfficialSubmission` ou da persona
  sintética em `AuthorTest`, sem confundir sujeito e ator autenticado;
- cota do revisor separada do limiar recebido pela submission;
- evidências e agregação versionada;
- preservação de `AssessmentPeerReview.Score` como unidades inteiras de
  `ScoreValue`, na escala `100` já normalizada em `SEQ-03`, caso a entidade
  continue armazenando a contribuição individual;
- anonimato na projeção e identidade preservada para auditoria;
- exclusão de todos os participantes quando o alvo for coletivo;
- transição durável `AwaitingInstructorResolution` quando o prazo encerrar sem o
  mínimo de evidências, além dos comandos idempotentes de extensão,
  reatribuição e resolução docente final. Se stages/evidências genéricos não
  representarem isso sem ambiguidade, apresentar o delta relacional neste gate.

O estado atual de `AssessmentPeerReview`, limitado a `Assigned`/`Submitted` e
sem lease ou vínculo com stage/round, não satisfaz esse contrato. O gate deve
decidir explicitamente entre evoluir essa entidade ou introduzir um owner
canônico novo. Essa constatação não pré-aprova nenhuma das alternativas.

Reutilizar tabelas atuais quando possuírem ownership e invariantes corretos.
`AssessmentPeerReview` pode permanecer como registro individual de claim e
evidência, mas não como segunda autoridade do resultado agregado.
Qualquer mudança aprovada entra em nova migration incremental e preserva a
cadeia histórica. Remover uma estrutura existente exige prova e aprovação
específicas, não apenas a classificação de que ela é antiga.

### Implementação

- adaptar a atribuição, claim e workspace anônimos existentes ao stage/round
  canônico, preservando a UX útil sem preservar autoridade paralela;
- impedir que aluno revise a própria submission ou uma submission de grupo do
  qual participe;
- manter revisores individuais mesmo quando o alvo for coletivo;
- adicionar lease, expiração, reatribuição e idempotência;
- definir mínimo de evidência, prazo e comportamento terminal de insuficiência;
- quando o prazo encerrar sem o mínimo, mover o stage para
  `AwaitingInstructorResolution`, sem zero e sem marcar `PeerReview` como
  concluído;
- implementar comandos autorizados e idempotentes para o instrutor estender a
  janela, reatribuir claims ou finalizar por resolução docente. A finalização
  cria evidência docente e evento de auditoria próprios, registra que o limiar
  peer não foi alcançado e não altera retroativamente `ReviewMethods`;
- transformar cada submit autorizado do workspace em `ReviewEvidence` da
  `GradingExecution`, entregue pelo dispatcher genérico; somente o handler de
  `PeerReview` aceita, agrega e conclui o stage;
- agregar resultados por submission conforme policy versionada;
- encaminhar opcionalmente o agregado para `InstructorReview`;
- exercitar múltiplas personas no test run;
- usar o agregado multipersona de `SEQ-12A`; não criar um payload ou runtime
  paralelo exclusivo de peer;
- registrar inicialmente somente `AuthorTest` e concluir os fluxos direto e
  combinado no test run multipersona;
- conectar claims, distribuição e agregação às submissions oficiais individuais
  e coletivas; somente após esses E2Es, a autorização e os efeitos acadêmicos
  passarem, registrar `OfficialSubmission` na configuração de produção e
  permitir publish. Os E2Es usam registro oficial controlado no ambiente de
  teste, sem antecipar essa promoção;
- remover no mesmo corte qualquer atribuição direta de score final,
  agregação/fan-out por submissions irmãs e notificação emitida diretamente por
  `PeerReviewAssignmentService`, controller ou action web;
- persistir na outbox os eventos canônicos de stage, resultado e release
  necessários aos efeitos posteriores; notificações externas e passback
  permanecem desligados até os consumers de `SEQ-15`;
- remover ou adaptar, no mesmo E2E, rotas e métodos antigos que permitam
  concluir peer review fora da `GradingExecution`;
- em regrade, reutilizar por referência claims submetidos e evidências
  finalizadas da rodada substituída, recalcular a agregação no novo stage e não
  distribuir novos claims implicitamente;
- executar os E2Es oficiais com ao menos dois learners matriculados em sessões
  distintas; o peer é contextual ao curso e nunca uma role de tenant;

### Gate

- claim expirado não consome cota e pode ser reatribuído;
- concorrência não duplica review, agregação ou finalização;
- anonimato externo e identidade auditável coexistem;
- nenhum integrante do grupo-alvo recebe claim sobre a própria submission;
- direto e combinado com instrutor passam no test run e nos E2Es individual e
  coletivo;
- falta de evidência nunca gera zero nem espera indefinidamente depois do prazo:
  entra em `AwaitingInstructorResolution` e somente um dos comandos explícitos
  de extensão, reatribuição ou resolução docente altera esse estado;
- resolução docente de insuficiência preserva claims/evidências recebidos,
  motivo, ator e antes/depois, sem declarar falsamente conclusão peer;
- regrade preserva a proveniência das evidências peer reutilizadas e não exige
  nova participação dos revisores;
- busca, testes de rota e testes de service comprovam que submit antigo não
  atribui score nem dispara notificação direta; existe uma única autoridade
  para evidência individual, agregação e resultado;
- filas, tarefas e painéis consomem projeções do stage/round canônico, sem
  restaurar `CanonicalRow`, submissions irmãs ou segunda autoridade;
- capability `AuthorTest` não autoriza publish; `OfficialSubmission` só existe
  em produção depois dos E2Es oficiais individual e coletivo.

## `SEQ-14`. Entregar a porta de `AIReview`

### Resultado

Um provider pode ser conectado sem alterar o orquestrador. Esta etapa entrega a
porta e sua durabilidade, não uma IA concreta de produção.

### `SCHEMA-GATE` condicional de AI

Primeiro provar se outbox, inbox e deduplicação do núcleo atendem ao contrato.
O inbox de transporte existente em `GameGuild.API` pertence ao host e não pode
ser importado por `GameGuild.Learning.Assessments`. A proposta deve preservar a
direção de dependência: Learning expõe portas e comandos genéricos; o host
compõe o adapter do provider e entrega respostas ao caso de uso idempotente de
Learning. Deduplicação autoritativa da resposta fica em receipts já adequados ou
em persistência do próprio módulo aprovada neste gate.
Somente se não atenderem, apresentar mudanças para:

- request estável e correlação com stage/round;
- resposta deduplicada e evidência do provider;
- timeout, retry e estado pendente;
- identidade e versão do modelo/provider.

Mudanças aprovadas entram em migration incremental forward-only, com criação
limpa e upgrade populado testados.

### Implementação

- criar interface, registry específico, capability descriptor e configuração
  de provider sobre `IReviewCapabilityRegistry`;
- manter `IAIReviewProvider`, requests, responses e transições no módulo de
  Assessments; integrações concretas e credenciais ficam em adapters compostos
  pelo host, sem referência do módulo para `GameGuild.API`;
- versionar request, response, evidência e identidade do modelo/provider;
- persistir stage e `AIReviewRequested` antes de qualquer chamada externa;
- despachar por outbox e receber por inbox deduplicada;
- entregar a resposta deduplicada ao lifecycle genérico de `SEQ-12A`, sem
  branch de `AIReview` no orquestrador;
- aplicar timeout, retry e estado pendente sem score de fallback;
- bloquear publish sem provider compatível registrado;
- usar provider controlado somente nos contract tests e test runs;
- provar que o handler permanece indiferente ao sujeito individual ou coletivo;
- em regrade, gerar uma nova solicitação com correlação ao novo stage, mantendo
  provider e policy fixados pelo manifest;
- manter produção indisponível até existir provider real configurado.

### Gate

- nenhuma chamada externa ocorre dentro da transação de submit;
- resposta duplicada não duplica evidência nem resultado;
- testes de arquitetura comprovam ausência de dependência
  `GameGuild.Learning.Assessments -> GameGuild.API` ou para provider concreto;
- indisponibilidade transitória mantém a execução pendente;
- ausência de provider bloqueia publish e nunca produz nota;
- provider controlado prova os fluxos direto e seguido por instrutor;
- a UI não afirma que `AIReview` está disponível em produção sem provider real.

## `SEQ-15`. Completar release, gradebook e operação

### Resultado

O E2E acadêmico já existe. Este marco acrescenta políticas e consumidores
operacionais avançados sem redefinir resultado, tentativa ou workflow.

### `SCHEMA-GATE` de operação

`Program.PassingScore` e os demais campos acadêmicos existentes já foram
convertidos em `SEQ-03`. Para consumers, filas, métricas e projeções, usar outbox
e estruturas existentes primeiro e somente propor persistência nova quando
consulta, retenção ou idempotência operacional não puderem ser atendidas
corretamente. O agendamento por rodada possui o delta esperado descrito abaixo.
Toda proposta exige aprovação e migration incremental própria; componentes sem
delta registram essa conclusão separadamente.

Para release agendado, o gate parte do estado já comprovado:

- `Assessment.ResultReleaseScheduledFor` congela a policy autoral no snapshot;
- `GradeResultRelease` é o registro final append-only e aceita somente
  `Released`;
- ainda não existe owner persistente do agendamento de uma rodada.

Portanto, apresentar uma proposta relacional obrigatória para o schedule por
`GradeRoundId`, com `ScheduledFor` em UTC, estado operacional, versão de
concorrência e índices eficientes para vencimento e retry. A proposta deve
manter `GradeResultRelease` como fato final append-only, preferindo um owner de
agendamento separado em vez de transformar ausência de release, agendamento e
release final em uma única linha mutável. Não adicionar
`AssessmentSubmissionId` redundante; a submission continua derivada pela
execução da rodada.

### Implementação

- habilitar o modo `scheduled` já reservado no contrato versionado de
  `AssessmentResultReleasePolicyV1`, sem alterar retrospectivamente o schema
  de policies de revisões publicadas;
- implementar `ScheduleGradeResultRelease` e
  `CancelScheduledGradeResultRelease` com ator, permissão, rodada esperada,
  versão de concorrência, idempotency key, motivo e auditoria. Reagendamento é
  uma nova execução idempotente de schedule sobre a versão esperada;
- depois da aprovação do gate, persistir `ScheduledFor` em UTC no owner de
  schedule e usar `TimeProvider` injetável. O worker busca schedules vencidos
  por índice, mas executa o mesmo `ReleaseGradeResult` com identidade de serviço
  autorizada; nunca cria ou altera `GradeResultRelease` diretamente;
- quando a rodada finalizar depois do horário configurado, a policy solicita
  release imediato pelo mesmo comando. Quando finalizar antes, cria o estado
  `Scheduled`. Cada nova rodada de regrade reaplica a policy sem alterar o
  agendamento ou a liberação das rodadas anteriores;
- cancelar um agendamento deixa a rodada derivadamente `Withheld` e preserva o
  histórico auditável do schedule; não cria release e não retira resultado já
  liberado. Retirada de release continua sendo outro caso de uso explicitamente
  fora deste corte;
- ampliar, se necessário, as policies além da seleção mínima entregue em
  `SEQ-10`; uma eventual média deve ser modelada como agregação explícita,
  mantendo uma única contribuição canônica;
- integrar `Program.PassingScore` em unidades inteiras de `PercentValue` e as
  projeções globais já normalizadas à consolidação do curso, sem conversão
  tardia ou cast para tipos fracionários;
- manter projeções agregadas precomputadas sem aritmética decimal em SQL;
- construir filas docentes por estado de review;
- autorizar filas e operações docentes pela capability `StaffReview` aprovada
  na Parte 3; edição ou publicação continuam exigindo suas próprias
  capabilities;
- implementar os consumers de notificação e passback, que permaneceram
  deliberadamente desligados na Parte 2, consumindo somente os eventos
  canônicos adequados e nunca comandos ou services de grading diretamente;
- registrar cada consumer com `ConsumerKey` estável e deduplicar sua entrega por
  `(EventId, ConsumerKey)`. Falha de notificação não reabre gradebook, progresso
  ou auditoria já confirmados; a mensagem encerra somente depois de todas as
  entregas obrigatórias capturadas confirmarem;
- não reproduzir eventos anteriores apenas porque um consumer foi habilitado.
  Qualquer replay histórico é uma operação explícita, autorizada e auditada;
- adicionar métricas, alertas, retry operacional e inspeção de falhas;
- garantir que `GradeResultFinalized` alimente projeções acadêmicas e auditoria;
- garantir que `GradeResultReleased` alimente aluno e notificações;
- testar reprocessamento e reconstrução idempotente das projeções.

### Gate

- resultado pode contribuir no gradebook enquanto ainda está retido do aluno;
- atividade de peso zero pode liberar nota e feedback sem contribuir no total;
- schedule, cancelamento, reagendamento e worker são idempotentes, respeitam
  versão/rodada esperadas e geram exatamente um `GradeResultReleased`;
- relógio avançado em teste libera somente rodadas vencidas; resultado
  finalizado após o horário é liberado sem permanecer preso em `Scheduled`;
- regrade agenda e libera cada rodada independentemente, preservando a última
  rodada learner-visible até o release da substituta;
- retry não duplica projeção, notificação ou passback;
- falhas independentes preservam os receipts dos consumers já concluídos e
  deixam pendentes somente as entregas ainda não confirmadas;
- nenhuma notificação ou passback nasce de `GradeSubmissionAsync`,
  `ActivityGrade`, `ContentInteraction` ou outro produtor removido;
- auditoria reconstrói round, atores, overrides e release;
- nenhum consumidor executa review ou recalcula resultado oficial.

Referência: [`06`](../06-gradebook-audit-and-operations.md).

## `SEQ-16`. Auditar e fechar o E2E

### Resultado

O fluxo já foi substituído e limpo por fatia. O último marco confirma que não
restaram autoridades concorrentes, referências obsoletas ou lacunas na matriz.

### Implementação

- procurar contratos descartados, nomes `*Graded` e código inalcançável;
- confirmar que rotas de aluno não expõem JSON autoral;
- confirmar que packages e UI não produzem score oficial;
- confirmar ausência de fan-out de grading por integrante;
- atualizar mapas de serialização e documentação arquitetural;
- executar a matriz dos nove workflows no test run;
- executar todos os workflows oficialmente implementados na jornada de aluno;
- repetir a matriz contextual obrigatória de owner, ao menos dois learners e
  outsider com sessões separadas;
- quando `ACCESS-03B` estiver disponível, repetir adicionalmente os cenários de
  collaborator e reviewer da equipe sem torná-los autoridade de `SelfReview`
  ou `PeerReview`;
- confirmar bloqueio de `AIReview` sem provider real;
- confirmar bloqueio de automated-only parcial enquanto a decisão de produto
  permanecer pendente;
- remover qualquer resíduo encontrado antes de concluir o marco.

### Gate final

- todos os itens da definição global de pronto do
  [`README`](../README.md#definição-global-de-pronto) estão satisfeitos;
- não existe caminho paralelo que gere score oficial;
- a cadeia completa cria o schema final em banco vazio;
- o upgrade de um banco populado pela migration anterior preserva dados,
  constraints e artefatos SQL ativos;
- projetos, packages e superfícies web pertencentes ao fluxo de grading passam
  em CI. Falhas de módulos externos são registradas para seus owners e não
  bloqueiam este gate;
- observabilidade distingue falha técnica, espera legítima e revisão humana.

## Definição de pronto da Parte 4

- o gate de `SEQ-12A` e todos os gates de `SEQ-12` a `SEQ-16` estão satisfeitos;
- todos os métodos usam o mesmo lifecycle de stage e o test run multipersona
  não possui efeitos acadêmicos;
- reviews adicionais reutilizam o mesmo orquestrador, autorização, rounds,
  evidências, resultado e release do E2E principal;
- `SelfReview` e `PeerReview` passam nos contextos de teste e oficial
  aplicáveis, inclusive para sujeito coletivo;
- insuficiência peer possui transição e resolução docente terminal, sem espera
  infinita, zero sintético ou falsa conclusão do método;
- `AIReview` possui porta durável e permanece indisponível em produção sem
  provider real;
- score global, release avançado, filas, notificações e passback consomem
  eventos canônicos sem recalcular grading;
- fan-out de eventos mantém confirmação durável e retry independente por
  consumer obrigatório;
- auditoria, observabilidade, mapas de serialização e matriz E2E estão
  completos;
- toda a suíte de grading acumulada das Partes 1, 2, 3 e 4 passa em CI com banco
  criado do zero e diff global sem drift depois de cada `SCHEMA-GATE`.

## Acompanhamento

| Marco | Status | Evidência |
| --- | --- | --- |
| gate `04-0` | aprovado | `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` aprovados em 2026-10-08; `ACCESS-03B` não bloqueante |
| `SEQ-12A` | pendente | lifecycle genérico, test run multipersona e regra de regrade |
| `SEQ-12` | pendente | `SelfReview` individual e coletivo |
| schema de `SEQ-13` | aguardando proposta e aprovação | lease, ownership de stage, identidades e estados de claim |
| `SEQ-13` | bloqueado pelo schema gate | `PeerReview` individual e coletivo |
| `SEQ-14` | pendente | contract test de provider e gate condicional |
| schema de schedule em `SEQ-15` | aguardando proposta e aprovação | owner por rodada, UTC, concorrência e índice de vencimento |
| `SEQ-15` | bloqueado pelo schema gate | integração global, operação e projeções idempotentes |
| `SEQ-16` | pendente | CI, auditoria e checklist global |
