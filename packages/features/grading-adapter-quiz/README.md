# @game-guild/grading-adapter-quiz

Adapter explícito entre os contratos genéricos de `@game-guild/grading` e o
domínio de questões de `@game-guild/quiz`.

O package recebe somente `QuizGradingItemInputV1[]`. Ele não conhece blocos,
documentos persistidos, UI, workflow acadêmico ou infraestrutura de banco.

`quizAssessmentTypeAdapter` é a única fronteira executável pública: sua chave e
versão unem projeção autoral, entrega learner-safe, decode de respostas e
avaliação determinística. Esses passos podem continuar separados em arquivos
internos, mas o core de grading nunca os registra ou resolve isoladamente.
