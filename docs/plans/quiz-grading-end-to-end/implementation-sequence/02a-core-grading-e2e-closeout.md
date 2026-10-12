# Fechamento da Parte 2. E2E principal de grading

Status: concluído; gate de grading aprovado e Parte 3 de acesso contextual
liberada.

## Objetivo

Fechar somente as lacunas encontradas depois da primeira execução da Parte 2.
Este documento não manda reexecutar `SEQ-07` a `SEQ-11` nem reimplementar o
runtime já entregue. Ele é o gate corretivo entre a Parte 2 e a Parte 3.

Plano de origem: [`02-core-grading-e2e.md`](./02-core-grading-e2e.md).

## Estado de partida

Já estão implementados o runtime comum, `AuthorTest`, `InstructorReview`,
`AutomatedReview`, submissions oficiais individuais e coletivas, release,
gradebook e projeção de progresso. As suítes direcionadas desses componentes
passam.

A auditoria posterior encontrou três pendências de encerramento:

1. o fluxo anterior de peer review ainda persiste review e emite notificação
   fora da `GradingExecution`, além de depender de `CanonicalRow` e submissions
   irmãs para grupos;
2. os fluxos oficiais são cobertos principalmente por testes diretos do runtime
   com EF InMemory, sem a prova acumulada HTTP + PostgreSQL exigida pelo gate;
3. o teste da migration de grading valida tabelas reduzidas e trechos SQL, mas
   não percorre a cadeia real de migrations sobre um banco populado.

## Limites deste fechamento

- não implementar `SelfReview`, `PeerReview` canônico ou `AIReview`; isso
  continua pertencendo à Parte 4;
- não reescrever `InstructorReview`, `AutomatedReview`, submissions, release ou
  gradebook que já passam nos testes;
- não editar, excluir ou recriar migrations, designers ou tabelas históricas;
- não remover `AssessmentPeerReview` apenas por sua origem anterior. A entidade
  pode ser reaproveitada em `SEQ-13` se o `SCHEMA-GATE` confirmar ownership e
  invariantes adequados;
- não corrigir Social Blog, TestingLab, assets ou outro módulo sem relação com o
  gate de grading. Falhas desses módulos devem ser registradas como não
  bloqueantes e resolvidas por seus respectivos owners;
- qualquer delta relacional descoberto interrompe este fechamento e exige um
  `SCHEMA-GATE` aprovado antes de alterar entidades EF, snapshot ou banco.

## `CLOSE-01`. Eliminar a autoridade peer paralela residual

### Resultado

Nenhuma rota, service, action ou projeção consegue persistir score/evidência,
agregar resultado ou emitir efeito acadêmico de peer review fora do runtime
canônico. A infraestrutura útil de claim e workspace pode permanecer preparada,
mas `PeerReview` continua indisponível para publicação até `SEQ-13`.

### Implementação

- inventariar `PeerReviewsController`, `IPeerReviewAssignmentService`,
  `PeerReviewAssignmentService`, commands, actions web, clients gerados,
  `GradingQueueService`, `TasksService`, SpeedGrader e produtores de
  notificação;
- remover ou tornar fail-closed o submit anterior que grava review fora de uma
  `GradingExecution`. Não criar um segundo adapter temporário nem antecipar o
  handler completo de `PeerReview`;
- retirar a notificação direta de `PeerReviewAssignmentService`. Efeitos
  externos continuam desligados até os consumers canônicos de `SEQ-15`;
- adaptar leituras, claims, fila, tarefas e SpeedGrader para a única submission
  coletiva e seu snapshot de participantes;
- remover dependências de `PeerReviewAssignmentService.CanonicalRow`, união de
  submissions irmãs e escolha de uma linha representativa do grupo;
- preservar migrations, tabelas, dados, entidade e UX reutilizável. Remoção
  física somente pode ocorrer em outro gate, com inventário e aprovação.

### Gate

- busca estrutural não encontra consumer de `CanonicalRow` nem agrupamento de
  submissions irmãs para representar uma tentativa coletiva;
- nenhuma rota anterior aceita score ou feedback peer como resultado oficial;
- nenhum service/controller/action peer envia notificação acadêmica diretamente;
- submissions coletivas continuam únicas e suas filas/projeções usam o sujeito
  coletivo canônico;
- testes negativos comprovam o fail-closed de `PeerReview` até `SEQ-13`;
- nenhum teste já aprovado de `InstructorReview` ou `AutomatedReview` regride.

## `CLOSE-02`. Provar os E2Es oficiais sobre HTTP e PostgreSQL

### Resultado

O caminho executado em produção de teste é exercitado da rota HTTP à
persistência PostgreSQL, sem construir o runtime manualmente nem substituir o
banco por EF InMemory.

### Implementação

- usar o host real da API com autenticação controlada e PostgreSQL isolado de
  teste;
- cobrir start, resume, submit, review, release e leitura learner-safe pelo
  endpoint público correspondente;
- executar `InstructorReview` e `AutomatedReview` em submission individual;
- executar `InstructorReview` e `AutomatedReview` em submission coletiva única;
- comprovar que resume e retry preservam revisão, manifest, entrega concreta e
  `DeliveryHash`;
- exercitar idempotency key repetida com payload idêntico e conflito com payload
  divergente;
- cobrir autorização negativa: outro aluno, integrante externo ao snapshot,
  ator sem permissão de review/release e acesso antecipado ao resultado;
- reconstruir gradebook e progresso a partir do estado persistido em novo scope,
  sem depender de objetos mantidos em memória pelo teste;
- comprovar que as rotas genéricas substituídas permanecem fail-closed para quiz
  avaliado.

### Gate

- os quatro fluxos oficiais passam via HTTP + PostgreSQL: individual e coletivo,
  cada um com `InstructorReview` e `AutomatedReview`;
- retry não duplica submission, execução, round, resultado, release, gradebook,
  progresso ou evento acadêmico;
- a submission coletiva produz uma execução e um resultado, com projeção por
  participante;
- o aluno vê somente o resultado liberado e nenhuma evidência privada;
- restart do host ou novo scope não altera a reconstrução do estado acadêmico.

## `CLOSE-03`. Provar criação limpa e upgrade pela cadeia real

### Resultado

A migration já existente de grading é validada no contexto da cadeia completa,
sem ser modificada e sem executar apenas fragmentos escolhidos de seu SQL.

### Implementação

- manter intactas `20260916194905_AddAssessmentGradingWorkflow`, seu designer e
  todas as migrations anteriores e posteriores;
- migrar um banco vazio até a ponta corrente e validar o schema final;
- criar outro banco na migration imediatamente anterior a
  `AddAssessmentGradingWorkflow`, inserir dados representativos usando o schema
  real daquele ponto e migrá-lo pela cadeia completa até a ponta corrente;
- executar a cadeia por `IMigrator`/EF, não chamando manualmente somente os
  marcadores SQL da migration;
- validar preservação e conversão de scores, pesos, submissions, atores,
  policies, métodos de review e artefatos SQL ativos;
- comparar o modelo final com o snapshot e reprovar drift fora das migrations
  versionadas;
- considerar como ponta a migration mais recente no momento da execução. No
  estado desta auditoria ela é `20260929000957_RebuildSocialBlog`; merges futuros
  podem avançar essa ponta sem mudar a regra.

### Gate

- criação limpa e upgrade populado passam em PostgreSQL;
- nenhum dado ou artefato histórico é removido para fazer o teste passar;
- o modelo final não possui drift;
- nenhuma migration histórica foi editada;
- se um delta relacional novo for realmente necessário, este marco para antes
  da alteração e apresenta um `SCHEMA-GATE` separado.

## `CLOSE-04`. Revalidar e liberar a Parte 3

### Implementação

- repetir contratos e testes dos packages `@game-guild/grading` e
  `@game-guild/grading-adapter-quiz`;
- repetir a suíte de `GameGuild.Learning.Assessments.UnitTests`;
- executar os testes PostgreSQL de migration e os E2Es de `CLOSE-02`;
- repetir os testes web de autoria, assessment, tentativa, resultado e
  SpeedGrader tocados pelo fluxo;
- executar build/typecheck global para identificar regressões relacionadas ao
  grading. Falhas pertencentes a outros módulos devem ser registradas e
  encaminhadas aos respectivos owners, sem bloquear este gate nem expandir o
  escopo do plano;
- registrar no acompanhamento da Parte 2 as evidências e marcar o gate como
  concluído somente depois de todos os itens acima.

## Evidências da execução em 30 de setembro de 2026

- `CLOSE-01`: o submit peer anterior passa a falhar fechado com
  `PeerReview.CanonicalRuntimeRequired`; não persiste score/feedback nem envia
  notificação direta. Ownership, filas e tarefas coletivas usam a submission
  única e `AssessmentSubmissionParticipant`. A busca estrutural não encontra
  `CanonicalRow`, `AttemptRowIds`, união ou escolha de submissions irmãs;
- `CLOSE-02`: quatro cenários passam pelo host real da API e PostgreSQL:
  individual e coletivo, cada um com `InstructorReview` e
  `AutomatedReview`. Os testes cobrem start/resume, replay e conflito de
  idempotência, submit, review, release, autorização negativa, privacidade
  antes do release e reconstrução em novo scope. Cada cenário persiste uma
  submission, uma execução, uma rodada, uma release e exatamente os dois
  eventos canônicos esperados;
- `CLOSE-03`: cinco testes passam em PostgreSQL. Um banco vazio percorre toda a
  cadeia e um banco populado na migration imediatamente anterior a
  `AddAssessmentGradingWorkflow` é atualizado por `IMigrator` até a ponta. Os
  dados, conversões para unidades inteiras e artefatos são verificados, sem
  drift e sem editar migration, designer ou snapshot;
- packages: `@game-guild/grading` passa 16 testes e
  `@game-guild/grading-adapter-quiz` passa 10 testes; ambos passam typecheck;
- backend: `GameGuild.Learning.Assessments.UnitTests` passa 396 testes. O
  projeto de Assessments compila com zero warning e zero erro. A solução
  completa da API compila com zero erro; a indisponibilidade de
  `api.nuget.org` produz somente avisos ambientais `NU1900` na auditoria de
  vulnerabilidades do restore;
- web direcionada: 45 testes de autoria, lifecycle, tentativa, resultado e
  SpeedGrader passam;
- qualidade do diff: `git diff --check` passa e nenhum arquivo histórico de
  migration foi alterado.

### Diagnósticos globais fora do escopo

O build de `apps/api/GameGuild.sln` está aprovado com zero erro; os avisos
`NU1900` observados são causados apenas pela indisponibilidade da consulta de
vulnerabilidades do NuGet no ambiente.

O typecheck de `apps/web` não apresenta erro em grading, quiz ou assessments,
mas falha em tipos gerados obsoletos de `.next` e em Social/Blogs e Testing
Lab. Esses módulos estão sendo desenvolvidos por outros owners e seus erros
são diagnósticos não bloqueantes para este fechamento.

Esses erros não serão corrigidos por expansão oportunista deste plano. Como as
validações direcionadas de grading, quiz e assessments e o build da API estão
aprovados, o `CLOSE-04` está concluído e a Parte 3 pode começar.

## Definição de pronto

- `CLOSE-01` a `CLOSE-04` concluídos;
- `SEQ-10` e `SEQ-11` possuem evidência relacional e HTTP, não apenas teste
  direto do service;
- não existe autoridade paralela de peer review ou representação coletiva por
  submissions irmãs;
- criação limpa, upgrade real populado e ausência de drift estão comprovados;
- migrations e armazenamento históricos foram preservados;
- o gate da Parte 2 está aprovado e `ACCESS-01` pode começar sem exceção
  implícita; `SEQ-12` continua bloqueado até `ACCESS-05`.

## Acompanhamento

| Marco | Status | Evidência esperada |
| --- | --- | --- |
| `CLOSE-01` | concluído | busca estrutural limpa, submit peer fail-closed e ausência de notificação/autoridade paralela |
| `CLOSE-02` | concluído | quatro E2Es oficiais aprovados via HTTP + PostgreSQL |
| `CLOSE-03` | concluído | criação limpa e upgrade real populado aprovados, sem migration histórica alterada e sem drift |
| `CLOSE-04` | concluído | suítes direcionadas e build da API aprovados; falhas web de outros módulos registradas como não bloqueantes |
