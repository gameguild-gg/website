# Parte 2. E2E principal de grading

## Objetivo

Construir o runtime compartilhado, validar `InstructorReview` e
`AutomatedReview` no test run e entregar o fluxo acadêmico oficial individual
e coletivo. Esta parte contém `SEQ-07` a `SEQ-11`.

Regras globais: [`08-implementation-sequence.md`](../08-implementation-sequence.md).

## Pré-requisitos

- Parte 1 concluída, testada e aprovada em seu gate de saída;
- baseline, contratos, revisão imutável e fronteira learner-safe estáveis;
- nenhuma capability oficial registrada por atalho fora desta parte.

## Fora do escopo

- `SelfReview`, `PeerReview` e provider real de `AIReview`;
- políticas avançadas de tentativas, release e operação;
- redesign operacional que não seja necessário ao primeiro E2E oficial.

## `SEQ-07`. Construir runtime comum e test run isolado

### Resultado

O runtime comum e o isolamento de `AuthorTest` são comprovados com uma revisão
e um handler controlados de infraestrutura. Nenhum workflow real nem jornada
do professor é declarado completo neste marco.

### Implementação

- implementar o orquestrador de stages e rounds sobre
  `IReviewStageHandler`;
- criar `AssessmentTestRun` referenciando candidata ou revisão ativa;
- criar um `AssessmentTestRunSubject` e uma `GradingExecution` por alvo
  sintético; reviews não-peer usam um subject padrão e peer usa vários;
- suportar start, resume, submit, restart e personas de teste;
- materializar uma única vez `AssessmentExecutionDeliveryV1` por
  `GradingExecution` no start, antes de expor o bundle ao cliente de teste;
  resume e
  retry reutilizam os mesmos bytes, enquanto restart cria nova execução e pode
  produzir outra entrega;
- congelar workflow, policy e referência à revisão no start, preservando os
  mesmos bytes do `AssessmentExecutionManifestV1` e o mesmo
  `ExecutionSnapshotHash` persistidos no prepare; cada stage resolve somente a
  versão exata fixada pelo manifest;
- registrar respostas, stages, diagnósticos e evidências de teste;
- concluir a execução por uma transição interna comum e persistir somente o
  resultado diagnóstico; não emitir `GradeResultFinalized`,
  `GradeResultReleased` ou outra mensagem acadêmica;
- aplicar a matriz de autorização distinguindo ator autenticado e persona;
- bloquear enrollment, progresso, submission oficial, gradebook, passback e
  notificação acadêmica no contexto `AuthorTest`;
- validar se a candidata testada ainda coincide com o draft e exibir readiness
  de publicação no contrato de aplicação, sem habilitar a jornada de produção
  enquanto faltar um handler real;
- provar o lifecycle do orquestrador com handler controlado somente nos testes
  de infraestrutura, sem declarar workflow de produção completo.

### Gate

- test run e tentativa oficial nunca compartilham a mesma execução;
- retry não duplica round ou stage;
- start e resume concorrentes devolvem a mesma entrega e o mesmo
  `DeliveryHash`; o cliente não consegue escolher ou substituir o challenge;
- persona simulada nunca aparece como ator autenticado na auditoria;
- finalizar test run não grava evento acadêmico nem alimenta consumer de
  gradebook;
- testar uma candidata não a ativa nem cria ou agenda tentativa de aluno.
- nenhum handler controlado é registrado na configuração de produção e a UI do
  professor não apresenta test run executável antes de `SEQ-08`.

Referência: [`03`](../03-author-assessment-test-runs.md).

## `SEQ-08`. Entregar `InstructorReview` no test run

### Resultado

O primeiro workflow fica completo no contexto de teste: resposta, fila de
revisão, resultado por item, feedback, finalização, override e regrade.

### Implementação

- implementar e registrar `InstructorReview` como review primário com
  capability `AuthorTest`;
- preparar, pela primeira vez com um handler real, uma candidata de
  `InstructorReview` e executar o test run sobre exatamente seu `revisionId`,
  manifest e `ExecutionSnapshotHash`; o registry controlado da Parte 1 não é
  uma candidata operacional de produção;
- manter a capability `OfficialSubmission` e o publish de produção bloqueados
  até a integração acadêmica de `SEQ-10`;
- autorizar somente instrutores válidos no contexto correto;
- criar superfície de correção por item com total calculado no servidor;
- registrar comentário, feedback, evidência e motivo de override;
- preservar rodadas anteriores em regrade;
- separar estado finalizado de estado liberado, sem emitir efeitos acadêmicos
  no test run;
- exibir histórico, ator real e estado corrente.

### Gate

- o professor conclui o fluxo inteiro sem editar answer payload;
- dois reviewers não finalizam a mesma rodada;
- override e regrade preservam ator, motivo, antes e depois;
- não há gradebook, notificação ou resultado de aluno;
- a candidata real preparada depois do registro de `InstructorReview` percorre
  o test run sem reconstrução de manifest.

## `SEQ-09`. Entregar `AutomatedReview` no test run

### Resultado

O servidor avalia deterministicamente o que conhece e mantém os demais itens
pendentes sem falha técnica nem score inventado.

### Implementação

- implementar e registrar avaliadores determinísticos C# por tipo de questão;
- usar `@game-guild/grading-adapter-quiz` como referência TypeScript exclusiva
  da semântica específica de quiz, sem reintroduzir imports de quiz em
  `@game-guild/grading`;
- declarar somente capability `AuthorTest` neste marco;
- preparar uma candidata real de `AutomatedReview` depois do registro dos
  avaliadores aplicáveis e executar o test run sobre o manifest fixado;
- compartilhar com os avaliadores C# as fixtures versionadas de
  `@game-guild/grading-adapter-quiz`, mantendo no servidor a autoridade sobre o
  resultado oficial;
- produzir resultado por item com evidência e versão do algoritmo;
- implementar `matching-position-v1` e `ordering-position-v1`: com
  `allowPartialCredit = true`, Matching calcula pares corretos sobre o total de
  pares exigidos e Ordering calcula itens na posição absoluta correta sobre o
  total de itens; com a flag falsa ou ausente, ambos continuam tudo-ou-nada;
- calcular a fração com aritmética exata, multiplicar por `maxScore` e
  quantizar uma única vez pela precisão e arredondamento de `ScoreValue`.
  Respostas ausentes contam como incorretas; IDs desconhecidos, repetidos ou
  cardinalidade acima do permitido invalidam o payload, sem produzir score;
- fixar essas chaves e versões no manifest e tratar qualquer outra semântica de
  crédito parcial como uma nova versão de algoritmo;
- executar a versão determinística fixada no manifest da revisão, sem promover
  automaticamente o avaliador mais recente do deploy;
- finalizar diretamente apenas quando todos os itens estiverem resolvidos;
- em `AutomatedReview,InstructorReview`, encaminhar pendentes e itens já
  avaliados para revisão final editável;
- manter a publicação de automated-only parcial condicionada à decisão de UX
  ainda pendente, sem alterar o comportamento correto do runtime;
- produzir diagnóstico de cobertura por item. Enquanto a decisão estiver
  pendente, `AutomatedReview` direto só pode obter readiness oficial quando
  todos os itens forem determinísticos; cobertura parcial exige
  `InstructorReview` como stage final;
- manter o publish oficial bloqueado até `SEQ-10`, inclusive quando a cobertura
  do test run for completa.

### Gate

- mesma definição e resposta produzem o mesmo resultado;
- implementação TypeScript do adapter e implementação C# autoritativa passam
  pelos mesmos vetores válidos, inválidos, pendentes e não suportados;
- fixtures de Matching e Ordering cobrem zero, acerto parcial, acerto total,
  flag desativada, IDs ausentes/desconhecidos/repetidos e arredondamento nos
  limites de `ScoreValue`;
- submit e retry são idempotentes;
- item não suportado permanece pendente e nunca recebe zero implícito;
- fluxos direto completo e combinado com instrutor passam no test run;
- readiness distingue cobertura total, cobertura parcial encaminhável ao
  instrutor e workflow não publicável.

## `SEQ-10`. Entregar o primeiro E2E acadêmico individual

### Resultado

Sem esperar `SelfReview`, `PeerReview` ou `AIReview`, o aluno percorre o fluxo
oficial individual com `InstructorReview` e `AutomatedReview`, recebe o
resultado permitido e gera a projeção mínima de gradebook.

### `SCHEMA-GATE` da tentativa individual

Apresentar e aprovar somente o necessário para:

- submission individual ligada a enrollment e revisão imutável;
- relação da submission com a `GradingExecution` que possui a entrega concreta
  aprovada no schema do núcleo, sem segundo storage específico da tentativa;
- tentativa, número, tempo, estado, idempotency key, request hash canônico,
  outcome persistido, escopo de unicidade e retenção da deduplicação;
- política de contribuição das tentativas no gradebook, caso
  `maxAttempts > 1` seja habilitado neste corte; persistência adicional só é
  apresentada se a policy aprovada for configurável por assessment;
- rounds e resultado oficial reutilizando o núcleo;
- estado de release `Withheld` ou `Released`;
- release versionado por rodada, com uma linha única identificada por
  `GradeRoundId`. A submission é derivada pelo owner da `GradingExecution`, e o
  gate deve provar por FK e por uma invariante verificável no banco que release
  só pode existir para rodada de execução oficial. Se a estrutura relacional
  não conseguir expressar a condição transitiva apenas com FKs, apresentar no
  `SCHEMA-GATE` a constraint de banco mínima, testada e necessária. Não duplicar
  `AssessmentSubmissionId` na linha de release nem usar uma única linha mutável
  que apague o release anterior;
- concorrência e deduplicação do comando `ReleaseGradeResult`, reutilizando a
  infraestrutura idempotente aprovada ou apresentando o delta necessário;
- projeção idempotente mínima no gradebook;
- semântica completa da colocação no gradebook: grupo de peso positivo
  contribui, grupo de peso zero permanece como prática e assessment sem grupo
  possui resultado sem colocação. O gate deve incluir a reprojeção idempotente
  causada por troca de grupo ou peso, sem reabrir grading;
- projeções por assessment, grupo e curso necessárias para aplicar uma única
  fórmula: a policy seleciona a contribuição efetiva do assessment;
  `groupRatio` divide a soma dos scores efetivos pela soma dos `MaxScore`
  capturados correspondentes; a contribuição do grupo multiplica essa razão por
  `WeightPercent`; o percentual do curso soma as contribuições dos grupos sem
  renormalização implícita. O gate deve provar que nenhuma consulta ou consumer
  recalcula uma variante dessa fórmula;
- projeção learner separada da projeção interna do gradebook. O gate deve provar
  como score, passed, feedback, breakdown e agregados permanecem ocultos até o
  release, inclusive em dashboard e workspace;
- projeção idempotente de progresso do content avaliado, conforme a policy de
  conclusão aprovada em `SEQ-00`; reutilizar `ContentInteraction` somente se ele
  puder ser tratado como read model sem autoridade de escrita genérica. Qualquer
  delta relacional necessário deve ser apresentado neste gate;
- uso de `AssessmentGroup.WeightPercent` e demais pesos ou percentuais já
  normalizados como inteiros de escala `100` em `SEQ-03`, sem reintroduzir
  coluna ou cálculo `decimal` neste gate;
- eventos `GradeResultFinalized` e `GradeResultReleased`;
- solicitação durável e idempotente de `ReleaseGradeResult` persistida na mesma
  transação da finalização quando a policy for `immediate`, reutilizando a
  outbox/infraestrutura de comandos aprovada. Uma queda entre commit e dispatch
  não pode deixar a rodada indefinidamente retida;
- índices de duplo start, duplo submit e concorrência.

Após aprovação, editar o mesmo baseline global e recriar os bancos afetados.
Não criar uma migration de evolução.

### Implementação

- resolver enrollment e autorização antes do start;
- integrar `InstructorReview` e `AutomatedReview` ao adapter
  `OfficialSubmission` e registrar suas capabilities primeiro apenas em uma
  composição controlada de integração/E2E, nunca na configuração de produção;
- no publish, resolver e validar o manifest já fixado no prepare. A capability
  `OfficialSubmission` deve satisfazer as mesmas chaves e versões de projector,
  gerador de entrega, decoder/normalizador, handler e algoritmo exercitadas para
  a revisão, sem fallback para versão recente e sem regenerar manifest, revisão
  ou `ExecutionSnapshotHash`;
- bloquear publish de `AutomatedReview` direto quando qualquer item não tiver
  cobertura determinística; permitir cobertura parcial somente quando
  `InstructorReview` for o stage final;
- nessa composição controlada, publicar a revisão candidata e executar o E2E
  oficial completo, com enrollment, submission, grading, release e projeções;
  somente depois do gate promover para a composição de produção exatamente as
  mesmas capability keys, versões e implementações imutáveis exercitadas. O
  preflight de produção continua fail-closed antes dessa promoção e volta a
  bloquear qualquer divergência depois dela;
- criar ou reabrir submission individual idempotentemente;
- congelar revisão, policy e tentativa no start e materializar no servidor a
  entrega concreta antes de retorná-la; variáveis, prompts públicos,
  `itemOrder`, ordenações internas e demais challenges oficiais nunca são
  aceitos do browser;
- aplicar disponibilidade, limite, política de contribuição e tempo no servidor;
- implementar ao menos uma policy aprovada que selecione uma tentativa
  finalizada e seja compartilhada por gradebook e integrações de score; a policy
  de conclusão governa progresso separadamente. Se isso ainda não estiver
  pronto, rejeitar `maxAttempts > 1` em
  save/publish/start;
- receber a resposta estruturada e acionar o mesmo orquestrador do test run;
- validar e persistir o `AssessmentResponseEnvelopeV1` pelo decoder exato do
  manifest antes de iniciar reviews; para quiz, aceitar somente
  `QuizAnswerEnvelopeV1` e nunca o formato textual anterior;
- substituir o branch de quiz avaliado em `activity-component.tsx` e a action
  `submitActivity` pelo start/submit de `AssessmentSubmission`; manter o
  caminho genérico somente para conteúdo não avaliável;
- manter no servidor a rejeição, introduzida em `SEQ-05`, de quiz avaliado em
  `ProgramContentController.SubmitContent`,
  `ProgramWriteService.SubmitUserContentAsync`,
  `ProgramCrudController.MarkContentCompleted`,
  `ProgramCrudController.MarkMyContentCompleted`,
  `ContentInteractionController.UpdateProgress`,
  `ContentInteractionController.SubmitContent`,
  `ContentInteractionController.CompleteContent` e nos services
  correspondentes; impedir também create, update ou delete de `ActivityGrade`
  para esse quiz por `ActivityGradeController` e `ActivityGradeService`.
  Remover branches, DTOs, clients, actions e testes antigos específicos no mesmo
  corte E2E;
- mapear a conclusão oficial para `GradeResultFinalized` na mesma transação do
  resultado; test run continua fora desse mapeamento. Quando a policy for
  `immediate`, a mesma transação também persiste uma solicitação durável e
  idempotente de `ReleaseGradeResult`, sem marcar a rodada como `Released`;
- processar a solicitação imediata por worker/dispatcher que chama o comando
  explícito `ReleaseGradeResult` com identidade de serviço da policy. A
  liberação manual chama a mesma fronteira com instrutor autorizado. Em ambos os
  casos o comando exige rodada esperada, concorrência otimista, idempotency key e
  auditoria; retry após queda é inócuo;
- emitir uma única mensagem `GradeResultReleased` por transição válida, sem
  reutilizar `GradeResultFinalized`;
- projetar no gradebook somente a contribuição efetiva determinada pela policy
  de tentativas e aplicar exclusivamente a fórmula canônica definida em
  `SEQ-00`: agregação por pontos dentro do grupo, multiplicação pelo peso e soma
  das contribuições de grupo sem renormalização;
- manter a projeção interna do gradebook apta a consumir resultado finalizado,
  mas construir toda projeção learner somente com rodadas liberadas. Enquanto
  existir contribuição interna retida, não retornar total, percentual,
  `FinalGrade`, denominador ou breakdown que permita inferir a nota oculta;
- projetar a conclusão do content somente a partir das transições canônicas de
  submission/resultado e da policy aprovada em `SEQ-00`; a projeção é
  idempotente por assessment, content, enrollment e transição de origem, e não
  transforma consumo ou chamada genérica em conclusão acadêmica. Um modo
  dependente de aprovação só produz conclusão learner-visible depois do release
  da rodada, conforme o contrato `on-release-and-pass` aprovado;
- apresentar ao aluno somente resultado liberado e feedback permitido;
- substituir os read models learner atuais no mesmo corte: inventariar e adaptar
  `AssessmentsController.GetSubmission`, `GetMySubmissions`, respostas de start e
  submit, `LearnerAssessmentSubmissionDto`, `LearnerAssessmentAttemptDto`,
  `GetLearnerDashboardQuery`, `GetLearnerCourseWorkspaceQuery`,
  `LearnerWorkspaceMapper`, `apps/web/src/lib/learner/records.ts`, DTOs de
  workspace, queries web e clients gerados. Nenhum deles pode ler diretamente
  `AssessmentSubmission.Score`, `Passed`, `Feedback` ou um agregado interno sem
  aplicar a projeção de release;
- verificar que o corte learner-safe de `SEQ-05` continua fechado;
- remover qualquer grading oficial no browser ou caminho oficial paralelo;
- adaptar a fila e a superfície atuais do SpeedGrader para ler stage/round
  oficial e produzir `InstructorReviewEvidenceV1`, override ou regrade por
  comandos do novo runtime, sem mutar score diretamente;
- manter regrade preso à revisão, manifest, entrega e respostas originais da
  execução. Iniciar regrade preserva a última rodada learner-visible; a nova
  rodada substitui a contribuição interna quando finaliza e substitui a visão do
  aluno somente quando é liberada. Avaliação por definição nova não usa o
  comando de regrade;
- provar o lifecycle de unpublish: ele bloqueia novos starts oficiais, mas uma
  submission iniciada antes da despublicação continua usando sua revisão,
  manifest, entrega e policy congelados;
- substituir, no mesmo corte, o endpoint
  `POST submissions/{submissionId}/grade`, `GradeSubmissionAsync`, a action web
  `gradeSubmission` e seus consumidores pelo contrato novo; remover cálculo
  por `Program.PassingScore`, fan-out, passback e notificação disparados pelo
  caminho anterior;
- nesta parte, remover produtores diretos antigos e persistir somente os eventos
  canônicos na outbox. Notificações externas e passback permanecem desligados
  até `SEQ-15`; a consulta autenticada do aluno ao resultado liberado não
  depende desses consumers. A rota congelada dos eventos desta parte inclui
  somente os consumers já habilitados; `SEQ-15` não altera mensagens anteriores
  nem inicia replay implícito;
- calcular `AssessmentSubmission.Passed` exclusivamente pelo
  `Assessment.PassingScore` absoluto capturado na revisão imutável. O primeiro
  E2E e a projeção mínima não consultam `Program.PassingScore`; esse percentual
  global, embora já normalizado em string desde `SEQ-03`, só ganha efeito
  acadêmico na consolidação do curso em `SEQ-15`;
- tratar grupo e peso como configuração da projeção, fora da revisão de
  execução. Alteração autorizada emite evento auditável e reprojeta de forma
  idempotente todas as contribuições afetadas; não cria round, regrade, release
  ou nova evidência. Remover o grupo retira a colocação, peso zero mantém a
  atividade sem contribuição e peso positivo aplica a contribuição canônica;
- manter nesta parte a fila docente oficial mínima e o deep link necessários
  ao `InstructorReview`; filtros e operação entre assessments permanecem na
  Parte 3;
- bloquear temporariamente start e submit de assessment coletivo no novo
  runtime até `SEQ-11` substituir o fan-out atual; não permitir que o caminho
  coletivo anterior continue produzindo resultado oficial.

### Gate

- `InstructorReview` passa em E2E oficial até resultado do aluno e gradebook;
- `AutomatedReview` direto completo e combinado com instrutor passam no mesmo
  E2E;
- o E2E oficial é executado primeiro com registro controlado e somente promove
  à produção as mesmas capability keys, versões e implementações; antes da
  promoção, produção continua incapaz de publicar ou iniciar esse workflow;
- duplo start e duplo submit não duplicam tentativa ou grading;
- replay de start ou submit com mesma chave e mesmo request hash retorna o
  outcome persistido; mesma chave com payload diferente gera conflito, e
  chamadas concorrentes respeitam a unique constraint aprovada;
- múltiplas tentativas não duplicam contribuição e todas as projeções obtêm a
  mesma contribuição efetiva; ou `maxAttempts > 1` permanece rejeitado;
- edição do assessment não altera tentativa iniciada;
- prepare, test run, publish e start oficial preservam o mesmo `revisionId`, os
  mesmos bytes canônicos de manifest e o mesmo `ExecutionSnapshotHash`;
- start, resume e retry da mesma tentativa preservam os bytes concretos e o
  `DeliveryHash`, sem aceitar challenge escolhido pelo cliente;
- `AssessmentSubmission.Passed` muda conforme `Assessment.PassingScore` e
  permanece inalterado quando apenas `Program.PassingScore` muda;
- aluno não acessa resultado retido nem evidência privada;
- dashboard, workspace, listagens de submissions, respostas de start/submit e
  gradebook learner não expõem nem permitem inferir score retido por valor
  agregado, percentual ou mudança de denominador;
- release manual sem permissão, contra rodada obsoleta ou com replay divergente
  é rejeitado; retry idêntico não duplica o evento canônico;
- queda depois do commit da finalização e antes do dispatch preserva a
  solicitação imediata; o retry executa `ReleaseGradeResult` e produz exatamente
  um `GradeResultReleased`, sem fundir finalização e release;
- release de uma rodada que não pertença à submission informada no comando é
  rejeitado e o banco não possui coluna redundante capaz de criar essa
  associação inválida;
- um resultado de peso zero, quando liberado pela policy, não contribui para a
  nota;
- peso positivo contribui mesmo quando o resultado ainda está retido;
- dois assessments com `MaxScore` diferentes no mesmo grupo são agregados pela
  soma de pontos obtidos sobre a soma dos pontos possíveis; grupos são
  multiplicados por seus pesos e somados sem renormalização. Arredondamento
  ocorre uma única vez por projeção canônica e todos os consumers concordam com
  os mesmos vetores;
- assessment sem grupo preserva resultado e histórico, mas não cria colocação
  nem contribuição no gradebook;
- trocar grupo ou peso reprojeta exatamente uma vez, preserva workflow,
  rounds, resultado e release e registra ator, antes e depois;
- a contribuição positiva retida permanece visível somente ao professor e aos
  consumidores internos; a visão learner conserva a última rodada liberada e
  não incorpora a rodada retida em agregados;
- regrade usa os mesmos `revisionId`, manifest, entrega e respostas, preserva a
  rodada anteriormente liberada até o novo release e nunca sobrescreve seu
  histórico;
- não existe rota antiga com autoridade concorrente;
- SpeedGrader e fila mínima concluem `InstructorReview` pelo novo runtime;
- testes comprovam que rota, service e action anteriores não estão registrados,
  que nenhum passback ou notificação direta é disparado e que os eventos
  canônicos necessários a esses consumers foram persistidos uma única vez;
- testes comprovam que notificações e passback não aparecem em
  `RequiredConsumerKeys` antes de `SEQ-15`, sem bloquear gradebook, progresso ou
  auditoria;
- testes comprovam que quiz avaliado usa somente `AssessmentSubmission`: o
  submit não cria nem altera diretamente `ContentInteraction`, progresso ou
  `ActivityGrade`, e não existe fallback para `submitActivity`; somente o
  projector canônico pode atualizar o read model de progresso;
- E2E comprova que o aluno não consegue concluir ou graduar o quiz pelas rotas
  genéricas e que a transição definida pela policy projeta exatamente uma
  conclusão para o enrollment;
- E2E de `on-release-and-pass` comprova que conclusão, `Passed` e efeitos
  equivalentes permanecem invisíveis enquanto a rodada estiver retida;
- E2E comprova que unpublish bloqueia novo start e não interrompe submission já
  iniciada;
- tentativa coletiva retorna indisponibilidade explícita até o gate seguinte,
  em vez de cair no `FanOutGroupSubmitAsync`.

## `SEQ-11`. Entregar tentativa oficial coletiva

### Resultado

O bloqueio temporário de grupos é removido somente depois que uma submission
coletiva passa a produzir uma única `GradingExecution`, uma única rodada e um
único resultado. O resultado final reutiliza as projeções oficiais de `SEQ-10`
para os participantes congelados.

### `SCHEMA-GATE` de grupo

Apresentar e aprovar:

- sujeito individual ou `CourseGroup` mutuamente exclusivo na submission;
- `StartedByUserId` sem propriedade exclusiva da tentativa;
- `AssessmentSubmissionParticipant` como snapshot;
- relação única entre submission coletiva e `GradingExecution`;
- unique indexes e lock lógico por `(AssessmentId, CourseGroupId, Attempt)`;
- draft de respostas compartilhado com versão de concorrência e request hash;
- trilha append-only para cada mutação aceita do draft, reutilizando a outbox
  ou auditoria durável existente quando ela garantir ator, versão anterior,
  nova versão, request hash, instante, retenção e unicidade idempotente; o
  registro não replica o conteúdo das respostas quando metadados e hash forem
  suficientes para auditoria;
- finalização única com ator e instante, além de deduplicação de submit por
  escopo, idempotency key e request hash;
- projeção idempotente por submission, round e participante.

Se outbox ou auditoria existente não garantir a trilha de mutações, o gate deve
apresentar explicitamente a nova entidade, constraints, índice idempotente e
retenção antes de qualquer edição do baseline.

Após aprovação, editar o mesmo baseline global e recriar os bancos afetados.

### Implementação

- resolver `CourseGroup` antes do grading;
- criar snapshot de participantes no primeiro start;
- materializar uma única entrega concreta para a execução coletiva; todos os
  participantes do snapshot veem o mesmo challenge e resume/retry preservam o
  mesmo `DeliveryHash`;
- reutilizar a submission coletiva em starts e submits concorrentes;
- validar que o ator pertence ao snapshot;
- implementar `SaveCollectiveAttemptDraftV1(expectedVersion, idempotencyKey,
  requestHash)` e
  `SubmitCollectiveAttemptV1(expectedVersion, idempotencyKey, requestHash)`;
- para cada save aceito, gravar na mesma transação um evento append-only
  `CollectiveAttemptDraftChanged` com ator, versões anterior e nova, request
  hash e timestamp; replay idêntico reutiliza o outcome e não cria segundo
  registro de auditoria;
- aceitar edição do draft por participante congelado até uma única finalização
  atômica; qualquer participante do snapshot pode finalizar na policy inicial;
  rejeitar versão obsoleta, payload conflitante, escrita após finalização e
  ator fora do snapshot;
- executar review, finalização e regrade uma vez por `GradingExecution`;
- emitir um único `GradeResultFinalized` coletivo;
- aplicar uma única release por rodada à submission coletiva e projetar gradebook e
  visibilidade de resultado para cada participante;
- projetar progresso por participante a partir da única transição coletiva
  canônica indicada pela policy, sem executar novamente grading ou criar
  submissions irmãs;
- remover `FanOutGroupSubmitAsync` e qualquer grading por integrante no mesmo
  corte;
- adaptar `GradingQueueService`, `TasksService`, actions e projeções do
  SpeedGrader para a única submission coletiva. Remover dependências de
  `PeerReviewAssignmentService.CanonicalRow`, de submissions irmãs clonadas e
  de qualquer escolha de linha canônica para representar um grupo;
- reabilitar start e submit coletivos somente após essa remoção;
- manter mudanças posteriores de membresia fora da tentativa histórica;
- validar inicialmente `InstructorReview` e `AutomatedReview` para grupos.

### Gate

- integrantes concorrentes reutilizam a mesma tentativa;
- dois submits idênticos retornam a mesma finalização, enquanto submits
  divergentes ou baseados em versão obsoleta não sobrescrevem respostas;
- o ator da finalização e todas as edições aceitas do draft podem ser
  reconstruídos pela trilha append-only; replay idêntico não duplica evento e
  tentativa conflitante não altera o draft;
- grading, finalização e regrade ocorrem uma vez;
- a release única da rodada e as projeções por participante são idempotentes;
- entrega, grading e resultado são únicos para o grupo, enquanto gradebook,
  progresso e visibilidade são projetados uma vez por participante;
- `AssessmentGroup` de peso permanece distinto de `CourseGroup`;
- busca e E2E comprovam ausência de fan-out de grading antigo;
- filas, tarefas e SpeedGrader leem a única submission/execução coletiva e
  nenhum consumer depende de `CanonicalRow` ou de submissions irmãs;
- fluxos coletivos de `InstructorReview` e `AutomatedReview` passam antes de
  habilitar os reviews seguintes.

## Definição de pronto da Parte 2

- todos os gates de `SEQ-07` a `SEQ-11` estão satisfeitos;
- toda a suíte acumulada das Partes 1 e 2 passa novamente; nenhum teste,
  contrato, preflight ou invariante aprovado na Parte 1 pode ser omitido;
- test run percorre o runtime real sem emitir efeito acadêmico;
- `InstructorReview` e `AutomatedReview` passam em test run e no fluxo oficial
  individual e coletivo;
- `Matching` e `Ordering` comprovam crédito parcial versionado e produzem o
  mesmo `ScoreValue` canônico na referência TypeScript e nos handlers C#
  autoritativos;
- aluno só acessa resultado liberado e nunca recebe evidência privada;
- nenhuma projeção learner, inclusive workspace e nota global do curso, expõe
  ou permite inferir contribuição retida;
- gradebook recebe uma única contribuição canônica e idempotente;
- content avaliado recebe uma única projeção canônica de progresso por
  enrollment, sem rota genérica concorrente;
- start, submit, review, release e projeções possuem testes de retry,
  concorrência e autorização negativa;
- SpeedGrader, endpoints e actions anteriores não mantêm autoridade paralela;
- quiz avaliado não usa `submitActivity`, `ContentInteraction` ou
  `ActivityGrade` como autoridade de resposta, progresso ou nota;
- toda execução individual ou coletiva reutiliza sua entrega concreta
  imutável, gerada e validada no servidor;
- `Assessment.PassingScore` decide o estado da submission e
  `Program.PassingScore` ainda não participa dessa decisão;
- o fan-out por integrante foi removido e uma submission coletiva gera uma
  única execução e um único resultado;
- testes unitários, integração, banco vazio, segurança e E2E acumulados passam
  em CI, com criação do banco do zero e diff global sem drift após cada edição
  aprovada do baseline.

## Gate para a Parte 3

A Parte 3 só pode começar após o fluxo principal operar em produção de teste
sem caminho substituído ainda autoritativo. As evidências devem incluir E2E individual e
coletivo, reprocessamento idempotente, autorização negativa e reconstrução da
projeção mínima de gradebook e progresso, além da reprodução da entrega
concreta pelo `DeliveryHash`.

## Acompanhamento

| Marco | Status | Evidência |
| --- | --- | --- |
| `SEQ-07` | pendente | runtime e isolamento do test run com handler controlado |
| `SEQ-08` | pendente | E2E de teste de `InstructorReview` |
| `SEQ-09` | pendente | E2E de teste de `AutomatedReview` |
| `SEQ-10` | pendente | E2E individual, entrega imutável, gradebook/progresso e release idempotente |
| `SEQ-11` | pendente | E2E coletivo, entrega compartilhada e schema aprovado |
