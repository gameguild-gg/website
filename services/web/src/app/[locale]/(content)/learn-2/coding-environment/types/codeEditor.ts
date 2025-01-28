export interface History {
  past: string[];
  future: string[];
}

export interface CodeFile {
  name: string;
  language: string;
  content: string;
  history: History;
}

