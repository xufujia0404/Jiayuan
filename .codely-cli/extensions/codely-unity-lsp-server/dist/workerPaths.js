import { existsSync } from 'node:fs';
import path from 'node:path';
/** Precompiled worker DLL locations (prefer shallow paths from csproj OutputPath). */
export const WORKER_DLL_REL = {
    shallowRelease: ['worker', 'runtime', 'release', 'UnityRoslynWorker.dll'],
    shallowDebug: ['worker', 'runtime', 'debug', 'UnityRoslynWorker.dll'],
    legacyRelease: [
        'worker',
        'UnityRoslynWorker',
        'bin',
        'Release',
        'net8.0',
        'UnityRoslynWorker.dll',
    ],
    legacyDebug: ['worker', 'UnityRoslynWorker', 'bin', 'Debug', 'net8.0', 'UnityRoslynWorker.dll'],
};
export function workerDllPath(extensionRoot, key) {
    return path.join(extensionRoot, ...WORKER_DLL_REL[key]);
}
export function anyPrebuiltWorkerDllExists(extensionRoot) {
    return (existsSync(workerDllPath(extensionRoot, 'shallowRelease')) ||
        existsSync(workerDllPath(extensionRoot, 'shallowDebug')) ||
        existsSync(workerDllPath(extensionRoot, 'legacyRelease')) ||
        existsSync(workerDllPath(extensionRoot, 'legacyDebug')));
}
export function workerCsprojPath(extensionRoot) {
    return path.join(extensionRoot, 'worker', 'UnityRoslynWorker', 'UnityRoslynWorker.csproj');
}
export function workerArtifactsPresent(extensionRoot) {
    return existsSync(workerCsprojPath(extensionRoot)) || anyPrebuiltWorkerDllExists(extensionRoot);
}
