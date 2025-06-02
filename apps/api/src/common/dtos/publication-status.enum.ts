export enum PublicationStatusEnum {
  DRAFT = 'DRAFT', // Rascunho: não está visível, mesmo para o autor em ambiente público
  PUBLISHED = 'PUBLISHED', // Publicado: disponível para visualização conforme a visibilidade definida
  FUTURE = 'FUTURE', // Agendado para publicação em data futura
  PENDING = 'PENDING', // Pendente de aprovação ou moderação
  TRASH = 'TRASH', // Marcado para exclusão
}
