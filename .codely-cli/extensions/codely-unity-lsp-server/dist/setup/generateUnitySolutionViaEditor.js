import { spawn } from 'node:child_process';
import { existsSync } from 'node:fs';
import { copyFile, mkdir, readFile, unlink } from 'node:fs/promises';
import { fileURLToPath } from 'node:url';
import path from 'node:path';
import { regenerateProjectFilesWithUnity } from './regenerateUnityProjectFiles.js';
import { generateSolutionViaConnectedEditor } from './unityTcpProjectGeneration.js';
import { assessUnityGeneratedProjectFiles, findEditorForProject, isUnityProjectRoot, resolveEditorCliBinary, } from './unityEnvironment.js';
export const BUNDLED_GEN_SLN_CLASS = 'CodelyLspTmp__GenSln_9f3a2b81';
const BUNDLED_GEN_SLN_FILE = `${BUNDLED_GEN_SLN_CLASS}.cs`;
const SCRIPT_MARKER = 'CodelyLspTmp__GenSln_9f3a2b81';
const DEFAULT_TIMEOUT_MS = 15 * 60 * 1000;
function resolveBundledScriptSourcePath() {
    return path.join(fileURLToPath(new URL('.', import.meta.url)), '..', '..', 'bundled', BUNDLED_GEN_SLN_FILE);
}
async function safeUnlink(filePath) {
    try {
        if (existsSync(filePath)) {
            await unlink(filePath);
        }
    }
    catch {
        // best-effort cleanup
    }
}
function isOurBundledScript(content) {
    return content.includes(SCRIPT_MARKER) && content.includes('Bundled by codely-unity-lsp');
}
async function prepareDestAndCopy(projectRoot, bundledSource) {
    const assetsDir = path.join(projectRoot, 'Assets');
    if (!existsSync(assetsDir)) {
        return { ok: false, error: 'Assets folder missing; not a valid Unity project layout.' };
    }
    const editorDir = path.join(projectRoot, 'Assets', 'Editor');
    await mkdir(editorDir, { recursive: true });
    const destPath = path.join(editorDir, BUNDLED_GEN_SLN_FILE);
    if (existsSync(destPath)) {
        try {
            const existing = await readFile(destPath, 'utf8');
            if (!isOurBundledScript(existing)) {
                return {
                    ok: false,
                    error: `Refusing to overwrite existing file (not codely temp script): ${destPath}`,
                };
            }
        }
        catch (error) {
            const message = error instanceof Error ? error.message : String(error);
            return { ok: false, error: `Cannot read existing ${destPath}: ${message}` };
        }
    }
    try {
        await copyFile(bundledSource, destPath);
    }
    catch (error) {
        const message = error instanceof Error ? error.message : String(error);
        return { ok: false, error: `Copy bundled script failed: ${message}` };
    }
    return { ok: true, destPath };
}
export async function runBundledGenerateUnitySolution(projectRoot, editorInstall, options) {
    const cli = resolveEditorCliBinary(editorInstall);
    if (!cli) {
        return {
            ok: false,
            command: '',
            logPath: '',
            exitCode: null,
            signal: null,
            error: 'Could not resolve Unity/Tuanjie editor CLI binary for batchmode.',
        };
    }
    const bundledSource = resolveBundledScriptSourcePath();
    if (!existsSync(bundledSource)) {
        return {
            ok: false,
            command: '',
            logPath: '',
            exitCode: null,
            signal: null,
            error: `Bundled script not found in extension package: ${bundledSource}`,
        };
    }
    const logDir = options?.logDir ?? path.join(projectRoot, '.codely');
    await mkdir(logDir, { recursive: true });
    const logPath = path.join(logDir, 'unity-lsp-batchmode-gensln.log');
    const prepared = await prepareDestAndCopy(projectRoot, bundledSource);
    if (!prepared.ok) {
        return {
            ok: false,
            command: '',
            logPath,
            exitCode: null,
            signal: null,
            error: prepared.error,
        };
    }
    const args = [
        '-batchmode',
        '-quit',
        '-nographics',
        '-projectPath',
        projectRoot,
        '-logFile',
        logPath,
        '-executeMethod',
        `${BUNDLED_GEN_SLN_CLASS}.Run`,
    ];
    const command = `${cli} ${args.map(arg => (/\s/.test(arg) ? JSON.stringify(arg) : arg)).join(' ')}`;
    const timeoutMs = options?.timeoutMs ?? DEFAULT_TIMEOUT_MS;
    try {
        const result = await new Promise(resolve => {
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
        return { ...result, destPath: prepared.destPath };
    }
    finally {
        await safeUnlink(prepared.destPath);
    }
}
export async function maybeRegenerateSlnOnStartup(targetPath) {
    if (process.env.CODELY_UNITY_LSP_SKIP_INIT_SLN === '1') {
        return;
    }
    const workspaceRoot = targetPath?.trim() ||
        process.env.UNITY_PROJECT_PATH?.trim() ||
        process.env.CODELY_WORKSPACE_PATH?.trim() ||
        '';
    if (!workspaceRoot || !existsSync(workspaceRoot) || !isUnityProjectRoot(workspaceRoot)) {
        return;
    }
    let assessment = assessUnityGeneratedProjectFiles(workspaceRoot);
    if (assessment.ok) {
        return;
    }
    const connectedEditorResult = await generateSolutionViaConnectedEditor(workspaceRoot);
    if (connectedEditorResult.ok) {
        process.stderr.write(`codely-unity-lsp-server: generated Unity project files via connected editor at ${connectedEditorResult.host}:${connectedEditorResult.port}\n`);
        assessment = assessUnityGeneratedProjectFiles(workspaceRoot);
        if (assessment.ok) {
            return;
        }
        process.stderr.write('codely-unity-lsp-server: connected editor command completed but .sln/.csproj are still missing; falling back to batchmode.\n');
    }
    else {
        process.stderr.write(`codely-unity-lsp-server: connected editor project generation unavailable: ${connectedEditorResult.error ?? 'unknown'}; falling back to batchmode.\n`);
    }
    const editor = findEditorForProject(workspaceRoot);
    if (!editor) {
        process.stderr.write(`codely-unity-lsp-server: no matching Unity editor found for ${workspaceRoot}; cannot auto-generate .sln/.csproj.\n`);
        return;
    }
    const logDir = path.join(workspaceRoot, '.codely');
    const bundled = await runBundledGenerateUnitySolution(workspaceRoot, editor, { logDir });
    if (!bundled.ok) {
        process.stderr.write(`codely-unity-lsp-server: bundled .sln generation did not succeed: ${bundled.error ?? 'unknown'} (log: ${bundled.logPath})\n`);
    }
    assessment = assessUnityGeneratedProjectFiles(workspaceRoot);
    if (assessment.ok) {
        return;
    }
    const regenerated = await regenerateProjectFilesWithUnity(workspaceRoot, editor, { logDir });
    if (!regenerated.ok) {
        process.stderr.write(`codely-unity-lsp-server: Unity SyncSolution regeneration did not succeed: ${regenerated.error ?? 'unknown'} (log: ${regenerated.logPath})\n`);
    }
}
