import { readFile } from 'node:fs/promises';
import { existsSync } from 'node:fs';
import { Socket } from 'node:net';
import path from 'node:path';
const DEFAULT_HOST = 'localhost';
const DEFAULT_PORT = 25916;
const CONNECT_TIMEOUT_MS = 2000;
const HANDSHAKE_TIMEOUT_MS = 2000;
const RESPONSE_TIMEOUT_MS = 10000;
const GENERATE_SOLUTION_SCRIPT = `
using System;
using System.IO;
using System.Linq;
using System.Reflection;
using UnityEditor;
using UnityEngine;

AssetDatabase.Refresh();
var projectRoot = Directory.GetParent(Application.dataPath)?.FullName;
if (string.IsNullOrEmpty(projectRoot))
{
    throw new Exception("Cannot resolve project root from Application.dataPath.");
}

var beforeSln = Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly);
var beforeCsproj = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly);
Debug.Log($"[codely-unity-lsp-server] Before generation: sln={beforeSln.Length}, csproj={beforeCsproj.Length}");

var loadedAssemblies = AppDomain.CurrentDomain.GetAssemblies();
var vsAssembly = loadedAssemblies.FirstOrDefault(a =>
    a.GetName().Name == "Microsoft.Unity.VisualStudio.Editor" ||
    a.GetType("Microsoft.Unity.VisualStudio.Editor.ProjectGeneration", false) != null ||
    a.GetType("Microsoft.Unity.VisualStudio.Editor.LegacyStyleProjectGeneration", false) != null);

if (vsAssembly == null)
{
    throw new Exception("Cannot find Microsoft.Unity.VisualStudio.Editor assembly. Ensure com.unity.ide.visualstudio is installed and compiled.");
}

var generatorType =
    vsAssembly.GetType("Microsoft.Unity.VisualStudio.Editor.LegacyStyleProjectGeneration", false) ??
    vsAssembly.GetType("Microsoft.Unity.VisualStudio.Editor.ProjectGeneration", false);

if (generatorType == null)
{
    throw new Exception("Cannot find LegacyStyleProjectGeneration or ProjectGeneration type.");
}

var generator = Activator.CreateInstance(generatorType, nonPublic: true);
if (generator == null)
{
    throw new Exception("Generator instance is null.");
}

var syncMethod = generatorType.GetMethod("Sync", BindingFlags.Instance | BindingFlags.Public);
if (syncMethod == null)
{
    throw new Exception("Sync() not found on Unity project generator.");
}

syncMethod.Invoke(generator, null);
AssetDatabase.Refresh();

var afterSln = Directory.GetFiles(projectRoot, "*.sln", SearchOption.TopDirectoryOnly);
var afterCsproj = Directory.GetFiles(projectRoot, "*.csproj", SearchOption.TopDirectoryOnly);
Debug.Log($"[codely-unity-lsp-server] After generation: sln={afterSln.Length}, csproj={afterCsproj.Length}");
`;
async function readUnityServerConfig(projectRoot) {
    const configPath = path.join(projectRoot, '.com-unity-codely.json');
    if (!existsSync(configPath)) {
        return null;
    }
    try {
        const raw = await readFile(configPath, 'utf8');
        return JSON.parse(raw);
    }
    catch {
        return null;
    }
}
function validPort(value) {
    return typeof value === 'number' && Number.isInteger(value) && value > 0 && value <= 65535 ? value : null;
}
function readFramedMessage(socket, timeoutMs) {
    return new Promise((resolve, reject) => {
        let buffer = Buffer.alloc(0);
        let expectedLength = null;
        const cleanup = () => {
            clearTimeout(timer);
            socket.off('data', onData);
            socket.off('error', onError);
            socket.off('close', onClose);
            socket.off('end', onClose);
        };
        const finish = (payload) => {
            cleanup();
            resolve(payload);
        };
        const fail = (error) => {
            cleanup();
            reject(error);
        };
        const onData = (chunk) => {
            buffer = Buffer.concat([buffer, chunk]);
            if (expectedLength === null && buffer.length >= 8) {
                expectedLength = Number(buffer.readBigUInt64BE(0));
            }
            if (expectedLength !== null && buffer.length >= 8 + expectedLength) {
                finish(buffer.subarray(8, 8 + expectedLength));
            }
        };
        const onError = (error) => fail(error);
        const onClose = () => fail(new Error('Connection closed before receiving full response'));
        const timer = setTimeout(() => {
            fail(new Error(`Timed out waiting for Unity TCP response after ${timeoutMs}ms`));
        }, timeoutMs);
        socket.on('data', onData);
        socket.once('error', onError);
        socket.once('close', onClose);
        socket.once('end', onClose);
    });
}
async function connectUnityTcp(host, port) {
    const socket = new Socket();
    socket.setNoDelay(true);
    await new Promise((resolve, reject) => {
        const timer = setTimeout(() => {
            reject(new Error(`Connection timeout to ${host}:${port}`));
        }, CONNECT_TIMEOUT_MS);
        socket.connect(port, host, () => {
            clearTimeout(timer);
            resolve();
        });
        socket.once('error', error => {
            clearTimeout(timer);
            reject(error);
        });
    });
    socket.setTimeout(RESPONSE_TIMEOUT_MS);
    const handshake = (await readFramedHandshake(socket)).toString('ascii').trim();
    if (!handshake.includes('WELCOME UNITY-TCP') || !handshake.includes('FRAMING=1')) {
        socket.destroy();
        throw new Error(`Unexpected Unity TCP handshake from ${host}:${port}: ${handshake}`);
    }
    return socket;
}
function readFramedHandshake(socket) {
    return new Promise((resolve, reject) => {
        let buffer = Buffer.alloc(0);
        const cleanup = () => {
            clearTimeout(timer);
            socket.off('data', onData);
            socket.off('error', onError);
            socket.off('close', onClose);
            socket.off('end', onClose);
        };
        const onData = (chunk) => {
            buffer = Buffer.concat([buffer, chunk]);
            if (buffer.includes(0x0a) || buffer.length >= 512) {
                cleanup();
                resolve(buffer);
            }
        };
        const onError = (error) => {
            cleanup();
            reject(error);
        };
        const onClose = () => {
            cleanup();
            reject(new Error('Unity TCP connection closed during handshake'));
        };
        const timer = setTimeout(() => {
            cleanup();
            reject(new Error(`Timed out waiting for Unity TCP handshake after ${HANDSHAKE_TIMEOUT_MS}ms`));
        }, HANDSHAKE_TIMEOUT_MS);
        socket.on('data', onData);
        socket.once('error', onError);
        socket.once('close', onClose);
        socket.once('end', onClose);
    });
}
async function sendUnityCommand(host, port, command) {
    const socket = await connectUnityTcp(host, port);
    try {
        const payload = Buffer.from(JSON.stringify(command), 'utf8');
        const header = Buffer.allocUnsafe(8);
        header.writeBigUInt64BE(BigInt(payload.length), 0);
        socket.write(header);
        socket.write(payload);
        const responseData = await readFramedMessage(socket, RESPONSE_TIMEOUT_MS);
        const response = JSON.parse(responseData.toString('utf8'));
        const isError = response.success === false || response.status === 'error';
        if (isError) {
            throw new Error(response.error || response.message || 'Unity TCP command failed');
        }
        return response.result ?? response;
    }
    finally {
        socket.destroy();
    }
}
export async function generateSolutionViaConnectedEditor(projectRoot) {
    const config = await readUnityServerConfig(projectRoot);
    const configuredPort = validPort(config?.unity_port);
    const host = typeof config?.unity_host === 'string' && config.unity_host.trim()
        ? config.unity_host.trim()
        : DEFAULT_HOST;
    const candidatePorts = configuredPort === null ? [DEFAULT_PORT] : [configuredPort, DEFAULT_PORT];
    let lastError = 'Unity TCP connection unavailable';
    for (const port of [...new Set(candidatePorts)]) {
        try {
            const response = await sendUnityCommand(host, port, {
                type: 'execute_csharp_script',
                params: {
                    script: GENERATE_SOLUTION_SCRIPT,
                    capture_logs: true,
                },
            });
            return { ok: true, host, port, response };
        }
        catch (error) {
            lastError = error instanceof Error ? error.message : String(error);
        }
    }
    return {
        ok: false,
        host,
        port: configuredPort ?? DEFAULT_PORT,
        error: lastError,
    };
}
