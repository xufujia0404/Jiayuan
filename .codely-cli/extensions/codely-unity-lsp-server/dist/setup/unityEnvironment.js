import { existsSync, readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';
const isWin = process.platform === 'win32';
const isMac = process.platform === 'darwin';
const isLinux = process.platform === 'linux';
export function isUnityProjectRoot(projectRoot) {
    if (!projectRoot || !existsSync(projectRoot)) {
        return false;
    }
    const assets = path.join(projectRoot, 'Assets');
    const settings = path.join(projectRoot, 'ProjectSettings');
    if (existsSync(assets) && existsSync(settings)) {
        return true;
    }
    return (existsSync(path.join(projectRoot, 'Assembly-CSharp.csproj')) ||
        existsSync(path.join(projectRoot, 'Assembly-CSharp-Editor.csproj')));
}
export function readProjectVersion(projectRoot) {
    const filePath = path.join(projectRoot, 'ProjectSettings', 'ProjectVersion.txt');
    try {
        const content = readFileSync(filePath, 'utf8');
        let editor = '';
        let tuanjie;
        for (const line of content.split(/\r?\n/)) {
            const trimmed = line.trim();
            if (trimmed.startsWith('m_EditorVersion:')) {
                editor = trimmed.replace(/^m_EditorVersion:\s*/, '').trim();
            }
            else if (trimmed.startsWith('m_TuanjieEditorVersion:')) {
                tuanjie = trimmed.replace(/^m_TuanjieEditorVersion:\s*/, '').trim();
            }
        }
        return editor ? { editor, tuanjie } : null;
    }
    catch {
        return null;
    }
}
export function hubUserdataPath(product) {
    if (isWin) {
        const appdata = process.env.APPDATA;
        if (!appdata) {
            return null;
        }
        return path.join(appdata, product === 'tuanjie' ? 'TuanjieHub' : 'UnityHub');
    }
    if (isMac) {
        const home = process.env.HOME;
        if (!home) {
            return null;
        }
        return path.join(home, 'Library', 'Application Support', product === 'tuanjie' ? 'TuanjieHub' : 'UnityHub');
    }
    if (isLinux) {
        const configDir = process.env.XDG_CONFIG_HOME
            ? process.env.XDG_CONFIG_HOME
            : process.env.HOME
                ? path.join(process.env.HOME, '.config')
                : null;
        if (!configDir) {
            return null;
        }
        return path.join(configDir, product === 'tuanjie' ? 'TuanjieHub' : 'UnityHub');
    }
    return null;
}
export function defaultEditorInstallPath(product) {
    if (isWin) {
        const programFiles = process.env.ProgramFiles;
        if (!programFiles) {
            return null;
        }
        return path.join(programFiles, product === 'tuanjie' ? path.join('Tuanjie', 'Hub', 'Editor') : path.join('Unity', 'Hub', 'Editor'));
    }
    if (isMac) {
        return path.join('/Applications', product === 'tuanjie' ? 'Tuanjie/Hub/Editor' : 'Unity/Hub/Editor');
    }
    if (isLinux) {
        const home = process.env.HOME;
        if (!home) {
            return null;
        }
        return path.join(home, product === 'tuanjie' ? 'Tuanjie/Hub/Editor' : 'Unity/Hub/Editor');
    }
    return null;
}
function readEditorsV2(userdata) {
    const out = [];
    for (const name of ['editors-v2.json', 'editors.json']) {
        const filePath = path.join(userdata, name);
        try {
            const content = readFileSync(filePath, 'utf8');
            const data = JSON.parse(content);
            if (!Array.isArray(data.data)) {
                continue;
            }
            for (const entry of data.data) {
                const version = entry.version ?? 'unknown';
                const locations = Array.isArray(entry.location)
                    ? entry.location
                    : entry.location
                        ? [entry.location]
                        : [];
                for (const locPath of locations) {
                    const trimmed = locPath.trim();
                    if (trimmed && existsSync(trimmed)) {
                        out.push({ version, path: trimmed });
                    }
                }
            }
        }
        catch {
            // ignore malformed or missing Hub metadata
        }
    }
    return out;
}
function readSecondaryInstallPath(userdata) {
    try {
        const content = readFileSync(path.join(userdata, 'secondaryInstallPath.json'), 'utf8');
        const value = JSON.parse(content).trim().replace(/^"|"$/g, '');
        return value && existsSync(value) && statSync(value).isDirectory() ? value : null;
    }
    catch {
        return null;
    }
}
function scanEditorVersions(editorRoot) {
    const out = [];
    try {
        const entries = readdirSync(editorRoot, { withFileTypes: true });
        for (const entry of entries) {
            if (entry.isDirectory() && entry.name && entry.name !== '.' && entry.name !== '..') {
                out.push({
                    version: entry.name,
                    path: path.join(editorRoot, entry.name),
                });
            }
        }
    }
    catch {
        // ignore missing install roots
    }
    return out;
}
function resolveEditorExecutable(dirPath, product) {
    try {
        const stat = statSync(dirPath);
        if (stat.isFile()) {
            return dirPath;
        }
    }
    catch {
        return dirPath;
    }
    if (isWin) {
        const exe = product === 'unity' ? 'Unity.exe' : 'Tuanjie.exe';
        const candidate1 = path.join(dirPath, 'Editor', exe);
        const candidate2 = path.join(dirPath, exe);
        if (existsSync(candidate1))
            return candidate1;
        if (existsSync(candidate2))
            return candidate2;
    }
    if (isMac) {
        const app = product === 'unity' ? 'Unity.app' : 'Tuanjie.app';
        const candidate = path.join(dirPath, app);
        if (existsSync(candidate))
            return candidate;
    }
    if (isLinux) {
        const editorSub = path.join(dirPath, 'Editor');
        const names = product === 'unity' ? ['Unity', 'Unity.x86_64'] : ['Tuanjie', 'Tuanjie.x86_64'];
        for (const name of names) {
            const candidate = path.join(editorSub, name);
            if (existsSync(candidate))
                return candidate;
        }
        for (const name of names) {
            const candidate = path.join(dirPath, name);
            if (existsSync(candidate))
                return candidate;
        }
    }
    return dirPath;
}
export function collectEditors(product) {
    const userdata = hubUserdataPath(product);
    const seen = new Set();
    const result = [];
    const push = (version, dirPath) => {
        const key = `${version}\0${dirPath}`;
        if (seen.has(key))
            return;
        seen.add(key);
        result.push({
            path: resolveEditorExecutable(dirPath, product).replace(/\\/g, '/'),
            version,
            product,
        });
    };
    const defaultPath = defaultEditorInstallPath(product);
    if (defaultPath && existsSync(defaultPath)) {
        for (const { version, path: dirPath } of scanEditorVersions(defaultPath)) {
            push(version, dirPath);
        }
    }
    if (userdata && existsSync(userdata)) {
        const secondary = readSecondaryInstallPath(userdata);
        if (secondary) {
            for (const { version, path: dirPath } of scanEditorVersions(secondary)) {
                push(version, dirPath);
            }
        }
        for (const { version, path: dirPath } of readEditorsV2(userdata)) {
            push(version, dirPath);
        }
    }
    result.sort((a, b) => a.path.localeCompare(b.path));
    return result;
}
export function findEditorForProject(projectRoot) {
    const version = readProjectVersion(projectRoot);
    if (!version?.editor) {
        return null;
    }
    const tryMatch = (wanted, product) => {
        for (const editor of collectEditors(product)) {
            if (editor.version === wanted) {
                return editor;
            }
        }
        return null;
    };
    if (version.tuanjie) {
        const matchedTuanjie = tryMatch(version.tuanjie, 'tuanjie');
        if (matchedTuanjie) {
            return matchedTuanjie;
        }
    }
    return tryMatch(version.editor, 'unity') ?? tryMatch(version.editor, 'tuanjie');
}
export function resolveEditorCliBinary(editorInstall) {
    const editorPath = editorInstall.path;
    if (isWin) {
        return existsSync(editorPath) ? editorPath : null;
    }
    if (isMac) {
        if (editorPath.endsWith('.app')) {
            const inner = path.join(editorPath, 'Contents', 'MacOS', editorInstall.product === 'tuanjie' ? 'Tuanjie' : 'Unity');
            return existsSync(inner) ? inner : null;
        }
        return existsSync(editorPath) ? editorPath : null;
    }
    if (isLinux) {
        return existsSync(editorPath) ? editorPath : null;
    }
    return null;
}
export function assessUnityGeneratedProjectFiles(projectRoot) {
    const reasons = [];
    let slnPaths = [];
    let csprojPaths = [];
    try {
        const entries = readdirSync(projectRoot);
        slnPaths = entries.filter(entry => entry.endsWith('.sln')).map(entry => path.join(projectRoot, entry));
        csprojPaths = entries.filter(entry => entry.endsWith('.csproj')).map(entry => path.join(projectRoot, entry));
    }
    catch {
        reasons.push('Cannot read project root directory');
        return { ok: false, slnPaths, csprojPaths, reasons };
    }
    if (slnPaths.length === 0) {
        reasons.push('No .sln file in project root');
    }
    if (csprojPaths.length === 0) {
        reasons.push('No .csproj file in project root');
    }
    return {
        ok: reasons.length === 0,
        slnPaths,
        csprojPaths,
        reasons,
    };
}
