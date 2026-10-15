# Quiz Grading Scenario Runner

Status: implementado; matriz multi-quiz adicionada em 2026-10-14.

Data: 2026-10-14.

## Estado da implementação

O runner inicial foi concluído em 2026-10-09. Em 2026-10-14, após a Parte 4,
ele foi expandido para testar os reviews executáveis no mesmo curso, com os
seguintes resultados:

- CLI `grading:scenario` disponível na raiz e em `apps/web`, com os comandos
  `list`, `prepare`, `status`, `verify`, `test` e `reset`;
- manifesto local versionado, escrita atômica, lock por cenário, credenciais
  com permissão restrita, ownership explícito e proteção contra hosts remotos;
- provisionamento e verificação por APIs públicas para `automated`,
  `instructor`, `automated-instructor`, `self`, `self-instructor`, `peer`,
  `peer-instructor` e `collective-automated-instructor`;
- cenário agregado `review-matrix`, que mantém sete quizzes independentes em
  um único curso e permite selecionar um subconjunto com `--quiz`;
- exatamente duas questões determinísticas por quiz, com valor total de dois
  pontos, para manter ações e resultados previsíveis;
- Playwright usando o mesmo manifesto para Test Run, negação do outsider,
  tentativa do learner, SpeedGrader, release e gradebook;
- reset explícito e idempotente, limitado aos recursos registrados e com
  recuperação de manifestos parciais sem recursos remotos;
- workflows individuais executados como testes Playwright separados por quiz,
  ainda que compartilhem o curso, grading group e personas da matriz;
- jornadas de `SelfReview` e `PeerReview`, diretas ou seguidas por
  `InstructorReview`, cobertas no Test Run e na tentativa oficial;
- cenário coletivo validado pela API com uma única submission e uma única
  projeção compartilhada pelos participantes;
- nenhuma alteração de endpoint, schema, tabela, snapshot ou migration.

A jornada visual coletiva permanece fora do runner de browser; sua semântica
acadêmica já é verificada pela API. `AIReview` também não entra na matriz
executável enquanto não houver provider aprovado e registrado: o runner não
substitui essa integração por um resultado artificial.

## Objetivo

Criar uma ferramenta local e automatizável que prepare cenários reproduzíveis
do fluxo de quiz e grading por meio dos casos de uso públicos da aplicação.
Ela deve permitir que uma pessoa abra diretamente o ponto do fluxo que deseja
validar e que o Playwright execute a mesma jornada sem manter uma segunda fonte
de preparação de dados.

O runner é uma infraestrutura de desenvolvimento e teste. Ele não pertence ao
runtime de grading, não adiciona comportamento ao produto e não altera o modelo
relacional.

Este plano é auxiliar ao fluxo de
[`quiz-grading-end-to-end`](./README.md). Seu MVP foi concluído antes da Parte 4,
reduzindo o custo de reproduzir e inspecionar os novos estados de review.

## Problema

Hoje existem três mecanismos úteis, mas separados:

- testes PostgreSQL/HTTP constroem fixtures e comprovam os contratos do
  backend, porém executam em host de teste e descartam o cenário;
- scripts browser criam dados para jornadas específicas, mas normalmente
  executam todo o fluxo e removem os recursos ao terminar;
- a aplicação local permite validar a UX real, mas exige que o desenvolvedor
  recrie manualmente curso, grupo, quiz, revisão, matrícula, tentativa e review.

Isso torna regressões visuais e funcionais caras de reproduzir. Quando o banco
é limpo ou uma preparação manual falha, todo o cenário precisa ser reconstruído.

## Resultado esperado

Um comando deve conseguir preparar um cenário até um checkpoint explícito e
imprimir as credenciais e URLs necessárias para continuar o teste manual:

```bash
pnpm grading:scenario prepare \
  --scenario automated-instructor \
  --checkpoint review-ready
```

O mesmo cenário deve poder ser consultado, verificado, automatizado e removido:

```bash
pnpm grading:scenario status --scenario automated-instructor
pnpm grading:scenario verify --scenario automated-instructor
pnpm grading:scenario test --scenario automated-instructor --headed
pnpm grading:scenario reset --scenario automated-instructor
```

A expansão multi-quiz usa o mesmo contrato operacional:

```bash
# Curso completo para inspeção humana a partir do Test Run
pnpm grading:scenario prepare \
  --scenario review-matrix \
  --checkpoint test-run-ready \
  --fresh

# Todos os workflows como testes Playwright separados
pnpm grading:scenario test --scenario review-matrix --fresh

# Apenas um subconjunto da matriz
pnpm grading:scenario test \
  --scenario review-matrix \
  --quiz self \
  --quiz peer-instructor \
  --fresh
```

`prepare` deixa os dados disponíveis. Limpeza automática é proibida no perfil
manual. `reset` é sempre uma ação explícita e limitada aos recursos registrados
no manifesto daquele cenário.

## Princípios

1. **Uma preparação, dois consumidores.** Teste humano e Playwright usam as
   mesmas definições, o mesmo provisionador e o mesmo manifesto.
2. **APIs reais.** Toda mutação passa pelos endpoints e casos de uso públicos,
   usando `@game-guild/client` ou uma chamada HTTP tipada quando o client ainda
   não expuser o endpoint.
3. **Autorização real.** Contas entram por `sign-up`/`sign-in`; ações são feitas
   pela persona correta. SystemAdmin pode apenas preparar o contexto
   administrativo que não possui fluxo público equivalente.
4. **Checkpoint monotônico.** Um cenário avança por estados conhecidos e nunca
   regride silenciosamente.
5. **Reprodução declarativa.** O manifesto ajuda a retomar o cenário, mas a
   definição versionada permite recriá-lo depois de qualquer reset do banco.
6. **Falha fechada.** Divergência entre manifesto e servidor produz diagnóstico;
   não autoriza escrita direta no banco nem reconstrução destrutiva implícita.
7. **Produção intocada.** Não há endpoint de seed, flag de bypass, componente
   visual de desenvolvimento ou branch de runtime destinado ao runner.

## Limites arquiteturais

- não criar ou alterar tabela, entidade EF, configuração, snapshot ou migration;
- não inserir, atualizar ou remover dados diretamente por SQL ou `DbContext`;
- não adicionar `/dev/seed`, `/test/setup` ou endpoint equivalente à API;
- não desabilitar tenant, membership, enrollment ou course capabilities;
- não armazenar access token da API em arquivo; esses tokens vivem somente na
  memória do processo. Um `storageState` Playwright pode conter cookies de
  sessão e, quando necessário, é tratado como credencial local efêmera, fora do
  Git, com acesso restrito e remoção explícita;
- não usar usuário admin como prova positiva da jornada de professor ou aluno;
- não redefinir contratos de quiz, grading ou review dentro dos scripts;
- refletir apenas reviews já executáveis pelos contratos oficiais. A matriz
  inclui `SelfReview` e `PeerReview` após a conclusão da Parte 4, mas mantém
  `AIReview` fora enquanto não houver provider aprovado;
- não executar contra host remoto no primeiro corte. API e web devem resolver
  para `localhost`, `127.0.0.1` ou endereço explicitamente permitido para o
  ambiente local;
- não apagar banco, volume Docker ou recurso sem ownership registrado no
  manifesto;
- não alterar o comportamento de `pnpm dev`, `pnpm dev:fast` ou dos testes
  existentes.

Se faltar um caso de uso público indispensável, a implementação deve registrar
o bloqueio. Não é permitido contorná-lo com persistência direta.

## Infraestrutura já disponível

O runner deve compor o que o repositório já possui:

- `playwright` para browser, contextos isolados, screenshots, traces e relatório;
- `playwright.config.ts` e `scripts/e2e-stack.mjs` para detectar ou iniciar o
  stack completo;
- `@game-guild/client` para autenticação e chamadas geradas da API;
- helpers de `apps/web/scripts/learning-browser-e2e-support.mjs` para cookies,
  sessão compartilhada e falhas HTTP do browser;
- fixtures determinísticas de `@game-guild/quiz-content` e
  `@game-guild/grading-adapter-quiz`;
- o padrão de fases do `coding-cycle-browser-e2e.mjs` para separar seed,
  jornada, verificação e relatório;
- `node:util.parseArgs`, `node:crypto` e `node:fs` para CLI, identificadores e
  manifesto, sem introduzir framework adicional de linha de comando.

Não adicionar Faker, Fishery, Testcontainers ou outra biblioteca de fixtures.
Dados determinísticos e identificadores por execução são mais úteis
para diagnóstico do que geração aleatória ampla.

## Organização proposta

```text
apps/web/scripts/quiz-grading-scenario/
  cli.mjs
  definitions.mjs
  manifest.mjs
  api.mjs
  provision.mjs
  verify.mjs
  browser.mjs
  cleanup.mjs
  support.test.mjs

e2e/learning/
  quiz-grading-scenario.spec.mjs

apps/web/test-results/quiz-grading-scenarios/
  <scenario-key>/
    manifest.json
    report.json
    evidence/
```

Responsabilidades:

- `cli.mjs`: parsing, dispatch dos comandos, códigos de saída e apresentação;
- `definitions.mjs`: catálogo declarativo e versionado de cenários;
- `manifest.mjs`: leitura, validação, escrita atômica e lock local;
- `api.mjs`: autenticação das personas e adaptação fina do client gerado;
- `provision.mjs`: avanço monotônico entre checkpoints;
- `verify.mjs`: leitura do estado autoritativo e invariantes por checkpoint;
- `browser.mjs`: sessões Playwright e URLs por persona;
- `cleanup.mjs`: remoção em ordem inversa, somente pelos casos de uso públicos;
- spec Playwright: jornadas visuais que consomem cenário e manifesto, sem criar
  sua própria fixture.

Os módulos podem ser consolidados durante o primeiro corte se permanecerem
pequenos. As fronteiras acima representam ownership, não uma exigência de um
arquivo para cada nome.

## Definição declarativa de cenário

Uma definição versionada deve conter somente intenção reproduzível:

```ts
interface QuizGradingScenarioDefinitionV2 {
    schemaVersion: 2;
    key: string;
    title: string;
    gradingGroup: {
        title: string;
        weightPercent: number;
    };
    quizzes: Array<{
        key: string;
        title: string;
        subject: "individual" | "collective";
        reviewMethods: readonly AssessmentReviewMethod[];
        release: "automatic" | "manual";
        document: QuizContentDocument;
        correctAnswer: QuizAnswerEnvelope;
        expected: {
            maxScore: ScoreValue;
            automatedScore: ScoreValue;
            selfReviewScore: ScoreValue;
            peerReviewScore: ScoreValue;
            instructorScore: ScoreValue;
        };
    }>;
}
```

O tipo ilustrativo deve ser ajustado aos contratos públicos reais durante a
implementação. Valores acadêmicos usam as unidades inteiras canônicas já
adotadas pelo grading; a definição não cria conversões paralelas.

### Cenários disponíveis

| Chave                             | Workflow                              | Finalidade                                               |
| --------------------------------- | ------------------------------------- | -------------------------------------------------------- |
| `automated`                       | `AutomatedReview`                     | correção determinística e conclusão sem professor        |
| `instructor`                      | `InstructorReview`                    | correção integral pelo professor                         |
| `automated-instructor`            | `AutomatedReview, InstructorReview`   | resultado automático seguido de revisão docente          |
| `self`                            | `SelfReview`                          | autoavaliação do aluno como resultado primário           |
| `self-instructor`                 | `SelfReview, InstructorReview`        | autoavaliação seguida de revisão docente                 |
| `peer`                            | `PeerReview`                          | dois alunos revisam anonimamente a tentativa um do outro |
| `peer-instructor`                 | `PeerReview, InstructorReview`        | agregado peer seguido de revisão docente                 |
| `collective-automated-instructor` | workflow combinado e sujeito coletivo | uma submission e um resultado compartilhados             |
| `review-matrix`                   | sete workflows individuais            | um único curso para execução seletiva ou agregada        |

Cada quiz usa a mesma fixture mínima de duas questões determinísticas,
`TrueFalse` e `SingleChoice`, com um ponto cada. A variação entre cenários fica
restrita ao workflow de review, evitando que tipo de questão e método de review
mudem simultaneamente. Tipos adicionais de quiz entram em uma expansão futura.

`AIReview` não é cenário executável sem provider registrado. Seu gate de
capability deve continuar falhando fechado, em vez de o runner simular uma IA.

## Checkpoints canônicos

Cada checkpoint inclui todos os anteriores:

| Checkpoint        | Estado autoritativo esperado                                            | Tela humana principal           |
| ----------------- | ----------------------------------------------------------------------- | ------------------------------- |
| `authoring-ready` | curso, grupo e quiz criados; conteúdo ainda editável                    | editor de conteúdo              |
| `test-run-ready`  | draft válido e candidate preparado; ainda sem efeito acadêmico          | editor do assessment e Test Run |
| `learner-ready`   | revisão e curso publicados; learner com enrollment ativo; sem tentativa | atividade em `/learn`           |
| `review-ready`    | tentativa oficial enviada e aguardando `InstructorReview`               | SpeedGrader                     |
| `release-ready`   | resultado finalizado e retido pela policy                               | release no SpeedGrader          |
| `released`        | resultado liberado e projeções acadêmicas processadas                   | nota/feedback do aluno          |

Nem todo workflow percorre todos os checkpoints. Por exemplo, `automated` só
produz `review-ready` se houver resolução pendente prevista pela policy; caso
contrário, `prepare` deve explicar que o checkpoint não é aplicável em vez de
fabricar um estado impossível.

### Semântica de avanço

- `prepare` sem manifesto cria o cenário e avança até o alvo;
- `prepare` com manifesto válido consulta o servidor e continua do checkpoint
  efetivamente confirmado;
- pedir checkpoint anterior não desfaz estado e retorna diagnóstico;
- `--fresh` equivale a `reset` seguro seguido de nova preparação;
- falha no meio preserva manifesto parcial e próxima ação sugerida;
- cada comando mutável envia idempotency key estável, armazenada no manifesto
  antes da chamada para que retry do processo não duplique efeitos;
- versões, hashes e IDs retornados pela API são persistidos imediatamente por
  escrita atômica em arquivo temporário seguida de rename.

## Manifesto local

O manifesto representa uma execução local, não a fonte autoritativa:

```ts
interface QuizGradingScenarioManifestV2 {
    schemaVersion: 2;
    definitionKey: string;
    quizKeys: string[];
    runId: string;
    createdAt: string;
    updatedAt: string;
    environment: {
        apiBaseUrl: string;
        webBaseUrl: string;
        tenantId: string;
    };
    checkpoint: string;
    personas: {
        instructor: LocalPersonaReference;
        learnerA: LocalPersonaReference;
        learnerB?: LocalPersonaReference;
        outsider?: LocalPersonaReference;
    };
    resources: {
        courseId: string;
        courseSlug: string;
        gradingGroupId: string;
        enrollmentIds: string[];
        quizzes: Record<
            string,
            {
                checkpoint: string;
                contentId: string;
                assessmentId: string;
                candidateRevisionId?: string;
                publishedRevisionId?: string;
                peerReviewIds: Record<string, string>;
                submissions: Record<
                    string,
                    {
                        submissionId?: string;
                        gradingExecutionId?: string;
                        gradeResultId?: string;
                    }
                >;
            }
        >;
    };
    commands: Record<string, { idempotencyKey: string; requestHash: string }>;
    urls: Record<string, string>;
}
```

Regras:

- diretório inteiro ignorado pelo Git;
- arquivo com permissão local restrita quando suportado;
- e-mail e senha das personas locais reservadas podem ser mantidos para o teste
  humano; `reset` remove os recursos do cenário, mas reutiliza essas contas;
- access tokens, cookies e secrets da aplicação nunca são persistidos;
- manifesto registra um marcador de ownership criado também no título/slug dos
  recursos, permitindo confirmar que `reset` não está apagando recurso alheio;
- versão desconhecida do manifesto falha com instrução de recriação, sem
  conversão ou legado, pois a ferramenta ainda não foi lançada.
- cada quiz possui checkpoint e recursos próprios; o checkpoint do manifesto é
  o menor deles e nunca afirma que toda a matriz avançou além do quiz mais
  atrasado;
- a seleção `quizKeys` é imutável durante uma execução. Alterá-la exige
  `reset`, impedindo que um manifesto parcial seja reinterpretado.

## Personas e sessões

Cada cenário individual deve preparar:

- instrutor proprietário: `Member` e `CreatorId` do curso;
- learner A: `Member` com enrollment ativo;
- outsider: `Member` sem enrollment nem grant, usado nas negações essenciais;
- SystemAdmin: somente bootstrap administrativo quando inevitável.

O cenário coletivo adiciona learner B e cria o sujeito coletivo pelos casos de
uso oficiais. Nenhuma persona é representada por role de produto persistida.

Para browser:

- um `BrowserContext` por persona;
- autenticação pelo fluxo real e `storageState` apenas em diretório local de
  resultados;
- contextos não compartilham cookies;
- `storageState` é um secret efêmero: não aparece em logs ou relatórios, possui
  permissão local restrita quando suportado e é removido por `reset`;
- o relatório identifica qual persona realizou cada ação.

## Comandos

### `prepare`

- valida stack e configuração local;
- cria ou retoma o cenário;
- avança até `--checkpoint`;
- executa `verify` ao terminar;
- imprime resumo, credenciais locais reservadas e links por persona;
- não abre browser nem remove dados automaticamente.

### `status`

- lê o manifesto sem mutar o servidor;
- consulta recursos suficientes para detectar ausência ou divergência;
- mostra checkpoint declarado, checkpoint confirmado e próxima ação possível.

### `verify`

- comprova invariantes do checkpoint na API;
- valida tenant, ownership, enrollment, lifecycle, workflow, subject,
  submission, execução, release e projeção conforme aplicável;
- retorna código diferente de zero em divergência;
- nunca corrige automaticamente o cenário.

### `test`

- usa o cenário existente ou chama `prepare` quando o alvo for informado;
- executa Playwright headless por padrão e aceita `--headed`;
- registra screenshots nas transições relevantes, trace em falha e relatório
  com assertions de UI e API;
- preserva cenário em falha por padrão para diagnóstico;
- aceita `--cleanup` somente como opção explícita para execução automatizada.

### `reset`

- exige manifesto válido, host local e correspondência do marcador de ownership;
- remove recursos em ordem inversa usando APIs públicas;
- tolera replay de remoção de recurso já ausente;
- não remove outro cenário, contas conhecidas, banco ou volume;
- mantém um relatório final e só então remove credenciais e `storageState`.

### `list`

- lista definições disponíveis e execuções locais;
- mostra checkpoint, saúde e URLs principais sem expor senha por padrão.

## URLs apresentadas ao desenvolvedor

Ao final de `prepare`, o resumo deve incluir no mínimo:

- editor do conteúdo do quiz;
- editor do assessment;
- Test Run do professor, quando preparado;
- atividade oficial do learner;
- SpeedGrader apontando para assessment e submission, quando disponível;
- tela de notas/resultados do learner após release.

As URLs devem ser derivadas de IDs/slugs retornados pela API e centralizadas em
uma função testada. Nenhum teste browser deve reconstruí-las de outra maneira.

## Automação Playwright

O spec reutiliza o manifesto e cria um teste serial independente por quiz. A
execução pode selecionar um quiz, um subconjunto ou toda a matriz e cobre:

1. instrutor abre assessment e encontra workflow e revisão esperados;
2. professor executa Test Run sem criar enrollment, submission oficial ou
   gradebook;
3. learner matriculado abre a atividade e outsider recebe negação adequada;
4. learner inicia, responde e envia a tentativa oficial;
5. as duas respostas determinísticas são enviadas por cada learner necessário;
6. `SelfReview` é submetido pelo próprio aluno quando configurado;
7. em `PeerReview`, learner A revisa B e learner B revisa A por contextos
   isolados e pelo workspace anônimo;
8. workflows com `InstructorReview` aparecem no SpeedGrader aguardando o
   professor, que resolve os dois itens;
9. release respeita a policy manual configurada;
10. learner só enxerga nota e feedback depois da liberação;
11. `verify` confirma API, workflow, score e projeção acadêmica por quiz.

O Playwright valida UX e integração browser. Assertions profundas de
persistência continuam nos testes PostgreSQL/HTTP; o runner não deve duplicar
todo o catálogo desses testes pelo browser.

## Perfis de execução

### Manual persistente

- usa o stack local já em execução;
- cria um namespace único por cenário;
- deixa dados e manifesto ativos após o comando;
- permite retomar em outro dia ou recriar após reset do banco;
- não executa teardown implícito em `SIGINT`.

### Automatizado

- usa identificador único por execução para permitir paralelismo;
- pode reutilizar `scripts/e2e-stack.mjs` para garantir o stack;
- prepara somente o checkpoint necessário ao spec;
- preserva recursos em falha, salvo configuração explícita de CI;
- em CI, limpa ao final depois de salvar relatório e evidências.

O MVP não precisa iniciar um PostgreSQL descartável próprio. Esse isolamento
pode ser adicionado depois sem alterar definições ou provisionador.

## Segurança operacional

- lock atômico por `scenario-key` impede duas preparações concorrentes do mesmo
  cenário local;
- `reset` recusa `apiBaseUrl` remoto ou divergente; se o banco local tiver sido
  recriado, confirma a mudança de `tenantId` pela identidade reservada antes de
  descartar o manifesto obsoleto, enquanto falhas de credencial no mesmo tenant
  continuam fechadas;
- chamadas HTTP registram método, rota sanitizada, status e correlation ID, mas
  nunca authorization header, cookie ou senha;
- erros de quota e permission são exibidos integralmente como falha do cenário;
- credenciais locais são estáveis por persona, nunca reutilizam usuário humano
  e só podem operar contra ambiente local;
- a proteção de endpoints continua sendo responsabilidade da API; o runner não
  considera gate visual como autorização;
- recursos parciais permanecem listados no manifesto até remoção confirmada.

## Fases de implementação

### Fase 1. Contratos, CLI e manifesto

- criar o diretório e comandos `list`, `status` e estrutura de `prepare`;
- definir schema V1 de definição e manifesto;
- implementar escrita atômica, lock e proteção de host local;
- cadastrar os três cenários individuais;
- adicionar testes Node para parsing, paths, lock, transições e proteção de
  limpeza;
- adicionar scripts `grading:scenario` na raiz e no app web sem mudar comandos
  existentes.

Gate:

- CLI funciona sem stack para `list` e diagnóstico de `status`;
- manifesto inválido ou host não local falham antes de qualquer mutação;
- não há dependência nova sem justificativa explícita.

### Fase 2. Provisionamento de autoria

- autenticar instrutor e criar ou reutilizar as contas locais reservadas;
- criar curso, grading group, conteúdo de quiz e assessment vinculado;
- salvar definição de quiz pela rota oficial de content authoring;
- configurar workflow, score, peso e release pela rota oficial do assessment;
- preparar candidate e comprovar Test Run;
- entregar `authoring-ready` e `test-run-ready`;
- implementar `verify` desses checkpoints.

Gate:

- uma execução nova chega a Test Run com um comando;
- rerun idêntico não duplica recursos ou candidate;
- Test Run não cria efeitos acadêmicos;
- usuário consegue fechar o processo e continuar manualmente pelas URLs
  impressas.

### Fase 3. Jornada oficial e checkpoints

- publicar revisão e curso;
- criar enrollment pela rota que alimenta o gate canônico de learner;
- entregar `learner-ready`;
- iniciar e enviar tentativa oficial com a conta do learner;
- avançar para `review-ready`, `release-ready` e `released` conforme workflow;
- implementar verificações de outsider e isolamento entre personas;
- adicionar cenário coletivo usando o mesmo provisionador.

Gate:

- checkpoints refletem estados reais, não apenas IDs presentes;
- automated, instructor e combinado chegam ao resultado esperado;
- cenário coletivo mantém uma submission e um resultado compartilhados;
- nenhuma etapa usa SystemAdmin para representar learner ou instructor.

### Fase 4. Browser e evidências

- criar o spec Playwright consumindo um manifesto existente;
- adicionar autenticação isolada por persona;
- cobrir Test Run, tentativa, SpeedGrader, release e resultado do learner;
- implementar screenshots, trace, relatório e `--headed`;
- fazer `test` reutilizar o provisionador em vez de possuir seed próprio;
- acrescentar execução de smoke apropriada à CI sem tornar a suíte completa
  obrigatória em todo commit.

Gate:

- o mesmo cenário pode ser continuado manualmente ou executado pelo browser;
- falha visual deixa dados e evidências suficientes para reprodução;
- nenhum spec duplica payload de criação de curso, quiz ou assessment.

### Fase 5. Reset e documentação

- inventariar e testar os deletes públicos necessários;
- implementar limpeza reversa, idempotente e protegida por ownership;
- documentar preparação manual, retomada, automação e recuperação de falha;
- registrar como novos reviews da Parte 4 adicionam definições e assertions sem
  alterar o núcleo do runner.

Gate:

- reset não afeta recursos fora do manifesto;
- cenário pode ser recriado integralmente depois de `dev:reset:data`;
- documentação possui exemplos para professor, learner e CI.

## Testes do próprio runner

### Unitários sem stack

- argumentos obrigatórios e combinações inválidas;
- schema e versão de definição/manifesto;
- grafo monotônico de checkpoints;
- escrita atômica e retomada depois de manifesto parcial;
- locks concorrentes;
- guard de host local;
- ownership e ordem de cleanup;
- construção centralizada de URLs;
- sanitização de logs.

### Integração com API local

- primeira preparação;
- replay depois de sucesso;
- retomada depois de falha intermediária;
- manifesto cujo recurso foi removido externamente;
- credencial inválida;
- tenant divergente;
- permission e quota negadas;
- reset repetido.

### Browser

- ao menos um E2E completo para `automated`;
- ao menos um E2E completo para `automated-instructor`;
- smoke de `instructor` até `review-ready` e conclusão pelo professor;
- E2Es completos de `self`, `self-instructor`, `peer` e
  `peer-instructor`, separados por quiz e também executáveis na matriz;
- negação de outsider;
- reload/resume nos pontos de tentativa e review;
- evidência preservada em falha.

## Critérios de conclusão

O plano estará concluído quando:

- um desenvolvedor puder chegar a qualquer checkpoint aplicável com um comando;
- o comando imprimir credenciais e URLs suficientes para teste humano;
- o cenário sobreviver ao encerramento do runner e puder ser retomado;
- a mesma definição puder recriar o cenário após limpeza do banco;
- reexecução não duplicar efeitos nem avançar silenciosamente além do alvo;
- Playwright consumir exatamente o mesmo cenário e produzir evidências úteis;
- automated, instructor, self e peer, diretos ou seguidos por instrutor, forem
  comprovados em testes separados e na execução agregada;
- o cenário coletivo for comprovado sem duplicar submission ou resultado;
- reset remover somente recursos de ownership confirmado;
- não houver endpoint de seed, acesso direto ao banco, bypass de autorização,
  mudança de schema ou migration;
- testes preexistentes de grading, quiz adapter e web continuarem aprovados.

## Relação com a Parte 4

O runner não implementa reviews; ele exercita os contratos entregues pela
Parte 4. Cada novo review executável deve acrescentar:

- uma definição de cenário compatível com a nova capability;
- os checkpoints que realmente se aplicam ao método;
- assertions da evidência e do estado de espera;
- uma jornada browser apenas quando houver interação humana correspondente.

`SelfReview` e `PeerReview` foram adicionados depois que seus contratos oficiais
se tornaram executáveis. `AIReview` permanece fora até existir provider real
aprovado e registrado. Dessa forma, o runner evidencia capacidades reais e não
cria mocks que façam um workflow incompleto parecer disponível.
