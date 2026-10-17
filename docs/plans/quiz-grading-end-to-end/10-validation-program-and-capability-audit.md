# Programa de validação e auditoria de capacidades de Quiz + Grading

Status: proposto; pronto para implementação incremental.

Data: 2026-10-17.

## Contexto

As quatro partes do fluxo de grading e o
[`Quiz Grading Scenario Runner`](./09-quiz-grading-scenario-runner.md) já estão
implementados. O runner atual comprova os workflows executáveis de review com
personas reais, checkpoints persistentes e APIs públicas, mas usa somente duas
questões determinísticas em cada quiz: `TRUE_FALSE` e `SINGLE_CHOICE`.

O próximo trabalho não é acrescentar funcionalidades presumidas ao grading. É
medir sistematicamente o produto existente para responder, com evidências:

- quais jornadas funcionam integralmente;
- onde há erro funcional, falha de integração ou regressão visual;
- quais capacidades estão parciais ou ausentes;
- quais comportamentos precisam de decisão de produto;
- quais falhas pertencem ao quiz, ao adapter, ao grading, à autorização, à UI
  ou apenas ao próprio harness de teste.

Este plano cria um programa contínuo de aceitação e auditoria. Ele amplia o
runner existente, organiza testes Playwright por responsabilidade e produz um
catálogo rastreável de capacidades. Uma futura skill de agente poderá executar
o protocolo, mas não será a fonte de verdade: contratos, fixtures, testes e
relatórios versionados continuam sendo autoritativos para pessoas, CI e IA.

## Decisão central

Adotar quatro matrizes ortogonais, em vez de cruzar toda combinação de tipo de
questão, workflow, persona, policy e estado:

1. **Review matrix:** mantém os workflows atuais sobre duas questões simples.
2. **Question-type matrix:** percorre todos os tipos e variantes relevantes de
   quiz usando um workflow representativo para cada capacidade.
3. **Lifecycle and access matrix:** cobre políticas, transições e gates de
   acesso com um conjunto pequeno de questões estáveis.
4. **Failure and recovery matrix:** força replay, concorrência, interrupção e
   estados obsoletos nos pontos críticos.

Essa separação permite atribuir uma falha à dimensão que realmente variou. A
suíte completa não deve materializar o produto cartesiano dessas matrizes.

## Objetivos

1. Manter um inventário executável de todas as capacidades públicas de quiz e
   grading.
2. Cobrir autoria, serialização, entrega ao aluno, resposta, grading, review,
   release e leitura do resultado sem duplicar contratos de domínio no E2E.
3. Permitir a mesma preparação tanto para Playwright quanto para inspeção
   humana persistente.
4. Produzir diagnóstico que diferencie bug, implementação parcial, decisão
   pendente, limitação explícita e falha do ambiente de teste.
5. Detectar regressões de persona, autorização, isolamento de tenant,
   idempotência, auditoria e visibilidade acadêmica.
6. Transformar os achados em uma lista priorizada de trabalho, sem implementar
   silenciosamente decisões de produto durante a avaliação.

## Não objetivos

- reescrever o Scenario Runner;
- criar um segundo modelo de quiz, resposta, grading ou review nos scripts;
- fazer o core de grading depender de tipos de quiz;
- desenvolver provider de IA, infraestrutura de assets ou funcionalidades de
  outros módulos para fazer um teste passar;
- testar todas as combinações possíveis pelo browser;
- usar SystemAdmin como substituto de instrutor, reviewer ou aluno;
- inserir fixtures diretamente por SQL ou `DbContext`;
- introduzir um novo runner, framework de assertions, gerador, mock server,
  scanner ou sistema de relatórios enquanto o stack já adotado cobrir o caso;
- alterar endpoints, tabelas, entidades, snapshots ou migrations como parte
  da infraestrutura de avaliação;
- converter toda lacuna observada em requisito obrigatório do produto.

Se a auditoria demonstrar que uma capacidade exige mudança de persistência, a
mudança deve receber decisão e plano próprios. Quando aprovada, deve incluir a
migration correspondente; este plano não concede autorização antecipada para
alteração de schema.

## Invariantes arquiteturais

1. `@game-guild/quiz` continua dono dos tipos, respostas e avaliação local.
2. `@game-guild/quiz-content` continua dono do documento de conteúdo e seu
   round-trip.
3. `@game-guild/quiz-surface` continua dono dos editores e players.
4. `@game-guild/grading` permanece genérico e desconhece quiz.
5. `@game-guild/grading-adapter-quiz` traduz itens, respostas e capabilities de
   quiz para os contratos genéricos de grading.
6. O Scenario Runner pode conhecer quiz porque é um harness específico de
   integração; essa dependência nunca retorna ao core de grading.
7. Testes browser usam APIs oficiais para preparar estado. A UI é usada para a
   interação sob teste, não como mecanismo universal e lento de seed.
8. Assertions profundas de persistência permanecem em testes de integração
   HTTP/PostgreSQL. Playwright valida contrato observável, não consulta banco.
9. Toda persona usa sessão isolada e autorização real.
10. Dados de cenário têm ownership, reset explícito e proteção de host local.

As invariantes de [`AGENTS.md`](../../../AGENTS.md) continuam obrigatórias,
especialmente pureza de módulos, direção de dependências e autorização de
endpoints.

## Stack de testes adotado

Este programa deve reutilizar exclusivamente ferramentas já presentes no
repositório. A primeira implementação não adiciona dependências de teste.

| Ferramenta existente | Uso neste programa |
| --- | --- |
| Vitest | schemas, round-trips, adapters, scoring e contratos dos packages |
| Testing Library e `user-event` | comportamento dos componentes sem subir a aplicação completa |
| Playwright | jornadas reais, múltiplas personas, traces, screenshots e inspeção de rede |
| xUnit | regras e integração do backend |
| FluentAssertions | assertions legíveis nos testes .NET |
| Testcontainers.PostgreSql | persistência real, transações, concorrência e idempotência |
| AutoFixture | dados auxiliares de testes .NET quando não esconder regras acadêmicas |
| Moq | isolamento de portas externas em unit tests, nunca substituição do fluxo oficial |
| Coverlet | medição de cobertura como sinal auxiliar, não como prova de correção |
| NBomber | carga e estabilidade operacional depois da correção determinística estar comprovada |

Regras de uso:

1. Testcontainers reutiliza a infraestrutura dos integration tests existentes;
   não será criado um segundo ambiente de banco para grading.
2. Testes de persistência criam estado pelas APIs ou serviços públicos sob
   teste. Acesso direto ao banco serve apenas para o harness administrar o
   container ou comprovar invariantes que não possuem projeção pública.
3. NBomber mede concorrência sustentada, latência, throughput e recuperação.
   A correção exata de double submit, idempotência e release é definida antes
   por testes xUnit determinísticos.
4. Mocks ficam nas fronteiras de unit test. As jornadas E2E usam API,
   autenticação, autorização e PostgreSQL reais.
5. Coverage não define o gate isoladamente. Um trecho executado sem assertions
   observáveis continua sendo lacuna de teste.
6. Qualquer ferramenta adicional exige uma lacuna reproduzível que não possa
   ser coberta por este stack e uma decisão separada antes da instalação.

## Diagnóstico da base atual

### O que já existe

- comando `pnpm grading:scenario` com `prepare`, `status`, `verify`, `test`,
  `reset` e `list`;
- manifestos, locks, checkpoints e cleanup protegido;
- personas `instructor`, `learnerA`, `learnerB` e `outsider`;
- `review-matrix` com sete quizzes no mesmo curso;
- jornadas separadas de `AutomatedReview`, `InstructorReview`, `SelfReview`,
  `PeerReview` e combinações finais com instrutor;
- Test Run, tentativa oficial, SpeedGrader, release e gradebook do aluno;
- cenário coletivo verificado por API;
- traces e screenshots preservados em falha;
- fixtures dos 14 tipos em
  `@game-guild/grading-adapter-quiz/testing`.

### Limitações conhecidas antes da auditoria

- o Playwright de grading usa somente `TRUE_FALSE` e `SINGLE_CHOICE`;
- o cenário coletivo é validado pela API e ignorado no browser;
- `AIReview` não é executável sem provider aprovado;
- `ESSAY` é classificado pelo adapter como `instructor-review`;
- `NUMERIC` e `FORMULA` possuem avaliação no domínio de quiz, mas permanecem
  `unsupported` no grading oficial;
- `RATING` sem `correctRating` é `unsupported` no grading oficial;
- `HOTSPOT` positivo depende de uma imagem resolvível, mas a auditoria não
  deve implementar infraestrutura de assets;
- o runner ainda não possui catálogo único de cobertura, IDs estáveis por
  caso, relatório consolidado de lacunas ou orçamento explícito de skips.

Esses pontos são estado inicial, não conclusão da auditoria.

## Modelo de cobertura

### Camada 0: inventário e exaustividade

Testes estáticos devem garantir que cada discriminante de `QuizEntryType` e
cada variante de `FillBlankInputType` possui:

- fixture autoral válida;
- resposta correta e incorreta quando aplicável;
- expectativa de capability oficial;
- driver browser ou justificativa explícita de não aplicabilidade;
- caso de round-trip e redaction learner-safe.

Adicionar um novo tipo ao domínio sem atualizar esse inventário deve falhar no
teste de arquitetura/cobertura.

### Camada 1: contratos de package

Testes Vitest cobrem, sem browser; Testing Library e `user-event` entram apenas
quando o contrato depende de interação de componente:

- schema e validação semântica;
- autoral -> documento -> autoral;
- autoral -> learner-safe sem answer key;
- resposta -> envelope -> resposta;
- classificação pelo adapter;
- score correto, incorreto, parcial, pendente e unsupported;
- valores inteiros canônicos de score;
- comportamento de tolerância e partial credit.

### Camada 2: integração HTTP e runtime

Testes xUnit com FluentAssertions e, quando houver persistência, o
Testcontainers.PostgreSql já adotado confirmam:

- candidate e revisão publicada imutáveis;
- tentativa oficial ligada à revisão correta;
- projeção de cada item para o grading;
- execução do workflow e transições de estado;
- auditoria, idempotência, release e gradebook;
- falhas de autorização e tenant;
- retries e concorrência que exigem observação do servidor.

AutoFixture pode gerar dados periféricos, mas quizzes, policies, rounds,
scores e expectativas acadêmicas permanecem builders/fixtures explícitos.

### Camada 3: Playwright por persona

O Playwright comprova somente o comportamento observável:

- autor consegue configurar, salvar e reabrir;
- aluno recebe a projeção correta e consegue responder;
- reviewer vê a tarefa adequada sem dados proibidos;
- instrutor consegue concluir ou corrigir o resultado;
- release controla nota e feedback exibidos ao aluno;
- navegação, reload e retomada mantêm estado coerente;
- não há respostas HTTP inesperadas ou exceções no browser.

Traces, screenshots, vídeo seletivo e captura de rede usam recursos nativos do
Playwright; este plano não introduz outro recorder ou sistema de E2E.

### Camada 4: aceitação humana orientada

Um cenário persistente oferece um curso completo para inspeção manual. A
execução humana usa o mesmo catálogo e registra observações pelos mesmos IDs de
caso, sem criar uma checklist paralela.

### Camada 5: desempenho e estabilidade operacional

Depois que um fluxo estiver correto nas camadas anteriores, suítes NBomber
reutilizam os padrões dos performance tests existentes para medir:

- submissões simultâneas sem duplicação de efeitos;
- disputa de claim/review e publicação concorrente;
- processamento de filas e schedules sob carga;
- latência de leitura do gradebook após release;
- recuperação depois de pressão transitória.

Essas suítes reportam métricas e violações de thresholds operacionais. Elas não
substituem assertions determinísticas de estado final e não entram no smoke de
todo PR.

## Matrizes canônicas

### 1. Review matrix

Manter o cenário atual e suas questões simples. Ele é responsável por:

- origem do review;
- ordem das etapas;
- tarefas das personas;
- revisão final do instrutor;
- retenção e release;
- score e gradebook;
- anonimato e reciprocidade de peer review.

Tipos complexos de questão não devem ser adicionados a essa matriz.

### 2. Question-type matrix

Criar um curso com quizzes independentes por família de item. Cada teste pode
selecionar apenas um caso, enquanto o modo manual mantém todos no mesmo curso.

| Caso | Tipo/variante | Expectativa oficial inicial |
| --- | --- | --- |
| `single-choice` | `SINGLE_CHOICE` | automated |
| `multiple-choice` | `MULTIPLE_CHOICE` | automated; conjunto exato e limite |
| `true-false` | `TRUE_FALSE` | automated |
| `fill-text` | `FILL_IN_THE_BLANK/TEXT` | automated |
| `fill-number` | `FILL_IN_THE_BLANK/NUMBER` | automated; unidade, precisão e tolerância |
| `fill-dropdown` | `FILL_IN_THE_BLANK/DROPDOWN` | automated |
| `fill-word-bank` | `FILL_IN_THE_BLANK/WORDBANK` | automated |
| `short-answer` | `SHORT_ANSWER` | automated; case sensitivity |
| `essay` | `ESSAY` | instructor review |
| `matching` | `MATCHING` | automated; total e parcial |
| `ordering` | `ORDERING` | automated; total e parcial |
| `categorization` | `CATEGORIZATION` | automated |
| `rating-keyed` | `RATING` com resposta | automated |
| `rating-unkeyed` | `RATING` sem resposta | unsupported/decision-needed |
| `numeric` | `NUMERIC` | unsupported no grading oficial atual |
| `formula` | `FORMULA` | unsupported no grading oficial atual |
| `hotspot` | `HOTSPOT` | automated quando o asset é resolvível |
| `highlight` | `HIGHLIGHT` | automated |

Cada caso automatizável deve possuir ao menos:

- resposta correta;
- resposta incorreta;
- ausência de resposta;
- reload antes da submissão;
- resultado por item e total esperado;
- feedback permitido antes e depois do release.

Casos de partial credit incluem um exemplo intermediário. Casos manual ou
unsupported comprovam a transição pendente correspondente; nunca convertem
ausência de avaliação em score zero.

### 3. Lifecycle and access matrix

Usar um quiz determinístico pequeno para variar:

- rascunho, candidate, publicação e edição posterior;
- Test Run sem efeitos acadêmicos;
- disponibilidade futura, aberta, encerrada e late submission;
- tentativa iniciada, retomada, submetida e repetida;
- limite de tentativas e política de contribuição;
- feedback imediato, pós-submit, pós-fechamento e manual, conforme contratos
  realmente executáveis;
- release automático, manual e agendado;
- reavaliação antes e depois do release;
- grupo com peso zero e positivo;
- sujeito individual e coletivo;
- instrutor proprietário, colaborador com grants, reviewer contextual, aluno
  matriculado e outsider.

Entradas configuráveis mas ainda sem runtime completo são registradas como
`partial` ou `missing`; o runner não fabrica suporte.

### 4. Failure and recovery matrix

Cobrir primeiro os riscos que podem duplicar ou publicar resultado incorreto:

- duplo clique/double submit;
- replay da mesma idempotency key e payload divergente;
- refresh ou encerramento durante tentativa e review;
- dois instrutores alterando a mesma rodada;
- release de rodada obsoleta após regrade;
- worker interrompido antes e depois de persistir efeito;
- schedule vencido processado mais de uma vez;
- indisponibilidade transitória de provider ou passback;
- sessão expirada e autorização removida no meio do fluxo;
- manifesto parcial, API reiniciada e retomada do cenário;
- chamada direta por outsider a URLs e endpoints conhecidos.

Nem todo caso pertence ao Playwright. Concorrência, worker e idempotência
profunda devem ficar em integração HTTP/PostgreSQL; o browser cobre apenas as
consequências observáveis relevantes.

## Contrato de um caso de validação

Todo caso recebe ID estável, por exemplo `QG-ITEM-MATCHING-PARTIAL-001`, e
declara:

```ts
interface QuizGradingValidationCase {
  id: string;
  suite: "review" | "question-type" | "lifecycle-access" | "failure-recovery";
  capability: string;
  personas: readonly string[];
  preconditions: readonly string[];
  actions: readonly string[];
  expected: {
    ui?: readonly string[];
    api?: readonly string[];
    grading?: readonly string[];
    audit?: readonly string[];
    learner?: readonly string[];
  };
  automation: "contract" | "api" | "browser" | "manual";
  requirement: "required" | "recommended" | "exploratory";
}
```

O tipo ilustrativo será implementado com validação runtime no harness. Texto
de documentação pode ser gerado a partir desse catálogo, mas não deve existir
uma segunda lista manual com casos diferentes.

## Classificação de resultados

| Status | Significado |
| --- | --- |
| `pass` | comportamento esperado comprovado |
| `bug` | implementação existente viola contrato definido |
| `partial` | parte da jornada funciona, mas o contrato não fecha integralmente |
| `missing` | capacidade necessária não possui implementação observável |
| `decision-needed` | produto ainda não definiu o comportamento correto |
| `harness-gap` | runner, fixture ou driver impede avaliar o produto |
| `external-blocker` | provider ou módulo externo indispensável não está disponível |
| `not-applicable` | caso não se aplica por decisão explícita de domínio |

Um teste não pode usar `skip` ou `fixme` permanente sem ID de caso, status,
justificativa e issue/decisão associada. Falha de infraestrutura não pode ser
registrada como bug de produto, e falha de produto não pode ser mascarada como
problema do runner.

## Oráculos e evidências

Cada fluxo completo deve combinar pelo menos dois oráculos:

1. **UI:** texto, controle, navegação e estado visível da persona.
2. **API:** lifecycle, item states, score, release e projeções retornadas pelos
   contratos públicos.

Quando aplicável, testes de integração adicionam:

3. **Auditoria:** ator, ação, rodada, motivo e correlação.
4. **Entrega:** gradebook, notificação ou passback sem duplicação.

Evidências por caso:

```text
apps/web/test-results/quiz-grading-validation/
  <run-id>/
    manifest.json
    report.json
    report.md
    evidence/
      <case-id>/
        screenshots/
        trace.zip
        api-snapshot.json
```

Regras:

- screenshot e trace são obrigatórios em falha e opcionais em sucesso;
- snapshots de API são sanitizados e nunca incluem token, cookie ou answer key
  entregue ao aluno;
- relatório registra commit, ambiente, definição e versões dos contratos;
- evidência local continua ignorada pelo Git;
- somente o catálogo e relatórios de auditoria deliberadamente publicados são
  versionados.

## Relatório de capacidade

Criar em `docs/testing/quiz-grading/`:

```text
README.md                 metodologia e comandos
capability-status.md      fotografia revisada do estado do produto
product-heuristics.md     critérios de avaliação comparativa
reports/                  auditorias deliberadamente preservadas
```

`capability-status.md` deve mostrar, por capacidade:

- expectativa;
- estado observado;
- camadas que a comprovam;
- último commit validado;
- evidência;
- owner técnico do domínio;
- issue ou decisão pendente;
- severidade e próxima ação.

O documento não é atualizado automaticamente por qualquer execução local. Um
comando gera um relatório candidato; a atualização da fotografia canônica
exige revisão humana para não transformar flakiness em verdade do produto.

## Heurísticas de produto

A auditoria deve verificar os grupos abaixo com base em práticas comuns de
plataformas de aprendizagem, sem assumir que todos são requisitos obrigatórios:

- autoria, preview, validação e round-trip;
- publicação imutável e edição de nova revisão;
- tentativa, autosave, retomada e limites;
- disponibilidade, prazo, atraso e acomodação;
- randomização e estabilidade da correção;
- correção automática, manual, self e peer;
- partial credit, tolerância e arredondamento;
- regrade, override e auditoria da alteração;
- release de nota, feedback e resposta correta;
- gradebook, peso, grupos e tentativa efetiva;
- notificações e tarefas pendentes;
- acessibilidade por teclado, foco, labels e leitura por tecnologia assistiva;
- privacidade entre alunos e isolamento de tenant;
- idempotência, concorrência, retry e recuperação operacional.

Para cada ausência, o relatório deve classificar a recomendação como:

- **essencial para correção acadêmica**;
- **necessária para operação segura**;
- **melhoria de UX/acessibilidade**;
- **capacidade avançada/opcional**;
- **fora do escopo atual**.

## Organização proposta

```text
apps/web/scripts/quiz-grading-scenario/
  definitions.mjs                 cenários e fixtures executáveis
  capability-catalog.mjs          IDs e expectativas de validação
  question-type-drivers.mjs       respostas browser por tipo
  report.mjs                      relatório estruturado e Markdown
  support.test.mjs                contratos do catálogo e runner

e2e/learning/quiz-grading/
  review-workflows.spec.mjs
  question-types.spec.mjs
  authoring-roundtrip.spec.mjs
  lifecycle-access.spec.mjs
  failure-recovery.spec.mjs

docs/testing/quiz-grading/
  README.md
  capability-status.md
  product-heuristics.md
  reports/
```

O spec atual pode ser movido gradualmente. Não fazer uma renomeação ampla antes
de o novo catálogo preservar todos os casos existentes.

## Cenários novos

### `question-type-matrix`

- um curso;
- quizzes independentes por família de tipo;
- seleção com `--case` e `--question-type`;
- respostas corretas, incorretas e parciais declaradas;
- Test Run e tentativa oficial;
- workflow escolhido pela capability real do item;
- modo manual persistente e testes browser separados.

### `lifecycle-access-matrix`

- um conjunto mínimo de quizzes determinísticos;
- assessments independentes por policy;
- personas e grants explícitos;
- seleção por caso para evitar execução longa.

### `acceptance-lab`

- agrega as definições estáveis das matrizes em um curso navegável;
- voltado à inspeção humana e demonstração;
- deixa dados ativos até `reset` explícito;
- imprime índice de URLs por suíte, caso e persona;
- não cria comportamento próprio nem expectativas diferentes das suítes.

`failure-recovery` pode usar recursos efêmeros próprios quando a mutação deixa
o cenário intencionalmente inconsistente. Ele não deve contaminar o
`acceptance-lab`.

## Personas

O catálogo usa contexto, não roles globais de produto:

- instrutor proprietário: `Member` e criador do curso;
- instrutor colaborador: `Member` com grants específicos;
- reviewer/TA: `Member` com grant contextual de review;
- learner A: enrollment ativo;
- learner B: enrollment ativo para peer/collective;
- outsider: `Member` sem enrollment ou grant;
- admin: somente bootstrap administrativo indispensável.

Self reviewer é o próprio aluno em atividade de `SelfReview`. Peer reviewer é
um aluno revisando a submissão de outro. Reviewer/TA é uma persona distinta e
só entra quando o produto expuser tarefa contextual correspondente.

## Estratégia Playwright

1. Um `BrowserContext` por persona, sem compartilhamento de cookie.
2. Fixtures de autenticação e captura de falhas HTTP comuns a todos os specs.
3. Drivers de item registrados por `QuizEntryType`, com teste de exaustividade.
4. Locators semânticos por role, label e test ID estável quando necessário.
5. Nenhuma espera baseada apenas em timeout; aguardar estado observável.
6. Testes independentes por caso, ainda que compartilhem o curso preparado.
7. Execução serial somente quando casos mutam o mesmo recurso.
8. `trace: retain-on-failure`, screenshot em falha e vídeo apenas para suites
   de diagnóstico em que ele tenha valor.
9. A11y básica integrada aos drivers: foco, teclado, nome acessível e ausência
   de clipping/overlap nos viewports suportados.
10. Desktop é o primeiro gate; mobile entra nos casos cujo fluxo é oficialmente
    suportado, sem duplicar toda a matriz.

## Skill/protocolo para agentes

Depois que catálogo, relatório e comandos estiverem estáveis, documentar um
protocolo de agente compartilhável. Ele deve:

1. ler `AGENTS.md`, o catálogo e a fotografia de capacidades;
2. registrar commit, ambiente e suíte escolhida;
3. preparar ou verificar o cenário sem limpar dados alheios;
4. executar primeiro a camada mais barata capaz de reproduzir o caso;
5. coletar UI, API e auditoria conforme o contrato;
6. classificar o resultado sem modificar o produto;
7. gerar relatório candidato e apontar evidências;
8. separar a fase de correção em outra tarefa/commit;
9. reexecutar o caso e seus vizinhos após a correção;
10. nunca promover `decision-needed` para requisito por inferência própria.

O protocolo deve ficar no repositório e ser referenciado por `AGENTS.md`, para
que continue utilizável por qualquer agente. Uma skill específica do Codex
pode apenas encapsular esses passos; ela não substitui comandos, testes ou
documentação e não deve ser requisito para a CI.

## Fases de implementação

### Fase 1. Catálogo e baseline

- criar os IDs e schema runtime dos casos;
- inventariar os 14 tipos e quatro variantes de fill blank;
- ligar o inventário às fixtures existentes do quiz adapter;
- registrar a capability oficial atual de cada caso;
- criar o formato `report.json` e renderer `report.md`;
- criar `docs/testing/quiz-grading/` com a primeira fotografia;
- registrar skips e blockers atuais sem alterar o produto.

Gate:

- todo `QuizEntryType` aparece exatamente uma vez no inventário base;
- variantes relevantes possuem IDs independentes;
- catálogo inválido ou duplicado falha em teste Node;
- relatório distingue produto, harness e ambiente;
- nenhuma jornada existente do `review-matrix` é alterada.

### Fase 2. Question-type matrix de packages e API

- criar fixtures completas de autoria e respostas a partir das fontes de teste
  existentes;
- fechar round-trip, learner redaction e envelope por tipo;
- testar score correto, incorreto, parcial, pending e unsupported;
- provisionar `question-type-matrix` por APIs públicas;
- verificar candidate, publicação, tentativa, item results e total;
- manter `NUMERIC`, `FORMULA` e `RATING` sem key com expectativa explícita
  atual, sem workaround.

Gate:

- todos os tipos têm resultado de contrato reproduzível;
- ausência de suporte nunca vira zero silencioso;
- answer keys não aparecem no payload learner;
- execução seletiva por caso funciona e reset continua limitado ao manifesto.

### Fase 3. Browser por tipo e autoria

- implementar drivers de resposta para todos os tipos aplicáveis;
- separar `question-types.spec.mjs` de `authoring-roundtrip.spec.mjs`;
- comprovar salvar, recarregar, publicar, responder e restaurar tentativa;
- adicionar correto/incorreto e partial credit onde aplicável;
- validar essay pelo instrutor;
- registrar hotspot positivo como `external-blocker` se não houver fixture de
  asset oficial utilizável, sem desenvolver assets neste plano;
- produzir evidência por case ID.

Gate:

- cada tipo possui teste browser ou justificativa rastreável;
- falha identifica tipo, persona, checkpoint e oráculo;
- autoria e runtime não dependem um do outro para preparar fixture;
- teste humano pode abrir todos os quizzes do curso preparado.

### Fase 4. Lifecycle, acesso e recuperação

- implementar os casos prioritários de policy e acesso;
- cobrir collaborator/reviewer somente pelos grants oficiais existentes;
- adicionar reload/resume, double submit e negações por URL direta;
- complementar com xUnit e Testcontainers.PostgreSql para concorrência,
  stale release, workers e schedules;
- validar auditoria e idempotência nos efeitos acadêmicos;
- adicionar NBomber somente aos fluxos já aprovados deterministicamente,
  mantendo resultados e thresholds fora do smoke de PR.

Gate:

- nenhum ator obtém ação além de seu contexto;
- replay não duplica tentativa, grade, release ou passback;
- rodada antiga não sobrescreve resultado atual;
- falhas operacionais possuem diagnóstico e caminho de retry;
- carga não introduz duplicação e seus limites são reportados separadamente de
  falhas funcionais.

### Fase 5. Acceptance Lab e auditoria de produto

- compor `acceptance-lab` sem duplicar definições;
- gerar índice navegável de URLs, casos e credenciais locais;
- executar a primeira auditoria completa assistida;
- classificar achados pelas heurísticas de produto;
- revisar manualmente a fotografia de capacidades;
- abrir decisões e bugs separados com evidência reproduzível.

Gate:

- uma preparação oferece todo o laboratório humano;
- cada achado referencia case ID, commit, evidência e owner;
- relatório separa requisito essencial, operação, UX e capacidade opcional;
- nenhuma lacuna é corrigida no mesmo commit da auditoria inicial.

### Fase 6. CI e protocolo de agente

- definir smoke pequeno para PR;
- executar suíte completa em agenda ou dispatch manual;
- publicar relatório e traces como artifacts;
- adicionar orçamento de skips/blockers com owner e prazo de revisão;
- documentar o protocolo de agente e referenciá-lo no guia adequado;
- opcionalmente empacotar o protocolo como skill após estabilização.

Gate:

- PRs recebem feedback rápido e determinístico;
- suíte longa não bloqueia toda alteração não relacionada;
- regressão em capacidade já marcada `pass` é visível;
- skill e execução humana produzem o mesmo comando e formato de relatório.

## Perfis de execução

### PR smoke

- contratos de exaustividade;
- review automatizado e automatizado + instrutor;
- tipos representativos: escolha, fill blank, partial credit e essay;
- negação de outsider;
- sem vídeo e com cleanup explícito depois de artifacts.

### Completo agendado/manual

- todas as matrizes automatizadas;
- todos os tipos e personas aplicáveis;
- viewports acordados;
- traces em falha;
- relatório consolidado.

### Operacional agendado/manual

- cenários NBomber isolados do E2E browser;
- ambiente e recursos da máquina registrados no relatório;
- thresholds versionados por cenário;
- nenhum resultado de performance executado em paralelo com outra carga local;
- falha funcional reproduzida primeiro em xUnit/Testcontainers antes de ser
  atribuída à carga.

### Exploração humana

- `acceptance-lab` persistente;
- browser headed e sessões separadas;
- sem cleanup implícito;
- observações registradas por case ID.

## Validação do próprio programa

- testes de schema, IDs duplicados e catálogo incompleto;
- testes de seleção por suíte, tipo e caso;
- teste de que `acceptance-lab` referencia definições existentes;
- teste de sanitização de report e snapshots;
- teste de que skip exige justificativa e issue/decisão;
- teste de retomada depois de processo interrompido;
- teste de que report candidato não sobrescreve fotografia canônica;
- teste de que o runner recusa host remoto e recurso sem ownership;
- teste de que novos discriminantes de quiz quebram a exaustividade.

## Critérios de conclusão

O programa estará implantado quando:

- os 14 tipos e variantes de lacuna estiverem inventariados;
- cada tipo tiver cobertura de contrato e API;
- cada interação suportada tiver driver Playwright ou blocker explícito;
- review, lifecycle, acesso e recovery forem matrizes separadas;
- o mesmo catálogo alimentar automação e inspeção humana;
- relatórios diferenciarem bug, partial, missing, decisão e harness;
- toda falha reproduzível apontar persona, checkpoint, evidência e owner;
- capacidades unsupported permanecerem fail-closed;
- não houver dependência de DB seed, endpoint de teste ou bypass de permissão;
- nenhum módulo genérico de grading adquirir dependência de quiz;
- smoke de PR e suíte completa tiverem perfis distintos;
- a fotografia de capacidades permitir priorizar a próxima implementação com
  base em evidência, não em impressão visual isolada.

## Ordem recomendada após este plano

1. Implementar Fase 1 e revisar o catálogo antes de escrever novos E2Es.
2. Executar Fase 2 para descobrir divergências de contrato sem custo de UI.
3. Implementar Fase 3 por famílias de questão, em lotes pequenos.
4. Corrigir bugs comprovados em commits separados e reexecutar casos vizinhos.
5. Só então ampliar lifecycle, recovery e heurísticas avançadas.

Essa ordem evita atribuir ao Playwright uma falha que já poderia ser localizada
no domínio, adapter ou runtime e mantém o diagnóstico utilizável durante a
evolução do produto.
