import { fileURLToPath, pathToFileURL } from 'node:url';
import { createMessageConnection, StreamMessageReader, StreamMessageWriter, } from 'vscode-jsonrpc/node.js';
import { CallHierarchyPrepareRequest, CallHierarchyIncomingCallsRequest, CallHierarchyOutgoingCallsRequest, DefinitionRequest, DidChangeTextDocumentNotification, DidCloseTextDocumentNotification, DidOpenTextDocumentNotification, DocumentSymbolRequest, HoverRequest, ImplementationRequest, InitializeRequest, InitializedNotification, PublishDiagnosticsNotification, ReferencesRequest, ShutdownRequest, WorkspaceSymbolRequest, } from 'vscode-languageserver-protocol';
import { workerClient } from './bridge/workerClient.js';
import { bootstrapLspRuntime } from './bootstrap.js';
import { unityRoslynAdapter } from './adapters/unityRoslynAdapter.js';
import { maybeRegenerateSlnOnStartup } from './setup/generateUnitySolutionViaEditor.js';
const docs = new Map();
let workspacePath;
let hasShutdown = false;
function uriToPath(uri) {
    if (!uri.startsWith('file://')) {
        return uri;
    }
    return fileURLToPath(uri);
}
function toWorkerPosition(params) {
    return {
        filePath: uriToPath(params.textDocument.uri),
        line: params.position.line + 1,
        character: params.position.character + 1,
        targetPath: workspacePath,
    };
}
function toRange(startLine, startCharacter, endLine, endCharacter) {
    return {
        start: { line: Math.max(0, startLine - 1), character: Math.max(0, startCharacter - 1) },
        end: { line: Math.max(0, endLine - 1), character: Math.max(0, endCharacter - 1) },
    };
}
function toLocations(result) {
    return result.locations.map(item => ({
        uri: pathToFileURL(item.filePath).href,
        range: toRange(item.startLine, item.startCharacter, item.endLine, item.endCharacter),
    }));
}
function toDiagnosticSeverity(value) {
    const normalized = value.toLowerCase();
    if (normalized.includes('error'))
        return 1;
    if (normalized.includes('warning') || normalized.includes('warn'))
        return 2;
    if (normalized.includes('info'))
        return 3;
    return 4;
}
async function publishDiagnosticsFor(uri, notify) {
    const doc = docs.get(uri);
    if (!doc)
        return;
    try {
        const result = await unityRoslynAdapter.diagnostics({
            filePath: doc.path,
            targetPath: workspacePath,
        });
        const diagnostics = result.map(item => ({
            severity: toDiagnosticSeverity(item.severity),
            message: item.message,
            range: toRange(item.startLine, item.startCharacter, item.endLine, item.endCharacter),
            code: item.id,
            source: 'codely-unity-lsp',
        }));
        const payload = { uri, diagnostics, version: doc.version };
        notify(PublishDiagnosticsNotification.method, payload);
    }
    catch (error) {
        process.stderr.write(`[codely-unity-lsp-server] diagnostics failed: ${String(error)}\n`);
    }
}
function applyIncrementalChange(text, change) {
    if (!('range' in change) || !change.range)
        return change.text;
    const lines = text.split('\n');
    const startLine = Math.max(0, change.range.start.line);
    const endLine = Math.max(0, change.range.end.line);
    const startCharacter = Math.max(0, change.range.start.character);
    const endCharacter = Math.max(0, change.range.end.character);
    const before = lines.slice(0, startLine);
    const after = lines.slice(endLine + 1);
    const start = lines[startLine] ?? '';
    const end = lines[endLine] ?? '';
    const mergedLine = start.slice(0, startCharacter) + change.text + end.slice(endCharacter);
    return [...before, mergedLine, ...after].join('\n');
}
function extractDocumentSymbols(text) {
    const symbols = [];
    const lines = text.split('\n');
    const classRegex = /\b(class|struct|interface|enum)\s+([A-Za-z_][A-Za-z0-9_]*)/;
    const methodRegex = /\b(public|private|protected|internal)?\s*(static\s+)?([A-Za-z_][A-Za-z0-9_<>,\[\]\.?]*)\s+([A-Za-z_][A-Za-z0-9_]*)\s*\(/;
    for (let i = 0; i < lines.length; i += 1) {
        const line = lines[i] ?? '';
        const classMatch = classRegex.exec(line);
        if (classMatch) {
            symbols.push({
                name: classMatch[2] ?? 'Class',
                kind: 5,
                range: toRange(i + 1, 1, i + 1, Math.max(line.length, 1)),
                selectionRange: toRange(i + 1, (classMatch.index ?? 0) + 1, i + 1, (classMatch.index ?? 0) + 1 + classMatch[0].length),
            });
            continue;
        }
        const methodMatch = methodRegex.exec(line);
        if (methodMatch) {
            symbols.push({
                name: methodMatch[4] ?? 'Method',
                kind: 6,
                range: toRange(i + 1, 1, i + 1, Math.max(line.length, 1)),
                selectionRange: toRange(i + 1, (methodMatch.index ?? 0) + 1, i + 1, (methodMatch.index ?? 0) + 1 + methodMatch[0].length),
            });
        }
    }
    return symbols;
}
function toCallHierarchyKind(value) {
    const normalized = value.toLowerCase();
    if (normalized.includes('method'))
        return 6;
    if (normalized.includes('class'))
        return 5;
    if (normalized.includes('interface'))
        return 11;
    if (normalized.includes('property'))
        return 7;
    if (normalized.includes('field'))
        return 8;
    if (normalized.includes('constructor'))
        return 9;
    if (normalized.includes('event'))
        return 23;
    return 13;
}
function toCallHierarchyItem(item) {
    return {
        name: item.name,
        detail: item.detail ?? undefined,
        kind: toCallHierarchyKind(item.kind),
        uri: pathToFileURL(item.filePath).href,
        range: toRange(item.startLine, item.startCharacter, item.endLine, item.endCharacter),
        selectionRange: toRange(item.selectionStartLine, item.selectionStartCharacter, item.selectionEndLine, item.selectionEndCharacter),
    };
}
async function main() {
    const connection = createMessageConnection(new StreamMessageReader(process.stdin), new StreamMessageWriter(process.stdout));
    connection.onRequest(InitializeRequest.type.method, async (params) => {
        const bootstrap = await bootstrapLspRuntime();
        if (!bootstrap.ok) {
            throw new Error(bootstrap.message);
        }
        const folders = params.workspaceFolders ?? [];
        workspacePath = folders.length > 0 ? uriToPath(folders[0].uri) : undefined;
        try {
            await maybeRegenerateSlnOnStartup(workspacePath);
        }
        catch (error) {
            process.stderr.write(`[codely-unity-lsp-server] startup sln regeneration failed: ${String(error)}\n`);
        }
        await unityRoslynAdapter.ensureSolution(workspacePath);
        return {
            capabilities: {
                definitionProvider: true,
                referencesProvider: true,
                hoverProvider: true,
                implementationProvider: true,
                callHierarchyProvider: true,
                textDocumentSync: 2,
                documentSymbolProvider: true,
                workspaceSymbolProvider: true,
            },
            serverInfo: {
                name: 'codely-unity-lsp-server',
                version: '0.1.0',
            },
        };
    });
    connection.onNotification(InitializedNotification.type.method, () => { });
    connection.onNotification(DidOpenTextDocumentNotification.type.method, async (params) => {
        const path = uriToPath(params.textDocument.uri);
        docs.set(params.textDocument.uri, {
            uri: params.textDocument.uri,
            version: params.textDocument.version,
            text: params.textDocument.text,
            path,
        });
        await publishDiagnosticsFor(params.textDocument.uri, connection.sendNotification.bind(connection));
    });
    connection.onNotification(DidChangeTextDocumentNotification.type.method, async (params) => {
        const current = docs.get(params.textDocument.uri);
        if (!current)
            return;
        let text = current.text;
        for (const change of params.contentChanges) {
            text = applyIncrementalChange(text, change);
        }
        docs.set(params.textDocument.uri, { ...current, text, version: params.textDocument.version });
        await publishDiagnosticsFor(params.textDocument.uri, connection.sendNotification.bind(connection));
    });
    connection.onNotification(DidCloseTextDocumentNotification.type.method, params => {
        docs.delete(params.textDocument.uri);
        connection.sendNotification(PublishDiagnosticsNotification.method, {
            uri: params.textDocument.uri,
            diagnostics: [],
        });
    });
    connection.onRequest(DefinitionRequest.type.method, async (params) => {
        const result = await unityRoslynAdapter.navigate('definition', toWorkerPosition(params));
        return toLocations(result);
    });
    connection.onRequest(ReferencesRequest.type.method, async (params) => {
        const result = await unityRoslynAdapter.navigate('references', toWorkerPosition(params));
        return toLocations(result);
    });
    connection.onRequest(HoverRequest.type.method, async (params) => {
        const result = await unityRoslynAdapter.hover(toWorkerPosition(params));
        const parts = [
            result.symbol ? `symbol: ${result.symbol}` : '',
            result.kind ? `kind: ${result.kind}` : '',
            result.containingSymbol ? `container: ${result.containingSymbol}` : '',
            result.assemblyName ? `assembly: ${result.assemblyName}` : '',
        ].filter(Boolean);
        const hover = {
            contents: {
                kind: 'markdown',
                value: ['```text', ...parts, '```'].join('\n'),
            },
        };
        return hover;
    });
    connection.onRequest(ImplementationRequest.type.method, async (params) => {
        const result = await unityRoslynAdapter.navigate('implementations', toWorkerPosition(params));
        return toLocations(result);
    });
    connection.onRequest(CallHierarchyPrepareRequest.type.method, async (params) => {
        const result = await unityRoslynAdapter.prepareCallHierarchy(toWorkerPosition(params));
        return result.map(toCallHierarchyItem);
    });
    connection.onRequest(CallHierarchyIncomingCallsRequest.type.method, async (params) => {
        const workerParams = {
            filePath: uriToPath(params.item.uri),
            line: params.item.selectionRange.start.line + 1,
            character: params.item.selectionRange.start.character + 1,
            targetPath: workspacePath,
        };
        const result = await unityRoslynAdapter.incomingCalls(workerParams);
        return result.map(row => ({
            from: toCallHierarchyItem(row.from),
            fromRanges: row.fromRanges.map(loc => toRange(loc.startLine, loc.startCharacter, loc.endLine, loc.endCharacter)),
        }));
    });
    connection.onRequest(CallHierarchyOutgoingCallsRequest.type.method, async (params) => {
        const workerParams = {
            filePath: uriToPath(params.item.uri),
            line: params.item.selectionRange.start.line + 1,
            character: params.item.selectionRange.start.character + 1,
            targetPath: workspacePath,
        };
        const result = await unityRoslynAdapter.outgoingCalls(workerParams);
        return result.map(row => ({
            to: toCallHierarchyItem(row.to),
            fromRanges: row.fromRanges.map(loc => toRange(loc.startLine, loc.startCharacter, loc.endLine, loc.endCharacter)),
        }));
    });
    connection.onRequest(DocumentSymbolRequest.type.method, async (params) => {
        const doc = docs.get(params.textDocument.uri);
        if (!doc)
            return [];
        return extractDocumentSymbols(doc.text);
    });
    connection.onRequest(WorkspaceSymbolRequest.type.method, async (params) => {
        const query = (params.query ?? '').trim().toLowerCase();
        const symbols = [];
        for (const doc of docs.values()) {
            const docSymbols = extractDocumentSymbols(doc.text);
            for (const symbol of docSymbols) {
                if (query && !symbol.name.toLowerCase().includes(query))
                    continue;
                symbols.push({
                    name: symbol.name,
                    kind: symbol.kind,
                    location: { uri: doc.uri, range: symbol.selectionRange },
                    containerName: undefined,
                });
            }
        }
        return symbols;
    });
    connection.onRequest(ShutdownRequest.type.method, async () => {
        hasShutdown = true;
        await workerClient.dispose();
        return null;
    });
    connection.onNotification('exit', () => {
        process.exit(hasShutdown ? 0 : 1);
    });
    connection.listen();
}
main().catch(async (error) => {
    process.stderr.write(`[codely-unity-lsp-server] fatal: ${String(error)}\n`);
    await workerClient.dispose();
    process.exit(1);
});
