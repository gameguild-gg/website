# Parte 3. Acesso contextual e personas reais

Status: implementada em 2026-10-08 no caminho crítico. `ACCESS-01`,
`ACCESS-02`, `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` estão concluídos. A
delegação de acesso para colaborador ou revisor da equipe permanece em
`ACCESS-03B`, como trilho operacional externo e não bloqueante.

## Objetivo

Comprovar o fluxo entregue nas Partes 1 e 2 com usuários autenticados que
representem os contextos reais do curso, antes de expandir os métodos de review.
Esta parte não cria roles de produto. Ela compõe a membership `Member` já
existente com matrícula ativa, autoria do curso ou permissions por recurso.
`SelfReview`, `PeerReview` e `InstructorReview` são etapas do workflow de
grading, não personas nem permissions globais. A permission de curso para
review representa exclusivamente a atuação da equipe acadêmica.

Regras globais: [`08-implementation-sequence.md`](../08-implementation-sequence.md).

## Estado encontrado

- registro local e OAuth já provisionam membership `Member` no tenant padrão;
- a jornada `/learn` já exige autenticação e usa enrollment para acesso ao
  curso, tentativa oficial e leitura do resultado;
- assessments já reconhecem parcialmente `CreatorId` e
  `Program.{courseId}.Review`;
- a consulta web `canEditCourse` já combina owner e resource permission, mas o
  layout autoral e o SpeedGrader ainda não consomem uma projeção única de
  capacidades;
- checagens de curso estão duplicadas entre filtros genéricos, controllers e
  web e podem divergir para owner, collaborator e reviewer da equipe;
- o E2E PostgreSQL de grading já usa atores distintos, porém autenticação por
  headers de teste, seed direto no banco e a string de role `Instructor` não
  comprovam a sessão real nem a composição contextual pretendida.

Portanto, esta parte integra e prova o sistema existente. Ela não o substitui.

## Limites arquiteturais

- não criar roles `Student`, `Instructor`, `Reviewer` ou `TA`;
- não criar tabela, coluna, migration, permission store ou sistema de sessão;
- não alterar a semântica de tenant, membership, actor context ou DAC mantida
  pelos módulos `GameGuild.Identity.*`;
- obter tenant exclusivamente do request/actor context; route, body e query não
  podem escolher o tenant autoritativo da operação;
- consumir somente os contratos públicos existentes de autenticação, tenant e
  autorização. Um defeito nesses contratos deve ser registrado para o owner do
  módulo, não contornado dentro de Learning;
- manter toda composição de course access no domínio `GameGuild.Learning.*` ou
  na borda web. Módulo de plataforma não pode depender de Learning;
- resolver participação acadêmica ativa pelos contratos de matrícula já
  existentes (`ProgramEnrollment` e a integração de `Learning.Enrollments`),
  sem criar um terceiro estado de aluno;
- manter a API como autoridade. Gates no frontend melhoram a UX, mas não
  substituem autorização em endpoint ou handler;
- manter `SystemAdmin` apenas na preparação administrativa. Nenhuma prova
  funcional positiva pode depender desse bypass;
- autorizar `SelfReview` pela relação entre ator, sujeito e submission, e
  `PeerReview` pela atribuição específica de `AssessmentPeerReview`; nenhuma
  dessas etapas pode conceder ou exigir permission ampla de review do curso;
- não alterar score, review workflow, release, gradebook ou runtime de grading,
  salvo para substituir uma checagem de acesso inconsistente pela política
  contextual aprovada nesta parte.

Se a implementação demonstrar necessidade de schema, a parte para e apresenta
o problema separadamente. Não há `SCHEMA-GATE` planejado para `ACCESS-01` a
`ACCESS-05`.

## Estado da execução em 2026-10-08

Foi implementado sem alteração de schema, migration ou módulo de plataforma:

- `CourseCapability` para `Learn`, `Edit`, `Publish`, `StaffReview` e acesso ao
  workspace, com projeção única `CourseAccessCapabilities`;
- `CourseAccessEvaluator` no domínio Courses, compondo actor/tenant,
  membership, `CreatorId`, enrollment ativa e grants exatos
  `Program.{courseId}.{Edit|Publish|Review}`;
- reader único de matrícula para o gate contextual, priorizando a matrícula
  canônica `ProgramEnrollment` e reconhecendo a integração atual de
  `Learning.Enrollments`, em coerência com o runtime oficial;
- endpoint autenticado `GET /v1/courses/{courseId}/access/capabilities` e filtro
  fail-closed reutilizado pelos controllers de Courses e Assessments;
- revalidação de enrollment ativa no início, continuação, envio e consulta das
  tentativas oficiais;
- separação entre `Edit`, `Publish` e `StaffReview` em assessments, grading
  queue, release, interações, tarefas e notificações;
- projeção server-side consumida pelos layouts e pela navegação web; o
  SpeedGrader, as submissões e os comandos de correção exigem `StaffReview`,
  enquanto autoria exige `Edit`;
- listagem autoral limitada a cursos próprios ou compartilhados e fixtures
  relacionais ajustadas para membership `Member`, sem role `Instructor` como
  fonte de autoridade;
- testes da matriz contextual, negações cross-tenant, enrollment inativa,
  grants sem promoção implícita, resolução do identificador por rota/query e
  gates da web;
- nomenclatura de código refinada para `StaffReview` e `CanReviewAsStaff` na
  API, projeção web e testes. O grant persistido continua sendo
  `Program.{courseId}.Review`, preservando o contrato oficial de Authorization.

### Fechamento do caminho crítico

- a fixture PostgreSQL passou a oferecer uma factory separada com a
  autenticação real da aplicação, sem remover o handler sintético usado pelos
  testes rápidos existentes;
- owner, aluno A, aluno B e outsider são registrados e autenticados por
  `sign-up`/`sign-in`, cada um com seu token real e membership `Member`;
- o bootstrap administrativo cria somente o tenant padrão exigido pela fixture
  de teste, que desabilita a inicialização de startup. Contas, cursos,
  matrículas, quizzes, assessments, tentativas, reviews e release usam os casos
  de uso HTTP oficiais;
- o E2E
  `ContextualAccessAndGradingRealSessionPostgreSqlHttpTests` comprova as duas
  políticas atuais (`AutomatedReview` e
  `AutomatedReview + InstructorReview`), isolamento entre alunos, outsider,
  resultado retido, release, revogação de matrícula, troca de contexto por
  curso e negação cross-tenant;
- nenhuma tabela, migration, role de produto ou módulo de plataforma foi
  alterado.

### Bloqueio operacional não crítico

O contrato público atual de Authorization não permite concluir com segurança a
atribuição de colaborador/revisor pelo owner do curso:

1. a decisão canônica existente (`IPermissionQueryService`) consulta grants
   codificados em `TenantPermission`;
2. `ResourcePermissionsController` e `IResourcePermissionService` gravam
   `ResourceUserPermission`, que não alimenta essa consulta;
3. os commands de resource permission exigem grants `.Share`/`.Admin`, não
   reconhecem `Program.CreatorId` e ainda expõem `GrantedByUserId` ou
   `UpdatedByUserId` no payload;
4. os commands de `TenantPermission` gravam no store consultado, mas só aceitam
   tenant admin; portanto não implementam o caso de uso de um owner administrar
   o acesso ao próprio curso.

Criar uma escrita direta, consultar os dois stores ou promover owner a tenant
admin introduziria uma segunda política e violaria os limites desta parte. O
owner de Authorization precisa oferecer um único contrato público que:

- grave e consulte o mesmo grant efetivo;
- derive tenant e ator exclusivamente do contexto;
- aceite uma autorização contextual de owner sem acoplar Identity a Learning;
- preserve security-version bump, expiração, deny-wins e audit log;
- ofereça consulta e revoke para a tela de acesso e para os testes de sessão.

Até esse contrato existir, ficam bloqueados somente a UI e os E2Es de delegação
de `ACCESS-03B` para colaborador ou revisor da equipe. Isso não bloqueia as
sessões reais de owner, dois learners e outsider, nem o E2E principal com o
owner executando `InstructorReview`. Os gates contextuais já entregues
permanecem utilizáveis e fail-closed.

## Matriz canônica de personas

As personas são estados contextuais e podem variar por curso para a mesma
conta.

| Persona de teste | Composição | Pode | Não pode |
| --- | --- | --- | --- |
| Aluno | `Member` ativa + `Enrollment` ativa no curso | acessar `/learn`, iniciar e enviar a própria tentativa e ler resultado liberado | autorar, publicar, executar review de equipe ou ler tentativa alheia |
| Instrutor proprietário | `Member` ativa + `Program.CreatorId` igual ao ator, no mesmo tenant | administrar o próprio curso, assessment, publicação, `StaffReview` e release | usar autoria sobre curso de outro tenant ou de outro owner |
| Instrutor colaborador (não bloqueante) | `Member` ativa + permissions explícitas no recurso `Program` | executar somente as ações concedidas, como `Edit`, `Publish` ou `StaffReview` | herdar poderes de owner ou tenant admin por rótulo de UI |
| Revisor da equipe/TA (não bloqueante) | `Member` ativa + `Program.{courseId}.Review` | abrir a fila segura, revisar, resolver e liberar quando a policy permitir | editar conteúdo ou publicar sem grants próprios |
| Outsider | `Member` ativa, sem matrícula e sem grants no curso | acessar somente superfícies públicas permitidas | entrar em `/learn`, autoria, fila ou resultado privado |
| Administrador | contexto administrativo já existente | preparar tenant, membership e grants | substituir qualquer persona nas provas de aceitação |

Regras de composição:

- autoria concede capacidades ao proprietário somente no curso cujo
  `CreatorId` corresponde ao ator e cujo tenant corresponde ao request context;
- permissions por recurso concedem a operação nomeada. `Edit`, `Publish` e
  `StaffReview` não se implicam silenciosamente entre si;
- matrícula não concede autoria nem `StaffReview`;
- permission de `StaffReview` não concede edição ou publicação;
- em `SelfReview`, o aluno atua somente sobre a própria submission quando a
  etapa estiver aberta;
- em `PeerReview`, o aluno atua somente sobre o
  `AssessmentPeerReview` atribuído a ele e nunca recebe `StaffReview` por isso;
- uma mesma conta deve poder ser aluna no curso A e proprietária,
  colaboradora ou revisora no curso B.

## Gate de entrada `03-0`

O fechamento `CLOSE-01` a `CLOSE-04` deve permanecer aprovado. Suas provas de
runtime continuam válidas; esta parte acrescenta identidade e autorização
reais, sem reimplementar `SEQ-07` a `SEQ-11`.

## `ACCESS-01`. Fechar o contrato de acesso do curso

### Resultado

Existe uma única matriz de capacidades de Learning, rastreável até os
mecanismos existentes de membership, enrollment, autoria e resource
permissions.

### Implementação

- inventariar os gates atuais de criação, leitura, edição, publicação,
  `StaffReview`, release e tentativa oficial;
- definir capacidades orientadas a ação, no mínimo: `Learn`, `Edit`, `Publish`
  e `StaffReview`, sem transformá-las em roles persistidas;
- documentar qual contrato público existente concede e consulta resource
  permissions, incluindo invalidação, versionamento e auditoria já oferecidos;
- definir `CreatorId` como regra contextual de owner, sempre limitada ao tenant;
- separar acesso de catálogo, acesso de learner e acesso de authoring;
- localizar checagens duplicadas, especialmente `CanManageCourseAsync`, filtros
  genéricos de `Program`, `canEditCourse` e gates de SpeedGrader;
- adicionar testes da matriz antes de consolidar os consumidores.

### Gate

- cada ação possui uma fonte de decisão explícita e fail-closed;
- nenhuma decisão depende de string `Instructor`, `Student`, `Reviewer` ou `TA`;
- não há alteração em módulos de plataforma nem em schema;
- a matriz comprova que permissions diferentes não se promovem implicitamente.

## `ACCESS-02`. Tornar a autorização da API coerente

### Resultado

Endpoints e handlers de Learning tomam a mesma decisão para o mesmo ator,
tenant, curso e ação.

### Implementação

- introduzir ou consolidar um avaliador de capacidades no domínio Learning que
  consuma `IActorContextAccessor`, autoria e os contratos públicos de
  autorização existentes;
- substituir cópias locais da política por esse avaliador nos fluxos de curso,
  content, assessment, test run, grading queue, instructor review e release;
- preservar `[Authorize]` ou `[AllowAnonymous]` explícito em todos os endpoints;
- fazer criação de curso por um `Member` autorizado resultar em owner por
  `CreatorId`, sem grant paralelo obrigatório nem role especial;
- garantir que learner só inicia ou consulta sua própria submission mediante
  matrícula ativa e que reviewer da equipe recebe apenas a projeção necessária;
- negar tenant ausente, tenant divergente, enrollment inativa, grant expirado e
  recurso inexistente sem revelar dados de outro contexto;
- não executar mutation de permission no caminho crítico; grant, update e
  revoke para a equipe pertencem a `ACCESS-03B` e só podem usar um contrato
  oficial que derive ator e tenant do contexto, versione a segurança e audite a
  operação.

### Regra de parada

Se o contrato público existente não conseguir conceder e consultar
`Program.{courseId}.{Permission}` de forma coerente, interromper somente a
delegação operacional de `ACCESS-03B` e entregar um diagnóstico ao owner de
Authorization. É proibido escrever direto em tabelas, introduzir um segundo
resolver ou enfraquecer o gate. Owner, learners e outsider continuam no caminho
crítico porque não dependem dessa mutação de permission.

### Gate

- testes de aplicação/API cobrem owner, learner, outsider e as decisões
  granulares de grants sem depender de sessões delegadas;
- owner, grants e enrollment produzem decisões idênticas em todos os endpoints
  equivalentes;
- grants de collaborator e reviewer da equipe seguem least privilege nos
  testes de aplicação, mesmo enquanto sua atribuição oficial permanece em
  `ACCESS-03B`;
- outsider, cross-tenant e tentativa de ler submission alheia falham fechado;
- nenhum teste positivo usa `SystemAdmin`.

## `ACCESS-03A`. Expor capacidades na web

### Resultado

O workspace mostra apenas as operações permitidas, sem apresentar personas ou
métodos de review como roles globais.

### Implementação

- aplicar gate server-side no layout de
  `/workspace/learning/courses/[course]` e nas rotas de assessments;
- proteger a entrada do SpeedGrader por capability `StaffReview`, mantendo a
  API como segunda barreira obrigatória;
- derivar navegação, botões de edição, publicação e review de equipe das
  capacidades retornadas pelo servidor;
- manter `/learn` vinculado a enrollment e separado do workspace autoral;
- listar no workspace apenas cursos criados pelo ator ou compartilhados por
  resource permission compatível;
- não usar `StaffReview` para autorizar `SelfReview` ou `PeerReview`;
- concluir a renomeação semântica de `Review`/`CanReview` para
  `StaffReview`/`CanReviewAsStaff` na API, projeção web e testes.

### Gate

- links e comandos inacessíveis não são renderizados;
- acesso direto por URL ainda é negado pela API;
- owner consegue administrar sem role especial;
- aluno e outsider não entram em superfícies autorais.

## `ACCESS-03B`. Delegar acesso à equipe

Status: bloqueado pelo contrato público de Authorization e não bloqueante para
`ACCESS-04`, `ACCESS-05` ou para a entrada na Parte 4.

### Resultado futuro

O owner poderá atribuir `Edit`, `Publish` e `StaffReview` a colaboradores ou
revisores da equipe usando um único contrato oficial de permission.

### Condições para retomar

- a escrita e a leitura devem usar o mesmo grant efetivo;
- tenant e ator devem vir exclusivamente do contexto autenticado;
- a autorização contextual do owner deve ser extensível sem introduzir
  vocabulário de Learning em módulos de plataforma;
- grant, update e revoke devem preservar security-version bump, expiração,
  deny-wins e audit log;
- a UI deve apresentar ações granulares, nunca roles de produto;
- é proibido escrever diretamente em tabelas, consultar stores concorrentes ou
  promover owner a tenant admin.

Quando essas condições forem atendidas, adicionar sessões e E2Es de
colaborador e revisor da equipe como extensão operacional, sem reabrir o gate
acadêmico principal.

## `ACCESS-04`. Criar fixture de sessões reais

### Resultado

Existe um arranjo de teste reproduzível com contas reais e contextos isolados,
sem headers sintéticos como única prova de identidade.

### Implementação

- criar contas `Member` por meio do fluxo suportado de autenticação e
  provisionamento de membership;
- abrir browser context, cookie ou token independente para owner, aluno A,
  aluno B e outsider;
- criar curso como owner e criar enrollment ativa para os dois alunos pelos
  casos de uso oficiais existentes;
- usar administrador somente para o bootstrap que realmente exigir
  administração do tenant;
- manter dados aleatórios e isolados por execução, com cleanup idempotente;
- corrigir o cenário relacional existente que usa `TenantMember.Role =
  "Instructor"`: ele deve usar `Member`, pois a autoridade vem de `CreatorId`
  ou grant contextual;
- preservar os testes HTTP com authentication handler sintético como testes
  rápidos de runtime, mas não tratá-los como prova suficiente do login e da
  sessão reais;
- adicionar colaborador e revisor da equipe somente depois de `ACCESS-03B`, sem
  torná-los pré-requisito da fixture acadêmica principal.

### Gate

- owner, aluno A, aluno B e outsider possuem sessão própria e tenant correto;
- nenhum cenário funcional troca ator apenas por header de teste;
- refresh/reabertura preserva a decisão esperada;
- o mesmo usuário é testado em contextos diferentes sem mudança de role;
- o owner executa `InstructorReview` pela autoria contextual, sem grant
  redundante de `StaffReview`.

## `ACCESS-05`. Revalidar o E2E de grading por persona

### Resultado

O fluxo principal de grading está comprovado do login ao resultado liberado
com os atores que serão usados na Parte 4.

### Cenários obrigatórios

1. owner cria curso, quiz e assessment, configura review e publica;
2. aluno matriculado realiza `AutomatedReview` e vê somente o resultado
   liberado;
3. aluno realiza `AutomatedReview + InstructorReview`; o owner conclui a
   pendência e libera o resultado;
4. aluno A não lê submission ou resultado privado do aluno B, e vice-versa;
5. outsider é negado em tentativa, submission, fila, review e resultado;
6. resultado retido não é visível ao aluno antes do release;
7. enrollment inativa ou revogada deixa de autorizar a jornada learner no
   próximo request protegido;
8. tentativa cross-tenant falha fechada;
9. uma conta aluna em um curso e owner em outro recebe capacidades distintas.

Este marco não implementa `SelfReview` nem `PeerReview`. Ele comprova as
sessões, ownership e isolamento que essas etapas consumirão na Parte 4.
Colaborador e revisor da equipe são cenários adicionais de `ACCESS-03B`, não
condições para aprovar este E2E.

### Verificação acumulada

- repetir os E2Es oficiais de `SEQ-10` e `SEQ-11`;
- repetir testes de authoring, learner activity, result, grading queue e
  SpeedGrader tocados;
- executar os testes de autorização e arquitetura exigidos pelo repositório;
- repetir, no mínimo, `GameGuild.Identity.Authorization.UnitTests`,
  `GameGuild.Identity.Authentication.UnitTests` e as suítes de
  arquitetura/segurança de `GameGuild.API.UnitTests`;
- executar build da API e typecheck/testes direcionados da web;
- registrar falhas de módulos externos para seus owners sem expandir o escopo.

## Definição de pronto da Parte 3

- `ACCESS-01`, `ACCESS-02`, `ACCESS-03A`, `ACCESS-04` e `ACCESS-05`
  concluídos; `ACCESS-03B` permanece um trilho operacional explicitamente não
  bloqueante enquanto depender do contrato externo;
- as personas são composições contextuais sobre `Member`, nunca roles de
  produto;
- a API resolve as capacidades e a web consome essa projeção, sem duplicar a
  política; a API permanece a autoridade;
- owner, dois alunos e outsider passam em sessões separadas;
- `StaffReview` é uma capability da equipe e não autoriza `SelfReview` ou
  `PeerReview`;
- nenhum fluxo positivo depende de `SystemAdmin` ou de acesso direto ao banco;
- nenhuma tabela, migration ou semântica de plataforma foi adicionada;
- as evidências das Partes 1 e 2 continuam aprovadas;
- a Parte 4 recebe atores reais para `SelfReview`, `PeerReview`, `AIReview` e
  operação avançada.

## Acompanhamento

| Marco | Status | Evidência |
| --- | --- | --- |
| gate `03-0` | aprovado | `CLOSE-01` a `CLOSE-04` permanecem aprovados |
| `ACCESS-01` | concluído | contrato e matriz de capacidades fechados |
| `ACCESS-02` | concluído | autorização uniforme e testes negativos na API |
| `ACCESS-03A` | concluído | gates web e nomenclatura `StaffReview`/`CanReviewAsStaff` validados |
| `ACCESS-03B` | bloqueado, não bloqueante | delegação da equipe aguarda contrato oficial de grant/revoke |
| `ACCESS-04` | concluído | fixture real de owner, dois alunos e outsider |
| `ACCESS-05` | concluído | E2E real do login ao resultado liberado nas duas políticas atuais |

### Evidências executadas

- `ContextualAccessAndGradingRealSessionPostgreSqlHttpTests`: 1 teste E2E
  PostgreSQL aprovado;
- regressão `AssessmentGradingPostgreSqlHttpTests.OfficialGradingFlow`: 4 testes
  aprovados;
- regressão `AssessmentCourseMembershipPostgreSqlHttpTests`: 9 cenários
  aprovados com todos os membros usando a membership `Member`;
- `GameGuild.Learning.Courses.UnitTests`: 854 testes aprovados;
- `GameGuild.Identity.Authorization.UnitTests`: 1.667 testes aprovados;
- `GameGuild.Identity.Authentication.UnitTests`: 2.316 testes aprovados;
- `GameGuild.API.UnitTests`: 1.079 testes aprovados;
- testes web direcionados: 108 testes aprovados em 6 arquivos;
- typecheck da web aprovado;
- `dotnet build apps/api/GameGuild.sln --no-restore -m:1` aprovado sem erro ou
  diagnóstico arquitetural. Permanecem somente os avisos `NU1902`/`NU1903` já
  existentes para `SixLabors.ImageSharp`.

## Gate para a Parte 4

Gate aprovado em 2026-10-08: `ACCESS-03A`, `ACCESS-04` e `ACCESS-05` demonstram
owner, dois learners e outsider autenticados no curso correto, sem depender de
role inventada, admin ou identity sintética no fluxo positivo. Esses learners
assumirão contextualmente `SelfReview` ou `PeerReview` somente na Parte 4.
`ACCESS-03B` não integra esse gate.
