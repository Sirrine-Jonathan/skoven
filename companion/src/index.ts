/**
 * Skoven Companion App — entry point
 *
 * Usage:
 *   cp .env.example .env
 *   # fill in your API key and project path
 *   npm install
 *   npm run dev        # development (ts-node)
 *   npm run build && npm start   # production
 */

import dotenv from "dotenv";
import path from "path";

// Load .env from companion/ directory
dotenv.config({ path: path.resolve(process.cwd(), ".env") });

import { startServer } from "./server/index.js";

console.log("╔═══════════════════════════════════════════╗");
console.log("║        Skoven Companion App            ║");
console.log("║  Voice-driven AI agent workspace      ║");
console.log("╚═══════════════════════════════════════════╝");
console.log();

startServer();
