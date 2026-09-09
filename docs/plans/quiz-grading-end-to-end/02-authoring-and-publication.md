# 02. Autoria e publicação

## Objetivo

Fazer com que quiz, grading, revisão autoral e assessment sejam validados e
persistidos por um único caso de uso da API, com uma UX clara para o professor.

Assessment é o braço operacional do content avaliável. Ele referencia e
configura como o conteúdo será aplicado e avaliado, mas não se torna uma segunda
fonte de verdade para enunciados, opções ou answer keys.

## Problemas atuais

- o quiz é salvo como `ProgramContent.JsonBody`;
- a web chama `reconcileQuizAssessment` depois, em outra operação;
- falha entre as chamadas pode deixar content e assessment divergentes;
- o host grava `AutoGraded,InstructorGraded` fixo;
- a lateral do assessment expõe flags como checkboxes e salva imediatamente;
- `gradingKind` por questão pode ser confundido com a origem definida em
  `ReviewMethods`;
- campos derivados podem ser alterados dos dois lados;
- não há estado próprio de publicação do assessment;
- `PassingScore` aparece na UI com semântica percentual, apesar de a entidade e
  a constraint originais o tratarem como pontos absolutos.

## Ownership

### Quiz content

- enunciados, opções e resposta correta;
- pontos como inteiro de unidades compatível com `ScoreValue` e única fonte
  mutável da pontuação por questão, além da configuração da questão;
- feedback autoral;
- apresentação visual autoral do quiz;
- `ContentGradingDefinitionV2` limitado à configuração adicional de grading
  por ID de item, sem copiar ID, pontos, tipo ou capability executável.

### Assessment

- workflow de review;
- disponibilidade, prazo e atraso;
- tentativas e tempo;
- grupo e peso por referência;
- rubrica e política de pares;
- apresentação operacional da aplicação;
- política de conclusão do content avaliado;
- política de liberação de resultado e feedback;
- passing score;
- draft operacional e ponteiro para a revisão publicada.

### Derivado

- `MaxScore` vem exclusivamente da soma validada de `QuizEntry.points`, é
  calculado pelo servidor e usa `ScoreValue`;
- `PassingScore` é um limiar absoluto entre zero e `MaxScore`;
- capability técnica por item é resolvida pelo adapter no manifest executável,
  não persistida como `gradingKind` na fonte autoral;
- modalidade de quiz é `StructuredAnswer`;
- vínculo com content é imutável depois de existirem tentativas;
- revisão publicada captura content e configuração de execução;
- grupo e peso permanecem fora da revisão e controlam somente a projeção atual
  do gradebook.

`ContentGradingDefinition` não continua carregando cópias de tentativas, tempo,
conclusão, feedback release, apresentação operacional ou passing score. Esses
campos passam atomicamente para `AssessmentExecutionPolicyV1`. Não há
reconciliação bidirecional nem preferência por "último valor salvo".

`AssessmentExecutionPolicyV1` é montado pela API a partir das fontes únicas do
agregado Assessment. Ele não é salvo como cópia mutável das mesmas colunas em
`DefinitionPayload`; somente a revisão imutável persiste o contrato composto.

## Caso de uso atômico

Separar intenção sem voltar a coordenar gravações no browser:

```text
SaveQuizAssessmentDraft
  -> authorize course management
  -> validate QuizContentDocument
  -> validate ContentGradingDefinitionV2
  -> validate AssessmentExecutionPolicyV1
  -> validate ReviewMethods and review policy
  -> reject duplicated points/gradingKind and orphan item IDs
  -> calculate MaxScore from QuizEntry.points and required capabilities
  -> save ProgramContent
  -> create/update linked Assessment
  -> commit once

PrepareQuizAssessmentRevision
  -> validate the saved draft
  -> validate learner-safe projection and AuthorTest capabilities
  -> build AssessmentAuthoringSourceV1
  -> canonicalize and calculate AuthoringSourceHash
  -> resolve exact executable versions into AssessmentExecutionManifestV1
  -> build AssessmentExecutionSnapshotV1
  -> canonicalize and calculate ExecutionSnapshotHash
  -> freeze immutable AssessmentDefinitionRevision
  -> return revisionId, authoringSourceHash, executionSnapshotHash and readiness

PublishQuizAssessmentRevision(revisionId)
  -> load the already frozen manifest and ExecutionSnapshotHash
  -> exact-resolve the same keys/versions and validate OfficialSubmission capabilities
  -> reject any attempt to rebuild or replace manifest bytes or execution hash
  -> require revision.AuthoringSourceHash == current authoring draft hash
  -> activate PublishedDefinitionRevisionId
  -> append outbox messages
  -> commit once

UnpublishQuizAssessmentRevision(expectedRevisionId, expectedVersion)
  -> authorize course management
  -> verify the expected active revision and optimistic version
  -> clear PublishedDefinitionRevisionId without deleting any revision
  -> append audit/outbox evidence
  -> commit once
```

A web não deve coordenar gravações independentes de content e assessment.
Publish direto pode preparar e ativar em uma transação. Quando houver test run,
publish deve ativar a mesma candidata já testada, nunca gerar outra revisão
silenciosamente.

## UX do assessment

Criar uma seção principal `Workflow de avaliação`, próxima de scoring:

1. cards ou radio group para os cinco reviews primários;
2. toggle de revisão final do instrutor para os quatro reviews não docentes;
3. resumo da sequência;
4. configuração contextual do método;
5. estado de capability/provider quando aplicável;
6. salvamento com o formulário, sem request por checkbox.

Grupo e peso ficam visualmente separados do workflow. Nenhuma mudança de peso
altera o método selecionado.

Após salvar uma configuração válida, `Testar assessment` prepara uma revisão
candidata e abre o test run operacional definido na fase seguinte. Na Parte 1,
essa infraestrutura é provada somente com registry controlado em testes; a ação
de produção permanece indisponível até `SEQ-08` registrar o primeiro handler
real com capability `AuthorTest`. Alterações
ainda não salvas devem ser salvas explicitamente antes do teste. Ao final, a
ação `Publicar esta revisão` ativa exatamente a candidata testada se o hash do
draft continuar igual e todos os stages declararem capability
`OfficialSubmission`.

Essa ação encerra o fluxo de teste e altera somente o ponteiro de revisão ativa.
Ela não cria submission, não agenda uma execução acadêmica e não encaminha o
professor ou qualquer aluno ao player oficial.
O `revisionId`, os bytes canônicos de `AssessmentExecutionManifestV1` e o
`ExecutionSnapshotHash` permanecem idênticos entre prepare, test run, publish e
start oficial.

A tela deve distinguir `Rascunho`, `Publicado` e `Alterações não publicadas`.
Salvar draft nunca muda a revisão ativa. `Publicar alterações` ativa a candidata
testada ou prepara e ativa uma nova revisão numa transação de publish direto.
`Despublicar` impede novos starts, mas preserva tentativas que já referenciam
revisões anteriores. A operação é autorizada, auditável, idempotente e usa a
revisão ativa e sua versão como precondições; uma chamada obsoleta não pode
despublicar outra revisão publicada concorrentemente.

Se o draft mudar depois do test run, marcar a candidata como desatualizada na
UI e impedir sua ativação. Ela continua consultável no test run, mas é
necessário preparar outra candidata para publicar as novas alterações.
Atualização de deploy, catálogo de versões ou health de provider não marca a
candidata como desatualizada: esses fatores são validados pelo manifest e pelo
preflight, não pelo hash autoral.

## Configurações específicas

- `PeerReview`: revisões por aluno, revisões exigidas por submissão, agregação,
  anonimato, rubrica, prazo e mínimo de evidência. A policy inicial fixa
  `AwaitingInstructorResolution` quando o prazo encerra sem o mínimo e não
  oferece zero ou conclusão automática como fallback;
- `AIReview`: provider e policy disponíveis, sem expor segredo ou prompt
  privado ao browser;
- `AutomatedReview`: cobertura determinística por questão, distinguindo itens
  avaliáveis e itens que permanecerão pendentes;
- `SelfReview`: instruções, feedback obrigatório e limites por item;
- `InstructorReview`: rubrica e fila docente;
- revisão final: exigência de motivo para override e política de feedback.

`AIReview` pode ser configurado em draft sem provider registrado. O publish é
bloqueado até que o provider selecionado esteja registrado e declare
compatibilidade com a policy. Saúde transitória é mostrada como condição de
execução; indisponibilidade deixa tentativas pendentes para retry e não exige
republicação. A UI mostra a causa sem oferecer nota simulada.

O professor pode escolher o método independentemente do peso, mas publicação
deve ser bloqueada quando faltarem pré-requisitos técnicos do método escolhido.
Capability `AuthorTest` permite preparar e testar uma candidata, mas não
autoriza publicação oficial. A UI deve mostrar readiness de teste e readiness
de publicação separadamente, sem inferir uma a partir da outra.

No workflow `AutomatedReview,InstructorReview`, cobertura parcial é válida: o
primeiro estágio avalia o que consegue e o instrutor resolve o restante. Em
`AutomatedReview` isolado, a UX final para cobertura parcial ainda será
decidida. Até essa decisão, publish oficial exige cobertura determinística de
todos os itens. O servidor expõe a cobertura por item; no test run, conclui o
estágio com resultado parcial e mantém a rodada não finalizada, sem tratar item
não determinístico como erro ou zero.

`GroupAssignment` não altera o workflow de review. A UI deve comunicar que a
tentativa e a avaliação serão compartilhadas pelo grupo, enquanto grupo de peso
continua sendo apenas configuração do gradebook. A publicação valida que o
group set existe e pode resolver participantes, mas o snapshot dos integrantes
só é criado no start da tentativa.

## Tarefas

- [ ] mapear e remover a coordenação `save content -> reconcile assessment` da
  web;
- [ ] criar comando transacional na API;
- [ ] separar casos de uso de salvar draft, preparar revisão e ativar revisão;
- [ ] implementar lifecycle `Draft`/`Published`/`ChangesPending` derivado da
  revisão ativa;
- [ ] impor um único assessment ativo por content quando aplicável;
- [ ] remover o workflow fixo de quiz;
- [ ] usar `InstructorReview` como default explícito de novos drafts até o
  professor escolher outro workflow;
- [ ] impedir edição de campos derivados no assessment editor;
- [ ] remover `attempts`, conclusão, feedback release, apresentação operacional
  e passing score de `ContentGradingDefinition`, movendo-os para o contrato do
  assessment;
- [ ] permitir configurar no assessment editor as policies iniciais de
  conclusão e de liberação de resultado/feedback; a revisão candidata deve
  capturar exatamente os valores configurados e inspecionados no test run, sem
  produzir seus efeitos acadêmicos nesse contexto;
- [ ] exigir prazo, mínimo recebido e política de resolução de insuficiência ao
  preparar `PeerReview`, mostrando que a resolução terminal é docente e
  auditável;
- [ ] implementar o seletor de workflow definido no plano de domínio;
- [ ] implementar configurações contextuais dos cinco reviews;
- [ ] consultar capabilities server-side para `AIReview`;
- [ ] consultar capabilities por método e contexto `AuthorTest` ou
  `OfficialSubmission`;
- [ ] fixar `AssessmentExecutionManifestV1` na candidata e impedir fallback
  para outra versão do adapter agregado, handler, policy ou provider;
- [ ] separar readiness de publicação de health operacional do provider;
- [ ] persistir a seleção somente ao salvar o formulário;
- [ ] validar pré-requisitos por método na publicação;
- [ ] manter publish de produção bloqueado até todos os stages possuírem
  capability oficial, mesmo que o test run tenha concluído;
- [ ] mostrar cobertura determinística por item sem converter cobertura parcial
  em erro;
- [ ] apresentar `GroupAssignment` como tentativa e avaliação compartilhadas,
  sem misturá-lo com grupo de peso ou `PeerReview`;
- [ ] criar revisão candidata somente em prepare/test ou publish direto, nunca
  em cada tecla ou preview;
- [ ] ativar candidata testada somente com `AuthoringSourceHash` ainda
  correspondente ao draft;
- [ ] implementar unpublish idempotente com revisão/versão esperadas, auditoria
  e preservação de revisões e execuções existentes;
- [ ] validar no publish o manifest fixado no prepare sem regenerar bytes,
  versões ou `ExecutionSnapshotHash`;
- [ ] corrigir `PassingScore` para pontos absolutos e remover qualquer cálculo
  de percentual do assessment na UI;
- [ ] definir comportamento ao desligar grading quando já existem tentativas;
- [ ] adicionar `Testar assessment` para draft salvo e válido e
  `Publicar esta revisão` para candidata não desatualizada;
- [ ] atualizar testes de content editor, assessment editor e actions.

## Arquivos principais

```text
apps/web/src/components/learning/console/courses/[course]/content/[contentId]/content-item-editor.tsx
apps/web/src/components/learning/console/courses/[course]/content/[contentId]/quiz-content-editor.tsx
apps/web/src/components/learning/console/courses/[course]/assessments/[assessmentId]/assessment-editor.tsx
apps/web/src/lib/learning/actions.ts
apps/api/Source/Modules/GameGuild.Learning.Assessments/Controllers/AssessmentsController.cs
apps/api/Source/Modules/GameGuild.Learning.Assessments/Services/AssessmentService.cs
apps/api/Source/Modules/GameGuild.Learning.Courses/Controllers/ProgramContentController.cs
```

## Testes

- salvar content e assessment com sucesso em uma transação;
- rollback integral sob erro de validação ou persistência;
- concorrência não cria dois assessments;
- cada um dos nove workflows reabre corretamente;
- combinações inválidas são rejeitadas pela API;
- `AIReview` sem provider salva draft, mas não publica;
- provider compatível e temporariamente indisponível não invalida publicação;
- alterar grupo ou peso preserva workflow;
- publicação rejeita método sem pré-requisitos;
- capability somente de test run prepara e testa, mas não publica;
- registry controlado prova a infraestrutura na Parte 1 sem criar candidata
  operacional de produção; o primeiro handler real prepara novamente a
  candidata em `SEQ-08`;
- prepare, test run, publish e start oficial preservam `revisionId`, manifest
  canônico e `ExecutionSnapshotHash`;
- editar o draft após test run impede ativar a candidata desatualizada;
- alterar somente manifest, catálogo ou deploy não cria falso
  `ChangesPending`; o `ExecutionSnapshotHash` continua identificando a execução
  testada;
- `AutomatedReview,InstructorReview` aceita cobertura parcial e encaminha os
  itens pendentes ao instrutor;
- `AutomatedReview` isolado com item não determinístico nunca publica zero nem
  entra em falha técnica, mas permanece sem readiness para publish oficial;
- quiz atribuído a grupo publica com uma única submission coletiva e sem
  alterar o workflow de review;
- publicar após test run ativa o mesmo `revisionId` exercitado;
- unpublish concorrente ou repetido não desativa revisão diferente, bloqueia
  novos starts e preserva tentativa já iniciada;
- edição posterior cria nova revisão sem modificar tentativas existentes.

## Critério de saída

- não existe divergência observável entre quiz e assessment;
- professor escolhe o workflow sem manipular flags;
- a API é a autoridade final da publicação;
- readiness de test run e readiness de publicação oficial permanecem
  independentes;
- test run e publish compartilham a mesma revisão imutável quando o professor
  publica o que testou;
- o assessment publicado aponta para um snapshot imutável e válido da
  definição;
- salvar alterações não publicadas não muda novas tentativas até novo publish;
- unpublish bloqueia novos starts sem apagar revisão ou interromper tentativa
  já iniciada;
- aprovação da submissão usa o limiar absoluto publicado do assessment, não o
  percentual global do curso.
