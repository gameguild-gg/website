declare module '*.wasm' {
  // webpack asset/resource loader returns a URL string
  const wasmUrl: string;
  export default wasmUrl;
}

declare module '*.tar' {
  // webpack asset/resource loader returns a URL string
  const tarUrl: string;
  export default tarUrl;
}