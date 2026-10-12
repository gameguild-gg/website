import type { CanvasArtifact } from './canvas.js';
import { createCanvasWasiImports } from './canvas-wasi.js';

export interface CanvasStartOptions {
    canvas: HTMLCanvasElement;
    onStdout?: (text: string) => void;
    onStderr?: (text: string) => void;
}

export interface CanvasSession {
    readonly runtimeProfile: string;
    stop(): void;
}

export interface CanvasRuntimeDependencies {
    importModule(url: string): Promise<unknown>;
    createModuleUrl(glue: Uint8Array): string;
    revokeModuleUrl(url: string): void;
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
}

function callable(record: Record<string, unknown>, name: string): CallableFunction | undefined {
    const value = record[name];
    return typeof value === 'function' ? value : undefined;
}

function ownedBuffer(bytes: Uint8Array): ArrayBuffer {
    const copy = new Uint8Array(bytes.byteLength);
    copy.set(bytes);
    return copy.buffer;
}

function defaultDependencies(): CanvasRuntimeDependencies {
    return {
        importModule: async (url) => import(/* webpackIgnore: true */ /* @vite-ignore */ url),
        createModuleUrl: (glue) => URL.createObjectURL(new Blob([ownedBuffer(glue)], { type: 'application/javascript' })),
        revokeModuleUrl: (url) => URL.revokeObjectURL(url),
    };
}

function startEntrypoint(
    profile: string,
    module: Record<string, unknown>,
    rawExports: WebAssembly.Exports | undefined,
): () => void {
    let frame = 0;
    if (profile === 'sdl3-runtime' && rawExports) {
        const init = callable(rawExports, 'SDL_AppInit');
        const iterate = callable(rawExports, 'SDL_AppIterate');
        if (init && iterate) {
            Reflect.apply(init, rawExports, [0, 0, 0]);
            const step = (): void => {
                const result = Reflect.apply(iterate, rawExports, [0]);
                if (Number(result) === 0) frame = requestAnimationFrame(step);
            };
            frame = requestAnimationFrame(step);
            return () => cancelAnimationFrame(frame);
        }
    }

    try {
        const callMain = callable(module, 'callMain');
        if (callMain) Reflect.apply(callMain, module, [[]]);
        else {
            const main = callable(module, '_main') ?? callable(module, 'main');
            if (main) Reflect.apply(main, module, [0, 0]);
        }
    } catch (error) {
        // Emscripten transfers a simulateInfiniteLoop main to its browser loop
        // with this exact sentinel. WASM traps and other errors still fail startup.
        if (error !== 'unwind') throw error;
    }
    return () => undefined;
}

function canvasInstantiation(
    artifact: CanvasArtifact,
    onOutput: (text: string) => void,
    capture: { exports?: WebAssembly.Exports; memory: WebAssembly.Memory | null },
    fail: (error: unknown) => void,
): (imports: WebAssembly.Imports, receive: (instance: WebAssembly.Instance) => void) => WebAssembly.Exports {
    return (imports, receive) => {
        const environment = { ...imports.env };
        // Direct wasm-ld applications retain C GL names; generated Emscripten
        // factories expose the same bindings with an emscripten_ prefix.
        for (const [name, value] of Object.entries(environment)) {
            if (name.startsWith('emscripten_gl') && typeof value === 'function') {
                const cName = name.slice('emscripten_'.length);
                if (environment[cName] === undefined) environment[cName] = value;
            }
        }
        const resolvedImports = {
            ...imports,
            env: environment,
            ...(artifact.runtimeProfile === 'sdl3-runtime' ? {
                wasi_snapshot_preview1: createCanvasWasiImports(() => capture.memory, onOutput),
            } : {}),
        };
        WebAssembly.compile(ownedBuffer(artifact.wasm)).then((module) => WebAssembly.instantiate(module, resolvedImports)).then((instance) => {
            capture.exports = instance.exports;
            const memory = instance.exports.memory;
            if (memory instanceof WebAssembly.Memory) capture.memory = memory;
            const exports = artifact.runtimeProfile === 'sdl3-runtime' ? new Proxy(instance.exports, {
                get(target, property, receiver) {
                    if ((property === '__wasm_call_ctors' || property === 'main' || property === '_main') && !(property in target)) {
                        return () => 0;
                    }
                    return Reflect.get(target, property, receiver);
                },
            }) : instance.exports;
            const patchedInstance = new Proxy(instance, {
                get(target, property, receiver) {
                    return property === 'exports' ? exports : Reflect.get(target, property, receiver);
                },
            });
            receive(patchedInstance);
        }).catch(fail);
        return {};
    };
}

export async function startCanvasArtifact(
    artifact: CanvasArtifact,
    options: CanvasStartOptions,
    dependencies: CanvasRuntimeDependencies = defaultDependencies(),
): Promise<CanvasSession> {
    const moduleUrl = dependencies.createModuleUrl(artifact.runtimeGlue);
    const capture: { exports?: WebAssembly.Exports; memory: WebAssembly.Memory | null } = { memory: null };
    let stopped = false;
    try {
        const namespace = await dependencies.importModule(moduleUrl);
        if (!isRecord(namespace)) throw new Error(`Canvas runtime '${artifact.runtimeProfile}' did not export a module namespace`);
        const factory = namespace.default;
        if (typeof factory !== 'function') throw new Error(`Canvas runtime '${artifact.runtimeProfile}' has no default factory`);
        const print = options.onStdout ?? (() => undefined);
        const printError = options.onStderr ?? (() => undefined);
        const config: Record<string, unknown> = {
            canvas: options.canvas,
            keyboardListeningElement: options.canvas,
            wasmBinary: artifact.wasm,
            locateFile: (filename: string) => filename,
            noInitialRun: true,
            print,
            printErr: printError,
        };
        let failInstantiation: (error: unknown) => void = () => undefined;
        const instantiationFailed = new Promise<never>((_, reject) => { failInstantiation = reject; });
        config.instantiateWasm = canvasInstantiation(artifact, printError, capture, failInstantiation);
        const value: unknown = await Promise.race([
            Promise.resolve().then(() => Reflect.apply(factory, namespace, [config])),
            instantiationFailed,
        ]);
        if (!isRecord(value)) throw new Error(`Canvas runtime '${artifact.runtimeProfile}' returned an invalid module`);
        const stopEntrypoint = startEntrypoint(artifact.runtimeProfile, value, capture.exports);
        return {
            runtimeProfile: artifact.runtimeProfile,
            stop() {
                if (stopped) return;
                stopped = true;
                stopEntrypoint();
                const pause = callable(value, 'pauseMainLoop');
                if (pause) Reflect.apply(pause, value, []);
                dependencies.revokeModuleUrl(moduleUrl);
            },
        };
    } catch (error) {
        dependencies.revokeModuleUrl(moduleUrl);
        throw error;
    }
}
