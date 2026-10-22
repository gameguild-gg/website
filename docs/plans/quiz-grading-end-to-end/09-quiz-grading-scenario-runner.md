# Infraestrutura de cenários de grading de quiz

Status: implementado e sucedido arquiteturalmente pelo plano
[`11-web-only-grading-validation.md`](./11-web-only-grading-validation.md).

Data original: 2026-10-14.

Atualizado: 2026-10-20.

## Resultado preservado

Este plano introduziu a base reproduzível usada para validar o grading de quiz:

- manifesto local versionado, escrita atômica e lock por cenário;
- ownership explícito e reset limitado aos recursos registrados;
- rejeição de hosts remotos;
- personas isoladas de instrutor, alunos e outsider;
- checkpoints retomáveis;
- cenários individuais, matriz de reviews e matriz de tipos de questão;
- criação de cursos, conteúdos, assessments, revisões, matrículas, tentativas,
  reviews, release e gradebook exclusivamente por APIs oficiais;
- evidências sanitizadas com IDs estáveis;
- nenhuma alteração de schema, tabela ou migration.

Os workflows executáveis abrangem `AutomatedReview`, `InstructorReview`,
`SelfReview`, `PeerReview` e as combinações terminadas por
`InstructorReview`. O cenário coletivo permanece validado pelo contrato de API,
e `AIReview` continua bloqueado sem provider aprovado.

## Decisão atual

A infraestrutura deixou de oferecer uma CLI operacional. A única superfície
humana para listar, preparar, atualizar, verificar e remover cenários é:

```text
/workspace/learning/grading-validation
```

Playwright continua executável por comandos de teste convencionais, com setup
e teardown próprios. O setup autentica um `SystemAdmin` local e prepara o
cenário pela página; as jornadas usam contextos independentes para cada
persona; o teardown consolida as evidências e remove somente recursos do
manifesto.

## Organização vigente

```text
apps/web/src/lib/grading-scenarios/
  contracts.ts
  registry.server.ts
  service.server.ts
  actions.ts
  adapters/quiz/
    adapter.server.ts
    definitions.mjs
    capability-catalog.mjs
    manifest.server.mjs
    provision.server.mjs
    verify.server.mjs
    cleanup.server.mjs

apps/web/src/components/learning/grading-validation/
  grading-scenario-lab.tsx

e2e/learning/quiz-grading/
  support/
  *.spec.mjs
```

O núcleo e a UI recebem somente contratos genéricos. Quiz conhece seus
payloads, capabilities e lifecycle dentro do adapter. Um futuro tipo de
assessment deve registrar outro adapter, sem adicionar condicionais de quiz ou
code ao núcleo.

## Invariantes mantidos

1. A ferramenta é local, desabilitada em produção e exige `SystemAdmin`.
2. API URL, tenant e ator não são aceitos do browser.
3. Credenciais não entram em logs, traces ou relatórios.
4. Preparação e cleanup usam APIs e autorização reais, nunca SQL direto.
5. O manifesto comprova ownership antes de qualquer remoção.
6. Falha de cleanup preserva o manifesto para recuperação posterior.
7. Resultado gerado é candidato; nunca altera o status canônico sem revisão
   humana.
8. O harness não autoriza alteração relacional.

## Operação e auditoria

Os comandos atuais e o protocolo de diagnóstico estão documentados em
[`docs/testing/quiz-grading/README.md`](../../testing/quiz-grading/README.md) e
[`AGENT_PROTOCOL.md`](../../testing/quiz-grading/AGENT_PROTOCOL.md). O desenho,
os gates e a motivação da remoção da CLI estão no plano 11.
