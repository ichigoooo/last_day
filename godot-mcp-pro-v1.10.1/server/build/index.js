#!/usr/bin/env node
import { McpServer } from "@modelcontextprotocol/sdk/server/mcp.js";
import { StdioServerTransport } from "@modelcontextprotocol/sdk/server/stdio.js";
import { StreamableHTTPServerTransport } from "@modelcontextprotocol/sdk/server/streamableHttp.js";
import { createServer } from "node:http";
import { randomUUID } from "node:crypto";
import { GodotConnection } from "./godot-connection.js";
import { registerProjectTools } from "./tools/project-tools.js";
import { registerSceneTools } from "./tools/scene-tools.js";
import { registerNodeTools } from "./tools/node-tools.js";
import { registerScriptTools } from "./tools/script-tools.js";
import { registerEditorTools } from "./tools/editor-tools.js";
import { registerInputTools } from "./tools/input-tools.js";
import { registerRuntimeTools } from "./tools/runtime-tools.js";
import { registerAnimationTools } from "./tools/animation-tools.js";
import { registerTilemapTools } from "./tools/tilemap-tools.js";
import { registerThemeTools } from "./tools/theme-tools.js";
import { registerProfilingTools } from "./tools/profiling-tools.js";
import { registerBatchTools } from "./tools/batch-tools.js";
import { registerShaderTools } from "./tools/shader-tools.js";
import { registerExportTools } from "./tools/export-tools.js";
import { registerResourceTools } from "./tools/resource-tools.js";
import { registerAnimationTreeTools } from "./tools/animation-tree-tools.js";
import { registerPhysicsTools } from "./tools/physics-tools.js";
import { registerScene3DTools } from "./tools/scene-3d-tools.js";
import { registerParticleTools } from "./tools/particle-tools.js";
import { registerNavigationTools } from "./tools/navigation-tools.js";
import { registerAudioTools } from "./tools/audio-tools.js";
import { registerTestTools } from "./tools/test-tools.js";
import { registerAnalysisTools } from "./tools/analysis-tools.js";
import { registerInputMapTools } from "./tools/input-map-tools.js";
import { MINIMAL_TOOLS, createFilteredServer } from "./utils/tool-filter.js";
const MINIMAL_MODE = process.argv.includes("--minimal");
const LITE_MODE = process.argv.includes("--lite") || MINIMAL_MODE;
const HTTP_MODE = process.argv.includes("--http");
const HTTP_PORT = parseInt(process.argv.find((_, i, a) => a[i - 1] === "--http-port") ||
    process.env.GODOT_MCP_HTTP_PORT ||
    "8001");
const explicitPort = process.env.GODOT_MCP_PORT;
const godot = new GodotConnection(parseInt(explicitPort || "6505"), !!explicitPort);
const serverName = MINIMAL_MODE
    ? "godot-mcp-pro-minimal"
    : LITE_MODE
        ? "godot-mcp-pro-lite"
        : "godot-mcp-pro";
const server = new McpServer({
    name: serverName,
    version: "1.10.1",
});
// In minimal mode, wrap the server to filter tool registrations
const toolServer = MINIMAL_MODE ? createFilteredServer(server, MINIMAL_TOOLS) : server;
// Core tools (always registered)
registerProjectTools(toolServer, godot);
registerSceneTools(toolServer, godot);
registerNodeTools(toolServer, godot);
registerScriptTools(toolServer, godot);
registerEditorTools(toolServer, godot);
registerInputTools(toolServer, godot);
registerRuntimeTools(toolServer, godot);
registerInputMapTools(toolServer, godot);
// Extended tools (Full mode only)
if (!LITE_MODE) {
    registerAnimationTools(server, godot);
    registerAnimationTreeTools(server, godot);
    registerAudioTools(server, godot);
    registerBatchTools(server, godot);
    registerExportTools(server, godot);
    registerNavigationTools(server, godot);
    registerParticleTools(server, godot);
    registerPhysicsTools(server, godot);
    registerProfilingTools(server, godot);
    registerResourceTools(server, godot);
    registerScene3DTools(server, godot);
    registerShaderTools(server, godot);
    registerTestTools(server, godot);
    registerThemeTools(server, godot);
    registerTilemapTools(server, godot);
    registerAnalysisTools(server, godot);
}
// Start server
async function main() {
    // WebSocket listener must start before stdio MCP; duplicate Cursor MCP sessions each spawn
    // a Node process — only one can bind GODOT_MCP_PORT; others must exit instead of staying "alive" without WS.
    try {
        await godot.connect();
    }
    catch (err) {
        const msg = err instanceof Error ? err.message : String(err);
        const code = err && typeof err === "object" && "code" in err
            ? err.code
            : undefined;
        console.error(`[MCP] WebSocket server failed: ${msg}`);
        if (explicitPort &&
            (code === "EADDRINUSE" ||
                /EADDRINUSE|address already in use/i.test(msg))) {
            console.error("[MCP] Port in use: another godot-mcp-pro instance is already running (e.g. multiple Cursor MCP sessions). Keep a single session or run: pkill -f 'godot-mcp-pro-v1.10.1/server/build/index.js'");
            process.exit(1);
        }
        throw err;
    }
    if (HTTP_MODE) {
        // Streamable HTTP transport — clients connect via http://host:port/mcp
        const transport = new StreamableHTTPServerTransport({
            sessionIdGenerator: () => randomUUID(),
        });
        await server.connect(transport);
        const httpServer = createServer(async (req, res) => {
            const url = new URL(req.url || "/", `http://${req.headers.host}`);
            if (url.pathname === "/mcp") {
                await transport.handleRequest(req, res);
            }
            else {
                res.writeHead(404).end("Not Found");
            }
        });
        httpServer.listen(HTTP_PORT, () => {
            const mode = MINIMAL_MODE ? "MINIMAL " : LITE_MODE ? "LITE " : "";
            console.error(`[MCP] Godot MCP Pro ${mode}started (HTTP transport on http://127.0.0.1:${HTTP_PORT}/mcp)`);
        });
    }
    else {
        // Default stdio transport
        const transport = new StdioServerTransport();
        await server.connect(transport);
        const modeLabel = MINIMAL_MODE
            ? "[MCP] Godot MCP Pro MINIMAL started (35 tools, stdio transport)"
            : LITE_MODE
                ? "[MCP] Godot MCP Pro LITE started (80 tools, stdio transport)"
                : "[MCP] Godot MCP Pro started (stdio transport)";
        console.error(modeLabel);
    }
}
main().catch((err) => {
    console.error("[MCP] Fatal error:", err);
    process.exit(1);
});
//# sourceMappingURL=index.js.map