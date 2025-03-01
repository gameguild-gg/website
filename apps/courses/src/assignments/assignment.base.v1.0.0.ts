export interface AssignmentBasev1_0_0 {
  format_ver: "1.0.0",
  rules: string[], // informa regras a serem utilzados
  title: string, // nome do curso/prova
  description: string, // ambiente da esquerda, trata de um texto formatado em markdown
  initialCode: string, // ambiente da direita
  language: string, // informa a linguagem utilizada
  inputs: string[], //entrada de dados para a função
  outputs: string[] //saida esperada
}