# Parte 1. Fundação e autoria

## Objetivo

Fechar a base arquitetural, contratual, relacional e autoral do grading antes
de executar qualquer test run ou tentativa acadêmica. Esta parte contém
`SEQ-00` a `SEQ-06` e deve ser concluída e testada isoladamente.

Regras globais: [`08-implementation-sequence.md`](../08-implementation-sequence.md).

## Pré-requisitos

- especificações temáticas `00` a `07` revisadas;
- autorização explícita para qualquer `SCHEMA-GATE` desta parte;
- ambientes descartáveis identificados para a recriação do baseline global.

## Fora do escopo

- executar respostas no test run;
- produzir submission ou resultado acadêmico;
- integrar gradebook, notificação ou passback;
- habilitar qualquer capability `OfficialSubmission` em produção.

## `SEQ-00`. Fechar decisões arquiteturais

### Resultado

As decisões que governam dados acadêmicos, concorrência e identidades ficam
registradas antes de qualquer mudança estrutural.

### Implementação

- aprovar ADR de lifecycle de `Assessment` e revisão imutável publicada;
- aprovar ADR de `ScoreValue`, `PercentValue`, precisão, arredondamento,
  agregação, passing score e crédito parcial. O ADR fixa Matching por proporção
  de pares corretos e Ordering por proporção de posições absolutas corretas,
  sempre com aritmética inteira exata e arredondamento único `half-up`. Ambos
  são inteiros escalados por `100`: `100` unidades de score representam `1`
  ponto e `100` unidades percentuais representam `1%`; `PercentValue` fica no
  intervalo `0..10000` e `ScoreValue` em `0..int32.MaxValue`;
- aprovar ADR de rodadas de grading, concorrência, idempotência, outbox e inbox.
  O ADR deve definir fan-out durável: uma mensagem acadêmica é persistida uma
  vez, mas cada consumidor obrigatório possui confirmação/deduplicação própria
  por `(EventId, ConsumerKey)`; a mensagem só encerra o dispatch depois de todos
  os consumidores capturados em sua rota confirmarem;
- aprovar ADR de versionamento executável: toda revisão fixa no manifest um
  único adapter agregado por tipo de assessment, além dos handlers, providers
  e policies que poderá executá-la. O adapter é responsável de forma coesa por
  projeção autoral, geração de entrega, decode/normalização e avaliação
  determinística do tipo, incluindo
  catálogo imutável de versões suportadas, resolução exata, preflight de
  startup/deploy contra revisões ativas, revisões retidas elegíveis a regrade e
  execuções não terminais, retenção de artefatos, rollback e retirada segura
  dessas versões;
- aprovar ADR de identidade da revisão com dois hashes independentes:
  `AuthoringSourceHash`, derivado somente da definição controlada pelo autor, e
  `ExecutionSnapshotHash`, derivado dessa definição mais o manifest executável
  fixado; somente o primeiro determina `Published` ou `ChangesPending`;
- aprovar ADR da entrega concreta por execução: a revisão imutável define o
  que pode ser executado, enquanto `AssessmentExecutionDeliveryV1`, pertencente
  à `GradingExecution`, congela uma única vez os challenges gerados, a ordem de
  apresentação e as demais variações vistas pelo sujeito. Persistir a saída
  concreta, sua versão e seu hash; uma seed pode ser evidência auxiliar, mas não
  substitui os bytes materializados. A entrega contém `itemOrder` explícito e
  nunca depende da ordem de propriedades de um objeto JSON. Na versão inicial,
  todo dado privado necessário à correção deve ser derivável da revisão
  imutável mais a entrega pública concreta; geradores não podem criar answer key
  privada aleatória adicional;
- aprovar ADR do sujeito da submission, tentativa coletiva e snapshot de
  participantes;
- registrar no ADR de rodadas que `GradingExecution` é a raiz persistente
  compartilhada, com owner relacional exatamente um entre
  `AssessmentTestRunSubject` e submission oficial;
- registrar que um test run pode possuir vários subjects sintéticos, cada um
  com execução própria, para suportar `PeerReview` multipersona;
- aprovar a estratégia global de reset do baseline do `ApplicationDbContext`,
  incluindo inventário da cadeia atual, modelo completo, ambientes afetados e
  verificação de drift não relacionado;
- inventariar separadamente todo artefato ativo criado por SQL manual e não
  representado pelo `IModel`: extensões, schemas, roles, grants, policies,
  funções, procedures, triggers, views, índices parciais ou concorrentes e
  dados estruturais indispensáveis. O baseline limpo deve reinstalar e testar
  esses artefatos na ordem correta; descartá-los sem decisão explícita elimina
  comportamento ativo do banco;
- inventariar as outboxes/inboxes existentes e definir como eventos acadêmicos
  duráveis deixam de usar o dispatch em processo de `SaveChangesAsync`;
- aprovar a política de conclusão de content avaliado e sua autoridade de
  projeção. A decisão define em qual transição canônica o content progride, se
  exige finalização, liberação ou aprovação, e se `ContentInteraction` será
  mantido somente como read model; nenhuma rota genérica pode decidir esse
  estado para conteúdo avaliável ligado a assessment. O modo dependente de aprovação é
  `on-release-and-pass`: a projeção learner-visible não ocorre antes do release
  da rodada, pois a própria conclusão revelaria `Passed`;
- fechar a política de retenção de revisões, rodadas, evidências e auditoria;
- definir que regrade de uma `GradingExecution` sempre reutiliza sua revisão,
  manifest, entrega e respostas originais. Avaliar uma definição diferente cria
  nova submission e nova execução por um caso de uso explícito; não é uma nova
  rodada da execução anterior;
- registrar que `AutomatedReview` parcial não falha nem atribui zero;
- manter como decisão de produto pendente somente a publicação de um workflow
  `AutomatedReview` isolado que contenha itens não determinísticos;
- aprovar a política inicial que determina a contribuição efetiva das
  tentativas finalizadas para o gradebook e decidir se ela é global ou
  configurável por assessment; o primeiro modo implementado seleciona uma
  tentativa, sem antecipar agregação por média;
  se múltiplas tentativas ainda não tiverem uma política implementável, o
  primeiro E2E oficial deve limitar `maxAttempts` a `1`;
- fixar a fórmula canônica inicial do gradebook. Para cada assessment, a policy
  de tentativas seleciona no máximo uma contribuição efetiva finalizada. Dentro
  do grupo, `groupRatio = sum(effectiveScore) / sum(capturedMaxScore)` sobre as
  contribuições efetivas existentes; portanto assessments com escalas distintas
  são ponderados por pontos, não por uma média de percentuais. Em seguida,
  `groupContribution = groupRatio * AssessmentGroup.WeightPercent` e
  `coursePercent = sum(groupContribution)`, sem renormalizar pesos ausentes ou
  grupos ainda sem contribuição. Estado incompleto é apresentado como parcial,
  nunca como nota final. Grupo de peso zero e assessment sem grupo não entram na
  soma. Grupo sem denominador positivo não produz contribuição nem resultado
  global oficial. Aritmética é exata na API e cada projeção derivada é
  arredondada uma única vez para unidades inteiras escaladas;
- exigir, antes de produzir resultado global oficial do curso por
  `Program.PassingScore`, que os grupos de peso positivo publicados totalizem
  exatamente `100%`; drafts podem permanecer incompletos, mas a API não corrige
  nem redistribui pesos silenciosamente;
- definir os contextos `AuthorTest` e `OfficialSubmission` como execuções
  distintas, ainda que reutilizem o mesmo orquestrador técnico.

### Gate

- os ADRs estão aprovados e não se contradizem;
- a operação global de baseline está documentada e separada do delta funcional
  de grading;
- a estratégia de outbox identifica explicitamente o mecanismo reutilizado ou
  justifica uma implementação específica de Assessments e garante receipt
  durável por `(EventId, ConsumerKey)`;
- regrade está limitado à revisão, manifest, entrega e respostas originais;
- entrega concreta possui `itemOrder`, formato canônico persistido e regra de
  derivação de todo dado privado de correção;
- o reset global possui catálogo dos artefatos SQL fora do `IModel`, ordem de
  instalação e testes de equivalência em banco vazio;
- ownership de content, assessment, grading, grupo e gradebook está explícito;
- fórmula, conjunto elegível, ponto de quantização e validação dos pesos do
  gradebook estão fechados sem cálculo alternativo por consumer;
- pontos autorais, configuração adicional por item e capability executável
  possuem fontes distintas e sem campos duplicados;
- nenhuma entidade EF ou migration foi alterada neste marco.

Referências: [`00`](../00-domain-and-workflows.md),
[`01`](../01-contracts-and-persistence.md) e
[`06`](../06-gradebook-audit-and-operations.md).

## `SEQ-01`. Consolidar contratos, domínio e autorização

### Resultado

Os contratos finais, o domínio puro e a matriz de autorização possuem uma
única especificação executável antes do corte coordenado de API, web e
persistência.

### Implementação

- fixar a substituição atômica de `AIGraded`, `AutoGraded` e
  `InstructorGraded` por `AIReview`, `AutomatedReview` e `InstructorReview`;
- fixar `SelfReview = 16` e os valores canônicos `1`, `2`, `4` e `8`;
- definir `ReviewMethods` como o contrato final que substituirá
  `GradingMethods` no corte de `SEQ-03`;
- centralizar validação, ordenação e descrição dos nove workflows;
- criar `packages/features/grading-adapter-quiz` com o nome público
  `@game-guild/grading-adapter-quiz` e direção de dependência
  `grading <- grading-adapter-quiz -> quiz`;
- extrair de `@game-guild/grading` para o novo package toda implementação
  específica de quiz: `src/adapters/quiz/**`, os testes de quiz e seus test
  vectors. Reescrever a API pública contra `QuizEntry` e `QuizAnswer`, sem
  transportar `QuizBlockLike`, `QuizBlockStorageLike`, codificações textuais ou
  outras formas estruturais de compatibilidade. Remover o export
  `./adapters/quiz`, o export de test vectors de quiz e a dependência de
  `@game-guild/quiz` do package de grading no mesmo corte, sem aliases ou
  reexports de compatibilidade;
- manter em `@game-guild/grading` somente contratos, value objects, registry,
  agregação e algoritmos independentes do tipo de assessment. O adapter pode
  importar apenas as APIs públicas de `@game-guild/grading` e
  `@game-guild/quiz`; grading, quiz e quiz-surface não podem depender do adapter;
- migrar `@game-guild/quiz-content` para importar do adapter todos os helpers
  específicos de quiz e manter imports diretos de grading somente para seus
  contratos genéricos. A composição web também importa o adapter quando
  precisar executar essa integração;
- definir no adapter `QuizGradingItemInputV1 { itemId, entry }` como fronteira
  de entrada. `quiz-content` projeta sua ordem e seus blocos para essa lista; o
  adapter não importa `block-list`, `quiz-content` nem aceita storage estrutural
  genérico;
- definir no adapter os pontos de integração de quiz: projeção de itens,
  extração de answer key, learner redaction, envelope canônico de resposta,
  decoder/normalizador, classificação de capability, avaliação determinística
  e fixtures de conformidade. A aplicação web compõe o adapter TypeScript na
  borda; a API implementa os handlers C# correspondentes sob os mesmos contratos
  e fixtures, sem transferir ao browser workflow, persistência ou autoridade
  acadêmica;
- definir no servidor a porta genérica e agregada `IAssessmentTypeAdapter`,
  pertencente ao runtime de assessments e grading. Ela expõe projeção autoral,
  geração de entrega, decode/normalização de resposta e avaliação
  determinística e é resolvida uma única vez por
  `(contentType, adapterKey, adapterVersion, executionContext)`, sem mencionar
  tipos de quiz no core;
- manter as implementações C# de quiz em um módulo/assembly adapter explícito,
  dependente apenas da porta genérica e dos contratos públicos de quiz. O
  composition root registra um `QuizAssessmentTypeAdapter` por `contentType`,
  chave e versão; projector, delivery generator, decoder e algoritmo de quiz
  podem existir como detalhes internos, mas não são portas nem capabilities
  registradas separadamente. Core, autoria genérica, handlers genéricos, rounds
  e resultados não podem importar DTO, parser, entidade ou namespace específico
  de quiz. Exatamente uma versão por `ProgramContentType` é marcada como atual
  para autoria; versões anteriores continuam resolvíveis por manifests
  congelados. Um novo tipo de assessment recebe outro adapter sem alterar o core;
- fechar e versionar `ContentGradingDefinitionV2`,
  `AssessmentExecutionPolicyV1`, `AssessmentAuthoringSourceV1`,
  `AssessmentExecutionSnapshotV1`, `AssessmentResponseEnvelopeV1`,
  `GradeItemResultV1`, `GradeResultV1`, evidências, stages e rounds;
- definir no core `AssessmentResponseEnvelopeV1` com `contentType`,
  `payloadSchema` e payload opaco, e no adapter `QuizAnswerEnvelopeV1` como
  união discriminada fechada das 14 respostas. Proibir delimitadores textuais,
  JSON embutido em strings e coerções numéricas fora de campos textuais do
  próprio domínio;
- usar exclusivamente `GradeResultV1` e `GradeItemResultV1` nos contratos de
  execução; detalhes de quiz só podem aparecer em evidência versionada
  referenciada pelo resultado genérico;
- definir `ContentGradingDefinitionV2.items` por ID autoral estável e remover de
  cada valor as cópias de ID, `points` e `gradingKind`. Para quiz,
  `QuizEntry.points` é a única fonte mutável e passa a usar unidades inteiras
  escaladas compatíveis com `ScoreValue`; `ReviewMethods` expressa intenção
  autoral e o manifest expressa suporte executável;
- definir a projeção imutável de itens criada no servidor pelo adapter C# de
  quiz durante o prepare, contendo `itemId`, `maxScore`, tipo de origem e
  referências privadas necessárias. O package TypeScript fornece a referência
  de contrato e fixtures, mas nunca envia a projeção autoritativa ao servidor.
  A projeção integra o snapshot de execução, não volta ao draft e nunca se
  torna segunda fonte mutável;
- incluir em `AssessmentExecutionPolicyV1` a política explícita de contribuição
  de tentativas, obrigatória quando `maxAttempts > 1`;
- incluir `AssessmentContentCompletionPolicyV1` na policy materializada e
  congelada, com os modos e o default aprovados em `SEQ-00`;
- fechar `AssessmentResultReleasePolicyV1` desde o início com os modos
  `immediate`, `manual` e `scheduled`; `scheduledFor` usa instante UTC canônico.
  O contrato reserva o modo agendado, mas sua capability e operação só são
  habilitadas em `SEQ-15`;
- fechar `GradingExecutionV1`, cuja identidade é compartilhada pelos handlers,
  mas cujo owner persistente é um `AssessmentTestRunSubject` ou uma
  `AssessmentSubmission`, nunca ambos;
- definir `ReviewCapabilityDescriptorV1` por método e contexto de execução. Um
  handler pode declarar `AuthorTest`, `OfficialSubmission` ou ambos; readiness
  em teste nunca implica capacidade acadêmica oficial;
- definir `AssessmentExecutionManifestV1`, referenciado pela revisão imutável e
  coberto por `ExecutionSnapshotHash`, nunca por `AuthoringSourceHash`. O
  manifest fixa por item `adapterKey` e `adapterVersion`, e por stage as chaves
  e versões exatas de handler, policy e provider aplicáveis. A avaliação
  determinística pertence à versão do adapter, sem uma segunda identidade de
  algoritmo no core; capability disponível no deploy não substitui versão
  fixada;
- fechar `AssessmentExecutionDeliveryV1`, `DeliveryHash` e `DeliveryHashVersion`
  como contratos distintos do snapshot da revisão. A entrega referencia revisão
  e manifest, registra por item o prompt/challenge learner-safe materializado,
  `itemOrder`, ordenações internas concretas e metadados indispensáveis à
  reprodução, mas nunca answer key, regra privada ou score. `DeliveryHash`
  cobre o JSON canônico completo da entrega; seus bytes UTF-8 canônicos são a
  fonte persistida, e não uma serialização posterior de entidade ou `jsonb`;
- exigir do contrato de gerador que qualquer correção privada seja derivável de
  `AssessmentDefinitionRevision` mais `AssessmentExecutionDeliveryV1`. Um tipo
  que necessite material privado aleatório adicional não possui capability até
  que um novo contrato e seu impacto de schema sejam aprovados;
- definir resolução exata do manifest, catálogo de versões suportadas e
  preflight que bloqueia startup/deploy antes de receber tráfego quando uma
  revisão ativa, revisão retida elegível a regrade ou execução não terminal não
  puder ser resolvida. Versões referenciadas permanecem no artefato até a
  política de retenção autorizar sua retirada; regrade resolve sempre o manifest
  original da execução, sem atualização implícita;
- definir a transição interna neutra de conclusão de uma
  `GradingExecution`. Somente o adapter `OfficialSubmission` a transforma no
  evento acadêmico `GradeResultFinalized`; `AuthorTest` persiste apenas o
  resultado diagnóstico;
- fechar o comando idempotente `ReleaseGradeResult`, incluindo ator, permissão,
  submission, rodada esperada, versão de concorrência, idempotency key, motivo
  opcional e evento de auditoria;
- definir `AssessmentResultRelease` como dependente somente de `GradeRoundId`
  único. A submission é derivada pelo owner
  `GradeRound -> GradingExecution -> AssessmentSubmission`; não persistir um
  `AssessmentSubmissionId` redundante que possa divergir da rodada. O comando
  recebe a submission para autorização e escopo, mas valida essa cadeia antes
  de gravar o release. O `SCHEMA-GATE` deve ainda definir uma invariante
  verificável no banco que impeça release para rodada de `AuthorTest`, por
  desenho relacional ou por constraint de banco aprovada e testada quando FKs
  simples não conseguirem expressar essa condição transitiva;
- fechar `IdempotentCommandEnvelopeV1` para start, save de draft de tentativa
  ou evidência, submit, finalização de evidência e release, com escopo de
  tenant/recurso/comando, ator, chave, request hash canônico, outcome persistido
  e retenção. Mesma chave e mesmo hash retornam o outcome anterior; mesma
  chave e payload divergente geram conflito, sempre após autorização do ator;
- fechar `SaveCollectiveAttemptDraftV1` e `SubmitCollectiveAttemptV1` com
  `expectedVersion`, idempotency key, request hash e identidade do ator. Draft
  aceita edição somente por participante congelado até uma única finalização
  atômica. Na policy inicial, qualquer participante do snapshot pode salvar e
  finalizar; `StartedByUserId` não concede autoridade exclusiva e todo ator é
  auditado;
- fechar `SaveCollectiveSelfReviewDraftV1` e
  `SubmitCollectiveSelfReviewV1` sobre o mesmo envelope idempotente. A
  evidência usa storage genérico de `ReviewEvidence`, mas cada mutação aceita
  do draft registra ator, versões anterior e nova, request hash e instante; replay
  idêntico reutiliza o outcome sem duplicar auditoria;
- implementar value objects, fixtures e parsers puros de `ScoreValue` e
  `PercentValue` em C# e TypeScript. O valor serializado é sempre um inteiro de
  unidades escaladas; parsers de entrada humana recebem texto decimal com no
  máximo duas casas, sem usar `float` ou `decimal` para a conversão;
- alterar atomicamente o schema público de `QuizEntry.points` para inteiro de
  unidades escaladas, atualizar factories, schemas, editors, fixtures e adapter
  e rejeitar strings ou números fracionários em runtime, sem dual-read;
- substituir `SaveQuizAssessmentDraft`, seus DTOs e sua interface no core por
  `SaveAssessmentDraft`, `AssessmentDraftResult` e
  `IAssessmentAuthoringService`. O caso de uso descobre o `contentType` e o tipo
  persistido pela fronteira registrada do content, resolve
  `IAssessmentTypeAdapter` e não contém constante, branch, mensagem ou namespace
  específico de quiz;
- encerrar o ownership do atual `Assessment.DefinitionPayload`: remover o
  setter e o payload genéricos. Policies complexas sem coluna só podem possuir
  fonte mutável tipada e com nome específico se o `SCHEMA-GATE` justificar sua
  persistência; nenhum campo relacional pode ser repetido nesse payload;
- definir o lifecycle e os contratos de `IReviewStageHandler`;
- fechar o contrato da projeção de progresso de content avaliado, incluindo
  evento de origem, policy de conclusão, chave idempotente, estado projetado e
  tratamento individual ou por participante de grupo;
- inventariar todos os produtores e consumidores do corte atômico, sem criar
  aliases temporários;
- atualizar [`grading-serialization-map.md`](../../../types/grading-serialization-map.md)
  conforme os contratos finais.

### Matriz de autorização obrigatória

Definir e testar, para cada comando:

```text
ExecutionContext
AuthenticatedActor
RepresentedSubject
RequiredPermission
ServiceIdentity permitida
Resource scope
Audit event
```

A matriz deve cobrir:

- professor operando test run e suas personas simuladas;
- aluno dono de tentativa individual;
- integrante de tentativa coletiva;
- aluno revisor em `PeerReview`;
- aluno realizando `SelfReview`;
- instrutor corrigindo, sobrescrevendo ou reavaliando;
- worker determinístico;
- worker de provider externo;
- leitor de resultado retido e liberado.

Fechar também a matriz de sujeito coletivo:

- `InstructorReview` e `AutomatedReview` operam sobre uma única execução do
  grupo sem conhecer seus participantes;
- `SelfReview` coletivo produz uma única evidência compartilhada; qualquer
  participante congelado pode editá-la enquanto estiver em draft, e um submit
  final versionado fecha a evidência registrando o ator real;
- `PeerReview` mantém revisores individuais, exclui todos os participantes do
  grupo-alvo e agrega as evidências em um único resultado da submission
  coletiva;
- resolução de participantes e elegibilidade acontece antes dos handlers;
  projeção para integrantes acontece somente depois da finalização.

Persona simulada nunca substitui o ator autenticado no registro de auditoria.

### Testes

- round-trip C#/JSON/TypeScript preserva exatamente o inteiro de unidades;
- fixtures cross-language cobrem todas as variantes de
  `QuizAnswerEnvelopeV1`, cardinalidade, limites, payloads malformados e versão
  desconhecida;
- testes rejeitam matching delimitado, JSON dentro de string, coordenadas
  textuais e qualquer alias do antigo `StructuredAnswerPayload`;
- aceitação e rejeição de todas as bitmasks de `0` a `31`;
- ordem canônica independente da ordem textual das flags;
- parsing de entrada humana, formatação de exibição, comparação, overflow,
  arredondamento `half-up` e limites de scores e percentuais;
- fixtures provam que alterar somente o catálogo ou o manifest executável não
  muda `AuthoringSourceHash`, enquanto qualquer mudança autoral muda os dois
  hashes;
- fixtures provam que alterar apenas capability ou versão de algoritmo não
  modifica `ContentGradingDefinitionV2` nem `AuthoringSourceHash`;
- schemas estritos rejeitam `points`, `gradingKind`, `contentBlockId` ou ID
  duplicado dentro dos valores de `ContentGradingDefinitionV2.items`;
- fixtures provam que duas execuções da mesma revisão podem possuir entregas
  concretas distintas, mas que start/resume/retry da mesma execução preservam
  exatamente os mesmos bytes e `DeliveryHash`;
- fixtures provam que `itemOrder` e toda ordenação interna são semanticamente
  explícitos, sobrevivem ao round-trip C#/JSON/TypeScript e alteram
  `DeliveryHash` quando mudam;
- testes de capability rejeitam gerador cujo resultado correto dependa de estado
  privado aleatório não derivável da revisão e da entrega persistida;
- testes de contrato provam que o adapter agregado é resolvido uma única vez
  pela versão exata fixada no manifest e que suas quatro operações pertencem à
  mesma implementação;
- ausência de estado operacional em documentos de quiz;
- teste de arquitetura falha se `@game-guild/grading` importar ou declarar
  dependência de quiz, quiz-content, quiz-surface ou do adapter de quiz;
- teste de arquitetura falha se o adapter importar caminhos internos dos
  packages ou se quiz e quiz-surface dependerem do adapter;
- teste de arquitetura .NET falha se o core de assessments/grading referenciar
  o módulo, namespace, DTOs ou parsers do adapter C# de quiz, e confirma que o
  adapter depende somente das portas genéricas e dos contratos públicos
  permitidos;
- testes do composition root resolvem o adapter C# de quiz exclusivamente por
  `contentType`, `adapterKey` e `adapterVersion` do manifest, e rejeitam chave ou
  versão não registrada sem branch específico no core;
- teste de arquitetura varre todo o core de assessments/grading e falha diante
  de símbolos, DTOs, serviços, constantes, mensagens ou branches nomeados para
  quiz; somente o assembly `QuizAdapter` e seu composition root podem conhecer
  esse tipo;
- exports públicos e typecheck comprovam que consumidores específicos de quiz
  importam `@game-guild/grading-adapter-quiz`, enquanto consumidores genéricos
  continuam importando somente `@game-guild/grading`;
- autorização positiva e negativa para cada linha da matriz;
- capability aceita e rejeita separadamente `AuthorTest` e
  `OfficialSubmission` para o mesmo método;
- manifest resolve exatamente as versões fixadas e rejeita ausência,
  substituição implícita ou retirada de versão ainda referenciada;
- preflight de startup/deploy falha antes de servir tráfego quando o catálogo
  anunciado não cobre revisões ativas, revisões retidas elegíveis a regrade e
  execuções não terminais do ambiente;
- política de contribuição ausente ou não suportada rejeita
  `maxAttempts > 1`;
- contrato de `ReleaseGradeResult` rejeita rodada obsoleta, ator sem permissão
  e replay com payload divergente;
- contrato e testes relacionais rejeitam release cuja rodada não pertença à
  submission informada no comando, sem depender de IDs duplicados na linha de
  release;
- start, saves de draft de tentativa ou evidência, submit e finalização de
  evidência retornam o mesmo outcome para replay idêntico e conflito para a
  mesma chave com request hash diferente, sem duplicar evento append-only;
- comandos coletivos rejeitam versão obsoleta, escrita após finalização e ator
  fora do snapshot.

### Gate

- schemas, fixtures e módulos puros de referência concordam sobre o
  vocabulário final;
- contratos não possuem ownership duplicado;
- rounds, stages, resultados e envelopes do core não possuem tipos ou campos
  específicos de quiz;
- o core de grading não contém implementação, test vector, export ou
  dependência de quiz;
- o novo adapter é o único package TypeScript que implementa a tradução
  executável entre quiz e grading; consumidores podem compô-lo, mas não repetir
  sua lógica;
- todo comando planejado possui ator, sujeito, permissão e auditoria definidos;
- entidades EF, baseline e tabelas permanecem inalterados;
- o código integrado atual somente será substituído no corte atômico de
  `SEQ-03`, sem fase de compatibilidade.

## `SEQ-02`. Aprovar o schema do núcleo e o reset global

Artefato de aprovação: [`01-schema-gate.md`](./01-schema-gate.md).

### Resultado

Somente a persistência necessária para autoria, revisão imutável, test run e
runtime comum é apresentada como delta funcional. Separadamente, o gate mostra
o impacto operacional de substituir a cadeia global do `ApplicationDbContext`.
Estruturas específicas de peer, AI, grupo e integrações ficam para os gates de
suas fatias.

### Inventário mínimo do núcleo

- lifecycle de `Assessment`, ponteiro para revisão ativa e
  `AssessmentDefinitionRevision` imutável;
- remoção de `Assessment.DefinitionPayload`, `DefinitionSchemaVersion` e do
  setter genérico. Se algum payload autoral tipado continuar necessário, o gate
  apresenta nome, schema, owner e campos e prova que não replica colunas;
- `ReviewMethods` com constraint das combinações válidas;
- `AssessmentTestRun`, seus subjects sintéticos e seu estado isolado;
- `GradingExecution` como owner de stages, rounds, resultado por item,
  resultado final e evidências;
- `AssessmentResponseEnvelopeV1` canônico persistido uma única vez na
  `GradingExecution`, com content type, payload schema, bytes validados,
  imutabilidade após submit e limites de tamanho; não reutilizar a coluna ou o
  contrato antigo `StructuredAnswerPayload` como alias;
- `AssessmentExecutionDeliveryV1` persistido como parte da
  `GradingExecution`, com schema version, JSON canônico textual concreto
  learner-safe, `DeliveryHash`, versão do hash e imutabilidade após a
  materialização; não usar `jsonb` quando o contrato exige preservar os bytes
  canônicos usados no hash;
- owner relacional mutuamente exclusivo entre `AssessmentTestRunSubject` e
  `AssessmentSubmission`, com FKs e índices únicos, sem `ownerId` opaco;
- revisão imutável referenciada por `GradingExecution`;
- `AssessmentExecutionManifestV1` imutável, referenciado pela revisão e coberto
  por `ExecutionSnapshotHash`, incluindo lifecycle das versões executáveis
  referenciadas do adapter agregado, handlers, providers e policies;
- fonte autoritativa de `AssessmentContentCompletionPolicyV1`: se o modo inicial
  for global, não criar coluna para repetir constante; se for configurável por
  assessment, apresentar sua persistência e validação neste gate;
- `AuthoringSourceHash` e `ExecutionSnapshotHash`, com versões de algoritmo e
  responsabilidades distintas; somente o hash autoral participa do lifecycle
  `Draft`/`Published`/`ChangesPending`;
- finalização do resultado sem estado ou tabela de release no contexto
  `AuthorTest`;
- outbox transacional reutilizada ou específica de Assessments conforme o ADR,
  além de controle otimista de concorrência e confirmação durável por consumidor
  obrigatório. Se a infraestrutura atual não oferecer fan-out com
  deduplicação por `(EventId, ConsumerKey)`, apresentar neste gate a entidade de
  delivery/inbox, constraints, índices, lifecycle e retenção;
- armazenamento de deduplicação necessário ao envelope idempotente, com escopo,
  request hash, outcome, unique constraint e retenção explícitos; reutilizar a
  infraestrutura existente somente se ela provar essas invariantes;
- colunas `integer` para unidades escaladas de scores e percentuais usados pelo
  núcleo;
- conversão coordenada de todos os scores, pesos e percentuais acadêmicos para
  `ScoreValue` ou `PercentValue` inteiros, incluindo
  `Assessment.MaxScore`, `Assessment.PassingScore`,
  `AssessmentSubmission.Score`, `AssessmentPeerReview.Score`,
  `Program.PassingScore` e `AssessmentGroup.WeightPercent`, com inventário de
  entidades, DTOs, commands, queries, clients gerados e consumidores;
- índices, unique constraints, CHECK constraints, collation e tokens de
  concorrência;
- retenção e relações de exclusão das revisões, test runs e evidências.

Não antecipar neste gate:

- snapshot de participantes de grupo;
- claims e leases específicos de `PeerReview`;
- inbox ou payload específico de provider de IA;
- projeções de gradebook, passback ou notificações;
- estado acadêmico de release, que pertence à submission oficial em `SEQ-10`;
- tabelas criadas apenas para uma UI futura.

### Operação global obrigatória

Além do delta de grading, apresentar:

- quantidade e finalidade das migrations e snapshots removidos;
- catálogo de todos os artefatos SQL ativos fora do `IModel`, com arquivo de
  origem, dependências, owner, ordem de instalação e decisão explícita de
  incorporar ou remover cada artefato. Nenhuma remoção pode ocorrer apenas por
  ele não aparecer no snapshot EF;
- modelo completo produzido pelo `ApplicationDbContext` antes e depois;
- diff que prove ausência de alterações não aprovadas em outros módulos;
- diff dos catálogos PostgreSQL relevantes antes e depois, incluindo funções,
  procedures, triggers, views, policies, grants, índices especiais e extensões;
- procedimento de recriação dos bancos locais, de desenvolvimento e teste;
- impacto no CI, design-time factory e startup com `MigrateAsync`;
- rollback por Git do baseline, nunca rollback de dados;
- responsáveis pela coordenação com os demais módulos da API.

### Gate manual obrigatório

Apresentar ao responsável pelo projeto:

1. tabelas novas;
2. tabelas removidas;
3. colunas novas, removidas ou renomeadas;
4. constraints e índices;
5. entidades que deixam de persistir `decimal`, `double`, `float` ou texto e
   passam a persistir unidades `integer` acadêmicas;
6. transações e tokens de concorrência;
7. baseline EF global que será reescrito;
8. diff completo do modelo global;
9. política que retira eventos acadêmicos do publisher em processo e os grava
   na outbox dentro da transação;
10. catálogo e estratégia de reinstalação de todo SQL ativo que não pertence ao
    `IModel`, incluindo os testes que provarão equivalência funcional.

Somente uma aprovação explícita libera `SEQ-03`.

## `SEQ-03`. Criar o baseline global e a persistência do núcleo

### Resultado

Um banco vazio nasce do modelo global aprovado, que inclui o delta do núcleo de
grading. Não há conversão de banco anterior nem preservação de dados de
desenvolvimento.

### Implementação

- atualizar entidades e configurações EF conforme o artefato aprovado;
- converter atomicamente `Program.PassingScore`,
  `AssessmentGroup.WeightPercent` e os demais scores, pesos e percentuais
  acadêmicos existentes aprovados para `ScoreValue` ou `PercentValue` inteiro,
  atualizando entidades, DTOs, commands, queries, services, clients gerados e
  consumidores no mesmo corte;
- aplicar no mesmo corte os nomes `ReviewMethods`, `AIReview`,
  `AutomatedReview`, `InstructorReview` e `SelfReview` em API, web e packages;
- remover os nomes e contratos substituídos no mesmo corte;
- substituir, como operação global coordenada, a cadeia histórica de
  desenvolvimento por um único baseline de criação compatível com o startup
  que usa `MigrateAsync`;
- incorporar ao baseline limpo, ou a módulos de instalação chamados por ele,
  todos os artefatos SQL ativos aprovados em `SEQ-02`, preservando dependências,
  owners, grants e operações que exigem execução fora de transação. Não copiar
  a cadeia histórica: materializar diretamente somente o estado final aprovado;
- não criar migration incremental, migration de dados ou backfill;
- recriar bancos locais, de desenvolvimento e de teste afetados;
- implementar repositories e transações do núcleo;
- implementar `GradingExecution` com owner relacional e concorrência otimista;
- persistir na execução os bytes canônicos validados de
  `AssessmentResponseEnvelopeV1` e remover
  `AssessmentSubmission.StructuredAnswerPayload`, DTOs e mappers substituídos,
  sem manter cópia ou alias na submission;
- remover `Assessment.DefinitionPayload`, `DefinitionSchemaVersion`,
  `SetDefinition` e todos os consumidores genéricos; introduzir uma fonte
  autoral tipada específica somente se ela constar no schema aprovado;
- implementar a persistência imutável de `AssessmentExecutionDeliveryV1` dentro
  da execução como JSON canônico textual, garantindo materialização única,
  `itemOrder` explícito e leitura byte a byte idêntica em resume e retry;
- implementar registry capaz de resolver exatamente cada versão fixada pelo
  `AssessmentExecutionManifestV1`;
- registrar e resolver por versão exata um adapter agregado por tipo de
  assessment, além dos handlers e policies genéricos;
- publicar, em cada artefato, o catálogo exato de versões suportadas e executar
  preflight no startup/deploy contra as revisões ativas, revisões retidas
  elegíveis a regrade e execuções não terminais do ambiente; falhar antes de
  receber tráfego quando faltar uma versão;
- manter artefatos versionados e caminho de rollback enquanto revisões ou
  execuções dentro da retenção dependerem deles; retirada de versão exige
  preflight limpo e decisão explícita sobre regrade;
- adaptar o pipeline de domain events para mapear eventos acadêmicos duráveis
  em registros de outbox antes do commit;
- impedir que esses eventos acadêmicos sejam publicados diretamente pelo
  `IPublisher` executado dentro de `SaveChangesAsync`;
- despachar a outbox somente após commit, com worker e consumers idempotentes;
- capturar a rota de consumidores aplicável ao evento, deduplicar cada entrega
  por `(EventId, ConsumerKey)` e marcar a mensagem como concluída somente quando
  todas as entregas obrigatórias forem confirmadas; adicionar consumidor futuro
  não reprocessa histórico implicitamente;
- executar somas com intermediário `bigint`/`BigInteger`, validar overflow ao
  materializar `int32` e aplicar arredondamento inteiro `half-up` uma única vez;
- proibir `decimal`, `numeric`, `real`, `double precision` e texto para os
  valores acadêmicos convertidos.

### Testes

- criação do banco do zero;
- diff do modelo global sem drift fora do delta aprovado;
- comparação automatizada dos artefatos SQL aprovados fora do `IModel`,
  exercitando ao menos funções, triggers, grants/policies e índices especiais;
- testes funcionais dos artefatos críticos provam comportamento, não apenas a
  existência de seus nomes no catálogo;
- constraints de workflow e lifecycle;
- constraint de owner único de `GradingExecution`, cardinalidade
  test-run/subjects e integridade de suas FKs;
- a operação de persistência materializa uma única entrega por execução,
  rejeita segunda escrita divergente e retorna os mesmos bytes e `DeliveryHash`
  em leitura repetida; o lifecycle de start é exercitado em `SEQ-07`;
- response envelope submetido é imutável, reproduz os mesmos bytes em retry e
  regrade e não possui segunda cópia na submission;
- resolução do manifest permanece estável entre deploys e falha de forma
  explícita quando uma versão exata não está registrada;
- preflight rejeita o artefato incompatível antes do tráfego, e o artefato de
  rollback continua capaz de resolver as versões retidas;
- round-trip, ordenação e aritmética inteira de score e percentual;
- busca estática e testes de contrato comprovam ausência de `decimal`,
  `double`, `float`, formatos textuais e casts fracionários nos campos
  acadêmicos convertidos,
  inclusive nos consumidores de `Program.PassingScore`;
- rollback atômico, concorrência otimista e deduplicação da outbox;
- crash após commit e antes do dispatch não perde evento;
- retry do worker não duplica efeito;
- falha de um consumidor mantém somente sua entrega pendente e não apaga as
  confirmações dos demais; replay não duplica efeitos já confirmados;
- a rota de consumers é congelada na criação do evento; alterar o registry não
  modifica mensagens anteriores nem dispara replay implícito;
- evento acadêmico não percorre simultaneamente outbox e publisher em processo;
- busca e build comprovam ausência dos nomes substituídos.

### Gate

- o banco vazio é criado diretamente no schema global aprovado;
- nenhuma tabela de outro módulo mudou sem aprovação explícita;
- nenhum artefato SQL ativo de outro módulo desapareceu ou mudou sem aprovação
  explícita e teste funcional correspondente;
- não existe sequência de transformação nem compatibilidade com o modelo
  descartado;
- nenhuma regra de negócio depende apenas de constraint do banco;
- API, web e packages usam somente o vocabulário final.

## `SEQ-04`. Fechar autoria atômica no servidor

### Resultado

Quiz content e assessment são salvos em um único caso de uso e passam a formar
uma fonte canônica estável antes de qualquer revisão ser preparada ou publicada.

### Implementação

- criar `SaveAssessmentDraft` transacional e genérico;
- validar `QuizContentDocument`, `ContentGradingDefinitionV2`,
  `AssessmentExecutionPolicyV1` e `ReviewMethods` em conjunto;
- tratar `QuizEntry.points` como fonte única, normalizar
  `ContentGradingDefinitionV2` pelos IDs existentes e rejeitar item órfão ou
  campo derivado enviado pelo cliente;
- calcular no servidor `MaxScore` exclusivamente dos pontos projetados pelo
  adapter e as capabilities exigidas, sem gravá-las no contrato autoral;
- gravar content e criar ou atualizar o assessment no mesmo commit;
- remover policies operacionais duplicadas do JSON autoral;
- remover qualquer uso do setter genérico `SetDefinition`; toda fonte mutável
  restante é escrita pelo caso de uso tipado e possui ownership único;
- atualizar o host web para usar o caso de uso único;
- remover reconciliação e criação oportunista de assessment no browser no
  mesmo corte;
- impedir gravações independentes de campos com o mesmo owner.

### Gate

- falha ou concorrência não deixa content e assessment divergentes;
- o servidor rejeita documento ou workflow inválido;
- alterar pontos em uma questão atualiza `MaxScore` no mesmo commit e não
  existe outro campo mutável capaz de preservar o valor anterior;
- não existe mais um segundo caminho de save autoritativo;
- o draft salvo contém toda a fonte necessária para gerar uma revisão.
- não existe `DefinitionPayload` genérico capaz de competir com o draft tipado.

Referência: [`02`](../02-authoring-and-publication.md).

## `SEQ-05`. Fechar a fronteira learner-safe e implementar capabilities

### Resultado

Antes de materializar ou publicar qualquer execução, o servidor sabe projetar
cada item sem answer key, fecha as rotas genéricas que hoje podem expor o JSON
autoral e verifica capacidade real por contexto de execução.

### Implementação

- implementar no adapter a projeção learner-safe sem answer key, rubrica privada, prompt
  privado ou regra de correção; prompts e challenges públicos indispensáveis
  ao sujeito pertencem à entrega concreta da execução;
- registrar chave e versão executável do adapter agregado no capability registry,
  sem fallback silencioso para a versão mais recente;
- não registrar projector, gerador de entrega, decoder ou algoritmo como
  capabilities independentes: todos pertencem à versão indivisível do adapter;
- inventariar todos os endpoints e mappers de `ProgramContent` acessíveis a
  aluno, visitante ou integração pública;
- inventariar também todos os caminhos genéricos de escrita hoje usados por
  conteúdo avaliável ligado a assessment: `submitActivity` e seus consumidores em
  `activity-component.tsx` e `peer-review-interface.tsx`,
  `ProgramContentController.SubmitContent`,
  `ProgramWriteService.SubmitUserContentAsync`,
  `ProgramCrudController.MarkContentCompleted`,
  `ProgramCrudController.MarkMyContentCompleted`,
  `ContentInteractionController.UpdateProgress`,
  `ContentInteractionController.SubmitContent`,
  `ContentInteractionController.CompleteContent`,
  `ContentInteractionService.UpdateProgressAsync`,
  `ContentInteractionService.SubmitContentAsync`,
  `ContentInteractionService.CompleteContentAsync`,
  `ActivityGradeController`, `ActivityGradeService`, `ContentProgressService`,
  `ProgramEnrollmentService`, clients gerados, actions, testes e todas as
  projeções e consultas de progresso correspondentes;
- separar DTO autoral, autorizado somente para gestão do curso, de DTO
  learner-safe; nenhuma rota learner/public pode retornar `JsonBody` autoral;
- remover o DTO autoral das rotas genéricas learner/public no mesmo corte. Até
  o start oficial existir em `SEQ-10`, conteúdo avaliável permanece fail-closed e
  só pode ser executado no test run pela projeção segura;
- fazer as rotas genéricas de submit, conclusão, atualização de progresso e
  grade rejeitarem no servidor qualquer conteúdo ligado a assessment com
  workflow de review ativo.
  `ContentInteraction` pode continuar servindo conteúdo não avaliável,
  telemetria de consumo e, se aprovado no ADR, read model projetado pelo fluxo
  canônico; nunca pode armazenar respostas nem receber conclusão ou grade por
  comando genérico para esse quiz;
- versionar o bundle de execução e o envelope de respostas por item;
- definir que o bundle referencia a entrega persistida e nunca aceita do
  browser variáveis geradas, seed, ordem inicial ou outro dado que determine o
  challenge oficial;
- validar no servidor item, tipo, cardinalidade e limites do payload;
- impedir score, feedback oficial ou estado de grading no payload do
  respondente;
- adaptar `QuizPlayer` para consumir o bundle e emitir resposta canônica sem
  executar grading oficial no browser;
- implementar `IReviewCapabilityRegistry` e o registro de handlers/providers;
- consultar capabilities por `ReviewMethod` e `ExecutionContext`, sem promover
  capability de `AuthorTest` para `OfficialSubmission`;
- distinguir capability estrutural, provider registrado e health transitório;
- o adapter rejeita internamente qualquer tipo de item para o qual não consiga
  cumprir integralmente projeção segura, entrega, decode e avaliação suportada;
- bloquear prepare/publish quando algum item não puder ser projetado com
  segurança.

### Testes

- snapshots confirmam ausência de dados privados;
- snapshots distinguem dados learner-safe da entrega e dados privados do
  grading, inclusive em questões `Numeric`, `Formula`, `Ordering`, `Matching` e
  `FillInTheBlank` com variação de apresentação;
- testes diretos de todas as rotas learner/public confirmam que query,
  expansão, mapper alternativo e endpoint genérico não expõem `JsonBody`,
  answer key, rubrica ou policy privada;
- testes de rota e service confirmam que todo submit, complete, update progress
  ou grade genérico de conteúdo ligado a assessment com review ativo é
  rejeitado antes de criar ou alterar
  `ContentInteraction`, progresso, `ActivityGrade` ou outro resultado
  acadêmico;
- payload que tenta substituir challenge, variáveis, seed ou ordem congelada é
  rejeitado e não altera a entrega persistida;
- todos os tipos suportados fazem round-trip de resposta;
- versões futuras desconhecidas são rejeitadas;
- capability ausente produz diagnóstico acionável;
- editar o draft não altera fixture de revisão já materializada em teste.

### Gate

- nenhum workflow pode ser preparado para test run sem adapter completo e
  capability `AuthorTest` registrada;
- nenhum workflow pode ser publicado oficialmente sem capability
  `OfficialSubmission` registrada;
- não existe rota learner/public capaz de recuperar DTO autoral; o corte de
  segurança não fica adiado ao primeiro E2E acadêmico;
- não existe rota genérica capaz de receber resposta ou produzir efeito
  acadêmico para conteúdo ligado a assessment com review ativo;
- o browser não possui caminho para produzir resultado oficial;
- registry básico existe antes de qualquer validação de provider em publish.

Referências: [`01`](../01-contracts-and-persistence.md) e
[`05`](../05-learner-attempts-and-results.md).

## `SEQ-06`. Implementar revisão imutável, publicação e UX autoral

### Resultado

O assessment possui draft autoral, revisão candidata imutável e referência
explícita para a revisão ativa utilizada por execuções. A infraestrutura de
publish está pronta, mas a produção continua fail-closed enquanto não houver
capability `OfficialSubmission` real.

### Implementação

- implementar `PrepareAssessmentRevision` e `PublishAssessmentRevision` como
  casos de uso genéricos;
- materializar `AssessmentExecutionPolicyV1` no servidor a partir do draft
  atômico;
- construir `AssessmentAuthoringSourceV1` por DTO explícito e calcular
  `AuthoringSourceHash` com JCS, SHA-256 e
  `AuthoringSourceHashVersion = "sha256-jcs-v1"`;
- materializar `AssessmentExecutionManifestV1` no prepare, construir
  `AssessmentExecutionSnapshotV1` com a fonte autoral e o manifest e calcular
  `ExecutionSnapshotHash` com JCS, SHA-256 e
  `ExecutionSnapshotHashVersion = "sha256-jcs-v1"`;
- no publish, resolver e validar exatamente o manifest já persistido na
  candidata; não reconstruir, substituir ou atualizar suas chaves, versões,
  bytes canônicos ou `ExecutionSnapshotHash`;
- impedir publish quando o `AuthoringSourceHash` atual divergir da candidata;
  mudança de deploy, catálogo ou health não cria `ChangesPending`;
- impedir prepare, publish ou start quando uma versão exata do manifest não
  puder ser resolvida; health transitório não altera o manifest já fixado;
- validar workflow, adapter agregado, handlers e provider por contexto:
  prepare/test exige `AuthorTest`, enquanto publish exige
  `OfficialSubmission`;
- manter health de provider como requisito de execução, não da definição;
- substituir checkboxes por review primário exclusivo e toggle de revisão
  final do instrutor;
- mostrar sequência, capabilities, draft, candidata, publicação e alterações
  pendentes;
- permitir configurar na tela de assessment a política inicial de liberação
  de resultado e feedback com `immediate` e `manual`, validar sua combinação
  com o workflow e materializá-la em `AssessmentExecutionPolicyV1`; o modo
  `scheduled` já pertence ao contrato V1, mas permanece indisponível na UI e na
  execução até `SEQ-15`;
- configurar passing score em pontos absolutos e separar grupo/peso do
  workflow;
- mostrar a policy de conclusão de content e, quando o ADR a tornar configurável
  por assessment, permitir alterá-la na mesma gravação atômica;
- permitir salvar métodos ainda indisponíveis e preparar somente quando o
  contexto de teste estiver disponível; bloquear publish com causa explícita
  até existir capability oficial;
- implementar `UnpublishAssessmentRevision` com autorização, auditoria,
  versão esperada do ponteiro ativo e idempotência. O comando remove somente a
  referência ativa, bloqueia novos starts oficiais e não altera nem apaga
  revisões, test runs ou execuções já iniciadas;
- comprovar a infraestrutura de publish com um capability registry controlado
  somente em testes de contrato, sem registrar handler falso ou ativar revisão
  na configuração de produção.

### Gate

- draft, candidata e ativa têm lifecycle inequívoco;
- `Published` e `ChangesPending` dependem somente de
  `AuthoringSourceHash`; `ExecutionSnapshotHash` prova os bytes e versões
  executados sem redefinir estado autoral;
- uma execução sempre referencia revisão imutável;
- a mesma revisão resolve o mesmo manifest executável antes e depois de deploy;
- o contrato de publish controlado preserva `revisionId`, bytes canônicos do
  manifest e `ExecutionSnapshotHash` produzidos no prepare; o E2E completo até
  test run e start oficial é comprovado em `SEQ-10`;
- publicação concorrente não ativa revisão obsoleta;
- alterar a política de liberação marca a revisão ativa como
  `ChangesPending`, e a candidata preserva exatamente a policy configurada;
- testes de contrato provam que o publisher ativaria exatamente a candidata
  preparada diante de uma capability oficial controlada;
- unpublish concorrente ou repetido não remove outra revisão ativa, não apaga
  histórico e deixa o assessment indisponível para novos starts; a preservação
  de tentativas já iniciadas será comprovada no E2E de `SEQ-10`;
- a UI de produção bloqueia publish enquanto nenhum handler declarar
  `OfficialSubmission`;
- nenhum método indisponível é tratado como executável.

## Definição de pronto da Parte 1

- todos os gates de `SEQ-00` a `SEQ-06` estão satisfeitos;
- todo impacto relacional foi aprovado antes da edição e o banco vazio nasce
  do baseline global aprovado, sem migration incremental ou dado migrado;
- contratos C# e TypeScript passam em round-trip e usam o mesmo vocabulário;
- `@game-guild/grading` não possui dependência de quiz, e seus contratos de
  resposta e resultado não expõem campos específicos desse domínio;
- fixtures das 14 variantes de `QuizAnswerEnvelopeV1` passam nos contratos
  TypeScript e C#, sem subcodificações textuais;
- autoria de content avaliável e assessment é transacional e possui um único
  caminho genérico de escrita autoritativo;
- rotas learner/public não expõem DTO autoral, answer key ou rubrica privada;
- revisão candidata é imutável, hash-verificada e vinculada ao manifest de
  execução aprovado;
- `GradingExecution` possui contrato e persistência aprovados para uma entrega
  concreta imutável, e o manifest fixa o adapter agregado por versão exata;
- regrade permanece na revisão, manifest, entrega e respostas originais da
  execução; não existe caminho que troque definição dentro de uma rodada
  posterior;
- todos os scores, pesos e percentuais acadêmicos já existentes no baseline,
  inclusive `Program.PassingScore`, usam inteiros escalados por `100` e não
  possuem consumidor fracionário ou textual remanescente;
- `QuizEntry.points` usa unidades inteiras e não existe outra cópia autoral
  mutável dos pontos do item;
- preflight de versão executável bloqueia deploy incompatível antes do tráfego
  e existe política operacional de retenção e rollback;
- a infraestrutura de prepare funciona com registry controlado nos testes, mas
  a UI de produção permanece sem candidata executável enquanto nenhum handler
  real declarar `AuthorTest`; a primeira candidata real é preparada em
  `SEQ-08`. Publish e execução oficial permanecem fail-closed;
- testes unitários, de contrato, integração, banco vazio, segurança e E2E
  autoral da parte passam em CI.

## Gate para a Parte 2

A Parte 2 só pode começar depois de uma revisão explícita das evidências acima.
Não basta concluir os PRs: baseline, contratos publicados, fixtures, matriz de
autorização e relatórios de teste devem estar versionados e sem pendência
classificada como bloqueadora.

## Acompanhamento

| Marco | Status | Evidência |
| --- | --- | --- |
| `SEQ-00` | pendente | ADRs aprovados |
| `SEQ-01` | pendente | contratos, adapter de quiz isolado, domínio e autorização |
| `SEQ-02` | pendente | schema do núcleo, entrega e reset global aprovados |
| `SEQ-03` | pendente | baseline global e persistência imutável da entrega |
| `SEQ-04` | pendente | testes de save draft atômico |
| `SEQ-05` | pendente | rotas learner/escritas genéricas fechadas e registry versionado |
| `SEQ-06` | pendente | prepare, policies autorais, publish fail-closed e E2E autoral |
