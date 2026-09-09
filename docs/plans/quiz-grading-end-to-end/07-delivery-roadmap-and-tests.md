# 07. Roadmap de entrega e testes

## Objetivo

Executar o plano em fatias verticais pequenas, sem uma refatoração longa que
deixe contratos, UI e backend em estados incompatíveis.

## Ordem de entrega

A ordem única e detalhada de implementação está em
[`08-implementation-sequence.md`](./08-implementation-sequence.md). Este
documento não mantém uma segunda lista de fases: ele define como dividir PRs e
quais testes e gates devem ser aplicados aos marcos daquela sequência.

## Estratégia de PRs

Cada marco pode ter múltiplos PRs, mas um PR não deve misturar:

- alteração ampla do baseline de schema e redesign amplo de UI;
- novo handler e refatoração do lifecycle inteiro;
- mudança de contrato sem consumidores atualizados;
- remoção de caminho antigo antes do E2E equivalente.

Ordem interna recomendada:

```text
contrato e testes -> domínio API -> persistência -> endpoints -> web -> E2E
```

Os gates são cumulativos. Depois de cada edição aprovada do baseline, o CI
recria o banco do zero, compara o modelo global e executa os testes da fatia
atual mais todos os contratos, testes e E2Es aprovados nas partes anteriores.
Uma parte nova não pode reduzir a suíte da anterior nem aceitar drift fora do
delta do `SCHEMA-GATE`.

## Matriz E2E de workflows

A matriz é ampliada em passagens incrementais:

1. em `SEQ-08` e `SEQ-09`, pelo test run do professor para
   `InstructorReview` e `AutomatedReview`;
2. em `SEQ-10`, pela primeira tentativa oficial individual desses dois
   workflows, incluindo release e gradebook mínimos;
3. em `SEQ-11`, acrescentando sujeito coletivo aos dois workflows já
   comprovados e removendo o fan-out anterior;
4. em `SEQ-12` e `SEQ-13`, novamente em test run e tentativa oficial individual
   e coletiva para completar `SelfReview` e `PeerReview`;
5. em `SEQ-14`, por contract test e test run com provider controlado de
   `AIReview`;
6. em `SEQ-15`, acrescentando score global e consumidores operacionais
   avançados.

| Primário | Direto | Com instrutor |
| --- | --- | --- |
| `PeerReview` | obrigatório | obrigatório |
| `AIReview` | contract test com provider | contract test com provider |
| `AutomatedReview` | obrigatório | obrigatório |
| `SelfReview` | obrigatório | obrigatório |
| `InstructorReview` | obrigatório | não aplicável |

No test run, cada cenário deve verificar:

1. criação e preparação da revisão candidata;
2. prepare, start e revisão imutável candidata;
3. payload learner-safe;
4. submissão estruturada;
5. ator autorizado;
6. resultado por item;
7. transição correta;
8. finalização direta ou espera pelo instrutor;
9. isolamento de efeitos acadêmicos;
10. idempotência;
11. inteiro de unidades e regra de passing score;
12. test run não ativa a revisão nem emite evento acadêmico;
13. capability `AuthorTest` não é aceita como `OfficialSubmission`;
14. publish, quando a capability oficial existir, ativa o mesmo revision ID
    testado se o draft não mudou;
15. `AutomatedReview` parcial preserva itens pendentes sem falhar.
16. revisão e execução resolvem exatamente o mesmo
    `AssessmentExecutionManifestV1` antes e depois de deploy;
17. `AuthoringSourceHash` não muda por alteração exclusiva de manifest,
    catálogo ou health, enquanto `ExecutionSnapshotHash` identifica a fonte e
    as versões efetivamente fixadas;
18. preflight rejeita um artefato incompatível antes de receber tráfego.
19. prepare, test run, publish e start oficial preservam o mesmo `revisionId`,
    os mesmos bytes canônicos do manifest e o mesmo `ExecutionSnapshotHash`.
20. start materializa `AssessmentExecutionDeliveryV1`; resume e retry devolvem os
    mesmos bytes e `DeliveryHash`, enquanto restart cria outra execução.
21. o decoder ignora ou rejeita qualquer tentativa do cliente de substituir
    challenge, variáveis, seed ou ordem inicial.
22. `itemOrder` e ordenações internas sobrevivem ao round-trip sem depender da
    ordem de propriedades JSON; gerador com answer key privada aleatória não
    derivável é rejeitado pela capability.

Na tentativa oficial, acrescentar:

1. enrollment, identidade e políticas reais;
2. distribuição real de `PeerReview`;
3. provider real de `AIReview`, quando instalado;
4. resultado e feedback do aluno;
5. gradebook conforme grupo e peso;
6. auditoria acadêmica e notificações;
7. policy canônica produzindo uma única contribuição efetiva das tentativas;
8. release manual autorizado, concorrente e idempotente;
9. tentativa coletiva com um resultado e projeções por participante.
10. start e submit idempotentes retornam o outcome anterior para replay
    idêntico e conflito para chave reutilizada com payload divergente;
11. SpeedGrader e fila docente usam stage/round canônico, sem endpoint antigo
    capaz de atribuir score diretamente;
12. draft coletivo versionado, auditoria append-only sem duplicação em replay e
    submit final atômico sob concorrência;
13. submit de `PeerReview` entra como evidência do stage canônico, sem score,
    agregação ou notificação autoritativos pelo caminho anterior.
14. quiz avaliado usa somente `AssessmentSubmission`; `submitActivity`,
    controllers/services genéricos, `ContentInteraction` e `ActivityGrade` não
    recebem resposta nem produzem efeito acadêmico para ele.
15. `AssessmentSubmission.Passed` usa o `Assessment.PassingScore` absoluto da
    revisão e não muda quando somente `Program.PassingScore` muda.
16. filas, tarefas e SpeedGrader representam grupo por uma única submission e
    execução, sem `CanonicalRow` ou submissions irmãs.
17. toda projeção learner, incluindo dashboard, workspace, gradebook e nota
    global, omite rodada retida e não permite inferi-la por agregado.
18. regrade permanece na revisão, manifest, entrega e respostas originais e
    preserva a última rodada liberada até o release da substituta.
19. `PeerReview` insuficiente após o prazo entra em
    `AwaitingInstructorResolution`; extensão, reatribuição e resolução docente
    são autorizadas, idempotentes e auditadas.
20. `on-release-and-pass` não altera progresso, pré-requisito, certificado ou
    outro sinal learner-visible antes do release da rodada aprovada.
21. release referencia somente `GradeRoundId`; o comando rejeita uma rodada que
    não pertença à submission informada para autorização, e a persistência
    rejeita rodada pertencente a `AuthorTest` mesmo sem passar pelo comando.
22. unpublish bloqueia novo start e preserva submission já iniciada.
23. assessment sem grupo mantém resultado sem colocação; troca de grupo ou peso
    reprojeta uma vez sem criar grading novo.
24. schedule, cancelamento, reagendamento e worker de release são idempotentes e
    emitem um único `GradeResultReleased` por rodada.

## Testes transversais

### Segurança

- answer key ausente em todos os DTOs do aluno;
- rota genérica, expansão e mapper alternativo não retornam `JsonBody` autoral
  a aluno ou visitante;
- rotas genéricas de submit, complete, update progress e grade rejeitam quiz
  avaliado antes de gravar resposta, conclusão, progresso,
  `ContentInteraction` ou `ActivityGrade`;
- score não pode ser injetado no payload de resposta;
- ator autenticado não é substituído por persona simulada no test run;
- `SelfReview` individual aceita somente o dono da tentativa; o coletivo aceita
  somente participantes do snapshot e mantém uma evidência única;
- peer acessa somente submissão atribuída e anônima;
- instrutor precisa de permissão no curso;
- serviços automático e de IA não usam credencial de usuário;
- provider ausente ou inválido não produz score.
- endpoints, DTOs, mappers e queries learner não expõem score, passed, feedback
  ou agregados de rodada `Withheld`/`Scheduled`;
- progresso, pré-requisitos e certificados não revelam `Passed` retido quando a
  policy for `on-release-and-pass`;

### Consistência

- todos os valores de bitmask de `0` a `31` são aceitos ou rejeitados conforme
  a matriz;
- ordem textual das flags não altera o pipeline;
- peso não altera métodos;
- total equivale aos itens;
- resultado automatizado parcial equivale à soma dos itens efetivamente
  avaliados e não inventa score para pendentes;
- mudança de definição não altera execução iniciada;
- cada `GradingExecution` possui exatamente um owner relacional;
- test run multipersona possui uma execução por sujeito sintético, sem misturar
  resultados de alvos diferentes;
- test run não possui estado de release acadêmico;
- test run não emite `GradeResultFinalized` nem `GradeResultReleased`;
- regrade preserva versão anterior e reutiliza revisão, manifest, entrega e
  respostas da execução; definição diferente exige nova submission/execução;
- `Assessment.PassingScore` não é substituído por `Program.PassingScore`;
- mean, median e partial credit preservam quatro casas na string persistida;
- scores e percentuais de largura fixa ordenam corretamente como texto;
- índices de score e percentual usam collation invariante e preservam a ordem
  nos valores de transição entre casas decimais;
- SQL não soma, tira média nem converte strings acadêmicas para `numeric`;
- projeções precomputadas são refeitas idempotentemente após resultado, peso ou
  policy de contribuição mudar;
- assessment sem grupo não recebe colocação no gradebook e troca de grupo/peso
  preserva workflow, rounds, resultado e release;
- todas as projeções obtêm a mesma contribuição efetiva, ou `maxAttempts > 1`
  é rejeitado enquanto não houver policy implementada.

### Concorrência

- duplo start reutiliza tentativa;
- start concorrente materializa uma única entrega e resume/retry preservam o
  mesmo `DeliveryHash`;
- duplo submit não duplica estágio;
- dois integrantes do grupo iniciando ou enviando em paralelo reutilizam a
  mesma submission coletiva;
- saves coletivos aceitos criam exatamente um
  `CollectiveAttemptDraftChanged` por nova versão; replay idêntico reutiliza o
  outcome e não duplica auditoria;
- saves do draft coletivo de `SelfReview` aplicam a mesma semântica de versão,
  request hash, outcome persistido e auditoria não duplicada;
- dois workers não executam o mesmo grading;
- dois reviewers não finalizam simultaneamente;
- retry não duplica evento, notificação ou passback;
- crash depois do commit e antes do dispatch não perde evento de outbox;
- evento acadêmico durável não é publicado simultaneamente pela outbox e pelo
  dispatcher em processo;
- cada consumer confirma `(EventId, ConsumerKey)` separadamente; falha de um
  mantém somente sua entrega pendente e retry não repete efeitos confirmados;
- a rota de `RequiredConsumerKeys` permanece congelada no evento; habilitar novo
  consumer não altera mensagens anteriores nem inicia replay implícito;
- claim expirado pode ser reatribuído sem consumir cota;
- último peer review concorrente agrega uma única vez;
- duas finalizações de rodada não substituem histórico;
- release contra rodada obsoleta falha; retry idêntico não duplica evento,
  projeção ou notificação.
- regrade mantém a última rodada liberada learner-visible até o release da nova;
- claims peer insuficientes após o prazo chegam uma única vez a
  `AwaitingInstructorResolution` sob concorrência.

### Contratos

- fixtures JSON válidas e inválidas;
- compatibilidade TypeScript/C#;
- teste de fronteira garante que `@game-guild/grading` não dependa de quiz e
  que toda tradução específica passe por `@game-guild/grading-adapter-quiz`;
- fixtures de quiz pertencem ao adapter e são executadas tanto pela referência
  TypeScript quanto pelos avaliadores autoritativos C#;
- fixtures de crédito parcial verificam Matching por pares corretos, Ordering
  por posição absoluta correta, tudo-ou-nada quando desativado e quantização
  idêntica de `ScoreValue` nas duas linguagens;
- round-trip de enums flags;
- redaction learner-safe;
- capability por contexto rejeita promoção de `AuthorTest` para
  `OfficialSubmission`;
- `SelfReview` e `PeerReview` registram primeiro `AuthorTest`; suas capabilities
  `OfficialSubmission` só aparecem depois dos E2Es oficiais individual e
  coletivo;
- contrato de `ReleaseGradeResult` valida ator, round esperado, versão e
  idempotency key;
- versionamento rejeita versão futura desconhecida.
- contrato da entrega exige `itemOrder`, JSON canônico textual e derivação de
  toda correção privada a partir da revisão mais a entrega pública;
- catálogo de versões suportadas e preflight cobrem todas as revisões ativas,
  revisões retidas elegíveis a regrade e execuções não terminais do ambiente;
- política de retenção mantém resolvíveis os manifests exigidos por rollback e
  regrade.

## Sem legacy e sem migrations incrementais

O produto não foi lançado. A implementação deve:

- atualizar os produtores e consumidores no mesmo corte de contrato;
- não manter aliases permanentes;
- não criar dual-read ou dual-write;
- não criar migration incremental, migration de dados ou backfill;
- não migrar documentos ou registros atuais;
- recriar bancos locais, de desenvolvimento e de teste afetados;
- substituir as migrations históricas de desenvolvimento por um único baseline
  EF global de criação compatível com `MigrateAsync`, ou substituir primeiro
  esse inicializador; não anexar uma migration de transformação;
- comparar o modelo global antes e depois e rejeitar drift não aprovado em
  qualquer outro módulo;
- inventariar todo SQL ativo fora do `IModel`, incluindo funções, procedures,
  triggers, policies, grants, views, extensões e índices especiais, e
  reinstalar diretamente o estado aprovado no baseline limpo;
- comparar catálogos PostgreSQL e executar testes funcionais dos artefatos SQL
  críticos; diff de snapshot EF isolado não prova equivalência do banco;
- ajustar diretamente o mesmo baseline a cada `SCHEMA-GATE` aprovado até que
  ele represente o contrato final;
- remover cada caminho substituído no mesmo corte em que o novo E2E passa.

Qualquer ambiente pré-lançamento que ainda contenha o schema descartado deve ser
resetado de forma coordenada. Preservar seus dados não faz parte deste plano.

## Checklist final

- [ ] nove workflows cobertos no domínio;
- [ ] cinco reviews reconhecidos pelo registry;
- [ ] quatro reviews executáveis sem integração externa;
- [ ] `AIReview` conectável por provider e bloqueado quando ausente;
- [ ] revisão docente opcional e sempre final;
- [ ] test run do professor isolado de efeitos acadêmicos;
- [ ] test run sem `GradeResultFinalized` ou `GradeResultReleased`;
- [ ] test run de peer com participantes e respostas independentes;
- [ ] snapshot imutável da definição por tentativa;
- [ ] lifecycle de publicação explícito e independente de visibility;
- [ ] capability `OfficialSubmission` obrigatória para publish;
- [ ] rotas learner/public sem DTO autoral antes de qualquer publish;
- [ ] score em inteiro de unidades e passing score sem conflito de ownership;
- [ ] peso e percentual acadêmicos em inteiros de escala `100`, sem `decimal`
  no banco;
- [ ] ownership separado entre content grading e assessment execution policy;
- [ ] `AuthoringSourceHash` e `ExecutionSnapshotHash` reproduzíveis entre C# e
  TypeScript, com responsabilidades distintas;
- [ ] manifest executável coberto por `ExecutionSnapshotHash` e resolvido por
  versão exata;
- [ ] publish valida sem regenerar a revisão, o manifest ou
  `ExecutionSnapshotHash` preparados e testados;
- [ ] preflight de deploy e retenção de artefatos impedem versão ativa
  irresolúvel;
- [ ] resultado por item e final persistidos;
- [ ] entrega canônica textual com `itemOrder`, hash reproduzível e sem estado
  privado aleatório não persistido;
- [ ] `GradingExecution` com owner relacional único para subject de test run ou
  submission;
- [ ] resultado parcial automatizado preservado sem falha;
- [ ] finalização e liberação independentes;
- [ ] release por rodada preserva a última visão learner durante regrade;
- [ ] `ReleaseGradeResult` idempotente e autorizado;
- [ ] test run sem linha ou estado de release acadêmico;
- [ ] aluno vê resultado correto sem vazamento por dashboard, workspace,
  gradebook, nota global, DTO ou agregado;
- [ ] quiz avaliado usa somente `AssessmentSubmission`; caminho genérico de
  content não recebe resposta nem cria progresso ou nota acadêmica;
- [ ] gradebook ponderado correto;
- [ ] `Assessment.PassingScore` decide a submission e `Program.PassingScore`
  somente a consolidação global do curso;
- [ ] contribuição efetiva das tentativas determinada por policy canônica, sem
  divergência entre consumers;
- [ ] auditoria e regrade completos, sempre sobre a revisão original da execução;
- [ ] histórico acadêmico transacional e outbox durável com receipt por
  `(EventId, ConsumerKey)`;
- [ ] eventos acadêmicos retirados do dispatch em processo após entrarem na
  outbox;
- [ ] peer claims com lease e resolução terminal auditável de evidência
  insuficiente;
- [ ] controller, service, actions e workspace de peer não mantêm autoridade
  paralela sobre score agregado ou notificação;
- [ ] `GradingQueueService`, `TasksService` e SpeedGrader não dependem de
  `CanonicalRow` ou submissions irmãs para sujeito coletivo;
- [ ] banco vazio sobe no schema final sem migration incremental ou backfill;
- [ ] banco vazio contém e executa todos os artefatos SQL ativos aprovados fora
  do `IModel`;
- [ ] quiz atribuído a grupo usa uma submission, uma rodada e um resultado;
- [ ] snapshot de participantes e projeções coletivas são idempotentes;
- [ ] draft coletivo versionado preserva respostas e possui trilha append-only
  do ator sob saves/submits concorrentes;
- [ ] draft coletivo de `SelfReview` usa envelope idempotente e trilha
  append-only sem duplicação em replay;
- [ ] filas e observabilidade operacionais;
- [ ] caminhos antigos removidos;
- [ ] métodos e seletores usam `Review`, enquanto ações e resultados usam
  `Grade`/`Grading`;
- [ ] mapas em `docs/types` atualizados com o código final.
