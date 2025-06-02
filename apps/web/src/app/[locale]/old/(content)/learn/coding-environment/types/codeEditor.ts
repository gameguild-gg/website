export interface History {
  past: string[];
  future: string[];
}

export interface CodeFile {
  id: string;
  name: string;
  language: string;
  content: string;
  history: History;
}

