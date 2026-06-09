import { workerClient } from '../bridge/workerClient.js';
export class UnityRoslynAdapter {
    async ensureSolution(targetPath) {
        await workerClient.request('loadSolution', { targetPath });
    }
    async navigate(kind, params) {
        const method = kind === 'definition'
            ? 'goToDefinition'
            : kind === 'references'
                ? 'findReferences'
                : 'findImplementations';
        return workerClient.request(method, params);
    }
    async diagnostics(params) {
        return workerClient.request('getDiagnostics', params);
    }
    async hover(params) {
        return workerClient.request('getSymbolInfo', params);
    }
    async prepareCallHierarchy(params) {
        return workerClient.request('prepareCallHierarchy', params);
    }
    async incomingCalls(params) {
        return workerClient.request('incomingCalls', params);
    }
    async outgoingCalls(params) {
        return workerClient.request('outgoingCalls', params);
    }
}
export const unityRoslynAdapter = new UnityRoslynAdapter();
