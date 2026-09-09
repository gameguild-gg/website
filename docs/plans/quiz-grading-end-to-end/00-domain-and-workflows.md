# 00. Domínio e workflows

## Objetivo

Fechar o vocabulário e as invariantes antes de alterar API, banco ou UI. Esta
fase elimina a ambiguidade entre capacidade técnica de uma questão, origem do
review e etapa de grading que publica o resultado.

## Estado atual

- `AssessmentGradingMethod` usa `PeerReview = 1`, `AIGraded = 2`,
  `AutoGraded = 4` e `InstructorGraded = 8`;
- `SelfReview = 16` ainda não existe;
- `Assessments.GradingMethods` persiste uma bitmask;
- a constraint atual permite qualquer combinação dos quatro bits conhecidos;
- a web mostra flags técnicas como checkboxes independentes;
- o host de quiz cria `AutoGraded,InstructorGraded` fixo;
- `PeerReview` já possui fluxo de atribuição e envio de avaliações entre
  alunos, ainda sem agregação no resultado oficial;
- a API ainda não executa os métodos de review como um pipeline;
- `Assessment` não possui lifecycle próprio de draft/publicação nem referência
  para uma revisão imutável da definição;
- stages, rodadas, evidências e resultado ainda não possuem uma raiz persistente
  compartilhada entre test run e submission oficial;
- scores acadêmicos do módulo ainda não possuem semântica uniforme e
  pesos/percentuais ainda usam `decimal`, embora o contrato final exija
  inteiros de escala `100`;
- `Assessment.PassingScore` e `Program.PassingScore` são usados com semânticas
  concorrentes em partes diferentes do sistema.

A API também possui `AssessmentType.SelfAssessment`. Esse enum descreve o tipo
da atividade e não substitui o futuro `SelfReview`. Um quiz autoavaliado
continua sendo `AssessmentType.Quiz` com o método `SelfReview`.

Arquivos centrais:

```text
apps/api/Source/Modules/GameGuild.Learning.Assessments/Models/AssessmentGradingMethod.cs
apps/api/Source/Modules/GameGuild.Learning.Assessments/Entities/Assessment.cs
apps/api/Source/Modules/GameGuild.Learning.Assessments/Services/AssessmentService.cs
apps/web/src/lib/learning/assessment-grading-methods.ts
apps/web/src/components/learning/console/courses/[course]/assessments/[assessmentId]/assessment-editor.tsx
```

## Renomeação canônica

| Atual | Alvo | Valor |
| --- | --- | ---: |
| `PeerReview` | `PeerReview` | 1 |
| `AIGraded` | `AIReview` | 2 |
| `AutoGraded` | `AutomatedReview` | 4 |
| `InstructorGraded` | `InstructorReview` | 8 |
| inexistente | `SelfReview` | 16 |

Os nomes mudam de `Graded` para `Review` porque identificam a origem da
avaliação. O resultado continua sendo grading. Atualizar C#, TypeScript, DTOs,
serialização e testes na mesma fase, sem aliases permanentes.

## Modelo-alvo

### Reviews primários

| Método | Ator | Natureza do resultado |
| --- | --- | --- |
| `PeerReview` | alunos revisores elegíveis | agregação das avaliações feitas nas submissões de outros alunos |
| `AIReview` | serviço de IA | probabilístico, versionado e explicável |
| `AutomatedReview` | serviço determinístico | reproduzível a partir da definição e da resposta |
| `SelfReview` | aluno da tentativa | autoavaliação declarada e validada |
| `InstructorReview` | instrutor autorizado | avaliação docente integral |

### Base existente para `PeerReview`

O código atual chama a flag de `PeerReview = 1` e já implementa a coleta das
avaliações individuais:

- aluno com submissão própria reivindica uma submissão elegível de outro aluno;
- a submissão é apresentada anonimamente;
- o revisor envia feedback e score direto ou baseado em rubrica;
- o autor recebe a revisão sem identidade do revisor;
- o instrutor pode consultar revisões e identidades.

O pipeline completará essa infraestrutura como método inicial de review.
Ainda será necessário definir quantas avaliações cada submissão deve receber,
como os scores são agregados, quando o estágio termina e como o resultado é
projetado em `AssessmentSubmission`. `PeerReviewsRequiredCount` hoje representa
quantas revisões cada aluno deve realizar; ele não responde, sozinho, quantas
revisões cada submissão precisa receber.

`AssessmentPeerReview` pode continuar sendo o registro de claim/evidência
individual se o `SCHEMA-GATE` confirmar ownership e invariantes. Ele não
continua como autoridade paralela de score agregado: o submit alimenta
`ReviewEvidence`, e somente o handler da `GradingExecution` conclui o stage e
produz o resultado.

### Revisão docente

Quando `InstructorReview` acompanha outro método, ele deixa de ser o método
inicial e passa a ser a etapa final obrigatória:

```text
resultado primário -> aguardando instrutor -> aprovado ou alterado -> final
```

Uma bitmask não armazena ordem. A ordem deve ser inferida pela regra canônica,
independentemente da ordem textual recebida no JSON.

### Combinações válidas no modelo completo

| Decimal | Flags | Estágios |
| ---: | --- | --- |
| 1 | `PeerReview` | alunos revisores |
| 2 | `AIReview` | IA |
| 4 | `AutomatedReview` | determinístico |
| 8 | `InstructorReview` | instrutor |
| 9 | `PeerReview,InstructorReview` | alunos revisores -> instrutor |
| 10 | `AIReview,InstructorReview` | IA -> instrutor |
| 12 | `AutomatedReview,InstructorReview` | determinístico -> instrutor |
| 16 | `SelfReview` | aluno |
| 24 | `SelfReview,InstructorReview` | aluno -> instrutor |

`None = 0` pode existir durante rascunho, mas publicação de assessment avaliado
exige um workflow válido e todas as capabilities necessárias.

## Invariantes do domínio

1. Exatamente um método inicial de review deve existir na publicação.
2. `InstructorReview` pode ser o método inicial ou a última etapa, nunca uma etapa
   intermediária.
3. Duas flags só são válidas quando uma delas é `InstructorReview`.
4. Três ou mais flags são sempre inválidas.
5. Grupo e peso não alteram o método inicial de review.
6. Somente a última etapa de grading configurada finaliza a submissão.
7. Cada estágio valida o papel do ator no servidor.
8. Alterar workflow não reinterpreta silenciosamente tentativas iniciadas; a
   tentativa usa o snapshot vigente no início.
9. Regrade posterior à finalização é uma operação distinta e auditável.
10. `AssessmentType` e `ReviewMethods` permanecem dimensões independentes.
11. Método sem capability operacional pode ser salvo em draft. Capability
    `AuthorTest` permite preparar/testar; publicação exige separadamente
    `OfficialSubmission` para todos os estágios.
12. Preparar/testar congela uma revisão candidata; publicar ativa exatamente
    essa candidata se o hash do draft ainda coincidir. Salvar draft não altera
    a revisão usada por tentativas existentes.
13. A visibilidade do content não substitui o estado de publicação do
    assessment.
14. Score, score máximo, score por item e agregações usam inteiros de escala
    `100` na persistência e no wire format.
15. `Assessment.PassingScore` é um valor absoluto na escala do assessment;
    `Program.PassingScore` é o percentual aplicado ao resultado global do curso.
16. `ReviewMethods` descreve o workflow publicado; indisponibilidade transitória
    de um provider altera o estado operacional, não reescreve o workflow.
17. `AutomatedReview` avalia todos os itens para os quais exista regra
    determinística e devolve os demais como `pending` ou `unsupported`; falta de
    cobertura não transforma o estágio em erro.
18. Em `AutomatedReview,InstructorReview`, o instrutor recebe o resultado
    parcial, resolve os itens pendentes e pode alterar os já avaliados.
19. Em `AutomatedReview` isolado, itens não determinísticos impedem somente a
    finalização da rodada. A política de UX/publicação para essa configuração
    permanece uma decisão de produto; o runtime nunca fabrica zero nem falha o
    estágio por falta de avaliador.
20. Finalizar um `GradeResult` e liberá-lo ao aluno são transições diferentes.
    Gradebook consome finalização; aluno e notificação consomem liberação.
21. Scores, pesos e percentuais acadêmicos persistidos usam inteiros de escala
    `100`; nenhuma tabela do fluxo mantém `decimal` ou ponto flutuante.
22. Quiz atribuído a grupo cria uma única submission coletiva, uma única rodada
    e um único `GradeResult`; os integrantes recebem projeções do resultado
    depois da finalização.
23. A composição do grupo é congelada no início da tentativa. Alterações
    posteriores de membresia não reescrevem resultado nem participantes.
24. O grading não conhece integrantes do grupo: recebe somente a submission e
    produz um resultado. Resolução do sujeito ocorre antes; fan-out de projeções
    ocorre depois.
25. `GradingExecution` é a raiz de stages, rodadas, evidências e resultados e
    possui exatamente um owner relacional: `AssessmentTestRunSubject` ou
    `AssessmentSubmission`, nunca ambos. Um test run agrega um ou mais sujeitos
    sintéticos; cada sujeito possui sua própria execução e resultado.
26. Somente a submission oficial possui policy e estado de liberação acadêmica.
    Test run termina em resultado diagnóstico e nunca em
    `GradeResultReleased`.
27. Em `SelfReview` coletivo existe uma única evidência compartilhada por rodada;
    qualquer participante congelado pode editar o draft, mas um único submit
    versionado o finaliza e registra o ator real.
28. Em `PeerReview` coletivo, os revisores continuam individuais, nenhum membro
    do grupo-alvo é elegível e a agregação produz um único resultado coletivo.
29. Conclusão de test run é diagnóstica e nunca emite
    `GradeResultFinalized`/`GradeResultReleased`; somente o adapter de
    submission oficial produz eventos acadêmicos.
30. Quando `maxAttempts > 1`, uma policy canônica determina exatamente uma
    contribuição efetiva para gradebook e integrações de score. A policy
    separada de conclusão define em qual transição o content progride e sua
    semântica diante de múltiplas tentativas. O corte inicial seleciona uma
    tentativa para score; agregações como média são modos distintos. Sem policy
    de contribuição implementada, múltiplas tentativas são inválidas.
31. Liberação manual ocorre pelo comando idempotente `ReleaseGradeResult`, com
    autorização, rodada esperada, concorrência e auditoria.
32. A revisão fixa definição e versões executáveis; cada `GradingExecution`
    materializa uma entrega concreta imutável com `itemOrder` explícito e JSON
    canônico textual. Resume e retry não regeneram challenge nem aceitam sua
    substituição pelo cliente.
33. Quiz ligado a assessment não recebe resposta, conclusão, progresso ou nota
    por rotas genéricas. Somente eventos canônicos da submission podem alimentar
    sua projeção de progresso.
34. Regrade permanece na revisão, manifest, entrega e respostas originais da
    execução. Avaliar outra definição cria nova submission e nova execução; não
    é uma rodada da execução anterior.
35. Release pertence a uma rodada. Regrade não apaga nem oculta implicitamente a
    última rodada já liberada; a nova visão learner começa somente no release da
    nova rodada.
36. Resultado finalizado pode alimentar o gradebook interno antes do release,
    mas nenhum DTO, workspace, dashboard ou agregado learner pode expor ou
    permitir inferir contribuição retida.
37. Todo dado privado necessário à correção de uma entrega gerada é derivável da
    revisão imutável mais a entrega pública concreta. Gerador incapaz de cumprir
    isso não possui capability na versão inicial.
38. Cada consumidor obrigatório de um evento acadêmico confirma sua própria
    entrega durável; falha de um consumer não apaga confirmações dos demais.
39. `PeerReview` sem evidência mínima após o prazo entra em
    `AwaitingInstructorResolution`; nunca recebe zero nem permanece sem caminho
    operacional terminal.
40. Conclusão de content dependente de aprovação usa
    `on-release-and-pass`: enquanto a rodada estiver retida ou agendada, nenhum
    progresso learner-visible pode revelar `Passed`.
41. Release pertence somente a `GradeRound`. A submission é derivada pelo owner
    da `GradingExecution`; não existe ID de submission duplicado na linha de
    release capaz de divergir dessa cadeia, e o banco rejeita release para
    rodada pertencente a `AuthorTest`.
42. Unpublish remove somente o ponteiro de revisão ativa, bloqueia novos starts
    e preserva revisões, test runs e submissions já iniciadas.
43. Crédito parcial determinístico é parte da versão do algoritmo. Matching usa
    a proporção de pares corretos e Ordering usa a proporção de itens na posição
    absoluta correta; ambos aplicam aritmética exata e uma única quantização de
    `ScoreValue`. Alterar a fórmula exige nova versão no manifest.

## Estados conceituais

Lifecycle autoral do assessment:

```text
Draft                    nenhuma revisão publicada
Published                draft atual coincide com a revisão ativa
ChangesPending           draft diverge da revisão ativa
```

Lifecycles coordenados de submission e `GradingExecution`:

```text
InProgress
Submitted ou Late
AwaitingPrimaryReview
PrimaryReviewed
AwaitingInstructorReview  somente quando InstructorReview acompanha o primário
AwaitingInstructorResolution  evidência peer insuficiente após o prazo
Regrading
Graded
Returned
```

Lifecycle independente de liberação do resultado:

```text
Withheld
Scheduled
Released
```

`Graded`/`Finalized` significa que o resultado acadêmico está pronto e pode
alimentar o gradebook interno. Somente `Released` permite exibir nota e feedback
ao aluno. Release é versionado por rodada; durante regrade, a última rodada
liberada continua learner-visible até o release da substituta. Esse lifecycle
existe somente para submission oficial; a política de feedback decide a
transição entre esses estados.

`AwaitingPrimaryReview` deve ser projetado em estados específicos para a UI e
as filas: `AwaitingPeerReview`, `AwaitingAIReview`,
`AwaitingAutomatedReview`, `AwaitingSelfReview` ou
`AwaitingInstructorReview`.

## Contrato de UX

Esta fase apenas define como a escolha deverá ser apresentada. A implementação
da tela pertence à fase de autoria e publicação.

Substituir os checkboxes por:

1. seleção exclusiva `Quem fará o review inicial?`;
2. toggle `Exigir revisão final do instrutor`, oculto quando
   `InstructorReview` já é o método inicial;
3. resumo visual da sequência antes de salvar;
4. configuração específica do método abaixo da seleção;
5. capabilities de test run e de execução oficial apresentadas separadamente
   junto do método;
6. bloqueio de publish, com motivo, quando o provider não estiver registrado ou
   não declarar a capability exigida;
7. indisponibilidade transitória apresentada como estado operacional e retry,
   sem obrigar nova publicação.

Rótulos de produto:

```text
Avaliação entre alunos
Avaliação por IA
Correção automática
Autoavaliação
Avaliação pelo instrutor
```

## Tarefas

- [ ] renomear enum, propriedade, DTOs e serialização para o vocabulário de
  review;
- [ ] adicionar `SelfReview = 16` sem renumerar os valores existentes;
- [ ] criar helper de domínio para identificar review primário, review final e
  ordem;
- [ ] centralizar `ValidateReviewMethods` na API;
- [ ] representar os nove workflows em helper client-safe TypeScript;
- [ ] rejeitar combinações inválidas em create, update e publish;
- [ ] validar capabilities de `AuthorTest` no prepare/test e de
  `OfficialSubmission` no publish;
- [ ] distinguir provider registrado/compatível de provider saudável no
  runtime;
- [ ] impedir que serialização dependa da ordem de um `Set`;
- [ ] adicionar testes unitários para todos os valores de `0` a `31`;
- [ ] adicionar invariantes de resultado parcial de `AutomatedReview`;
- [ ] modelar finalização e liberação como estados independentes;
- [ ] modelar submission coletiva e snapshot de participantes para quiz em
  grupo;
- [ ] modelar `GradingExecution` com owner relacional exclusivo entre subject
  sintético de test run e submission oficial;
- [ ] manter release acadêmico fora do contexto de test run;
- [ ] manter `GradeResultFinalized` fora do contexto de test run;
- [ ] definir contribuição canônica de tentativas e comando explícito de
  liberação;
- [ ] fechar ADRs de publicação, score e histórico antes de alterar o baseline
  de schema;

## Critério de saída

- API e web concordam sobre nomes, valores e os nove workflows;
- nenhuma combinação inválida pode ser salva como configuração publicável;
- draft pode manter método sem provider/capability, mas publish explica e
  bloqueia apenas configuração ausente ou incompatível;
- uma revisão publicada continua válida durante indisponibilidade transitória
  do provider;
- mudar peso ou grupo não muda as flags;
- `ReviewMethods` seleciona a origem da avaliação e `GradeResult` representa o
  efeito produzido;
- item sem avaliador determinístico permanece explícito e não causa erro nem
  score sintético;
- resultado finalizado não é confundido com resultado já liberado;
- tentativa de grupo gera uma avaliação, uma auditoria e um regrade, com
  projeções idempotentes para cada participante;
- os cinco métodos não são confundidos em código, texto ou testes.
