import { spawn } from 'node:child_process';
import { mkdir } from 'node:fs/promises';
import path from 'node:path';
import { resolveEditorCliBinary } from './unityEnvironment.js';
const DEFAULT_TIMEOUT_MS = 15 * 60 * 1000;
export async function regenerateProjectFilesWithUnity(projectRoot, editorInstall, options) {
    const cli = resolveEditorCliBinary(editorInstall);
    if (!cli) {
        return {
            ok: false,
            command: '',
            logPath: '',
            exitCode: null,
            signal: null,
            error: 'Could not resolve Unity editor CLI binary for batchmode.',
        };
    }
    const logDir = options?.logDir ?? path.join(projectRoot, '.codely');
    await mkdir(logDir, { recursive: true });
    const logPath = path.join(logDir, 'unity-lsp-batchmode.log');
    const args = [
        '-batchmode',
        '-quit',
        '-nographics',
        '-projectPath',
        projectRoot,
        '-logFile',
        logPath,
        '-executeMethod',
        'UnityEditor.SyncVS.SyncSolution',
    ];
    const timeoutMs = options?.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    const command = `${cli} ${args.map(arg => (/\s/.test(arg) ? JSON.stringify(arg) : arg)).join(' ')}`;
    return new Promise(resolve => {
        const child = spawn(cli, args, {
            stdio: ['ignore', 'ignore', 'pipe'],
            windowsHide: false,
        });
        const timer = setTimeout(() => {
            child.kill('SIGTERM');
        }, timeoutMs);
        child.stderr?.on('data', chunk => {
            process.stderr.write(chunk);
        });
        child.on('error', error => {
            clearTimeout(timer);
            resolve({
                ok: false,
                command,
                logPath,
                exitCode: null,
                signal: null,
                error: error.message,
            });
        });
        child.on('close', (code, signal) => {
            clearTimeout(timer);
            resolve({
                ok: code === 0,
                command,
                logPath,
                exitCode: code,
                signal,
                error: code === 0 ? undefined : `Unity exited with code ${code ?? 'null'} (see ${logPath})`,
            });
        });
    });
}
