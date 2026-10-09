# 08. Sequência canônica de implementação

## Objetivo

Transformar as especificações temáticas deste diretório em quatro partes
executáveis, ordenadas pelas dependências reais entre domínio, contratos,
persistência, API, interfaces e efeitos acadêmicos.

Este documento é o índice e o contrato global de execução. Os documentos `00`
a `06` continuam sendo as especificações de cada área, e o documento `07`
concentra a estratégia transversal de testes. A numeração desses documentos
não representa a ordem em que seus conteúdos devem ser codificados.

## Partes executáveis

| Parte | Marcos | Entrega verificável | Documento |
| --- | --- | --- | --- |
| 1 | `SEQ-00` a `SEQ-06` | fundação, autoria, segurança e publicação fail-closed | [Fundação e autoria](./implementation-sequence/01-foundation-and-authoring.md) |
| 2 | `SEQ-07` a `SEQ-11` | test run e E2E oficial individual e coletivo | [E2E principal](./implementation-sequence/02-core-grading-e2e.md) |
| Gate 2 → 3 | `CLOSE-01` a `CLOSE-04` | fechar somente lacunas encontradas após a primeira execução da Parte 2 | [Fechamento da Parte 2](./implementation-sequence/02a-core-grading-e2e-closeout.md) |
| 3 | `ACCESS-01`, `ACCESS-02`, `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` | acesso contextual, sessões reais e revalidação do E2E principal; `ACCESS-03B` é operacional e não bloqueante | [Acesso contextual](./implementation-sequence/03-contextual-access-and-personas.md) |
| 4 | `SEQ-12A`, depois `SEQ-12` a `SEQ-16` | lifecycle assíncrono comum, reviews adicionais, operação e auditoria final | [Expansão e operação](./implementation-sequence/04-review-expansion-and-operations.md) |

A Parte 2 não começa até a Parte 1 estar concluída e testada. A Parte 3 não
começa até a Parte 2 e seu fechamento `CLOSE-01` a `CLOSE-04` estarem concluídos
e testados. A Parte 4 não começa até `ACCESS-03A`, `ACCESS-04` e `ACCESS-05`
comprovarem o fluxo com owner, ao menos dois learners e outsider em sessões
reais. A delegação de equipe em `ACCESS-03B` não bloqueia esse gate. O
fechamento não repete `SEQ-07` a `SEQ-11`: executa apenas o delta registrado
após a auditoria. Cada documento possui pré-requisitos, definição de pronto,
acompanhamento e gate de passagem próprios.

## Ordem global

```mermaid
flowchart LR
    P1["Parte 1<br/>Fundação e autoria<br/>SEQ-00 a SEQ-06"]
    G1{"Gate da Parte 1<br/>aprovado?"}
    P2["Parte 2<br/>E2E principal<br/>SEQ-07 a SEQ-11"]
    C2["Fechamento da Parte 2<br/>CLOSE-01 a CLOSE-04"]
    G2{"Gate da Parte 2<br/>aprovado?"}
    P3["Parte 3<br/>Acesso contextual<br/>ACCESS-01, 02, 03A, 04 e 05"]
    G3{"Gate de personas<br/>aprovado?"}
    P4["Parte 4<br/>Expansão e operação<br/>SEQ-12A, depois SEQ-12 a SEQ-16"]
    DONE["Grading E2E concluído"]

    P1 --> G1
    G1 -->|Não| P1
    G1 -->|Sim| P2
    P2 --> C2
    C2 --> G2
    G2 -->|Não| C2
    G2 -->|Sim| P3
    P3 --> G3
    G3 -->|Não| P3
    G3 -->|Sim| P4
    P4 --> DONE
```

Dentro de cada parte, os marcos continuam estritamente sequenciais. Dividir o
plano não autoriza executar marcos em paralelo nem antecipar schema ou
capabilities de uma parte posterior.

## Como executar

1. Executar somente uma parte por vez.
2. Dentro da parte ativa, executar os marcos na ordem apresentada.
3. Não iniciar um marco antes de satisfazer o gate do marco anterior.
4. Abrir PRs menores dentro de um marco quando necessário, sem atravessar o
   gate do marco seguinte.
5. Atualizar primeiro contrato e testes, depois domínio API, persistência
   aprovada, endpoints, web e E2E da mesma fatia.
6. Encerrar a parte com sua suíte acumulada e uma revisão explícita das
   evidências antes de liberar a parte seguinte. Suíte acumulada significa os
   testes da parte atual mais todos os contratos, testes e E2Es aprovados nas
   partes anteriores.
7. Não introduzir aliases, dual-read ou dual-write permanentes. Compatibilidade
   transitória só pode existir com owner, prazo e gate de remoção explícitos;
   preservar uma coluna histórica como shadow property inerte não a torna fonte
   autoritativa.
8. Toda alteração relacional aprovada cria uma migration incremental
   forward-only. As migrations e designers existentes são preservados e o
   snapshot corrente recebe somente o novo delta; um eventual squash do
   baseline global é outra operação, fora deste plano, e exige inventário,
   backup e coordenação de todos os módulos do `ApplicationDbContext`.
9. Interromper a execução em todo `SCHEMA-GATE` para apresentar o impacto e
   obter aprovação explícita antes de editar entidades EF, configurações,
   migrations, snapshot ou tabelas.
10. Não manter dois caminhos autoritativos após uma substituição. O código
    anterior deixa de ler e escrever no mesmo corte em que seu substituto passa
    no E2E; a remoção física de armazenamento segue um gate próprio e pode ficar
    para depois quando sua preservação for necessária a upgrade, rollback ou
    recuperação.
11. `SEQ-16` é auditoria final, não depósito para limpezas conhecidas que
    poderiam ter sido feitas nos marcos anteriores.
12. Depois de toda migration aprovada, testar a criação do banco do zero e o
    upgrade de um banco populado pela migration anterior, executar o diff global
    de modelo e repetir a suíte acumulada. Um gate de schema não está concluído
    enquanto uma parte anterior regredir ou algum dado/artefato desaparecer sem
    autorização.
13. A revisão imutável fixa definição e versões executáveis; cada
    `GradingExecution` fixa separadamente a entrega concreta apresentada ao
    sujeito. A entrega possui `itemOrder` explícito e JSON canônico textual;
    resume ou retry nunca regeneram challenge.
14. A Parte 2 remove produtores diretos antigos e emite os eventos canônicos,
    mas mantém notificações externas e passback desligados. Esses consumers só
    entram em `SEQ-15`.
15. Regrade permanece na revisão, manifest, entrega e respostas originais da
    execução. Avaliar outra definição cria nova submission e nova execução, não
    uma rodada da execução anterior.
16. Projeção interna pode consumir resultado finalizado; toda projeção learner
    consome apenas resultado liberado e não pode expor agregados que permitam
    inferir contribuição retida.
17. Cada evento acadêmico possui confirmação durável por consumer obrigatório;
    uma falha não apaga receipts concluídos nem marca o fan-out inteiro como
    entregue.
18. A evolução incremental preserva todo artefato SQL ativo aprovado fora do
    `IModel`. Snapshot EF e diff de tabelas não substituem o inventário e os
    testes de funções, procedures, triggers, policies, grants, views, extensões
    e índices especiais.
19. `Assessment.DefinitionPayload` e seu setter genérico deixam de ser usados
    pelo runtime. Fonte mutável de policy só existe com contrato tipado, nome
    próprio, ownership exclusivo e aprovação no `SCHEMA-GATE`; as colunas
    históricas permanecem inertes até uma remoção posterior comprovadamente
    segura.
20. Release persiste somente `GradeRoundId` único; a submission é derivada pelo
    owner da `GradingExecution` e validada no comando.
21. Conclusão dependente de aprovação é `on-release-and-pass`; nenhum sinal
    learner-visible pode revelar `Passed` antes do release.
22. Unpublish remove somente o ponteiro ativo, bloqueia novos starts e preserva
    revisões e execuções já iniciadas.
23. Grupo e peso alteram somente a projeção auditada do gradebook. Assessment
    sem grupo tem resultado sem colocação; a mudança não cria grading novo.
24. Release agendado usa comandos idempotentes e o mesmo
    `ReleaseGradeResult`; worker nunca escreve `Released` diretamente.
25. `@game-guild/grading` permanece independente de tipos de assessment.
    Integrações específicas são packages de borda, começando por
    `@game-guild/grading-adapter-quiz`, com dependência simultânea das APIs
    públicas de grading e quiz e sem dependência reversa.
26. A mesma fronteira existe no servidor: o core expõe portas genéricas
    resolvidas pelo manifest, enquanto implementações C# de quiz ficam em um
    adapter registrado no composition root e nunca são importadas pelo core.
27. O gradebook usa uma única fórmula: selecionar a contribuição efetiva de cada
    assessment, somar score e `MaxScore` dentro do grupo, multiplicar a razão
    pelo peso do grupo e somar as contribuições dos grupos, sem renormalização
    implícita. Grupos positivos precisam totalizar `100%` antes de produzir
    resultado global oficial do curso.
28. Capability `OfficialSubmission` nova é exercitada primeiro numa composição
    controlada de E2E. Produção recebe somente as mesmas chaves, versões e
    implementações depois da aprovação desse gate.
29. Policy de release imediato persiste uma solicitação idempotente na mesma
    transação da finalização. Um worker chama `ReleaseGradeResult`; finalização e
    liberação continuam transições e eventos distintos.
30. Aluno, instrutor, colaborador e revisor são personas contextuais, não roles
    de tenant. Todos usam a membership `Member`; enrollment, `CreatorId` e
    permissions por `Program` determinam a capacidade em cada curso.
31. A Parte 3 consome os contratos públicos de `GameGuild.Identity.*` e não
    altera sua semântica, schema ou armazenamento. Qualquer lacuna de plataforma
    é devolvida ao owner do módulo em vez de receber bypass em Learning.
32. `SystemAdmin` serve apenas ao bootstrap administrativo e não participa de
    cenário funcional positivo. API e handler continuam sendo a autoridade;
    ocultar um comando na web não é autorização.
33. Os novos cenários acadêmicos usam `Enrollment` ativa como prova de
    participação. Permission de recurso não substitui enrollment e enrollment
    não concede authoring ou review.
34. Antes dos métodos adicionais, `SEQ-12A` substitui a porta exclusivamente
    síncrona por um lifecycle genérico de stage. O orquestrador reage a
    transições e não cria branches por `ReviewMethod`.
35. Test run multipersona mantém subjects e execuções relacionais separados e
    só conclui quando todas as execuções obrigatórias terminarem; nenhuma delas
    produz efeitos acadêmicos.
36. Regrade reexecuta reviews de sistema, reutiliza por referência evidências
    humanas finalizadas de self/peer e exige nova resolução docente. Nova coleta
    humana é outro caso de uso e nunca ocorre implicitamente.
37. O inbox de transporte do host não é dependência de módulos de domínio.
    Learning expõe portas e comandos idempotentes, e adapters concretos são
    compostos em `GameGuild.API` sem inverter a direção de dependência.
38. Policy autoral de release agendado e schedule operacional por rodada são
    dados distintos. `GradeResultRelease` permanece o fato final append-only;
    qualquer owner de schedule exige `SCHEMA-GATE` e aprovação próprios.

## Política de evolução do schema

Esta seção segue o
[`ADR-20260903-development-database-baseline`](../../architecture/ADR-20260903-development-database-baseline.md).
O plano não congela antecipadamente tabelas de funcionalidades ainda não
implementadas e não autoriza mudanças estruturais silenciosas. Como o contexto
EF é compartilhado por todos os módulos, cada gate inclui uma auditoria global
do modelo, da cadeia de migrations e do catálogo PostgreSQL; somente o delta de
grading aprovado pode mudar o modelo produzido.

Cada fatia que prevê impacto relacional começa por um `SCHEMA-GATE`:

```text
desenhar somente o necessário para a fatia
  -> apresentar tabelas, colunas, constraints, índices e remoções
    -> obter aprovação explícita
      -> adicionar migration incremental forward-only
        -> testar criação limpa e upgrade populado
          -> provar preservação de dados, catálogo e constraints
```

Isso permite aprender com fatias verticais sem reescrever o passado. Ao final,
o repositório mantém a cadeia de migrations capaz de criar o schema do zero e
de atualizar a versão imediatamente anterior. Cada migration deve comparar o
modelo completo e rejeitar drift fora do delta aprovado.

O diff possui duas dimensões obrigatórias: `IModel`/snapshot EF e catálogo
PostgreSQL. Todo SQL ativo fora do modelo deve ser inventariado com arquivo de
origem, owner, dependências, ordem de instalação e teste funcional. Migrations
históricas não são editadas para acomodar o novo delta. Qualquer squash futuro
é uma operação independente, com backup verificável e confirmação de que nenhum
ambiente ativo depende da cadeia anterior.

Ausência de uso no código atual não basta para classificar armazenamento como
removível. O gate de remoção precisa provar que todos os dados relevantes foram
materializados no novo owner, que nenhum produtor ou consumidor continua ativo,
que upgrade e rollback/restore são seguros e que a exclusão foi aprovada
explicitamente. Sem essa prova, a estrutura é preservada e mantida sem autoridade
de runtime.

Para cada alteração proposta, o gate deve informar:

```text
nome proposto
owner do dado
motivo da persistência
operações de leitura e escrita
cardinalidade e lifecycle
constraints e índices
política de retenção
efeito de concorrência
alternativa rejeitada
artefatos SQL fora do IModel afetados e seus testes
```

## Matriz dos marcos

| Parte | Marco | Entrega principal | Depende de | Schema |
| --- | --- | --- | --- | --- |
| 1 | `SEQ-00` | ADRs e decisões fechadas | nenhuma | não |
| 1 | `SEQ-01` | contratos, adapter de quiz, workflows e autorização | `SEQ-00` | não |
| 1 | `SEQ-02` | delta incremental do núcleo aprovado | `SEQ-01` | somente desenho |
| 1 | `SEQ-03` | migration incremental, núcleo e entrega por execução | aprovação de `SEQ-02` | sim, global |
| 1 | `SEQ-04` | autoria transacional no servidor | `SEQ-03` | não previsto |
| 1 | `SEQ-05` | projeção segura, corte learner e capabilities | `SEQ-04` | não previsto |
| 1 | `SEQ-06` | revisão imutável, publish/unpublish preparados e UX autoral | `SEQ-05` | não previsto |
| 2 | `SEQ-07` | runtime e test run isolado com handler controlado | Parte 1 aprovada | não previsto |
| 2 | `SEQ-08` | `InstructorReview` no test run | `SEQ-07` | não previsto |
| 2 | `SEQ-09` | `AutomatedReview` no test run | `SEQ-08` | não previsto |
| 2 | `SEQ-10` | tentativa oficial, progresso, release e gradebook mínimos | `SEQ-09` | `SCHEMA-GATE` |
| 2 | `SEQ-11` | tentativa oficial coletiva | `SEQ-10` | `SCHEMA-GATE` |
| Gate 2 → 3 | `CLOSE-01` | retirar autoridade peer paralela e dependências de submissions irmãs | primeira implementação de `SEQ-11` | não previsto; parar se houver delta |
| Gate 2 → 3 | `CLOSE-02` | E2Es oficiais HTTP + PostgreSQL | `CLOSE-01` | não |
| Gate 2 → 3 | `CLOSE-03` | criação limpa e upgrade real populado | `CLOSE-02` | não; valida migrations existentes |
| Gate 2 → 3 | `CLOSE-04` | suíte acumulada e aprovação do gate | `CLOSE-03` | não |
| 3 | `ACCESS-01` | contrato e matriz contextual de capacidades | fechamento da Parte 2 aprovado | não |
| 3 | `ACCESS-02` | autorização uniforme na API de Learning | `ACCESS-01` | não; interromper se surgir necessidade |
| 3 | `ACCESS-03A` | gates contextuais na web e nomenclatura `StaffReview` | `ACCESS-02` | não |
| 3 | `ACCESS-03B` | delegação operacional para collaborator e reviewer da equipe | `ACCESS-02`; não bloqueia `ACCESS-04` | não; aguarda contrato de Authorization |
| 3 | `ACCESS-04` | fixture de owner, dois learners e outsider com sessões reais | `ACCESS-03A` | não |
| 3 | `ACCESS-05` | E2E principal revalidado por persona | `ACCESS-04` | não |
| 4 | `SEQ-12A` | lifecycle genérico de stage, test run multipersona e regra de regrade | Parte 3 aprovada | não previsto; parar se houver delta |
| 4 | `SEQ-12` | `SelfReview` em teste e oficial | `SEQ-12A` | `SCHEMA-GATE` |
| 4 | `SEQ-13` | `PeerReview` em teste e oficial | `SEQ-12` | `SCHEMA-GATE` |
| 4 | `SEQ-14` | porta durável de `AIReview` | `SEQ-13` | `SCHEMA-GATE` condicional |
| 4 | `SEQ-15` | release agendado, integração global e operação avançados | `SEQ-14` | `SCHEMA-GATE`; schedule possui delta esperado |
| 4 | `SEQ-16` | auditoria e fechamento | `SEQ-15` | não |

`Não previsto` significa que o marco deve usar o schema já aprovado. Se a
implementação demonstrar que falta uma coluna, constraint, índice ou entidade,
o marco para, abre um `SCHEMA-GATE` e somente continua após nova aprovação.

## Organização dos PRs

Um marco pode ser dividido em vários PRs, seguindo esta ordem interna:

```text
contrato e testes
  -> domínio puro
    -> aplicação e autorização
      -> SCHEMA-GATE, quando necessário
        -> persistência aprovada
          -> endpoints
            -> web
              -> integração e E2E
                -> remoção do caminho substituído
```

Cada PR deve declarar:

- parte, marco e subentrega atendidos;
- contrato alterado;
- efeito em schema: `nenhum` ou referência ao `SCHEMA-GATE` aprovado;
- invariantes e linhas da matriz de autorização cobertas;
- testes executados;
- caminho anterior removido na fatia;
- confirmação de que não existe segunda autoridade para o mesmo dado.

Não misturar no mesmo PR:

- migration estrutural ampla e redesign amplo de UI;
- novo review handler e reescrita do lifecycle;
- mudança de contrato sem atualizar produtores e consumidores;
- remoção antes do E2E substituto;
- manutenção do caminho anterior depois do E2E substituto;
- trabalho pertencente a partes diferentes.

## Condições de parada

A implementação deve parar e retornar ao planejamento quando:

1. surgir necessidade de tabela, coluna, índice ou constraint sem
   `SCHEMA-GATE` aprovado;
2. um mesmo dado passar a ter dois owners mutáveis;
3. a UI se tornar responsável exclusiva por autorização ou validade;
4. um review precisar fabricar score para prosseguir;
5. uma chamada externa entrar na transação acadêmica;
6. test run produzir qualquer efeito acadêmico;
7. tentativa coletiva começar a executar grading por participante;
8. finalização e liberação precisarem compartilhar o mesmo evento;
9. um PR exigir mudança relacional, backfill ou compatibilidade transitória sem
   `SCHEMA-GATE` e estratégia de upgrade aprovados;
10. score, peso ou percentual acadêmico exigir `decimal`, `float` ou `double`
    persistido;
11. um caminho substituído continuar autoritativo depois do gate E2E;
12. persona simulada for tratada como ator autenticado ou sujeito oficial;
13. evento acadêmico durável continuar sendo publicado diretamente em processo
    em vez de ser gravado na outbox transacional;
14. uma migration ou atualização do snapshot produzir drift não aprovado em
    outro módulo da API;
15. rota learner/public ainda puder retornar DTO autoral ou answer key;
16. capability `AuthorTest` for usada para autorizar publish ou execução
    `OfficialSubmission`;
17. gradebook aceitar múltiplas tentativas sem política canônica de
    contribuição;
18. test run emitir `GradeResultFinalized` ou `GradeResultReleased`;
19. liberação manual alterar estado sem passar pelo comando idempotente e
    autorizado `ReleaseGradeResult`;
20. uma parte seguinte começar antes da definição de pronto e dos testes da
    parte atual terem sido aprovados;
21. revisão ou execução depender da versão mais recente do deploy em vez do
    `AssessmentExecutionManifestV1` fixado;
22. comando idempotente aceitar a mesma chave com request hash divergente;
23. SpeedGrader, endpoint ou service anterior continuar capaz de atribuir score
    fora do stage/round canônico;
24. um artefato alcançar tráfego sem que o preflight tenha comprovado todas as
    versões exigidas por revisões ativas, revisões retidas elegíveis a regrade
    e execuções não terminais;
25. um `SCHEMA-GATE` terminar sem teste de banco vazio, upgrade populado, diff
    global e suíte acumulada das partes já aprovadas;
26. publish, start ou regrade reconstruir o manifest, trocar seus bytes ou
    alterar o `ExecutionSnapshotHash` da revisão preparada;
27. `AssessmentSubmission.Passed` consultar `Program.PassingScore` em vez do
    `Assessment.PassingScore` absoluto da revisão;
28. rota genérica de `ProgramContent` ou `ContentInteraction` aceitar resposta,
    produzir progresso acadêmico ou criar nota para quiz avaliado;
29. save de draft coletivo de tentativa ou evidência ficar fora do envelope
    idempotente ou duplicar auditoria em replay;
30. fila, tarefa ou SpeedGrader representar grupo por `CanonicalRow` ou
    submissions irmãs depois de `SEQ-11`;
31. start, resume ou retry regenerar valores, prompts públicos, ordenações ou
    outro challenge em vez de reutilizar `AssessmentExecutionDeliveryV1` e seu
    `DeliveryHash` persistidos;
32. o browser puder enviar variáveis, seed, ordem inicial ou outro campo capaz
    de substituir a entrega concreta da execução;
33. rota genérica de complete/update progress ou escrita de `ActivityGrade`
    decidir conclusão ou nota de quiz ligado a assessment;
34. notificação ou passback for conectado antes de `SEQ-15` ou consumir comando
    e service de grading em vez dos eventos canônicos;
35. regrade trocar revisão, manifest, entrega, respostas ou bytes canônicos da
    `GradingExecution` original;
36. `AssessmentExecutionDeliveryV1` depender da ordem de propriedades de
    `items`, não persistir `itemOrder` ou depender de answer key privada aleatória
    não derivável da revisão e da entrega concreta;
37. dashboard, workspace, DTO, client ou agregado learner expor ou permitir
    inferir score de rodada `Withheld` ou `Scheduled`;
38. release de nova rodada apagar a evidência de release da rodada anterior ou
    regrade ocultar implicitamente a última rodada já liberada;
39. evidência insuficiente de `PeerReview` ultrapassar o prazo sem transição para
    `AwaitingInstructorResolution` e sem comando terminal auditável;
40. uma mensagem de outbox for marcada como concluída antes da confirmação
    durável de todos os `ConsumerKey` obrigatórios capturados para ela;
41. uma migration omitir ou remover artefato SQL ativo apenas porque ele não
    aparece no `IModel`, ou o gate não possuir teste funcional desse artefato;
42. `Assessment.DefinitionPayload` ou outro payload genérico continuar como
    segunda fonte mutável da definição;
43. uma linha de release persistir `AssessmentSubmissionId` redundante ou puder
    referenciar rodada pertencente a outra submission ou a um `AuthorTest`;
44. policy dependente de aprovação projetar conclusão, pré-requisito,
    certificado ou outro sinal learner-visible antes do release;
45. unpublish apagar revisão/execução existente ou permitir novo start pela
    revisão despublicada;
46. mudança de grupo ou peso criar round, regrade, release ou evidência, ou
    assessment sem grupo entrar no gradebook;
47. scheduler alterar release diretamente, usar relógio não injetável ou emitir
    mais de um `GradeResultReleased` para a mesma rodada;
48. core C# de assessments/grading importar DTO, parser, entidade ou namespace do
    adapter de quiz, ou resolver quiz por branch específico fora do registry;
49. produção registrar capability `OfficialSubmission` antes de seu E2E
    controlado, ou promover chave, versão ou implementação diferente da
    exercitada;
50. consumer de gradebook usar média de percentuais, renormalizar pesos, fazer
    aritmética SQL ou divergir da fórmula canônica por pontos;
51. policy `immediate` depender de chamada em memória depois do commit, sem
    solicitação durável capaz de sobreviver a queda e retry;
52. uma migration, coluna, tabela, trigger, função ou dado histórico ser removido
    somente por ser considerado antigo, não utilizado ou anterior ao lançamento,
    sem prova de materialização, inventário de consumidores, teste de upgrade e
    aprovação explícita;
53. `Student`, `Instructor`, `Reviewer` ou `TA` ser introduzido como role de
    tenant para representar contexto de curso;
54. código de Learning escrever diretamente em tabelas de permission, forjar
    actor ou criar resolver paralelo para contornar o contrato de Authorization;
55. frontend liberar ação protegida sem confirmação equivalente na API, ou
    considerar elemento oculto como autorização suficiente;
56. teste positivo de owner, learner, collaborator ou reviewer da equipe
    depender de `SystemAdmin`;
57. `Edit`, `Publish`, `StaffReview` ou enrollment promover implicitamente
    outra capacidade sem regra aprovada e teste explícito;
58. `SEQ-12A` começar antes de `ACCESS-05` comprovar as sessões e a matriz de
    acesso contextuais.

## Acompanhamento global

| Parte | Status | Gate de conclusão |
| --- | --- | --- |
| 1. Fundação e autoria | concluída | base contratual, relacional, segura e autoral aprovada |
| 2. E2E principal | concluída | test run e fluxo oficial individual/coletivo aprovados |
| Fechamento 2 → 3 | concluído | `CLOSE-01` a `CLOSE-04` aprovados sem reexecutar toda a Parte 2 |
| 3. Acesso contextual | concluída | `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` aprovados com owner, dois learners e outsider; `ACCESS-03B` não bloqueante |
| 4. Expansão e operação | não iniciada; gate aprovado | começar por `SEQ-12A`; schema de peer e schedule continua sem aprovação |

O detalhe de cada marco é atualizado somente no documento da parte
correspondente. Este índice registra apenas a passagem entre as quatro entregas.
