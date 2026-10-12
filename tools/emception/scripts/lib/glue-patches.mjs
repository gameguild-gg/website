import { access, readFile, writeFile } from 'node:fs/promises';
import path from 'node:path';

export const PATCH_SET_VERSION = 'emception-glue-v5';

const ENV_NEEDLE = 'var ENV={};';
const ENV_MARKER = 'moduleArg["ENV"]';
const SYSTEM_NEEDLE = 'if(!command)return 0;return-52';
const SYSTEM_ASYNC_ONLY = 'if(!command)return 0;if(Module["systemCallback"]){return Asyncify.handleAsync(function(){return Module["systemCallback"](UTF8ToString(command))})}return-52';
const SYSTEM_BARE = 'if(!command)return 0;if(Module["systemCallback"]){return Module["systemCallback"](UTF8ToString(command))}return-52';
const SYSTEM_REPLACEMENT = 'if(!command)return 0;if(Module["systemCallbackSync"]){var sr=Module["systemCallbackSync"](UTF8ToString(command));if(sr!==undefined)return sr}if(Module["systemCallback"]){return Asyncify.handleAsync(function(){return Module["systemCallback"](UTF8ToString(command))})}return-52';
const OPENAT_NEEDLE = 'path=SYSCALLS.getStr(path);path=SYSCALLS.calculateAt(dirfd,path);var mode=varargs?syscallGetVarargI():0;';
const CMAKE_PIPE_POLL_LEGACY = 'poll(stream,timeout,notifyCallback){var pipe=stream.node.pipe;if((stream.flags&2097155)===1){return 256|4}for(var bucket of pipe.buckets){if(bucket.offset-bucket.roffset>0){return 64|1}}return 0}';
const CMAKE_PIPE_POLL_PATCHED = 'poll(stream,timeout,notifyCallback){var pipe=stream.node.pipe;if((stream.flags&2097155)===1){if(pipe.refcnt<=1)return 4|8;return 256|4}if(pipe.refcnt<=1){for(var bucket of pipe.buckets){if(bucket.offset-bucket.roffset>0){return 64|1|16}}return 16}for(var bucket of pipe.buckets){if(bucket.offset-bucket.roffset>0){return 64|1}}return 0}';
const CMAKE_SYSCALL_POLL_LEGACY = 'if(stream.stream_ops.poll){flags=stream.stream_ops.poll(stream,-1)}else{flags=5}';
const CMAKE_SYSCALL_POLL_PATCHED = 'if(stream.stream_ops.poll){flags=stream.stream_ops.poll(stream,-1);if(flags===0&&timeout<0&&stream.node&&stream.node.pipe){flags=(stream.flags&2097155)===1?12:16}}else{flags=5}';
const CMAKE_PIPE_POLL_ASYNC = 'if(notifyCallback)pipe.registerReadableHandler(notifyCallback);return 0';
const CMAKE_SYSCALL_POLL_ASYNC = 'if(isAsyncContext&&timeout){flags=stream.stream_ops.poll(stream,timeout,makeNotifyCallback(stream,pollfd))}else flags=stream.stream_ops.poll(stream,-1)';
const CANVAS_RUNTIME_NAMES = ['sdl3', 'raylib', 'allegro'];

const CANVAS_COMMON_PATCHES = [
  {
    label: 'wasmBinary',
    needle: 'var wasmBinary;var ABORT=false',
    replacement: 'var wasmBinary=Module["wasmBinary"];var ABORT=false',
    marker: 'wasmBinary=Module["wasmBinary"]',
  },
  {
    label: 'instantiateAsync',
    needle: 'instantiateAsync(binary,binaryFile,imports){if(!binary){try{var response=fetch(',
    replacement: 'instantiateAsync(binary,binaryFile,imports){if(binary){return WebAssembly.instantiate(binary,imports)}if(!binary){try{var response=fetch(',
    marker: 'instantiateAsync(binary,binaryFile,imports){if(binary){return WebAssembly.instantiate',
  },
  {
    label: 'callUserCallback',
    needle: 'var callUserCallback=func=>{if(ABORT){return}try{return func()}catch(e){handleException(e)}finally{maybeExit()}}',
    replacement: 'var callUserCallback=func=>{if(ABORT){return}try{return func()}catch(e){if(e instanceof WebAssembly.RuntimeError){ABORT=1;try{Module.pauseMainLoop?.();}catch(_){}return;}handleException(e)}finally{maybeExit()}}',
    marker: 'if(e instanceof WebAssembly.RuntimeError){ABORT=1;try{Module.pauseMainLoop?.()',
  },
  {
    label: 'handleException',
    needle: 'var handleException=e=>{if(e instanceof ExitStatus||e=="unwind"){return EXITSTATUS}quit_(1,e)}',
    replacement: 'var handleException=e=>{if(e instanceof ExitStatus||e=="unwind"){return EXITSTATUS}if(e instanceof WebAssembly.RuntimeError){ABORT=1;try{Module.pauseMainLoop?.();}catch(_){}return EXITSTATUS}quit_(1,e)}',
    marker: 'return EXITSTATUS}if(e instanceof WebAssembly.RuntimeError)',
  },
  {
    label: 'C exit import',
    needle: 'var wasmImports={',
    replacement: 'var wasmImports={exit:_exit,',
    marker: 'exit:_exit,',
  },
];

function replaceRequired(content, patch, filename, applied) {
  if (content.includes(patch.marker)) return content;
  if (!content.includes(patch.needle)) {
    throw new Error(`unsupported ${filename} generated shape: missing ${patch.label}`);
  }
  applied.push(patch.label);
  return content.replace(patch.needle, patch.replacement);
}

export function applyCanvasRuntimePatches(source, filename) {
  const applied = [];
  let content = source;
  for (const patch of CANVAS_COMMON_PATCHES) {
    content = replaceRequired(content, patch, filename, applied);
  }

  const legacyEmAsmFallback = (name) =>
    `if(!ASM_CONSTS[${name}]){var _s=UTF8ToString(${name});ASM_CONSTS[${name}]=eval("(function($0,$1,$2,$3,$4,$5,$6,$7,$8,$9){"+_s+"})");}`;
  const applicationEmAsm = (name) =>
    `if(!ASM_CONSTS[${name}]||!ASM_CONSTS[${name}].emceptionApplicationAsm){var _s=UTF8ToString(${name});ASM_CONSTS[${name}]=eval("(function($0,$1,$2,$3,$4,$5,$6,$7,$8,$9){"+_s+"})");ASM_CONSTS[${name}].emceptionApplicationAsm=true;}`;
  const dispatchers = [
    { label: 'EM_ASM', declaration: 'var runEmAsmFunction=(code,sigPtr,argbuf)=>{var args=readEmAsmArgs(sigPtr,argbuf);', address: 'code' },
    ...(filename !== 'raylib-runtime.mjs' ? [{
      label: 'main-thread EM_ASM',
      declaration: 'var runMainThreadEmAsm=(emAsmAddr,sigPtr,argbuf,sync)=>{var args=readEmAsmArgs(sigPtr,argbuf);',
      address: 'emAsmAddr',
    }] : []),
  ];
  for (const { label, declaration, address } of dispatchers) {
    // The factory's baked-in addresses belong to its stub WASM, not the
    // separately linked application. Decode that application's memory even
    // when its address collides with an entry in the stub's ASM_CONSTS table.
    const tail = `return ASM_CONSTS[${address}](...args)}`;
    const original = declaration + tail;
    const legacy = declaration + legacyEmAsmFallback(address) + tail;
    content = replaceRequired(content, {
      label,
      needle: content.includes(legacy) ? legacy : original,
      replacement: declaration + applicationEmAsm(address) + tail,
      marker: `ASM_CONSTS[${address}].emceptionApplicationAsm=true`,
    }, filename, applied);
  }
  if (filename !== 'sdl3-runtime.mjs') return { content, applied };

  const sdlPatches = [
    // The separately linked application can import C abort even when the stub did not.
    // Match the pinned Emscripten 5.0.7 libcore implementation; retain its fatal runtime abort.
    { label: 'C abort import', needle: 'var wasmImports={', replacement: 'var wasmImports={_abort_js:()=>abort(""),', marker: '_abort_js:' },
    { label: 'free declaration', needle: 'var _main,_SDL_free,', replacement: 'var _free,_main,_SDL_free,', marker: 'var _free,_main,_SDL_free,' },
    { label: 'malloc fallback', needle: '_malloc=wasmExports["malloc"]', replacement: '_malloc=wasmExports["malloc"]||wasmExports["SDL_malloc"]', marker: '_malloc=wasmExports["malloc"]||' },
    { label: 'free fallback', needle: '_SDL_free=Module["_SDL_free"]=wasmExports["SDL_free"]', replacement: '_SDL_free=Module["_SDL_free"]=wasmExports["SDL_free"];_free=wasmExports["free"]||_SDL_free', marker: '_free=wasmExports["free"]||_SDL_free' },
    { label: 'string allocation', needle: 'var stringToNewUTF8=str=>{var size=lengthBytesUTF8(str)+1;var ret=_malloc(size)', replacement: 'var stringToNewUTF8=str=>{var size=lengthBytesUTF8(str)+1;var allocFn=_malloc||_SDL_malloc;var ret=allocFn(size)', marker: 'var allocFn=_malloc||_SDL_malloc' },
    { label: 'canvas keyboard scope', needle: 'var keyEventHandlerFunc=e=>{var keyEventData=JSEvents.keyEvent', replacement: 'var keyEventHandlerFunc=e=>{if(Module["canvas"]&&e.target!==Module["canvas"])return;var keyEventData=JSEvents.keyEvent', marker: 'e.target!==Module["canvas"]' },
  ];
  for (const patch of sdlPatches) {
    content = replaceRequired(content, patch, filename, applied);
  }
  return { content, applied };
}

function patchEnv(content, applied) {
  if (content.includes(ENV_MARKER) || !content.includes(ENV_NEEDLE)) return content;
  applied.push('env');
  return content.replace(
    ENV_NEEDLE,
    `${ENV_NEEDLE}if(moduleArg&&moduleArg["ENV"]){for(var _k in moduleArg["ENV"]){ENV[_k]=moduleArg["ENV"][_k]}}`,
  );
}

function patchSystem(content, filename, applied) {
  const hasSystemImplementation = /(?:function\s+__emscripten_system\b|(?:var|let|const)\s+__emscripten_system\b|__emscripten_system\s*=)/.test(content);
  if (!hasSystemImplementation || content.includes('Module["systemCallbackSync"]')) return content;
  const supportedNeedle = [SYSTEM_ASYNC_ONLY, SYSTEM_BARE, SYSTEM_NEEDLE].find((needle) => content.includes(needle));
  if (!supportedNeedle) {
    throw new Error(`${filename}: unsupported __emscripten_system shape`);
  }
  applied.push('system');
  return content.replace(supportedNeedle, SYSTEM_REPLACEMENT);
}

function patchCallMain(content, applied) {
  const oldTail = 'try{var ret=entryFunction(argc,argv);exitJS(ret,true);return ret}catch(e){return handleException(e)}';
  const newTail = 'try{var ret=entryFunction(argc,argv);if(typeof Asyncify!=="undefined"&&Asyncify.currData){return Asyncify.whenDone().then(function(r){try{exitJS(r,true);return r}catch(e){return handleException(e)}},function(e){return handleException(e)});}exitJS(ret,true);return ret}catch(e){return handleException(e)}';
  if (content.includes('args.unshift(thisProgram)') && content.includes(oldTail)) {
    applied.push('callMain');
    return content.replace(oldTail, newTail);
  }
  if (!content.includes('var argc=0;var argv=0;') || content.includes('args.unshift(thisProgram)')) return content;

  const functionStart = content.indexOf('function callMain()');
  const bodyStart = content.indexOf('{', functionStart);
  if (functionStart === -1 || bodyStart === -1) {
    throw new Error('unsupported callMain shape');
  }
  let depth = 0;
  let bodyEnd = -1;
  for (let index = bodyStart; index < content.length; index += 1) {
    if (content[index] === '{') depth += 1;
    if (content[index] === '}') depth -= 1;
    if (depth === 0) {
      bodyEnd = index;
      break;
    }
  }
  if (bodyEnd === -1) throw new Error('unterminated callMain body');

  const replacement = [
    'function callMain(args=[])', '{', 'var entryFunction=_main;', 'args.unshift(thisProgram);',
    'var argc=args.length;', 'var oldPages=(wasmMemory.buffer.byteLength/65536)|0;', 'wasmMemory.grow(1);',
    'updateMemoryViews();', 'var scratch=oldPages*65536;', 'var argv=scratch;',
    'var strBase=scratch+(argc+1)*4;', 'for(var i=0;i<argc;i++){',
    'HEAPU32[(argv>>2)+i]=strBase;', 'var len=lengthBytesUTF8(args[i])+1;',
    'stringToUTF8Array(args[i],HEAPU8,strBase,len);', 'strBase+=len;', '}',
    'HEAPU32[(argv>>2)+argc]=0;', 'try{var ret=entryFunction(argc,argv);',
    'if(typeof Asyncify!=="undefined"&&Asyncify.currData){',
    'return Asyncify.whenDone().then(function(r){try{exitJS(r,true);return r}catch(e){return handleException(e)}},function(e){return handleException(e)});',
    '}', 'exitJS(ret,true);', 'return ret}catch(e){return handleException(e)}', '}',
  ].join('');
  applied.push('callMain');
  return content.slice(0, functionStart) + replacement + content.slice(bodyEnd + 1);
}

function patchOpenat(content, applied) {
  if (content.includes('onPreOpen') || !content.includes(OPENAT_NEEDLE)) return content;
  const subprocess = 'if(path==="/tmp/__dispatch_subprocess__"&&Module["subprocessDispatch"]){return Asyncify.handleAsync(function(){return Module["subprocessDispatch"]().then(function(){return FS.open(path,flags,mode).fd})})}';
  const vfs = 'if((!Module["isCachedSync"]||!Module["isCachedSync"](path))&&Module["onPreOpen"]){return Asyncify.handleAsync(function(){return Module["onPreOpen"](path).then(function(){return FS.open(path,flags,mode).fd})})}';
  applied.push('openat');
  return content.replace(OPENAT_NEEDLE, OPENAT_NEEDLE + subprocess + vfs);
}

function patchCmakePoll(content, filename, applied) {
  if (filename !== 'cmake.mjs') return content;
  const hasAsyncPoll = content.includes(CMAKE_PIPE_POLL_ASYNC) && content.includes(CMAKE_SYSCALL_POLL_ASYNC);
  const hasLegacyPatch = content.includes(CMAKE_PIPE_POLL_PATCHED) && content.includes(CMAKE_SYSCALL_POLL_PATCHED);
  if (hasAsyncPoll || hasLegacyPatch) return content;
  if (!content.includes(CMAKE_PIPE_POLL_LEGACY) || !content.includes(CMAKE_SYSCALL_POLL_LEGACY)) {
    throw new Error(`${filename}: unsupported pipe poll shape`);
  }
  applied.push('pipefs-pollhup', 'syscall-poll-pipe-hup');
  return content
    .replace(CMAKE_PIPE_POLL_LEGACY, CMAKE_PIPE_POLL_PATCHED)
    .replace(CMAKE_SYSCALL_POLL_LEGACY, CMAKE_SYSCALL_POLL_PATCHED);
}

export function applyGluePatches(source, filename) {
  const applied = [];
  let content = patchEnv(source, applied);
  content = patchCallMain(content, applied);
  content = patchSystem(content, filename, applied);
  content = patchOpenat(content, applied);
  content = patchCmakePoll(content, filename, applied);
  return { content, applied };
}

async function exists(filePath) {
  try {
    await access(filePath);
    return true;
  } catch {
    return false;
  }
}

export async function patchGlueDirectory({ libDirectory, tools }) {
  let foundFiles = 0;
  let changedFiles = 0;
  let patchCount = 0;
  for (const tool of tools) {
    const gluePath = path.join(libDirectory, `${tool}.mjs`);
    const wasmPath = path.join(libDirectory, `${tool}.wasm`);
    const [hasGlue, hasWasm] = await Promise.all([exists(gluePath), exists(wasmPath)]);
    if (hasGlue !== hasWasm) {
      const present = hasWasm ? `${tool}.wasm` : `${tool}.mjs`;
      const missing = hasWasm ? `${tool}.mjs` : `${tool}.wasm`;
      throw new Error(`${present} exists without ${missing} in ${libDirectory}`);
    }
    if (!hasGlue) continue;
    foundFiles += 1;
    const source = await readFile(gluePath, 'utf8');
    const result = applyGluePatches(source, path.basename(gluePath));
    patchCount += result.applied.length;
    if (result.content !== source) {
      await writeFile(gluePath, result.content);
      changedFiles += 1;
    }
  }
  if (foundFiles === 0) throw new Error(`no generated tool glue found in ${libDirectory}`);
  return { foundFiles, changedFiles, patchCount };
}

export async function patchCanvasRuntimeDirectory({ runtimeDirectory, runtimes = CANVAS_RUNTIME_NAMES }) {
  let changedFiles = 0;
  let patchCount = 0;
  for (const runtime of runtimes) {
    const filename = `${runtime}-runtime.mjs`;
    const runtimePath = path.join(runtimeDirectory, filename);
    const wasmPath = path.join(runtimeDirectory, `${runtime}-runtime.wasm`);
    const [hasGlue, hasWasm] = await Promise.all([exists(runtimePath), exists(wasmPath)]);
    if (!hasGlue || !hasWasm) {
      throw new Error(`required canvas runtime pair is incomplete: ${runtimePath} + ${wasmPath}`);
    }
    const source = await readFile(runtimePath, 'utf8');
    const result = applyCanvasRuntimePatches(source, filename);
    patchCount += result.applied.length;
    if (result.content !== source) {
      await writeFile(runtimePath, result.content);
      changedFiles += 1;
    }
  }
  return { foundFiles: runtimes.length, changedFiles, patchCount };
}
