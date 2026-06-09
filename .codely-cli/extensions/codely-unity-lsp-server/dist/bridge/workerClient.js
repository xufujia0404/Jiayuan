import { randomUUID } from 'node:crypto';
import path from 'node:path';
import { spawn, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { fileURLToPath } from 'node:url';
import { workerArtifactsPresent, workerCsprojPath, workerDllPath } from '../workerPaths.js';
function getProjectRoot() {
    // Prefer process cwd because extension launcher sets cwd=${extensionPath}.
    const cwd = process.cwd();
    if (cwd && existsSync(path.join(cwd, 'gemini-extension.json')) && workerArtifactsPresent(cwd)) {
        return cwd;
    }
    const currentFile = fileURLToPath(import.meta.url);
    return path.resolve(path.dirname(currentFile), '..', '..');
}
function buildWorkerEnvironment(baseEnv) {
    const env = { ...baseEnv };
    if (!env.DOTNET_SOLUTION_PATH && env.UNITY_PROJECT_PATH) {
        env.DOTNET_SOLUTION_PATH = env.UNITY_PROJECT_PATH;
    }
    return env;
}
function resolveUnityBundledDotnet() {
    const fromEnv = process.env.CODELY_UNITY_BUNDLED_DOTNET?.trim();
    if (fromEnv && existsSync(fromEnv)) {
        return fromEnv;
    }
    const editorPath = process.env.UNITY_EDITOR_PATH?.trim();
    if (editorPath) {
        const candidates = process.platform === 'win32'
            ? [path.join(editorPath, 'Data', 'NetCoreRuntime', 'dotnet.exe')]
            : [path.join(editorPath, 'Contents', 'NetCoreRuntime', 'dotnet')];
        for (const candidate of candidates) {
            if (existsSync(candidate)) {
                return candidate;
            }
        }
    }
    return null;
}
function canUseDotnetCommand(command) {
    const result = spawnSync(command, ['--info'], {
        encoding: 'utf8',
        timeout: 3000,
    });
    return result.status === 0;
}
function resolveWorkerLaunch(projectRoot) {
    const dllCandidates = [
        workerDllPath(projectRoot, 'shallowRelease'),
        workerDllPath(projectRoot, 'shallowDebug'),
        workerDllPath(projectRoot, 'legacyRelease'),
        workerDllPath(projectRoot, 'legacyDebug'),
    ];
    for (const dllPath of dllCandidates) {
        if (existsSync(dllPath)) {
            // Prefer Unity/Tuanjie bundled dotnet to match editor runtime; fallback to system dotnet.
            const unityDotnet = resolveUnityBundledDotnet();
            if (unityDotnet && canUseDotnetCommand(unityDotnet)) {
                return { command: unityDotnet, args: [dllPath], source: 'unity-bundled-dotnet' };
            }
            return { command: 'dotnet', args: [dllPath], source: 'system-dotnet' };
        }
    }
    return {
        command: 'dotnet',
        args: [
            'run',
            '--project',
            workerCsprojPath(projectRoot),
            '--no-launch-profile',
            '--no-restore',
        ],
        source: 'system-dotnet-run',
    };
}
export class WorkerClient {
    child;
    pending = new Map();
    buffer = '';
    ensureStarted() {
        if (this.child && !this.child.killed) {
            return this.child;
        }
        const projectRoot = getProjectRoot();
        const workerLaunch = resolveWorkerLaunch(projectRoot);
        const child = spawn(workerLaunch.command, workerLaunch.args, {
            cwd: projectRoot,
            env: buildWorkerEnvironment(process.env),
            stdio: ['pipe', 'pipe', 'pipe'],
        });
        child.stdout.setEncoding('utf8');
        child.stderr.setEncoding('utf8');
        process.stderr.write(`[codely-unity-lsp-server] worker launch via ${workerLaunch.source}: ${workerLaunch.command} ${workerLaunch.args.join(' ')}\n`);
        child.stdout.on('data', (chunk) => this.handleStdout(chunk));
        child.stderr.on('data', (chunk) => {
            process.stderr.write(chunk);
        });
        child.on('error', error => {
            const launchError = new Error(`UnityRoslynWorker failed to start via ${workerLaunch.command}: ${String(error)}`);
            for (const pending of this.pending.values()) {
                pending.reject(launchError);
            }
            this.pending.clear();
            this.child = undefined;
        });
        child.on('exit', (code, signal) => {
            const error = new Error(`UnityRoslynWorker exited unexpectedly (code=${code ?? 'null'}, signal=${signal ?? 'null'})`);
            for (const pending of this.pending.values()) {
                pending.reject(error);
            }
            this.pending.clear();
            this.child = undefined;
        });
        this.child = child;
        return child;
    }
    handleStdout(chunk) {
        this.buffer += chunk;
        while (true) {
            const newlineIndex = this.buffer.indexOf('\n');
            if (newlineIndex < 0) {
                return;
            }
            const line = this.buffer.slice(0, newlineIndex).trim();
            this.buffer = this.buffer.slice(newlineIndex + 1);
            if (!line) {
                continue;
            }
            if (!line.startsWith('{')) {
                process.stderr.write(`${line}\n`);
                continue;
            }
            let message;
            try {
                message = JSON.parse(line);
            }
            catch (error) {
                process.stderr.write(`Failed to parse UnityRoslynWorker output: ${String(error)}\n${line}\n`);
                continue;
            }
            const pending = this.pending.get(message.id);
            if (!pending) {
                continue;
            }
            this.pending.delete(message.id);
            if (message.ok) {
                pending.resolve(message.result);
            }
            else {
                pending.reject(new Error(message.error || 'Unknown worker error'));
            }
        }
    }
    async request(method, params) {
        const child = this.ensureStarted();
        const id = randomUUID();
        const request = { id, method, params };
        return new Promise((resolve, reject) => {
            this.pending.set(id, {
                resolve: value => resolve(value),
                reject,
            });
            child.stdin.write(`${JSON.stringify(request)}\n`, error => {
                if (!error) {
                    return;
                }
                this.pending.delete(id);
                reject(error);
            });
        });
    }
    async dispose() {
        if (!this.child || this.child.killed) {
            return;
        }
        this.child.kill();
        this.child = undefined;
    }
}
export const workerClient = new WorkerClient();
