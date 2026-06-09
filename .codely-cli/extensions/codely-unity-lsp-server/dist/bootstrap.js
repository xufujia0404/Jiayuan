import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { anyPrebuiltWorkerDllExists, workerArtifactsPresent, workerCsprojPath, workerDllPath, } from './workerPaths.js';
/** Project root is fixed to codely-unity-lsp-server itself. */
function projectRoot() {
    // Prefer process cwd because extension launcher sets cwd=${extensionPath}.
    const cwd = process.cwd();
    if (cwd && existsSync(path.join(cwd, 'gemini-extension.json')) && workerArtifactsPresent(cwd)) {
        return cwd;
    }
    // dist/bootstrap.js -> one level up is extension root (not two; two would be ~/.codely-cli/extensions).
    const currentFile = fileURLToPath(import.meta.url);
    return path.resolve(path.dirname(currentFile), '..');
}
function workerDllExists(root) {
    return anyPrebuiltWorkerDllExists(root);
}
function run(command, args, cwd, timeoutMs = 10_000) {
    return new Promise(resolve => {
        const child = spawn(command, args, { cwd, stdio: ['ignore', 'pipe', 'pipe'] });
        let output = '';
        let didTimeout = false;
        const timeout = setTimeout(() => {
            didTimeout = true;
            child.kill('SIGKILL');
        }, timeoutMs);
        child.stdout.on('data', chunk => {
            output += chunk.toString();
        });
        child.stderr.on('data', chunk => {
            output += chunk.toString();
        });
        child.on('error', error => {
            clearTimeout(timeout);
            resolve({ code: 1, output: String(error) });
        });
        child.on('close', code => {
            clearTimeout(timeout);
            if (didTimeout) {
                resolve({
                    code: 1,
                    output: `Command timed out after ${timeoutMs}ms: ${command} ${args.join(' ')}`,
                });
                return;
            }
            resolve({ code: code ?? 1, output });
        });
    });
}
export async function bootstrapLspRuntime() {
    const root = projectRoot();
    const dotnetVersion = await run('dotnet', ['--version'], root);
    if (dotnetVersion.code !== 0) {
        return {
            ok: false,
            message: [
                '[codely-unity-lsp-server] Dotnet runtime is required but not found.',
                '[codely-unity-lsp-server] Please download and install .NET 8 runtime for your platform:',
                'https://dotnet.microsoft.com/download/dotnet/8.0',
                '[codely-unity-lsp-server] Codely 尝试自动安装缺失的runtime和sdk（或提供一键安装引导）。',
                '[codely-unity-lsp-server] 安装完成后请重启 Codely 再试。',
                `Debug details:\n${dotnetVersion.output}`.trim(),
            ].join('\n'),
        };
    }
    if (workerDllExists(root)) {
        return { ok: true, message: 'runtime ready' };
    }
    const project = workerCsprojPath(root);
    if (!existsSync(project)) {
        return {
            ok: false,
            message: [
                '[codely-unity-lsp-server] UnityRoslynWorker.dll not found and no .csproj to build from.',
                `Expected Release DLL at: ${workerDllPath(root, 'shallowRelease')}`,
                'Reinstall the extension zip built with npm run package (includes worker), or use a full dev layout with sources.',
            ].join('\n'),
        };
    }
    const buildResult = await run('dotnet', ['build', project, '-c', 'Release'], root);
    if (buildResult.code !== 0) {
        return {
            ok: false,
            message: [
                '[codely-unity-lsp-server] Failed to build UnityRoslynWorker automatically.',
                'You can retry manually with:',
                `dotnet build "${project}" -c Release`,
                `Build output:\n${buildResult.output}`.trim(),
            ].join('\n'),
        };
    }
    return { ok: true, message: 'runtime ready (worker built)' };
}
