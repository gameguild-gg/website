# Validação de grading operada exclusivamente pela interface web

Status: implementado.

Data: 2026-10-20.

## Resultado da implementação

- a página `/workspace/learning/grading-validation` é a única superfície
  humana para preparar, inspecionar, verificar e remover cenários;
- o núcleo genérico, o registry e as server actions ficam em
  `apps/web/src/lib/grading-scenarios`, enquanto o lifecycle de quiz permanece
  encapsulado em `adapters/quiz`;
- Playwright prepara e remove cenários pela página protegida, usando sessões
  reais e contextos isolados para as personas;
- os comandos `grading:validation:contracts`, `grading:validation:smoke` e
  `grading:validation:full` iniciam runners de teste, sem CLI operacional;
- a antiga CLI e seu diretório de scripts foram removidos sem camada legada;
- autorização local, ownership, locks, sanitização, IDs estáveis e evidências
  candidatas continuam cobertos;
- a automação inicia uma stack E2E enxuta, sem watchers do SDK ou hot reload da
  API e com heap do Next.js limitado, sem alterar o `dev:fast` interativo;
- nenhuma tabela, configuração de modelo, snapshot ou migration foi alterada.

## Contexto

O `Quiz Grading Scenario Runner` passou a oferecer dois meios de operação:

- uma CLI com `prepare`, `status`, `verify`, `reset`, `list`, `catalog` e
  `test`;
- uma interface local em `/workspace/learning/grading-validation`.

Essa duplicidade não acompanha o padrão predominante do web app. Operações
humanas são normalmente expostas por páginas, componentes, server actions e
clientes oficiais da API. Scripts Node continuam existindo para iniciar uma
suíte automatizada, mas não formam uma segunda superfície administrativa para
criar, inspecionar e remover estado da aplicação.

Além disso, a implementação intermediária fez código em `apps/web/src`
importar módulos de `apps/web/scripts/quiz-grading-scenario`. A aplicação passou
a depender da ferramenta de linha de comando, quando a direção correta é o
teste depender de contratos estáveis da aplicação ou exercitar sua interface.

Este plano substitui a decisão anterior de manter a CLI como porta headless. A
interface web será a única superfície humana e operacional para cenários. As
suítes continuam iniciadas por comandos de teste convencionais, mas deixam de
usar uma CLI de lifecycle de cenário.

## Decisão central

Adotar a seguinte separação:

```text
Pessoa
  -> interface web de validação
    -> serviço genérico de cenários de grading
      -> registry de adapters
        -> adapter de quiz
          -> APIs oficiais

Playwright/Vitest
  -> runner padrão do framework
    -> fixtures privadas e/ou interface web
      -> mesmas APIs e regras observáveis
```

Consequências:

1. Não haverá comando público `grading:scenario`.
2. Não haverá comandos manuais `prepare`, `status`, `verify`, `reset`, `list`
   ou `catalog` no terminal.
3. A aplicação web não importará arquivos de `apps/web/scripts` nem de `e2e`.
4. `grading:validation:smoke`, `grading:validation:full` e testes de contrato
   poderão continuar como comandos que apenas iniciam Playwright, Vitest ou
   xUnit.
5. O núcleo de cenários continuará desconhecendo quiz, code ou qualquer outro
   tipo de assessment.
6. Quiz será conectado exclusivamente por seu adapter.
7. Um futuro code assessment será conectado por outro adapter, sem
   condicionais no núcleo ou na UI.

## Objetivos

1. Tornar a página web a única forma suportada de uma pessoa preparar,
   inspecionar, verificar e remover cenários.
2. Remover a CLI e qualquer documentação que instrua seu uso.
3. Corrigir a direção de dependência entre aplicação e infraestrutura de
   teste.
4. Preservar os cenários, checkpoints, personas, ownership, reset protegido,
   capability catalog e evidências já existentes.
5. Manter Playwright e CI reproduzíveis sem transformar comandos de teste em
   uma nova CLI operacional.
6. Preservar a separação entre grading genérico e integrações específicas de
   assessment.

## Não objetivos

- alterar o domínio de grading, quiz ou review;
- adicionar `grading-adapter-code` nesta etapa;
- criar endpoint de seed ou endpoint exclusivo para E2E;
- acessar PostgreSQL diretamente para preparar cenários;
- criar tabelas, entidades, migrations ou persistência relacional para o lab;
- mover tipos de quiz para o package de grading;
- substituir Playwright, Vitest ou xUnit;
- manter aliases ou wrappers legados para `grading:scenario`;
- transformar a página em uma funcionalidade disponível em produção.

## Leitura do padrão existente

O padrão a preservar é:

```text
page.tsx
  -> componente de UI
    -> server action
      -> contexto autenticado
        -> cliente/serviço oficial
          -> API
```

Os scripts de browser existentes são aceitáveis porque executam uma jornada de
teste fechada e encerram. Eles não expõem um conjunto de operações para uma
pessoa administrar recursos. Assim, a remoção da CLI não significa remover
`pnpm test`, Playwright ou scripts que apenas iniciam uma suíte.

## Invariantes arquiteturais

1. Os contratos, serviços, actions e componentes genéricos de grading
   scenarios não podem importar `@game-guild/quiz`,
   `@game-guild/quiz-content`, `@game-guild/quiz-surface`,
   `@game-guild/grading-adapter-quiz` ou módulos do adapter de quiz. Somente o
   composition root pode importar adapters concretos para registrá-los.
2. Somente o adapter de quiz conhece definições, payloads, question types e
   capabilities de quiz.
3. A UI recebe labels, targets, workflows, checkpoints, links e resultados
   pelos contratos genéricos do adapter.
4. O composition root registra adapters concretos explicitamente.
5. Nenhum arquivo carregado pela aplicação importa de `apps/web/scripts` ou
   `e2e`.
6. Código de browser, reporters e drivers de questão permanece no suporte de
   teste e não entra no bundle da aplicação.
7. Toda mutação usa APIs oficiais, autorização real, tenant real e atores
   adequados.
8. O acesso ao lab falha fechado fora de desenvolvimento local ou sem
   `SystemAdmin`.
9. Reset remove somente recursos comprovadamente pertencentes ao manifesto do
   cenário.
10. O harness não autoriza mudanças de schema. Qualquer necessidade real de
    persistência recebe decisão, plano e migration próprios.

## Organização alvo

```text
apps/web/src/lib/grading-scenarios/
  contracts.ts
  registry.server.ts
  service.server.ts
  access-policy.ts
  access.server.ts
  actions.ts
  adapters/
    quiz/
      adapter.server.ts
      contracts.ts
      definitions.ts
      provision.server.ts
      inspect.server.ts
      verify.server.ts
      cleanup.server.ts
      manifest.server.ts

apps/web/src/components/learning/grading-validation/
  grading-scenario-lab.tsx
  ...componentes auxiliares

apps/web/src/app/[locale]/(dashboards)/workspace/learning/
  grading-validation/page.tsx

e2e/learning/quiz-grading/
  fixtures/
  support/
  *.spec.mjs

packages/features/grading-adapter-quiz/src/testing/
  fixtures e contratos puros reutilizáveis
```

Responsabilidades:

| Área                           | Responsabilidade                                     |
| ------------------------------ | ---------------------------------------------------- |
| `grading-scenarios`            | contratos e orquestração genéricos da interface      |
| `adapters/quiz`                | tradução e lifecycle específico dos cenários de quiz |
| `grading-validation`           | interação humana, estado visual e comandos da página |
| `e2e/learning/quiz-grading`    | jornadas automatizadas, browser drivers e evidências |
| `grading-adapter-quiz/testing` | fixtures puras do contrato quiz -> grading           |

Um futuro adapter seguirá o mesmo desenho:

```text
apps/web/src/lib/grading-scenarios/adapters/code/
  adapter.server.ts
  definitions.ts
  provision.server.ts
  verify.server.ts
  cleanup.server.ts
```

O registro desse adapter será a única alteração necessária no núcleo de
composição. A UI genérica não receberá `if (adapter === "quiz")` ou
`if (adapter === "code")`.

## Paridade antes da remoção

Cada capacidade humana da CLI precisa existir na página antes do corte:

| CLI atual        | Destino web                                             |
| ---------------- | ------------------------------------------------------- |
| `list`           | seletor de adapter/cenário e estados existentes         |
| `catalog`        | visão dos targets, IDs estáveis, capabilities e suporte |
| `prepare`        | ação `Prepare` com cenário, checkpoint e targets        |
| `status`         | estado carregado e ação de atualização                  |
| `verify`         | ação `Verify` e lista de checks                         |
| `reset`          | diálogo de confirmação e reset protegido por ownership  |
| credenciais/URLs | painel de personas e links copiáveis                    |
| `test`           | suíte Playwright, não ação operacional humana           |

`--headed`, filtros de caso e seleção de specs pertencem ao runner de teste e
não precisam ser reproduzidos como controles administrativos.

## Estratégia para testes sem CLI

### Operação humana

A pessoa usa somente:

```text
http://localhost:3000/workspace/learning/grading-validation
```

A página prepara o cenário e apresenta links e credenciais para as jornadas de
instrutor, learners, reviewer e outsider.

### Playwright

Playwright continua sendo iniciado por `pnpm`, mas o comando apenas inicia uma
suíte. A suíte deve possuir lifecycle próprio:

1. um setup autenticado abre a página de validação;
2. seleciona adapter, cenário, targets e checkpoint;
3. aciona `Prepare` e aguarda confirmação observável;
4. coleta somente os links e identidades necessários à execução;
5. abre contextos isolados para cada persona;
6. executa as jornadas e registra evidências;
7. um teardown autenticado usa `Reset`, salvo quando a política de diagnóstico
   mandar preservar o cenário com falha.

Fixtures de baixo nível podem chamar APIs oficiais dentro do processo de teste
quando a camada sob teste não for a interface de preparação. Elas não serão
expostas como comandos independentes nem poderão acessar o banco diretamente.

### Comandos permitidos

Exemplos de entradas que permanecem válidas:

```bash
pnpm grading:validation:contracts
pnpm grading:validation:smoke
pnpm grading:validation:full
```

Esses comandos devem mapear diretamente para runners de teste. Nenhum deles
aceitará subcomandos de gerenciamento de cenário.

## Sequência de implementação

### Fase 1. Caracterização e paridade da página

1. Congelar testes das operações atuais e registrar a baseline.
2. Mapear todos os comandos e opções da CLI para UI ou runner de teste.
3. Completar na página a visualização do capability catalog e dos IDs estáveis.
4. Garantir atualização de status, checks, links, credenciais e checkpoints.
5. Cobrir acessos negados para produção, host remoto, sessão ausente e usuário
   sem `SystemAdmin`.

Gate:

- nenhuma tarefa humana ainda depende do terminal;
- a página oferece paridade com `list`, `catalog`, `prepare`, `status`,
  `verify` e `reset`;
- operações repetidas e concorrentes continuam protegidas.

### Fase 2. Inverter a dependência e separar adapters

1. Manter contratos, registry, serviço, access policy e actions no núcleo
   genérico de `grading-scenarios`.
2. Criar o diretório `adapters/quiz` e mover para ele definições, provision,
   inspect, verify, cleanup e manifesto necessários à página.
3. Remover qualquer import de `apps/web/scripts/quiz-grading-scenario` a partir
   de `apps/web/src`.
4. Separar módulos puros de módulos `server-only`.
5. Fazer o adapter retornar apenas contratos genéricos para a UI.
6. Ampliar os testes de arquitetura para impedir regressões de direção e
   vocabulário.
7. Manter um adapter fictício de `code` nos testes da UI para provar a
   extensibilidade sem implementar code grading.

Gate:

- `apps/web/src` não depende de `apps/web/scripts` nem de `e2e`;
- o núcleo genérico não contém vocabulário ou imports de quiz;
- os cenários existentes continuam preparando, verificando e removendo os
  mesmos recursos.

### Fase 3. Migrar a automação

1. Mover browser drivers, reporters, seleção de casos e coleta de evidências
   para `e2e/learning/quiz-grading/support`.
2. Criar setup/teardown Playwright para o lab web.
3. Substituir invocações de `grading:scenario test` por specs, projects, tags
   ou `--grep` nativos do Playwright.
4. Fazer `grading:validation:smoke` e `grading:validation:full` chamarem
   Playwright diretamente.
5. Fazer o catálogo executável continuar sendo validado por Vitest e pelos
   specs com IDs estáveis.
6. Preservar traces, screenshots, relatórios sanitizados e classificação de
   falhas.
7. Validar interrupção, retomada, teardown após falha e preservação opcional de
   evidência.

Gate:

- smoke e full não chamam `cli.mjs`;
- todas as personas continuam em contextos independentes;
- falhas deixam evidência e não removem recursos alheios;
- nenhuma suíte depende de SQL, `DbContext` ou endpoint de seed.

### Fase 4. Corte atômico da CLI

1. Remover `apps/web/scripts/quiz-grading-scenario/cli.mjs`.
2. Remover scripts `grading:scenario` do `package.json` raiz e de
   `apps/web/package.json`.
3. Remover `grading:validation:catalog` como comando; o catálogo ficará na UI
   e em testes de contrato.
4. Remover ou realocar os demais arquivos do diretório de scripts até que ele
   deixe de existir.
5. Remover exemplos, troubleshooting e protocolo baseados em comandos de
   lifecycle.
6. Não manter wrapper, alias, aviso de depreciação ou compatibilidade legada,
   pois a plataforma não foi lançada.

Gate:

- `rg` não encontra `grading:scenario`, `cli.mjs` ou imports do antigo
  diretório;
- documentação humana aponta somente para a interface web;
- documentação de CI aponta somente para runners de teste.

### Fase 5. Validação final

1. Executar testes de arquitetura do núcleo e dos adapters.
2. Executar testes da página, ações, access policy e estados de erro.
3. Executar contratos de `@game-guild/grading` e
   `@game-guild/grading-adapter-quiz`.
4. Executar os contratos .NET do quiz adapter.
5. Executar smoke Playwright pela nova preparação web.
6. Executar ao menos uma matriz completa com cleanup e uma preservando
   evidência em falha controlada.
7. Executar typecheck, lint e build web aplicáveis.
8. Confirmar que nenhum arquivo de migration, snapshot ou configuração de
   modelo foi alterado.

Gate:

- operação humana, smoke, full e contratos passam sem a CLI;
- a página falha fechada fora do ambiente autorizado;
- nenhuma regressão de quiz/grading ou dependência invertida permanece.

## Arquivos afetados

### Remover

- `apps/web/scripts/quiz-grading-scenario/cli.mjs`;
- entradas `grading:scenario` dos `package.json`;
- documentação de operação humana por terminal;
- antigo diretório de scripts depois da realocação integral.

### Reorganizar

- módulos de definição, manifesto, provision, verify e cleanup;
- capability catalog e fixtures puras;
- browser drivers e geração de evidências;
- scripts `grading:validation:smoke` e `grading:validation:full`.

### Preservar

- rota e componentes da interface web;
- contratos genéricos e registry de adapters;
- ownership, locks e escrita atômica do manifesto local;
- APIs oficiais e autorização existente;
- IDs estáveis do catálogo;
- relatórios e evidências sanitizados.

## Segurança e operação

1. A rota permanece inacessível em `NODE_ENV=production`.
2. Somente hosts locais explicitamente aceitos podem operar o lab.
3. A sessão deve possuir `SystemAdmin`; a ausência de acesso retorna 404.
4. API URL, tenant e ator são definidos no servidor, nunca aceitos do browser.
5. Credenciais de persona são exibidas apenas na página protegida e não entram
   em logs, traces ou relatórios persistentes.
6. Toda mutação é auditável pelas APIs existentes.
7. Reset continua exigindo manifesto válido, tenant correspondente e ownership
   comprovado.
8. Locks impedem duas operações sobre o mesmo cenário.

## Riscos e mitigação

| Risco                                                | Mitigação                                                         |
| ---------------------------------------------------- | ----------------------------------------------------------------- |
| operação longa exceder a requisição da server action | checkpoints persistentes, operação retomável e status consultável |
| Playwright ficar acoplado a detalhes visuais         | locators acessíveis estáveis e helpers concentrados no setup      |
| falha durante cleanup                                | teardown idempotente e manifesto de ownership preservado          |
| código de teste entrar no bundle web                 | teste de arquitetura proibindo imports de `e2e` e `scripts`       |
| adapter futuro exigir condicionais na UI             | contrato genérico e teste com adapter fictício de code            |
| credenciais vazarem em evidência                     | sanitização obrigatória e assertions de redaction                 |
| ferramenta ser exposta em produção                   | gate por ambiente, host e role, todos testados negativamente      |

## Critérios de conclusão

O plano estará concluído quando:

- a interface web for a única superfície humana de lifecycle dos cenários;
- não existir mais `grading:scenario` nem `cli.mjs`;
- `apps/web/src` não importar arquivos de scripts ou E2E;
- núcleo, UI e serviços genéricos permanecerem independentes de quiz;
- quiz estiver inteiramente encapsulado em seu adapter;
- um adapter fictício de code funcionar nos testes sem mudança na UI;
- Playwright preparar e limpar cenários sem uma CLI operacional;
- smoke, full, contratos, typecheck e lint passarem;
- ownership, autorização, tenant, locks e sanitização permanecerem cobertos;
- nenhuma tabela, migration ou regra de domínio tiver sido alterada para
  acomodar a ferramenta.

## Ordem de execução recomendada

Executar as fases em ordem. A CLI só deve ser removida depois que a página
atingir paridade e a automação deixar de depender dela. O corte final deve ser
atômico: não haverá período permanente com duas interfaces nem camada de
compatibilidade legada.
